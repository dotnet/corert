// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

using Internal.Runtime.TypeLoader;
using Internal.Text;

using ILCompiler;
using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;

namespace Internal.Runtime.JitSupport
{
    public abstract class ExternObjectSymbolNode : DependencyNodeCore<NodeFactory>, ISymbolNode
    {
        public ExternObjectSymbolNode()
        {
        }

        protected override string GetName(NodeFactory factory) { throw new PlatformNotSupportedException(); }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb) { throw new PlatformNotSupportedException(); }
        public int Offset => 0;

        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override bool HasDynamicDependencies => false;
        public override bool HasConditionalStaticDependencies => false;
        public override bool StaticDependenciesAreComputed => true;

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory) => null;
        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory) => null;
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory) => null;

        public bool RepresentsIndirectionCell => true;

        /// <summary>
        /// Return a "GenericDictionaryCell" which can be used to get a pointer sized value 
        /// points to or is what this node is used with.
        /// </summary>
        public abstract GenericDictionaryCell GetDictionaryCell();
    }
}
