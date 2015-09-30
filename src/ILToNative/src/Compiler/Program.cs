// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using Internal.CommandLine;

namespace ILToNative
{
    class Program
    {
        bool _help;

        string _outputPath;

        Dictionary<string, string> _inputFilePaths = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        Dictionary<string, string> _referenceFilePaths = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

        CompilerTypeSystemContext _compilerTypeSystemContext;

        Program()
        {
        }

        void Help()
        {
            Console.WriteLine("ILToNative compiler version " + typeof(Program).Assembly.GetName().Version.ToString());
            Console.WriteLine();
            Console.WriteLine("-help        Display this usage message (Short form: -?)");
            Console.WriteLine("-out         Specify output file name");
            Console.WriteLine("-reference   Reference metadata from the specified assembly (Short form: -r)");
        }

        void ParseCommandLine(string[] args)
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
                    _outputPath = parser.GetStringValue();
                    break;

                case "r":
                case "reference":
                    parser.AppendExpandedPaths(_referenceFilePaths, false);
                    break;

                default:
                    throw new CommandLineException("Unrecognized option: " + parser.GetCurrentOption());
                }
            }
        }

        EcmaModule GetEntryPointModule()
        {
            EcmaModule mainModule = null;
            foreach (var inputFile in _inputFilePaths)
            {
                EcmaModule module = _compilerTypeSystemContext.GetModuleForSimpleName(inputFile.Key);
                if (module.PEReader.PEHeaders.IsExe)
                {
                    if (mainModule != null)
                        throw new CommandLineException("Multiple entrypoint modules");
                    mainModule = module;
                }
            }
            return mainModule;
        }

        void SingleFileCompilation()
        {
            EcmaModule entryPointModule = GetEntryPointModule();
            if (entryPointModule == null)
                throw new CommandLineException("No entrypoint module");

            int entryPointToken = entryPointModule.PEReader.PEHeaders.CorHeader.EntryPointTokenOrRelativeVirtualAddress;
            MethodDesc entryPointMethod = entryPointModule.GetMethod(MetadataTokens.EntityHandle(entryPointToken));

            Compilation compilation = new Compilation(_compilerTypeSystemContext);
            compilation.Log = Console.Out;
            compilation.Out = new StreamWriter(File.Create(_outputPath));

            compilation.CompileSingleFile(entryPointMethod);
        }

        int Run(string[] args)
        {
            ParseCommandLine(args);

            if (_help)
            {
                Help();
                return 1;
            }

            if (_inputFilePaths.Count == 0)
                throw new CommandLineException("No input files specified");

            if (_outputPath == null)
                throw new CommandLineException("Output filename must be specified (/out <file>)");

            _compilerTypeSystemContext = new CompilerTypeSystemContext(new TargetDetails(TargetArchitecture.X64));
            _compilerTypeSystemContext.InputFilePaths = _inputFilePaths;
            _compilerTypeSystemContext.ReferenceFilePaths = _referenceFilePaths;

            _compilerTypeSystemContext.SetSystemModule(_compilerTypeSystemContext.GetModuleForSimpleName("mscorlib"));

            // For now, we can do single file compilation only
            // TODO: Multifile
            SingleFileCompilation();

            return 0;
        }

        static int Main(string[] args)
        {
            try
            {
                return new Program().Run(args);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Error: " + e.Message);
                return 1;
            }
        }
    }
}
