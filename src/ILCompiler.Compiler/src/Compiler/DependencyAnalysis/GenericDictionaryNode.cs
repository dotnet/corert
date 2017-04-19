// Licensed to the .NET Foundation under one or more agreements.
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
    public abstract class GenericDictionaryNode : ObjectNode, IExportableSymbolNode
    {
        protected const string MangledNamePrefix = "__GenericDict_";

        protected abstract TypeSystemContext Context { get; }

        public abstract Instantiation TypeInstantiation { get; }

        public abstract Instantiation MethodInstantiation { get; }

        public abstract DictionaryLayoutNode GetDictionaryLayout(NodeFactory factory);

        public sealed override ObjectNodeSection Section =>
            Context.Target.IsWindows ? ObjectNodeSection.ReadOnlyDataSection : ObjectNodeSection.DataSection;
        
        public sealed override bool StaticDependenciesAreComputed => true;

        public sealed override bool IsShareable => true;

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            return new DependencyList
            {
                new DependencyListEntry(GetDictionaryLayout(factory), "Dictionary layout"),
            };
        }

        public abstract bool IsExported(NodeFactory factory);

        public abstract void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb);
        public abstract int Offset { get; }

        public sealed override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder builder = new ObjectDataBuilder(factory, relocsOnly);
            builder.AddSymbol(this);
            builder.RequireInitialPointerAlignment();

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
            layout.EmitDictionaryData(ref builder, factory, this);            
        }

        protected sealed override string GetName(NodeFactory factory)
        {
            return this.GetMangledName(factory.NameMangler);
        }
    }

    public sealed class TypeGenericDictionaryNode : GenericDictionaryNode
    {
        private TypeDesc _owningType;

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(MangledNamePrefix).Append(nameMangler.GetMangledTypeName(_owningType));
        }
        public override int Offset => 0;
        public override Instantiation TypeInstantiation => _owningType.Instantiation;
        public override Instantiation MethodInstantiation => new Instantiation();
        protected override TypeSystemContext Context => _owningType.Context;

        public override bool IsExported(NodeFactory factory) => factory.CompilationModuleGroup.ExportsType(OwningType);

        public TypeDesc OwningType => _owningType;

        public static string GetMangledName(NameMangler nameMangler, TypeDesc owningType)
        {
            return MangledNamePrefix + nameMangler.GetMangledTypeName(owningType);
        }

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
            DependencyList result = null;

            if (factory.CompilationModuleGroup.ShouldPromoteToFullType(_owningType))
            {
                result = new DependencyList();

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

        public TypeGenericDictionaryNode(TypeDesc owningType)
        {
            Debug.Assert(!owningType.IsCanonicalSubtype(CanonicalFormKind.Any));
            Debug.Assert(!owningType.IsRuntimeDeterminedSubtype);
            Debug.Assert(owningType.HasInstantiation);

            _owningType = owningType;
        }
    }

    public sealed class MethodGenericDictionaryNode : GenericDictionaryNode
    {
        private MethodDesc _owningMethod;

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(MangledNamePrefix).Append(nameMangler.GetMangledMethodName(_owningMethod));
        }
        public override int Offset => _owningMethod.Context.Target.PointerSize;
        public override Instantiation TypeInstantiation => _owningMethod.OwningType.Instantiation;
        public override Instantiation MethodInstantiation => _owningMethod.Instantiation;
        protected override TypeSystemContext Context => _owningMethod.Context;

        public override bool IsExported(NodeFactory factory) => factory.CompilationModuleGroup.ExportsMethod(OwningMethod);

        public MethodDesc OwningMethod => _owningMethod;

        public static string GetMangledName(NameMangler nameMangler, MethodDesc owningMethod)
        {
            return MangledNamePrefix + nameMangler.GetMangledMethodName(owningMethod);
        }

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            return GenericMethodsHashtableNode.GetGenericMethodsHashtableDependenciesForMethod(factory, _owningMethod);
        }

        public override DictionaryLayoutNode GetDictionaryLayout(NodeFactory factory)
        {
            return factory.GenericDictionaryLayout(_owningMethod.GetCanonMethodTarget(CanonicalFormKind.Specific));
        }

        protected override void EmitDataInternal(ref ObjectDataBuilder builder, NodeFactory factory)
        {
            // Method generic dictionaries get prefixed by the hash code of the owning method
            // to allow quick lookups of additional details by the type loader.

            builder.EmitInt(_owningMethod.GetHashCode());
            if (builder.TargetPointerSize == 8)
                builder.EmitInt(0);

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
