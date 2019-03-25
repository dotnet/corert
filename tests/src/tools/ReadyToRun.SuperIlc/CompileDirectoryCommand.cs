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
            bool noJit,
            bool noEtw,
            DirectoryInfo[] referencePath)
        {
            if (inputDirectory == null)
            {
                Console.Error.WriteLine("--input-directory is a required argument.");
                return 1;
            }

            if (outputDirectory == null)
            {
                outputDirectory = inputDirectory;
            }

            if (outputDirectory.IsParentOf(inputDirectory))
            {
                Console.Error.WriteLine("Error: Input and output folders must be distinct, and the output directory (which gets deleted) better not be a parent of the input directory.");
                return 1;
            }

            List<string> referencePaths = referencePath?.Select(x => x.ToString())?.ToList();
            string coreRunPath = SuperIlcHelpers.FindCoreRun(referencePaths);

            IEnumerable<CompilerRunner> runners = SuperIlcHelpers.CompilerRunners(
                inputDirectory.ToString(), outputDirectory.ToString(), cpaotDirectory.ToString(), crossgenDirectory.ToString(), noJit, referencePaths);

            Application application = Application.FromDirectory(inputDirectory.FullName, runners, outputDirectory.FullName, noEtw, coreRunPath);
            if (application == null)
            {
                Console.Error.WriteLine($"No managed app found in {inputDirectory.FullName}");
            }
            string applicationSetLogPath = Path.Combine(inputDirectory.ToString(), "application-set.log");

            using (ApplicationSet applicationSet = new ApplicationSet(new Application[] { application }, runners, coreRunPath, applicationSetLogPath))
            {
                return applicationSet.Build(coreRunPath, runners, applicationSetLogPath) ? 0 : 1;
            }
        }
    }    
}
