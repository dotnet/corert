// Licensed to the .NET Foundation under one or more agreements.
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
    }
}
