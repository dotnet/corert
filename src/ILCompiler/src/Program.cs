// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.CommandLine;
using System.Runtime.InteropServices;

using Internal.IL;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using Internal.CommandLine;

using Debug = System.Diagnostics.Debug;
using InstructionSet = Internal.JitInterface.InstructionSet;

namespace ILCompiler
{
    internal class Program
    {
        private const string DefaultSystemModule = "System.Private.CoreLib";

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
        private string _systemModuleName = DefaultSystemModule;
        private bool _multiFile;
        private bool _nativeLib;
        private string _exportsFile;
        private bool _useScanner;
        private bool _noScanner;
        private bool _preinitStatics;
        private bool _noPreinitStatics;
        private bool _emitStackTraceData;
        private string _mapFileName;
        private string _metadataLogFileName;
        private bool _noMetadataBlocking;
        private bool _disableReflection;
        private bool _completeTypesMetadata;
        private bool _scanReflection;
        private bool _methodBodyFolding;
        private bool _singleThreaded;
        private string _instructionSet;

        private string _singleMethodTypeName;
        private string _singleMethodName;
        private IReadOnlyList<string> _singleMethodGenericArgs;

        private bool _rootAllApplicationAssemblies;

        private IReadOnlyList<string> _codegenOptions = Array.Empty<string>();

        private IReadOnlyList<string> _rdXmlFilePaths = Array.Empty<string>();

        private IReadOnlyList<string> _initAssemblies = Array.Empty<string>();

        private IReadOnlyList<string> _appContextSwitches = Array.Empty<string>();

        private IReadOnlyList<string> _runtimeOptions = Array.Empty<string>();

        private IReadOnlyList<string> _removedFeatures = Array.Empty<string>();

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

            // Workaround for https://github.com/dotnet/corefx/issues/25267
            // If pointer size is 8, we're obviously not an X86 process...
            if (_targetArchitecture == TargetArchitecture.X86 && IntPtr.Size == 8)
                _targetArchitecture = TargetArchitecture.X64;
        }

