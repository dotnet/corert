﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Internal.IL;
using Internal.Text;
using Internal.TypeSystem;

using ILCompiler.DependencyAnalysisFramework;

namespace ILCompiler.DependencyAnalysis
{
    public enum GenericLookupResultReferenceType
    {
        Direct,             // The slot stores a direct pointer to the target
        Indirect,           // The slot is an indirection cell which points to the direct pointer
        ConditionalIndirect, // The slot may be a direct pointer or an indirection cell, depending on the last digit
    }

    // Represents a generic lookup within a canonical method body.
    // TODO: unify with NativeFormat.FixupSignatureKind
    public enum LookupResultType
    {
        Invalid,
        EEType,             // a type
        UnwrapNullable,     // a type (The type T described by a type spec that is generic over Nullable<T>)
        NonGcStatic,        // the non-gc statics of a type
        GcStatic,           // the gc statics of a type
        Method,             // a method
        InterfaceDispatchCell,  // the dispatch cell for calling an interface method
        MethodDictionary,   // a dictionary for calling a generic method
        UnboxingStub,       // the unboxing stub for a method
        ArrayType,          // an array of type
        DefaultCtor,        // default ctor of a type
        TlsIndex,           // tls index of a type
        TlsOffset,          // tls offset of a type
        AllocObject,        // the allocator of a type
        GvmVtableOffset,    // vtable offset of a generic virtual method
        ProfileCounter,     // profiling counter cell
        MethodLdToken,      // a ldtoken result for a method
        FieldLdToken,       // a ldtoken result for a field
        Field,              // a field descriptor
        IsInst,             // isinst helper
        CastClass,          // castclass helper
        AllocArray,         // the array allocator of a type
        CheckArrayElementType, // check the array element type
        TypeSize,           // size of the type
        FieldOffset,        // field offset
        CallingConvention_NoInstParam,      // CallingConventionConverterThunk NO_INSTANTIATING_PARAM
        CallingConvention_HasInstParam,     // CallingConventionConverterThunk HAS_INSTANTIATING_PARAM
        CallingConvention_MaybeInstParam,   // CallingConventionConverterThunk MAYBE_INSTANTIATING_PARAM
        VtableOffset,       // Offset of a virtual method into the type's vtable
        Constrained,        // ConstrainedCallDesc
        ConstrainedDirect,  // Direct ConstrainedCallDesc
    }

    public interface IGenericLookupResultTocWriter
    {
        void WriteData(GenericLookupResultReferenceType referenceType, LookupResultType slotType, TypeSystemEntity context);
    }

    public struct GenericLookupResultContext
    {
        private readonly TypeSystemEntity _canonicalOwner;

        public readonly Instantiation TypeInstantiation;

        public readonly Instantiation MethodInstantiation;

        public TypeSystemEntity Context
        {
            get
            {
                if (_canonicalOwner is TypeDesc)
                {
                    var owningTypeDefinition = (MetadataType)((TypeDesc)_canonicalOwner).GetTypeDefinition();
                    Debug.Assert(owningTypeDefinition.Instantiation.Length == TypeInstantiation.Length);
                    Debug.Assert(MethodInstantiation.IsNull || MethodInstantiation.Length == 0);

                    return owningTypeDefinition.MakeInstantiatedType(TypeInstantiation);
                }
                
                Debug.Assert(_canonicalOwner is MethodDesc);
                MethodDesc owningMethodDefinition = ((MethodDesc)_canonicalOwner).GetTypicalMethodDefinition();
                Debug.Assert(owningMethodDefinition.Instantiation.Length == MethodInstantiation.Length);

                MethodDesc concreteMethod = owningMethodDefinition;
                if (!TypeInstantiation.IsNull && TypeInstantiation.Length > 0)
                {
                    TypeDesc owningType = owningMethodDefinition.OwningType;
                    Debug.Assert(owningType.Instantiation.Length == TypeInstantiation.Length);
                    concreteMethod = owningType.Context.GetMethodForInstantiatedType(owningMethodDefinition, ((MetadataType)owningType).MakeInstantiatedType(TypeInstantiation));
                }
                else
                {
                    Debug.Assert(owningMethodDefinition.OwningType.Instantiation.IsNull
                        || owningMethodDefinition.OwningType.Instantiation.Length == 0);
                }

                return concreteMethod.MakeInstantiatedMethod(MethodInstantiation);
            }
        }

        public GenericLookupResultContext(TypeSystemEntity canonicalOwner, Instantiation typeInst, Instantiation methodInst)
        {
            _canonicalOwner = canonicalOwner;
            TypeInstantiation = typeInst;
            MethodInstantiation = methodInst;
        }
    }

    /// <summary>
    /// Represents the result of a generic lookup within a canonical method body.
    /// The concrete artifact the generic lookup will result in can only be determined after substituting
    /// runtime determined types with a concrete generic context. Use
    /// <see cref="GetTarget(NodeFactory, Instantiation, Instantiation, GenericDictionaryNode)"/> to obtain the concrete
    /// node the result points to.
    /// </summary>
    public abstract class GenericLookupResult
    {
        protected abstract int ClassCode { get; }
        public abstract ISymbolNode GetTarget(NodeFactory factory, GenericLookupResultContext dictionary);
        public abstract void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb);
        public abstract override string ToString();
        protected abstract int CompareToImpl(GenericLookupResult other, TypeSystemComparer comparer);
        protected abstract bool EqualsImpl(GenericLookupResult obj);
        protected abstract int GetHashCodeImpl();

        public sealed override bool Equals(object obj)
        {
            GenericLookupResult other = obj as GenericLookupResult;
            if (obj == null)
                return false;

            return ClassCode == other.ClassCode && EqualsImpl(other);
        }

        public sealed override int GetHashCode()
        {
            return ClassCode * 31 + GetHashCodeImpl();
        }

        public virtual void EmitDictionaryEntry(ref ObjectDataBuilder builder, NodeFactory factory, GenericLookupResultContext dictionary)
        {
            ISymbolNode target = GetTarget(factory, dictionary);
            if (LookupResultReferenceType(factory) == GenericLookupResultReferenceType.ConditionalIndirect)
            {
                builder.EmitPointerRelocOrIndirectionReference(target);
            }
            else
            {
                builder.EmitPointerReloc(target);
            }
        }

        public virtual GenericLookupResultReferenceType LookupResultReferenceType(NodeFactory factory)
        {
            return GenericLookupResultReferenceType.Direct;
        }

        public abstract NativeLayoutVertexNode TemplateDictionaryNode(NodeFactory factory);

        public abstract void WriteDictionaryTocData(NodeFactory factory, IGenericLookupResultTocWriter writer);

        // Call this api to get non-reloc dependencies that arise from use of a dictionary lookup
        public virtual IEnumerable<DependencyNodeCore<NodeFactory>> NonRelocDependenciesFromUsage(NodeFactory factory)
        {
            return Array.Empty<DependencyNodeCore<NodeFactory>>();
        }

        public class Comparer
        {
            private TypeSystemComparer _comparer;

            public Comparer(TypeSystemComparer comparer)
            {
                _comparer = comparer;
            }

