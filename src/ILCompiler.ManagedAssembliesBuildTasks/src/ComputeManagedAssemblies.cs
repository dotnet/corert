// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;


using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ManagedAssembliesBuildTask
{
    public class ComputeManagedAssemblies : Task
    {
        [Required]
        public ITaskItem[] Assemblies
        {
            get;
            set;
        }

        /// <summary>
        /// The CoreRT-specific System.Private.* assemblies that must be used instead of the netcoreapp2.0 versions.
        /// </summary>
        [Required]
        public ITaskItem[] SdkAssemblies
        {
            get;
            set;
        }

        /// <summary>
        /// The set of AOT-specific framework assemblies we currently need to use which will replace the same-named ones
        /// in the app's closure.
        /// </summary>
        [Required]
        public ITaskItem[] FrameworkAssemblies
        {
            get;
            set;
        }

        [Output]
        public ITaskItem[] ManagedAssemblies
        {
            get;
            set;
        }

        [Output]
        public ITaskItem[] AssembliesToSkipPublish
        {
            get;
            set;
        }

        public override bool Execute()
        {
            List<ITaskItem> list = new List<ITaskItem>();
            List<ITaskItem> assembliesToSkipPublish = new List<ITaskItem>();

            ITaskItem[] assemblies = this.Assemblies;


            var simdVectorLength = SimdVectorLength.Vector128Bit;
            var targetDetails = new TargetDetails(TargetArchitecture.ARM64, TargetOS.Windows, TargetAbi.CoreRT, simdVectorLength);

            var coreRTFrameworkAssembliesToUse = new HashSet<string>();

            foreach (var x in SdkAssemblies)
            {
                coreRTFrameworkAssembliesToUse.Add(Path.GetFileName(x.ItemSpec));
            }

            foreach (var x in FrameworkAssemblies)
            {
                coreRTFrameworkAssembliesToUse.Add(Path.GetFileName(x.ItemSpec));
            }

            for (int i = 0; i < assemblies.Length; i++)
            {
                ITaskItem taskItem = assemblies[i];

                // Skip crossgen images
                if (taskItem.ItemSpec.EndsWith(".ni.dll", StringComparison.OrdinalIgnoreCase))
                {
                    assembliesToSkipPublish.Add(taskItem);
                    continue;
                }

                // Skip the native apphost (whose name ends up colliding with the CoreRT output binary) and supporting assemblies
                if (taskItem.ItemSpec.EndsWith("apphost.exe", StringComparison.OrdinalIgnoreCase) || taskItem.ItemSpec.Contains("hostfxr") || taskItem.ItemSpec.Contains("hostpolicy"))
                {
                    assembliesToSkipPublish.Add(taskItem);
                    continue;
                }

                // Prototype aid - remove the native CoreCLR runtime pieces from the publish folder
                if (taskItem.ItemSpec.Contains("microsoft.netcore.app") && (taskItem.ItemSpec.Contains("\\native\\") || taskItem.ItemSpec.Contains("/native/")))
                {
                    assembliesToSkipPublish.Add(taskItem);
                    continue;
                }

                // Remove any assemblies whose implementation we want to come from CoreRT's package.
                // Currently that's System.Private.* SDK assemblies and a bunch of framework assemblies.
                if (coreRTFrameworkAssembliesToUse.Contains(Path.GetFileName(taskItem.ItemSpec)))
                {
                    assembliesToSkipPublish.Add(taskItem);
                    continue;
                }

                try
                {
                    using (var moduleStream = File.OpenRead(taskItem.ItemSpec))
                    using (var module = new PEReader(moduleStream))
                    {
                        var moduleMetadataReader = module.GetMetadataReader();
                        if (moduleMetadataReader.IsAssembly)
                        {
                            string culture = moduleMetadataReader.GetString(moduleMetadataReader.GetAssemblyDefinition().Culture);
                            if (culture == "" || culture.Equals("neutral", StringComparison.OrdinalIgnoreCase))
                            {
                                // CoreRT doesn't consume resource assemblies yet so skip them
                                assembliesToSkipPublish.Add(taskItem);
                                list.Add(taskItem);
                            }
                        }
                    }
                }
                catch (TypeSystemException.BadImageFormatException)
                {
                }
                catch (BadImageFormatException)
                {
                }
            }

            ManagedAssemblies = list.ToArray();
            AssembliesToSkipPublish = assembliesToSkipPublish.ToArray();

            return true;
        }
    }
}
