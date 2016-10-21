// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        string ISymbolNode.MangledName => "__indirection" + _indirectedNode.MangledName;
        int ISymbolNode.Offset => 0;

        public override ObjectNodeSection Section => ObjectNodeSection.DataSection;

        public override bool StaticDependenciesAreComputed => true;

        protected override string GetName() => ((ISymbolNode)this).MangledName;

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
