// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using ILCompiler.DependencyAnalysisFramework;

using Internal.Text;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents references to canonical method bodies with the capability to make
    /// the canonical reference concrete when given an instantiation context.
    /// This node is used to represent references from canonical method bodies to other
    /// canonical methods.
    /// </summary>
    internal class RuntimeDeterminedMethodNode : DependencyNodeCore<NodeFactory>, IMethodNode, INodeWithRuntimeDeterminedDependencies
    {
        private readonly IMethodNode _canonicalMethodNode;

        public MethodDesc Method { get; }

        // Implementation of ISymbolNode that makes this node act as a symbol for the canonical body
        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            _canonicalMethodNode.AppendMangledName(nameMangler, sb);
        }
        public int Offset => _canonicalMethodNode.Offset;
        public bool RepresentsIndirectionCell => _canonicalMethodNode.RepresentsIndirectionCell;

        public RuntimeDeterminedMethodNode(MethodDesc method, IMethodNode canonicalMethod)
        {
            Debug.Assert(!method.IsSharedByGenericInstantiations);
            Debug.Assert(method.IsRuntimeDeterminedExactMethod);
            Method = method;
            _canonicalMethodNode = canonicalMethod;
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            yield return new DependencyListEntry(_canonicalMethodNode, "Canonical body");
        }

        protected override string GetName(NodeFactory factory) => $"{Method.ToString()} backed by {_canonicalMethodNode.GetMangledName(factory.NameMangler)}";

        public IEnumerable<DependencyListEntry> InstantiateDependencies(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            yield return new DependencyListEntry(
                factory.ShadowConcreteMethod(Method.GetNonRuntimeDeterminedMethodFromRuntimeDeterminedMethodViaSubstitution(typeInstantiation, methodInstantiation)), "concrete method");
        }

        public override bool HasConditionalStaticDependencies => false;
        public override bool HasDynamicDependencies => false;
        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override bool StaticDependenciesAreComputed => true;

        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory) => null;
        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory) => null;

        int ISortableNode.ClassCode => 258139501;

        int ISortableNode.CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return comparer.Compare(_canonicalMethodNode, ((RuntimeDeterminedMethodNode)other)._canonicalMethodNode);
        }
    }
}
