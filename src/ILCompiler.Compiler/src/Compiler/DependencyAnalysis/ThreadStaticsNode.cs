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
    public class ThreadStaticsNode : EmbeddedObjectNode, ISymbolDefinitionNode
    {
        private MetadataType _type;

        public ThreadStaticsNode(MetadataType type, NodeFactory factory)
        {
            _type = type;
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        protected override void OnMarked(NodeFactory factory)
        {
            factory.ThreadStaticsRegion.AddEmbeddedObject(this);
        }

        public static string GetMangledName(TypeDesc type, NameMangler nameMangler)
        {
            return nameMangler.CompilationUnitPrefix + "__ThreadStaticBase_" + nameMangler.GetMangledTypeName(type);
        }

        int ISymbolNode.Offset => 0;

        int ISymbolDefinitionNode.Offset => OffsetFromBeginningOfArray;
 
        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(GetMangledName(_type, nameMangler));
        }

        private ISymbolNode GetGCStaticEETypeNode(NodeFactory factory)
        {
            GCPointerMap map = GCPointerMap.FromThreadStaticLayout(_type);
            return factory.GCStaticEEType(map);
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            List<DependencyListEntry> result = new List<DependencyListEntry>();

            result.Add(new DependencyListEntry(factory.ThreadStaticsRegion, "ThreadStatics Region"));

            if (factory.Target.Abi == TargetAbi.CoreRT)
            {
                result.Add(new DependencyListEntry(GetGCStaticEETypeNode(factory), "ThreadStatic EEType"));
            }

            if (factory.TypeSystemContext.HasEagerStaticConstructor(_type))
            {
                result.Add(new DependencyListEntry(factory.EagerCctorIndirection(_type.GetStaticConstructor()), "Eager .cctor"));
            }

            return result;
        }

        public override bool StaticDependenciesAreComputed => true;

        public override void EncodeData(ref ObjectDataBuilder builder, NodeFactory factory, bool relocsOnly)
        {
            if (factory.Target.Abi == TargetAbi.CoreRT)
            {
                // At runtime, an instance of the GCStaticEEType will be created and a GCHandle to it
                // will be written in this location.
                builder.RequireInitialPointerAlignment();
                builder.EmitPointerReloc(GetGCStaticEETypeNode(factory));
            }
            else
            {
                builder.RequireInitialAlignment(_type.ThreadStaticFieldAlignment.AsInt);
                builder.EmitZeros(_type.ThreadStaticFieldSize.AsInt);
            }
        }
    }

    public class ThreadStaticsRegionNode : ArrayOfEmbeddedDataNode<EmbeddedObjectNode>
    {
        private TargetAbi _targetAbi;

        public ThreadStaticsRegionNode(string startSymbolMangledName, string endSymbolMangledName, IComparer<EmbeddedObjectNode> nodeSorter, TargetAbi targetAbi)
            : base(startSymbolMangledName, endSymbolMangledName, nodeSorter)
        {
            _targetAbi = targetAbi;
        }

        public override ObjectNodeSection Section
        {
            get
            {
                return _targetAbi == TargetAbi.ProjectN ? ObjectNodeSection.TLSSection : ObjectNodeSection.DataSection;
            }
        }
    }
}
