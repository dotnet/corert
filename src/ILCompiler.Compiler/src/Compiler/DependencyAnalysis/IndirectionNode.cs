// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a general-purpose pointer indirection to another symbol.
    /// </summary>
    public class IndirectionNode : ObjectNode, ISymbolDefinitionNode
    {
        private ISortableSymbolNode _indirectedNode;
        private int _offsetDelta;
        private TargetDetails _target;

        public IndirectionNode(TargetDetails target, ISortableSymbolNode indirectedNode, int offsetDelta)
        {
            _indirectedNode = indirectedNode;
            _offsetDelta = offsetDelta;
            _target = target;
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("__indirection");
            _indirectedNode.AppendMangledName(nameMangler, sb);
            sb.Append("_" + _offsetDelta);
        }
        public int Offset => 0;

        public override ObjectNodeSection Section
        {
            get
            {
                if (_target.IsWindows)
                    return ObjectNodeSection.ReadOnlyDataSection;
                else
                    return ObjectNodeSection.DataSection;
            }
        }

        public override bool IsShareable => true;

        public override bool StaticDependenciesAreComputed => true;

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            var builder = new ObjectDataBuilder(factory, relocsOnly);
            builder.RequireInitialPointerAlignment();
            builder.AddSymbol(this);

            builder.EmitPointerReloc(_indirectedNode, _offsetDelta);

            return builder.ToObjectData();
        }

        public override int ClassCode => -1401349230;

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return comparer.Compare(_indirectedNode, ((IndirectionNode)other)._indirectedNode);
        }
    }
}
