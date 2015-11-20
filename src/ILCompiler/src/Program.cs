// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using Internal.CommandLine;

namespace ILCompiler
{
    class Program
    {
        bool _help;

        string _outputPath;

        Dictionary<string, string> _inputFilePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> _referenceFilePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        string _systemModuleName = "System.Private.CoreLib";

        CompilationOptions _options;

        CompilerTypeSystemContext _compilerTypeSystemContext;

        Program()
        {
        }

        void Help()
        {
            Console.WriteLine("ILCompiler compiler version " + typeof(Program).GetTypeInfo().Assembly.GetName().Version.ToString());
            Console.WriteLine();
            Console.WriteLine("-help        Display this usage message (Short form: -?)");
            Console.WriteLine("-out         Specify output file name");
            Console.WriteLine("-dgmllog     Dump dgml log of dependency graph to specified file");
            Console.WriteLine("-fulllog     Generate full dependency log graph");
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

                case "dgmllog":
                    _options.DgmlLog = parser.GetStringValue();
                    break;
                   
                case "fulllog":
                    _options.FullLog = true;
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
                    _systemModuleName = parser.GetStringValue();
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
                EcmaModule module = _compilerTypeSystemContext.GetModuleFromPath(inputFile.Value);
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
            List<MethodDesc> rootMethods = new List<MethodDesc>();
            MethodDesc entryPointMethod = null;

            EcmaModule entryPointModule = GetEntryPointModule();
            if (entryPointModule != null)
            {
                int entryPointToken = entryPointModule.PEReader.PEHeaders.CorHeader.EntryPointTokenOrRelativeVirtualAddress;
                entryPointMethod = entryPointModule.GetMethod(MetadataTokens.EntityHandle(entryPointToken));
            }

            Compilation compilation = new Compilation(_compilerTypeSystemContext, _options);
            compilation.Log = Console.Out;
            compilation.OutputPath = _outputPath;
            if (_options.IsCppCodeGen)
            {
                // Set the entrypoint module
                compilation.EntryPointModule = entryPointModule;

                // Set the default output path for CPPCodgen
                compilation.CPPOutPath = Path.GetDirectoryName(_outputPath);

                // Don't set Out when using object writer which is handled by LLVM.
                // Set the writer corresponding to the entrypoint module.
                compilation.Out = compilation.GetOutWriterForModule(entryPointModule); 
            }

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

            TargetOS targetOS;
#if FXCORE
            // We could offer this as a command line option, but then we also need to
            // load a different RyuJIT, so this is a future nice to have...
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                targetOS = TargetOS.Windows;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                targetOS = TargetOS.Linux;
            else
                throw new NotImplementedException();
#else
            targetOS = TargetOS.Windows;
#endif

            _compilerTypeSystemContext = new CompilerTypeSystemContext(new TargetDetails(TargetArchitecture.X64, targetOS));
            _compilerTypeSystemContext.InputFilePaths = _inputFilePaths;
            _compilerTypeSystemContext.ReferenceFilePaths = _referenceFilePaths;

            _compilerTypeSystemContext.SetSystemModule(_compilerTypeSystemContext.GetModuleForSimpleName(_systemModuleName));

            // For now, we can do single file compilation only
            // TODO: Multifile
            SingleFileCompilation();

            return 0;
        }

        static int Main(string[] args)
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
