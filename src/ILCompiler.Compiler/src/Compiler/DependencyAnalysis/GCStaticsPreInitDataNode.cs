// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using Internal.Text;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    public class GCStaticsPreInitDataNode : ObjectNode, IExportableSymbolNode
    {
        private MetadataType _type;
        private List<FieldDesc> _sortedPreInitFields;

        public GCStaticsPreInitDataNode(MetadataType type, List<FieldDesc> sortedPreInitFields)
        {
            Debug.Assert(!type.IsCanonicalSubtype(CanonicalFormKind.Specific));
            _type = type;
            _sortedPreInitFields = sortedPreInitFields;
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.NodeMangler.GCStatics(_type) + "__PreInitData");
        }

        public int Offset => 0;
        public MetadataType Type => _type;

        public static string GetMangledName(TypeDesc type, NameMangler nameMangler)
        {
            return nameMangler.NodeMangler.GCStatics(type);
        }

        public virtual bool IsExported(NodeFactory factory) => factory.CompilationModuleGroup.ExportsType(Type);

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            // relocs has all the dependencies we need
            return null;
        }

        public override bool StaticDependenciesAreComputed => true;

        public override ObjectNodeSection Section => ObjectNodeSection.DataSection;
        public override bool IsShareable => EETypeNode.IsTypeNodeShareable(_type);

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // We only need this for CoreRT (at least for now) as we emit static field value directly in GCStaticsNode for N
            Debug.Assert(factory.Target.Abi == TargetAbi.CoreRT);

            return GetDataForPreInitDataField(this, _type, _sortedPreInitFields, factory, relocsOnly);
        }

        public static ObjectData GetDataForPreInitDataField(
            ISymbolDefinitionNode node, 
            MetadataType _type, List<FieldDesc> sortedPreInitFields,
            NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder builder = new ObjectDataBuilder(factory, relocsOnly);

            builder.RequireInitialAlignment(_type.GCStaticFieldAlignment.AsInt);

            int staticOffset = 0;
            int staticOffsetEnd = _type.GCStaticFieldSize.AsInt;
            int idx = 0;

            while (staticOffset < staticOffsetEnd)
            {
                int writeTo = staticOffsetEnd;
                if (idx < sortedPreInitFields.Count)
                {
                    writeTo = sortedPreInitFields[idx].Offset.AsInt;
                }

                // Emit the zero before the next preinitField
                builder.EmitZeros(writeTo - staticOffset);
                staticOffset = writeTo;

                // Emit a pointer reloc to the frozen data
                if (idx < sortedPreInitFields.Count)
                {
                    builder.EmitPointerReloc(factory.SerializedFrozenArray(sortedPreInitFields[idx].PreInitDataField));
                    staticOffset += factory.Target.PointerSize;
                }
            }

            builder.AddSymbol(node);

            return builder.ToObjectData();
        }
    }
}
