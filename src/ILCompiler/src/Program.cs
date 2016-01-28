// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
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

        private void Help()
        {
            Console.WriteLine("ILCompiler compiler version " + typeof(Program).GetTypeInfo().Assembly.GetName().Version.ToString());
            Console.WriteLine();
            Console.WriteLine("-help        Display this usage message (Short form: -?)");
            Console.WriteLine("-out         Specify output file name");
            Console.WriteLine("-reference   Reference metadata from the specified assembly (Short form: -r)");
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
#else
            _options.TargetOS = TargetOS.Windows;
#endif

            _options.TargetArchitecture = TargetArchitecture.X64;
        }

        // TODO: Use System.CommandLine for command line parsing
        // https://github.com/dotnet/corert/issues/568
        private void ParseCommandLine(string[] args)
        {
            var parser = new CommandLineParser(args);

            string option;
            while ((option = parser.GetOption()) != null)
            {
                switch (option.ToLowerInvariant())
                {
                    case "?":
                    case "help":
                        _help = true;
                        break;

                    case "":
                    case "in":
                        parser.AppendExpandedPaths(_inputFilePaths, true);
                        break;

                    case "o":
                    case "out":
                        _options.OutputFilePath = parser.GetStringValue();
                        break;

                    case "dgmllog":
                        _options.DgmlLog = parser.GetStringValue();
                        break;

                    case "fulllog":
                        _options.FullLog = true;
                        break;

                    case "verbose":
                        _options.Verbose = true;
                        break;

                    case "r":
                    case "reference":
                        parser.AppendExpandedPaths(_referenceFilePaths, false);
                        break;

                    case "cpp":
                        _options.IsCppCodeGen = true;
                        break;

                    case "nolinenumbers":
                        _options.NoLineNumbers = true;
                        break;

                    case "systemmodule":
                        _options.SystemModuleName = parser.GetStringValue();
                        break;

                    default:
                        throw new CommandLineException("Unrecognized option: " + parser.GetCurrentOption());
                }
            }
        }

        private void SingleFileCompilation()
        {
            Compilation compilation = new Compilation(_options);
            compilation.Log = _options.Verbose ? Console.Out : TextWriter.Null;

            compilation.CompileSingleFile();
        }

        private int Run(string[] args)
        {
            InitializeDefaultOptions();

            ParseCommandLine(args);

            if (_help)
            {
                Help();
                return 1;
            }

            if (_options.InputFilePaths.Count == 0)
                throw new CommandLineException("No input files specified");

            if (_options.OutputFilePath == null)
                throw new CommandLineException("Output filename must be specified (/out <file>)");

            // For now, we can do single file compilation only
            // TODO: Multifile
            SingleFileCompilation();

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
                return 1;
            }
#endif
        }
    }
}
