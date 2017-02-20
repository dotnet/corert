// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.NativeFormat;
using Internal.Text;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a hash table of delegate marshalling stub types generated into the image.
    /// </summary>
    internal sealed class DelegateMarshallingStubMapNode : ObjectNode, ISymbolNode
    {
        private ObjectAndOffsetSymbolNode _endSymbol;
        private ExternalReferencesTableNode _externalReferences;

        public DelegateMarshallingStubMapNode(ExternalReferencesTableNode externalReferences)
        {
            _endSymbol = new ObjectAndOffsetSymbolNode(this, 0, "__delegate_marshalling_stub_map_End", true);
            _externalReferences = externalReferences;
        }

        public ISymbolNode EndSymbol => _endSymbol;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix).Append("__delegate_marshalling_stub_map");
        }
        public int Offset => 0;
        public override bool IsShareable => false;

        public override ObjectNodeSection Section => _externalReferences.Section;

        public override bool StaticDependenciesAreComputed => true;

        protected override string GetName() => this.GetMangledName();

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // This node does not trigger generation of other nodes.
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolNode[] { this });

            var writer = new NativeWriter();
            var typeMapHashTable = new VertexHashtable();

            Section hashTableSection = writer.NewSection();
            hashTableSection.Place(typeMapHashTable);

            foreach (var delegateEntry in factory.MetadataManager.DelegateMarshalingThunks)
            {
                Internal.TypeSystem.TypeDesc delegateType = ((Internal.IL.Stubs.DelegateMarshallingMethodThunk)delegateEntry.Value).DelegateType;
                Vertex vertex = writer.GetTuple(
                    writer.GetUnsignedConstant(_externalReferences.GetIndex(factory.NecessaryTypeSymbol(delegateType))),
                    writer.GetUnsignedConstant(_externalReferences.GetIndex(factory.MethodEntrypoint(delegateEntry.Value)))
                );

                int hashCode = delegateType.GetHashCode();
                typeMapHashTable.Append((uint)hashCode, hashTableSection.Place(vertex));
            }

            byte[] hashTableBytes = writer.Save();

            _endSymbol.SetSymbolOffset(hashTableBytes.Length);

            return new ObjectData(hashTableBytes, Array.Empty<Relocation>(), 1, new ISymbolNode[] { this, _endSymbol });
        }
    }
}
