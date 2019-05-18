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
                .AddCommand(CompileSubtree())
                .AddCommand(CompileNugetPackages());

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
                        Framework(),
                        UseFramework(),
                        Release(),
                        LargeBubble(),
                        ReferencePath(),
                        IssuesPath(),
                        CompilationTimeoutMinutes(),
                        ExecutionTimeoutMinutes(),
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
                        Framework(),
                        UseFramework(),
                        Release(),
                        LargeBubble(),
                        ReferencePath(),
                        IssuesPath(),
                        CompilationTimeoutMinutes(),
                        ExecutionTimeoutMinutes(),
                    },
                    handler: CommandHandler.Create<BuildOptions>(CompileSubtreeCommand.CompileSubtree));

            Command CompileNugetPackages() =>
                new Command("compile-nuget", "Restore a list of Nuget packages into an empty console app, publish, and optimize with Crossgen / CPAOT",
                    new Option[]
                    {
                        OutputDirectory(),
                        PackageList(),
                        CoreRootDirectory(),
                        Crossgen(),
                        CpaotDirectory(),
                        NoCleanup(),
                        CompilationTimeoutMinutes(),
                        ExecutionTimeoutMinutes(),
                    },
                    handler: CommandHandler.Create<BuildOptions>(CompileNugetCommand.CompileNuget));

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

            Option Framework() =>
                new Option(new[] { "--framework" }, "Precompile and use native framework", new Argument<bool>());

            Option UseFramework() =>
                new Option(new[] { "--use-framework" }, "Use native framework (don't precompile, assume previously compiled)", new Argument<bool>());

            Option Release() =>
                new Option(new[] { "--release" }, "Build the tests in release mode", new Argument<bool>());

            Option LargeBubble() =>
                new Option(new[] { "--large-bubble" }, "Assume all input files as part of one version bubble", new Argument<bool>());

            Option IssuesPath() =>
                new Option(new[] { "--issues-path", "-ip" }, "Path to issues.targets", new Argument<FileInfo[]>() { Arity = ArgumentArity.ZeroOrMore });

            Option CompilationTimeoutMinutes() =>
                new Option(new[] { "--compilation-timeout-minutes", "-ct" }, "Compilation timeout (minutes)", new Argument<int>());

            Option ExecutionTimeoutMinutes() =>
                new Option(new[] { "--execution-timeout-minutes", "-et" }, "Execution timeout (minutes)", new Argument<int>());

            //
            // compile-nuget specific options
            //
            Option PackageList() =>
                new Option(new[] { "--package-list", "-pl" }, "Text file containing a package name on each line", new Argument<FileInfo>().ExistingOnly());
        }
    }
}
