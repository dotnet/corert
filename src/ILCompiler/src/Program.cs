// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.CommandLine;
using System.Runtime.InteropServices;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using Internal.CommandLine;

namespace ILCompiler
{
    internal class Program
    {
        private Dictionary<string, string> _inputFilePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, string> _referenceFilePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private string _outputFilePath;
        private bool _isCppCodegen;
        private bool _isWasmCodegen;
        private bool _isVerbose;

        private string _dgmlLogFileName;
        private bool _generateFullDgmlLog;
        private string _scanDgmlLogFileName;
        private bool _generateFullScanDgmlLog;

        private TargetArchitecture _targetArchitecture;
        private string _targetArchitectureStr;
        private TargetOS _targetOS;
        private string _targetOSStr;
        private OptimizationMode _optimizationMode;
        private bool _enableDebugInfo;
        private string _ilDump;
        private string _systemModuleName = "System.Private.CoreLib";
        private bool _multiFile;
        private bool _useSharedGenerics;
        private bool _useScanner;
        private bool _noScanner;
        private string _mapFileName;
        private string _metadataLogFileName;

        private string _singleMethodTypeName;
        private string _singleMethodName;
        private IReadOnlyList<string> _singleMethodGenericArgs;

        private IReadOnlyList<string> _codegenOptions = Array.Empty<string>();

        private IReadOnlyList<string> _rdXmlFilePaths = Array.Empty<string>();

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
        }

