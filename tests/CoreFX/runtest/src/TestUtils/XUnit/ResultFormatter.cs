// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/// <summary>
/// This helper class reads xunit xml log files and prints the tallied test results to the console
/// </summary>
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json;

namespace CoreFX.TestUtils.XUnit
{
    public static class ResultFormatter
    {
        private static string logDir;
        private static string logPattern;

        public static void Main(string[] args)
        {
            ArgumentSyntax syntax = ParseCommandLine(args);
            IEnumerable<string> logFiles = DiscoverLogs(logDir, logPattern);
            PrintTotals(logFiles);
        }

        private static void PrintTotals(IEnumerable<string> logFiles)
        {
            int total = 0;
            int passed = 0;
            int failed = 0;
            int skipped = 0;
            ulong timeElapsed = 0;

            foreach(string logFile in logFiles)
            {
                // XMLReader escapes the character sequence \\.. as just a single backslash \ - Is this intended behavior? 
                using (XmlReader reader = XmlReader.Create(logFile.Replace(@"\\..", @"\..")))
                {
                    reader.MoveToContent();
                    reader.ReadToDescendant("collection");
                    do
                    {
                        // Get total tests in current element
                        string totalAttr = reader.GetAttribute("total");
                        int currentTotal;
                        Int32.TryParse(totalAttr, out currentTotal);
                        total += currentTotal;

                        // Get passed tests 
                        string passedAttr = reader.GetAttribute("passed");
                        int currentPassed;
                        Int32.TryParse(passedAttr, out currentPassed);
                        passed += currentPassed;

                        // Get failed tests
                        string failedAttr = reader.GetAttribute("failed");
                        int currentFailed;
                        Int32.TryParse(failedAttr, out currentFailed);
                        failed += currentFailed;

                        // Get skipped tests
                        string skippedAttr = reader.GetAttribute("skipped");
                        int currentSkipped;
                        Int32.TryParse(skippedAttr, out currentSkipped);
                        skipped += currentSkipped;

                        // Get time elapsed
                        string timeAttr = reader.GetAttribute("time");
                        ulong currentTime;
                        UInt64.TryParse(timeAttr, out currentTime);
                        timeElapsed += currentTime;

                    } while (reader.ReadToNextSibling("collection"));

                }
            }

            Console.WriteLine("=== CoreFX TEST EXECUTION SUMMARY ===: ");
            Console.WriteLine(String.Format("Total: {0}, Passed: {1}, Failed: {2}, Skipped: {3}", total, passed, failed, timeElapsed));
            Console.WriteLine("Detailed logs written to: " + logDir);

        }

        private static ArgumentSyntax ParseCommandLine(string[] args)
        {

            ArgumentSyntax argSyntax = ArgumentSyntax.Parse(args, syntax =>
            {
                syntax.DefineOption("log|logDirectory|logDir", ref logDir, "Path to directory of xml test results");
                syntax.DefineOption("pattern|p", ref logPattern, "Pattern of XUnit log filenames for which to search");
            });

            return argSyntax;
        }

        public static IEnumerable<string> DiscoverLogs(string logDirectory, string logPattern)
        {
            Debug.Assert(Directory.Exists(logDirectory));
            Console.WriteLine(logDirectory);
            var logFiles = Directory.EnumerateFiles(logDirectory, logPattern, SearchOption.AllDirectories);
            
            return logFiles;
        }


    }
}
