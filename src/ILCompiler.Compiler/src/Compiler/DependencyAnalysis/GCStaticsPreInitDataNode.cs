// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using Internal.Text;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Contains all GC static fields for a particular EEType.
    /// Fields that have preinitialized data are pointer reloc pointing to frozen objects.
    /// Other fields are initialized with 0.
    /// We simply memcpy these over the GC static EEType object.
    /// </summary>
    public class GCStaticsPreInitDataNode : ObjectNode, ISymbolDefinitionNode
    {
        private MetadataType _type;
        private List<PreInitFieldInfo> _sortedPreInitFields;

        public GCStaticsPreInitDataNode(MetadataType type, List<PreInitFieldInfo> preInitFields)
        {
            Debug.Assert(!type.IsCanonicalSubtype(CanonicalFormKind.Specific));
            _type = type;

            // sort the PreInitFieldInfo to appear in increasing offset order for easier emitting
            _sortedPreInitFields = new List<PreInitFieldInfo>(preInitFields);
            _sortedPreInitFields.Sort(PreInitFieldInfo.FieldDescCompare);
        }

        protected override string GetName(NodeFactory factory) => GetMangledName(_type, factory.NameMangler);

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(GetMangledName(_type, nameMangler));
        }

        public int Offset => 0;
        public MetadataType Type => _type;

        public static string GetMangledName(TypeDesc type, NameMangler nameMangler)
        {
            return nameMangler.NodeMangler.GCStatics(type) + "__PreInitData";
        }

        public override bool StaticDependenciesAreComputed => true;

        public override ObjectNodeSection Section => ObjectNodeSection.ReadOnlyDataSection;
        public override bool IsShareable => EETypeNode.IsTypeNodeShareable(_type);

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // We only need this for CoreRT (at least for now) as we emit static field value directly in GCStaticsNode for N
            Debug.Assert(factory.Target.Abi == TargetAbi.CoreRT);

            return GetDataForPreInitDataField(
                this, _type, _sortedPreInitFields, 
                factory.Target.PointerSize,     // CoreRT static size calculation includes EEType - skip it
                factory, relocsOnly);
        }

        public static ObjectData GetDataForPreInitDataField(
            ISymbolDefinitionNode node, 
            MetadataType _type, List<PreInitFieldInfo> sortedPreInitFields,
            int startOffset,
            NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder builder = new ObjectDataBuilder(factory, relocsOnly);

            builder.RequireInitialAlignment(_type.GCStaticFieldAlignment.AsInt);

            int staticOffset = startOffset;
            int staticOffsetEnd = _type.GCStaticFieldSize.AsInt;
            int idx = 0;

            while (staticOffset < staticOffsetEnd)
            {
                PreInitFieldInfo fieldInfo = idx < sortedPreInitFields.Count ? sortedPreInitFields[idx] : null;
                int writeTo = staticOffsetEnd;
                if (fieldInfo != null)
                    writeTo = fieldInfo.Field.Offset.AsInt;

                // Emit the zero before the next preinitField
                builder.EmitZeros(writeTo - staticOffset);
                staticOffset = writeTo;

                if (fieldInfo != null)
                {
                    int count = builder.CountBytes;

                    if (fieldInfo.Field.FieldType.IsValueType)
                    {
                        // Emit inlined data for value types
                        fieldInfo.WriteData(ref builder, factory);
                    }
                    else
                    {
                        // Emit a pointer reloc to the frozen data for reference types
                        builder.EmitPointerReloc(factory.SerializedFrozenArray(fieldInfo));
                    }

                    staticOffset += builder.CountBytes - count;
                    idx++;
                }
            }

            builder.AddSymbol(node);

            return builder.ToObjectData();
        }

        public override int ClassCode => 1148300665;

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return comparer.Compare(_type, ((GCStaticsPreInitDataNode)other)._type);
        }
    }
}
