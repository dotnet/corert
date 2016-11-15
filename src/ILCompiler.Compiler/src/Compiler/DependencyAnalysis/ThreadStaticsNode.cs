// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents the thread static region of a given type. This is very similar to <see cref="GCStaticsNode"/>,
    /// since the actual storage will be allocated on the GC heap at runtime and is allowed to contain GC pointers.
    /// </summary>
    public class ThreadStaticsNode : EmbeddedObjectNode, ISymbolNode
    {
        private MetadataType _type;

        public ThreadStaticsNode(MetadataType type, NodeFactory factory)
        {
            _type = type;
        }

        protected override string GetName() => this.GetMangledName();

        protected override void OnMarked(NodeFactory factory)
        {
            factory.ThreadStaticsRegion.AddEmbeddedObject(this);
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("__ThreadStaticBase_").Append(NodeFactory.NameMangler.GetMangledTypeName(_type));
        }

        private ISymbolNode GetGCStaticEETypeNode(NodeFactory factory)
        {
            GCPointerMap map = GCPointerMap.FromThreadStaticLayout(_type);
            return factory.GCStaticEEType(map);
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            DependencyListEntry[] result;
            if (factory.TypeSystemContext.HasEagerStaticConstructor(_type))
            {
                result = new DependencyListEntry[3];
                result[2] = new DependencyListEntry(factory.EagerCctorIndirection(_type.GetStaticConstructor()), "Eager .cctor");
            }
            else
                result = new DependencyListEntry[2];

            result[0] = new DependencyListEntry(factory.ThreadStaticsRegion, "ThreadStatics Region");
            result[1] = new DependencyListEntry(GetGCStaticEETypeNode(factory), "ThreadStatic EEType");
            return result;
        }

        public override bool StaticDependenciesAreComputed => true;

        public override void EncodeData(ref ObjectDataBuilder builder, NodeFactory factory, bool relocsOnly)
        {
            builder.RequirePointerAlignment();

            // At runtime, an instance of the GCStaticEEType will be created and a GCHandle to it
            // will be written in this location.
            builder.EmitPointerReloc(GetGCStaticEETypeNode(factory));
        }
    }
}
