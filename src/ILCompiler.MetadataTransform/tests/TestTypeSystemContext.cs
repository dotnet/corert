// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

using Debug = System.Diagnostics.Debug;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using System.Reflection.PortableExecutable;
using System.IO;

namespace MetadataTransformTests
{
    class TestTypeSystemContext : MetadataTypeSystemContext
    {
        Dictionary<string, EcmaModule> _modules = new Dictionary<string, EcmaModule>(StringComparer.OrdinalIgnoreCase);

        public EcmaModule GetModuleForSimpleName(string simpleName, bool throwIfNotFound = true)
        {
            EcmaModule module;
            if (!_modules.TryGetValue(simpleName, out module))
            {
                module = CreateModuleForSimpleName(simpleName);
            }

            if (module == null && throwIfNotFound)
            {
                throw new FileNotFoundException(simpleName + ".dll");
            }
            return module;
        }

        public EcmaModule CreateModuleForSimpleName(string simpleName)
        {
            EcmaModule module = null;
            try
            {
                module = EcmaModule.Create(this, new PEReader(File.OpenRead(simpleName + ".dll")), containingAssembly: null);
            }
            catch (FileNotFoundException)
            {
                // FileNotFound is treated as being unable to load the module
            }

            _modules.Add(simpleName, module);
            return module;
        }

        public override ModuleDesc ResolveAssembly(System.Reflection.AssemblyName name, bool throwIfNotFound)
        {
            return GetModuleForSimpleName(name.Name, throwIfNotFound);
        }

        public override bool SupportsUniversalCanon => true;
        public override bool SupportsCanon => true;
    }
}