        private ArgumentSyntax ParseCommandLine(string[] args)
        {
            IReadOnlyList<string> inputFiles = Array.Empty<string>();
            IReadOnlyList<string> referenceFiles = Array.Empty<string>();

            bool optimize = false;

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
                syntax.DefineOption("o|out", ref _outputFilePath, "Output file path");
                syntax.DefineOption("O", ref optimize, "Enable optimizations");
                syntax.DefineOption("g", ref _enableDebugInfo, "Emit debugging information");
                syntax.DefineOption("cpp", ref _isCppCodegen, "Compile for C++ code-generation");
                syntax.DefineOption("wasm", ref _isWasmCodegen, "Compile for WebAssembly code-generation");
                syntax.DefineOption("dgmllog", ref _dgmlLogFileName, "Save result of dependency analysis as DGML");
                syntax.DefineOption("fulllog", ref _generateFullDgmlLog, "Save detailed log of dependency analysis");
                syntax.DefineOption("scandgmllog", ref _scanDgmlLogFileName, "Save result of scanner dependency analysis as DGML");
                syntax.DefineOption("scanfulllog", ref _generateFullScanDgmlLog, "Save detailed log of scanner dependency analysis");
                syntax.DefineOption("verbose", ref _isVerbose, "Enable verbose logging");
                syntax.DefineOption("systemmodule", ref _systemModuleName, "System module name (default: System.Private.CoreLib)");
                syntax.DefineOption("multifile", ref _multiFile, "Compile only input files (do not compile referenced assemblies)");
                syntax.DefineOption("waitfordebugger", ref waitForDebugger, "Pause to give opportunity to attach debugger");
                syntax.DefineOption("usesharedgenerics", ref _useSharedGenerics, "Enable shared generics");
                syntax.DefineOptionList("codegenopt", ref _codegenOptions, "Define a codegen option");
                syntax.DefineOptionList("rdxml", ref _rdXmlFilePaths, "RD.XML file(s) for compilation");
                syntax.DefineOption("map", ref _mapFileName, "Generate a map file");
                syntax.DefineOption("metadatalog", ref _metadataLogFileName, "Generate a metadata log file");
                syntax.DefineOption("scan", ref _useScanner, "Use IL scanner to generate optimized code (implied by -O)");
                syntax.DefineOption("noscan", ref _noScanner, "Do not use IL scanner to generate optimized code");
                syntax.DefineOption("ildump", ref _ilDump, "Dump IL assembly listing for compiler-generated IL");

                syntax.DefineOption("targetarch", ref _targetArchitectureStr, "Target architecture for cross compilation");
                syntax.DefineOption("targetos", ref _targetOSStr, "Target OS for cross compilation");

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

            _optimizationMode = optimize ? OptimizationMode.Blended : OptimizationMode.None;

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
            
            if (_outputFilePath == null)
                throw new CommandLineException("Output filename must be specified (/out <file>)");

            //
            // Set target Architecture and OS
            //
            if (_targetArchitectureStr != null)
            {
                if (_targetArchitectureStr.Equals("x86", StringComparison.OrdinalIgnoreCase))
                    _targetArchitecture = TargetArchitecture.X86;
                else if (_targetArchitectureStr.Equals("x64", StringComparison.OrdinalIgnoreCase))
                    _targetArchitecture = TargetArchitecture.X64;
                else if (_targetArchitectureStr.Equals("arm", StringComparison.OrdinalIgnoreCase))
                    _targetArchitecture = TargetArchitecture.ARM;
                else if (_targetArchitectureStr.Equals("armel", StringComparison.OrdinalIgnoreCase))
                    _targetArchitecture = TargetArchitecture.ARMEL;
                else if (_targetArchitectureStr.Equals("arm64", StringComparison.OrdinalIgnoreCase))
                    _targetArchitecture = TargetArchitecture.ARM64;
                else
                    throw new CommandLineException("Target architecture is not supported");
            }
            if (_targetOSStr != null)
            {
                if (_targetOSStr.Equals("windows", StringComparison.OrdinalIgnoreCase))
                    _targetOS = TargetOS.Windows;
                else if (_targetOSStr.Equals("linux", StringComparison.OrdinalIgnoreCase))
                    _targetOS = TargetOS.Linux;
                else if (_targetOSStr.Equals("osx", StringComparison.OrdinalIgnoreCase))
                    _targetOS = TargetOS.OSX;
                else
                    throw new CommandLineException("Target OS is not supported");
            }

            if (_isWasmCodegen)
                _targetArchitecture = TargetArchitecture.Wasm32;
            //
            // Initialize type system context
            //

            SharedGenericsMode genericsMode = _useSharedGenerics || (!_isCppCodegen && !_isWasmCodegen) ?
                SharedGenericsMode.CanonicalReferenceTypes : SharedGenericsMode.Disabled;

            // TODO: compiler switch for SIMD support?
            var simdVectorLength = (_isCppCodegen || _isWasmCodegen) ? SimdVectorLength.None : SimdVectorLength.Vector128Bit; 
            var targetDetails = new TargetDetails(_targetArchitecture, _targetOS, TargetAbi.CoreRT, simdVectorLength);
            var typeSystemContext = new CompilerTypeSystemContext(targetDetails, genericsMode);

            //
            // TODO: To support our pre-compiled test tree, allow input files that aren't managed assemblies since
            // some tests contain a mixture of both managed and native binaries.
            //
            // See: https://github.com/dotnet/corert/issues/2785
            //
            // When we undo this this hack, replace this foreach with
            //  typeSystemContext.InputFilePaths = _inputFilePaths;
            //
            Dictionary<string, string> inputFilePaths = new Dictionary<string, string>();
            foreach (var inputFile in _inputFilePaths)
            {
                try
                {
                    var module = typeSystemContext.GetModuleFromPath(inputFile.Value);
                    inputFilePaths.Add(inputFile.Key, inputFile.Value);
                }
                catch (TypeSystemException.BadImageFormatException)
                {
                    // Keep calm and carry on.
                }
            }

            typeSystemContext.InputFilePaths = inputFilePaths;
            typeSystemContext.ReferenceFilePaths = _referenceFilePaths;

            typeSystemContext.SetSystemModule(typeSystemContext.GetModuleForSimpleName(_systemModuleName));

            if (typeSystemContext.InputFilePaths.Count == 0)
                throw new CommandLineException("No input files specified");

            //
            // Initialize compilation group and compilation roots
            //

            // Single method mode?
            MethodDesc singleMethod = CheckAndParseSingleMethodModeArguments(typeSystemContext);

            CompilationModuleGroup compilationGroup;
            List<ICompilationRootProvider> compilationRoots = new List<ICompilationRootProvider>();
            if (singleMethod != null)
            {
                // Compiling just a single method
                compilationGroup = new SingleMethodCompilationModuleGroup(singleMethod);
                compilationRoots.Add(new SingleMethodRootProvider(singleMethod));
            }
            else
            {
                // Either single file, or multifile library, or multifile consumption.
                EcmaModule entrypointModule = null;
                foreach (var inputFile in typeSystemContext.InputFilePaths)
                {
                    EcmaModule module = typeSystemContext.GetModuleFromPath(inputFile.Value);

                    if (module.PEReader.PEHeaders.IsExe)
                    {
                        if (entrypointModule != null)
                            throw new Exception("Multiple EXE modules");
                        entrypointModule = module;
                    }

                    // TODO: Wasm fails to compile some of the exported methods due to missing opcodes
                    if (!_isWasmCodegen)
                    {
                        compilationRoots.Add(new ExportedMethodsRootProvider(module));
                    }
                }

                if (entrypointModule != null)
                {
                    // TODO: Wasm fails to compile some of the library initializers
                    if (!_isWasmCodegen)
                    {
                        LibraryInitializers libraryInitializers =
                            new LibraryInitializers(typeSystemContext, _isCppCodegen);
                        compilationRoots.Add(new MainMethodRootProvider(entrypointModule, libraryInitializers.LibraryInitializerMethods));
                    }
                    else
                    {
                        compilationRoots.Add(new RawMainMethodRootProvider(entrypointModule));
                    }
                }

                if (_multiFile)
                {
                    List<EcmaModule> inputModules = new List<EcmaModule>();

                    foreach (var inputFile in typeSystemContext.InputFilePaths)
                    {
                        EcmaModule module = typeSystemContext.GetModuleFromPath(inputFile.Value);

                        if (entrypointModule == null)
                        {
                            // This is a multifile production build - we need to root all methods
                            compilationRoots.Add(new LibraryRootProvider(module));
                        }
                        inputModules.Add(module);
                    }

                    compilationGroup = new MultiFileSharedCompilationModuleGroup(typeSystemContext, inputModules);
                }
                else
                {
                    if (entrypointModule == null)
                        throw new Exception("No entrypoint module");

                    // TODO: Wasm fails to compile some of the xported methods due to missing opcodes
                    if (!_isWasmCodegen)
                    {
                        compilationRoots.Add(new ExportedMethodsRootProvider((EcmaModule)typeSystemContext.SystemModule));
                    }

                    compilationGroup = new SingleFileCompilationModuleGroup(typeSystemContext);
                }

                foreach (var rdXmlFilePath in _rdXmlFilePaths)
                {
                    compilationRoots.Add(new RdXmlRootProvider(typeSystemContext, rdXmlFilePath));
                }
            }

            //
            // Compile
            //

            CompilationBuilder builder;
            if (_isWasmCodegen)
                builder = new WebAssemblyCodegenCompilationBuilder(typeSystemContext, compilationGroup);
            else if (_isCppCodegen)
                builder = new CppCodegenCompilationBuilder(typeSystemContext, compilationGroup);
            else
                builder = new RyuJitCompilationBuilder(typeSystemContext, compilationGroup);

            bool useScanner = _useScanner ||
                (_optimizationMode != OptimizationMode.None && !_isCppCodegen && !_isWasmCodegen);

            useScanner &= !_noScanner;

            ILScanResults scanResults = null;
            if (useScanner)
            {
                ILScannerBuilder scannerBuilder = builder.GetILScannerBuilder()
                    .UseCompilationRoots(compilationRoots);

                if (_scanDgmlLogFileName != null)
                    scannerBuilder.UseDependencyTracking(_generateFullScanDgmlLog ? DependencyTrackingLevel.All : DependencyTrackingLevel.First);

                IILScanner scanner = scannerBuilder.ToILScanner();

                scanResults = scanner.Scan();
            }

            var logger = new Logger(Console.Out, _isVerbose);

            DebugInformationProvider debugInfoProvider = _enableDebugInfo ?
                (_ilDump == null ? new DebugInformationProvider() : new ILAssemblyGeneratingMethodDebugInfoProvider(_ilDump, new EcmaOnlyDebugInformationProvider())) :
                new NullDebugInformationProvider();

            DependencyTrackingLevel trackingLevel = _dgmlLogFileName == null ?
                DependencyTrackingLevel.None : (_generateFullDgmlLog ? DependencyTrackingLevel.All : DependencyTrackingLevel.First);

            CompilerGeneratedMetadataManager metadataManager = new CompilerGeneratedMetadataManager(compilationGroup, typeSystemContext, _metadataLogFileName);

            builder
                .UseBackendOptions(_codegenOptions)
                .UseMetadataManager(metadataManager)
                .UseLogger(logger)
                .UseDependencyTracking(trackingLevel)
                .UseCompilationRoots(compilationRoots)
                .UseOptimizationMode(_optimizationMode)
                .UseDebugInfoProvider(debugInfoProvider);

            if (scanResults != null)
            {
                // If we have a scanner, feed the vtable analysis results to the compilation.
                // This could be a command line switch if we really wanted to.
                builder.UseVTableSliceProvider(scanResults.GetVTableLayoutInfo());

                // If we have a scanner, feed the generic dictionary results to the compilation.
                // This could be a command line switch if we really wanted to.
                builder.UseGenericDictionaryLayoutProvider(scanResults.GetDictionaryLayoutInfo());
            }

            ICompilation compilation = builder.ToCompilation();

            ObjectDumper dumper = _mapFileName != null ? new ObjectDumper(_mapFileName) : null;

            CompilationResults compilationResults = compilation.Compile(_outputFilePath, dumper);

            if (_dgmlLogFileName != null)
                compilationResults.WriteDependencyLog(_dgmlLogFileName);

            if (scanResults != null)
            {
                SimdHelper simdHelper = new SimdHelper();

                if (_scanDgmlLogFileName != null)
                    scanResults.WriteDependencyLog(_scanDgmlLogFileName);

                // If the scanner and compiler don't agree on what to compile, the outputs of the scanner might not actually be usable.
                // We are going to check this two ways:
                // 1. The methods and types generated during compilation are a subset of method and types scanned
                // 2. The methods and types scanned are a subset of methods and types compiled (this has a chance to hold for unoptimized builds only).

                // Check that methods and types generated during compilation are a subset of method and types scanned
                bool scanningFail = false;
                DiffCompilationResults(ref scanningFail, compilationResults.CompiledMethodBodies, scanResults.CompiledMethodBodies,
                    "Methods", "compiled", "scanned", method => !(method.GetTypicalMethodDefinition() is EcmaMethod));
                DiffCompilationResults(ref scanningFail, compilationResults.ConstructedEETypes, scanResults.ConstructedEETypes,
                    "EETypes", "compiled", "scanned", type => !(type.GetTypeDefinition() is EcmaType));

                // If optimizations are enabled, the results will for sure not match in the other direction due to inlining, etc.
                // But there's at least some value in checking the scanner doesn't expand the universe too much in debug.
                if (_optimizationMode == OptimizationMode.None)
                {
                    // Check that methods and types scanned are a subset of methods and types compiled

                    // If we find diffs here, they're not critical, but still might be causing a Size on Disk regression.
                    bool dummy = false;

                    // We additionally skip methods in SIMD module because there's just too many intrisics to handle and IL scanner
                    // doesn't expand them. They would show up as noisy diffs.
                    DiffCompilationResults(ref dummy, scanResults.CompiledMethodBodies, compilationResults.CompiledMethodBodies,
                    "Methods", "scanned", "compiled", method => !(method.GetTypicalMethodDefinition() is EcmaMethod) || simdHelper.IsInSimdModule(method.OwningType));
                    DiffCompilationResults(ref dummy, scanResults.ConstructedEETypes, compilationResults.ConstructedEETypes,
                        "EETypes", "scanned", "compiled", type => !(type.GetTypeDefinition() is EcmaType));
                }

                if (scanningFail)
                    throw new Exception("Scanning failure");
            }

            if (debugInfoProvider is IDisposable)
                ((IDisposable)debugInfoProvider).Dispose();

            return 0;
        }

