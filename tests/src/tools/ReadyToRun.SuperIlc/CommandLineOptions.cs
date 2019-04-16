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
                .AddCommand(CompileFolder())
                .AddCommand(CompileSubtree());

            return parser;

            Command CompileFolder() =>
                new Command("compile-directory", "Compile all assemblies in directory",
                    new Option[]
                    {
                        InputDirectory(),
                        OutputDirectory(),
                        CoreRootDirectory(),
                        CpaotDirectory(),
                        Crossgen(),
                        NoJit(),
                        NoExe(),
                        NoEtw(),
                        NoCleanup(),
                        Sequential(),
                        ReferencePath()
                    },
                    handler: CommandHandler.Create<BuildOptions>(CompileDirectoryCommand.CompileDirectory));

            Command CompileSubtree() =>
                new Command("compile-subtree", "Build each directory in a given subtree containing any managed assemblies as a separate app",
                    new Option[]
                    {
                        InputDirectory(),
                        OutputDirectory(),
                        CoreRootDirectory(),
                        CpaotDirectory(),
                        Crossgen(),
                        NoJit(),
                        NoExe(),
                        NoEtw(),
                        NoCleanup(),
                        Sequential(),
                        ReferencePath()
                    },
                    handler: CommandHandler.Create<BuildOptions>(CompileSubtreeCommand.CompileSubtree));

            // Todo: Input / Output directories should be required arguments to the command when they're made available to handlers
            // https://github.com/dotnet/command-line-api/issues/297
            Option InputDirectory() =>
                new Option(new[] { "--input-directory", "-in" }, "Folder containing assemblies to optimize", new Argument<DirectoryInfo>().ExistingOnly());

            Option OutputDirectory() =>
                new Option(new[] { "--output-directory", "-out" }, "Folder to emit compiled assemblies", new Argument<DirectoryInfo>().LegalFilePathsOnly());

            Option CoreRootDirectory() =>
                new Option(new[] { "--core-root-directory", "-cr" }, "Location of the CoreCLR CORE_ROOT folder", new Argument<DirectoryInfo>().ExistingOnly());

            Option CpaotDirectory() =>
                new Option(new[] { "--cpaot-directory", "-cpaot" }, "Folder containing the CPAOT compiler", new Argument<DirectoryInfo>().ExistingOnly());

            Option ReferencePath() =>
                new Option(new[] { "--reference-path", "-r" }, "Folder containing assemblies to reference during compilation", new Argument<DirectoryInfo[]>() { Arity = ArgumentArity.ZeroOrMore }.ExistingOnly());

            Option Crossgen() =>
                new Option(new[] { "--crossgen" }, "Compile the apps using Crossgen in the CORE_ROOT folder", new Argument<bool>());

            Option NoJit() =>
                new Option(new[] { "--nojit" }, "Don't run tests in JITted mode", new Argument<bool>());

            Option NoEtw() =>
                new Option(new[] { "--noetw" }, "Don't capture jitted methods using ETW", new Argument<bool>());

            Option NoExe() =>
                new Option(new[] { "--noexe" }, "Compilation-only mode (don't execute the built apps)", new Argument<bool>());

            Option NoCleanup() =>
                new Option(new[] { "--nocleanup" }, "Don't clean up compilation artifacts after test runs", new Argument<bool>());

            Option Sequential() =>
                new Option(new[] { "--sequential" }, "Run tests sequentially", new Argument<bool>());
        }
    }
}
