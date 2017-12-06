// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;

using ILCompiler.DependencyAnalysisFramework;

using Internal.Text;

namespace ILCompiler.DependencyAnalysis
{
    internal class SymbolWithOffsetNode : DependencyNodeCore<NodeFactory>, ISymbolNode
    {
        private ISymbolNode _target;
        private int _offset;

        public SymbolWithOffsetNode(ISymbolNode target, int offset)
        {
            _target = target;
            _offset = offset;
        }

        public int Offset => _offset;

        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override bool HasDynamicDependencies => false;
        public override bool HasConditionalStaticDependencies => false;
        public override bool StaticDependenciesAreComputed => true;
        public bool RepresentsIndirectionCell => false;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            _target.AppendMangledName(nameMangler, sb);
        }

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory context)
        {
            return null;
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            return new DependencyListEntry[]
            {
                new DependencyListEntry(_target, "Target"),
            };
        }

        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory context)
        {
            return null;
        }

        protected override string GetName(NodeFactory context)
        {
            return "__offs_" + _offset.ToStringInvariant() + "_from_" + _target.GetMangledName(context.NameMangler);
        }
    }
}
