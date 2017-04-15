// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

using Internal.Text;
using Internal.TypeSystem;
using Internal.NativeFormat;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a hashtable with information about all statics regions for all compiled generic types.
    /// </summary>
    internal sealed class StaticsInfoHashtableNode : ObjectNode, ISymbolNode
    {
        private ObjectAndOffsetSymbolNode _endSymbol;
        private ExternalReferencesTableNode _externalReferences;
        private ExternalReferencesTableNode _nativeStaticsReferences;

        public StaticsInfoHashtableNode(ExternalReferencesTableNode externalReferences, ExternalReferencesTableNode nativeStaticsReferences)
        {
            _endSymbol = new ObjectAndOffsetSymbolNode(this, 0, "_StaticsInfoHashtableNode_End", true);
            _externalReferences = externalReferences;
            _nativeStaticsReferences = nativeStaticsReferences;
        }

        public ISymbolNode EndSymbol => _endSymbol;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix).Append("_StaticsInfoHashtableNode");
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
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolNode[] { this });

            NativeWriter writer = new NativeWriter();
            VertexHashtable hashtable = new VertexHashtable();
            Section section = writer.NewSection();

            section.Place(hashtable);

            foreach (var type in factory.MetadataManager.GetTypesWithConstructedEETypes())
            {
                if (!type.HasInstantiation || type.IsCanonicalSubtype(CanonicalFormKind.Any) || type.IsGenericDefinition)
                    continue;

                MetadataType metadataType = type as MetadataType;
                if (metadataType == null)
                    continue;

                VertexBag bag = new VertexBag();

                if (metadataType.GCStaticFieldSize.AsInt > 0)
                {
                    ISymbolNode gcStaticIndirection = factory.Indirection(factory.TypeGCStaticsSymbol(metadataType));
                    bag.AppendUnsigned(BagElementKind.GcStaticData, _nativeStaticsReferences.GetIndex(gcStaticIndirection));
                }
                if (metadataType.NonGCStaticFieldSize.AsInt > 0)
                {
                    ISymbolNode nonGCStaticIndirection = factory.Indirection(factory.TypeNonGCStaticsSymbol(metadataType));
                    bag.AppendUnsigned(BagElementKind.NonGcStaticData, _nativeStaticsReferences.GetIndex(nonGCStaticIndirection));
                }

                // TODO: TLS

                if (bag.ElementsCount > 0)
                {
                    uint typeId = _externalReferences.GetIndex(factory.NecessaryTypeSymbol(type));
                    Vertex staticsInfo = writer.GetTuple(writer.GetUnsignedConstant(typeId), bag);

                    hashtable.Append((uint)type.GetHashCode(), section.Place(staticsInfo));
                }
            }

            byte[] hashTableBytes = writer.Save();

            _endSymbol.SetSymbolOffset(hashTableBytes.Length);

            return new ObjectData(hashTableBytes, Array.Empty<Relocation>(), 1, new ISymbolNode[] { this, _endSymbol });
        }
    }
}
