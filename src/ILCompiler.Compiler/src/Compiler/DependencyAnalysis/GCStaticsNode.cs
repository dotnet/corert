// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        public GCStaticsNode(MetadataType type)
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
            DependencyListEntry[] result;
            if (context.TypeInitializationManager.HasEagerStaticConstructor(_type))
            {
                result = new DependencyListEntry[3];
                result[2] = new DependencyListEntry(context.EagerCctorIndirection(_type.GetStaticConstructor()), "Eager .cctor");
            }
            else
                result = new DependencyListEntry[2];

            result[0] = new DependencyListEntry(context.GCStaticsRegion, "GCStatics Region");
            result[1] = new DependencyListEntry(GetGCStaticEETypeNode(context), "GCStatic EEType");
            return result;
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
