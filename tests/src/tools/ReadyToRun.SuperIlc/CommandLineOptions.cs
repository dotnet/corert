// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.IO;

namespace ReadyToRun.SuperIlc
{
    internal static class CommandLineOptions
    {
        public static CommandLineBuilder Build()
        {
            var parser = new CommandLineBuilder()
                .AddCommand(CompileFolder());
            
            return parser;

            Command CompileFolder() =>
                new Command("compile-directory", "Compile all assemblies in directory", 
                    new Option[] 
                    {
                        ToolPath(),
                        InputDirectory(),
                        OutputDirectory(),
                        UseCrossgen(),
                        UseCpaot(),
                        ReferencePath()
                    },
                    handler: CommandHandler.Create<DirectoryInfo, DirectoryInfo, DirectoryInfo, bool, bool, DirectoryInfo[]>(CompileDirectoryCommand.CompileDirectory));

            Option ToolPath() =>
                new Option(new[] {"--tool-directory", "-t"}, "Directory containing the selected optimizing compiler", new Argument<DirectoryInfo>().ExistingOnly());

            // Todo: Input / Output directories should be required arguments to the command when they're made available to handlers
            // https://github.com/dotnet/command-line-api/issues/297
            Option InputDirectory() =>
                new Option(new [] {"--input-directory", "-in"}, "Folder containing assemblies to optimize", new Argument<DirectoryInfo>().ExistingOnly());

            Option OutputDirectory() =>
                new Option(new [] {"--output-directory", "-out"}, "Folder to emit compiled assemblies", new Argument<DirectoryInfo>().LegalFilePathsOnly());

            Option UseCrossgen() =>
                new Option("--crossgen", "Compile with CoreCLR Crossgen", new Argument<bool>());

            Option UseCpaot() =>
                new Option("--cpaot", "Compile with CoreRT CPAOT", new Argument<bool>());

            Option ReferencePath() =>
                new Option(new[] {"--reference-path", "-r"}, "Folder containing assemblies to reference during compilation", new Argument<DirectoryInfo[]>(){Arity = ArgumentArity.ZeroOrMore}.ExistingOnly());
        }
    }
}
