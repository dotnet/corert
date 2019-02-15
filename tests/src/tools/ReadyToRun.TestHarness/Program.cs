// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using System.IO;
using System.Linq;
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
        private const int NormalTestTimeout = 2 * 60 * 1000;

        // Timeout under GC stress in milliseconds
        private const int GCStressTestTimeout = 30 * 60 * 1000;

        // Error code returned when events get lost. Use this to re-run the test a few times.
        private const int StatusTestErrorEventsLost = -101;
        private const int StatusTestErrorMethodsJitted = -102;
        private const int StatusTestErrorTimeOut = -103;
        private const int StatusTestErrorBadInput = -104;
        private const int StatusTestErrorNoAssemblyLoadEvents = -105;
        private const int StatusTestPassed = 100;

        private static bool _help;
        private static string _coreRunExePath;
        private static string _testExe;
        private static IReadOnlyList<string> _referenceFilenames = Array.Empty<string>();
        private static IReadOnlyList<string> _referenceFolders = Array.Empty<string>();
        private static string _whitelistFilename;
        private static IReadOnlyList<string> _testargs = Array.Empty<string>();
        private static bool _noEtl;

        static void ShowUsage()
        {
            Console.WriteLine("dotnet ReadyToRun.TestHarness --corerun <PathToCoreRun> --in <PathToTestBinary> --ref [ReferencedBinaries] --whitelist [MethodWhiteListFile] --testargs [TestArgs] --include [FoldersContainingAssemblies]");
        }

        private static ArgumentSyntax ParseCommandLine(string[] args)
        {
            ArgumentSyntax argSyntax = ArgumentSyntax.Parse(args, syntax =>
            {
                syntax.ApplicationName = "ReadyToRun.TestHarness";
                syntax.HandleHelp = false;
                syntax.HandleErrors = true;
                syntax.HandleResponseFiles = true;

                syntax.DefineOption("h|help", ref _help, "Help message for TestHarness");
                syntax.DefineOption("c|corerun", ref _coreRunExePath, "Path to CoreRun");
                syntax.DefineOption("i|in", ref _testExe, "Path to test exe");
                syntax.DefineOptionList("r|ref", ref _referenceFilenames, "Paths to referenced assemblies");
                syntax.DefineOptionList("include", ref _referenceFolders, "Folders containing assemblies to monitor");
                syntax.DefineOption("w|whitelist", ref _whitelistFilename, "Path to method whitelist file");
                syntax.DefineOptionList("testargs", ref _testargs, "Args to pass into test");
                syntax.DefineOption("noetl", ref _noEtl, "Run the test without ETL enabled");
            });
            return argSyntax;
        }

        static int Main(string[] args)
        {
            int exitCode = StatusTestErrorBadInput;

            ArgumentSyntax syntax = ParseCommandLine(args);

            if (_help)
            {
                ShowUsage();
                return 0;
            }

            if (!File.Exists(_coreRunExePath))
            {
                Console.WriteLine($"Error: {_coreRunExePath} is an invalid path.");
                ShowUsage();
                return exitCode;
            }

            if (!File.Exists(_testExe))
            {
                Console.WriteLine($"Error: {_testExe} is an invalid path.");
                ShowUsage();
                return exitCode;
            }

            string passThroughArguments = "";
            if (_testargs.Count > 0)
            {
                passThroughArguments = string.Join(' ', _testargs);
            }

            var testModules = new HashSet<string>();
            var testFolders = new HashSet<string>();
            testModules.Add(_testExe.ToLower());
            foreach (string reference in _referenceFilenames)
            {
                // CoreCLR generates ETW events with all lower case native image files that break our string comparison.
                testModules.Add(reference.ToLower());
            }

            foreach (string reference in _referenceFolders)
            {
                string absolutePath = reference.ToAbsoluteDirectoryPath();

                if (!Directory.Exists(reference))
                {
                    Console.WriteLine($"Error: {reference} does not exist.");
                    ShowUsage();
                    return exitCode;
                }
                testFolders.Add(reference.ToLower());
            }

            if (_noEtl)
            {
                RunTest(null, null, passThroughArguments, out exitCode);
            }
            else
            {
                using (var session = new TraceEventSession("ReadyToRunTestSession"))
                {
                    var r2rMethodFilter = new ReadyToRunJittedMethods(session, testModules, testFolders);
                    session.EnableProvider(ClrTraceEventParser.ProviderGuid, TraceEventLevel.Verbose, (ulong)(ClrTraceEventParser.Keywords.Jit | ClrTraceEventParser.Keywords.Loader));

                    Task.Run(() => RunTest(session, r2rMethodFilter, passThroughArguments, out exitCode));

                    // Block, processing callbacks for events we subscribed to
                    session.Source.Process();

                    Console.WriteLine("Test execution " + (exitCode == StatusTestPassed ? "PASSED" : "FAILED"));
                    int analysisResult = AnalyzeResults(r2rMethodFilter, _whitelistFilename);

                    Console.WriteLine("Test jitted method analysis " + (analysisResult == StatusTestPassed ? "PASSED" : "FAILED"));

                    // If the test passed, return the Jitted method analysis result
                    // If the test failed, return its execution exit code
                    exitCode = exitCode == StatusTestPassed ? analysisResult : exitCode;
                }
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

            if (jittedMethods.AssembliesWithEventsCount == 0)
            {
                Console.WriteLine($"Error: No test assemblies were loaded by the runtime. This is likely a test harness bug / ETW issue");
                return StatusTestErrorNoAssemblyLoadEvents;
            }

            Console.WriteLine("");
            Console.WriteLine("*** Jitted method analysis ***");

            Console.WriteLine("");
            Console.WriteLine("White listed methods that were jitted (these don't count as test failures):");
            int whiteListedJittedMethodCount = 0;

            List<string> jittedMethodNames = new List<string>();
            foreach (KeyValuePair<string, HashSet<string>> jittedMethod in jittedMethods.JittedMethods.OrderBy(kvp => kvp.Key))
            {
                bool wasWhiteListed = whiteListedMethods.Contains(jittedMethod.Key);
                if (!wasWhiteListed)
                {
                    // Check assembly-qualified whitelist clauses
                    wasWhiteListed = true;
                    foreach (string assemblyName in jittedMethod.Value)
                    {
                        string fullName = GetFullModuleName(assemblyName, jittedMethod.Key);
                        if (!whiteListedMethods.Contains(fullName))
                        {
                            jittedMethodNames.Add(fullName);
                            wasWhiteListed = false;
                        }
                    }
                }
                if (wasWhiteListed)
                {
                    Console.WriteLine(jittedMethod.Key);
                    whiteListedJittedMethodCount++;
                }
            }

            if (whiteListedJittedMethodCount == 0)
            {
                Console.WriteLine("-None-");
            }

            Console.WriteLine("");
            Console.WriteLine("Methods that were jitted without a whitelist entry (test failure):");
            foreach (string jittedMethod in jittedMethodNames)
            {
                Console.WriteLine(jittedMethod);
            }
            if (jittedMethodNames.Count == 0)
            {
                Console.WriteLine("-None-");
            }
            else
            {
                Console.WriteLine($"Error: {jittedMethodNames.Count} methods were jitted.");
                return StatusTestErrorMethodsJitted;
            }

            return StatusTestPassed;
        }

        private static string GetFullModuleName(string moduleName, string methodName)
        {
            return $"[{moduleName}]{methodName}";
        }

        private static void RunTest(TraceEventSession session, ReadyToRunJittedMethods r2rMethodFilter, string testArguments, out int exitCode)
        {
            exitCode = -100;

            try
            {
                using (var process = new Process())
                {
                    process.StartInfo.FileName = _coreRunExePath;
                    process.StartInfo.Arguments = _testExe + " " + testArguments;
                    process.StartInfo.UseShellExecute = false;

                    process.Start();

                    if (r2rMethodFilter != null)
                    {
                        r2rMethodFilter.SetProcessId(process.Id);
                    }

                    process.OutputDataReceived += delegate (object sender, DataReceivedEventArgs args)
                    {
                        Console.WriteLine(args.Data);
                    };

                    process.ErrorDataReceived += delegate (object sender, DataReceivedEventArgs args)
                    {
                        Console.WriteLine(args.Data);
                    };

                    int timeoutToUse = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("__GCSTRESSLEVEL")) ? NormalTestTimeout : GCStressTestTimeout;

                    if (process.WaitForExit(timeoutToUse))
                    {
                        exitCode = process.ExitCode;
                    }
                    else
                    {
                        // Do our best to kill it if there's a timeout, but if it fails, not much we can do
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                        }

                        Console.WriteLine("Test execution timed out after {0} seconds.", timeoutToUse / 1000);
                        exitCode = StatusTestErrorTimeOut;
                    }

                    Console.WriteLine($"Test exited with code {process.ExitCode}");

                    if (session != null && session.EventsLost > 0)
                    {
                        exitCode = StatusTestErrorEventsLost;
                        Console.WriteLine($"Error - {session.EventsLost} got lost in the nether.");
                        return;
                    }
                }
            }
            finally
            {
                // Stop ETL collection on the main thread
                session?.Stop();
            }
        }
    }
}
