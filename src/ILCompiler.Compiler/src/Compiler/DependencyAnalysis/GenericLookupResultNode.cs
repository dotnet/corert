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
    /// <summary>
    /// Represents the result of a generic lookup within a canonical method body.
    /// This node is abstract and doesn't get emitted into the object file. The concrete
    /// artifact the generic lookup will result in can only be determined after substituting
    /// runtime determined types with a concrete generic context. Use
    /// <see cref="GetTarget(NodeFactory, Instantiation, Instantiation)"/> to obtain the concrete
    /// node the result points to.
    /// </summary>
    public abstract class GenericLookupResultNode : DependencyNodeCore<NodeFactory>
    {
        public abstract ISymbolNode GetTarget(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation);
        public abstract string GetMangledName(NameMangler nameMangler);

        public sealed override bool HasConditionalStaticDependencies => false;
        public sealed override bool HasDynamicDependencies => false;
        public sealed override bool InterestingForDynamicDependencyAnalysis => false;
        public sealed override bool StaticDependenciesAreComputed => true;
        public sealed override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory) => null;
        public sealed override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(
            List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory) => null;
        public sealed override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(
            NodeFactory factory) => null;
    }

    /// <summary>
    /// Generic lookup result that points to an EEType.
    /// </summary>
    internal sealed class TypeHandleDictionaryEntry : GenericLookupResultNode
    {
        private TypeDesc _type;

        public TypeHandleDictionaryEntry(TypeDesc type)
        {
            Debug.Assert(type.IsRuntimeDeterminedSubtype, "Concrete type in a generic dictionary?");
            _type = type;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            TypeDesc instantiatedType = _type.InstantiateSignature(typeInstantiation, methodInstantiation);
            return factory.NecessaryTypeSymbol(instantiatedType);
        }

        public override string GetMangledName(NameMangler nameMangler)
        {
            return $"TypeHandle_{nameMangler.GetMangledTypeName(_type)}";
        }

        protected override string GetName() => $"TypeHandle: {_type}";
    }
}
