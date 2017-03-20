// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

using Internal.Text;
using Internal.TypeSystem;
using Internal.NativeFormat;

using FieldTableFlags = Internal.Runtime.FieldTableFlags;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a map between reflection metadata and native field offsets.
    /// </summary>
    internal sealed class ReflectionFieldMapNode : ObjectNode, ISymbolNode
    {
        private ObjectAndOffsetSymbolNode _endSymbol;
        private ExternalReferencesTableNode _externalReferences;

        public ReflectionFieldMapNode(ExternalReferencesTableNode externalReferences)
        {
            _endSymbol = new ObjectAndOffsetSymbolNode(this, 0, "__field_to_offset_map_End", true);
            _externalReferences = externalReferences;
        }

        public ISymbolNode EndSymbol => _endSymbol;
        
        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix).Append("__field_to_offset_map");
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

            var writer = new NativeWriter();
            var fieldMapHashTable = new VertexHashtable();

            Section hashTableSection = writer.NewSection();
            hashTableSection.Place(fieldMapHashTable);

            foreach (var fieldMapping in factory.MetadataManager.GetFieldMapping(factory))
            {
                FieldDesc field = fieldMapping.Entity;

                if (field.IsLiteral || field.HasRva)
                    continue;

                FieldTableFlags flags;
                if (field.IsThreadStatic)
                {
                    flags = FieldTableFlags.ThreadStatic;
                }
                else if (field.IsStatic)
                {
                    flags = FieldTableFlags.Static;

                    if (field.HasGCStaticBase)
                        flags |= FieldTableFlags.IsGcSection;

                    if (field.OwningType.HasInstantiation)
                        flags |= FieldTableFlags.FieldOffsetEncodedDirectly;
                }
                else
                {
                    flags = FieldTableFlags.Instance | FieldTableFlags.FieldOffsetEncodedDirectly;
                }

                // TODO: support emitting field info without a handle for generics in multifile
                flags |= FieldTableFlags.HasMetadataHandle;

                if (field.OwningType.IsCanonicalSubtype(CanonicalFormKind.Universal))
                    flags |= FieldTableFlags.IsUniversalCanonicalEntry;

                // Grammar of a hash table entry:
                // Flags + DeclaringType + MdHandle or Name + Cookie or Ordinal or Offset

                Vertex vertex = writer.GetUnsignedConstant((uint)flags);

                uint declaringTypeId = _externalReferences.GetIndex(factory.NecessaryTypeSymbol(field.OwningType));
                vertex = writer.GetTuple(vertex,
                    writer.GetUnsignedConstant(declaringTypeId));

                if ((flags & FieldTableFlags.HasMetadataHandle) != 0)
                {
                    // Only store the offset portion of the metadata handle to get better integer compression
                    vertex = writer.GetTuple(vertex,
                        writer.GetUnsignedConstant((uint)(fieldMapping.MetadataHandle & MetadataManager.MetadataOffsetMask)));
                }
                else
                {
                    throw new NotImplementedException();
                }

                if ((flags & FieldTableFlags.IsUniversalCanonicalEntry) != 0)
                {
                    throw new NotImplementedException();
                }
                else
                {
                    switch (flags & FieldTableFlags.StorageClass)
                    {
                        case FieldTableFlags.ThreadStatic:
                            // TODO: thread statics
                            continue;

                        case FieldTableFlags.Static:
                            {
                                if (field.OwningType.HasInstantiation)
                                {
                                    vertex = writer.GetTuple(vertex, writer.GetUnsignedConstant((uint)(field.Offset.AsInt)));
                                }
                                else
                                {
                                    MetadataType metadataType = (MetadataType)field.OwningType;

                                    int cctorOffset = 0;
                                    if (!field.HasGCStaticBase && factory.TypeSystemContext.HasLazyStaticConstructor(metadataType))
                                        cctorOffset += NonGCStaticsNode.GetClassConstructorContextStorageSize(factory.TypeSystemContext.Target, metadataType);

                                    ISymbolNode staticsNode = field.HasGCStaticBase ?
                                        factory.TypeGCStaticsSymbol(metadataType) :
                                        factory.TypeNonGCStaticsSymbol(metadataType);

                                    if (!field.HasGCStaticBase || factory.Target.Abi == TargetAbi.ProjectN)
                                    {
                                        uint index = _externalReferences.GetIndex(staticsNode, field.Offset.AsInt + cctorOffset);
                                        vertex = writer.GetTuple(vertex, writer.GetUnsignedConstant(index));
                                    }
                                    else
                                    {
                                        Debug.Assert(field.HasGCStaticBase && factory.Target.Abi == TargetAbi.CoreRT);

                                        uint index = _externalReferences.GetIndex(staticsNode);
                                        vertex = writer.GetTuple(vertex, writer.GetUnsignedConstant(index));
                                        vertex = writer.GetTuple(vertex, writer.GetUnsignedConstant((uint)(field.Offset.AsInt + cctorOffset)));
                                    }
                                }
                            }
                            break;

                        case FieldTableFlags.Instance:
                            vertex = writer.GetTuple(vertex, writer.GetUnsignedConstant((uint)field.Offset.AsInt));
                            break;
                    }
                }

                int hashCode = field.OwningType.ConvertToCanonForm(CanonicalFormKind.Specific).GetHashCode();
                fieldMapHashTable.Append((uint)hashCode, hashTableSection.Place(vertex));
            }

            byte[] hashTableBytes = writer.Save();

            _endSymbol.SetSymbolOffset(hashTableBytes.Length);

            return new ObjectData(hashTableBytes, Array.Empty<Relocation>(), 1, new ISymbolNode[] { this, _endSymbol });
        }
    }
}
