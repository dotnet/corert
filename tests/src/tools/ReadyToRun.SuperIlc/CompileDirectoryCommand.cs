// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;

namespace ReadyToRun.SuperIlc
{
    class CompileDirectoryCommand
    {
        public static int CompileDirectory(DirectoryInfo toolDirectory, DirectoryInfo inputDirectory, DirectoryInfo outputDirectory, bool crossgen, bool cpaot, DirectoryInfo[] referencePath)
        {
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
                Console.WriteLine("--output-directory is a required argument.");
                return 1;
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

            if (outputDirectory.Exists)
            {
                try
                {
                    outputDirectory.Delete(recursive: true);
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

            outputDirectory.Create();

            bool success = true;
            // Copy unmanaged files (runtime, native dependencies, resources, etc)
            foreach (string file in Directory.EnumerateFiles(inputDirectory.FullName))
            {
                if (ComputeManagedAssemblies.IsManaged(file))
                {
                    // Compile managed code
                    if (!runner.CompileAssembly(file))
                    {
                        success = false;

                        // On compile failure, pass through the input IL assembly so the output is still usable
                        File.Copy(file, Path.Combine(outputDirectory.FullName, Path.GetFileName(file)));
                    }
                }
                else
                {
                    // Copy through all other files
                    File.Copy(file, Path.Combine(outputDirectory.FullName, Path.GetFileName(file)));
                }
            }

            return success ? 0 : 1;
        }

        static bool OutputPathIsParentOfInputPath(DirectoryInfo inputPath, DirectoryInfo outputPath)
        {
            if (inputPath == outputPath)
                return true;

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
