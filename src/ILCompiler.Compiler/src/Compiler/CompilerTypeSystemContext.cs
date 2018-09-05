// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using Internal.IL;

using Interlocked = System.Threading.Interlocked;

namespace ILCompiler
{
    public partial class CompilerTypeSystemContext : MetadataTypeSystemContext, IMetadataStringDecoderProvider
    {
        private MetadataFieldLayoutAlgorithm _metadataFieldLayoutAlgorithm = new CompilerMetadataFieldLayoutAlgorithm();
        private RuntimeDeterminedFieldLayoutAlgorithm _runtimeDeterminedFieldLayoutAlgorithm = new RuntimeDeterminedFieldLayoutAlgorithm();
        private VectorOfTFieldLayoutAlgorithm _vectorOfTFieldLayoutAlgorithm;
        private MetadataRuntimeInterfacesAlgorithm _metadataRuntimeInterfacesAlgorithm = new MetadataRuntimeInterfacesAlgorithm();
        private ArrayOfTRuntimeInterfacesAlgorithm _arrayOfTRuntimeInterfacesAlgorithm;
        private MetadataVirtualMethodAlgorithm _virtualMethodAlgorithm = new MetadataVirtualMethodAlgorithm();

        private SimdHelper _simdHelper;

        private TypeDesc[] _arrayOfTInterfaces;
        
        private MetadataStringDecoder _metadataStringDecoder;

        private class ModuleData
        {
            public string SimpleName;
            public string FilePath;

            public EcmaModule Module;
            public MemoryMappedViewAccessor MappedViewAccessor;
        }

        private class ModuleHashtable : LockFreeReaderHashtable<EcmaModule, ModuleData>
        {
            protected override int GetKeyHashCode(EcmaModule key)
            {
                return key.GetHashCode();
            }
            protected override int GetValueHashCode(ModuleData value)
            {
                return value.Module.GetHashCode();
            }
            protected override bool CompareKeyToValue(EcmaModule key, ModuleData value)
            {
                return Object.ReferenceEquals(key, value.Module);
            }
            protected override bool CompareValueToValue(ModuleData value1, ModuleData value2)
            {
                return Object.ReferenceEquals(value1.Module, value2.Module);
            }
            protected override ModuleData CreateValueFromKey(EcmaModule key)
            {
                Debug.Fail("CreateValueFromKey not supported");
                return null;
            }
        }
        private ModuleHashtable _moduleHashtable = new ModuleHashtable();

        private class SimpleNameHashtable : LockFreeReaderHashtable<string, ModuleData>
        {
            StringComparer _comparer = StringComparer.OrdinalIgnoreCase;

            protected override int GetKeyHashCode(string key)
            {
                return _comparer.GetHashCode(key);
            }
            protected override int GetValueHashCode(ModuleData value)
            {
                return _comparer.GetHashCode(value.SimpleName);
            }
            protected override bool CompareKeyToValue(string key, ModuleData value)
            {
                return _comparer.Equals(key, value.SimpleName);
            }
            protected override bool CompareValueToValue(ModuleData value1, ModuleData value2)
            {
                return _comparer.Equals(value1.SimpleName, value2.SimpleName);
            }
            protected override ModuleData CreateValueFromKey(string key)
            {
                Debug.Fail("CreateValueFromKey not supported");
                return null;
            }
        }
        private SimpleNameHashtable _simpleNameHashtable = new SimpleNameHashtable();

        private SharedGenericsMode _genericsMode;
        
        public CompilerTypeSystemContext(TargetDetails details, SharedGenericsMode genericsMode)
            : base(details)
        {
            _genericsMode = genericsMode;

            _vectorOfTFieldLayoutAlgorithm = new VectorOfTFieldLayoutAlgorithm(_metadataFieldLayoutAlgorithm);

            GenericsConfig = new SharedGenericsConfiguration();
        }

        public SharedGenericsConfiguration GenericsConfig
        {
            get;
        }

        public IReadOnlyDictionary<string, string> InputFilePaths
        {
            get;
            set;
        }

        public IReadOnlyDictionary<string, string> ReferenceFilePaths
        {
            get;
            set;
        }

        private bool _supportsLazyCctors;

        public override void SetSystemModule(ModuleDesc systemModule)
        {
            base.SetSystemModule(systemModule);
            _supportsLazyCctors = systemModule.GetType("System.Runtime.CompilerServices", "ClassConstructorRunner", false) != null;
        }

        public override ModuleDesc ResolveAssembly(System.Reflection.AssemblyName name, bool throwIfNotFound)
        {
            // TODO: catch typesystem BadImageFormatException and throw a new one that also captures the
            // assembly name that caused the failure. (Along with the reason, which makes this rather annoying).
            return GetModuleForSimpleName(name.Name, throwIfNotFound);
        }

