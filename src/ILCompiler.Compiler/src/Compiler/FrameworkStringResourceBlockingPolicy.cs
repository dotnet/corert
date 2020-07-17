// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler
{
    /// <summary>
    /// A resource blocking policy that blocks RESX resources in framework assemblies.
    /// This is useful for size-conscious scenarios where the conveniece of having
    /// proper exception messages in framework-throw exceptions is not important.
    /// </summary>
    public sealed class FrameworkStringResourceBlockingPolicy : ManifestResourceBlockingPolicy
    {
        public override bool IsManifestResourceBlocked(ModuleDesc module, string resourceName)
        {
            // The embedded RESX files all have names that end with .resources, so use that as the initial filter.
            if (!resourceName.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
                return false;

            // Assuming multimodule and non-ecma assemblies are unsupported
            EcmaModule ecmaModule = (EcmaModule)module;

            // If this is not a framework assembly, no resources are blocked
            if (!IsFrameworkAssembly(ecmaModule))
                return false;

            MetadataReader reader = ecmaModule.MetadataReader;
            
            // We have a resource in the framework assembly. Now check if this is a RESX
            foreach (ManifestResourceHandle resourceHandle in reader.ManifestResources)
            {
                ManifestResource resource = reader.GetManifestResource(resourceHandle);
                if (reader.StringComparer.Equals(resource.Name, resourceName) &&
                    resource.Implementation.IsNil)
                {
                    PEMemoryBlock resourceDirectory =
                        ecmaModule.PEReader.GetSectionData(ecmaModule.PEReader.PEHeaders.CorHeader.ResourcesDirectory.RelativeVirtualAddress);
                    BlobReader blob = resourceDirectory.GetReader((int)resource.Offset, resourceDirectory.Length - (int)resource.Offset);
                    int length = (int)blob.ReadUInt32();
                    if (length > 4)
                    {
                        // Check for magic bytes that correspond to RESX
                        if (blob.ReadUInt32() == 0xBEEFCACE)
                            return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Gets a value indicating whether '<paramref name="module"/>' is a framework assembly.
        /// </summary>
        public static bool IsFrameworkAssembly(EcmaModule module)
        {
            MetadataReader reader = module.MetadataReader;

            // We look for [assembly:AssemblyMetadata(".NETFrameworkAssembly", "")]

            foreach (CustomAttributeHandle attributeHandle in reader.GetAssemblyDefinition().GetCustomAttributes())
            {
                if (!reader.GetAttributeNamespaceAndName(attributeHandle, out StringHandle namespaceHandle, out StringHandle nameHandle))
                    continue;

                if (!reader.StringComparer.Equals(namespaceHandle, "System.Reflection") ||
                    !reader.StringComparer.Equals(nameHandle, "AssemblyMetadataAttribute"))
                    continue;

                var attributeTypeProvider = new CustomAttributeTypeProvider(module);
                CustomAttribute attribute = reader.GetCustomAttribute(attributeHandle);
                CustomAttributeValue<TypeDesc> decodedAttribute = attribute.DecodeValue(attributeTypeProvider);

                if (decodedAttribute.FixedArguments.Length != 2)
                    continue;

                if (decodedAttribute.FixedArguments[0].Value is string s && s == ".NETFrameworkAssembly")
                    return true;
            }

            return false;
        }
    }
}
