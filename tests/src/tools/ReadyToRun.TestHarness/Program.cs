// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ReadyToRun.TestHarness
{
    //
    // Test scenarios this harness will be useful for:
    // * Given a fully ready-to-run compiled test, validate no methods in the test module are Jitted
    // * Given a change to a dependent class, determine the set of r2r methods that are now 
    //    invalid and fall back to Jit.
    //
    class Program
    {
        // Default timeout in milliseconds
        private const int DefaultTestTimeOut = 60000;

        // Error code returned when events get lost. Use this to re-run the test a few times.
        private const int StatusTestErrorEventsLost = -101;
        private const int StatusTestErrorMethodsJitted = -102;
        private const int StatusTestErrorTimeOut = -103;
        private const int StatusTestErrorBadInput = -104;
        private const int StatusTestPassed = 100;

        static void ShowUsage()
        {
            Console.WriteLine("dotnet ReadyToRun.Harness <PathToCoreRun> <PathToTestBinary> [MethodWhiteListFile]");
        }

        static int Main(string[] args)
        {
            int exitCode = StatusTestErrorBadInput;
            string whiteListFile = null;

            if (args.Length < 2)
            {
                Console.WriteLine("Error: Missing required arguments.");
                ShowUsage();
                return exitCode;
            }

            if (!File.Exists(args[0]))
            {
                Console.WriteLine($"Error: {args[0]} is an invalid path.");
                ShowUsage();
                return exitCode;
            }

            if (!File.Exists(args[1]))
            {
                Console.WriteLine($"Error: {args[1]} is an invalid path.");
                ShowUsage();
                return exitCode;
            }

            int passThroughIndex = -1;
            string passThroughArguments = "";
            if (args.Length >= 3)
            {
                if (args[2] == "/testargs")
                {
                    passThroughIndex = 2;
                } else
                {
                    if (!File.Exists(args[2]))
                    {
                        Console.WriteLine($"Error: {args[2]} is an invalid path.");
                        ShowUsage();
                        return exitCode;
                    }
                    whiteListFile = args[2];

                    if (args.Length >= 4 && args[3] == "/testargs")
                    {
                        passThroughIndex = 3;
                    }
                }
                
                if (passThroughIndex > -1 && (args.Length - passThroughIndex - 1) > 0)
                {
                    passThroughArguments = string.Join(' ', args, passThroughIndex + 1, args.Length - passThroughIndex - 1);
                }
            }

            string coreRunExePath = args[0];
            string testExe = args[1];

            // TODO: CoreCLR test bed has tests with multiple assemblies - we'll need to add them here when we support that
            var testModules = new HashSet<string>();
            testModules.Add(testExe);

            using (var session = new TraceEventSession("ReadyToRunTestSession"))
            {
                var r2rMethodFilter = new ReadyToRunJittedMethods(session, testModules);
                session.EnableProvider(ClrTraceEventParser.ProviderGuid, TraceEventLevel.Verbose, (ulong)(ClrTraceEventParser.Keywords.Jit | ClrTraceEventParser.Keywords.Loader));
                
                exitCode = RunTestWrapper(session, coreRunExePath, testExe, passThroughArguments).Result;

                Console.WriteLine("Test execution " + (exitCode == StatusTestPassed ? "PASSED" : "FAILED"));
                int analysisResult = AnalyzeResults(r2rMethodFilter, whiteListFile);

                Console.WriteLine("Test jitted method analysis " + (analysisResult == StatusTestPassed ? "PASSED" : "FAILED"));

                // If the test passed, return the Jitted method analysis result
                // If the test failed, return its execution exit code
                exitCode = exitCode == StatusTestPassed ? analysisResult : exitCode;
            }

            Console.WriteLine($"Final test result: {exitCode}");
            return exitCode;
        }

        private static int AnalyzeResults(ReadyToRunJittedMethods jittedMethods, string whiteListFilePath = null)
        {
            var whiteListedMethods = new HashSet<string>();
            if (!string.IsNullOrEmpty(whiteListFilePath))
            {
                using (TextReader tr = File.OpenText(whiteListFilePath))
                {
                    string line = "";
                    while ((line = tr.ReadLine()) != null)
                    {
                        whiteListedMethods.Add(line);
                    }
                }
            }

            Console.WriteLine("");
            Console.WriteLine("*** Jitted method analysis ***");

            Console.WriteLine("");
            Console.WriteLine("White listed methods that were jitted (these don't count as test failures):");
            int whiteListedJittedMethodCount = 0;
            foreach (var jittedMethod in jittedMethods.JittedMethods)
            {
                if (whiteListedMethods.Contains(jittedMethod.methodName))
                {
                    Console.WriteLine(jittedMethod.methodName);
                    ++whiteListedJittedMethodCount;
                }
            }

            Console.WriteLine("");
            Console.WriteLine("Methods that were jitted without a whitelist entry (test failure):");
            int jittedMethodCount = 0;
            foreach (var jittedMethod in jittedMethods.JittedMethods)
            {
                if (!whiteListedMethods.Contains(jittedMethod.methodName))
                {
                    Console.WriteLine(jittedMethod.methodName);
                    ++jittedMethodCount;
                }
            }

            if (jittedMethodCount > 0)
            {
                Console.WriteLine($"Error: {jittedMethodCount} methods were jitted.");
                return StatusTestErrorMethodsJitted;
            }

            return StatusTestPassed;
        }

        private static async Task<int> RunTestWrapper(TraceEventSession session, string coreRunExePath, string testExe, string passThroughArguments)
        {
            return await Task.Run(() => RunTest(session, coreRunExePath, testExe, passThroughArguments));
        }

        private static int RunTest(TraceEventSession session, string coreRunPath, string testExecutable, string testArguments)
        {
            int exitCode = -100;

            try
            {
                using (var process = new Process())
                {
                    process.StartInfo.FileName = coreRunPath;
                    process.StartInfo.Arguments = testExecutable + " " + testArguments;
                    process.StartInfo.UseShellExecute = false;
                    
                    process.Start();

                    process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs args)
                    {
                        Console.WriteLine(args.Data);
                    };

                    process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs args)
                    {
                        Console.WriteLine(args.Data);
                    };

                    if (process.WaitForExit(DefaultTestTimeOut))
                    {
                        exitCode = process.ExitCode;
                    }
                    else
                    {
                        Console.WriteLine("Test execution timed out.");
                        exitCode = StatusTestErrorTimeOut;
                    }

                    Console.WriteLine($"Test exited with code {process.ExitCode}");
                    
                    if (session.EventsLost > 0)
                    {
                        Console.WriteLine($"Error - {session.EventsLost} got lost in the nether.");
                        return StatusTestErrorEventsLost;
                    }
                }
            }
            finally
            {
                // Stop ETL collection on the main thread
                session.Stop();
            }
            
            return exitCode;
        }
    }
}
