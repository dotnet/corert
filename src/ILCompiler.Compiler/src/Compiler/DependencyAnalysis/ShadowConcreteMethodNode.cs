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
    /// Represents a concrete method on a generic type (or a generic method) that doesn't
    /// have code emitted in the executable because it's physically backed by a canonical
    /// method body. The purpose of this node is to track the dependencies of the concrete
    /// method body, as if it was generated. The node acts as a symbol for the canonical
    /// method for convenience.
    /// </summary>
    internal class ShadowConcreteMethodNode<T> : DependencyNodeCore<NodeFactory>, IMethodNode
        where T : DependencyNodeCore<NodeFactory>, IMethodNode
    {
        /// <summary>
        /// Gets the canonical method body that defines the dependencies of this node.
        /// </summary>
        public T CanonicalMethodNode { get; }

        /// <summary>
        /// Gets the concrete method represented by this node.
        /// </summary>
        public MethodDesc Method { get; }

        // Implementation of ISymbolNode that makes this node act as a symbol for the canonical body
        int ISymbolNode.Offset => CanonicalMethodNode.Offset;
        string ISymbolNode.MangledName => CanonicalMethodNode.MangledName;

        public override bool StaticDependenciesAreComputed
            => CanonicalMethodNode.StaticDependenciesAreComputed;

        public ShadowConcreteMethodNode(MethodDesc method, T canonicalMethod)
        {
            Debug.Assert(!method.IsCanonicalMethod(CanonicalFormKind.Any));
            // TODO: assert the method is not runtime determined
            Debug.Assert(canonicalMethod.Method.IsCanonicalMethod(CanonicalFormKind.Any));
            Debug.Assert(canonicalMethod.Method == method.GetCanonMethodTarget(CanonicalFormKind.Specific));
            Method = method;
            CanonicalMethodNode = canonicalMethod;
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            // Make sure the canonical body gets generated
            yield return new DependencyListEntry(CanonicalMethodNode, "Canonical body");

            // Instantiate the runtime determined dependencies of the canonical method body
            // with the concrete instantiation of the method to get concrete dependencies.
            Instantiation typeInst = Method.OwningType.Instantiation;
            Instantiation methodInst = Method.Instantiation;

            foreach (DependencyListEntry canonDep in CanonicalMethodNode.GetStaticDependencies(factory))
            {
                var runtimeDep = canonDep.Node as INodeWithRuntimeDeterminedDependencies;
                if (runtimeDep != null)
                {
                    foreach (var d in runtimeDep.InstantiateDependencies(factory, typeInst, methodInst))
                    {
                        yield return d;
                    }
                }
            }
        }

        protected override string GetName()
        {
            return $"{Method.ToString()} backed by {CanonicalMethodNode.MangledName}";
        }

        public sealed override bool HasConditionalStaticDependencies => false;
        public sealed override bool HasDynamicDependencies => false;
        public sealed override bool InterestingForDynamicDependencyAnalysis => false;
        public sealed override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(
            List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory) => null;
        public sealed override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(
            NodeFactory factory) => null;
    }
}
