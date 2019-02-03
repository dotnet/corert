// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace ReadyToRun.SuperIlc
{
    class Program
    {
        static void ShowUsage()
        {
            Console.WriteLine("dotnet SuperIlc --in <FolderToCompile> --out <OutputFolder> --ref <Referenced assembly folder> [--cpaot] [--crossgen]");
        }

        static void Main(string[] args)
        {
            var commandLine = new CommandLineOptions();
            var helpSyntax = commandLine.ParseCommandLine(args);

            if (commandLine.Help || args.Length == 0)
            {
                Console.WriteLine(helpSyntax.GetHelpText());
                return;
            }

            if (!commandLine.Validate())
                return;

            CompilerRunner runner;
            if (commandLine.OptimizingTool == ReadyToRunTool.Cpaot)
            {
                runner = new CpaotRunner(commandLine.ToolPath, commandLine.InputPath, commandLine.OutputPath, commandLine.ReferenceFolders);
            }
            else
            {
                runner = new CrossgenRunner(commandLine.ToolPath, commandLine.InputPath, commandLine.OutputPath, commandLine.ReferenceFolders);
            }

            foreach (var assemblyToOptimize in ComputeManagedAssemblies.GetManagedAssembliesInFolder(commandLine.InputPath))
            {
                // Compile all assemblies in the input folder
                runner.CompileAssembly(assemblyToOptimize);
            }
        }
    }
}
