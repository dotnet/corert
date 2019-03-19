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
        public static int CompileDirectory(DirectoryInfo toolDirectory, DirectoryInfo inputDirectory, DirectoryInfo outputDirectory, bool crossgen, bool cpaot, DirectoryInfo[] referencePath)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            if (toolDirectory == null)
            {
                Console.WriteLine("--tool-directory is a required argument.");
                return 1;
            }

            if (inputDirectory == null)
            {
                Console.WriteLine("--input-directory is a required argument.");
                return 1;
            }

            if (outputDirectory == null)
            {
                outputDirectory = inputDirectory;
            }

            if (OutputPathIsParentOfInputPath(inputDirectory, outputDirectory))
            {
                Console.WriteLine("Error: Input and output folders must be distinct, and the output directory (which gets deleted) better not be a parent of the input directory.");
                return 1;
            }

            CompilerRunner runner;
            if (cpaot)
            {
                runner = new CpaotRunner(toolDirectory.ToString(), inputDirectory.ToString(), outputDirectory.ToString(), referencePath?.Select(x => x.ToString())?.ToList());
            }
            else
            {
                runner = new CrossgenRunner(toolDirectory.ToString(), inputDirectory.ToString(), outputDirectory.ToString(), referencePath?.Select(x => x.ToString())?.ToList());
            }

            string runnerOutputPath = runner.GetOutputPath();
            if (Directory.Exists(runnerOutputPath))
            {
                try
                {
                    Directory.Delete(runnerOutputPath, recursive: true);
                }
                catch (Exception ex) when (
                    ex is UnauthorizedAccessException
                    || ex is DirectoryNotFoundException
                    || ex is IOException
                )
                {
                    Console.WriteLine($"Error: Could not delete output folder {outputDirectory.FullName}. {ex.Message}");
                    return 1;
                }
            }

            Directory.CreateDirectory(runnerOutputPath);

            List<ProcessInfo> compilationsToRun = new List<ProcessInfo>();

            // Copy unmanaged files (runtime, native dependencies, resources, etc)
            foreach (string file in Directory.EnumerateFiles(inputDirectory.FullName))
            {
                if (ComputeManagedAssemblies.IsManaged(file))
                {
                    ProcessInfo compilationToRun = runner.CompilationProcess(file);
                    compilationToRun.InputFileName = file;
                    compilationsToRun.Add(compilationToRun);
                }
                else
                {
                    // Copy through all other files
                    File.Copy(file, Path.Combine(runnerOutputPath, Path.GetFileName(file)));
                }
            }

            ParallelRunner.Run(compilationsToRun);

            bool success = true;
            List<string> failedCompilationAssemblies = new List<string>();
            int successfulCompileCount = 0;

            foreach (ProcessInfo processInfo in compilationsToRun)
            {
                if (processInfo.Succeeded)
                {
                    successfulCompileCount++;
                }
                else
                {
                    File.Copy(processInfo.InputFileName, Path.Combine(runnerOutputPath, Path.GetFileName(processInfo.InputFileName)));
                    failedCompilationAssemblies.Add(processInfo.InputFileName);
                }
            }

            Console.WriteLine($"Compiled {successfulCompileCount}/{successfulCompileCount + failedCompilationAssemblies.Count} assemblies in {stopwatch.ElapsedMilliseconds} msecs.");

            if (failedCompilationAssemblies.Count > 0)
            {
                Console.WriteLine($"Failed to compile {failedCompilationAssemblies.Count} assemblies:");
                foreach (var assembly in failedCompilationAssemblies)
                {
                    Console.WriteLine(assembly);
                }
            }

            return success ? 0 : 1;
        }

        static bool OutputPathIsParentOfInputPath(DirectoryInfo inputPath, DirectoryInfo outputPath)
        {
            DirectoryInfo parentInfo = inputPath.Parent;
            while (parentInfo != null)
            {
                if (parentInfo == outputPath)
                    return true;

                parentInfo = parentInfo.Parent;

            }

            return false;
        }
    }    
}
