// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
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
    public class CompilerTypeSystemContext : TypeSystemContext, IMetadataStringDecoderProvider
    {
        static readonly string[] s_wellKnownTypeNames = new string[] {
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

        MetadataType[] _wellKnownTypes = new MetadataType[s_wellKnownTypeNames.Length];

        MetadataFieldLayoutAlgorithm _metadataFieldLayoutAlgorithm = new CompilerMetadataFieldLayoutAlgorithm();
        MetadataRuntimeInterfacesAlgorithm _metadataRuntimeInterfacesAlgorithm = new MetadataRuntimeInterfacesAlgorithm();
        ArrayOfTRuntimeInterfacesAlgorithm _arrayOfTRuntimeInterfacesAlgorithm;

        MetadataStringDecoder _metadataStringDecoder;

        Dictionary<string, EcmaModule> _modules = new Dictionary<string, EcmaModule>(StringComparer.OrdinalIgnoreCase);

        class ModuleData
        {
            public string Path;
            public Microsoft.DiaSymReader.ISymUnmanagedReader PdbReader;
        }
        Dictionary<EcmaModule, ModuleData> _moduleData = new Dictionary<EcmaModule, ModuleData>();

        public CompilerTypeSystemContext(TargetDetails details)
            : base(details)
        {
        }

        public IDictionary<string, string> InputFilePaths
        {
            get;
            set;
        }

        public IDictionary<string, string> ReferenceFilePaths
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

        public override MetadataType GetWellKnownType(WellKnownType wellKnownType)
        {
            return _wellKnownTypes[(int)wellKnownType - 1];
        }

        public override ModuleDesc ResolveAssembly(System.Reflection.AssemblyName name)
        {
            return GetModuleForSimpleName(name.Name);
        }

        public EcmaModule GetModuleForSimpleName(string simpleName)
        {
            EcmaModule existingModule;
            if (_modules.TryGetValue(simpleName, out existingModule))
                return existingModule;

            string filePath;
            if (!InputFilePaths.TryGetValue(simpleName, out filePath))
            {
                if (!ReferenceFilePaths.TryGetValue(simpleName, out filePath))
                    throw new FileNotFoundException("Assembly not found: " + simpleName);
            }

            EcmaModule module = new EcmaModule(this, new PEReader(File.OpenRead(filePath)));

            MetadataReader metadataReader = module.MetadataReader;
            string actualSimpleName = metadataReader.GetString(metadataReader.GetAssemblyDefinition().Name);
            if (!actualSimpleName.Equals(simpleName, StringComparison.OrdinalIgnoreCase))
                throw new FileNotFoundException("Assembly name does not match filename " + filePath);

            AddModule(simpleName, filePath, module);

            return module;
        }

        public EcmaModule GetModuleFromPath(string filePath)
        {
            // This method is not expected to be called frequently. Linear search is acceptable.
            foreach (var entry in _moduleData)
            {
                if (entry.Value.Path == filePath)
                    return entry.Key;
            }

            EcmaModule module = new EcmaModule(this, new PEReader(File.OpenRead(filePath)));

            MetadataReader metadataReader = module.MetadataReader;
            string simpleName = metadataReader.GetString(metadataReader.GetAssemblyDefinition().Name);
            if (_modules.ContainsKey(simpleName))
                throw new FileNotFoundException("Module with same simple name already exists " + filePath);

            AddModule(simpleName, filePath, module);

            return module;
        }

        private void AddModule(string simpleName, string filePath, EcmaModule module)
        {
            _modules.Add(simpleName, module);

            ModuleData moduleData = new ModuleData()
            {
                Path = filePath
            };

            InitializeSymbolReader(moduleData);

            _moduleData.Add(module, moduleData);
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

        MetadataStringDecoder IMetadataStringDecoderProvider.GetMetadataStringDecoder()
        {
            if (_metadataStringDecoder == null)
                _metadataStringDecoder = new CachingMetadataStringDecoder(0x10000); // TODO: Tune the size
            return _metadataStringDecoder;
        }

        //
        // Symbols
        //

        PdbSymbolProvider _pdbSymbolProvider;

        private void InitializeSymbolReader(ModuleData moduleData)
        {
            if (_pdbSymbolProvider == null)
                _pdbSymbolProvider = new PdbSymbolProvider();

            moduleData.PdbReader = _pdbSymbolProvider.GetSymbolReaderForFile(moduleData.Path);
       }

        public IEnumerable<ILSequencePoint> GetSequencePointsForMethod(MethodDesc method)
        {
            EcmaMethod ecmaMethod = method.GetTypicalMethodDefinition() as EcmaMethod;
            if (ecmaMethod == null)
                return null;

            ModuleData moduleData = _moduleData[ecmaMethod.Module];
            if (moduleData.PdbReader == null)
                return null;

            return _pdbSymbolProvider.GetSequencePointsForMethod(moduleData.PdbReader, MetadataTokens.GetToken(ecmaMethod.Handle));
        }

        public IEnumerable<LocalVariable> GetLocalVariableNamesForMethod(MethodDesc method)
        {
            EcmaMethod ecmaMethod = method.GetTypicalMethodDefinition() as EcmaMethod;
            if (ecmaMethod == null)
                return null;

            ModuleData moduleData = _moduleData[ecmaMethod.Module];
            if (moduleData.PdbReader == null)
                return null;

            return _pdbSymbolProvider.GetLocalVariableNamesForMethod(moduleData.PdbReader, MetadataTokens.GetToken(ecmaMethod.Handle));
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
