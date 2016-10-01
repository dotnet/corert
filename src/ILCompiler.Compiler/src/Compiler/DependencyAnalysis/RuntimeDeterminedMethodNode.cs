// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using Internal.TypeSystem;
using ILCompiler.DependencyAnalysisFramework;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents references to canonical method bodies with the capability to make
    /// the canonical reference concrete when given an instantiation context.
    /// This node is used to represent references from canonical method bodies to other
    /// canonical methods.
    /// </summary>
    internal class RuntimeDeterminedMethodNode<T> : DependencyNodeCore<NodeFactory>, IMethodNode, INodeWithRuntimeDeterminedDependencies
        where T : DependencyNodeCore<NodeFactory>, IMethodNode
    {
        private readonly T _canonicalMethodNode;

        public MethodDesc Method { get; }

        // Implementation of ISymbolNode that makes this node act as a symbol for the canonical body
        int ISymbolNode.Offset => _canonicalMethodNode.Offset;
        string ISymbolNode.MangledName => _canonicalMethodNode.MangledName;

        public RuntimeDeterminedMethodNode(MethodDesc method, T canonicalMethod)
        {
            Debug.Assert(!method.IsCanonicalMethod(CanonicalFormKind.Any));
            // TODO: assert the method is runtime determined
            Method = method;
            _canonicalMethodNode = canonicalMethod;
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            yield return new DependencyListEntry(_canonicalMethodNode, "Canonical body");
        }

        protected override string GetName()
        {
            return $"{Method.ToString()} backed by {_canonicalMethodNode.MangledName}";
        }

        public IEnumerable<DependencyListEntry> InstantiateDependencies(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            yield return new DependencyListEntry(
                factory.ShadowConcreteMethod(Method.InstantiateSignature(typeInstantiation, methodInstantiation)), "concrete method");
        }

        public override bool HasConditionalStaticDependencies => false;
        public override bool HasDynamicDependencies => false;
        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override bool StaticDependenciesAreComputed => true;
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(
            List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory) => null;
        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(
            NodeFactory factory) => null;
    }
}
