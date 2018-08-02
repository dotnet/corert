// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.NativeFormat;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a hash table of struct marshalling stub types generated into the image.
    /// </summary>
    internal sealed class StructMarshallingStubMapNode : ObjectNode, ISymbolDefinitionNode
    {
        private ObjectAndOffsetSymbolNode _endSymbol;
        private ExternalReferencesTableNode _externalReferences;

        public StructMarshallingStubMapNode(ExternalReferencesTableNode externalReferences)
        {
            _endSymbol = new ObjectAndOffsetSymbolNode(this, 0, "__struct_marshalling_stub_map_End", true);
            _externalReferences = externalReferences;
        }

        public ISymbolDefinitionNode EndSymbol => _endSymbol;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix).Append("__struct_marshalling_stub_map");
        }
        public int Offset => 0;
        public override bool IsShareable => false;

        public override ObjectNodeSection Section => _externalReferences.Section;

        public override bool StaticDependenciesAreComputed => true;

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // This node does not trigger generation of other nodes.
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this });

            var writer = new NativeWriter();
            var typeMapHashTable = new VertexHashtable();

            Section hashTableSection = writer.NewSection();
            hashTableSection.Place(typeMapHashTable);

            foreach (var structEntry in ((CompilerGeneratedInteropStubManager)factory.InteropStubManager).GetStructMarshallingTypes())
            {
                // the order of data written is as follows:
                //  0. managed struct type
                //  1. struct marshalling thunk
                //  2. struct unmarshalling thunk
                //  3. struct cleanup thunk
                //  4. size
                //  5. NumFields<< 1 | HasInvalidLayout
                //  6  for each field
                //      a. name
                //      b. offset

                var structType = structEntry.StructType;
                var nativeType = structEntry.NativeStructType;
                Vertex thunks= writer.GetTuple(
                    writer.GetUnsignedConstant(_externalReferences.GetIndex(factory.MethodEntrypoint(structEntry.MarshallingThunk))),
                    writer.GetUnsignedConstant(_externalReferences.GetIndex(factory.MethodEntrypoint(structEntry.UnmarshallingThunk))),
                    writer.GetUnsignedConstant(_externalReferences.GetIndex(factory.MethodEntrypoint(structEntry.CleanupThunk))));

                uint size = (uint)nativeType.InstanceFieldSize.AsInt;
                uint mask = (uint)(nativeType.Fields.Length << 1)  | (uint)(nativeType.HasInvalidLayout ? 1 : 0);

                Vertex data = writer.GetTuple(
                     thunks,
                     writer.GetUnsignedConstant(size),
                     writer.GetUnsignedConstant(mask)
                    );

                for (int i = 0; i < nativeType.Fields.Length; i++)
                {
                    data = writer.GetTuple(
                        data,
                        writer.GetStringConstant(nativeType.Fields[i].Name),
                        writer.GetUnsignedConstant((uint)nativeType.Fields[i].Offset.AsInt)
                        );
                }

                Vertex vertex = writer.GetTuple(
                    writer.GetUnsignedConstant(_externalReferences.GetIndex(factory.NecessaryTypeSymbol(structType))),
                    data
                );

                int hashCode = structType.GetHashCode();
                typeMapHashTable.Append((uint)hashCode, hashTableSection.Place(vertex));
            }

            byte[] hashTableBytes = writer.Save();

            _endSymbol.SetSymbolOffset(hashTableBytes.Length);

            return new ObjectData(hashTableBytes, Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this, _endSymbol });
        }

        protected internal override int Phase => (int)ObjectNodePhase.Ordered;
        public override int ClassCode => (int)ObjectNodeOrder.StructMarshallingStubMapNode;
    }
}
