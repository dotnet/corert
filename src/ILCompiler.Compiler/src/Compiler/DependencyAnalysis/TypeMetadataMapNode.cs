// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

using Internal.NativeFormat;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a map between EETypes and metadata records within the <see cref="MetadataNode"/>.
    /// </summary>
    internal sealed class TypeMetadataMapNode : ObjectNode, ISymbolNode
    {
        private ObjectAndOffsetSymbolNode _endSymbol;
        private ExternalReferencesTableNode _externalReferences;

        public TypeMetadataMapNode(ExternalReferencesTableNode externalReferences)
        {
            _endSymbol = new ObjectAndOffsetSymbolNode(this, 0, ((ISymbolNode)this).MangledName + "End");
            _externalReferences = externalReferences;
        }

        public ISymbolNode EndSymbol
        {
            get
            {
                return _endSymbol;
            }
        }

        string ISymbolNode.MangledName
        {
            get
            {
                return NodeFactory.CompilationUnitPrefix + "__type_to_metadata_map";
            }
        }

        int ISymbolNode.Offset
        {
            get
            {
                return 0;
            }
        }

        public override ObjectNodeSection Section
        {
            get
            {
                return ObjectNodeSection.DataSection;
            }
        }

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return true;
            }
        }

        protected override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // This node does not trigger generation of other nodes.
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolNode[] { this });

            var writer = new NativeWriter();
            var typeMapHashTable = new VertexHashtable();

            Section hashTableSection = writer.NewSection();
            hashTableSection.Place(typeMapHashTable);

            foreach (var mappingEntry in factory.MetadataManager.GetTypeDefinitionMapping())
            {
                if (!factory.CompilationModuleGroup.ContainsType(mappingEntry.Entity))
                    continue;

                // We are looking for any EEType - constructed or not, it has to be in the mapping
                // table so that we can map it to metadata.
                EETypeNode node = null;
                
                if (!mappingEntry.Entity.IsGenericDefinition)
                {
                    node = factory.ConstructedTypeSymbol(mappingEntry.Entity) as EETypeNode;
                }
                
                if (node == null || !node.Marked)
                {
                    // This might have been a typeof() expression.
                    node = factory.NecessaryTypeSymbol(mappingEntry.Entity) as EETypeNode;
                }

                if (node.Marked)
                {
                    Vertex vertex = writer.GetTuple(
                        writer.GetUnsignedConstant(_externalReferences.GetIndex(node)),
                        writer.GetUnsignedConstant((uint)mappingEntry.MetadataHandle)
                        );

                    int hashCode = node.Type.GetHashCode();
                    typeMapHashTable.Append((uint)hashCode, hashTableSection.Place(vertex));
                }
            }

            MemoryStream ms = new MemoryStream();
            writer.Save(ms);
            byte[] hashTableBytes = ms.ToArray();

            _endSymbol.SetSymbolOffset(hashTableBytes.Length);

            return new ObjectData(hashTableBytes, Array.Empty<Relocation>(), 1, new ISymbolNode[] { this, _endSymbol });
        }
    }
}
