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
    class CompileDirectoryCommand
    {
        public static int CompileDirectory(
            DirectoryInfo inputDirectory,
            DirectoryInfo outputDirectory,
            DirectoryInfo crossgenDirectory,
            DirectoryInfo cpaotDirectory,
            DirectoryInfo[] referencePath)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

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
            string coreRunPath = null;
            foreach (string path in referencePaths)
            {
                string candidatePath = Path.Combine(path, "CoreRun.exe");
                if (File.Exists(candidatePath))
                {
                    coreRunPath = candidatePath;
                    break;
                }
            }

            if (coreRunPath == null)
            {
                Console.Error.WriteLine("CoreRun.exe not found in reference folders, execution won't run");
            }

            List<CompilerRunner> runners = new List<CompilerRunner>();
            runners.Add(new JitRunner(null, inputDirectory.ToString(), outputDirectory.ToString(), referencePaths));

            if (cpaotDirectory != null)
            {
                runners.Add(new CpaotRunner(cpaotDirectory.ToString(), inputDirectory.ToString(), outputDirectory.ToString(), referencePaths));
            }
            if (crossgenDirectory != null)
            {
                runners.Add(new CrossgenRunner(crossgenDirectory.ToString(), inputDirectory.ToString(), outputDirectory.ToString(), referencePaths));
            }

            List<string> compilationInputFiles = new List<string>();
            List<string> passThroughFiles = new List<string>();
            string mainExecutable = null;

            // Copy unmanaged files (runtime, native dependencies, resources, etc)
            foreach (string file in Directory.EnumerateFiles(inputDirectory.FullName))
            {
                bool isManagedAssembly = ComputeManagedAssemblies.IsManaged(file);
                if (isManagedAssembly)
                {
                    compilationInputFiles.Add(file);
                }
                else
                {
                    passThroughFiles.Add(file);
                }
                if (Path.GetExtension(file).Equals(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    mainExecutable = file;
                }
            }

            foreach (CompilerRunner runner in runners)
            {
                string runnerOutputPath = runner.GetOutputPath();
                runnerOutputPath.RecreateDirectory();
                foreach (string file in passThroughFiles)
                {
                    File.Copy(file, Path.Combine(runnerOutputPath, Path.GetFileName(file)));
                }
            }

            List<ProcessInfo> compilationsToRun = new List<ProcessInfo>();

            Application application = new Application(compilationInputFiles, mainExecutable, runners, coreRunPath);
            foreach (ProcessInfo[] compilation in application.Compilations)
            {
                foreach (CompilerRunner runner in runners)
                {
                    ProcessInfo compilationProcess = compilation[(int)runner.Index];
                    if (compilationProcess != null)
                    {
                        compilationsToRun.Add(compilationProcess);
                    }
                }
            }

            compilationsToRun.Sort((a, b) => b.CompilationCostHeuristic.CompareTo(a.CompilationCostHeuristic));

            ParallelRunner.Run(compilationsToRun);

            bool success = true;
            List<KeyValuePair<string, string>> failedCompilationsPerBuilder = new List<KeyValuePair<string, string>>();
            int successfulCompileCount = 0;

            foreach (ProcessInfo[] compilation in application.Compilations)
            {
                string file = null;
                string failedBuilders = null;
                foreach (CompilerRunner runner in runners)
                {
                    ProcessInfo runnerProcess = compilation[(int)runner.Index];
                    if (runnerProcess != null && !runnerProcess.Succeeded)
                    {
                        File.Copy(runnerProcess.InputFileName, runnerProcess.OutputFileName);
                        if (file == null)
                        {
                            file = runnerProcess.InputFileName;
                            failedBuilders = runner.CompilerName;
                        }
                        else
                        {
                            failedBuilders += "; " + runner.CompilerName;
                        }
                    }
                }
                if (file != null)
                {
                    failedCompilationsPerBuilder.Add(new KeyValuePair<string, string>(file, failedBuilders));
                    success = false;
                }
                else
                {
                    successfulCompileCount++;
                }
            }

            Console.WriteLine($"Compiled {successfulCompileCount} / {successfulCompileCount + failedCompilationsPerBuilder.Count} assemblies in {stopwatch.ElapsedMilliseconds} msecs.");

            if (failedCompilationsPerBuilder.Count > 0)
            {
                Console.WriteLine($"Failed to compile {failedCompilationsPerBuilder.Count} assemblies:");
                foreach (KeyValuePair<string, string> assemblyBuilders in failedCompilationsPerBuilder)
                {
                    string assemblySpec = assemblyBuilders.Key;
                    if (runners.Count > 1)
                    {
                        assemblySpec += " (" + assemblyBuilders.Value + ")";
                    }
                    Console.WriteLine(assemblySpec);
                }
            }

            if (coreRunPath != null)
            {
                List<ProcessInfo> executionsToRun = new List<ProcessInfo>();
                foreach (CompilerRunner runner in runners)
                {
                    bool compilationsSucceeded = application.Compilations.All(comp => comp[(int)runner.Index]?.Succeeded ?? true);
                    if (compilationsSucceeded)
                    {
                        ProcessInfo executionProcess = application.Execution[(int)runner.Index];
                        if (executionProcess != null)
                        {
                            executionsToRun.Add(executionProcess);
                        }
                    }
                }

                ParallelRunner.Run(executionsToRun);
            }

            return success ? 0 : 1;
        }
    }    
}
