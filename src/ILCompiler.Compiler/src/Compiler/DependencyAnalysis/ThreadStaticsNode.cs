﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Internal.TypeSystem;
using System.Collections.Generic;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents the thread static region of a given type. This is very similar to <see cref="GCStaticsNode"/>,
    /// since the actual storage will be allocated on the GC heap at runtime and is allowed to contain GC pointers.
    /// </summary>
    public class ThreadStaticsNode : EmbeddedObjectNode, ISymbolNode
    {
        MetadataType _type;

        public ThreadStaticsNode(MetadataType type, NodeFactory factory)
        {
            _type = type;
        }

        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }

        protected override void OnMarked(NodeFactory factory)
        {
            factory.ThreadStaticsRegion.AddEmbeddedObject(this);
        }

        string ISymbolNode.MangledName
        {
            get
            {
                return "__ThreadStaticBase_" + NodeFactory.NameMangler.GetMangledTypeName(_type);
            }
        }

        public ISymbolNode GetGCStaticEETypeNode(NodeFactory context)
        {
            // TODO Replace with better gcDesc computation algorithm when we add gc handling to the type system
            // TODO This logic should be shared with GCStaticsNode.
            bool[] gcDesc = new bool[_type.ThreadStaticFieldSize / context.Target.PointerSize + 1];
            return context.GCStaticEEType(gcDesc);
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {

            return new DependencyListEntry[] { new DependencyListEntry(context.ThreadStaticsRegion, "ThreadStatics Region"),
                                               new DependencyListEntry(GetGCStaticEETypeNode(context), "ThreadStatic EEType")};
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

            // At runtime, an instance of the GCStaticEEType will be created and a GCHandle to it
            // will be written in this location.
            builder.EmitPointerReloc(GetGCStaticEETypeNode(factory));
        }
    }
}
