// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using ILCompiler.DependencyAnalysisFramework;

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

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

        public MethodDesc Method => _decl;

        public VirtualMethodUseNode(MethodDesc decl)
        {
            // Generic virtual methods are tracked by an orthogonal mechanism.
            Debug.Assert(!decl.HasInstantiation);
            _decl = decl;
        }

        protected override string GetName() => $"VirtualMethodUse {_decl.ToString()}";

        protected override void OnMarked(NodeFactory factory)
        {
            // If the VTable slice is getting built on demand, the fact that the virtual method is used means
            // that the slot is used.
            var lazyVTableSlice = factory.VTable(_decl.OwningType) as LazilyBuiltVTableSliceNode;
            if (lazyVTableSlice != null && !_decl.HasInstantiation)
                lazyVTableSlice.AddEntry(factory, _decl);
        }

        public override bool HasConditionalStaticDependencies => false;
        public override bool HasDynamicDependencies => false;
        public override bool InterestingForDynamicDependencyAnalysis => false;

        public override bool StaticDependenciesAreComputed => true;

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            List<DependencyListEntry> dependencies = new List<DependencyListEntry>();
            
            if (factory.MetadataManager.IsReflectionInvokable(_decl) && _decl.IsAbstract)
            {
                if (factory.MetadataManager.HasReflectionInvokeStubForInvokableMethod(_decl) && !_decl.IsCanonicalMethod(CanonicalFormKind.Any))
                {
                    MethodDesc invokeStub = factory.MetadataManager.GetReflectionInvokeStub(_decl);
                    MethodDesc canonInvokeStub = invokeStub.GetCanonMethodTarget(CanonicalFormKind.Specific);
                    if (invokeStub != canonInvokeStub)
                        dependencies.Add(new DependencyListEntry(factory.FatFunctionPointer(invokeStub), "Reflection invoke"));
                    else
                        dependencies.Add(new DependencyListEntry(factory.MethodEntrypoint(invokeStub), "Reflection invoke"));
                }
            }

            if (!_decl.IsSharedByGenericInstantiations)
                dependencies.Add(new DependencyListEntry(factory.ReadyToRunHelper(ReadyToRunHelperId.VirtualCall, _decl), "Reflection invoke"));

            MethodDesc canonDecl = _decl.GetCanonMethodTarget(CanonicalFormKind.Specific);
            if (canonDecl != _decl)
                dependencies.Add(new DependencyListEntry(factory.VirtualMethodUse(canonDecl), "Canonical method"));

            return dependencies;
        }

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory) => null;
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory) => null;
    }
}
