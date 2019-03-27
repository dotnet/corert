// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ReadyToRun.SuperIlc
{
    class CompileSubtreeCommand
    {
        public static int CompileSubtree(
            DirectoryInfo inputDirectory,
            DirectoryInfo outputDirectory,
            DirectoryInfo crossgenDirectory,
            DirectoryInfo cpaotDirectory,
            bool noJit,
            //bool noExe,
            bool noEtw,
            DirectoryInfo[] referencePath)
        {
            const bool noExe = false;

            if (inputDirectory == null)
            {
                Console.WriteLine("--input-directory is a required argument.");
                return 1;
            }

            if (outputDirectory == null)
            {
                outputDirectory = inputDirectory;
            }

            if (outputDirectory.IsParentOf(inputDirectory))
            {
                Console.WriteLine("Error: Input and output folders must be distinct, and the output directory (which gets deleted) better not be a parent of the input directory.");
                return 1;
            }

            List<string> referencePaths = referencePath?.Select(x => x.ToString())?.ToList();
            string coreRunPath = SuperIlcHelpers.FindCoreRun(referencePaths);

            IEnumerable<CompilerRunner> runners = SuperIlcHelpers.CompilerRunners(
                inputDirectory.ToString(), outputDirectory.ToString(), cpaotDirectory?.ToString(), crossgenDirectory?.ToString(), noJit, referencePaths);

            PathExtensions.DeleteOutputFolders(inputDirectory.ToString(), recursive: true);

            string[] directories = new string[] { inputDirectory.FullName }
                .Concat(
                    inputDirectory
                        .EnumerateDirectories("*", SearchOption.AllDirectories)
                        .Select(dirInfo => dirInfo.FullName)
                        .Where(path => !Path.GetExtension(path).Equals(".out", StringComparison.OrdinalIgnoreCase)))
                .ToArray();

            List<Application> applications = new List<Application>();
            int relativePathOffset = directories[0].Length + 1;
            int count = 0;
            foreach (string directory in directories)
            {
                string outputDirectoryPerApp = outputDirectory.FullName;
                if (directory.Length > relativePathOffset)
                {
                    outputDirectoryPerApp = Path.Combine(outputDirectoryPerApp, directory.Substring(relativePathOffset));
                }
                try
                {
                    Application application = Application.FromDirectory(directory.ToString(), runners, outputDirectoryPerApp, noExe, noEtw, coreRunPath);
                    if (application != null)
                    {
                        applications.Add(application);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("Error scanning folder {0}: {1}", directory, ex.Message);
                }
                if (++count % 100 == 0)
                {
                    Console.WriteLine($@"Found {applications.Count} apps in {count} / {directories.Length} folders");
                }
            }
            Console.WriteLine($@"Found {applications.Count} apps total in {directories.Length} folders");

            string timeStamp = DateTime.Now.ToString("MMdd-hhmm");
            string applicationSetLogPath = Path.Combine(inputDirectory.ToString(), "subtree-" + timeStamp + ".log");

            using (ApplicationSet applicationSet = new ApplicationSet(applications, runners, coreRunPath, applicationSetLogPath))
            {
                bool success = applicationSet.Build(coreRunPath, runners, applicationSetLogPath);

                string combinedSetLogPath = Path.Combine(inputDirectory.ToString(), "combined-" + timeStamp + ".log");
                using (StreamWriter combinedLog = new StreamWriter(combinedSetLogPath))
                {
                    StreamWriter[] perRunnerLog = new StreamWriter[(int)CompilerIndex.Count];
                    foreach (CompilerRunner runner in runners)
                    {
                        string runnerLogPath = Path.Combine(inputDirectory.ToString(), runner.CompilerName + "-" + timeStamp + ".log");
                        perRunnerLog[(int)runner.Index] = new StreamWriter(runnerLogPath);
                    }

                    foreach (Application app in applicationSet.Applications)
                    {
                        foreach (ProcessInfo[] compilation in app.Compilations)
                        {
                            foreach (CompilerRunner runner in runners)
                            {
                                ProcessInfo compilationProcess = compilation[(int)runner.Index];
                                if (compilationProcess != null)
                                {
                                    string log = $"\nCOMPILE {runner.CompilerName}:{compilationProcess.InputFileName}\n" + File.ReadAllText(compilationProcess.LogPath);
                                    perRunnerLog[(int)runner.Index].Write(log);
                                    combinedLog.Write(log);
                                }
                            }
                        }
                        foreach (ProcessInfo[] execution in app.Executions)
                        {
                            foreach (CompilerRunner runner in runners)
                            {
                                ProcessInfo executionProcess = execution[(int)runner.Index];
                                if (executionProcess != null)
                                {
                                    string log = $"\nEXECUTE {runner.CompilerName}:{executionProcess.InputFileName}\n" + File.ReadAllText(executionProcess.LogPath);
                                    perRunnerLog[(int)runner.Index].Write(log);
                                    combinedLog.Write(log);
                                }
                            }
                        }
                    }

                    foreach (CompilerRunner runner in runners)
                    {
                        perRunnerLog[(int)runner.Index].Dispose();
                    }
                }

                return success ? 0 : 1;
            }
        }
    }
}
