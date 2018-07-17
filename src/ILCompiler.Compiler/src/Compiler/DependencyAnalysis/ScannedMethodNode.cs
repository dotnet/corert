// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using ILCompiler.DependencyAnalysisFramework;

using Internal.Text;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a method that should be scanned by an IL scanner and its dependencies
    /// analyzed.
    /// </summary>
    public class ScannedMethodNode : DependencyNodeCore<NodeFactory>, IMethodBodyNode
    {
        private readonly MethodDesc _method;
        private DependencyList _dependencies;

        public ScannedMethodNode(MethodDesc method)
        {
            Debug.Assert(!method.IsAbstract);
            Debug.Assert(method.GetCanonMethodTarget(CanonicalFormKind.Specific) == method);
            _method = method;
        }

        public MethodDesc Method => _method;

        public int Offset => 0;

        public bool RepresentsIndirectionCell => false;

        public override bool StaticDependenciesAreComputed => _dependencies != null;

        public void InitializeDependencies(NodeFactory factory, IEnumerable<DependencyListEntry> dependencies)
        {
            _dependencies = new DependencyList(dependencies);
            CodeBasedDependencyAlgorithm.AddDependenciesDueToMethodCodePresence(ref _dependencies, factory, _method);
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.GetMangledMethodName(_method));
        }
        
        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            Debug.Assert(_dependencies != null);
            return _dependencies;
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory) => null;
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory) => null;
        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override bool HasDynamicDependencies => false;
        public override bool HasConditionalStaticDependencies => false;

        int ISortableNode.ClassCode => -1381809560;

        int ISortableNode.CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return comparer.Compare(Method, ((ScannedMethodNode)other).Method);
        }
    }
}