            public int Compare(GenericLookupResult x, GenericLookupResult y)
            {
                if (x == y)
                {
                    return 0;
                }

                int codeX = x.ClassCode;
                int codeY = y.ClassCode;
                if (codeX == codeY)
                {
                    Debug.Assert(x.GetType() == y.GetType());

                    int result = x.CompareToImpl(y, _comparer);

                    // We did a reference equality check above so an "Equal" result is not expected
                    Debug.Assert(result != 0);

                    return result;
                }
                else
                {
                    Debug.Assert(x.GetType() != y.GetType());
                    return codeX > codeY ? -1 : 1;
                }
            }
        }
    }

    /// <summary>
    /// Generic lookup result that points to an EEType.
    /// </summary>
    public sealed class TypeHandleGenericLookupResult : GenericLookupResult
    {
        private TypeDesc _type;

        protected override int ClassCode => 1623839081;

        public TypeHandleGenericLookupResult(TypeDesc type)
        {
            Debug.Assert(type.IsRuntimeDeterminedSubtype, "Concrete type in a generic dictionary?");
            _type = type;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, GenericLookupResultContext dictionary)
        {
            // We are getting a constructed type symbol because this might be something passed to newobj.
            TypeDesc instantiatedType = _type.GetNonRuntimeDeterminedTypeFromRuntimeDeterminedSubtypeViaSubstitution(dictionary.TypeInstantiation, dictionary.MethodInstantiation);
            return factory.ConstructedTypeSymbol(instantiatedType);
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("TypeHandle_");
            sb.Append(nameMangler.GetMangledTypeName(_type));
        }

        public TypeDesc Type => _type;
        public override string ToString() => $"TypeHandle: {_type}";

        public override NativeLayoutVertexNode TemplateDictionaryNode(NodeFactory factory)
        {
            return factory.NativeLayout.TypeHandleDictionarySlot(_type);
        }

        public override GenericLookupResultReferenceType LookupResultReferenceType(NodeFactory factory)
        {
            if (factory.CompilationModuleGroup.CanHaveReferenceThroughImportTable)
            {
                return GenericLookupResultReferenceType.ConditionalIndirect;
            }
            else
            {
                return GenericLookupResultReferenceType.Direct;
            }
        }

        public override void WriteDictionaryTocData(NodeFactory factory, IGenericLookupResultTocWriter writer)
        {
            writer.WriteData(LookupResultReferenceType(factory), LookupResultType.EEType, _type);
        }

        protected override int CompareToImpl(GenericLookupResult other, TypeSystemComparer comparer)
        {
            return comparer.Compare(_type, ((TypeHandleGenericLookupResult)other)._type);
        }

        protected override int GetHashCodeImpl()
        {
            return _type.GetHashCode();
        }

        protected override bool EqualsImpl(GenericLookupResult obj)
        {
            return ((TypeHandleGenericLookupResult)obj)._type == _type;
        }
    }


    /// <summary>
    /// Generic lookup result that points to an EEType where if the type is Nullable&lt;X&gt; the EEType is X
    /// </summary>
    internal sealed class UnwrapNullableTypeHandleGenericLookupResult : GenericLookupResult
    {
        private TypeDesc _type;

        protected override int ClassCode => 53521918;

        public UnwrapNullableTypeHandleGenericLookupResult(TypeDesc type)
        {
            Debug.Assert(type.IsRuntimeDeterminedSubtype, "Concrete type in a generic dictionary?");
            _type = type;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, GenericLookupResultContext dictionary)
        {
            TypeDesc instantiatedType = _type.GetNonRuntimeDeterminedTypeFromRuntimeDeterminedSubtypeViaSubstitution(dictionary.TypeInstantiation, dictionary.MethodInstantiation);

            // Unwrap the nullable type if necessary
            if (instantiatedType.IsNullable)
                instantiatedType = instantiatedType.Instantiation[0];

            // We are getting a constructed type symbol because this might be something passed to newobj.
            return factory.ConstructedTypeSymbol(instantiatedType);
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("UnwrapNullable_");
            sb.Append(nameMangler.GetMangledTypeName(_type));
        }

        public override string ToString() => $"UnwrapNullable: {_type}";

        public override NativeLayoutVertexNode TemplateDictionaryNode(NodeFactory factory)
        {
            return factory.NativeLayout.UnwrapNullableTypeDictionarySlot(_type);
        }

        public override GenericLookupResultReferenceType LookupResultReferenceType(NodeFactory factory)
        {
            if (factory.CompilationModuleGroup.CanHaveReferenceThroughImportTable)
            {
                return GenericLookupResultReferenceType.ConditionalIndirect;
            }
            else
            {
                return GenericLookupResultReferenceType.Direct;
            }
        }

        public override void WriteDictionaryTocData(NodeFactory factory, IGenericLookupResultTocWriter writer)
        {
            writer.WriteData(LookupResultReferenceType(factory), LookupResultType.UnwrapNullable, _type);
        }

        protected override int CompareToImpl(GenericLookupResult other, TypeSystemComparer comparer)
        {
            return comparer.Compare(_type, ((UnwrapNullableTypeHandleGenericLookupResult)other)._type);
        }

        protected override int GetHashCodeImpl()
        {
            return _type.GetHashCode();
        }

        protected override bool EqualsImpl(GenericLookupResult obj)
        {
            return ((UnwrapNullableTypeHandleGenericLookupResult)obj)._type == _type;
        }
    }

    /// <summary>
    /// Generic lookup result that puts a field offset into the generic dictionary.
    /// </summary>
    internal sealed class FieldOffsetGenericLookupResult : GenericLookupResult
    {
        private FieldDesc _field;

        protected override int ClassCode => -1670293557;

        public FieldOffsetGenericLookupResult(FieldDesc field)
        {
            Debug.Assert(field.OwningType.IsRuntimeDeterminedSubtype, "Concrete field in a generic dictionary?");
            _field = field;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, GenericLookupResultContext dictionary)
        {
            Debug.Assert(false, "GetTarget for a FieldOffsetGenericLookupResult doesn't make sense. It isn't a pointer being emitted");
            return null;
        }

        public override void EmitDictionaryEntry(ref ObjectDataBuilder builder, NodeFactory factory, GenericLookupResultContext dictionary)
        {
            FieldDesc instantiatedField = _field.GetNonRuntimeDeterminedFieldFromRuntimeDeterminedFieldViaSubstitution(dictionary.TypeInstantiation, dictionary.MethodInstantiation);
            int offset = instantiatedField.Offset.AsInt;
            builder.EmitNaturalInt(offset);
        }


        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("FieldOffset_");
            sb.Append(nameMangler.GetMangledFieldName(_field));
        }

        public override string ToString() => $"FieldOffset: {_field}";

        public override NativeLayoutVertexNode TemplateDictionaryNode(NodeFactory factory)
        {
            return factory.NativeLayout.FieldOffsetDictionarySlot(_field);
        }

        public override void WriteDictionaryTocData(NodeFactory factory, IGenericLookupResultTocWriter writer)
        {
            writer.WriteData(LookupResultReferenceType(factory), LookupResultType.FieldOffset, _field);
        }

        protected override int CompareToImpl(GenericLookupResult other, TypeSystemComparer comparer)
        {
            return comparer.Compare(_field, ((FieldOffsetGenericLookupResult)other)._field);
        }

        protected override int GetHashCodeImpl()
        {
            return _field.GetHashCode();
        }

        protected override bool EqualsImpl(GenericLookupResult obj)
        {
            return ((FieldOffsetGenericLookupResult)obj)._field == _field;
        }
    }

    /// <summary>
    /// Generic lookup result that puts a vtable offset into the generic dictionary.
    /// </summary>
    internal sealed class VTableOffsetGenericLookupResult : GenericLookupResult
    {
        private MethodDesc _method;

        protected override int ClassCode => 386794182;

        public VTableOffsetGenericLookupResult(MethodDesc method)
        {
            Debug.Assert(method.IsRuntimeDeterminedExactMethod, "Concrete method in a generic dictionary?");
            _method = method;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, GenericLookupResultContext dictionary)
        {
            Debug.Assert(false, "GetTarget for a VTableOffsetGenericLookupResult doesn't make sense. It isn't a pointer being emitted");
            return null;
        }

        public override void EmitDictionaryEntry(ref ObjectDataBuilder builder, NodeFactory factory, GenericLookupResultContext dictionary)
        {
            Debug.Assert(false, "VTableOffset contents should only be generated into generic dictionaries at runtime");
            builder.EmitNaturalInt(0);
        }


        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("VTableOffset_");
            sb.Append(nameMangler.GetMangledMethodName(_method));
        }

        public override string ToString() => $"VTableOffset: {_method}";

        public override NativeLayoutVertexNode TemplateDictionaryNode(NodeFactory factory)
        {
            return factory.NativeLayout.VTableOffsetDictionarySlot(_method);
        }

        public override IEnumerable<DependencyNodeCore<NodeFactory>> NonRelocDependenciesFromUsage(NodeFactory factory)
        {
            MethodDesc canonMethod = _method.GetCanonMethodTarget(CanonicalFormKind.Universal);

            // If we're producing a full vtable for the type, we don't need to report virtual method use.
            if (factory.VTable(canonMethod.OwningType).HasFixedSlots)
                return Array.Empty<DependencyNodeCore<NodeFactory>>();

            return new DependencyNodeCore<NodeFactory>[] {
                factory.VirtualMethodUse(canonMethod)
            };
        }

        public override void WriteDictionaryTocData(NodeFactory factory, IGenericLookupResultTocWriter writer)
        {
            writer.WriteData(LookupResultReferenceType(factory), LookupResultType.VtableOffset, _method);
        }

        protected override int CompareToImpl(GenericLookupResult other, TypeSystemComparer comparer)
        {
            return comparer.Compare(_method, ((VTableOffsetGenericLookupResult)other)._method);
        }

        protected override int GetHashCodeImpl()
        {
            return _method.GetHashCode();
        }

        protected override bool EqualsImpl(GenericLookupResult obj)
        {
            return ((VTableOffsetGenericLookupResult)obj)._method == _method;
        }
    }

    /// <summary>
    /// Generic lookup result that points to a RuntimeMethodHandle.
    /// </summary>
    internal sealed class MethodHandleGenericLookupResult : GenericLookupResult
    {
        private MethodDesc _method;

        protected override int ClassCode => 394272689;

        public MethodHandleGenericLookupResult(MethodDesc method)
        {
            Debug.Assert(method.IsRuntimeDeterminedExactMethod, "Concrete method in a generic dictionary?");
            _method = method;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, GenericLookupResultContext dictionary)
        {
            MethodDesc instantiatedMethod = _method.GetNonRuntimeDeterminedMethodFromRuntimeDeterminedMethodViaSubstitution(dictionary.TypeInstantiation, dictionary.MethodInstantiation);
            return factory.RuntimeMethodHandle(instantiatedMethod);
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("MethodHandle_");
            sb.Append(nameMangler.GetMangledMethodName(_method));
        }

        public override string ToString() => $"MethodHandle: {_method}";

        public override NativeLayoutVertexNode TemplateDictionaryNode(NodeFactory factory)
        {
            return factory.NativeLayout.MethodLdTokenDictionarySlot(_method);
        }

        public override void WriteDictionaryTocData(NodeFactory factory, IGenericLookupResultTocWriter writer)
        {
            writer.WriteData(LookupResultReferenceType(factory), LookupResultType.MethodLdToken, _method);
        }

        protected override int CompareToImpl(GenericLookupResult other, TypeSystemComparer comparer)
        {
            return comparer.Compare(_method, ((MethodHandleGenericLookupResult)other)._method);
        }

        protected override int GetHashCodeImpl()
        {
            return _method.GetHashCode();
        }

        protected override bool EqualsImpl(GenericLookupResult obj)
        {
            return ((MethodHandleGenericLookupResult)obj)._method == _method;
        }
    }

    /// <summary>
    /// Generic lookup result that points to a RuntimeFieldHandle.
    /// </summary>
    internal sealed class FieldHandleGenericLookupResult : GenericLookupResult
    {
        private FieldDesc _field;

        protected override int ClassCode => -196995964;

        public FieldHandleGenericLookupResult(FieldDesc field)
        {
            Debug.Assert(field.OwningType.IsRuntimeDeterminedSubtype, "Concrete field in a generic dictionary?");
            _field = field;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, GenericLookupResultContext dictionary)
        {
            FieldDesc instantiatedField = _field.GetNonRuntimeDeterminedFieldFromRuntimeDeterminedFieldViaSubstitution(dictionary.TypeInstantiation, dictionary.MethodInstantiation);
            return factory.RuntimeFieldHandle(instantiatedField);
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("FieldHandle_");
            sb.Append(nameMangler.GetMangledFieldName(_field));
        }

        public override string ToString() => $"FieldHandle: {_field}";

        public override NativeLayoutVertexNode TemplateDictionaryNode(NodeFactory factory)
        {
            return factory.NativeLayout.FieldLdTokenDictionarySlot(_field);
        }

        public override void WriteDictionaryTocData(NodeFactory factory, IGenericLookupResultTocWriter writer)
        {
            writer.WriteData(LookupResultReferenceType(factory), LookupResultType.FieldLdToken, _field);
        }

        protected override int CompareToImpl(GenericLookupResult other, TypeSystemComparer comparer)
        {
            return comparer.Compare(_field, ((FieldHandleGenericLookupResult)other)._field);
        }

        protected override int GetHashCodeImpl()
        {
            return _field.GetHashCode();
        }

        protected override bool EqualsImpl(GenericLookupResult obj)
        {
            return ((FieldHandleGenericLookupResult)obj)._field == _field;
        }
    }

    /// <summary>
    /// Generic lookup result that points to a method dictionary.
    /// </summary>
    internal sealed class MethodDictionaryGenericLookupResult : GenericLookupResult
    {
        private MethodDesc _method;

        protected override int ClassCode => -467418176;

        public MethodDictionaryGenericLookupResult(MethodDesc method)
        {
            Debug.Assert(method.IsRuntimeDeterminedExactMethod, "Concrete method in a generic dictionary?");
            _method = method;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, GenericLookupResultContext dictionary)
        {
            MethodDesc instantiatedMethod = _method.GetNonRuntimeDeterminedMethodFromRuntimeDeterminedMethodViaSubstitution(dictionary.TypeInstantiation, dictionary.MethodInstantiation);
            return factory.MethodGenericDictionary(instantiatedMethod);
        }

        public override GenericLookupResultReferenceType LookupResultReferenceType(NodeFactory factory)
        {
            if (factory.CompilationModuleGroup.CanHaveReferenceThroughImportTable)
            {
                return GenericLookupResultReferenceType.ConditionalIndirect;
            }
            else
            {
                return GenericLookupResultReferenceType.Direct;
            }
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("MethodDictionary_");
            sb.Append(nameMangler.GetMangledMethodName(_method));
        }

        public override string ToString() => $"MethodDictionary: {_method}";

        public override NativeLayoutVertexNode TemplateDictionaryNode(NodeFactory factory)
        {
            return factory.NativeLayout.MethodDictionaryDictionarySlot(_method);
        }

        public override void WriteDictionaryTocData(NodeFactory factory, IGenericLookupResultTocWriter writer)
        {
            writer.WriteData(LookupResultReferenceType(factory), LookupResultType.MethodDictionary, _method);
        }

        protected override int CompareToImpl(GenericLookupResult other, TypeSystemComparer comparer)
        {
            return comparer.Compare(_method, ((MethodDictionaryGenericLookupResult)other)._method);
        }

        protected override int GetHashCodeImpl()
        {
            return _method.GetHashCode();
        }

        protected override bool EqualsImpl(GenericLookupResult obj)
        {
            return ((MethodDictionaryGenericLookupResult)obj)._method == _method;
        }
    }

    /// <summary>
    /// Generic lookup result that is a function pointer.
    /// </summary>
    internal sealed class MethodEntryGenericLookupResult : GenericLookupResult
    {
        private MethodDesc _method;
        private bool _isUnboxingThunk;

        protected override int ClassCode => 1572293098;

        public MethodEntryGenericLookupResult(MethodDesc method, bool isUnboxingThunk)
        {
            Debug.Assert(method.IsRuntimeDeterminedExactMethod);
            _method = method;
            _isUnboxingThunk = isUnboxingThunk;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, GenericLookupResultContext dictionary)
        {
            MethodDesc instantiatedMethod = _method.GetNonRuntimeDeterminedMethodFromRuntimeDeterminedMethodViaSubstitution(dictionary.TypeInstantiation, dictionary.MethodInstantiation);
            return factory.FatFunctionPointer(instantiatedMethod, _isUnboxingThunk);
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            if (!_isUnboxingThunk)
                sb.Append("MethodEntry_");
            else
                sb.Append("UnboxMethodEntry_");

            sb.Append(nameMangler.GetMangledMethodName(_method));
        }

        public override string ToString() => $"MethodEntry: {_method}";

        public override NativeLayoutVertexNode TemplateDictionaryNode(NodeFactory factory)
        {
            MethodDesc canonMethod = _method.GetCanonMethodTarget(CanonicalFormKind.Specific);

            //
            // For universal canonical methods, we don't need the unboxing stub really, because
            // the calling convention translation thunk will handle the unboxing (and we can avoid having a double thunk here)
            // We just need the flag in the native layout info signature indicating that we needed an unboxing stub
            //
            bool getUnboxingStubNode = _isUnboxingThunk && !canonMethod.IsCanonicalMethod(CanonicalFormKind.Universal);

            return factory.NativeLayout.MethodEntrypointDictionarySlot(
                _method, 
                _isUnboxingThunk,
                factory.MethodEntrypoint(canonMethod, getUnboxingStubNode));
        }

        public override void WriteDictionaryTocData(NodeFactory factory, IGenericLookupResultTocWriter writer)
        {
            writer.WriteData(LookupResultReferenceType(factory), LookupResultType.Method, _method);
        }

        protected override int CompareToImpl(GenericLookupResult other, TypeSystemComparer comparer)
        {
            var otherEntry = (MethodEntryGenericLookupResult)other;
            int result = (_isUnboxingThunk ? 1 : 0) - (otherEntry._isUnboxingThunk ? 1 : 0);
            if (result != 0)
                return result;

            return comparer.Compare(_method, otherEntry._method);
        }

        protected override int GetHashCodeImpl()
        {
            return _method.GetHashCode();
        }

        protected override bool EqualsImpl(GenericLookupResult obj)
        {
            return ((MethodEntryGenericLookupResult)obj)._method == _method &&
                ((MethodEntryGenericLookupResult)obj)._isUnboxingThunk == _isUnboxingThunk;
        }
    }

    /// <summary>
    /// Generic lookup result that points to a dispatch cell.
    /// </summary>
    internal sealed class VirtualDispatchCellGenericLookupResult : GenericLookupResult
    {
        private MethodDesc _method;

        protected override int ClassCode => 643566930;

        public VirtualDispatchCellGenericLookupResult(MethodDesc method)
        {
            Debug.Assert(method.IsRuntimeDeterminedExactMethod);
            Debug.Assert(method.IsVirtual);
            Debug.Assert(method.OwningType.IsInterface);

            _method = method;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, GenericLookupResultContext context)
        {
            MethodDesc instantiatedMethod = _method.GetNonRuntimeDeterminedMethodFromRuntimeDeterminedMethodViaSubstitution(context.TypeInstantiation, context.MethodInstantiation);

            TypeSystemEntity contextOwner = context.Context;
            GenericDictionaryNode dictionary =
                contextOwner is TypeDesc ?
                (GenericDictionaryNode)factory.TypeGenericDictionary((TypeDesc)contextOwner) :
                (GenericDictionaryNode)factory.MethodGenericDictionary((MethodDesc)contextOwner);

            return factory.InterfaceDispatchCell(instantiatedMethod, dictionary.GetMangledName(factory.NameMangler));
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("DispatchCell_");
            sb.Append(nameMangler.GetMangledMethodName(_method));
        }

        public override string ToString() => $"DispatchCell: {_method}";

        public override NativeLayoutVertexNode TemplateDictionaryNode(NodeFactory factory)
        {
            return factory.NativeLayout.InterfaceCellDictionarySlot(_method);
        }

        public override void WriteDictionaryTocData(NodeFactory factory, IGenericLookupResultTocWriter writer)
        {
            writer.WriteData(LookupResultReferenceType(factory), LookupResultType.InterfaceDispatchCell, _method);
        }

        protected override int CompareToImpl(GenericLookupResult other, TypeSystemComparer comparer)
        {
            return comparer.Compare(_method, ((VirtualDispatchCellGenericLookupResult)other)._method);
        }

        protected override int GetHashCodeImpl()
        {
            return _method.GetHashCode();
        }

        protected override bool EqualsImpl(GenericLookupResult obj)
        {
            return ((VirtualDispatchCellGenericLookupResult)obj)._method == _method;
        }
    }

    /// <summary>
    /// Generic lookup result that points to the non-GC static base of a type.
    /// </summary>
    internal sealed class TypeNonGCStaticBaseGenericLookupResult : GenericLookupResult
    {
        private MetadataType _type;

        protected override int ClassCode => -328863267;

        public TypeNonGCStaticBaseGenericLookupResult(TypeDesc type)
        {
            Debug.Assert(type.IsRuntimeDeterminedSubtype, "Concrete static base in a generic dictionary?");
            Debug.Assert(type is MetadataType);
            _type = (MetadataType)type;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, GenericLookupResultContext dictionary)
        {
            var instantiatedType = (MetadataType)_type.GetNonRuntimeDeterminedTypeFromRuntimeDeterminedSubtypeViaSubstitution(dictionary.TypeInstantiation, dictionary.MethodInstantiation);
            return factory.Indirection(factory.TypeNonGCStaticsSymbol(instantiatedType));
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("NonGCStaticBase_");
            sb.Append(nameMangler.GetMangledTypeName(_type));
        }

        public override string ToString() => $"NonGCStaticBase: {_type}";

        public override NativeLayoutVertexNode TemplateDictionaryNode(NodeFactory factory)
        {
            return factory.NativeLayout.NonGcStaticDictionarySlot(_type);
        }

        public override GenericLookupResultReferenceType LookupResultReferenceType(NodeFactory factory)
        {
            return GenericLookupResultReferenceType.Indirect;
        }

        public override void WriteDictionaryTocData(NodeFactory factory, IGenericLookupResultTocWriter writer)
        {
            writer.WriteData(LookupResultReferenceType(factory), LookupResultType.NonGcStatic, _type);
        }

        protected override int CompareToImpl(GenericLookupResult other, TypeSystemComparer comparer)
        {
            return comparer.Compare(_type, ((TypeNonGCStaticBaseGenericLookupResult)other)._type);
        }

        protected override int GetHashCodeImpl()
        {
            return _type.GetHashCode();
        }

        protected override bool EqualsImpl(GenericLookupResult obj)
        {
            return ((TypeNonGCStaticBaseGenericLookupResult)obj)._type == _type;
        }
    }

    /// <summary>
    /// Generic lookup result that points to the threadstatic base index of a type.
    /// </summary>
    internal sealed class TypeThreadStaticBaseIndexGenericLookupResult : GenericLookupResult
    {
        private MetadataType _type;

        protected override int ClassCode => -177446371;

        public TypeThreadStaticBaseIndexGenericLookupResult(TypeDesc type)
        {
            Debug.Assert(type.IsRuntimeDeterminedSubtype, "Concrete static base in a generic dictionary?");
            Debug.Assert(type is MetadataType);
            _type = (MetadataType)type;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, GenericLookupResultContext dictionary)
        {
            var instantiatedType = (MetadataType)_type.GetNonRuntimeDeterminedTypeFromRuntimeDeterminedSubtypeViaSubstitution(dictionary.TypeInstantiation, dictionary.MethodInstantiation);
            return factory.TypeThreadStaticIndex(instantiatedType);
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("ThreadStaticBase_");
            sb.Append(nameMangler.GetMangledTypeName(_type));
        }

        public override string ToString() => $"ThreadStaticBase: {_type}";

        public override NativeLayoutVertexNode TemplateDictionaryNode(NodeFactory factory)
        {
            return factory.NativeLayout.NotSupportedDictionarySlot;
        }

        public override void WriteDictionaryTocData(NodeFactory factory, IGenericLookupResultTocWriter writer)
        {
            // TODO
            throw new NotImplementedException();
        }

        protected override int CompareToImpl(GenericLookupResult other, TypeSystemComparer comparer)
        {
            return comparer.Compare(_type, ((TypeThreadStaticBaseIndexGenericLookupResult)other)._type);
        }

        protected override int GetHashCodeImpl()
        {
            return _type.GetHashCode();
        }

        protected override bool EqualsImpl(GenericLookupResult obj)
        {
            return ((TypeThreadStaticBaseIndexGenericLookupResult)obj)._type == _type;
        }
    }

    /// <summary>
    /// Generic lookup result that points to the GC static base of a type.
    /// </summary>
    public sealed class TypeGCStaticBaseGenericLookupResult : GenericLookupResult
    {
        private MetadataType _type;

        protected override int ClassCode => 429225829;

        public TypeGCStaticBaseGenericLookupResult(TypeDesc type)
        {
            Debug.Assert(type.IsRuntimeDeterminedSubtype, "Concrete static base in a generic dictionary?");
            Debug.Assert(type is MetadataType);
            _type = (MetadataType)type;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, GenericLookupResultContext dictionary)
        {
            var instantiatedType = (MetadataType)_type.GetNonRuntimeDeterminedTypeFromRuntimeDeterminedSubtypeViaSubstitution(dictionary.TypeInstantiation, dictionary.MethodInstantiation);
            return factory.Indirection(factory.TypeGCStaticsSymbol(instantiatedType));
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("GCStaticBase_");
            sb.Append(nameMangler.GetMangledTypeName(_type));
        }

        public MetadataType Type => _type;
        public override string ToString() => $"GCStaticBase: {_type}";

        public override NativeLayoutVertexNode TemplateDictionaryNode(NodeFactory factory)
        {
            return factory.NativeLayout.GcStaticDictionarySlot(_type);
        }

        public override GenericLookupResultReferenceType LookupResultReferenceType(NodeFactory factory)
        {
            return GenericLookupResultReferenceType.Indirect;
        }

        public override void WriteDictionaryTocData(NodeFactory factory, IGenericLookupResultTocWriter writer)
        {
            writer.WriteData(LookupResultReferenceType(factory), LookupResultType.GcStatic, _type);
        }

        protected override int CompareToImpl(GenericLookupResult other, TypeSystemComparer comparer)
        {
            return comparer.Compare(_type, ((TypeGCStaticBaseGenericLookupResult)other)._type);
        }

        protected override int GetHashCodeImpl()
        {
            return _type.GetHashCode();
        }

        protected override bool EqualsImpl(GenericLookupResult obj)
        {
            return ((TypeGCStaticBaseGenericLookupResult)obj)._type == _type;
        }
    }

    /// <summary>
    /// Generic lookup result that points to an object allocator.
    /// </summary>
    internal sealed class ObjectAllocatorGenericLookupResult : GenericLookupResult
    {
        private TypeDesc _type;

        protected override int ClassCode => -1671431655;

        public ObjectAllocatorGenericLookupResult(TypeDesc type)
        {
            Debug.Assert(type.IsRuntimeDeterminedSubtype, "Concrete type in a generic dictionary?");
            _type = type;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, GenericLookupResultContext dictionary)
        {
            TypeDesc instantiatedType = _type.GetNonRuntimeDeterminedTypeFromRuntimeDeterminedSubtypeViaSubstitution(dictionary.TypeInstantiation, dictionary.MethodInstantiation);
            return factory.Indirection(factory.ExternSymbol(JitHelper.GetNewObjectHelperForType(instantiatedType)));
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("AllocObject_");
            sb.Append(nameMangler.GetMangledTypeName(_type));
        }

        public override string ToString() => $"AllocObject: {_type}";

        public override NativeLayoutVertexNode TemplateDictionaryNode(NodeFactory factory)
        {
            return factory.NativeLayout.AllocateObjectDictionarySlot(_type);
        }

        public override GenericLookupResultReferenceType LookupResultReferenceType(NodeFactory factory)
        {
            return GenericLookupResultReferenceType.Indirect;
        }

        public override void WriteDictionaryTocData(NodeFactory factory, IGenericLookupResultTocWriter writer)
        {
            writer.WriteData(LookupResultReferenceType(factory), LookupResultType.AllocObject, _type);
        }

        protected override int CompareToImpl(GenericLookupResult other, TypeSystemComparer comparer)
        {
            return comparer.Compare(_type, ((ObjectAllocatorGenericLookupResult)other)._type);
        }

        protected override int GetHashCodeImpl()
        {
            return _type.GetHashCode();
        }

        protected override bool EqualsImpl(GenericLookupResult obj)
        {
            return ((ObjectAllocatorGenericLookupResult)obj)._type == _type;
        }
    }

    /// <summary>
    /// Generic lookup result that points to an array allocator.
    /// </summary>
    internal sealed class ArrayAllocatorGenericLookupResult : GenericLookupResult
    {
        private TypeDesc _type;

        protected override int ClassCode => -927905284;

        public ArrayAllocatorGenericLookupResult(TypeDesc type)
        {
            Debug.Assert(type.IsRuntimeDeterminedSubtype, "Concrete type in a generic dictionary?");
            _type = type;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, GenericLookupResultContext dictionary)
        {
            TypeDesc instantiatedType = _type.GetNonRuntimeDeterminedTypeFromRuntimeDeterminedSubtypeViaSubstitution(dictionary.TypeInstantiation, dictionary.MethodInstantiation);
            Debug.Assert(instantiatedType.IsArray);
            return factory.Indirection(factory.ExternSymbol(JitHelper.GetNewArrayHelperForType(instantiatedType)));
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("AllocArray_");
            sb.Append(nameMangler.GetMangledTypeName(_type));
        }

        public override string ToString() => $"AllocArray: {_type}";

        public override NativeLayoutVertexNode TemplateDictionaryNode(NodeFactory factory)
        {
            return factory.NativeLayout.AllocateArrayDictionarySlot(_type);
        }

        public override GenericLookupResultReferenceType LookupResultReferenceType(NodeFactory factory)
        {
            return GenericLookupResultReferenceType.Indirect;
        }

        public override void WriteDictionaryTocData(NodeFactory factory, IGenericLookupResultTocWriter writer)
        {
            writer.WriteData(LookupResultReferenceType(factory), LookupResultType.AllocArray, _type);
        }

        protected override int CompareToImpl(GenericLookupResult other, TypeSystemComparer comparer)
        {
            return comparer.Compare(_type, ((ArrayAllocatorGenericLookupResult)other)._type);
        }

        protected override int GetHashCodeImpl()
        {
            return _type.GetHashCode();
        }

        protected override bool EqualsImpl(GenericLookupResult obj)
        {
            return ((ArrayAllocatorGenericLookupResult)obj)._type == _type;
        }
    }

    /// <summary>
    /// Generic lookup result that points to an cast helper.
    /// </summary>
    internal sealed class CastClassGenericLookupResult : GenericLookupResult
    {
        private TypeDesc _type;

        protected override int ClassCode => 1691016084;

        public CastClassGenericLookupResult(TypeDesc type)
        {
            Debug.Assert(type.IsRuntimeDeterminedSubtype, "Concrete type in a generic dictionary?");
            _type = type;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, GenericLookupResultContext dictionary)
        {
            TypeDesc instantiatedType = _type.GetNonRuntimeDeterminedTypeFromRuntimeDeterminedSubtypeViaSubstitution(dictionary.TypeInstantiation, dictionary.MethodInstantiation);
            return factory.Indirection(factory.ExternSymbol(JitHelper.GetCastingHelperNameForType(instantiatedType, true)));
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("CastClass_");
            sb.Append(nameMangler.GetMangledTypeName(_type));
        }

        public override string ToString() => $"CastClass: {_type}";

        public override NativeLayoutVertexNode TemplateDictionaryNode(NodeFactory factory)
        {
            return factory.NativeLayout.CastClassDictionarySlot(_type);
        }

        public override GenericLookupResultReferenceType LookupResultReferenceType(NodeFactory factory)
        {
            return GenericLookupResultReferenceType.Indirect;
        }

        public override void WriteDictionaryTocData(NodeFactory factory, IGenericLookupResultTocWriter writer)
        {
            writer.WriteData(LookupResultReferenceType(factory), LookupResultType.CastClass, _type);
        }

        protected override int CompareToImpl(GenericLookupResult other, TypeSystemComparer comparer)
        {
            return comparer.Compare(_type, ((CastClassGenericLookupResult)other)._type);
        }

        protected override int GetHashCodeImpl()
        {
            return _type.GetHashCode();
        }

        protected override bool EqualsImpl(GenericLookupResult obj)
        {
            return ((CastClassGenericLookupResult)obj)._type == _type;
        }
    }
    
    /// <summary>
    /// Generic lookup result that points to an isInst helper.
    /// </summary>
    internal sealed class IsInstGenericLookupResult : GenericLookupResult
    {
        private TypeDesc _type;

        protected override int ClassCode => 1724059349;

        public IsInstGenericLookupResult(TypeDesc type)
        {
            Debug.Assert(type.IsRuntimeDeterminedSubtype, "Concrete type in a generic dictionary?");
            _type = type;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, GenericLookupResultContext dictionary)
        {
            TypeDesc instantiatedType = _type.GetNonRuntimeDeterminedTypeFromRuntimeDeterminedSubtypeViaSubstitution(dictionary.TypeInstantiation, dictionary.MethodInstantiation);
            return factory.Indirection(factory.ExternSymbol(JitHelper.GetCastingHelperNameForType(instantiatedType, false)));
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("IsInst_");
            sb.Append(nameMangler.GetMangledTypeName(_type));
        }

        public override string ToString() => $"IsInst: {_type}";

        public override NativeLayoutVertexNode TemplateDictionaryNode(NodeFactory factory)
        {
            return factory.NativeLayout.IsInstDictionarySlot(_type);
        }

        public override GenericLookupResultReferenceType LookupResultReferenceType(NodeFactory factory)
        {
            return GenericLookupResultReferenceType.Indirect;
        }

        public override void WriteDictionaryTocData(NodeFactory factory, IGenericLookupResultTocWriter writer)
        {
            writer.WriteData(LookupResultReferenceType(factory), LookupResultType.IsInst, _type);
        }

        protected override int CompareToImpl(GenericLookupResult other, TypeSystemComparer comparer)
        {
            return comparer.Compare(_type, ((IsInstGenericLookupResult)other)._type);
        }

        protected override int GetHashCodeImpl()
        {
            return _type.GetHashCode();
        }

        protected override bool EqualsImpl(GenericLookupResult obj)
        {
            return ((IsInstGenericLookupResult)obj)._type == _type;
        }
    }

    internal sealed class ThreadStaticIndexLookupResult : GenericLookupResult
    {
        private TypeDesc _type;

        protected override int ClassCode => -25938157;

        public ThreadStaticIndexLookupResult(TypeDesc type)
        {
            Debug.Assert(type.IsRuntimeDeterminedSubtype, "Concrete type in a generic dictionary?");
            _type = type;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, GenericLookupResultContext dictionary)
        {
            UtcNodeFactory utcNodeFactory = factory as UtcNodeFactory;
            Debug.Assert(utcNodeFactory != null);
            TypeDesc instantiatedType = _type.GetNonRuntimeDeterminedTypeFromRuntimeDeterminedSubtypeViaSubstitution(dictionary.TypeInstantiation, dictionary.MethodInstantiation);
            return utcNodeFactory.TypeThreadStaticsIndexSymbol(instantiatedType);
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("TlsIndex_");
            sb.Append(nameMangler.GetMangledTypeName(_type));
        }

        public override string ToString() => $"ThreadStaticIndex: {_type}";

        public override NativeLayoutVertexNode TemplateDictionaryNode(NodeFactory factory)
        {
            return factory.NativeLayout.TlsIndexDictionarySlot(_type);
        }

        public override GenericLookupResultReferenceType LookupResultReferenceType(NodeFactory factory)
        {
            return GenericLookupResultReferenceType.ConditionalIndirect;
        }

        public override void WriteDictionaryTocData(NodeFactory factory, IGenericLookupResultTocWriter writer)
        {
            writer.WriteData(LookupResultReferenceType(factory), LookupResultType.TlsIndex, _type);
        }

        protected override int CompareToImpl(GenericLookupResult other, TypeSystemComparer comparer)
        {
            return comparer.Compare(_type, ((ThreadStaticIndexLookupResult)other)._type);
        }

        protected override int GetHashCodeImpl()
        {
            return _type.GetHashCode();
        }

        protected override bool EqualsImpl(GenericLookupResult obj)
        {
            return ((ThreadStaticIndexLookupResult)obj)._type == _type;
        }
    }

    public sealed class ThreadStaticOffsetLookupResult : GenericLookupResult
    {
        private TypeDesc _type;

        protected override int ClassCode => -1678275787;

        public ThreadStaticOffsetLookupResult(TypeDesc type)
        {
            Debug.Assert(type.IsRuntimeDeterminedSubtype, "Concrete type in a generic dictionary?");
            _type = type;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, GenericLookupResultContext dictionary)
        {
            UtcNodeFactory utcNodeFactory = factory as UtcNodeFactory;
            Debug.Assert(utcNodeFactory != null);
            TypeDesc instantiatedType = _type.GetNonRuntimeDeterminedTypeFromRuntimeDeterminedSubtypeViaSubstitution(dictionary.TypeInstantiation, dictionary.MethodInstantiation);
            Debug.Assert(instantiatedType is MetadataType);
            return utcNodeFactory.TypeThreadStaticsOffsetSymbol((MetadataType)instantiatedType);
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("TlsOffset_");
            sb.Append(nameMangler.GetMangledTypeName(_type));
        }

        public TypeDesc Type => _type;
        public override string ToString() => $"ThreadStaticOffset: {_type}";

        public override NativeLayoutVertexNode TemplateDictionaryNode(NodeFactory factory)
        {
            return factory.NativeLayout.TlsOffsetDictionarySlot(_type);
        }

        public override GenericLookupResultReferenceType LookupResultReferenceType(NodeFactory factory)
        {
            return GenericLookupResultReferenceType.ConditionalIndirect;
        }

        public override void WriteDictionaryTocData(NodeFactory factory, IGenericLookupResultTocWriter writer)
        {
            writer.WriteData(LookupResultReferenceType(factory), LookupResultType.TlsOffset, _type);
        }

        protected override int CompareToImpl(GenericLookupResult other, TypeSystemComparer comparer)
        {
            return comparer.Compare(_type, ((ThreadStaticOffsetLookupResult)other)._type);
        }

        protected override int GetHashCodeImpl()
        {
            return _type.GetHashCode();
        }

        protected override bool EqualsImpl(GenericLookupResult obj)
        {
            return ((ThreadStaticOffsetLookupResult)obj)._type == _type;
        }
    }

    internal sealed class DefaultConstructorLookupResult : GenericLookupResult
    {
        private TypeDesc _type;

        protected override int ClassCode => -1391112482;

        public DefaultConstructorLookupResult(TypeDesc type)
        {
            Debug.Assert(type.IsRuntimeDeterminedSubtype, "Concrete type in a generic dictionary?");
            _type = type;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, GenericLookupResultContext dictionary)
        {
            TypeDesc instantiatedType = _type.GetNonRuntimeDeterminedTypeFromRuntimeDeterminedSubtypeViaSubstitution(dictionary.TypeInstantiation, dictionary.MethodInstantiation);
            MethodDesc defaultCtor = instantiatedType.GetDefaultConstructor();

            if (defaultCtor == null)
            {
                // If there isn't a default constructor, use the fallback one.
                MetadataType missingCtorType = factory.TypeSystemContext.SystemModule.GetKnownType("System", "Activator");
                missingCtorType = missingCtorType.GetKnownNestedType("ClassWithMissingConstructor");                
                defaultCtor = missingCtorType.GetParameterlessConstructor();
            }
            else
            {
                defaultCtor = defaultCtor.GetCanonMethodTarget(CanonicalFormKind.Specific);
            }

            return factory.MethodEntrypoint(defaultCtor);
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("DefaultCtor_");
            sb.Append(nameMangler.GetMangledTypeName(_type));
        }

        public override string ToString() => $"DefaultConstructor: {_type}";

        public override NativeLayoutVertexNode TemplateDictionaryNode(NodeFactory factory)
        {
            return factory.NativeLayout.DefaultConstructorDictionarySlot(_type);
        }

        public override void WriteDictionaryTocData(NodeFactory factory, IGenericLookupResultTocWriter writer)
        {
            writer.WriteData(LookupResultReferenceType(factory), LookupResultType.DefaultCtor, _type);
        }

        protected override int CompareToImpl(GenericLookupResult other, TypeSystemComparer comparer)
        {
            return comparer.Compare(_type, ((DefaultConstructorLookupResult)other)._type);
        }

        protected override int GetHashCodeImpl()
        {
            return _type.GetHashCode();
        }

        protected override bool EqualsImpl(GenericLookupResult obj)
        {
            return ((DefaultConstructorLookupResult)obj)._type == _type;
        }
    }

    internal sealed class CallingConventionConverterLookupResult : GenericLookupResult
    {
        private CallingConventionConverterKey _callingConventionConverter;

        protected override int ClassCode => -581806472;

        public CallingConventionConverterLookupResult(CallingConventionConverterKey callingConventionConverter)
        {
            _callingConventionConverter = callingConventionConverter;
            Debug.Assert(Internal.Runtime.UniversalGenericParameterLayout.MethodSignatureHasVarsNeedingCallingConventionConverter(callingConventionConverter.Signature));
        }

        public override ISymbolNode GetTarget(NodeFactory factory, GenericLookupResultContext dictionary)
        {
            Debug.Assert(false, "GetTarget for a CallingConventionConverterLookupResult doesn't make sense. It isn't a pointer being emitted");
            return null;
        }

        public override void EmitDictionaryEntry(ref ObjectDataBuilder builder, NodeFactory factory, GenericLookupResultContext dictionary)
        {
            Debug.Assert(false, "CallingConventionConverterLookupResult contents should only be generated into generic dictionaries at runtime");
            builder.EmitNaturalInt(0);
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("CallingConventionConverterLookupResult_");
            sb.Append(_callingConventionConverter.GetName());
        }

        public override string ToString() => "CallingConventionConverterLookupResult";

        public override NativeLayoutVertexNode TemplateDictionaryNode(NodeFactory factory)
        {
            return factory.NativeLayout.CallingConventionConverter(_callingConventionConverter);
        }

        public override void WriteDictionaryTocData(NodeFactory factory, IGenericLookupResultTocWriter writer)
        {
            // TODO
            throw new NotImplementedException();
        }

        protected override int CompareToImpl(GenericLookupResult other, TypeSystemComparer comparer)
        {
            var otherEntry = (CallingConventionConverterLookupResult)other;
            int result = (int)(_callingConventionConverter.ConverterKind - otherEntry._callingConventionConverter.ConverterKind);
            if (result != 0)
                return result;

            return comparer.Compare(_callingConventionConverter.Signature, otherEntry._callingConventionConverter.Signature);
        }

        protected override int GetHashCodeImpl()
        {
            return _callingConventionConverter.GetHashCode();
        }

        protected override bool EqualsImpl(GenericLookupResult obj)
        {
            return ((CallingConventionConverterLookupResult)obj)._callingConventionConverter.Equals(_callingConventionConverter);
        }
    }

    internal sealed class TypeSizeLookupResult : GenericLookupResult
    {
        private TypeDesc _type;

        protected override int ClassCode => -367755250;

        public TypeSizeLookupResult(TypeDesc type)
        {
            _type = type;
            Debug.Assert(type.IsRuntimeDeterminedSubtype, "Concrete type in a generic dictionary?");
        }
        public override ISymbolNode GetTarget(NodeFactory factory, GenericLookupResultContext dictionary)
        {
            Debug.Assert(false, "GetTarget for a TypeSizeLookupResult doesn't make sense. It isn't a pointer being emitted");
            return null;
        }

        public override void EmitDictionaryEntry(ref ObjectDataBuilder builder, NodeFactory factory, GenericLookupResultContext dictionary)
        {
            TypeDesc instantiatedType = _type.GetNonRuntimeDeterminedTypeFromRuntimeDeterminedSubtypeViaSubstitution(dictionary.TypeInstantiation, dictionary.MethodInstantiation);
            int typeSize;

            if (_type.IsDefType)
            {
                typeSize = ((DefType)_type).InstanceFieldSize.AsInt;
            }
            else
            {
                typeSize = factory.TypeSystemContext.Target.PointerSize;
            }

            builder.EmitNaturalInt(typeSize);
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("TypeSize_");
            sb.Append(nameMangler.GetMangledTypeName(_type));
        }

        public override string ToString() => $"TypeSize: {_type}";

        public override NativeLayoutVertexNode TemplateDictionaryNode(NodeFactory factory)
        {
            return factory.NativeLayout.TypeSizeDictionarySlot(_type);
        }

        public override void WriteDictionaryTocData(NodeFactory factory, IGenericLookupResultTocWriter writer)
        {
            writer.WriteData(LookupResultReferenceType(factory), LookupResultType.TypeSize, _type);
        }

        protected override int CompareToImpl(GenericLookupResult other, TypeSystemComparer comparer)
        {
            return comparer.Compare(_type, ((TypeSizeLookupResult)other)._type);
        }

        protected override int GetHashCodeImpl()
        {
            return _type.GetHashCode();
        }

        protected override bool EqualsImpl(GenericLookupResult obj)
        {
            return ((TypeSizeLookupResult)obj)._type == _type;
        }
    }

    internal sealed class ConstrainedMethodUseLookupResult : GenericLookupResult
    {
        MethodDesc _constrainedMethod;
        TypeDesc _constraintType;
        bool _directCall;

        protected override int ClassCode => -1525377658;

        public ConstrainedMethodUseLookupResult(MethodDesc constrainedMethod, TypeDesc constraintType, bool directCall)
        {
            _constrainedMethod = constrainedMethod;
            _constraintType = constraintType;
            _directCall = directCall;

            Debug.Assert(_constraintType.IsRuntimeDeterminedSubtype || _constrainedMethod.IsRuntimeDeterminedExactMethod, "Concrete type in a generic dictionary?");
            Debug.Assert(!_constrainedMethod.HasInstantiation || !_directCall, "Direct call to constrained generic method isn't supported");
        }

        public override ISymbolNode GetTarget(NodeFactory factory, GenericLookupResultContext dictionary)
        {
            MethodDesc instantiatedConstrainedMethod = _constrainedMethod.GetNonRuntimeDeterminedMethodFromRuntimeDeterminedMethodViaSubstitution(dictionary.TypeInstantiation, dictionary.MethodInstantiation);
            TypeDesc instantiatedConstraintType = _constraintType.GetNonRuntimeDeterminedTypeFromRuntimeDeterminedSubtypeViaSubstitution(dictionary.TypeInstantiation, dictionary.MethodInstantiation);

            MethodDesc implMethod = instantiatedConstraintType.GetClosestDefType().ResolveInterfaceMethodToVirtualMethodOnType(instantiatedConstrainedMethod);

            // AOT use of this generic lookup is restricted to finding methods on valuetypes (runtime usage of this slot in universal generics is more flexible)
            Debug.Assert(instantiatedConstraintType.IsValueType);
            Debug.Assert(implMethod.OwningType == instantiatedConstraintType);

            if (implMethod.HasInstantiation)
            {
                return factory.ExactCallableAddress(implMethod);
            }
            else
            {
                return factory.MethodEntrypoint(implMethod);
            }
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("ConstrainedMethodUseLookupResult_");
            sb.Append(nameMangler.GetMangledTypeName(_constraintType));
            sb.Append(nameMangler.GetMangledMethodName(_constrainedMethod));
            if (_directCall)
                sb.Append("Direct");
        }

        public override string ToString() => $"ConstrainedMethodUseLookupResult: {_constraintType} {_constrainedMethod} {_directCall}";

        public override NativeLayoutVertexNode TemplateDictionaryNode(NodeFactory factory)
        {
            return factory.NativeLayout.ConstrainedMethodUse(_constrainedMethod, _constraintType, _directCall);
        }

        public override void WriteDictionaryTocData(NodeFactory factory, IGenericLookupResultTocWriter writer)
        {
            // TODO
            throw new NotImplementedException();
        }

        protected override int CompareToImpl(GenericLookupResult other, TypeSystemComparer comparer)
        {
            var otherResult = (ConstrainedMethodUseLookupResult)other;
            int result = (_directCall ? 1 : 0) - (otherResult._directCall ? 1 : 0);
            if (result != 0)
                return result;

            result = comparer.Compare(_constraintType, otherResult._constraintType);
            if (result != 0)
                return result;

            return comparer.Compare(_constrainedMethod, otherResult._constrainedMethod);
        }

        protected override int GetHashCodeImpl()
        {
            return _constrainedMethod.GetHashCode() * 13 + _constraintType.GetHashCode();
        }

        protected override bool EqualsImpl(GenericLookupResult obj)
        {
            var other = (ConstrainedMethodUseLookupResult)obj;
            return _constrainedMethod == other._constrainedMethod &&
                _constraintType == other._constraintType &&
                _directCall == other._directCall;
        }
    }
}
