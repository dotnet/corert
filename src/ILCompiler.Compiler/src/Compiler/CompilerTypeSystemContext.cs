// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

using ILCompiler.SymbolReader;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler
{
    public class CompilerTypeSystemContext : TypeSystemContext, IMetadataStringDecoderProvider
    {
        private static readonly string[] s_wellKnownTypeNames = new string[] {
            "Void",
            "Boolean",
            "Char",
            "SByte",
            "Byte",
            "Int16",
            "UInt16",
            "Int32",
            "UInt32",
            "Int64",
            "UInt64",
            "IntPtr",
            "UIntPtr",
            "Single",
            "Double",

            "ValueType",
            "Enum",
            "Nullable`1",

            "Object",
            "String",
            "Array",
            "MulticastDelegate",

            "RuntimeTypeHandle",
            "RuntimeMethodHandle",
            "RuntimeFieldHandle",

            "Exception",
        };

        private MetadataType[] _wellKnownTypes = new MetadataType[s_wellKnownTypeNames.Length];

        private MetadataFieldLayoutAlgorithm _metadataFieldLayoutAlgorithm = new CompilerMetadataFieldLayoutAlgorithm();
        private MetadataRuntimeInterfacesAlgorithm _metadataRuntimeInterfacesAlgorithm = new MetadataRuntimeInterfacesAlgorithm();
        private ArrayOfTRuntimeInterfacesAlgorithm _arrayOfTRuntimeInterfacesAlgorithm;

        private MetadataStringDecoder _metadataStringDecoder;

        private class ModuleData
        {
            public string SimpleName;
            public string FilePath;

            public EcmaModule Module;
            public MemoryMappedViewAccessor MappedViewAccessor;

            public PdbSymbolReader PdbReader;
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
                Debug.Assert(false, "CreateValueFromKey not supported");
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
                Debug.Assert(false, "CreateValueFromKey not supported");
                return null;
            }
        }
        private SimpleNameHashtable _simpleNameHashtable = new SimpleNameHashtable();

        public CompilerTypeSystemContext(TargetDetails details)
            : base(details)
        {
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

        public void SetSystemModule(EcmaModule systemModule)
        {
            InitializeSystemModule(systemModule);

            // Sanity check the name table
            Debug.Assert(s_wellKnownTypeNames[(int)WellKnownType.MulticastDelegate - 1] == "MulticastDelegate");

            // Initialize all well known types - it will save us from checking the name for each loaded type
            for (int typeIndex = 0; typeIndex < _wellKnownTypes.Length; typeIndex++)
            {
                MetadataType type = systemModule.GetType("System", s_wellKnownTypeNames[typeIndex]);
                type.SetWellKnownType((WellKnownType)(typeIndex + 1));
                _wellKnownTypes[typeIndex] = type;
            }
        }

        public override DefType GetWellKnownType(WellKnownType wellKnownType)
        {
            return _wellKnownTypes[(int)wellKnownType - 1];
        }

        public override ModuleDesc ResolveAssembly(System.Reflection.AssemblyName name, bool throwIfNotFound)
        {
            return GetModuleForSimpleName(name.Name, throwIfNotFound);
        }

        public EcmaModule GetModuleForSimpleName(string simpleName, bool throwIfNotFound = true)
        {
            ModuleData existing;
            if (_simpleNameHashtable.TryGetValue(simpleName, out existing))
                return existing.Module;

            string filePath;
            if (!InputFilePaths.TryGetValue(simpleName, out filePath))
            {
                if (!ReferenceFilePaths.TryGetValue(simpleName, out filePath))
                {
                    if (throwIfNotFound)
                        throw new FileNotFoundException("Assembly not found: " + simpleName);
                    return null;
                }
            }

            return AddModule(filePath, simpleName);
        }

        public EcmaModule GetModuleFromPath(string filePath)
        {
            // This method is not expected to be called frequently. Linear search is acceptable.
            foreach (var entry in ModuleHashtable.Enumerator.Get(_moduleHashtable))
            {
                if (entry.FilePath == filePath)
                    return entry.Module;
            }

            return AddModule(filePath, null);
        }

        private unsafe static PEReader OpenPEFile(string filePath, out MemoryMappedViewAccessor mappedViewAccessor)
        {
            // System.Reflection.Metadata has heuristic that tries to save virtual address space. This heuristic does not work
            // well for us since it can make IL access very slow (call to OS for each method IL query). We will map the file
            // ourselves to get the desired performance characteristics reliably.

            MemoryMappedFile mappedFile = null;
            MemoryMappedViewAccessor accessor = null;

            try
            {
                mappedFile = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
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
            }
        }

        private EcmaModule AddModule(string filePath, string expectedSimpleName)
        {
            MemoryMappedViewAccessor mappedViewAccessor = null;
            try
            {
                PEReader peReader = OpenPEFile(filePath, out mappedViewAccessor);

                EcmaModule module = new EcmaModule(this, peReader);

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
                    ModuleData actualModuleData = _simpleNameHashtable.AddOrGetExisting(moduleData);
                    if (actualModuleData != moduleData)
                    {
                        if (actualModuleData.FilePath != filePath)
                            throw new FileNotFoundException("Module with same simple name already exists " + filePath);
                        return actualModuleData.Module;
                    }
                    mappedViewAccessor = null; // Ownership has been transfered

                    _moduleHashtable.AddOrGetExisting(moduleData);
                }

                // TODO: Thread-safety for symbol reading
                InitializeSymbolReader(moduleData);

                return module;
            }
            finally
            {
                if (mappedViewAccessor != null)
                    mappedViewAccessor.Dispose();
            }
        }

        public override FieldLayoutAlgorithm GetLayoutAlgorithmForType(DefType type)
        {
            return _metadataFieldLayoutAlgorithm;
        }

        public override RuntimeInterfacesAlgorithm GetRuntimeInterfacesAlgorithmForNonPointerArrayType(ArrayType type)
        {
            if (_arrayOfTRuntimeInterfacesAlgorithm == null)
            {
                _arrayOfTRuntimeInterfacesAlgorithm = new ArrayOfTRuntimeInterfacesAlgorithm(SystemModule.GetType("System", "Array`1"));
            }
            return _arrayOfTRuntimeInterfacesAlgorithm;
        }

        public override RuntimeInterfacesAlgorithm GetRuntimeInterfacesAlgorithmForMetadataType(MetadataType type)
        {
            return _metadataRuntimeInterfacesAlgorithm;
        }

        public MetadataStringDecoder GetMetadataStringDecoder()
        {
            if (_metadataStringDecoder == null)
                _metadataStringDecoder = new CachingMetadataStringDecoder(0x10000); // TODO: Tune the size
            return _metadataStringDecoder;
        }

        //
        // Symbols
        //

        private void InitializeSymbolReader(ModuleData moduleData)
        {
            // Assume that the .pdb file is next to the binary
            var pdbFilename = Path.ChangeExtension(moduleData.FilePath, ".pdb");

            if (!File.Exists(pdbFilename))
                return;

            // Try to open the symbol file as portable pdb first
            PdbSymbolReader reader = PortablePdbSymbolReader.TryOpen(pdbFilename, GetMetadataStringDecoder());
            if (reader == null)
            {
                // Fallback to the diasymreader for non-portable pdbs
                reader = UnmanagedPdbSymbolReader.TryOpenSymbolReaderForMetadataFile(moduleData.FilePath);
            }

            moduleData.PdbReader = reader;
        }

        public IEnumerable<ILSequencePoint> GetSequencePointsForMethod(MethodDesc method)
        {
            EcmaMethod ecmaMethod = method.GetTypicalMethodDefinition() as EcmaMethod;
            if (ecmaMethod == null)
                return null;

            ModuleData moduleData;
            _moduleHashtable.TryGetValue(ecmaMethod.Module, out moduleData);
            Debug.Assert(moduleData != null);

            if (moduleData.PdbReader == null)
                return null;

            return moduleData.PdbReader.GetSequencePointsForMethod(MetadataTokens.GetToken(ecmaMethod.Handle));
        }

        public IEnumerable<ILLocalVariable> GetLocalVariableNamesForMethod(MethodDesc method)
        {
            EcmaMethod ecmaMethod = method.GetTypicalMethodDefinition() as EcmaMethod;
            if (ecmaMethod == null)
                return null;

            ModuleData moduleData;
            _moduleHashtable.TryGetValue(ecmaMethod.Module, out moduleData);
            Debug.Assert(moduleData != null);

            if (moduleData.PdbReader == null)
                return null;

            return moduleData.PdbReader.GetLocalVariableNamesForMethod(MetadataTokens.GetToken(ecmaMethod.Handle));
        }

        public IEnumerable<string> GetParameterNamesForMethod(MethodDesc method)
        {
            EcmaMethod ecmaMethod = method.GetTypicalMethodDefinition() as EcmaMethod;
            if (ecmaMethod == null)
                yield break;

            ParameterHandleCollection parameters = ecmaMethod.MetadataReader.GetMethodDefinition(ecmaMethod.Handle).GetParameters();

            if (!ecmaMethod.Signature.IsStatic)
            {
                yield return "_this";
            }

            foreach (var parameterHandle in parameters)
            {
                Parameter p = ecmaMethod.MetadataReader.GetParameter(parameterHandle);
                yield return ecmaMethod.MetadataReader.GetString(p.Name);
            }
        }
    }
}
