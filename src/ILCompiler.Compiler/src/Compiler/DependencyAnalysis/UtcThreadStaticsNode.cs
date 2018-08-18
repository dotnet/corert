// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public class UtcThreadStaticsNode : ObjectNode, ISymbolDefinitionNode, ISymbolNodeWithDebugInfo, ISortableSymbolNode
    {
        private MetadataType _type;

        public UtcThreadStaticsNode(MetadataType type)
        {
            _type = type;
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.NodeMangler.ThreadStatics(_type));
        }

        public int Offset => 0;
        public MetadataType Type => _type;

        public IDebugInfo DebugInfo => NullTypeIndexDebugInfo.Instance;

        public static string GetMangledName(TypeDesc type, NameMangler nameMangler)
        {
            return nameMangler.NodeMangler.ThreadStatics(type);
        }

        public virtual ExportForm GetExportForm(NodeFactory factory) => factory.CompilationModuleGroup.GetExportTypeForm(Type);

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            DependencyList dependencyList = new DependencyList();

            if (factory.TypeSystemContext.HasEagerStaticConstructor(_type))
            {
                dependencyList.Add(factory.EagerCctorIndirection(_type.GetStaticConstructor()), "Eager .cctor");
            }

            dependencyList.Add(((UtcNodeFactory)factory).TypeThreadStaticGCDescNode(_type), "GC Desc");
            EETypeNode.AddDependenciesForStaticsNode(factory, _type, ref dependencyList);
            return dependencyList;
        }

        public override bool StaticDependenciesAreComputed => true;

        public override ObjectNodeSection Section => ObjectNodeSection.TLSSection;
        public override bool IsShareable => EETypeNode.IsTypeNodeShareable(_type);

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder builder = new ObjectDataBuilder(factory, relocsOnly);
            builder.RequireInitialPointerAlignment();
            builder.EmitZeros(_type.ThreadGcStaticFieldSize.AsInt);
            builder.AddSymbol(this);
            return builder.ToObjectData();
        }

        public sealed override int ClassCode => -1421136129;

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return comparer.Compare(_type, ((UtcThreadStaticsNode)other)._type);
        }
    }
}