        public ModuleDesc GetModuleForSimpleName(string simpleName, bool throwIfNotFound = true)
        {
            ModuleData existing;
            if (_simpleNameHashtable.TryGetValue(simpleName, out existing))
                return existing.Module;

            string filePath;
            if (!InputFilePaths.TryGetValue(simpleName, out filePath))
            {
                if (!ReferenceFilePaths.TryGetValue(simpleName, out filePath))
                {
                    // We allow the CanonTypesModule to not be an EcmaModule.
                    if (((IAssemblyDesc)CanonTypesModule).GetName().Name == simpleName)
                        return CanonTypesModule;

                    // TODO: the exception is wrong for two reasons: for one, this should be assembly full name, not simple name.
                    // The other reason is that on CoreCLR, the exception also captures the reason. We should be passing two
                    // string IDs. This makes this rather annoying.
                    if (throwIfNotFound)
                        ThrowHelper.ThrowFileNotFoundException(ExceptionStringID.FileLoadErrorGeneric, simpleName);
                    return null;
                }
            }

            return AddModule(filePath, simpleName, true);
        }

        public EcmaModule GetModuleFromPath(string filePath)
        {
            return GetOrAddModuleFromPath(filePath, true);
        }

        public EcmaModule GetMetadataOnlyModuleFromPath(string filePath)
        {
            return GetOrAddModuleFromPath(filePath, false);
        }

        private EcmaModule GetOrAddModuleFromPath(string filePath, bool useForBinding)
        {
            // This method is not expected to be called frequently. Linear search is acceptable.
            foreach (var entry in ModuleHashtable.Enumerator.Get(_moduleHashtable))
            {
                if (entry.FilePath == filePath)
                    return entry.Module;
            }

            return AddModule(filePath, null, useForBinding);
        }

        public static unsafe PEReader OpenPEFile(string filePath, out MemoryMappedViewAccessor mappedViewAccessor)
        {
            // System.Reflection.Metadata has heuristic that tries to save virtual address space. This heuristic does not work
            // well for us since it can make IL access very slow (call to OS for each method IL query). We will map the file
            // ourselves to get the desired performance characteristics reliably.

            FileStream fileStream = null;
            MemoryMappedFile mappedFile = null;
            MemoryMappedViewAccessor accessor = null;
            try
            {
                // Create stream because CreateFromFile(string, ...) uses FileShare.None which is too strict
                fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, false);
                mappedFile = MemoryMappedFile.CreateFromFile(
                    fileStream, null, fileStream.Length, MemoryMappedFileAccess.Read, HandleInheritability.None, true);
                accessor = mappedFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

                var safeBuffer = accessor.SafeMemoryMappedViewHandle;
                var peReader = new PEReader((byte*)safeBuffer.DangerousGetHandle(), (int)safeBuffer.ByteLength);

                // MemoryMappedFile does not need to be kept around. MemoryMappedViewAccessor is enough.

                mappedViewAccessor = accessor;
                accessor = null;

                return peReader;
            }
            finally
            {
                if (accessor != null)
                    accessor.Dispose();
                if (mappedFile != null)
                    mappedFile.Dispose();
                if (fileStream != null)
                    fileStream.Dispose();
            }
        }

        private EcmaModule AddModule(string filePath, string expectedSimpleName, bool useForBinding)
        {
            MemoryMappedViewAccessor mappedViewAccessor = null;
            PdbSymbolReader pdbReader = null;
            try
            {
                PEReader peReader = OpenPEFile(filePath, out mappedViewAccessor);
                pdbReader = OpenAssociatedSymbolFile(filePath, peReader);

                EcmaModule module = EcmaModule.Create(this, peReader, pdbReader);

                MetadataReader metadataReader = module.MetadataReader;
                string simpleName = metadataReader.GetString(metadataReader.GetAssemblyDefinition().Name);

                if (expectedSimpleName != null && !simpleName.Equals(expectedSimpleName, StringComparison.OrdinalIgnoreCase))
                    throw new FileNotFoundException("Assembly name does not match filename " + filePath);

                ModuleData moduleData = new ModuleData()
                {
                    SimpleName = simpleName,
                    FilePath = filePath,
                    Module = module,
                    MappedViewAccessor = mappedViewAccessor
                };

                lock (this)
                {
                    if (useForBinding)
                    {
                        ModuleData actualModuleData = _simpleNameHashtable.AddOrGetExisting(moduleData);
                        if (actualModuleData != moduleData)
                        {
                            if (actualModuleData.FilePath != filePath)
                                throw new FileNotFoundException("Module with same simple name already exists " + filePath);
                            return actualModuleData.Module;
                        }
                    }
                    mappedViewAccessor = null; // Ownership has been transfered
                    pdbReader = null; // Ownership has been transferred

                    _moduleHashtable.AddOrGetExisting(moduleData);
                }

                return module;
            }
            finally
            {
                if (mappedViewAccessor != null)
                    mappedViewAccessor.Dispose();
                if (pdbReader != null)
                    pdbReader.Dispose();
            }
        }

