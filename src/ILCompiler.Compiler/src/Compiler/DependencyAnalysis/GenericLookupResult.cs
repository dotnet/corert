// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Internal.IL;
using Internal.Text;
using Internal.TypeSystem;

using ILCompiler.DependencyAnalysisFramework;

using FatFunctionPointerConstants = Internal.Runtime.FatFunctionPointerConstants;

namespace ILCompiler.DependencyAnalysis
{
    public enum GenericLookupResultReferenceType
    {
        Direct,             // The slot stores a direct pointer to the target
        Indirect,           // The slot is an indirection cell which points to the direct pointer
        ConditionalIndirect, // The slot may be a direct pointer or an indirection cell, depending on the last digit
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
        public abstract ISymbolNode GetTarget(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation, GenericDictionaryNode dictionary);
        public abstract void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb);
        public abstract override string ToString();

        public virtual void EmitDictionaryEntry(ref ObjectDataBuilder builder, NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation, GenericDictionaryNode dictionary)
        {
            builder.EmitPointerReloc(GetTarget(factory, typeInstantiation, methodInstantiation, dictionary));
        }

        public virtual GenericLookupResultReferenceType LookupResultReferenceType(NodeFactory factory)
        {
            return GenericLookupResultReferenceType.Direct;
        }

        public abstract NativeLayoutVertexNode TemplateDictionaryNode(NodeFactory factory);

        // Call this api to get non-reloc dependencies that arise from use of a dictionary lookup
        public virtual IEnumerable<DependencyNodeCore<NodeFactory>> NonRelocDependenciesFromUsage(NodeFactory factory)
        {
            return Array.Empty<DependencyNodeCore<NodeFactory>>();
        }
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

