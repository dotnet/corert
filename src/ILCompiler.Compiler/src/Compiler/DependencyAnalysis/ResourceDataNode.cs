﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Text;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Blob of data containing resources for all assemblies generated into the image.
    /// Resources are simply copied from the inputs and concatenated into this blob.
    /// All format information is provided by <see cref="ResourceIndexNode"/>
    /// </summary>
    internal class ResourceDataNode : ObjectNode, ISymbolNode
    {
        /// <summary>
        /// Resource index information generated while extracting resources into the data blob
        /// </summary>
        private List<ResourceIndexData> IndexData { get; set; }
        private int TotalLength { get; set; }

        public ResourceDataNode()
        {
            _endSymbol = new ObjectAndOffsetSymbolNode(this, 0, "__embedded_resourcedata_End", true);
        }

        private ObjectAndOffsetSymbolNode _endSymbol;
        public ISymbolNode EndSymbol => _endSymbol;

        public override bool IsShareable => false;

        public override ObjectNodeSection Section => ObjectNodeSection.ReadOnlyDataSection;

        public override bool StaticDependenciesAreComputed => true;

        public int Offset => 0;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix).Append("__embedded_resourcedata");
        }

        protected override string GetName() => this.GetMangledName();

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // This node has no relocations.
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolNode[] { this });
            
            byte[] blob = GenerateResourceBlob(factory);
            return new ObjectData(
                blob,
                Array.Empty<Relocation>(),
                1,
                new ISymbolNode[]
                {
                                this,
                                EndSymbol
                });
        }

        public List<ResourceIndexData> GetOrCreateIndexData(NodeFactory factory)
        {
            if (IndexData != null)
            {
                return IndexData;
            }

            TotalLength = 0;
            IndexData = new List<ResourceIndexData>();
            // Build up index information
            foreach (EcmaAssembly module in factory.MetadataManager.GetModulesWithMetadata().OfType<EcmaAssembly>())
            {
                PEMemoryBlock resourceDirectory = module.PEReader.GetSectionData(module.PEReader.PEHeaders.CorHeader.ResourcesDirectory.RelativeVirtualAddress);

                try
                {
                    checked
                    {
                        foreach (var resourceHandle in module.MetadataReader.ManifestResources)
                        {
                            ManifestResource resource = module.MetadataReader.GetManifestResource(resourceHandle);

                            // Don't try to embed linked resources or resources in other assemblies
                            if (!resource.Implementation.IsNil)
                            {
                                continue;
                            }

                            string resourceName = module.MetadataReader.GetString(resource.Name);
                            string assemblyName = module.GetName().FullName;
                            BlobReader reader = resourceDirectory.GetReader((int)resource.Offset, resourceDirectory.Length - (int)resource.Offset);
                            int length = (int)reader.ReadUInt32();
                            ResourceIndexData indexData = new ResourceIndexData(assemblyName, resourceName, TotalLength, (int)resource.Offset + sizeof(Int32), module, length);
                            IndexData.Add(indexData);
                            TotalLength += length;
                        }
                    }
                }
                catch (OverflowException)
                {
                    throw new BadImageFormatException();
                }
            }

            return IndexData;
        }

        /// <summary>
        /// Extracts resources from all modules being compiled into a single blob and saves
        /// the information needed to create an index into that blob.
        /// </summary>
        private byte[] GenerateResourceBlob(NodeFactory factory)
        {
            GetOrCreateIndexData(factory);

            // Read resources into the blob
            byte[] resourceBlob = new byte[TotalLength];
            int currentPos = 0;
            foreach (ResourceIndexData indexData in IndexData)
            {
                EcmaModule module = indexData.EcmaModule;
                PEMemoryBlock resourceDirectory = module.PEReader.GetSectionData(module.PEReader.PEHeaders.CorHeader.ResourcesDirectory.RelativeVirtualAddress);
                Debug.Assert(currentPos == indexData.NativeOffset);
                BlobReader reader = resourceDirectory.GetReader(indexData.EcmaOffset, indexData.Length);
                byte[] resourceData = reader.ReadBytes(indexData.Length);
                Buffer.BlockCopy(resourceData, 0, resourceBlob, currentPos, resourceData.Length);
                currentPos += resourceData.Length;
            }

            _endSymbol.SetSymbolOffset(resourceBlob.Length);
            return resourceBlob;
        }
    }

    /// <summary>
    /// Data about individual manifest resources
    /// </summary>
    internal class ResourceIndexData
    {
        public ResourceIndexData(string assemblyName, string resourceName, int nativeOffset, int ecmaOffset, EcmaModule ecmaModule, int length)
        {
            AssemblyName = assemblyName;
            ResourceName = resourceName;
            NativeOffset = nativeOffset;
            EcmaOffset = ecmaOffset;
            EcmaModule = ecmaModule;
            Length = length;
        }

        /// <summary>
        /// Full name of the assembly that contains the resource
        /// </summary>
        public string AssemblyName { get; }

        /// <summary>
        /// Name of the resource
        /// </summary>
        public string ResourceName { get; }

        /// <summary>
        /// Offset of the resource within the native resource blob
        /// </summary>
        public int NativeOffset { get; }

        /// <summary>
        /// Offset of the resource within the .mresources section of the ECMA module
        /// </summary>
        public int EcmaOffset { get; }

        /// <summary>
        /// Module the resource is defined in
        /// </summary>
        public EcmaModule EcmaModule { get; }

        /// <summary>
        /// Length of the resource
        /// </summary>
        public int Length { get; }
    }
}