        private ArgumentSyntax ParseCommandLine(string[] args)
        {
            IReadOnlyList<string> inputFiles = Array.Empty<string>();
            IReadOnlyList<string> referenceFiles = Array.Empty<string>();

            bool optimize = false;
            bool optimizeSpace = false;
            bool optimizeTime = false;

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
                syntax.DefineOption("Os", ref optimizeSpace, "Enable optimizations, favor code space");
                syntax.DefineOption("Ot", ref optimizeTime, "Enable optimizations, favor code speed");
                syntax.DefineOption("g", ref _enableDebugInfo, "Emit debugging information");
                syntax.DefineOption("cpp", ref _isCppCodegen, "Compile for C++ code-generation");
                syntax.DefineOption("wasm", ref _isWasmCodegen, "Compile for WebAssembly code-generation");
                syntax.DefineOption("nativelib", ref _nativeLib, "Compile as static or shared library");
                syntax.DefineOption("exportsfile", ref _exportsFile, "File to write exported method definitions");
                syntax.DefineOption("dgmllog", ref _dgmlLogFileName, "Save result of dependency analysis as DGML");
                syntax.DefineOption("fulllog", ref _generateFullDgmlLog, "Save detailed log of dependency analysis");
                syntax.DefineOption("scandgmllog", ref _scanDgmlLogFileName, "Save result of scanner dependency analysis as DGML");
                syntax.DefineOption("scanfulllog", ref _generateFullScanDgmlLog, "Save detailed log of scanner dependency analysis");
                syntax.DefineOption("verbose", ref _isVerbose, "Enable verbose logging");
                syntax.DefineOption("systemmodule", ref _systemModuleName, "System module name (default: System.Private.CoreLib)");
                syntax.DefineOption("multifile", ref _multiFile, "Compile only input files (do not compile referenced assemblies)");
                syntax.DefineOption("waitfordebugger", ref waitForDebugger, "Pause to give opportunity to attach debugger");
                syntax.DefineOptionList("codegenopt", ref _codegenOptions, "Define a codegen option");
                syntax.DefineOptionList("rdxml", ref _rdXmlFilePaths, "RD.XML file(s) for compilation");
                syntax.DefineOption("rootallapplicationassemblies", ref _rootAllApplicationAssemblies, "Consider all non-framework assemblies dynamically used");
                syntax.DefineOption("map", ref _mapFileName, "Generate a map file");
                syntax.DefineOption("metadatalog", ref _metadataLogFileName, "Generate a metadata log file");
                syntax.DefineOption("nometadatablocking", ref _noMetadataBlocking, "Ignore metadata blocking for internal implementation details");
                syntax.DefineOption("disablereflection", ref _disableReflection, "Disable generation of reflection metadata");
                syntax.DefineOption("completetypemetadata", ref _completeTypesMetadata, "Generate complete metadata for types");
                syntax.DefineOption("scanreflection", ref _scanReflection, "Scan IL for reflection patterns");
                syntax.DefineOption("scan", ref _useScanner, "Use IL scanner to generate optimized code (implied by -O)");
                syntax.DefineOption("noscan", ref _noScanner, "Do not use IL scanner to generate optimized code");
                syntax.DefineOption("ildump", ref _ilDump, "Dump IL assembly listing for compiler-generated IL");
                syntax.DefineOption("stacktracedata", ref _emitStackTraceData, "Emit data to support generating stack trace strings at runtime");
                syntax.DefineOption("methodbodyfolding", ref _methodBodyFolding, "Fold identical method bodies");
                syntax.DefineOptionList("initassembly", ref _initAssemblies, "Assembly(ies) with a library initializer");
                syntax.DefineOptionList("appcontextswitch", ref _appContextSwitches, "System.AppContext switches to set");
                syntax.DefineOptionList("runtimeopt", ref _runtimeOptions, "Runtime options to set");
                syntax.DefineOptionList("removefeature", ref _removedFeatures, "Framework features to remove");
                syntax.DefineOption("singlethreaded", ref _singleThreaded, "Run compilation on a single thread");
                syntax.DefineOption("instructionset", ref _instructionSet, "Instruction set to allow or disallow");
                syntax.DefineOption("preinitstatics", ref _preinitStatics, "Interpret static constructors at compile time if possible (implied by -O)");
                syntax.DefineOption("nopreinitstatics", ref _noPreinitStatics, "Do not interpret static constructors at compile time");

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

            _optimizationMode = OptimizationMode.None;
            if (optimizeSpace)
            {
                if (optimizeTime)
                    Console.WriteLine("Warning: overriding -Ot with -Os");
                _optimizationMode = OptimizationMode.PreferSize;
            }
            else if (optimizeTime)
                _optimizationMode = OptimizationMode.PreferSpeed;
            else if (optimize)
                _optimizationMode = OptimizationMode.Blended;

            foreach (var input in inputFiles)
                Helpers.AppendExpandedPaths(_inputFilePaths, input, true);

            foreach (var reference in referenceFiles)
                Helpers.AppendExpandedPaths(_referenceFilePaths, reference, false);

            return argSyntax;
        }

