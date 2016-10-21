// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents the result of a generic lookup within a canonical method body.
    /// The concrete artifact the generic lookup will result in can only be determined after substituting
    /// runtime determined types with a concrete generic context. Use
    /// <see cref="GetTarget(NodeFactory, Instantiation, Instantiation)"/> to obtain the concrete
    /// node the result points to.
    /// </summary>
    public abstract class GenericLookupResult
    {
        public virtual int NumberOfIndirections => 0;

        public abstract ISymbolNode GetTarget(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation);
        public abstract string GetMangledName(NameMangler nameMangler);
        public abstract override string ToString();
    }

    /// <summary>
    /// Generic lookup result that points to an EEType.
    /// </summary>
    internal sealed class TypeHandleGenericLookupResult : GenericLookupResult
    {
        private TypeDesc _type;

        public TypeHandleGenericLookupResult(TypeDesc type)
        {
            Debug.Assert(type.IsRuntimeDeterminedSubtype, "Concrete type in a generic dictionary?");
            _type = type;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            // We are getting a constructed type symbol because this might be something passed to newobj.
            TypeDesc instantiatedType = _type.InstantiateSignature(typeInstantiation, methodInstantiation);
            return factory.ConstructedTypeSymbol(instantiatedType);
        }

        public override string GetMangledName(NameMangler nameMangler)
        {
            return $"TypeHandle_{nameMangler.GetMangledTypeName(_type)}";
        }

        public override string ToString() => $"TypeHandle: {_type}";
    }

    /// <summary>
    /// Generic lookup result that points to a method dictionary.
    /// </summary>
    internal sealed class MethodDictionaryGenericLookupResult : GenericLookupResult
    {
        private MethodDesc _method;

        public MethodDictionaryGenericLookupResult(MethodDesc method)
        {
            Debug.Assert(method.IsRuntimeDeterminedExactMethod, "Concrete method in a generic dictionary?");
            _method = method;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            MethodDesc instantiatedMethod = _method.InstantiateSignature(typeInstantiation, methodInstantiation);
            return factory.MethodGenericDictionary(instantiatedMethod);
        }

        public override string GetMangledName(NameMangler nameMangler)
        {
            return $"MethodHandle_{nameMangler.GetMangledMethodName(_method)}";
        }

        public override string ToString() => $"MethodHandle: {_method}";
    }

    /// <summary>
    /// Generic lookup result that points to a virtual dispatch stub.
    /// </summary>
    internal sealed class VirtualDispatchGenericLookupResult : GenericLookupResult
    {
        private MethodDesc _method;

        public VirtualDispatchGenericLookupResult(MethodDesc method)
        {
            Debug.Assert(method.IsRuntimeDeterminedExactMethod);
            _method = method;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            MethodDesc instantiatedMethod = _method.InstantiateSignature(typeInstantiation, methodInstantiation);
            return factory.ReadyToRunHelper(ReadyToRunHelperId.VirtualCall, instantiatedMethod);
        }

        public override string GetMangledName(NameMangler nameMangler)
        {
            return $"VirtualCall_{nameMangler.GetMangledMethodName(_method)}";
        }

        public override string ToString() => $"VirtualCall: {_method}";
    }

    /// <summary>
    /// Generic lookup result that points to the non-GC static base of a type.
    /// </summary>
    internal sealed class TypeNonGCStaticBaseGenericLookupResult : GenericLookupResult
    {
        private MetadataType _type;

        public TypeNonGCStaticBaseGenericLookupResult(TypeDesc type)
        {
            Debug.Assert(type.IsRuntimeDeterminedSubtype, "Concrete static base in a generic dictionary?");
            Debug.Assert(type is MetadataType);
            _type = (MetadataType)type;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            var instantiatedType = (MetadataType)_type.InstantiateSignature(typeInstantiation, methodInstantiation);
            return factory.TypeNonGCStaticsSymbol(instantiatedType);
        }

        public override string GetMangledName(NameMangler nameMangler)
        {
            return $"NonGCStaticBase_{nameMangler.GetMangledTypeName(_type)}";
        }

        public override string ToString() => $"NonGCStaticBase: {_type}";
    }

    /// <summary>
    /// Generic lookup result that points to the GC static base of a type.
    /// </summary>
    internal sealed class TypeGCStaticBaseGenericLookupResult : GenericLookupResult
    {
        private MetadataType _type;

        // We need to indirect twice:
        // 1. The dictionary points to a cell that is fixed up to point to the handle table.
        // 2. From the handle table we get to the GC heap allocated object.
        public override int NumberOfIndirections => 2;

        public TypeGCStaticBaseGenericLookupResult(TypeDesc type)
        {
            Debug.Assert(type.IsRuntimeDeterminedSubtype, "Concrete static base in a generic dictionary?");
            Debug.Assert(type is MetadataType);
            _type = (MetadataType)type;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            var instantiatedType = (MetadataType)_type.InstantiateSignature(typeInstantiation, methodInstantiation);
            return factory.TypeGCStaticsSymbol(instantiatedType);
        }

        public override string GetMangledName(NameMangler nameMangler)
        {
            return $"GCStaticBase_{nameMangler.GetMangledTypeName(_type)}";
        }

        public override string ToString() => $"GCStaticBase: {_type}";
    }
}
