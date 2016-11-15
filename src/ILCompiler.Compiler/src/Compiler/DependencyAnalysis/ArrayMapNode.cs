// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

using Internal.NativeFormat;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a hash table of array types generated into the image.
    /// </summary>
    internal sealed class ArrayMapNode : ObjectNode, ISymbolNode
    {
        private ObjectAndOffsetSymbolNode _endSymbol;
        private ExternalReferencesTableNode _externalReferences;

        public ArrayMapNode(ExternalReferencesTableNode externalReferences)
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
                return NodeFactory.CompilationUnitPrefix + "__array_type_map";
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

            foreach (var arrayType in factory.MetadataManager.GetArrayTypeMapping())
            {
                if (!arrayType.IsSzArray)
                    continue;

                // TODO: This should only be emitted for arrays of value types. The type loader builds everything else.

                // Go with a necessary type symbol. It will be upgraded to a constructed one if a constructed was emitted.
                IEETypeNode arrayTypeSymbol = factory.NecessaryTypeSymbol(arrayType);

                Vertex vertex = writer.GetUnsignedConstant(_externalReferences.GetIndex(arrayTypeSymbol));

                int hashCode = arrayType.GetHashCode();
                typeMapHashTable.Append((uint)hashCode, hashTableSection.Place(vertex));
            }

            MemoryStream ms = new MemoryStream();
            writer.Save(ms);
            byte[] hashTableBytes = ms.ToArray();

            _endSymbol.SetSymbolOffset(hashTableBytes.Length);

            return new ObjectData(hashTableBytes, Array.Empty<Relocation>(), 1, new ISymbolNode[] { this, _endSymbol });
        }
    }
}
