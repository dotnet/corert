// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.CommandLine;
using System.Runtime.InteropServices;

using Internal.TypeSystem;

using Internal.CommandLine;

namespace ILCompiler
{
    internal class Program
    {
        private CompilationOptions _options;

        private Dictionary<string, string> _inputFilePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, string> _referenceFilePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private bool _help;

        private Program()
        {
        }

        private void Help(string helpText)
        {
            Console.WriteLine();
            Console.Write("Microsoft (R) .NET Native IL Compiler");
            Console.Write(" ");
            Console.Write(typeof(Program).GetTypeInfo().Assembly.GetName().Version);
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine(helpText);
        }

        private void InitializeDefaultOptions()
        {
            _options = new CompilationOptions();

            _options.InputFilePaths = _inputFilePaths;
            _options.ReferenceFilePaths = _referenceFilePaths;

            _options.SystemModuleName = "System.Private.CoreLib";

#if FXCORE
            // We could offer this as a command line option, but then we also need to
            // load a different RyuJIT, so this is a future nice to have...
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                _options.TargetOS = TargetOS.Windows;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                _options.TargetOS = TargetOS.Linux;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                _options.TargetOS = TargetOS.OSX;
            else
                throw new NotImplementedException();

            switch (RuntimeInformation.ProcessArchitecture)
            {
            case Architecture.X86:
                _options.TargetArchitecture = TargetArchitecture.X86;
                break;
            case Architecture.X64:
                _options.TargetArchitecture = TargetArchitecture.X64;
                break;
            case Architecture.Arm:
                _options.TargetArchitecture = TargetArchitecture.ARM;
                break;
            case Architecture.Arm64:
                _options.TargetArchitecture = TargetArchitecture.ARM64;
                break;
            default:
                throw new NotImplementedException();
            }
#else
            _options.TargetOS = TargetOS.Windows;
            _options.TargetArchitecture = TargetArchitecture.X64;
#endif
        }

        private ArgumentSyntax ParseCommandLine(string[] args)
        {
            IReadOnlyList<string> inputFiles = Array.Empty<string>();
            IReadOnlyList<string> referenceFiles = Array.Empty<string>();

            AssemblyName name = typeof(Program).GetTypeInfo().Assembly.GetName();
            ArgumentSyntax argSyntax = ArgumentSyntax.Parse(args, syntax =>
            {
                syntax.ApplicationName = name.Name.ToString();

                // HandleHelp writes to error, fails fast with crash dialog and lacks custom formatting.
                syntax.HandleHelp = false;
                syntax.HandleErrors = true;

                syntax.DefineOption("h|help", ref _help, "Help message for ILC");
                syntax.DefineOptionList("r|reference", ref referenceFiles, "Reference file(s) for compilation");
                syntax.DefineOption("o|out", ref _options.OutputFilePath, "Output file path");
                syntax.DefineOption("cpp", ref _options.IsCppCodeGen, "Compile for C++ code-generation");
                syntax.DefineOption("nolinenumbers", ref _options.NoLineNumbers, "Debug line numbers for C++ code-generation");
                syntax.DefineOption("dgmllog", ref _options.DgmlLog, "Save result of dependency analysis as DGML");
                syntax.DefineOption("fulllog", ref _options.FullLog, "Save detailed log of dependency analysis");
                syntax.DefineOption("verbose", ref _options.Verbose, "Enable verbose logging");
                syntax.DefineOption("systemmodule", ref _options.SystemModuleName, "System module name (default: System.Private.CoreLib)");
                syntax.DefineOption("multifile", ref _options.MultiFile, "Compile only input files (do not compile referenced assemblies)");
                syntax.DefineParameterList("in", ref inputFiles, "Input file(s) to compile");
            });
            foreach (var input in inputFiles)
                Helpers.AppendExpandedPaths(_inputFilePaths, input, true);

            foreach (var reference in referenceFiles)
                Helpers.AppendExpandedPaths(_referenceFilePaths, reference, false);

            return argSyntax;
        }

        private int Run(string[] args)
        {
            InitializeDefaultOptions();

            ArgumentSyntax syntax = ParseCommandLine(args);
            if (_help)
            {
                Help(syntax.GetHelpText());
                return 1;
            }

            if (_options.InputFilePaths.Count == 0)
                throw new CommandLineException("No input files specified");

            if (_options.OutputFilePath == null)
                throw new CommandLineException("Output filename must be specified (/out <file>)");

            Compilation compilation = new Compilation(_options);
            compilation.Log = _options.Verbose ? Console.Out : TextWriter.Null;
            compilation.Compile();

            return 0;
        }

        private static int Main(string[] args)
        {
#if DEBUG
            return new Program().Run(args);
#else
            try
            {
                return new Program().Run(args);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Error: " + e.Message);
                Console.Error.WriteLine(e.ToString());
                return 1;
            }
#endif
        }
    }
}
