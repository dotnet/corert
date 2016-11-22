// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Text;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a general-purpose pointer indirection to another symbol.
    /// </summary>
    public class IndirectionNode : ObjectNode, ISymbolNode
    {
        private ISymbolNode _indirectedNode;

        public IndirectionNode(ISymbolNode indirectedNode)
        {
            _indirectedNode = indirectedNode;
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("__indirection");
            _indirectedNode.AppendMangledName(nameMangler, sb);
        }
        public int Offset => 0;

        public override ObjectNodeSection Section => ObjectNodeSection.DataSection;
        public override bool IsShareable => false;

        public override bool StaticDependenciesAreComputed => true;

        protected override string GetName() => this.GetMangledName();

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            var builder = new ObjectDataBuilder(factory);
            builder.RequirePointerAlignment();
            builder.DefinedSymbols.Add(this);

            builder.EmitPointerReloc(_indirectedNode);

            return builder.ToObjectData();
        }
    }
}
