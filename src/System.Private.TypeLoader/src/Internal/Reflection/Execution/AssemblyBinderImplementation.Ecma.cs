// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;

using Internal.Reflection.Core;
using Internal.Runtime.TypeLoader;

using System.Reflection.Runtime.General;

using System.Reflection.PortableExecutable;
using System.Reflection.Metadata;
using System.Collections.Immutable;

using System.Reflection.Runtime.Assemblies;

namespace Internal.Reflection.Execution
{
    //=============================================================================================================================
    // The assembly resolution policy for Project N's emulation of "classic reflection."
    //
    // The policy is very simple: the only assemblies that can be "loaded" are those that are statically linked into the running
    // native process. There is no support for probing for assemblies in directories, user-supplied files, GACs, NICs or any
    // other repository.
    //=============================================================================================================================
    public sealed partial class AssemblyBinderImplementation : AssemblyBinder
    {
        /// Abstraction to hold PE data for an ECMA module
        private class PEInfo
        {
            public PEInfo(AssemblyName name, MetadataReader reader, PEReader pe)
            {
                Name = name;
                Reader = reader;
                PE = pe;
            }

            public readonly AssemblyName Name;
            public readonly MetadataReader Reader;
            public readonly PEReader PE;
        }

        private static LowLevelList<PEInfo> s_ecmaLoadedAssemblies = new LowLevelList<PEInfo>();

        partial void BindEcmaByteArray(byte[] rawAssembly, byte[] rawSymbolStore, ref AssemblyBindResult bindResult, ref Exception exception, ref bool? result)
        {
            // 1. Load byte[] into immutable array for use by PEReader/MetadataReader
            ImmutableArray<byte> assemblyData = ImmutableArray.Create(rawAssembly);
            PEReader pe = new PEReader(assemblyData);
            MetadataReader reader = pe.GetMetadataReader();

            // 2. Create AssemblyName from MetadataReader
            RuntimeAssemblyName runtimeAssemblyName = reader.GetAssemblyDefinition().ToRuntimeAssemblyName(reader);
            AssemblyName asmName = new AssemblyName();
            runtimeAssemblyName.CopyToAssemblyName(asmName);

            lock(s_ecmaLoadedAssemblies)
            {
                // 3. Attempt to bind to already loaded assembly
                if (Bind(asmName, out bindResult, out exception))
                {
                    result = true;
                    return;
                }
                exception = null;

                // 4. If that fails, then add newly created metareader to global cache of byte array loaded modules
                PEInfo peinfo = new PEInfo(asmName, reader, pe);

                s_ecmaLoadedAssemblies.Add(peinfo);
                ModuleList moduleList = ModuleList.Instance;
                ModuleInfo newModuleInfo = new EcmaModuleInfo(moduleList.SystemModule.Handle, pe, reader);
                moduleList.RegisterModule(newModuleInfo);

                // 5. Then try to load by name again. This load should always succeed
                if (Bind(asmName, out bindResult, out exception))
                {
                    result = true;
                    return;
                }

                result = false;
                Debug.Assert(exception != null); // We must have an error on load. At this time this could happen due to ambiguous name matching
            }
        }

        partial void BindEcmaAssemblyName(AssemblyName refName, ref AssemblyBindResult result, ref Exception exception, ref bool foundMatch)
        {
            lock(s_ecmaLoadedAssemblies)
            {
                for (int i = 0; i < s_ecmaLoadedAssemblies.Count; i++)
                {
                    PEInfo info = s_ecmaLoadedAssemblies[i];
                    if (AssemblyNameMatches(refName, info.Name))
                    {
                        if (foundMatch)
                        {
                            exception = new AmbiguousMatchException();
                            return;
                        }

                        foundMatch = true;
                        result.EcmaMetadataReader = info.Reader;
                    }
                }
            }
        }
    }
}