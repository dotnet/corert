// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.IO;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;

using global::System.Reflection.Runtime.General;

using global::Internal.Reflection.Core;
using global::Internal.Runtime.TypeLoader;

using System.Reflection.PortableExecutable;
using System.Reflection.Metadata;
using System.Collections.Immutable;

using System.Reflection.Runtime.Assemblies;

namespace System.Reflection.Runtime.General
{
    //
    // Collect various metadata reading tasks for better chunking...
    //
    internal static class EcmaMetadataReaderExtensions
    {
        public static string GetString(this StringHandle handle, MetadataReader reader)
        {
            return reader.GetString(handle);
        }

        public static string GetStringOrNull(this StringHandle handle, MetadataReader reader)
        {
            if (handle.IsNil)
                return null;

            return reader.GetString(handle);
        }

        public static RuntimeAssemblyName ToRuntimeAssemblyName(this AssemblyDefinition assemblyDefinition, MetadataReader reader)
        {
            return CreateRuntimeAssemblyNameFromMetadata(
                reader,
                assemblyDefinition.Name,
                assemblyDefinition.Version,
                assemblyDefinition.Culture,
                assemblyDefinition.PublicKey,
                assemblyDefinition.Flags
                );
        }

        public static RuntimeAssemblyName ToRuntimeAssemblyName(this AssemblyReferenceHandle assemblyReferenceHandle, MetadataReader reader)
        {
            AssemblyReference assemblyReference = reader.GetAssemblyReference(assemblyReferenceHandle);
            return CreateRuntimeAssemblyNameFromMetadata(
                reader,
                assemblyReference.Name,
                assemblyReference.Version,
                assemblyReference.Culture,
                assemblyReference.PublicKeyOrToken,
                assemblyReference.Flags
                );
        }

        private static RuntimeAssemblyName CreateRuntimeAssemblyNameFromMetadata(
            MetadataReader reader,
            StringHandle name,
            Version version,
            StringHandle culture,
            BlobHandle publicKeyOrToken,
            AssemblyFlags assemblyFlags)
        {
            AssemblyNameFlags assemblyNameFlags = AssemblyNameFlags.None;
            if (0 != (assemblyFlags & AssemblyFlags.PublicKey))
                assemblyNameFlags |= AssemblyNameFlags.PublicKey;
            if (0 != (assemblyFlags & AssemblyFlags.Retargetable))
                assemblyNameFlags |= AssemblyNameFlags.Retargetable;
            int contentType = ((int)assemblyFlags) & 0x00000E00;
            assemblyNameFlags |= (AssemblyNameFlags)contentType;

            byte[] publicKeyOrTokenByteArray = null;
            if (!publicKeyOrToken.IsNil)
            {
                ImmutableArray<byte> publicKeyOrTokenBlob = reader.GetBlobContent(publicKeyOrToken);
                publicKeyOrTokenByteArray = new byte[publicKeyOrTokenBlob.Length];
                publicKeyOrTokenBlob.CopyTo(publicKeyOrTokenByteArray);
            }
            
            return new RuntimeAssemblyName(
                name.GetString(reader),
                version,
                culture.GetString(reader),
                assemblyNameFlags,
                publicKeyOrTokenByteArray
                );
        }
    }
}

namespace Internal.Reflection.Execution
{
    /// Abstraction to hold PE data for an ECMA module
    public class PEInfo
    {
        public AssemblyName Name;
        public MetadataReader Reader;
        public PEReader PE;
    }

    //=============================================================================================================================
    // The assembly resolution policy for Project N's emulation of "classic reflection."
    //
    // The policy is very simple: the only assemblies that can be "loaded" are those that are statically linked into the running
    // native process. There is no support for probing for assemblies in directories, user-supplied files, GACs, NICs or any
    // other repository.
    //=============================================================================================================================
    public sealed partial class AssemblyBinderImplementation : AssemblyBinder
    {


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
                PEInfo peinfo = new PEInfo();
                peinfo.PE = pe;
                peinfo.Reader = reader;
                peinfo.Name = asmName;

                s_ecmaLoadedAssemblies.Add(peinfo);
                ModuleList moduleList = ModuleList.Instance;
                ModuleInfo newModuleInfo = new ModuleInfo(moduleList.SystemModule.Handle, ModuleType.Ecma, peinfo);
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