// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Internal.Text;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a frozen array
    /// </summary>
    public class FrozenArrayNode : EmbeddedObjectNode, ISymbolDefinitionNode
    {
        private PreInitFieldInfo _preInitFieldInfo;
        
        public FrozenArrayNode(PreInitFieldInfo preInitFieldInfo)
        {
            _preInitFieldInfo = preInitFieldInfo;
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix).Append("__FrozenArr_")
                .Append(nameMangler.GetMangledFieldName(_preInitFieldInfo.Field));
        }

        public override bool StaticDependenciesAreComputed => true;

        int ISymbolNode.Offset => 0;

        int ISymbolDefinitionNode.Offset
        {
            get
            {
                // The frozen array symbol points at the EEType portion of the object, skipping over the sync block
                return OffsetFromBeginningOfArray + _preInitFieldInfo.Field.Context.Target.PointerSize;
            }
        }

        private IEETypeNode GetEETypeNode(NodeFactory factory)
        {
            var fieldType = _preInitFieldInfo.Type;
            var node = factory.ConstructedTypeSymbol(fieldType);
            Debug.Assert(!node.RepresentsIndirectionCell);  // Array are always local
            return node;
        }

        public override void EncodeData(ref ObjectDataBuilder dataBuilder, NodeFactory factory, bool relocsOnly)
        {
            // Sync Block
            dataBuilder.EmitZeroPointer();

            // EEType
            dataBuilder.EmitPointerReloc(GetEETypeNode(factory));

            // numComponents
            dataBuilder.EmitInt(_preInitFieldInfo.Length);

            int pointerSize = _preInitFieldInfo.Field.Context.Target.PointerSize;
            Debug.Assert(pointerSize == 8 || pointerSize == 4);

            if (pointerSize == 8)
            {
                // padding numComponents in 64-bit
                dataBuilder.EmitInt(0);
            }

            // byte contents
            _preInitFieldInfo.WriteData(ref dataBuilder, factory, relocsOnly);
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            ObjectDataBuilder builder = new ObjectDataBuilder(factory, true);
            EncodeData(ref builder, factory, true);
            Relocation[] relocs = builder.ToObjectData().Relocs;
            DependencyList dependencies = null;

            if (relocs != null)
            {
                dependencies = new DependencyList();
                foreach (Relocation reloc in relocs)
                {
                    dependencies.Add(reloc.Target, "reloc");
                }
            }

            return dependencies;
        }

        protected override void OnMarked(NodeFactory factory)
        {
            factory.FrozenSegmentRegion.AddEmbeddedObject(this);
        }

        public override int ClassCode => 1789429316;

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return _preInitFieldInfo.CompareTo(((FrozenArrayNode)other)._preInitFieldInfo, comparer);
        }
    }
}
