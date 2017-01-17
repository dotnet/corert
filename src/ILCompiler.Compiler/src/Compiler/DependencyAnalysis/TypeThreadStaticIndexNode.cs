// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a node containing information necessary at runtime to locate type's thread static base.
    /// </summary>
    internal class TypeThreadStaticIndexNode : ObjectNode, ISymbolNode
    {
        private MetadataType _type;

        public TypeThreadStaticIndexNode(MetadataType type)
        {
            _type = type;
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("__TypeThreadStaticIndex_")
              .Append(NodeFactory.NameMangler.GetMangledTypeName(_type));
        }
        public int Offset => 0;
        protected override string GetName() => this.GetMangledName();
        public override ObjectNodeSection Section => ObjectNodeSection.ReadOnlyDataSection;
        public override bool IsShareable => true;
        public override bool StaticDependenciesAreComputed => true;

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            return new DependencyList
            {
                new DependencyListEntry(factory.TypeThreadStaticsSymbol(_type), "Thread static storage")
            };
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder objData = new ObjectDataBuilder(factory);

            objData.Alignment = objData.TargetPointerSize;
            objData.DefinedSymbols.Add(this);

            int typeTlsIndex = 0;
            if (!relocsOnly)
            {
                var node = factory.TypeThreadStaticsSymbol(_type);
                typeTlsIndex = factory.ThreadStaticsRegion.IndexOfEmbeddedObject(node);
            }

            objData.EmitPointerReloc(factory.TypeManagerIndirection);
            objData.EmitNaturalInt(typeTlsIndex);

            return objData.ToObjectData();
        }
    }
}
