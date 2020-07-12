// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.Modules;
using System.Reflection.Runtime.Modules.EcmaFormat;
using System.Reflection.Runtime.MethodInfos;
using System.Reflection.Runtime.MethodInfos.EcmaFormat;
using System.Reflection.Runtime.TypeInfos;
using System.Reflection.Runtime.TypeInfos.EcmaFormat;
using System.Reflection.Runtime.TypeParsing;
using System.Reflection.Runtime.CustomAttributes;
using System.Collections.Generic;

using Internal.Reflection.Core;
using Internal.Reflection.Core.Execution;

using Internal.Reflection.Tracing;

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

using Internal.Runtime.TypeLoader;

namespace System.Reflection.Runtime.Assemblies.EcmaFormat
{
    internal sealed partial class EcmaFormatRuntimeAssembly : RuntimeAssembly
    {
        private readonly string _location;
        public override string Location => _location ?? "";

        private EcmaFormatRuntimeAssembly(MetadataReader reader, string assemblyPath)
        {
            AssemblyDefinition = reader.GetAssemblyDefinition();
            MetadataReader = reader;
            _location = assemblyPath;
        }

        public sealed override IEnumerable<CustomAttributeData> CustomAttributes
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.Assembly_CustomAttributes(this);
#endif
                foreach (CustomAttributeData cad in RuntimeCustomAttributeData.GetCustomAttributes(MetadataReader, MetadataReader.GetAssemblyDefinition().GetCustomAttributes()))
                    yield return cad;
            }
        }

        public sealed override IEnumerable<TypeInfo> DefinedTypes
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.Assembly_DefinedTypes(this);
#endif
                TypeDefinitionHandleCollection allTypes = MetadataReader.TypeDefinitions;

                bool firstType = true; // The first type is always the module type, which isn't returned by this api.
                foreach (TypeDefinitionHandle typeDefinitionHandle in allTypes)
                {
                    if (firstType)
                    {
                        firstType = false;
                        continue;
                    }
                    yield return typeDefinitionHandle.GetNamedType(MetadataReader);
                }
            }
        }

        public sealed override IEnumerable<Type> ExportedTypes
        {
            get
            {
                return Array.Empty<Type>();
                /*
                 * TODO! Implement
                foreach (QScopeDefinition scope in AllScopes)
                {
                    MetadataReader reader = scope.Reader;
                    ScopeDefinition scopeDefinition = scope.ScopeDefinition;
                    IEnumerable<NamespaceDefinitionHandle> topLevelNamespaceHandles = new NamespaceDefinitionHandle[] { scopeDefinition.RootNamespaceDefinition };
                    IEnumerable<NamespaceDefinitionHandle> allNamespaceHandles = reader.GetTransitiveNamespaces(topLevelNamespaceHandles);
                    IEnumerable<TypeDefinitionHandle> allTopLevelTypes = reader.GetTopLevelTypes(allNamespaceHandles);
                    IEnumerable<TypeDefinitionHandle> allTypes = reader.GetTransitiveTypes(allTopLevelTypes, publicOnly: true);
                    foreach (TypeDefinitionHandle typeDefinitionHandle in allTypes)
                        yield return typeDefinitionHandle.ResolveTypeDefinition(reader);
                }*/
            }
        }

        protected sealed override IEnumerable<TypeForwardInfo> TypeForwardInfos
        {
            get
            {
                MetadataReader reader = MetadataReader;
                foreach (ExportedTypeHandle exportedTypeHandle in reader.ExportedTypes)
                {
                    ExportedType exportedType = reader.GetExportedType(exportedTypeHandle);
                    if (!exportedType.IsForwarder)
                        continue;

                    EntityHandle implementation = exportedType.Implementation;
                    if (implementation.Kind != HandleKind.AssemblyReference) // This check also weeds out nested types. This is intentional.
                        continue;
                    RuntimeAssemblyName redirectedAssemblyName = ((AssemblyReferenceHandle)implementation).ToRuntimeAssemblyName(reader);

                    string typeName = exportedType.Name.GetString(reader);
                    string namespaceName = exportedType.Namespace.GetString(reader);

                    yield return new TypeForwardInfo(redirectedAssemblyName, namespaceName, typeName);
                }
            }
        }

        private unsafe struct InternalManifestResourceInfo
        {
            public bool Found;
            public string FileName;
            public Assembly ReferencedAssembly;
            public byte* PointerToResource;
            public uint SizeOfResource;
            public ResourceLocation ResourceLocation;
        }

        private unsafe InternalManifestResourceInfo GetInternalManifestResourceInfo(string resourceName)
        {
            InternalManifestResourceInfo result = new InternalManifestResourceInfo();
            ManifestResourceHandleCollection manifestResources = MetadataReader.ManifestResources;
            foreach (var resourceHandle in manifestResources)
            {
                ManifestResource resource = MetadataReader.GetManifestResource(resourceHandle);
                if (MetadataReader.StringComparer.Equals(resource.Name, resourceName))
                {
                    result.Found = true;
                    if (resource.Implementation.IsNil)
                    {
                        checked
                        {
                            // Embedded data resource
                            result.ResourceLocation = ResourceLocation.Embedded | ResourceLocation.ContainedInManifestFile;
                            PEReader pe = PEReader;

                            PEMemoryBlock resourceDirectory = pe.GetSectionData(pe.PEHeaders.CorHeader.ResourcesDirectory.RelativeVirtualAddress);
                            BlobReader reader = resourceDirectory.GetReader((int)resource.Offset, resourceDirectory.Length - (int)resource.Offset);
                            uint length = reader.ReadUInt32();
                            result.PointerToResource = reader.CurrentPointer;

                            // Length check the size of the resource to ensure it fits in the PE file section, note, this is only safe as its in a checked region
                            if (length + sizeof(Int32) > reader.Length)
                                throw new BadImageFormatException();
                            result.SizeOfResource = length;
                        }
                    }
                    else
                    {
                        if (resource.Implementation.Kind == HandleKind.AssemblyFile)
                        {
                            // Get file name
                            result.ResourceLocation = default(ResourceLocation);
                            AssemblyFile file = MetadataReader.GetAssemblyFile((AssemblyFileHandle)resource.Implementation);
                            if (file.ContainsMetadata)
                            {
                                result.ResourceLocation = ResourceLocation.Embedded;
                                throw new PlatformNotSupportedException(); // Support for multi-module assemblies is not implemented on this platform
                            }
                            result.FileName = MetadataReader.GetString(file.Name);
                        }
                        else if (resource.Implementation.Kind == HandleKind.AssemblyReference)
                        {
                            // Resolve assembly reference
                            result.ResourceLocation = ResourceLocation.ContainedInAnotherAssembly;
                            RuntimeAssemblyName destinationAssemblyName = ((AssemblyReferenceHandle)resource.Implementation).ToRuntimeAssemblyName(MetadataReader);
                            result.ReferencedAssembly = RuntimeAssembly.GetRuntimeAssemblyIfExists(destinationAssemblyName);
                        }
                    }
                }
            }

            return result;
        }

        public sealed override ManifestResourceInfo GetManifestResourceInfo(string resourceName)
        {
            if (resourceName == null)
                throw new ArgumentNullException(nameof(resourceName));
            if (resourceName.Equals(""))
                throw new ArgumentException(nameof(resourceName));

            InternalManifestResourceInfo internalManifestResourceInfo = GetInternalManifestResourceInfo(resourceName);

            if (internalManifestResourceInfo.ResourceLocation == ResourceLocation.ContainedInAnotherAssembly)
            {
                // Must get resource info from other assembly, and OR in the contained in another assembly information
                ManifestResourceInfo underlyingManifestResourceInfo = internalManifestResourceInfo.ReferencedAssembly.GetManifestResourceInfo(resourceName);
                internalManifestResourceInfo.FileName = underlyingManifestResourceInfo.FileName;
                internalManifestResourceInfo.ResourceLocation = underlyingManifestResourceInfo.ResourceLocation | ResourceLocation.ContainedInAnotherAssembly;
                if (underlyingManifestResourceInfo.ReferencedAssembly != null)
                    internalManifestResourceInfo.ReferencedAssembly = underlyingManifestResourceInfo.ReferencedAssembly;
            }

            return new ManifestResourceInfo(internalManifestResourceInfo.ReferencedAssembly, internalManifestResourceInfo.FileName, internalManifestResourceInfo.ResourceLocation);
        }

        public sealed override String[] GetManifestResourceNames()
        {
            ManifestResourceHandleCollection manifestResources = MetadataReader.ManifestResources;
            string[] resourceNames = new string[manifestResources.Count];

            int iResource = 0;
            foreach (var resourceHandle in manifestResources)
            {
                ManifestResource resource = MetadataReader.GetManifestResource(resourceHandle);
                resourceNames[iResource] = MetadataReader.GetString(resource.Name);
                iResource++;
            }

            return resourceNames;
        }

        public sealed override Stream GetManifestResourceStream(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (name.Equals(""))
                throw new ArgumentException(nameof(name));

            InternalManifestResourceInfo internalManifestResourceInfo = GetInternalManifestResourceInfo(name);

            if ((internalManifestResourceInfo.ResourceLocation & ResourceLocation.Embedded) != 0)
            {
                unsafe
                {
                    return new UnmanagedMemoryStream(internalManifestResourceInfo.PointerToResource, internalManifestResourceInfo.SizeOfResource);
                }
            }
            else
            {
                if (internalManifestResourceInfo.ResourceLocation == ResourceLocation.ContainedInAnotherAssembly)
                {
                    return internalManifestResourceInfo.ReferencedAssembly.GetManifestResourceStream(name);
                }
                else
                {
                    // Linked resource case.
                    // TODO Implement linked resources, when FileStream, and CodeBase are fully implemented
                    throw new NotImplementedException();
                }
            }
        }

        public sealed override string ImageRuntimeVersion
        {
            get
            {
                return MetadataReader.MetadataVersion;
            }
        }

        public sealed override Module ManifestModule
        {
            get
            {
                return EcmaFormatRuntimeModule.GetRuntimeModule(this);
            }
        }

        internal sealed override RuntimeAssemblyName RuntimeAssemblyName
        {
            get
            {
                return AssemblyDefinition.ToRuntimeAssemblyName(MetadataReader);
            }
        }

        public sealed override bool Equals(Object obj)
        {
            EcmaFormatRuntimeAssembly other = obj as EcmaFormatRuntimeAssembly;
            return Equals(other);
        }

        public bool Equals(EcmaFormatRuntimeAssembly other)
        {
            if (other == null)
                return false;

            return Object.ReferenceEquals(other.MetadataReader, MetadataReader);
        }

        public sealed override int GetHashCode()
        {
            return MetadataReader.GetHashCode();
        }

        public sealed override MethodInfo EntryPoint
        {
            get
            {
                CorHeader corHeader = PEReader.PEHeaders.CorHeader;
                if ((corHeader.Flags & CorFlags.NativeEntryPoint) != 0)
                {
                    // Entrypoint is an RVA to an unmanaged method 
                    return null;
                }

                int entryPointToken = corHeader.EntryPointTokenOrRelativeVirtualAddress;
                if (entryPointToken == 0)
                {
                    // No entrypoint 
                    return null;
                }

                Handle handle = MetadataTokens.Handle(entryPointToken);

                if (handle.Kind != HandleKind.MethodDefinition)
                {
                    return null;
                }

                MethodDefinitionHandle methodHandle = (MethodDefinitionHandle)handle;
                TypeDefinitionHandle declaringType = MetadataReader.GetMethodDefinition(methodHandle).GetDeclaringType();
                RuntimeTypeInfo runtimeType = declaringType.GetNamedType(MetadataReader);

                return RuntimeNamedMethodInfo<EcmaFormatMethodCommon>.GetRuntimeNamedMethodInfo(new EcmaFormatMethodCommon(methodHandle, (EcmaFormatRuntimeNamedTypeInfo)runtimeType, runtimeType), runtimeType);
            }
        }

        internal AssemblyDefinition AssemblyDefinition { get; }
        internal MetadataReader MetadataReader { get; }
        internal PEReader PEReader
        {
            get
            {
                EcmaModuleInfo moduleInfo = ModuleList.Instance.GetModuleInfoForMetadataReader(MetadataReader);
                return moduleInfo.PE;
            }
        }

        internal sealed override void RunModuleConstructor()
        {
            // TODO: throw InvalidOperationException for introspection only assemblies
            // TODO: ensure module is loaded and that the module cctor ran. If the module is already
            // loaded, do nothing and return (cctor already ran).
            throw new NotImplementedException();
        }
    }
}
