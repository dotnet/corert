// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;

using Internal.Reflection.Core;
using Internal.Runtime.TypeLoader;
using Internal.Runtime.Augments;

using System.Reflection.Runtime.General;

using System.Reflection.PortableExecutable;
using System.Reflection.Metadata;
using System.Collections.Immutable;

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
            public PEInfo(RuntimeAssemblyName name, MetadataReader reader, PEReader pe)
            {
                Name = name;
                Reader = reader;
                PE = pe;
            }

            public readonly RuntimeAssemblyName Name;
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
            RuntimeAssemblyName runtimeAssemblyName = reader.GetAssemblyDefinition().ToRuntimeAssemblyName(reader).CanonicalizePublicKeyToken();

            lock(s_ecmaLoadedAssemblies)
            {
                // 3. Attempt to bind to already loaded assembly
                if (Bind(runtimeAssemblyName, cacheMissedLookups: false, out bindResult, out exception))
                {
                    result = true;
                    return;
                }
                exception = null;

                // 4. If that fails, then add newly created metareader to global cache of byte array loaded modules
                PEInfo peinfo = new PEInfo(runtimeAssemblyName, reader, pe);

                s_ecmaLoadedAssemblies.Add(peinfo);
                ModuleList moduleList = ModuleList.Instance;
                ModuleInfo newModuleInfo = new EcmaModuleInfo(moduleList.SystemModule.Handle, pe, reader);
                moduleList.RegisterModule(newModuleInfo);

                // 5. Then try to load by name again. This load should always succeed
                if (Bind(runtimeAssemblyName, cacheMissedLookups: true, out bindResult, out exception))
                {
                    result = true;
                    return;
                }

                result = false;
                Debug.Assert(exception != null); // We must have an error on load. At this time this could happen due to ambiguous name matching
            }
        }

        partial void BindEcmaAssemblyName(RuntimeAssemblyName refName, bool cacheMissedLookups, ref AssemblyBindResult result, ref Exception exception, ref Exception preferredException, ref bool foundMatch)
        {
            lock(s_ecmaLoadedAssemblies)
            {
                for (int i = 0; i < s_ecmaLoadedAssemblies.Count; i++)
                {
                    PEInfo info = s_ecmaLoadedAssemblies[i];
                    if (AssemblyNameMatches(refName, info.Name, ref preferredException))
                    {
                        if (foundMatch)
                        {
                            exception = new AmbiguousMatchException();
                            return;
                        }

                        result.EcmaMetadataReader = info.Reader;
                        foundMatch = result.EcmaMetadataReader != null;

                        // For failed matches, we will never be able to succeed, so return now
                        if (!foundMatch)
                            return;
                    }
                }

                if (!foundMatch)
                {
                    try
                    {
                        // Not found in already loaded list, attempt to source assembly from disk
                        foreach (string filePath in FilePathsForAssembly(refName))
                        {
                            FileStream ownedFileStream = null;
                            PEReader ownedPEReader = null;
                            try
                            {
                                if (!RuntimeAugments.FileExists(filePath))
                                    continue;

                                try
                                {
                                    ownedFileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                                }
                                catch (System.IO.IOException)
                                {
                                    // Failure to open a file is not fundamentally an assembly load error, but it does indicate this file cannot be used
                                    continue;
                                }

                                ownedPEReader = new PEReader(ownedFileStream);
                                // FileStream ownership transferred to ownedPEReader
                                ownedFileStream = null;

                                if (!ownedPEReader.HasMetadata)
                                    continue;

                                MetadataReader reader = ownedPEReader.GetMetadataReader();
                                // Create AssemblyName from MetadataReader
                                RuntimeAssemblyName runtimeAssemblyName = reader.GetAssemblyDefinition().ToRuntimeAssemblyName(reader).CanonicalizePublicKeyToken();

                                // If assembly name doesn't match, it isn't the one we're looking for. Continue to look for more assemblies
                                if (!AssemblyNameMatches(refName, runtimeAssemblyName, ref preferredException))
                                    continue;

                                // This is the one we are looking for, add it to the list of loaded assemblies
                                PEInfo peinfo = new PEInfo(runtimeAssemblyName, reader, ownedPEReader);

                                s_ecmaLoadedAssemblies.Add(peinfo);

                                // At this point the PE reader is no longer owned by this code, but is owned by the s_ecmaLoadedAssemblies list
                                PEReader pe = ownedPEReader;
                                ownedPEReader = null;

                                ModuleList moduleList = ModuleList.Instance;
                                ModuleInfo newModuleInfo = new EcmaModuleInfo(moduleList.SystemModule.Handle, pe, reader);
                                moduleList.RegisterModule(newModuleInfo);

                                foundMatch = true;
                                result.EcmaMetadataReader = peinfo.Reader;
                                break;
                            }
                            finally
                            {
                                if (ownedFileStream != null)
                                    ownedFileStream.Dispose();

                                if (ownedPEReader != null)
                                    ownedPEReader.Dispose();
                            }
                        }
                    }
                    catch (System.IO.IOException)
                    { }
                    catch (System.ArgumentException)
                    { }
                    catch (System.BadImageFormatException badImageFormat)
                    {
                        exception = badImageFormat;
                    }

                    // Cache missed lookups
                    if (cacheMissedLookups && !foundMatch)
                    {
                        PEInfo peinfo = new PEInfo(refName, null, null);
                        s_ecmaLoadedAssemblies.Add(peinfo);
                    }
                }
            }
        }

        public IEnumerable<string> FilePathsForAssembly(RuntimeAssemblyName refName)
        {
            // Check for illegal characters in file name
            if (refName.Name.IndexOfAny(Path.GetInvalidFileNameChars()) != -1)
                yield break;

            // Implement simple probing for assembly in application base directory and culture specific directory
            string probingDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string cultureQualifiedDirectory = probingDirectory;

            if (!String.IsNullOrEmpty(refName.CultureName))
            {
                cultureQualifiedDirectory = Path.Combine(probingDirectory, refName.CultureName);
            }
            else
            {
                // Loading non-resource dlls not yet supported
                yield break;
            }

            // Attach assembly name
            yield return Path.Combine(cultureQualifiedDirectory, refName.Name + ".dll");
        }

        partial void InsertEcmaLoadedAssemblies(List<AssemblyBindResult> loadedAssemblies)
        {
            lock (s_ecmaLoadedAssemblies)
            {
                for (int i = 0; i < s_ecmaLoadedAssemblies.Count; i++)
                {
                    PEInfo info = s_ecmaLoadedAssemblies[i];
                    if (info.Reader == null)
                        continue;

                    AssemblyBindResult result = default(AssemblyBindResult);
                    result.EcmaMetadataReader = info.Reader;
                    loadedAssemblies.Add(result);
                }
            }
        }
    }
}
