// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using ILCompiler.DependencyAnalysisFramework;
using Internal.Text;

namespace ILCompiler.DependencyAnalysis
{
    internal class WebAssemblyBlockRefNode : DependencyNodeCore<NodeFactory>, ISymbolNode
    {
        readonly string mangledName;

        public WebAssemblyBlockRefNode(string mangledName)
        {
            this.mangledName = mangledName;
        }

        public override bool HasConditionalStaticDependencies => false;
        public override bool HasDynamicDependencies => false;
        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override bool StaticDependenciesAreComputed => true;
        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            return Enumerable.Empty<DependencyListEntry>();
        }

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory context)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory context)
        {
            throw new NotImplementedException();
        }

        protected override string GetName(NodeFactory context)
        {
            throw new NotImplementedException();
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(mangledName);
        }

        public int Offset { get; }
        public bool RepresentsIndirectionCell { get; }
    }
}
