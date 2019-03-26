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
            bool noEtw,
            DirectoryInfo[] referencePath)
        {
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
                Application application = Application.FromDirectory(directory.ToString(), runners, outputDirectoryPerApp, noEtw, coreRunPath);
                if (application != null)
                {
                    applications.Add(application);
                }
                if (++count % 100 == 0)
                {
                    Console.WriteLine($@"Found {applications.Count} apps in {count} folders");
                }
            }

            string applicationSetLogPath = Path.Combine(inputDirectory.ToString(), "application-set.log");

            using (ApplicationSet applicationSet = new ApplicationSet(applications, runners, coreRunPath, applicationSetLogPath))
            {
                return applicationSet.Build(coreRunPath, runners, applicationSetLogPath) ? 0 : 1;
            }
        }
    }
}
