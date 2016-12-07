// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using ILCompiler.DependencyAnalysisFramework;

using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    // This node represents the concept of a virtual method being used.
    // It has no direct depedencies, but may be referred to by conditional static 
    // dependencies, or static dependencies from elsewhere.
    //
    // It is used to keep track of uses of virtual methods to ensure that the
    // vtables are properly constructed
    internal class VirtualMethodUseNode : DependencyNodeCore<NodeFactory>
    {
        private MethodDesc _decl;

        public VirtualMethodUseNode(MethodDesc decl)
        {
            _decl = decl;
        }

        protected override string GetName() => $"VirtualMethodUse {_decl.ToString()}";

        protected override void OnMarked(NodeFactory factory)
        {
            // If the VTable slice is getting built on demand, the fact that the virtual method is used means
            // that the slot is used.
            var lazyVTableSlice = factory.VTable(_decl.OwningType) as LazilyBuiltVTableSliceNode;
            if (lazyVTableSlice != null)
                lazyVTableSlice.AddEntry(factory, _decl);
        }

        public override bool HasConditionalStaticDependencies => false;
        public override bool HasDynamicDependencies => false;
        public override bool InterestingForDynamicDependencyAnalysis => false;

        public override bool StaticDependenciesAreComputed => true;

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            MethodDesc canonDecl = _decl.GetCanonMethodTarget(CanonicalFormKind.Specific);
            if (canonDecl != _decl)
                return new[] { new DependencyListEntry(factory.VirtualMethodUse(canonDecl), "Canonical method") };

            return null;
        }

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory) => null;
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory) => null;
    }
}
