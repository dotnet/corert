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

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using Internal.IL;

namespace ILCompiler
{
    public class CompilerTypeSystemContext : MetadataTypeSystemContext, IMetadataStringDecoderProvider
    {
        private MetadataFieldLayoutAlgorithm _metadataFieldLayoutAlgorithm = new CompilerMetadataFieldLayoutAlgorithm();
        private MetadataRuntimeInterfacesAlgorithm _metadataRuntimeInterfacesAlgorithm = new MetadataRuntimeInterfacesAlgorithm();
        private ArrayOfTRuntimeInterfacesAlgorithm _arrayOfTRuntimeInterfacesAlgorithm;
        private MetadataVirtualMethodAlgorithm _virtualMethodAlgorithm = new MetadataVirtualMethodAlgorithm();
        private CachingVirtualMethodEnumerationAlgorithm _virtualMethodEnumAlgorithm = new CachingVirtualMethodEnumerationAlgorithm();
        private DelegateVirtualMethodEnumerationAlgorithm _delegateVirtualMethodEnumAlgorithm = new DelegateVirtualMethodEnumerationAlgorithm();

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

        private class DelegateInfoHashtable : LockFreeReaderHashtable<TypeDesc, DelegateInfo>
        {
            protected override int GetKeyHashCode(TypeDesc key)
            {
                return key.GetHashCode();
            }
            protected override int GetValueHashCode(DelegateInfo value)
            {
                return value.Type.GetHashCode();
            }
            protected override bool CompareKeyToValue(TypeDesc key, DelegateInfo value)
            {
                return Object.ReferenceEquals(key, value.Type);
            }
            protected override bool CompareValueToValue(DelegateInfo value1, DelegateInfo value2)
            {
                return Object.ReferenceEquals(value1.Type, value2.Type);
            }
            protected override DelegateInfo CreateValueFromKey(TypeDesc key)
            {
                return new DelegateInfo(key);
            }
        }
        private DelegateInfoHashtable _delegateInfoHashtable = new DelegateInfoHashtable();

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

        private EcmaModule AddModule(string filePath, string expectedSimpleName)
        {
            MemoryMappedViewAccessor mappedViewAccessor = null;
            PdbSymbolReader pdbReader = null;
            try
            {
                PEReader peReader = OpenPEFile(filePath, out mappedViewAccessor);
                pdbReader = OpenAssociatedSymbolFile(filePath);

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
                    ModuleData actualModuleData = _simpleNameHashtable.AddOrGetExisting(moduleData);
                    if (actualModuleData != moduleData)
                    {
                        if (actualModuleData.FilePath != filePath)
                            throw new FileNotFoundException("Module with same simple name already exists " + filePath);
                        return actualModuleData.Module;
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

        public DelegateInfo GetDelegateInfo(TypeDesc delegateType)
        {
            return _delegateInfoHashtable.GetOrCreateValue(delegateType);
        }

        public override FieldLayoutAlgorithm GetLayoutAlgorithmForType(DefType type)
        {
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

        public override VirtualMethodAlgorithm GetVirtualMethodAlgorithmForType(TypeDesc type)
        {
            Debug.Assert(!type.IsArray, "Wanted to call GetClosestMetadataType?");

            return _virtualMethodAlgorithm;
        }

        public override VirtualMethodEnumerationAlgorithm GetVirtualMethodEnumerationAlgorithmForType(TypeDesc type)
        {
            Debug.Assert(!type.IsArray, "Wanted to call GetClosestMetadataType?");

            if (type.IsDelegate)
                return _delegateVirtualMethodEnumAlgorithm;

            return _virtualMethodEnumAlgorithm;
        }

        protected override Instantiation ConvertInstantiationToCanonForm(Instantiation instantiation, CanonicalFormKind kind, out bool changed)
        {
            return RuntimeDeterminedCanonicalizationAlgorithm.ConvertInstantiationToCanonForm(instantiation, kind, out changed);
        }

        protected override TypeDesc ConvertToCanon(TypeDesc typeToConvert, CanonicalFormKind kind)
        {
            return RuntimeDeterminedCanonicalizationAlgorithm.ConvertToCanon(typeToConvert, kind);
        }

        protected override TypeDesc ConvertToCanon(TypeDesc typeToConvert, ref CanonicalFormKind kind)
        {
            return RuntimeDeterminedCanonicalizationAlgorithm.ConvertToCanon(typeToConvert, ref kind);
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

        private PdbSymbolReader OpenAssociatedSymbolFile(string peFilePath)
        {
            // Assume that the .pdb file is next to the binary
            var pdbFilename = Path.ChangeExtension(peFilePath, ".pdb");

            if (!File.Exists(pdbFilename))
                return null;

            // Try to open the symbol file as portable pdb first
            PdbSymbolReader reader = PortablePdbSymbolReader.TryOpen(pdbFilename, GetMetadataStringDecoder());
            if (reader == null)
            {
                // Fallback to the diasymreader for non-portable pdbs
                reader = UnmanagedPdbSymbolReader.TryOpenSymbolReaderForMetadataFile(peFilePath);
            }

            return reader;
        }
    }
}
