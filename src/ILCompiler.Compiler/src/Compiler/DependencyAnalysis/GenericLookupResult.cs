// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

using Internal.Text;
using Internal.TypeSystem;

using FatFunctionPointerConstants = Internal.Runtime.FatFunctionPointerConstants;

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
        public abstract ISymbolNode GetTarget(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation);
        public abstract void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb);
        public abstract override string ToString();

        /// <summary>
        /// Gets the offset to be applied to the symbol returned by
        /// <see cref="GetTarget(NodeFactory, Instantiation, Instantiation)"/> to get the actual target.
        /// </summary>
        public virtual int TargetDelta => 0;
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

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("TypeHandle_");
            sb.Append(nameMangler.GetMangledTypeName(_type));
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

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("MethodHandle_");
            sb.Append(nameMangler.GetMangledMethodName(_method));
        }

        public override string ToString() => $"MethodHandle: {_method}";
    }

    /// <summary>
    /// Generic lookup result that is a function pointer.
    /// </summary>
    internal sealed class MethodEntryGenericLookupResult : GenericLookupResult
    {
        private MethodDesc _method;

        public MethodEntryGenericLookupResult(MethodDesc method)
        {
            Debug.Assert(method.IsRuntimeDeterminedExactMethod);
            _method = method;
        }

        public override int TargetDelta => FatFunctionPointerConstants.Offset;

        public override ISymbolNode GetTarget(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            MethodDesc instantiatedMethod = _method.InstantiateSignature(typeInstantiation, methodInstantiation);
            return factory.FatFunctionPointer(instantiatedMethod);
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("MethodEntry_");
            sb.Append(nameMangler.GetMangledMethodName(_method));
        }

        public override string ToString() => $"MethodEntry: {_method}";
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

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("VirtualCall_");
            sb.Append(nameMangler.GetMangledMethodName(_method));
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

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("NonGCStaticBase_");
            sb.Append(nameMangler.GetMangledTypeName(_type));
        }

        public override string ToString() => $"NonGCStaticBase: {_type}";
    }

    /// <summary>
    /// Generic lookup result that points to the GC static base of a type.
    /// </summary>
    internal sealed class TypeGCStaticBaseGenericLookupResult : GenericLookupResult
    {
        private MetadataType _type;

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

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("GCStaticBase_");
            sb.Append(nameMangler.GetMangledTypeName(_type));
        }

        public override string ToString() => $"GCStaticBase: {_type}";
    }
}
