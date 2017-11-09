// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILVerify
{
    class SimpleTypeSystemContext : MetadataTypeSystemContext
    {
        private readonly IResolver _resolver;

        private RuntimeInterfacesAlgorithm _arrayOfTRuntimeInterfacesAlgorithm;
        private MetadataRuntimeInterfacesAlgorithm _metadataRuntimeInterfacesAlgorithm = new MetadataRuntimeInterfacesAlgorithm();

        // cache from simple name to EcmaModule
        private readonly Dictionary<string, EcmaModule> _modules = new Dictionary<string, EcmaModule>(StringComparer.OrdinalIgnoreCase);

        internal EcmaModule _inferredSystemModule;

        public SimpleTypeSystemContext(IResolver resolver)
        {
            _resolver = resolver;
        }

        public override ModuleDesc ResolveAssembly(AssemblyName name, bool throwIfNotFound = true)
        {
            return GetModule(name, throwIfNotFound);
        }

        public override ModuleDesc ResolveModule(AssemblyName name, bool throwIfNotFound = true)
        {
            return GetModule(name, throwIfNotFound);
        }

        protected override RuntimeInterfacesAlgorithm GetRuntimeInterfacesAlgorithmForNonPointerArrayType(ArrayType type)
        {
            if (_arrayOfTRuntimeInterfacesAlgorithm == null)
            {
                _arrayOfTRuntimeInterfacesAlgorithm = new SimpleArrayOfTRuntimeInterfacesAlgorithm(SystemModule);
            }
            return _arrayOfTRuntimeInterfacesAlgorithm;
        }

        protected override RuntimeInterfacesAlgorithm GetRuntimeInterfacesAlgorithmForDefType(DefType type)
        {
            return _metadataRuntimeInterfacesAlgorithm;
        }

        internal EcmaModule GetModule(AssemblyName name, bool throwIfNotFound = true)
        {
            if (_modules.TryGetValue(name.Name, out EcmaModule existingModule))
            {
                return existingModule;
            }

            EcmaModule module = CreateModule(name);
            if (module is null && throwIfNotFound)
            {
                throw new VerifierException("Assembly or module not found: " + name.Name);
            }

            return module;
        }

        private EcmaModule CreateModule(AssemblyName name)
        {
            PEReader peReader = _resolver.Resolve(name);
            if (peReader is null)
            {
                return null;
            }

            EcmaModule module = EcmaModule.Create(this, peReader);

            MetadataReader metadataReader = module.MetadataReader;

            if (this.SystemModule == null && IsSystemModule(metadataReader))
            {
                Debug.Assert(_inferredSystemModule is null);
                _inferredSystemModule = module;
            }

            StringHandle nameHandle = metadataReader.IsAssembly
                ? metadataReader.GetAssemblyDefinition().Name
                : metadataReader.GetModuleDefinition().Name;

            string actualSimpleName = metadataReader.GetString(nameHandle);
            if (!actualSimpleName.Equals(name.Name, StringComparison.OrdinalIgnoreCase))
                throw new VerifierException($"Assembly name '{actualSimpleName}' does not match filename '{name}'");

            _modules.Add(name.Name, module);

            return module;
        }

        private bool IsSystemModule(MetadataReader metadataReader)
        {
            if (metadataReader.AssemblyReferences.Count > 0)
            {
                return false;
            }

            // TODO check for System.Object too

            return true;
        }
    }
}
