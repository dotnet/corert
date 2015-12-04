// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Debug = System.Diagnostics.Debug;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using System.Reflection.PortableExecutable;
using System.IO;

namespace TypeSystemTests
{
    class TestTypeSystemContext : TypeSystemContext
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
        };

        MetadataType[] _wellKnownTypes = new MetadataType[s_wellKnownTypeNames.Length];

        EcmaModule _systemModule;

        Dictionary<string, EcmaModule> _modules = new Dictionary<string, EcmaModule>(StringComparer.OrdinalIgnoreCase);

        MetadataFieldLayoutAlgorithm _metadataFieldLayout = new TestMetadataFieldLayoutAlgorithm();
        MetadataRuntimeInterfacesAlgorithm _metadataRuntimeInterfacesAlgorithm = new MetadataRuntimeInterfacesAlgorithm();
        ArrayOfTRuntimeInterfacesAlgorithm _arrayOfTRuntimeInterfacesAlgorithm;

        public TestTypeSystemContext(TargetArchitecture arch)
            : base(new TargetDetails(arch, TargetOS.Unknown))
        {
        }

        public override DefType GetWellKnownType(WellKnownType wellKnownType)
        {
            return _wellKnownTypes[(int)wellKnownType - 1];
        }

        public void SetSystemModule(EcmaModule systemModule)
        {
            _systemModule = systemModule;

            // Sanity check the name table
            Debug.Assert(s_wellKnownTypeNames[(int)WellKnownType.MulticastDelegate - 1] == "MulticastDelegate");

            // Initialize all well known types - it will save us from checking the name for each loaded type
            for (int typeIndex = 0; typeIndex < _wellKnownTypes.Length; typeIndex++)
            {
                MetadataType type = _systemModule.GetType("System", s_wellKnownTypeNames[typeIndex]);
                type.SetWellKnownType((WellKnownType)(typeIndex + 1));
                _wellKnownTypes[typeIndex] = type;
            }
        }

        public EcmaModule GetModuleForSimpleName(string simpleName)
        {
            EcmaModule existingModule;
            if (_modules.TryGetValue(simpleName, out existingModule))
                return existingModule;

            return CreateModuleForSimpleName(simpleName);
        }

        public EcmaModule CreateModuleForSimpleName(string simpleName)
        {
            EcmaModule module = new EcmaModule(this, new PEReader(File.OpenRead(simpleName + ".dll")));
            _modules.Add(simpleName, module);
            return module;
        }

        public override ModuleDesc ResolveAssembly(System.Reflection.AssemblyName name)
        {
            return GetModuleForSimpleName(name.Name);
        }

        public override FieldLayoutAlgorithm GetLayoutAlgorithmForType(DefType type)
        {
            return _metadataFieldLayout;
        }

        public override RuntimeInterfacesAlgorithm GetRuntimeInterfacesAlgorithmForNonPointerArrayType(ArrayType type)
        {
            if (_arrayOfTRuntimeInterfacesAlgorithm == null)
            {
                _arrayOfTRuntimeInterfacesAlgorithm = new ArrayOfTRuntimeInterfacesAlgorithm(_systemModule.GetType("System", "Array`1"));
            }
            return _arrayOfTRuntimeInterfacesAlgorithm;
        }

        public override RuntimeInterfacesAlgorithm GetRuntimeInterfacesAlgorithmForMetadataType(MetadataType type)
        {
            return _metadataRuntimeInterfacesAlgorithm;
        }
    }
}
