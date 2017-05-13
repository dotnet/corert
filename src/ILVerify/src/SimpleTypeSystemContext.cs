// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using Internal.CommandLine;

namespace ILVerify
{
    class SimpleTypeSystemContext : MetadataTypeSystemContext
    {
        Dictionary<string, EcmaModule> _modules = new Dictionary<string, EcmaModule>(StringComparer.OrdinalIgnoreCase);

        class ModuleData
        {
            public string Path;
        }
        Dictionary<EcmaModule, ModuleData> _moduleData = new Dictionary<EcmaModule, ModuleData>();

        public SimpleTypeSystemContext()
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

        public override ModuleDesc ResolveAssembly(AssemblyName name, bool throwIfNotFound = true)
        {
            return GetModuleForSimpleName(name.Name);
        }

        public EcmaModule GetModuleForSimpleName(string simpleName)
        {
            EcmaModule existingModule;
            if (_modules.TryGetValue(simpleName, out existingModule))
                return existingModule;

            return CreateModuleForSimpleName(simpleName);
        }

        private EcmaModule CreateModuleForSimpleName(string simpleName)
        {
            string filePath;
            if (!InputFilePaths.TryGetValue(simpleName, out filePath))
            {
                if (!ReferenceFilePaths.TryGetValue(simpleName, out filePath))
                    throw new CommandLineException("Assembly not found: " + simpleName);
            }

            PEReader peReader = new PEReader(File.OpenRead(filePath));
            EcmaModule module = EcmaModule.Create(this, peReader);

            MetadataReader metadataReader = module.MetadataReader;
            string actualSimpleName = metadataReader.GetString(metadataReader.GetAssemblyDefinition().Name);
            if (!actualSimpleName.Equals(simpleName, StringComparison.OrdinalIgnoreCase))
                throw new CommandLineException("Assembly name does not match filename " + filePath);

            _modules.Add(simpleName, module);

            ModuleData moduleData = new ModuleData() { Path = filePath };
            _moduleData.Add(module, moduleData);

            return module;
        }

        public EcmaModule GetModuleFromPath(string filePath)
        {
            // This is called once for every assembly that should be verified, so linear search is acceptable.
            foreach (KeyValuePair<EcmaModule, ModuleData> entry in _moduleData)
            {
                EcmaModule curModule = entry.Key;
                ModuleData curData = entry.Value;
                if (curData.Path == filePath)
                    return curModule;
            }
            
            PEReader peReader = new PEReader(File.OpenRead(filePath));
            EcmaModule module = EcmaModule.Create(this, peReader);

            MetadataReader metadataReader = module.MetadataReader;
            string simpleName = metadataReader.GetString(metadataReader.GetAssemblyDefinition().Name);
            if (_modules.ContainsKey(simpleName))
                throw new CommandLineException("Module with same simple name already exists " + filePath);

            _modules.Add(simpleName, module);

            ModuleData moduleData = new ModuleData() { Path = filePath };
            _moduleData.Add(module, moduleData);

            return module;
        }

        public string GetModulePath(EcmaModule module)
        {
            return _moduleData[module].Path;
        }
    }
}
