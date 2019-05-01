﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ReadyToRun.SuperIlc
{
    class CompileSubtreeCommand
    {
        public static int CompileSubtree(BuildOptions options)
        {
            if (options.InputDirectory == null)
            {
                Console.WriteLine("--input-directory is a required argument.");
                return 1;
            }

            if (options.OutputDirectory == null)
            {
                options.OutputDirectory = options.InputDirectory;
            }

            if (options.OutputDirectory.IsParentOf(options.InputDirectory))
            {
                Console.WriteLine("Error: Input and output folders must be distinct, and the output directory (which gets deleted) better not be a parent of the input directory.");
                return 1;
            }

            IEnumerable<CompilerRunner> runners = options.CompilerRunners(isFramework: false);

            PathExtensions.DeleteOutputFolders(options.OutputDirectory.ToString(), recursive: true);

            string[] directories = new string[] { options.InputDirectory.FullName }
                .Concat(
                    options.InputDirectory
                        .EnumerateDirectories("*", SearchOption.AllDirectories)
                        .Select(dirInfo => dirInfo.FullName)
                        .Where(path => !Path.GetExtension(path).Equals(".out", StringComparison.OrdinalIgnoreCase)))
                .ToArray();

            List<BuildFolder> folders = new List<BuildFolder>();
            int relativePathOffset = directories[0].Length + 1;
            int folderCount = 0;
            int compilationCount = 0;
            int executionCount = 0;
            foreach (string directory in directories)
            {
                string outputDirectoryPerFolder = options.OutputDirectory.FullName;
                if (directory.Length > relativePathOffset)
                {
                    outputDirectoryPerFolder = Path.Combine(outputDirectoryPerFolder, directory.Substring(relativePathOffset));
                }
                try
                {
                    BuildFolder folder = BuildFolder.FromDirectory(directory.ToString(), runners, outputDirectoryPerFolder, options);
                    if (folder != null)
                    {
                        folders.Add(folder);
                        compilationCount += folder.Compilations.Count;
                        executionCount += folder.Executions.Count;
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("Error scanning folder {0}: {1}", directory, ex.Message);
                }
                if (++folderCount % 100 == 0)
                {
                    Console.Write($@"Found {folders.Count} folders to build ");
                    Console.Write($@"({compilationCount} compilations, ");
                    if (!options.NoExe)
                    {
                        Console.Write($@"{executionCount} executions, ");
                    }
                    Console.WriteLine($@"{folderCount} / {directories.Length} folders scanned)");
                }
            }
            Console.Write($@"Found {folders.Count} folders to build ({compilationCount} compilations, ");
            if (!options.NoExe)
            {
                Console.Write($@"{executionCount} executions, ");
            }
            Console.WriteLine($@"{directories.Length} folders scanned)");

            BuildFolderSet folderSet = new BuildFolderSet(folders, runners, options);
            bool success = folderSet.Build(runners);
            folderSet.WriteLogs();

            if (!options.NoCleanup)
            {
                PathExtensions.DeleteOutputFolders(options.OutputDirectory.ToString(), recursive: true);
            }

            return success ? 0 : 1;
        }

        private static void OutputBuckets(Dictionary<string, List<ProcessInfo>> buckets, string outputFile)
        {
            using (StreamWriter output = new StreamWriter(outputFile))
            {
                output.WriteLine($@"#buckets: {buckets.Count}, #failures: {buckets.Sum(b => b.Value.Count)}");

                IEnumerable<KeyValuePair<string, List<ProcessInfo>>> orderedBuckets = buckets.OrderByDescending(bucket => bucket.Value.Count);
                foreach (KeyValuePair<string, List<ProcessInfo>> bucketKvp in orderedBuckets)
                {
                    bucketKvp.Value.Sort((a, b) => a.InputFileName.CompareTo(b.InputFileName));
                    output.WriteLine($@"    [{bucketKvp.Value.Count} failures] {bucketKvp.Key}");
                }

                output.WriteLine();

                output.WriteLine("Detailed bucket info:");

                foreach (KeyValuePair<string, List<ProcessInfo>> bucketKvp in orderedBuckets)
                {
                    output.WriteLine("");
                    output.WriteLine($@"Bucket name: {bucketKvp.Key}");
                    output.WriteLine($@"Failing tests ({bucketKvp.Value.Count} total):");

                    foreach (ProcessInfo failure in bucketKvp.Value)
                    {
                        output.WriteLine($@"   {failure.InputFileName}");
                    }

                    output.WriteLine();
                    output.WriteLine($@"Detailed test failures:");

                    foreach (ProcessInfo failure in bucketKvp.Value)
                    {
                        output.WriteLine($@"Test: {failure.InputFileName}");
                        try
                        {
                            output.WriteLine(File.ReadAllText(failure.LogPath));
                        }
                        catch (Exception ex)
                        {
                            output.WriteLine($"Error reading file {failure.LogPath}: {ex.Message}");
                        }
                        output.WriteLine();
                    }
                }
                output.WriteLine();
            }
        }

        private static string AnalyzeCompilationFailure(ProcessInfo process)
        {
            try
            {
                if (process.TimedOut)
                {
                    return "Timed out";
                }

                string[] lines = File.ReadAllLines(process.LogPath);

                for (int lineIndex = 2; lineIndex < lines.Length; lineIndex++)
                {
                    string line = lines[lineIndex];
                    if (line.Length == 0 ||
                        line.StartsWith("EXEC : warning") ||
                        line.StartsWith("To repro,") ||
                        line.StartsWith("Emitting R2R PE file") ||
                        line.StartsWith("Warning: ") ||
                        line.StartsWith("Info: ") ||
                        line == "Assertion Failed")
                    {
                        continue;
                    }
                    return line;
                }
                return string.Join("; ", lines);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static string AnalyzeExecutionFailure(ProcessInfo process)
        {
            try
            {
                if (process.TimedOut)
                {
                    return "Timed out";
                }

                string[] lines = File.ReadAllLines(process.LogPath);

                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    string line = lines[lineIndex];
                    if (line.StartsWith("Assert failure"))
                    {
                        int openParen = line.IndexOf('(');
                        int closeParen = line.IndexOf(')', openParen + 1);
                        if (openParen > 0 && closeParen > openParen)
                        {
                            line = line.Substring(0, openParen) + line.Substring(closeParen + 1);
                        }
                        return line;
                    }
                    else if (line.StartsWith("Unhandled Exception:"))
                    {
                        int leftBracket = line.IndexOf('[');
                        int rightBracket = line.IndexOf(']', leftBracket + 1);
                        if (leftBracket >= 0 && rightBracket > leftBracket)
                        {
                            line = line.Substring(0, leftBracket) + line.Substring(rightBracket + 1);
                        }
                        return line;
                    }
                }

                return $"Exit code: {process.ExitCode} = 0x{process.ExitCode:X8}, expected {process.ExpectedExitCode}";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}
