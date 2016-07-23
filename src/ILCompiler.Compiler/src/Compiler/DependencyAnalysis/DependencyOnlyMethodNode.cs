// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

using Internal.TypeSystem;
using ILCompiler.DependencyAnalysisFramework;
using System.Collections.Generic;

namespace ILCompiler.DependencyAnalysis
{
    public class DependencyOnlyMethodNode : DependencyNodeCore<NodeFactory>
    {
        private MethodDesc _methodRepresented;
        private DependencyNodeCore<NodeFactory> _canonicalNode;

        public DependencyOnlyMethodNode(MethodDesc methodRepresented, DependencyNodeCore<NodeFactory> canonicalNode)
        {
            // TODO: assert methodRepresented is not runtime determined
            Debug.Assert(!methodRepresented.IsCanonicalMethod(CanonicalFormKind.Any));
            _methodRepresented = methodRepresented;
            _canonicalNode = canonicalNode;
        }

        public MethodDesc MethodRepresented => _methodRepresented;

        public override bool HasConditionalStaticDependencies => false;
        public override bool HasDynamicDependencies => false;
        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(
            NodeFactory factory) => null;
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(
            List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory) => null;

        public override bool StaticDependenciesAreComputed => _canonicalNode.StaticDependenciesAreComputed;

        public override string GetName() => _methodRepresented.ToString();

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            Instantiation typeInstantiation = _methodRepresented.OwningType.Instantiation;
            Instantiation methodInstantiation = _methodRepresented.Instantiation;

            foreach (var canonicalMethodDependency in _canonicalNode.GetStaticDependencies(factory))
            {
                DependencyNodeCore<NodeFactory> canonicalDependencyNode = canonicalMethodDependency.Node;
                if (canonicalDependencyNode is ReadyToRunGenericLookupHelperNode)
                {
                    var canonicalGenericLookupHelperNode = canonicalDependencyNode as ReadyToRunGenericLookupHelperNode;
                    ISymbolNode concreteTarget = factory.GetGenericFixupTarget(
                        canonicalGenericLookupHelperNode.Target.FixupKind, canonicalGenericLookupHelperNode.Target.Target,
                        typeInstantiation, methodInstantiation);
                    yield return new DependencyListEntry(concreteTarget, canonicalMethodDependency.Reason);
                }
            }
        }
    }
}