        [System.Diagnostics.Conditional("DEBUG")]
        private void DiffCompilationResults<T>(ref bool result, IEnumerable<T> set1, IEnumerable<T> set2, string prefix,
            string set1name, string set2name, Predicate<T> filter)
        {
            HashSet<T> diff = new HashSet<T>(set1);
            diff.ExceptWith(set2);

            // TODO: move ownership of compiler-generated entities to CompilerTypeSystemContext.
            // https://github.com/dotnet/corert/issues/3873
            diff.RemoveWhere(filter);

            if (diff.Count > 0)
            {
                result = true;

                Console.WriteLine($"*** {prefix} {set1name} but not {set2name}:");

                foreach (var d in diff)
                {
                    Console.WriteLine(d.ToString());
                }
            }
        }

        private TypeDesc FindType(CompilerTypeSystemContext context, string typeName)
        {
            ModuleDesc systemModule = context.SystemModule;

            TypeDesc foundType = systemModule.GetTypeByCustomAttributeTypeName(typeName, false, (typeDefName, module, throwIfNotFound) =>
            {
                return (MetadataType)context.GetCanonType(typeDefName)
                    ?? CustomAttributeTypeNameParser.ResolveCustomAttributeTypeDefinitionName(typeDefName, module, throwIfNotFound);
            });
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

        private static bool DumpReproArguments(CodeGenerationFailedException ex)
        {
            Console.WriteLine("To repro, add following arguments to the command line:");

            MethodDesc failingMethod = ex.Method;

            var formatter = new CustomAttributeTypeNameFormatter((IAssemblyDesc)failingMethod.Context.SystemModule);

            Console.Write($"--singlemethodtypename {formatter.FormatName(failingMethod.OwningType)}");
            Console.Write($" --singlemethodname {failingMethod.Name}");

            for (int i = 0; i < failingMethod.Instantiation.Length; i++)
                Console.Write($" --singlemethodgenericarg {formatter.FormatName(failingMethod.Instantiation[i])}");

            return false;
        }

        private static int Main(string[] args)
        {
#if DEBUG
            try
            {
                return new Program().Run(args);
            }
            catch (CodeGenerationFailedException ex) when (DumpReproArguments(ex))
            {
                throw new NotSupportedException(); // Unreachable
            }
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
