// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using Internal.CommandLine;

namespace ILToNative
{
    class CompilerTypeSystemContext : TypeSystemContext
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

        Dictionary<string, EcmaModule> _modules = new Dictionary<string, EcmaModule>(StringComparer.InvariantCultureIgnoreCase);

        class ModuleData
        {
            public string Path;
            // public ISymbolReader SymbolReader;

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

        public override MetadataType GetWellKnownType(WellKnownType wellKnownType)
        {
            return _wellKnownTypes[(int)wellKnownType - 1];
        }

        public override object ResolveAssembly(System.Reflection.AssemblyName name)
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

            EcmaModule module = new EcmaModule(this, new PEReader(File.OpenRead(filePath)));

            MetadataReader metadataReader = module.MetadataReader;
            string actualSimpleName = metadataReader.GetString(metadataReader.GetAssemblyDefinition().Name);
            if (!actualSimpleName.Equals(simpleName, StringComparison.InvariantCultureIgnoreCase))
                throw new CommandLineException("Assembly name does not match filename " + filePath);

            _modules.Add(simpleName, module);

            ModuleData moduleData = new ModuleData() { Path = filePath };
            _moduleData.Add(module, moduleData);

            return module;
        }
    }
}