        private IReadOnlyCollection<MethodDesc> CreateInitializerList(TypeSystemContext context)
        {
            List<ModuleDesc> assembliesWithInitalizers = new List<ModuleDesc>();

            // Build a list of assemblies that have an initializer that needs to run before
            // any user code runs.
            foreach (string initAssemblyName in _initAssemblies)
            {
                ModuleDesc assembly = context.ResolveAssembly(new AssemblyName(initAssemblyName));
                assembliesWithInitalizers.Add(assembly);
            }

            var libraryInitializers = new LibraryInitializers(context, assembliesWithInitalizers);

            List<MethodDesc> initializerList = new List<MethodDesc>(libraryInitializers.LibraryInitializerMethods);

            // If there are any AppContext switches the user wishes to enable, generate code that sets them.
            if (_appContextSwitches.Count > 0)
            {
                MethodDesc appContextInitMethod = new Internal.IL.Stubs.StartupCode.AppContextInitializerMethod(
                    context.GeneratedAssembly.GetGlobalModuleType(), _appContextSwitches);
                initializerList.Add(appContextInitMethod);
            }

            return initializerList;
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
                    _targetArchitecture = TargetArchitecture.ARM;
                else if (_targetArchitectureStr.Equals("arm64", StringComparison.OrdinalIgnoreCase))
                    _targetArchitecture = TargetArchitecture.ARM64;
                else if (_targetArchitectureStr.Equals("wasm", StringComparison.OrdinalIgnoreCase))
                {
                    _targetArchitecture = TargetArchitecture.Wasm32;
                    _isWasmCodegen = true;
                }
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

            InstructionSetSupportBuilder instructionSetSupportBuilder = new InstructionSetSupportBuilder(_targetArchitecture);

            // The runtime expects certain baselines that the codegen can assume as well.
            if ((_targetArchitecture == TargetArchitecture.X86) || (_targetArchitecture == TargetArchitecture.X64))
            {
                instructionSetSupportBuilder.AddSupportedInstructionSet("sse");
                instructionSetSupportBuilder.AddSupportedInstructionSet("sse2");
            }
            else if (_targetArchitecture == TargetArchitecture.ARM64)
            {
                instructionSetSupportBuilder.AddSupportedInstructionSet("base");
                instructionSetSupportBuilder.AddSupportedInstructionSet("neon");
            }

            if (_instructionSet != null)
            {
                List<string> instructionSetParams = new List<string>();

                // Normalize instruction set format to include implied +.
                string[] instructionSetParamsInput = _instructionSet.Split(',');
                for (int i = 0; i < instructionSetParamsInput.Length; i++)
                {
                    string instructionSet = instructionSetParamsInput[i];

                    if (String.IsNullOrEmpty(instructionSet))
                        throw new CommandLineException("Instruction set must not be empty");

                    char firstChar = instructionSet[0];
                    if ((firstChar != '+') && (firstChar != '-'))
                    {
                        instructionSet = "+" + instructionSet;
                    }
                    instructionSetParams.Add(instructionSet);
                }

                Dictionary<string, bool> instructionSetSpecification = new Dictionary<string, bool>();
                foreach (string instructionSetSpecifier in instructionSetParams)
                {
                    string instructionSet = instructionSetSpecifier.Substring(1, instructionSetSpecifier.Length - 1);

                    bool enabled = instructionSetSpecifier[0] == '+' ? true : false;
                    if (enabled)
                    {
                        if (!instructionSetSupportBuilder.AddSupportedInstructionSet(instructionSet))
                            throw new CommandLineException($"Unrecognized instruction set '{instructionSet}'");
                    }
                    else
                    {
                        if (!instructionSetSupportBuilder.RemoveInstructionSetSupport(instructionSet))
                            throw new CommandLineException($"Unrecognized instruction set '{instructionSet}'");
                    }
                }
            }

            instructionSetSupportBuilder.ComputeInstructionSetFlags(out var supportedInstructionSet, out var unsupportedInstructionSet,
                (string specifiedInstructionSet, string impliedInstructionSet) =>
                    throw new CommandLineException(String.Format("Unsupported combination of instruction sets: {0}/{1}", specifiedInstructionSet, impliedInstructionSet)));

            InstructionSetSupportBuilder optimisticInstructionSetSupportBuilder = new InstructionSetSupportBuilder(_targetArchitecture);

            // Optimistically assume some instruction sets are present.
            if ((_targetArchitecture == TargetArchitecture.X86) || (_targetArchitecture == TargetArchitecture.X64))
            {
                // We set these hardware features as enabled always, as most
                // of hardware in the wild supports them. Note that we do not indicate support for AVX, or any other
                // instruction set which uses the VEX encodings as the presence of those makes otherwise acceptable
                // code be unusable on hardware which does not support VEX encodings, as well as emulators that do not
                // support AVX instructions.
                //
                // The compiler is able to generate runtime IsSupported checks for the following instruction sets.
                optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("ssse3");
                optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("aes");
                optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("pclmul");
                optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("lzcnt");

                // NOTE: we don't optimistically enable SSE4.1/SSE4.2 because RyuJIT can opportunistically use
                // these instructions in e.g. optimizing Math.Round or Vector<T> operations without IsSupported guards.

                // If SSE4.2 was enabled, we can also opportunistically enable POPCNT
                Debug.Assert(InstructionSet.X64_SSE42 == InstructionSet.X86_SSE42);
                if (supportedInstructionSet.HasInstructionSet(InstructionSet.X64_SSE42))
                {
                    optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("popcnt");
                }

                // If AVX was enabled, we can opportunistically enable FMA/BMI
                Debug.Assert(InstructionSet.X64_AVX == InstructionSet.X86_AVX);
                if (supportedInstructionSet.HasInstructionSet(InstructionSet.X64_AVX))
                {
                    optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("fma");
                    optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("bmi1");
                    optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("bmi2");
                }
            }
            else if (_targetArchitecture == TargetArchitecture.ARM64)
            {
                optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("aes");
                optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("crc");
                optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("sha1");
                optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("sha2");
                optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("lse");
            }

            optimisticInstructionSetSupportBuilder.ComputeInstructionSetFlags(out var optimisticInstructionSet, out _,
                (string specifiedInstructionSet, string impliedInstructionSet) => throw new NotSupportedException());
            optimisticInstructionSet.Remove(unsupportedInstructionSet);
            optimisticInstructionSet.Add(supportedInstructionSet);

            var instructionSetSupport = new InstructionSetSupport(supportedInstructionSet,
                                                                  unsupportedInstructionSet,
                                                                  optimisticInstructionSet,
                                                                  InstructionSetSupportBuilder.GetNonSpecifiableInstructionSetsForArch(_targetArchitecture),
                                                                  _targetArchitecture);

            bool supportsReflection = !_disableReflection && _systemModuleName == DefaultSystemModule;

            //
            // Initialize type system context
            //

            SharedGenericsMode genericsMode = SharedGenericsMode.CanonicalReferenceTypes;

            var simdVectorLength = (_isCppCodegen || _isWasmCodegen) ? SimdVectorLength.None : instructionSetSupport.GetVectorTSimdVector();
            var targetAbi = _isCppCodegen ? TargetAbi.CppCodegen : TargetAbi.CoreRT;
            var targetDetails = new TargetDetails(_targetArchitecture, _targetOS, targetAbi, simdVectorLength);
            CompilerTypeSystemContext typeSystemContext = 
                new CompilerTypeSystemContext(targetDetails, genericsMode, supportsReflection ? DelegateFeature.All : 0);

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
            if (!typeSystemContext.InputFilePaths.ContainsKey(_systemModuleName)
                && !typeSystemContext.ReferenceFilePaths.ContainsKey(_systemModuleName))
                throw new CommandLineException($"System module {_systemModuleName} does not exists. Make sure that you specify --systemmodule");

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
                bool systemModuleIsInputModule = false;
                foreach (var inputFile in typeSystemContext.InputFilePaths)
                {
                    EcmaModule module = typeSystemContext.GetModuleFromPath(inputFile.Value);

                    if (module.PEReader.PEHeaders.IsExe)
                    {
                        if (entrypointModule != null)
                            throw new Exception("Multiple EXE modules");
                        entrypointModule = module;
                    }

                    if (module == typeSystemContext.SystemModule)
                        systemModuleIsInputModule = true;

                    compilationRoots.Add(new ExportedMethodsRootProvider(module));
                }

                if (entrypointModule != null)
                {
                    compilationRoots.Add(new MainMethodRootProvider(entrypointModule, CreateInitializerList(typeSystemContext)));
                    compilationRoots.Add(new RuntimeConfigurationRootProvider(_runtimeOptions));
                    compilationRoots.Add(new ExpectedIsaFeaturesRootProvider(instructionSetSupport));
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
                    if (entrypointModule == null && !_nativeLib)
                        throw new Exception("No entrypoint module");

                    if (!systemModuleIsInputModule)
                        compilationRoots.Add(new ExportedMethodsRootProvider((EcmaModule)typeSystemContext.SystemModule));
                    compilationGroup = new SingleFileCompilationModuleGroup();
                }

                if (_nativeLib)
                {
                    // Set owning module of generated native library startup method to compiler generated module,
                    // to ensure the startup method is included in the object file during multimodule mode build
                    compilationRoots.Add(new NativeLibraryInitializerRootProvider(typeSystemContext.GeneratedAssembly, CreateInitializerList(typeSystemContext)));
                    compilationRoots.Add(new RuntimeConfigurationRootProvider(_runtimeOptions));
                    compilationRoots.Add(new ExpectedIsaFeaturesRootProvider(instructionSetSupport));
                }

                if (_rdXmlFilePaths.Count > 0)
                    Console.WriteLine("Warning: RD.XML processing will change before release (https://github.com/dotnet/corert/issues/5001)");
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

            string compilationUnitPrefix = _multiFile ? System.IO.Path.GetFileNameWithoutExtension(_outputFilePath) : "";
            builder.UseCompilationUnitPrefix(compilationUnitPrefix);

            PInvokeILEmitterConfiguration pinvokePolicy;
            if (!_isCppCodegen && !_isWasmCodegen)
                pinvokePolicy = new ConfigurablePInvokePolicy(typeSystemContext.Target);
            else
                pinvokePolicy = new DirectPInvokePolicy();

            RemovedFeature removedFeatures = 0;
            foreach (string feature in _removedFeatures)
            {
                if (feature == "EventSource")
                    removedFeatures |= RemovedFeature.Etw;
                else if (feature == "FrameworkStrings")
                    removedFeatures |= RemovedFeature.FrameworkResources;
                else if (feature == "Globalization")
                    removedFeatures |= RemovedFeature.Globalization;
                else if (feature == "Comparers")
                    removedFeatures |= RemovedFeature.Comparers;
                else if (feature == "SerializationGuard")
                    removedFeatures |= RemovedFeature.SerializationGuard;
                else if (feature == "XmlNonFileStream")
                    removedFeatures |= RemovedFeature.XmlDownloadNonFileStream;
            }

            ILProvider ilProvider = new CoreRTILProvider();

            if (removedFeatures != 0)
                ilProvider = new RemovingILProvider(ilProvider, removedFeatures);

            var stackTracePolicy = _emitStackTraceData ?
                (StackTraceEmissionPolicy)new EcmaMethodStackTraceEmissionPolicy() : new NoStackTraceEmissionPolicy();

            MetadataBlockingPolicy mdBlockingPolicy;
            ManifestResourceBlockingPolicy resBlockingPolicy;
            UsageBasedMetadataGenerationOptions metadataGenerationOptions = UsageBasedMetadataGenerationOptions.IteropILScanning;
            if (supportsReflection)
            {
                mdBlockingPolicy = _noMetadataBlocking
                    ? (MetadataBlockingPolicy)new NoMetadataBlockingPolicy()
                    : new BlockedInternalsBlockingPolicy(typeSystemContext);

                resBlockingPolicy = (removedFeatures & RemovedFeature.FrameworkResources) != 0
                    ? new FrameworkStringResourceBlockingPolicy()
                    : (ManifestResourceBlockingPolicy)new NoManifestResourceBlockingPolicy();

                metadataGenerationOptions |= UsageBasedMetadataGenerationOptions.AnonymousTypeHeuristic;
                if (_completeTypesMetadata)
                    metadataGenerationOptions |= UsageBasedMetadataGenerationOptions.CompleteTypesOnly;
                if (_scanReflection)
                    metadataGenerationOptions |= UsageBasedMetadataGenerationOptions.ReflectionILScanning;
                if (_rootAllApplicationAssemblies)
                    metadataGenerationOptions |= UsageBasedMetadataGenerationOptions.FullUserAssemblyRooting;
            }
            else
            {
                mdBlockingPolicy = new FullyBlockedMetadataBlockingPolicy();
                resBlockingPolicy = new FullyBlockedManifestResourceBlockingPolicy();
            }

            DynamicInvokeThunkGenerationPolicy invokeThunkGenerationPolicy = new DefaultDynamicInvokeThunkGenerationPolicy();

            MetadataManager metadataManager = new UsageBasedMetadataManager(
                    compilationGroup,
                    typeSystemContext,
                    mdBlockingPolicy,
                    resBlockingPolicy,
                    _metadataLogFileName,
                    stackTracePolicy,
                    invokeThunkGenerationPolicy,
                    ilProvider,
                    metadataGenerationOptions);

            InteropStateManager interopStateManager = new InteropStateManager(typeSystemContext.GeneratedAssembly);
            InteropStubManager interopStubManager = new UsageBasedInteropStubManager(interopStateManager, pinvokePolicy);

            // Unless explicitly opted in at the command line, we enable scanner for retail builds by default.
            // We don't do this for CppCodegen and Wasm, because those codegens are behind.
            // We also don't do this for multifile because scanner doesn't simulate inlining (this would be
            // fixable by using a CompilationGroup for the scanner that has a bigger worldview, but
            // let's cross that bridge when we get there).
            bool useScanner = _useScanner ||
                (_optimizationMode != OptimizationMode.None && !_isCppCodegen && !_isWasmCodegen && !_multiFile);

            useScanner &= !_noScanner;

            // Enable static data preinitialization in optimized builds.
            bool preinitStatics = _preinitStatics ||
                (_optimizationMode != OptimizationMode.None && !_isCppCodegen && !_multiFile);
            preinitStatics &= !_noPreinitStatics;

            var preinitManager = new PreinitializationManager(typeSystemContext, compilationGroup, ilProvider, preinitStatics);
            builder
                .UseILProvider(ilProvider)
                .UsePreinitializationManager(preinitManager);

            ILScanResults scanResults = null;
            if (useScanner)
            {
                ILScannerBuilder scannerBuilder = builder.GetILScannerBuilder()
                    .UseCompilationRoots(compilationRoots)
                    .UseMetadataManager(metadataManager)
                    .UseSingleThread(enable: _singleThreaded)
                    .UseInteropStubManager(interopStubManager);

                if (_scanDgmlLogFileName != null)
                    scannerBuilder.UseDependencyTracking(_generateFullScanDgmlLog ? DependencyTrackingLevel.All : DependencyTrackingLevel.First);

                IILScanner scanner = scannerBuilder.ToILScanner();

                scanResults = scanner.Scan();

                metadataManager = ((UsageBasedMetadataManager)metadataManager).ToAnalysisBasedMetadataManager();

                interopStubManager = scanResults.GetInteropStubManager(interopStateManager, pinvokePolicy);
            }

            var logger = new Logger(Console.Out, _isVerbose);

            DebugInformationProvider debugInfoProvider = _enableDebugInfo ?
                (_ilDump == null ? new DebugInformationProvider() : new ILAssemblyGeneratingMethodDebugInfoProvider(_ilDump, new EcmaOnlyDebugInformationProvider())) :
                new NullDebugInformationProvider();

            DependencyTrackingLevel trackingLevel = _dgmlLogFileName == null ?
                DependencyTrackingLevel.None : (_generateFullDgmlLog ? DependencyTrackingLevel.All : DependencyTrackingLevel.First);

            compilationRoots.Add(metadataManager);
            compilationRoots.Add(interopStubManager);

            builder
                .UseInstructionSetSupport(instructionSetSupport)
                .UseBackendOptions(_codegenOptions)
                .UseMethodBodyFolding(enable: _methodBodyFolding)
                .UseSingleThread(enable: _singleThreaded)
                .UseMetadataManager(metadataManager)
                .UseInteropStubManager(interopStubManager)
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

                // If we feed any outputs of the scanner into the compilation, it's essential
                // we use scanner's devirtualization manager. It prevents optimizing codegens
                // from accidentally devirtualizing cases that can never happen at runtime
                // (e.g. devirtualizing a method on a type that never gets allocated).
                builder.UseDevirtualizationManager(scanResults.GetDevirtualizationManager());
            }

