// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Internal.TypeSystem;
using System.Reflection.PortableExecutable;
using System.IO;

namespace TypeSystemTests
{
    class TestTypeSystemContext : MetadataTypeSystemContext
    {
        Dictionary<string, ModuleDesc> _modules = new Dictionary<string, ModuleDesc>(StringComparer.OrdinalIgnoreCase);

        MetadataFieldLayoutAlgorithm _metadataFieldLayout = new TestMetadataFieldLayoutAlgorithm();
        MetadataRuntimeInterfacesAlgorithm _metadataRuntimeInterfacesAlgorithm = new MetadataRuntimeInterfacesAlgorithm();
        ArrayOfTRuntimeInterfacesAlgorithm _arrayOfTRuntimeInterfacesAlgorithm;
        VirtualMethodAlgorithm _virtualMethodAlgorithm = new MetadataVirtualMethodAlgorithm();
        VirtualMethodEnumerationAlgorithm _virtualMethodEnumAlgorithm = new MetadataVirtualMethodEnumerationAlgorithm();

        public TestTypeSystemContext(TargetArchitecture arch)
            : base(new TargetDetails(arch, TargetOS.Unknown))
        {
        }

        public ModuleDesc GetModuleForSimpleName(string simpleName)
        {
            ModuleDesc existingModule;
            if (_modules.TryGetValue(simpleName, out existingModule))
                return existingModule;

            return CreateModuleForSimpleName(simpleName);
        }

        public ModuleDesc CreateModuleForSimpleName(string simpleName)
        {
            ModuleDesc module = new Internal.TypeSystem.Ecma.EcmaModule(this, new PEReader(File.OpenRead(simpleName + ".dll")));
            _modules.Add(simpleName, module);
            return module;
        }

        public override ModuleDesc ResolveAssembly(System.Reflection.AssemblyName name, bool throwIfNotFound)
        {
            return GetModuleForSimpleName(name.Name);
        }

        public override FieldLayoutAlgorithm GetLayoutAlgorithmForType(DefType type)
        {
            return _metadataFieldLayout;
        }

        protected override RuntimeInterfacesAlgorithm GetRuntimeInterfacesAlgorithmForNonPointerArrayType(ArrayType type)
        {
            if (_arrayOfTRuntimeInterfacesAlgorithm == null)
            {
                _arrayOfTRuntimeInterfacesAlgorithm = new ArrayOfTRuntimeInterfacesAlgorithm(SystemModule.GetType("System", "Array`1"));
            }
            return _arrayOfTRuntimeInterfacesAlgorithm;
        }

        protected override RuntimeInterfacesAlgorithm GetRuntimeInterfacesAlgorithmForDefType(DefType type)
        {
            return _metadataRuntimeInterfacesAlgorithm;
        }

        public override VirtualMethodAlgorithm GetVirtualMethodAlgorithmForType(TypeDesc type)
        {
            return _virtualMethodAlgorithm;
        }

        public override VirtualMethodEnumerationAlgorithm GetVirtualMethodEnumerationAlgorithmForType(TypeDesc type)
        {
            return _virtualMethodEnumAlgorithm;
        }
    }
}
