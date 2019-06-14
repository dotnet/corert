// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;
using Internal.Text;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class SectionStartNode : DependencyNodeCore<NodeFactory>, ISymbolDefinitionNode
    {
        public readonly string SectionName;

        public SectionStartNode(string sectionName)
        {
            SectionName = sectionName;
        }

        public int Offset => 0;

        public bool RepresentsIndirectionCell => false;

        public override bool InterestingForDynamicDependencyAnalysis => false;

        public override bool HasDynamicDependencies => false;

        public override bool HasConditionalStaticDependencies => false;

        public override bool StaticDependenciesAreComputed => true;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("SectionStartSymbol->");
            sb.Append(SectionName);
        }

        protected override string GetName(NodeFactory factory)
        {
            Utf8StringBuilder sb = new Utf8StringBuilder();
            AppendMangledName(factory.NameMangler, sb);
            return sb.ToString();
        }

        public override IEnumerable<DependencyNodeCore<NodeFactory>.CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory context) => null;

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context) => null;

        public override IEnumerable<DependencyNodeCore<NodeFactory>.CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory context) => null;

    }
}