        public override FieldLayoutAlgorithm GetLayoutAlgorithmForType(DefType type)
        {
            if (type == UniversalCanonType)
                return UniversalCanonLayoutAlgorithm.Instance;
            else if (type.IsRuntimeDeterminedType)
                return _runtimeDeterminedFieldLayoutAlgorithm;
            else if (_simdHelper.IsVectorOfT(type))
                return _vectorOfTFieldLayoutAlgorithm;
            else
                return _metadataFieldLayoutAlgorithm;
        }

        protected override RuntimeInterfacesAlgorithm GetRuntimeInterfacesAlgorithmForNonPointerArrayType(ArrayType type)
        {
            if (_arrayOfTRuntimeInterfacesAlgorithm == null)
            {
                _arrayOfTRuntimeInterfacesAlgorithm = new ArrayOfTRuntimeInterfacesAlgorithm(SystemModule.GetKnownType("System", "Array`1"));
            }
            return _arrayOfTRuntimeInterfacesAlgorithm;
        }

        protected override RuntimeInterfacesAlgorithm GetRuntimeInterfacesAlgorithmForDefType(DefType type)
        {
            return _metadataRuntimeInterfacesAlgorithm;
        }

        /// <summary>
        /// Returns true if <paramref name="type"/> is a generic interface type implemented by arrays.
        /// </summary>
        public bool IsGenericArrayInterfaceType(TypeDesc type)
        {
            // Hardcode the fact that all generic interfaces on array types have arity 1
            if (!type.IsInterface || type.Instantiation.Length != 1)
                return false;

            if (_arrayOfTInterfaces == null)
            {
                DefType[] implementedInterfaces = SystemModule.GetKnownType("System", "Array`1").ExplicitlyImplementedInterfaces;
                TypeDesc[] interfaceDefinitions = new TypeDesc[implementedInterfaces.Length];
                for (int i = 0; i < interfaceDefinitions.Length; i++)
                    interfaceDefinitions[i] = implementedInterfaces[i].GetTypeDefinition();
                Interlocked.CompareExchange(ref _arrayOfTInterfaces, interfaceDefinitions, null);
            }

            TypeDesc interfaceDefinition = type.GetTypeDefinition();
            foreach (var arrayInterfaceDefinition in _arrayOfTInterfaces)
            {
                if (interfaceDefinition == arrayInterfaceDefinition)
                    return true;
            }

            return false;
        }

        public override VirtualMethodAlgorithm GetVirtualMethodAlgorithmForType(TypeDesc type)
        {
            Debug.Assert(!type.IsArray, "Wanted to call GetClosestMetadataType?");

            return _virtualMethodAlgorithm;
        }

        protected override IEnumerable<MethodDesc> GetAllMethods(TypeDesc type)
        {
            if (type.IsDelegate)
            {
                return GetAllMethodsForDelegate(type);
            }
            else if (type.IsEnum)
            {
                return GetAllMethodsForEnum(type);
            }
            else if (type.IsValueType)
            {
                return GetAllMethodsForValueType(type);
            }

            return type.GetMethods();
        }

        protected virtual IEnumerable<MethodDesc> GetAllMethodsForDelegate(TypeDesc type)
        {
            // Inject the synthetic methods that support the implementation of the delegate.
            InstantiatedType instantiatedType = type as InstantiatedType;
            if (instantiatedType != null)
            {
                DelegateInfo info = GetDelegateInfo(type.GetTypeDefinition());
                foreach (MethodDesc syntheticMethod in info.Methods)
                    yield return GetMethodForInstantiatedType(syntheticMethod, instantiatedType);
            }
            else
            {
                DelegateInfo info = GetDelegateInfo(type);
                foreach (MethodDesc syntheticMethod in info.Methods)
                    yield return syntheticMethod;
            }

            // Append all the methods defined in metadata
            foreach (var m in type.GetMethods())
                yield return m;
        }

        protected override Instantiation ConvertInstantiationToCanonForm(Instantiation instantiation, CanonicalFormKind kind, out bool changed)
        {
            if (_genericsMode == SharedGenericsMode.CanonicalReferenceTypes)
                return RuntimeDeterminedCanonicalizationAlgorithm.ConvertInstantiationToCanonForm(instantiation, kind, out changed);

            Debug.Assert(_genericsMode == SharedGenericsMode.Disabled);
            changed = false;
            return instantiation;
        }

