// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Internal.TypeSystem;
using ILCompiler.DependencyAnalysisFramework;

namespace ILCompiler.DependencyAnalysis
{
    class ShadowConcreteMethodNode : DependencyNodeCore<NodeFactory>, IMethodNode
    {
        public DependencyNodeCore<NodeFactory> CanonicalMethodNode { get; }

        public MethodDesc Method { get; }

        public int Offset => ((ISymbolNode)CanonicalMethodNode).Offset;

        public string MangledName => ((ISymbolNode)CanonicalMethodNode).MangledName;

        public override bool StaticDependenciesAreComputed
            => CanonicalMethodNode.StaticDependenciesAreComputed;

        public ShadowConcreteMethodNode(MethodDesc method, IMethodNode canonicalMethod)
        {
            Debug.Assert(!method.IsCanonicalMethod(CanonicalFormKind.Any));
            // TODO: assert the method is not runtime determined
            Method = method;
            CanonicalMethodNode = (DependencyNodeCore<NodeFactory>)canonicalMethod;
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            yield return new DependencyListEntry(CanonicalMethodNode, "Canonical body");

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
            return $"{Method.ToString()} backed by {((ISymbolNode)CanonicalMethodNode).MangledName}";
        }

        public override bool HasConditionalStaticDependencies => false;
        public override bool HasDynamicDependencies => false;
        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(
            List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory) => null;
        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(
            NodeFactory factory) => null;
    }



    class RuntimeDeterminedMethodNode : DependencyNodeCore<NodeFactory>, IMethodNode, INodeWithRuntimeDeterminedDependencies
    {
        public DependencyNodeCore<NodeFactory> CanonicalMethodNode { get; }

        public MethodDesc Method { get; }

        public int Offset => ((ISymbolNode)CanonicalMethodNode).Offset;

        public string MangledName => ((ISymbolNode)CanonicalMethodNode).MangledName;

        public RuntimeDeterminedMethodNode(MethodDesc method, IMethodNode canonicalMethod)
        {
            Debug.Assert(!method.IsCanonicalMethod(CanonicalFormKind.Any));
            // TODO: assert the method is runtime determined
            Method = method;
            CanonicalMethodNode = (DependencyNodeCore<NodeFactory>)canonicalMethod;
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            yield return new DependencyListEntry(CanonicalMethodNode, "Canonical body");
        }

        protected override string GetName()
        {
            return $"{Method.ToString()} backed by {((ISymbolNode)CanonicalMethodNode).MangledName}";
        }

        public IEnumerable<DependencyListEntry> InstantiateDependencies(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            yield return new DependencyListEntry(
                factory.DependencyOnlyMethod(Method.InstantiateSignature(typeInstantiation, methodInstantiation)), "concrete method");
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