            ICompilation compilation = builder.ToCompilation();

            ObjectDumper dumper = _mapFileName != null ? new ObjectDumper(_mapFileName) : null;

            CompilationResults compilationResults = compilation.Compile(_outputFilePath, dumper);
            if (_exportsFile != null)
            {
                ExportsFileWriter defFileWriter = new ExportsFileWriter(typeSystemContext, _exportsFile);
                foreach (var compilationRoot in compilationRoots)
                {
                    if (compilationRoot is ExportedMethodsRootProvider provider)
                        defFileWriter.AddExportedMethods(provider.ExportedMethods);
                }

                defFileWriter.EmitExportedMethods();
            }

            if (_dgmlLogFileName != null)
                compilationResults.WriteDependencyLog(_dgmlLogFileName);

            if (scanResults != null)
            {
                if (_scanDgmlLogFileName != null)
                    scanResults.WriteDependencyLog(_scanDgmlLogFileName);

                // If the scanner and compiler don't agree on what to compile, the outputs of the scanner might not actually be usable.
                // We are going to check this two ways:
                // 1. The methods and types generated during compilation are a subset of method and types scanned
                // 2. The methods and types scanned are a subset of methods and types compiled (this has a chance to hold for unoptimized builds only).

                // Check that methods and types generated during compilation are a subset of method and types scanned
                bool scanningFail = false;
                DiffCompilationResults(ref scanningFail, compilationResults.CompiledMethodBodies, scanResults.CompiledMethodBodies,
                    "Methods", "compiled", "scanned", method => !(method.GetTypicalMethodDefinition() is EcmaMethod) || method.Name == "ThrowPlatformNotSupportedException" || method.Name == "ThrowArgumentOutOfRangeException");
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
                    "Methods", "scanned", "compiled", method => !(method.GetTypicalMethodDefinition() is EcmaMethod) || method.OwningType.IsIntrinsic);
                    DiffCompilationResults(ref dummy, scanResults.ConstructedEETypes, compilationResults.ConstructedEETypes,
                        "EETypes", "scanned", "compiled", type => !(type.GetTypeDefinition() is EcmaType));
                }

                if (scanningFail)
                    throw new Exception("Scanning failure");
            }

            if (debugInfoProvider is IDisposable)
                ((IDisposable)debugInfoProvider).Dispose();

            preinitManager.LogStatistics(logger);

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

            Console.Write($"--singlemethodtypename \"{formatter.FormatName(failingMethod.OwningType, true)}\"");
            Console.Write($" --singlemethodname {failingMethod.Name}");

            for (int i = 0; i < failingMethod.Instantiation.Length; i++)
                Console.Write($" --singlemethodgenericarg \"{formatter.FormatName(failingMethod.Instantiation[i], true)}\"");

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
