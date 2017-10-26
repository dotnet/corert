﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;

using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a generic dictionary for a concrete generic type instantiation
    /// or generic method instantiation. The dictionary is used from canonical code
    /// at runtime to look up runtime artifacts that depend on the concrete
    /// context the generic type or method was instantiated with.
    /// </summary>
    public abstract class GenericDictionaryNode : ObjectNode, IExportableSymbolNode, ISortableSymbolNode
    {
        private readonly NodeFactory _factory;

        protected abstract TypeSystemContext Context { get; }

        public abstract TypeSystemEntity OwningEntity { get; }

        public abstract Instantiation TypeInstantiation { get; }

        public abstract Instantiation MethodInstantiation { get; }

        public abstract DictionaryLayoutNode GetDictionaryLayout(NodeFactory factory);
        
        public sealed override bool StaticDependenciesAreComputed => true;

        public sealed override bool IsShareable => true;

        int ISymbolNode.Offset => 0;

        public abstract bool IsExported(NodeFactory factory);

        public abstract void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb);

        protected abstract int HeaderSize { get; }

        int ISymbolDefinitionNode.Offset => HeaderSize;

        public override ObjectNodeSection Section => GetDictionaryLayout(_factory).DictionarySection(_factory);

        public GenericDictionaryNode(NodeFactory factory)
        {
            _factory = factory;
        }

        public sealed override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder builder = new ObjectDataBuilder(factory, relocsOnly);
            builder.AddSymbol(this);
            builder.RequireInitialPointerAlignment();

            DictionaryLayoutNode layout = GetDictionaryLayout(factory);

            // Node representing the generic dictionary layout might be one of two kinds:
            // With fixed slots, or where slots are added as we're expanding the graph.
            // If it's the latter, we can't touch the collection of slots before the graph expansion
            // is complete (relocsOnly == false). It's someone else's responsibility
            // to make sure the dependencies are properly generated.
            // If this is a dictionary layout with fixed slots, it's the responsibility of
            // each dictionary to ensure the targets are marked.
            if (layout.HasFixedSlots || !relocsOnly)
            {
                // TODO: pass the layout we already have to EmitDataInternal
                EmitDataInternal(ref builder, factory, relocsOnly);
            }

            return builder.ToObjectData();
        }

        protected virtual void EmitDataInternal(ref ObjectDataBuilder builder, NodeFactory factory, bool fixedLayoutOnly)
        {
            DictionaryLayoutNode layout = GetDictionaryLayout(factory);
            layout.EmitDictionaryData(ref builder, factory, this, fixedLayoutOnly: fixedLayoutOnly);
        }

        protected sealed override string GetName(NodeFactory factory)
        {
            return this.GetMangledName(factory.NameMangler);
        }

        int ISortableSymbolNode.ClassCode => ClassCode;

        int ISortableSymbolNode.CompareToImpl(ISortableSymbolNode other, CompilerComparer comparer)
        {
            return CompareToImpl((ObjectNode)other, comparer);
        }
    }

    public sealed class TypeGenericDictionaryNode : GenericDictionaryNode
    {
        private TypeDesc _owningType;

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.NodeMangler.TypeGenericDictionary(_owningType));
        }

        protected override int HeaderSize => 0;
        public override Instantiation TypeInstantiation => _owningType.Instantiation;
        public override Instantiation MethodInstantiation => new Instantiation();
        protected override TypeSystemContext Context => _owningType.Context;
        public override TypeSystemEntity OwningEntity => _owningType;
        public override bool IsExported(NodeFactory factory) => factory.CompilationModuleGroup.ExportsType(OwningType);
        public TypeDesc OwningType => _owningType;

        public override DictionaryLayoutNode GetDictionaryLayout(NodeFactory factory)
        {
            return factory.GenericDictionaryLayout(_owningType.ConvertToCanonForm(CanonicalFormKind.Specific));
        }

        public override bool HasConditionalStaticDependencies => true;

        private static bool ContributesToDictionaryLayout(MethodDesc method)
        {
            // Generic methods have their own generic dictionaries
            if (method.HasInstantiation)
                return false;

            // Abstract methods don't have a body
            if (method.IsAbstract)
                return false;

            // PInvoke methods, runtime imports, etc. are not permitted on generic types,
            // but let's not crash the compilation because of that.
            if (method.IsPInvoke || method.IsRuntimeImplemented)
                return false;

            return true;
        }

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            DependencyList result = new DependencyList();

            result.Add(GetDictionaryLayout(factory), "Layout");

            if (factory.CompilationModuleGroup.ShouldPromoteToFullType(_owningType))
            {
                // If the compilation group wants this type to be fully promoted, it means the EEType is going to be
                // COMDAT folded with other EETypes generated in a different object file. This means their generic
                // dictionaries need to have identical contents. The only way to achieve that is by generating
                // the entries for all methods that contribute to the dictionary, and sorting the dictionaries.
                foreach (var method in _owningType.GetAllMethods())
                {
                    if (!ContributesToDictionaryLayout(method))
                        continue;

                    result.Add(factory.MethodEntrypoint(method.GetCanonMethodTarget(CanonicalFormKind.Specific)),
                        "Cross-objectfile equivalent dictionary");
                }
            }

            // Lazy generic use of the Activator.CreateInstance<T> heuristic requires tracking type parameters that are used in lazy generics.
            if (factory.LazyGenericsPolicy.UsesLazyGenerics(_owningType))
            {
                foreach (var arg in _owningType.Instantiation)
                {
                    // Skip types that do not have a default constructor (not interesting).
                    if (arg.IsValueType || arg.GetDefaultConstructor() == null)
                        continue;

                    result.Add(new DependencyListEntry(
                        factory.DefaultConstructorFromLazy(arg.ConvertToCanonForm(CanonicalFormKind.Specific)),
                        "Default constructor for lazy generics"));
                }
            }

            return result;
        }

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory)
        {
            // The generic dictionary layout is shared between all the canonically equivalent
            // instantiations. We need to track the dependencies of all canonical method bodies
            // that use the same dictionary layout.
            foreach (var method in _owningType.GetAllMethods())
            {
                if (!ContributesToDictionaryLayout(method))
                    continue;

                // If a canonical method body was compiled, we need to track the dictionary
                // dependencies in the context of the concrete type that owns this dictionary.
                yield return new CombinedDependencyListEntry(
                    factory.ShadowConcreteMethod(method),
                    factory.MethodEntrypoint(method.GetCanonMethodTarget(CanonicalFormKind.Specific)),
                    "Generic dictionary dependency");
            }
        }

        public TypeGenericDictionaryNode(TypeDesc owningType, NodeFactory factory)
            : base(factory)
        {
            Debug.Assert(!owningType.IsCanonicalSubtype(CanonicalFormKind.Any));
            Debug.Assert(!owningType.IsRuntimeDeterminedSubtype);
            Debug.Assert(owningType.HasInstantiation);
            Debug.Assert(owningType.ConvertToCanonForm(CanonicalFormKind.Specific) != owningType);

            _owningType = owningType;
        }

        protected internal override int ClassCode => 889700584;

        protected internal override int CompareToImpl(SortableDependencyNode other, CompilerComparer comparer)
        {
            return comparer.Compare(_owningType, ((TypeGenericDictionaryNode)other)._owningType);
        }
    }

    public sealed class MethodGenericDictionaryNode : GenericDictionaryNode
    {
        private MethodDesc _owningMethod;

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.NodeMangler.MethodGenericDictionary(_owningMethod));
        }
        protected override int HeaderSize => _owningMethod.Context.Target.PointerSize;
        public override Instantiation TypeInstantiation => _owningMethod.OwningType.Instantiation;
        public override Instantiation MethodInstantiation => _owningMethod.Instantiation;
        protected override TypeSystemContext Context => _owningMethod.Context;
        public override TypeSystemEntity OwningEntity => _owningMethod;
        public override bool IsExported(NodeFactory factory) => factory.CompilationModuleGroup.ExportsMethodDictionary(OwningMethod);
        public MethodDesc OwningMethod => _owningMethod;

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            DependencyList dependencies = new DependencyList();

            dependencies.Add(GetDictionaryLayout(factory), "Layout");

            GenericMethodsHashtableNode.GetGenericMethodsHashtableDependenciesForMethod(ref dependencies, factory, _owningMethod);

            factory.InteropStubManager.AddMarshalAPIsGenericDependencies(ref dependencies, factory, _owningMethod);

            // Lazy generic use of the Activator.CreateInstance<T> heuristic requires tracking type parameters that are used in lazy generics.
            if (factory.LazyGenericsPolicy.UsesLazyGenerics(_owningMethod))
            {
                foreach (var arg in _owningMethod.OwningType.Instantiation)
                {
                    // Skip types that do not have a default constructor (not interesting).
                    if (arg.IsValueType || arg.GetDefaultConstructor() == null)
                        continue;

                    dependencies.Add(new DependencyListEntry(
                        factory.DefaultConstructorFromLazy(arg.ConvertToCanonForm(CanonicalFormKind.Specific)),
                        "Default constructor for lazy generics"));
                }
                foreach (var arg in _owningMethod.Instantiation)
                {
                    // Skip types that do not have a default constructor (not interesting).
                    if (arg.IsValueType || arg.GetDefaultConstructor() == null)
                        continue;

                    dependencies.Add(new DependencyListEntry(
                        factory.DefaultConstructorFromLazy(arg.ConvertToCanonForm(CanonicalFormKind.Specific)),
                        "Default constructor for lazy generics"));
                }
            }

            return dependencies;
        }

        public override DictionaryLayoutNode GetDictionaryLayout(NodeFactory factory)
        {
            return factory.GenericDictionaryLayout(_owningMethod.GetCanonMethodTarget(CanonicalFormKind.Specific));
        }

        protected override void EmitDataInternal(ref ObjectDataBuilder builder, NodeFactory factory, bool fixedLayoutOnly)
        {
            // Method generic dictionaries get prefixed by the hash code of the owning method
            // to allow quick lookups of additional details by the type loader.

            builder.EmitInt(_owningMethod.GetHashCode());
            if (builder.TargetPointerSize == 8)
                builder.EmitInt(0);

            Debug.Assert(builder.CountBytes == ((ISymbolDefinitionNode)this).Offset);

            // Lazy method dictionaries are generated by the compiler, but they have no entries within them. (They are used solely to identify the exact method)
            // The dictionary layout may be filled in by various needs for generic lookups, but those are handled in a lazy fashion.
            if (factory.LazyGenericsPolicy.UsesLazyGenerics(OwningMethod))
                return;

            base.EmitDataInternal(ref builder, factory, fixedLayoutOnly);
        }

        public MethodGenericDictionaryNode(MethodDesc owningMethod, NodeFactory factory)
            : base(factory)
        {
            Debug.Assert(!owningMethod.IsSharedByGenericInstantiations);
            Debug.Assert(owningMethod.HasInstantiation);
            Debug.Assert(owningMethod.GetCanonMethodTarget(CanonicalFormKind.Specific) != owningMethod);

            _owningMethod = owningMethod;
        }

        protected internal override int ClassCode => -1245704203;

        protected internal override int CompareToImpl(SortableDependencyNode other, CompilerComparer comparer)
        {
            return comparer.Compare(_owningMethod, ((MethodGenericDictionaryNode)other)._owningMethod);
        }
    }
}