        public override ISymbolNode GetTarget(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation, GenericDictionaryNode dictionary)
        {
            // We are getting a constructed type symbol because this might be something passed to newobj.
            TypeDesc instantiatedType = _type.InstantiateSignature(typeInstantiation, methodInstantiation);
            IEETypeNode typeNode = factory.ConstructedTypeSymbol(instantiatedType);

            if (typeNode.RepresentsIndirectionCell)
            {
                // Imported eetype needs another indirection. Setting the lowest bit to indicate that.
                Debug.Assert(LookupResultReferenceType(factory) == GenericLookupResultReferenceType.ConditionalIndirect);
                return factory.Indirection(typeNode, 1);
            }
            else
            {
                return typeNode;
            }
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("TypeHandle_");
            sb.Append(nameMangler.GetMangledTypeName(_type));
        }

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
    }


    /// <summary>
    /// Generic lookup result that points to an EEType where if the type is Nullable<X> the EEType is X
    /// </summary>
    internal sealed class UnwrapNullableTypeHandleGenericLookupResult : GenericLookupResult
    {
        private TypeDesc _type;

        public UnwrapNullableTypeHandleGenericLookupResult(TypeDesc type)
        {
            Debug.Assert(type.IsRuntimeDeterminedSubtype, "Concrete type in a generic dictionary?");
            _type = type;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation, GenericDictionaryNode dictionary)
        {
            TypeDesc instantiatedType = _type.InstantiateSignature(typeInstantiation, methodInstantiation);

            // Unwrap the nullable type if necessary
            if (instantiatedType.IsNullable)
                instantiatedType = instantiatedType.Instantiation[0];

            // We are getting a constructed type symbol because this might be something passed to newobj.
            IEETypeNode typeNode = factory.ConstructedTypeSymbol(instantiatedType);

            if (typeNode.RepresentsIndirectionCell)
            {
                // Imported eetype needs another indirection. Setting the lowest bit to indicate that.
                Debug.Assert(LookupResultReferenceType(factory) == GenericLookupResultReferenceType.ConditionalIndirect);
                return factory.Indirection(typeNode, 1);
            }
            else
            {
                return typeNode;
            }
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
    }

    /// <summary>
    /// Generic lookup result that puts a field offset into the generic dictionary.
    /// </summary>
    internal sealed class FieldOffsetGenericLookupResult : GenericLookupResult
    {
        private FieldDesc _field;

        public FieldOffsetGenericLookupResult(FieldDesc field)
        {
            Debug.Assert(field.OwningType.IsRuntimeDeterminedSubtype, "Concrete field in a generic dictionary?");
            _field = field;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation, GenericDictionaryNode dictionary)
        {
            Debug.Assert(false, "GetTarget for a FieldOffsetGenericLookupResult doesn't make sense. It isn't a pointer being emitted");
            return null;
        }

        public override void EmitDictionaryEntry(ref ObjectDataBuilder builder, NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation, GenericDictionaryNode dictionary)
        {
            FieldDesc instantiatedField = _field.InstantiateSignature(typeInstantiation, methodInstantiation);
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
    }

    /// <summary>
    /// Generic lookup result that puts a vtable offset into the generic dictionary.
    /// </summary>
    internal sealed class VTableOffsetGenericLookupResult : GenericLookupResult
    {
        private MethodDesc _method;

        public VTableOffsetGenericLookupResult(MethodDesc method)
        {
            Debug.Assert(method.IsRuntimeDeterminedExactMethod, "Concrete method in a generic dictionary?");
            _method = method;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation, GenericDictionaryNode dictionary)
        {
            Debug.Assert(false, "GetTarget for a VTableOffsetGenericLookupResult doesn't make sense. It isn't a pointer being emitted");
            return null;
        }

        public override void EmitDictionaryEntry(ref ObjectDataBuilder builder, NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation, GenericDictionaryNode dictionary)
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
            return new DependencyNodeCore<NodeFactory>[] {
                factory.VirtualMethodUse(_method.GetCanonMethodTarget(CanonicalFormKind.Universal))
            };
        }
    }

    /// <summary>
    /// Generic lookup result that points to a RuntimeMethodHandle.
    /// </summary>
    internal sealed class MethodHandleGenericLookupResult : GenericLookupResult
    {
        private MethodDesc _method;

        public MethodHandleGenericLookupResult(MethodDesc method)
        {
            Debug.Assert(method.IsRuntimeDeterminedExactMethod, "Concrete method in a generic dictionary?");
            _method = method;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation, GenericDictionaryNode dictionary)
        {
            MethodDesc instantiatedMethod = _method.InstantiateSignature(typeInstantiation, methodInstantiation);
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
    }

    /// <summary>
    /// Generic lookup result that points to a RuntimeFieldHandle.
    /// </summary>
    internal sealed class FieldHandleGenericLookupResult : GenericLookupResult
    {
        private FieldDesc _field;

        public FieldHandleGenericLookupResult(FieldDesc field)
        {
            Debug.Assert(field.OwningType.IsRuntimeDeterminedSubtype, "Concrete field in a generic dictionary?");
            _field = field;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation, GenericDictionaryNode dictionary)
        {
            FieldDesc instantiatedField = _field.InstantiateSignature(typeInstantiation, methodInstantiation);
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

        public override ISymbolNode GetTarget(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation, GenericDictionaryNode dictionary)
        {
            MethodDesc instantiatedMethod = _method.InstantiateSignature(typeInstantiation, methodInstantiation);
            ISymbolNode methodDictionaryNode = factory.MethodGenericDictionary(instantiatedMethod);
            if (methodDictionaryNode.RepresentsIndirectionCell)
            {
                // Imported method dictionary needs another indirection. Setting the lowest bit to indicate that.
                Debug.Assert(LookupResultReferenceType(factory) == GenericLookupResultReferenceType.ConditionalIndirect);
                return factory.Indirection(methodDictionaryNode, 1);
            }
            else
            {
                return methodDictionaryNode;
            }
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

        public override ISymbolNode GetTarget(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation, GenericDictionaryNode dictionary)
        {
            MethodDesc instantiatedMethod = _method.InstantiateSignature(typeInstantiation, methodInstantiation);
            return factory.FatFunctionPointer(instantiatedMethod);
        }

        public override void EmitDictionaryEntry(ref ObjectDataBuilder builder, NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation, GenericDictionaryNode dictionary)
        {
            builder.EmitPointerReloc(GetTarget(factory, typeInstantiation, methodInstantiation, dictionary), FatFunctionPointerConstants.Offset);
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("MethodEntry_");
            sb.Append(nameMangler.GetMangledMethodName(_method));
        }

        public override string ToString() => $"MethodEntry: {_method}";

        public override NativeLayoutVertexNode TemplateDictionaryNode(NodeFactory factory)
        {
            return factory.NativeLayout.MethodEntrypointDictionarySlot
                        (_method, unboxing: true, functionPointerTarget: factory.MethodEntrypoint(_method.GetCanonMethodTarget(CanonicalFormKind.Specific)));
        }
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
            Debug.Assert(method.IsVirtual);

            // Normal virtual methods don't need a generic lookup.
            Debug.Assert(method.OwningType.IsInterface || method.HasInstantiation);

            _method = method;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation, GenericDictionaryNode dictionary)
        {
            if (factory.Target.Abi == TargetAbi.CoreRT)
            {
                MethodDesc instantiatedMethod = _method.InstantiateSignature(typeInstantiation, methodInstantiation);
                return factory.ReadyToRunHelper(ReadyToRunHelperId.VirtualCall, instantiatedMethod);
            }
            else
            {
                MethodDesc instantiatedMethod = _method.InstantiateSignature(dictionary.TypeInstantiation, dictionary.MethodInstantiation);
                return factory.InterfaceDispatchCell(instantiatedMethod, dictionary.GetMangledName(factory.NameMangler));
            }
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("VirtualCall_");
            sb.Append(nameMangler.GetMangledMethodName(_method));
        }

        public override string ToString() => $"VirtualCall: {_method}";

        public override NativeLayoutVertexNode TemplateDictionaryNode(NodeFactory factory)
        {
            if (factory.Target.Abi == TargetAbi.CoreRT)
            {
                return factory.NativeLayout.NotSupportedDictionarySlot;
            }
            else
            {
                return factory.NativeLayout.InterfaceCellDictionarySlot(_method);
            }
        }
    }

    /// <summary>
    /// Generic lookup result that points to a virtual function address load stub.
    /// </summary>
    internal sealed class VirtualResolveGenericLookupResult : GenericLookupResult
    {
        private MethodDesc _method;

        public VirtualResolveGenericLookupResult(MethodDesc method)
        {
            Debug.Assert(method.IsRuntimeDeterminedExactMethod);
            Debug.Assert(method.IsVirtual);

            // Normal virtual methods don't need a generic lookup.
            Debug.Assert(method.OwningType.IsInterface || method.HasInstantiation);

            _method = method;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation, GenericDictionaryNode dictionary)
        {
            if (factory.Target.Abi == TargetAbi.CoreRT)
            {
                MethodDesc instantiatedMethod = _method.InstantiateSignature(typeInstantiation, methodInstantiation);

                // https://github.com/dotnet/corert/issues/2342 - we put a pointer to the virtual call helper into the dictionary
                // but this should be something that will let us compute the target of the dipatch (e.g. interface dispatch cell).
                return factory.ReadyToRunHelper(ReadyToRunHelperId.VirtualCall, instantiatedMethod);
            }
            else
            {
                MethodDesc instantiatedMethod = _method.InstantiateSignature(dictionary.TypeInstantiation, dictionary.MethodInstantiation);
                return factory.InterfaceDispatchCell(instantiatedMethod, dictionary.GetMangledName(factory.NameMangler));
            }
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("VirtualResolve_");
            sb.Append(nameMangler.GetMangledMethodName(_method));
        }

        public override string ToString() => $"VirtualResolve: {_method}";

        public override NativeLayoutVertexNode TemplateDictionaryNode(NodeFactory factory)
        {
            if (factory.Target.Abi == TargetAbi.CoreRT)
            {
                return factory.NativeLayout.NotSupportedDictionarySlot;
            }
            else
            {
                return factory.NativeLayout.InterfaceCellDictionarySlot(_method);
            }
        }
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

        public override ISymbolNode GetTarget(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation, GenericDictionaryNode dictionary)
        {
            var instantiatedType = (MetadataType)_type.InstantiateSignature(typeInstantiation, methodInstantiation);
            ISymbolNode target = factory.TypeNonGCStaticsSymbol(instantiatedType);

            // The dictionary entry always points to the beginning of the data.
            if (factory.TypeSystemContext.HasLazyStaticConstructor(instantiatedType))
            {
                return factory.Indirection(target, NonGCStaticsNode.GetClassConstructorContextStorageSize(factory.Target, instantiatedType));
            }
            else
            {
                return factory.Indirection(target);
            }
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
    }

    /// <summary>
    /// Generic lookup result that points to the threadstatic base index of a type.
    /// </summary>
    internal sealed class TypeThreadStaticBaseIndexGenericLookupResult : GenericLookupResult
    {
        private MetadataType _type;

        public TypeThreadStaticBaseIndexGenericLookupResult(TypeDesc type)
        {
            Debug.Assert(type.IsRuntimeDeterminedSubtype, "Concrete static base in a generic dictionary?");
            Debug.Assert(type is MetadataType);
            _type = (MetadataType)type;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation, GenericDictionaryNode dictionary)
        {
            var instantiatedType = (MetadataType)_type.InstantiateSignature(typeInstantiation, methodInstantiation);
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

        public override ISymbolNode GetTarget(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation, GenericDictionaryNode dictionary)
        {
            var instantiatedType = (MetadataType)_type.InstantiateSignature(typeInstantiation, methodInstantiation);
            return factory.Indirection(factory.TypeGCStaticsSymbol(instantiatedType));
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("GCStaticBase_");
            sb.Append(nameMangler.GetMangledTypeName(_type));
        }

        public override string ToString() => $"GCStaticBase: {_type}";

        public override NativeLayoutVertexNode TemplateDictionaryNode(NodeFactory factory)
        {
            if (factory.Target.Abi == TargetAbi.CoreRT)
            {
                return factory.NativeLayout.NotSupportedDictionarySlot;
            }
            else
            {
                return factory.NativeLayout.GcStaticDictionarySlot(_type);
            }
        }

        public override GenericLookupResultReferenceType LookupResultReferenceType(NodeFactory factory)
        {
            return GenericLookupResultReferenceType.Indirect;
        }
    }

    /// <summary>
    /// Generic lookup result that points to an object allocator.
    /// </summary>
    internal sealed class ObjectAllocatorGenericLookupResult : GenericLookupResult
    {
        private TypeDesc _type;

        public ObjectAllocatorGenericLookupResult(TypeDesc type)
        {
            Debug.Assert(type.IsRuntimeDeterminedSubtype, "Concrete type in a generic dictionary?");
            _type = type;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation, GenericDictionaryNode dictionary)
        {
            TypeDesc instantiatedType = _type.InstantiateSignature(typeInstantiation, methodInstantiation);
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
    }

    /// <summary>
    /// Generic lookup result that points to an array allocator.
    /// </summary>
    internal sealed class ArrayAllocatorGenericLookupResult : GenericLookupResult
    {
        private TypeDesc _type;

        public ArrayAllocatorGenericLookupResult(TypeDesc type)
        {
            Debug.Assert(type.IsRuntimeDeterminedSubtype, "Concrete type in a generic dictionary?");
            _type = type;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation, GenericDictionaryNode dictionary)
        {
            TypeDesc instantiatedType = _type.InstantiateSignature(typeInstantiation, methodInstantiation);
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
    }

    internal sealed class ThreadStaticIndexLookupResult : GenericLookupResult
    {
        private TypeDesc _type;

        public ThreadStaticIndexLookupResult(TypeDesc type)
        {
            Debug.Assert(type.IsRuntimeDeterminedSubtype, "Concrete type in a generic dictionary?");
            _type = type;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation, GenericDictionaryNode dictionary)
        {
            UtcNodeFactory utcNodeFactory = factory as UtcNodeFactory;
            Debug.Assert(utcNodeFactory != null);
            TypeDesc instantiatedType = _type.InstantiateSignature(typeInstantiation, methodInstantiation);
            return factory.Indirection(utcNodeFactory.TypeThreadStaticsIndexSymbol(instantiatedType));
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
            return GenericLookupResultReferenceType.Indirect;
        }
    }

    internal sealed class ThreadStaticOffsetLookupResult : GenericLookupResult
    {
        private TypeDesc _type;

        public ThreadStaticOffsetLookupResult(TypeDesc type)
        {
            Debug.Assert(type.IsRuntimeDeterminedSubtype, "Concrete type in a generic dictionary?");
            _type = type;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation, GenericDictionaryNode dictionary)
        {
            UtcNodeFactory utcNodeFactory = factory as UtcNodeFactory;
            Debug.Assert(utcNodeFactory != null);
            TypeDesc instantiatedType = _type.InstantiateSignature(typeInstantiation, methodInstantiation);
            Debug.Assert(instantiatedType is MetadataType);
            return factory.Indirection(utcNodeFactory.TypeThreadStaticsOffsetSymbol(instantiatedType as MetadataType));
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("TlsOffset_");
            sb.Append(nameMangler.GetMangledTypeName(_type));
        }

        public override string ToString() => $"ThreadStaticOffset: {_type}";

        public override NativeLayoutVertexNode TemplateDictionaryNode(NodeFactory factory)
        {
            return factory.NativeLayout.TlsOffsetDictionarySlot(_type);
        }

        public override GenericLookupResultReferenceType LookupResultReferenceType(NodeFactory factory)
        {
            return GenericLookupResultReferenceType.Indirect;
        }
    }

    internal sealed class DefaultConstructorLookupResult : GenericLookupResult
    {
        private TypeDesc _type;

        public DefaultConstructorLookupResult(TypeDesc type)
        {
            Debug.Assert(type.IsRuntimeDeterminedSubtype, "Concrete type in a generic dictionary?");
            _type = type;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation, GenericDictionaryNode dictionary)
        {
            TypeDesc instantiatedType = _type.InstantiateSignature(typeInstantiation, methodInstantiation);
            MethodDesc defaultCtor = instantiatedType.GetDefaultConstructor();

            if (defaultCtor == null)
            {
                // If there isn't a default constructor, use the fallback one.
                MetadataType missingCtorType = factory.TypeSystemContext.SystemModule.GetKnownType("System", "Activator");
                missingCtorType = missingCtorType.GetNestedType("ClassWithMissingConstructor");                
                Debug.Assert(missingCtorType != null);
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
    }

    internal sealed class CallingConventionConverterLookupResult : GenericLookupResult
    {
        private CallingConventionConverterKey _callingConventionConverter;

        public CallingConventionConverterLookupResult(CallingConventionConverterKey callingConventionConverter)
        {
            _callingConventionConverter = callingConventionConverter;
            Debug.Assert(Internal.Runtime.UniversalGenericParameterLayout.MethodSignatureHasVarsNeedingCallingConventionConverter(callingConventionConverter.Signature));
        }

        public override ISymbolNode GetTarget(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation, GenericDictionaryNode dictionary)
        {
            Debug.Assert(false, "GetTarget for a CallingConventionConverterLookupResult doesn't make sense. It isn't a pointer being emitted");
            return null;
        }

        public override void EmitDictionaryEntry(ref ObjectDataBuilder builder, NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation, GenericDictionaryNode dictionary)
        {
            Debug.Assert(false, "CallingConventionConverterLookupResult contents should only be generated into generic dictionaries at runtime");
            builder.EmitNaturalInt(0);
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("CallingConventionConverterLookupResult_");
            sb.Append(_callingConventionConverter.GetName(nameMangler));
        }

        public override string ToString() => "CallingConventionConverterLookupResult";

        public override NativeLayoutVertexNode TemplateDictionaryNode(NodeFactory factory)
        {
            return factory.NativeLayout.CallingConventionConverter(_callingConventionConverter);
        }
    }
}
