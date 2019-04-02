// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.NativeFormat;
using Internal.Runtime;
using Internal.Text;
using Internal.TypeSystem;
using Internal.TypeSystem.Interop;

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
                //  managed struct type
                //  NumFields<< 2 | (HasInvalidLayout ? (2:0)) | (MarshallingRequired ? (1:0))
                //  If MarshallingRequired:
                //    size
                //    struct marshalling thunk
                //    struct unmarshalling thunk
                //    struct cleanup thunk
                //  For each field field:
                //     name
                //     offset

                var structType = structEntry.StructType;
                var nativeType = structEntry.NativeStructType;

                Vertex marshallingData = null;
                if (MarshalHelpers.IsStructMarshallingRequired(structType))
                {
                    Vertex thunks = writer.GetTuple(
                        writer.GetUnsignedConstant(_externalReferences.GetIndex(factory.MethodEntrypoint(structEntry.MarshallingThunk))),
                        writer.GetUnsignedConstant(_externalReferences.GetIndex(factory.MethodEntrypoint(structEntry.UnmarshallingThunk))),
                        writer.GetUnsignedConstant(_externalReferences.GetIndex(factory.MethodEntrypoint(structEntry.CleanupThunk))));

                    uint size = (uint)nativeType.InstanceFieldSize.AsInt;
                    marshallingData = writer.GetTuple(writer.GetUnsignedConstant(size), thunks);
                }

                Vertex fieldOffsetData = null;
                for (int i = 0; i < nativeType.Fields.Length; i++)
                {
                    var row = writer.GetTuple(
                        writer.GetStringConstant(nativeType.Fields[i].Name),
                        writer.GetUnsignedConstant((uint)nativeType.Fields[i].Offset.AsInt)
                        );

                    fieldOffsetData = (fieldOffsetData != null) ? writer.GetTuple(fieldOffsetData, row) : row;
                }

                uint mask = (uint)((marshallingData != null) ? InteropDataConstants.HasMarshallers : 0) |
                            (uint)(nativeType.HasInvalidLayout ? InteropDataConstants.HasInvalidLayout : 0) |
                            (uint)(nativeType.Fields.Length << InteropDataConstants.FieldCountShift);

                Vertex data = writer.GetUnsignedConstant(mask);
                if (marshallingData != null)
                    data = writer.GetTuple(data, marshallingData);

                if (fieldOffsetData != null)
                    data = writer.GetTuple(data, fieldOffsetData);

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