        protected override TypeDesc ConvertToCanon(TypeDesc typeToConvert, CanonicalFormKind kind)
        {
            if (_genericsMode == SharedGenericsMode.CanonicalReferenceTypes)
                return RuntimeDeterminedCanonicalizationAlgorithm.ConvertToCanon(typeToConvert, kind);

            Debug.Assert(_genericsMode == SharedGenericsMode.Disabled);
            return typeToConvert;
        }

        protected override TypeDesc ConvertToCanon(TypeDesc typeToConvert, ref CanonicalFormKind kind)
        {
            if (_genericsMode == SharedGenericsMode.CanonicalReferenceTypes)
                return RuntimeDeterminedCanonicalizationAlgorithm.ConvertToCanon(typeToConvert, ref kind);

            Debug.Assert(_genericsMode == SharedGenericsMode.Disabled);
            return typeToConvert;
        }

        public override bool SupportsUniversalCanon => false;
        public override bool SupportsCanon => _genericsMode != SharedGenericsMode.Disabled;

        public MetadataStringDecoder GetMetadataStringDecoder()
        {
            if (_metadataStringDecoder == null)
                _metadataStringDecoder = new CachingMetadataStringDecoder(0x10000); // TODO: Tune the size
            return _metadataStringDecoder;
        }

        protected override bool ComputeHasGCStaticBase(FieldDesc field)
        {
            Debug.Assert(field.IsStatic);

            if (field.IsThreadStatic)
                return true;

            TypeDesc fieldType = field.FieldType;
            if (fieldType.IsValueType)
                return ((DefType)fieldType).ContainsGCPointers;
            else
                return fieldType.IsGCPointer;
        }

        //
        // Symbols
        //

        private PdbSymbolReader OpenAssociatedSymbolFile(string peFilePath, PEReader peReader)
        {
            // Assume that the .pdb file is next to the binary
            var pdbFilename = Path.ChangeExtension(peFilePath, ".pdb");
            string searchPath = "";

            if (!File.Exists(pdbFilename))
            {
                pdbFilename = null;

                // If the file doesn't exist, try the path specified in the CodeView section of the image
                foreach (DebugDirectoryEntry debugEntry in peReader.ReadDebugDirectory())
                {
                    if (debugEntry.Type != DebugDirectoryEntryType.CodeView)
                        continue;

                    string candidateFileName = peReader.ReadCodeViewDebugDirectoryData(debugEntry).Path;
                    if (Path.IsPathRooted(candidateFileName) && File.Exists(candidateFileName))
                    {
                        pdbFilename = candidateFileName;
                        searchPath = Path.GetDirectoryName(pdbFilename);
                        break;
                    }
                }

                if (pdbFilename == null)
                    return null;
            }

            // Try to open the symbol file as portable pdb first
            PdbSymbolReader reader = PortablePdbSymbolReader.TryOpen(pdbFilename, GetMetadataStringDecoder());
            if (reader == null)
            {
                // Fallback to the diasymreader for non-portable pdbs
                reader = UnmanagedPdbSymbolReader.TryOpenSymbolReaderForMetadataFile(peFilePath, searchPath);
            }

            return reader;
        }
    }

    /// <summary>
    /// Specifies the mode in which canonicalization should occur.
    /// </summary>
    public enum SharedGenericsMode
    {
        Disabled,
        CanonicalReferenceTypes,
    }

    public class SharedGenericsConfiguration
    {
        //
        // Universal Shared Generics heuristics magic values determined empirically
        //
        public long UniversalCanonGVMReflectionRootHeuristic_InstantiationCount { get; }
        public long UniversalCanonGVMDepthHeuristic_NonCanonDepth { get; }
        public long UniversalCanonGVMDepthHeuristic_CanonDepth { get; }

        // Controls how many different instantiations of a generic method, or method on generic type
        // should be allowed before trying to fall back to only supplying USG in the reflection
        // method table.
        public long UniversalCanonReflectionMethodRootHeuristic_InstantiationCount { get; }

        // To avoid infinite generic recursion issues during debug type record generation, attempt to 
        // use canonical form for types with high generic complexity. 
        public long MaxGenericDepthOfDebugRecord { get; }

        public SharedGenericsConfiguration()
        {
            UniversalCanonGVMReflectionRootHeuristic_InstantiationCount = 4;
            UniversalCanonGVMDepthHeuristic_NonCanonDepth = 2;
            UniversalCanonGVMDepthHeuristic_CanonDepth = 1;

            // Unlike the GVM heuristics which are intended to kick in aggressively
            // this heuristic exists to make it so that a fair amount of generic
            // expansion is allowed. Numbers are chosen to allow a fairly large
            // amount of generic expansion before trimming.
            UniversalCanonReflectionMethodRootHeuristic_InstantiationCount = 1024;

            MaxGenericDepthOfDebugRecord = 15;
        }
    };
}
