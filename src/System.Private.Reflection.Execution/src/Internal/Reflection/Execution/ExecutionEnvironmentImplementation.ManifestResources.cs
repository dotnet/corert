// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.IO;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;
using global::System.Runtime.InteropServices;

using global::Internal.IO;

using global::Internal.Runtime.Augments;

using global::Internal.Metadata.NativeFormat;

using global::Internal.Reflection.Core;
using global::Internal.Reflection.Core.Execution;
using global::Internal.Reflection.Execution.MethodInvokers;

namespace Internal.Reflection.Execution
{
    //==========================================================================================================
    // These ExecutionEnvironment entrypoints implement support for manifest resource streams on the Assembly class.
    //==========================================================================================================
    internal sealed partial class ExecutionEnvironmentImplementation : ExecutionEnvironment
    {
        public sealed override ManifestResourceInfo GetManifestResourceInfo(Assembly assembly, String resourceName)
        {
            throw new PlatformNotSupportedException();
        }

        public sealed override String[] GetManifestResourceNames(Assembly assembly)
        {
            LowLevelList<ResourceInfo> resourceInfos = GetExtractedResources(assembly);
            String[] names = new String[resourceInfos.Count];
            for (int i = 0; i < resourceInfos.Count; i++)
            {
                names[i] = resourceInfos[i].Name;
            }
            return names;
        }

        public sealed override Stream GetManifestResourceStream(Assembly assembly, String name)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            Stream resultFromFile = ReadFileFromAppPackage(name);
            if (resultFromFile != null)
                return resultFromFile;

            // If that didn't work, this was an embedded resource. The toolchain should have extracted the resource
            // to an external file under a _Resources directory inside the app package. Go retrieve it now.
            LowLevelList<ResourceInfo> resourceInfos = GetExtractedResources(assembly);
            for (int i = 0; i < resourceInfos.Count; i++)
            {
                ResourceInfo resourceInfo = resourceInfos[i];
                if (name == resourceInfo.Name)
                {
                    String extractedResourceFile = ExtractedResourcesDirectory + @"\" + resourceInfo.Index + ".rsrc";
                    return ReadFileFromAppPackage(extractedResourceFile);
                }
            }
            return null;
        }

        private LowLevelList<ResourceInfo> GetExtractedResources(Assembly assembly)
        {
            LowLevelDictionary<String, LowLevelList<ResourceInfo>> extractedResourceDictionary = this.ExtractedResourceDictionary;
            String assemblyName = assembly.GetName().FullName;
            LowLevelList<ResourceInfo> resourceInfos;
            if (!extractedResourceDictionary.TryGetValue(assemblyName, out resourceInfos))
                return new LowLevelList<ResourceInfo>();
            return resourceInfos;
        }

        private LowLevelDictionary<String, LowLevelList<ResourceInfo>> ExtractedResourceDictionary
        {
            get
            {
                if (s_extractedResourceDictionary == null)
                {
                    // Lazily create the extracted resource dictionary. If two threads race here, we may construct two dictionaries
                    // and overwrite one - this is ok since the dictionaries are read-only once constructed and they contain the identical data.

                    LowLevelDictionary<String, LowLevelList<ResourceInfo>> dict = new LowLevelDictionary<String, LowLevelList<ResourceInfo>>();

                    String extractedResourcesIndexFile = ExtractedResourcesDirectory + @"\index.txt";
                    {
                        // Open _Resources\index.txt which is a file created by the toolchain and contains a list of entries like this:
                        //
                        //    <Fusion-style assemblyname1>
                        //       <ResourceName0>     (contents live in _Resources\0.rsrc)
                        //       <ResourceName1>     (contents live in _Resources\1.rsrc)
                        //    <Fusion-style assemblyname2>
                        //       <ResourceName2>     (contents live in _Resources\2.rsrc)
                        //       <ResourceName3>     (contents live in _Resources\3.rsrc)
                        //     

                        using (Stream s = ReadFileFromAppPackage(extractedResourcesIndexFile))
                        {
                            LowLevelStreamReader sr = new LowLevelStreamReader(s);

                            int index = 0;
                            String line;
                            LowLevelList<ResourceInfo> resourceInfos = null;
                            while ((line = sr.ReadLine()) != null)
                            {
                                if (line.Trim().Length == 0)
                                    continue;

                                // If indented, the line is a resource name - if not indented, it's an assembly name.
                                if (!line.StartsWith(" "))
                                {
                                    // Roundtrip the assembly name from index.txt through our own AssemblyName class to remove any variances
                                    // between the CCI-created assembly name and ours.
                                    String normalizedAssemblyName = new AssemblyName(line).FullName;
                                    resourceInfos = new LowLevelList<ResourceInfo>();
                                    dict.Add(normalizedAssemblyName, resourceInfos);
                                }
                                else
                                {
                                    String resourceName = line.TrimStart();
                                    resourceInfos.Add(new ResourceInfo(resourceName, index++));
                                }
                            }
                        }
                    }
                    s_extractedResourceDictionary = dict;
                }
                return s_extractedResourceDictionary;
            }
        }

        private Stream ReadFileFromAppPackage(String name)
        {
            if (WinRTInterop.Callbacks.IsAppxModel())
                return (Stream)WinRTInterop.Callbacks.ReadFileIntoStream(name);

            String pathToRunningExe = RuntimeAugments.TryGetFullPathToMainApplication();
            String directoryContainingRunningExe = Path.GetDirectoryName(pathToRunningExe);
            String fullName = Path.Combine(directoryContainingRunningExe, name);
            return (Stream)RuntimeAugments.OpenFileIfExists(fullName);
        }

        private const String ExtractedResourcesDirectory = "_Resources";

        //
        // This dictionary gets us from assembly + resource name to the name of a _Resources\<nnn>.rsrc file which contains the
        // extracted resource.
        //
        // The dictionary's key is a Fusion-style assembly name.
        // The dictionary's value is a list of <resourcename,index> tuples.
        //
        // To get to the extract resource, we construct the local file path _Resources\<index>.rsrc.
        //
        private static volatile LowLevelDictionary<String, LowLevelList<ResourceInfo>> s_extractedResourceDictionary;

        private struct ResourceInfo
        {
            public ResourceInfo(String name, int index)
            {
                _name = name;
                _index = index;
            }

            public String Name { get { return _name; } }
            public int Index { get { return _index; } }
            private String _name;
            private int _index;
        }
    }
}

