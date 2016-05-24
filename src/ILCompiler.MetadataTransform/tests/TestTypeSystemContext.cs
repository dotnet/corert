// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        public EcmaModule GetModuleForSimpleName(string simpleName)
        {
            EcmaModule existingModule;
            if (_modules.TryGetValue(simpleName, out existingModule))
                return existingModule;

            return CreateModuleForSimpleName(simpleName);
        }

        public EcmaModule CreateModuleForSimpleName(string simpleName)
        {
            EcmaModule module = EcmaModule.Create(this, new PEReader(File.OpenRead(simpleName + ".dll")));
            _modules.Add(simpleName, module);
            return module;
        }

        public override ModuleDesc ResolveAssembly(System.Reflection.AssemblyName name, bool throwIfNotFound)
        {
            return GetModuleForSimpleName(name.Name);
        }
    }
}
