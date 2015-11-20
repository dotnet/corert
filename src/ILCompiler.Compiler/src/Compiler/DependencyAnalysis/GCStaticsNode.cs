// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Internal.TypeSystem;
using ILCompiler.DependencyAnalysisFramework;

namespace ILCompiler.DependencyAnalysis
{
    public class GCStaticsNode : EmbeddedObjectNode, ISymbolNode
    {
        private MetadataType _type;

        public GCStaticsNode(MetadataType type, NodeFactory factory)
        {
            _type = type;
        }

        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }

        protected override void OnMarked(NodeFactory factory)
        {
            factory.GCStaticsRegion.AddEmbeddedObject(this);
        }

        string ISymbolNode.MangledName
        {
            get
            {
                return "__GCStaticBase_" + NodeFactory.NameMangler.GetMangledTypeName(_type);
            }
        }

        public ISymbolNode GetGCStaticEETypeNode(NodeFactory context)
        {
            // TODO Replace with better gcDesc computation algorithm when we add gc handling to the type system
            bool[] gcDesc = new bool[_type.GCStaticFieldSize / context.Target.PointerSize + 1];
            return context.GCStaticEEType(gcDesc);
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            return new DependencyListEntry[] { new DependencyListEntry(context.GCStaticsRegion, "GCStatics Region"),
                                               new DependencyListEntry(GetGCStaticEETypeNode(context), "GCStatic EEType")};
        }

        int ISymbolNode.Offset
        {
            get
            {
                return Offset;
            }
        }

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return true;
            }
        }

        public override void EncodeData(ref ObjectDataBuilder builder, NodeFactory factory, bool relocsOnly)
        {
            builder.RequirePointerAlignment();
            builder.EmitPointerReloc(GetGCStaticEETypeNode(factory));
        }
    }
}
