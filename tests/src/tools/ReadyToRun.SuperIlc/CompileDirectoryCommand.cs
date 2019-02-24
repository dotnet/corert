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

            CompilerRunner runner;
            if (cpaot)
            {
                runner = new CpaotRunner(toolDirectory.ToString(), inputDirectory.ToString(), outputDirectory.ToString(), referencePath?.Select(x => x.ToString())?.ToList());
            }
            else
            {
                runner = new CrossgenRunner(toolDirectory.ToString(), inputDirectory.ToString(), outputDirectory.ToString(), referencePath?.Select(x => x.ToString())?.ToList());
            }

            bool success = true;
            foreach (var assemblyToOptimize in ComputeManagedAssemblies.GetManagedAssembliesInFolder(inputDirectory.ToString()))
            {
                // Compile all assemblies in the input folder
                if (!runner.CompileAssembly(assemblyToOptimize))
                    success = false;
            }

            return success ? 0 : 1;
        }
    }    
}
