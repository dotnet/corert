// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;

using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a generic dictionary for a concrete generic type instantiation
    /// or generic method instantiation. The dictionary is used from canonical code
    /// at runtime to look up runtime artifacts that depend on the concrete
    /// context the generic type or method was instantiated with.
    /// </summary>
    internal abstract class GenericDictionaryNode : ObjectNode, ISymbolNode
    {
        protected const string MangledNamePrefix = "__GenericDict_";

        protected abstract TypeSystemContext Context { get; }

        protected abstract Instantiation TypeInstantiation { get; }

        protected abstract Instantiation MethodInstantiation { get; }

        protected abstract DictionaryLayoutNode GetDictionaryLayout(NodeFactory factory);

        public sealed override ObjectNodeSection Section =>
            Context.Target.IsWindows ? ObjectNodeSection.ReadOnlyDataSection : ObjectNodeSection.DataSection;
        
        public sealed override bool StaticDependenciesAreComputed => true;

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            return new DependencyList
            {
                new DependencyListEntry(GetDictionaryLayout(factory), "Dictionary layout"),
            };
        }

        public abstract int Offset { get; }

        public abstract string MangledName { get; }

        public sealed override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder builder = new ObjectDataBuilder(factory);
            builder.DefinedSymbols.Add(this);
            builder.RequirePointerAlignment();

            // Node representing the generic dictionary doesn't have any dependencies for
            // dependency analysis purposes. The dependencies are tracked as dependencies of the
            // concrete method bodies. When we reach the object data emission phase, the dependencies
            // should all already have been marked.
            if (!relocsOnly)
            {
                EmitDataInternal(ref builder, factory);
            }

            return builder.ToObjectData();
        }

        protected virtual void EmitDataInternal(ref ObjectDataBuilder builder, NodeFactory factory)
        {
            DictionaryLayoutNode layout = GetDictionaryLayout(factory);

            Instantiation typeInst = this.TypeInstantiation;
            Instantiation methodInst = this.MethodInstantiation;

            foreach (var entry in layout.Entries)
            {
                ISymbolNode targetNode = entry.GetTarget(factory, typeInst, methodInst);
                int targetDelta = entry.TargetDelta;
                builder.EmitPointerReloc(targetNode, targetDelta);
            }
        }

        protected sealed override string GetName()
        {
            return MangledName;
        }
    }

    internal sealed class TypeGenericDictionaryNode : GenericDictionaryNode
    {
        private TypeDesc _owningType;

        public override int Offset => 0;
        public override string MangledName => MangledNamePrefix + NodeFactory.NameMangler.GetMangledTypeName(_owningType);

        protected override Instantiation TypeInstantiation => _owningType.Instantiation;
        protected override Instantiation MethodInstantiation => new Instantiation();
        protected override TypeSystemContext Context => _owningType.Context;

        protected override DictionaryLayoutNode GetDictionaryLayout(NodeFactory factory)
        {
            return factory.GenericDictionaryLayout(_owningType.ConvertToCanonForm(CanonicalFormKind.Specific));
        }

        public override bool HasConditionalStaticDependencies => true;

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory)
        {
            // The generic dictionary layout is shared between all the canonically equivalent
            // instantiations. We need to track the dependencies of all canonical method bodies
            // that use the same dictionary layout.
            foreach (var method in _owningType.GetAllMethods())
            {
                // Static and generic methods have their own generic dictionaries
                if (method.Signature.IsStatic || method.HasInstantiation)
                    continue;

                // Abstract methods don't have a body
                if (method.IsAbstract)
                    continue;

                // If a canonical method body was compiled, we need to track the dictionary
                // dependencies in the context of the concrete type that owns this dictionary.
                yield return new CombinedDependencyListEntry(
                    factory.ShadowConcreteMethod(method),
                    factory.MethodEntrypoint(method.GetCanonMethodTarget(CanonicalFormKind.Specific)),
                    "Generic dictionary dependency");
            }
        }

        public TypeGenericDictionaryNode(TypeDesc owningType)
        {
            Debug.Assert(!owningType.IsCanonicalSubtype(CanonicalFormKind.Any));
            Debug.Assert(!owningType.IsRuntimeDeterminedSubtype);
            Debug.Assert(owningType.HasInstantiation);

            _owningType = owningType;
        }
    }

    internal sealed class MethodGenericDictionaryNode : GenericDictionaryNode
    {
        private MethodDesc _owningMethod;

        public override int Offset => _owningMethod.Context.Target.PointerSize;
        public override string MangledName => MangledNamePrefix + NodeFactory.NameMangler.GetMangledMethodName(_owningMethod);

        protected override Instantiation TypeInstantiation => _owningMethod.OwningType.Instantiation;
        protected override Instantiation MethodInstantiation => _owningMethod.Instantiation;
        protected override TypeSystemContext Context => _owningMethod.Context;

        protected override DictionaryLayoutNode GetDictionaryLayout(NodeFactory factory)
        {
            return factory.GenericDictionaryLayout(_owningMethod.GetCanonMethodTarget(CanonicalFormKind.Specific));
        }

        protected override void EmitDataInternal(ref ObjectDataBuilder builder, NodeFactory factory)
        {
            // Method generic dictionaries get prefixed by the hash code of the owning method
            // to allow quick lookups of additional details by the type loader.

            if (builder.TargetPointerSize == 8)
                builder.EmitInt(0);
            builder.EmitInt(_owningMethod.GetHashCode());

            Debug.Assert(builder.CountBytes == Offset);

            base.EmitDataInternal(ref builder, factory);
        }

        public MethodGenericDictionaryNode(MethodDesc owningMethod)
        {
            Debug.Assert(!owningMethod.IsSharedByGenericInstantiations);
            Debug.Assert(owningMethod.HasInstantiation);

            _owningMethod = owningMethod;
        }
    }
}
