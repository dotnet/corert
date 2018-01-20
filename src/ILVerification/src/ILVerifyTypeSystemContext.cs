// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Internal.IL;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILVerify
{
    class ILVerifyTypeSystemContext : MetadataTypeSystemContext
    {
        internal readonly IResolver _resolver;

        private RuntimeInterfacesAlgorithm _arrayOfTRuntimeInterfacesAlgorithm;
        private MetadataRuntimeInterfacesAlgorithm _metadataRuntimeInterfacesAlgorithm = new MetadataRuntimeInterfacesAlgorithm();

        private readonly Dictionary<PEReader, EcmaModule> _modulesCache = new Dictionary<PEReader, EcmaModule>();

        public ILVerifyTypeSystemContext(IResolver resolver)
        {
            _resolver = resolver;
        }

        public override ModuleDesc ResolveAssembly(AssemblyName name, bool throwIfNotFound = true)
        {
            PEReader peReader = _resolver.Resolve(name);
            if (peReader == null && throwIfNotFound)
            {
                throw new VerifierException("Assembly or module not found: " + name.Name);
            }

            var module = GetModule(peReader);
            VerifyModuleName(name, module);
            return module;
        }

        private static void VerifyModuleName(AssemblyName name, EcmaModule module)
        {
            MetadataReader metadataReader = module.MetadataReader;
            StringHandle nameHandle = metadataReader.IsAssembly
                ? metadataReader.GetAssemblyDefinition().Name
                : metadataReader.GetModuleDefinition().Name;

            string actualSimpleName = metadataReader.GetString(nameHandle);
            if (!actualSimpleName.Equals(name.Name, StringComparison.OrdinalIgnoreCase))
            {
                throw new VerifierException($"Actual PE name '{actualSimpleName}' does not match provided name '{name}'");
            }
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

        internal EcmaModule GetModule(PEReader peReader)
        {
            if (peReader == null)
            {
                return null;
            }

            if (_modulesCache.TryGetValue(peReader, out EcmaModule existingModule))
            {
                return existingModule;
            }

            EcmaModule module = EcmaModule.Create(this, peReader);
            _modulesCache.Add(peReader, module);
            return module;
        }
    }
}
