// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using ILCompiler.DependencyAnalysisFramework;

using Internal.Text;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a symbol that is defined externally and statically linked to the output obj file.
    /// </summary>
    public class ExternSymbolNode : DependencyNodeCore<NodeFactory>, ISortableSymbolNode
    {
        private Utf8String _name;

        public ExternSymbolNode(Utf8String name)
        {
            _name = name;
        }

        protected override string GetName(NodeFactory factory) => $"ExternSymbol {_name.ToString()}";

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(_name);
        }
        public int Offset => 0;
        public virtual bool RepresentsIndirectionCell => false;

        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override bool HasDynamicDependencies => false;
        public override bool HasConditionalStaticDependencies => false;
        public override bool StaticDependenciesAreComputed => true;

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory) => null;
        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory) => null;
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory) => null;

#if !SUPPORT_JIT
        int ISortableSymbolNode.ClassCode => 1092559304;

        int ISortableSymbolNode.CompareToImpl(ISortableSymbolNode other, CompilerComparer comparer)
        {
            return _name.CompareTo(((ExternSymbolNode)other)._name);
        }
#endif
    }
}
