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

        private TargetArchitecture _targetArchitecture;
        private TargetOS _targetOS;
        private string _systemModuleName = "System.Private.CoreLib";
        private bool _multiFile;

        private string _singleMethodTypeName;
        private string _singleMethodName;
        private IReadOnlyList<string> _singleMethodGenericArgs;

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

#if FXCORE
            // We could offer this as a command line option, but then we also need to
            // load a different RyuJIT, so this is a future nice to have...
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                _targetOS = TargetOS.Windows;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                _targetOS = TargetOS.Linux;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                _targetOS = TargetOS.OSX;
            else
                throw new NotImplementedException();

            switch (RuntimeInformation.ProcessArchitecture)
            {
            case Architecture.X86:
                _targetArchitecture = TargetArchitecture.X86;
                break;
            case Architecture.X64:
                _targetArchitecture = TargetArchitecture.X64;
                break;
            case Architecture.Arm:
                _targetArchitecture = TargetArchitecture.ARM;
                break;
            case Architecture.Arm64:
                _targetArchitecture = TargetArchitecture.ARM64;
                break;
            default:
                throw new NotImplementedException();
            }
#else
            _targetOS = TargetOS.Windows;
            _targetArchitecture = TargetArchitecture.X64;
#endif
        }

        private ArgumentSyntax ParseCommandLine(string[] args)
        {
            IReadOnlyList<string> inputFiles = Array.Empty<string>();
            IReadOnlyList<string> referenceFiles = Array.Empty<string>();

            bool waitForDebugger = false;
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
                syntax.DefineOption("systemmodule", ref _systemModuleName, "System module name (default: System.Private.CoreLib)");
                syntax.DefineOption("multifile", ref _multiFile, "Compile only input files (do not compile referenced assemblies)");
                syntax.DefineOption("waitfordebugger", ref waitForDebugger, "Pause to give opportunity to attach debugger");

                syntax.DefineOption("singlemethodtypename", ref _singleMethodTypeName, "Single method compilation: name of the owning type");
                syntax.DefineOption("singlemethodname", ref _singleMethodName, "Single method compilation: name of the method");
                syntax.DefineOptionList("singlemethodgenericarg", ref _singleMethodGenericArgs, "Single method compilation: generic arguments to the method");

                syntax.DefineParameterList("in", ref inputFiles, "Input file(s) to compile");
            });
            if (waitForDebugger)
            {
                Console.WriteLine("Waiting for debugger to attach. Press ENTER to continue");
                Console.ReadLine();
            }
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

            if (_inputFilePaths.Count == 0)
                throw new CommandLineException("No input files specified");

            if (_options.OutputFilePath == null)
                throw new CommandLineException("Output filename must be specified (/out <file>)");

            //
            // Initialize type system context
            //

            var typeSystemContext = new CompilerTypeSystemContext(new TargetDetails(_targetArchitecture, _targetOS));
            typeSystemContext.InputFilePaths = _inputFilePaths;
            typeSystemContext.ReferenceFilePaths = _referenceFilePaths;

            typeSystemContext.SetSystemModule(typeSystemContext.GetModuleForSimpleName(_systemModuleName));

            //
            // Initialize compilation group
            //

            // Single method mode?
            MethodDesc singleMethod = CheckAndParseSingleMethodModeArguments(typeSystemContext);

            CompilationModuleGroup compilationGroup;
            if (singleMethod != null)
            {
                compilationGroup = new SingleMethodCompilationModuleGroup(typeSystemContext, singleMethod);
            }
            else if (_multiFile)
            {
                compilationGroup = new MultiFileCompilationModuleGroup(typeSystemContext);
            }
            else
            {
                compilationGroup = new SingleFileCompilationModuleGroup(typeSystemContext);
            }

            //
            // Compile
            //

            Compilation compilation = new Compilation(_options, typeSystemContext, compilationGroup);
            compilation.Log = _options.Verbose ? Console.Out : TextWriter.Null;
            compilation.Compile();

            return 0;
        }

        private TypeDesc FindType(CompilerTypeSystemContext context, string typeName)
        {
            TypeDesc foundType = context.SystemModule.GetTypeByCustomAttributeTypeName(typeName);
            if (foundType == null)
                throw new CommandLineException($"Type '{typeName}' not found");

            return foundType;
        }

        private MethodDesc CheckAndParseSingleMethodModeArguments(CompilerTypeSystemContext context)
        {
            if (_singleMethodName == null && _singleMethodTypeName == null && _singleMethodGenericArgs == null)
                return null;

            if (_singleMethodName == null || _singleMethodTypeName == null)
                throw new CommandLineException("Both method name and type name are required parameters for single method mode");

            TypeDesc owningType = FindType(context, _singleMethodTypeName);

            // TODO: allow specifying signature to distinguish overloads
            MethodDesc method = owningType.GetMethod(_singleMethodName, null);
            if (method == null)
                throw new CommandLineException($"Method '{_singleMethodName}' not found in '{_singleMethodTypeName}'");

            if (method.HasInstantiation != (_singleMethodGenericArgs != null) ||
                (method.HasInstantiation && (method.Instantiation.Length != _singleMethodGenericArgs.Count)))
            {
                throw new CommandLineException(
                    $"Expected {method.Instantiation.Length} generic arguments for method '{_singleMethodName}' on type '{_singleMethodTypeName}'");
            }

            if (method.HasInstantiation)
            {
                List<TypeDesc> genericArguments = new List<TypeDesc>();
                foreach (var argString in _singleMethodGenericArgs)
                    genericArguments.Add(FindType(context, argString));
                method = method.MakeInstantiatedMethod(genericArguments.ToArray());
            }

            return method;
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
