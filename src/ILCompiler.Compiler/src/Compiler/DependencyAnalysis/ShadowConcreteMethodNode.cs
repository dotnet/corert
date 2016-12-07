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
        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            CanonicalMethodNode.AppendMangledName(nameMangler, sb);
        }
        public int Offset => CanonicalMethodNode.Offset;

        public override bool StaticDependenciesAreComputed
            => CanonicalMethodNode.StaticDependenciesAreComputed;

        public ShadowConcreteMethodNode(MethodDesc method, T canonicalMethod)
        {
            Debug.Assert(!method.IsSharedByGenericInstantiations);
            Debug.Assert(!method.IsRuntimeDeterminedExactMethod);
            Debug.Assert(canonicalMethod.Method.IsSharedByGenericInstantiations);
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

            // Reflection invoke stub handling is here because in the current reflection model we reflection-enable
            // all methods that are compiled. Ideally the list of reflection enabled methods should be known before
            // we even start the compilation process (with the invocation stubs being compilation roots like any other).
            // The existing model has it's problems: e.g. the invocability of the method depends on inliner decisions.
            if (factory.MetadataManager.HasReflectionInvokeStub(Method))
            {
                MethodDesc invokeStub = factory.MetadataManager.GetReflectionInvokeStub(Method);
                MethodDesc canonInvokeStub = invokeStub.GetCanonMethodTarget(CanonicalFormKind.Specific);
                if (invokeStub != canonInvokeStub)
                    yield return new DependencyListEntry(factory.FatFunctionPointer(invokeStub), "Reflection invoke");
                else
                    yield return new DependencyListEntry(factory.MethodEntrypoint(invokeStub), "Reflection invoke");
            }
        }

        protected override string GetName() => $"{Method.ToString()} backed by {CanonicalMethodNode.GetMangledName()}";

        public sealed override bool HasConditionalStaticDependencies => false;
        public sealed override bool HasDynamicDependencies => false;
        public sealed override bool InterestingForDynamicDependencyAnalysis => false;

        public sealed override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory) => null;
        public sealed override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory) => null;
    }
}
