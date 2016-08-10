// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ILCompiler.DependencyAnalysisFramework;
using Internal.IL;
using Internal.Runtime;
using Internal.TypeSystem;
using System;
using System.Collections.Generic;

using Debug = System.Diagnostics.Debug;
using GenericVariance = Internal.Runtime.GenericVariance;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Given a type, EETypeNode writes an EEType data structure in the format expected by the runtime.
    /// 
    /// Format of an EEType:
    /// 
    /// Field Size      | Contents
    /// ----------------+-----------------------------------
    /// UInt16          | Component Size. For arrays this is the element type size, for strings it is 2 (.NET uses 
    ///                 | UTF16 character encoding), for generic type definitions it is the number of generic parameters,
    ///                 | and 0 for all other types.
    ///                 |
    /// UInt16          | EETypeKind (Normal, Array, Pointer type). Flags for: IsValueType, IsCrossModule, HasPointers,
    ///                 | HasOptionalFields, IsInterface, IsGeneric. Top 5 bits are used for enum CorElementType to
    ///                 | record whether it's back by an Int32, Int16 etc
    ///                 |
    /// Uint32          | Base size.
    ///                 |
    /// [Pointer Size]  | Related type. Base type for regular types. Element type for arrays / pointer types.
    ///                 |
    /// UInt16          | Number of VTable slots (X)
    ///                 |
    /// UInt16          | Number of interfaces implemented by type (Y)
    ///                 |
    /// UInt32          | Hash code
    ///                 |
    /// [Pointer Size]  | Pointer to containing Module indirection cell
    ///                 |
    /// X * [Ptr Size]  | VTable entries (optional)
    ///                 |
    /// Y * [Ptr Size]  | Pointers to interface map data structures (optional)
    ///                 |
    /// [Pointer Size]  | Pointer to finalizer method (optional)
    ///                 |
    /// [Pointer Size]  | Pointer to optional fields (optional)
    ///                 |
    /// [Pointer Size]  | Pointer to the generic type argument of a Nullable&lt;T&gt; (optional)
    ///                 |
    /// [Pointer Size]  | Pointer to the generic type definition EEType (optional)
    ///                 |
    /// [Pointer Size]  | Pointer to the generic argument and variance info (optional)
    /// </summary>
    internal partial class EETypeNode : ObjectNode, ISymbolNode, IEETypeNode
    {
        protected TypeDesc _type;
        protected EETypeOptionalFieldsBuilder _optionalFieldsBuilder = new EETypeOptionalFieldsBuilder();

        public EETypeNode(TypeDesc type)
        {
            _type = type;
        }

        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }

        public override bool ShouldSkipEmittingObjectNode(NodeFactory factory)
        {
            // If there is a constructed version of this node in the graph, emit that instead
            return ((DependencyNode)factory.ConstructedTypeSymbol(_type)).Marked;
        }

        public TypeDesc Type
        {
            get { return _type; }
        }
        
        public override ObjectNodeSection Section
        {
            get
            {
                if (_type.Context.Target.IsWindows)
                    return ObjectNodeSection.ReadOnlyDataSection;
                else
                    return ObjectNodeSection.DataSection;
            }
        }

        public override bool ShouldShareNodeAcrossModules(NodeFactory factory)
        {
            return factory.CompilationModuleGroup.ShouldShareAcrossModules(_type);
        }

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return true;
            }
        }

        public void SetDispatchMapIndex(int index)
        {
            _optionalFieldsBuilder.SetFieldValue(EETypeOptionalFieldsElement.DispatchMap, checked((uint)index));
        }

        int ISymbolNode.Offset
        {
            get
            {
                return GCDescSize;
            }
        }

        string ISymbolNode.MangledName
        {
            get
            {
                return "__EEType_" + NodeFactory.NameMangler.GetMangledTypeName(_type);
            }
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly)
        {
            ObjectDataBuilder objData = new ObjectDataBuilder(factory);
            objData.Alignment = 16;
            objData.DefinedSymbols.Add(this);

            ComputeOptionalEETypeFields(factory);

            OutputGCDesc(ref objData);
            OutputComponentSize(ref objData);
            OutputFlags(factory, ref objData);
            OutputBaseSize(ref objData);
            OutputRelatedType(factory, ref objData);

            // Avoid consulting VTable slots until they're guaranteed complete during final data emission
            if (!relocsOnly)
            {
                OutputVirtualSlotAndInterfaceCount(factory, ref objData);
            }

            objData.EmitInt(_type.GetHashCode());
            objData.EmitPointerReloc(factory.ModuleManagerIndirection);
            
            // Avoid consulting VTable slots until they're guaranteed complete during final data emission
            if (!relocsOnly)
            {
                OutputVirtualSlots(factory, ref objData, _type, _type);
            }

            OutputInterfaceMap(factory, ref objData);
            OutputFinalizerMethod(factory, ref objData);
            OutputOptionalFields(factory, ref objData);
            OutputNullableTypeParameter(factory, ref objData);
            OutputGenericInstantiationDetails(factory, ref objData);
            
            return objData.ToObjectData();
        }

        /// <summary>
        /// Returns the offset within an EEType of the beginning of VTable entries
        /// </summary>
        /// <param name="pointerSize">The size of a pointer in bytes in the target architecture</param>
        public static int GetVTableOffset(int pointerSize)
        {
            return 16 + 2 * pointerSize;
        }

        protected virtual int GCDescSize => 0;
        
        protected virtual void OutputGCDesc(ref ObjectDataBuilder builder)
        {
            // Non-constructed EETypeNodes get no GC Desc
            Debug.Assert(GCDescSize == 0);
        }
        
        private void OutputComponentSize(ref ObjectDataBuilder objData)
        {
            if (_type.IsArray)
            {
                int elementSize = ((ArrayType)_type).ElementType.GetElementSize();
                if (elementSize >= 64 * 1024)
                {
                    // TODO: Array of type 'X' cannot be created because base value type is too large.
                    throw new TypeLoadException();
                }

                objData.EmitShort((short)elementSize);
            }
            else if (_type.IsString)
            {
                objData.EmitShort(2);
            }
            else
            {
                objData.EmitShort(0);
            }
        }

        private void OutputFlags(NodeFactory factory, ref ObjectDataBuilder objData)
        {
            UInt16 flags = EETypeBuilderHelpers.ComputeFlags(_type);

            if (_type.GetTypeDefinition() == factory.ArrayOfTEnumeratorType)
            {
                // Generic array enumerators use special variance rules recognized by the runtime
                flags |= (UInt16)EETypeFlags.GenericVarianceFlag;
            }

            // Todo: RelatedTypeViaIATFlag when we support cross-module EETypes
            // Todo: Generic Type Definition EETypes

            if (_optionalFieldsBuilder.IsAtLeastOneFieldUsed())
            {
                flags |= (UInt16)EETypeFlags.OptionalFieldsFlag;
            }

            objData.EmitShort((short)flags);
        }

        private void OutputBaseSize(ref ObjectDataBuilder objData)
        {
            int pointerSize = _type.Context.Target.PointerSize;
            int minimumObjectSize = pointerSize * 3;
            int objectSize;

            if (_type.IsDefType)
            {
                objectSize = pointerSize +
                    ((DefType)_type).InstanceByteCount; // +pointerSize for SyncBlock

                if (_type.IsValueType)
                    objectSize += pointerSize; // + EETypePtr field inherited from System.Object
            }
            else if (_type.IsArray)
            {
                objectSize = 3 * pointerSize; // SyncBlock + EETypePtr + Length
                if (!_type.IsSzArray)
                    objectSize +=
                        2 * _type.Context.GetWellKnownType(WellKnownType.Int32).GetElementSize() * ((ArrayType)_type).Rank;
            }
            else if (_type.IsPointer)
            {
                // Object size 0 is a sentinel value in the runtime for parameterized types that means "Pointer Type"
                objData.EmitInt(0);
                return;
            }
            else
                throw new NotImplementedException();

            objectSize = AlignmentHelper.AlignUp(objectSize, pointerSize);
            objectSize = Math.Max(minimumObjectSize, objectSize);

            if (_type.IsString)
            {
                // If this is a string, throw away objectSize we computed so far. Strings are special.
                // SyncBlock + EETypePtr + length + firstChar
                objectSize = 2 * pointerSize +
                    _type.Context.GetWellKnownType(WellKnownType.Int32).GetElementSize() +
                    _type.Context.GetWellKnownType(WellKnownType.Char).GetElementSize();
            }

            objData.EmitInt(objectSize);
        }

        protected virtual ISymbolNode GetBaseTypeNode(NodeFactory factory)
        {
            return _type.BaseType != null ? factory.NecessaryTypeSymbol(_type.BaseType) : null;
        }

        private void OutputRelatedType(NodeFactory factory, ref ObjectDataBuilder objData)
        {
            ISymbolNode relatedTypeNode = null;

            if (_type.IsArray || _type.IsPointer)
            {
                var parameterType = ((ParameterizedType)_type).ParameterType;
                relatedTypeNode = factory.NecessaryTypeSymbol(parameterType);
            }
            else
            {
                TypeDesc baseType = _type.BaseType;
                if (baseType != null)
                {
                    relatedTypeNode = GetBaseTypeNode(factory);
                }
            }

            if (relatedTypeNode != null)
            {
                objData.EmitPointerReloc(relatedTypeNode);
            }
            else
            {
                objData.EmitZeroPointer();
            }
        }

        protected virtual void OutputVirtualSlotAndInterfaceCount(NodeFactory factory, ref ObjectDataBuilder objData)
        {
            objData.EmitShort(0);
            objData.EmitShort(0);
        }

        protected virtual void OutputVirtualSlots(NodeFactory factory, ref ObjectDataBuilder objData, TypeDesc implType, TypeDesc declType)
        {
            // Non-constructed EETypes have no VTable
        }
        
        protected virtual void OutputInterfaceMap(NodeFactory factory, ref ObjectDataBuilder objData)
        {
            // Non-constructed EETypes have no interface map
        }

        private void OutputFinalizerMethod(NodeFactory factory, ref ObjectDataBuilder objData)
        {
            MethodDesc finalizerMethod = _type.GetFinalizer();

            if (finalizerMethod != null)
            {
                objData.EmitPointerReloc(factory.MethodEntrypoint(finalizerMethod));
            }
        }

        private void OutputOptionalFields(NodeFactory factory, ref ObjectDataBuilder objData)
        {
            if(_optionalFieldsBuilder.IsAtLeastOneFieldUsed())
            {
                objData.EmitPointerReloc(factory.EETypeOptionalFields(_optionalFieldsBuilder));
            }
        }

        private void OutputNullableTypeParameter(NodeFactory factory, ref ObjectDataBuilder objData)
        {
            if (_type.IsNullable)
            {
                objData.EmitPointerReloc(factory.NecessaryTypeSymbol(_type.Instantiation[0]));
            }
        }

        private void OutputGenericInstantiationDetails(NodeFactory factory, ref ObjectDataBuilder objData)
        {
            if (_type.HasInstantiation && !_type.IsTypeDefinition)
            {
                objData.EmitPointerReloc(factory.NecessaryTypeSymbol(_type.GetTypeDefinition()));

                GenericCompositionDetails details;
                if (_type.GetTypeDefinition() == factory.ArrayOfTEnumeratorType)
                {
                    // Generic array enumerators use special variance rules recognized by the runtime
                    details = new GenericCompositionDetails(_type.Instantiation, new[] { GenericVariance.ArrayCovariant });
                }
                else
                    details = new GenericCompositionDetails(_type);

                objData.EmitPointerReloc(factory.GenericComposition(details));
            }
        }

        /// <summary>
        /// Populate the OptionalFieldsRuntimeBuilder if any optional fields are required.
        /// </summary>
        private void ComputeOptionalEETypeFields(NodeFactory factory)
        {
            ComputeRareFlags(factory);
            ComputeNullableValueOffset();
            ComputeICastableVirtualMethodSlots(factory);
            ComputeValueTypeFieldPadding();
        }

        void ComputeRareFlags(NodeFactory factory)
        {
            uint flags = 0;

            if (_type.IsNullable)
            {
                flags |= (uint)EETypeRareFlags.IsNullableFlag;
            }

            if (factory.TypeSystemContext.HasLazyStaticConstructor(_type))
            {
                flags |= (uint)EETypeRareFlags.HasCctorFlag;
            }

            if (EETypeBuilderHelpers.ComputeRequiresAlign8(_type))
            {
                flags |= (uint)EETypeRareFlags.RequiresAlign8Flag;
            }

            if (_type.IsDefType && ((DefType)_type).IsHfa)
            {
                flags |= (uint)EETypeRareFlags.IsHFAFlag;
            }

            if (flags != 0)
            {
                _optionalFieldsBuilder.SetFieldValue(EETypeOptionalFieldsElement.RareFlags, flags);
            }
        }

        /// <summary>
        /// To support boxing / unboxing, the offset of the value field of a Nullable type is recorded on the EEType.
        /// This is variable according to the alignment requirements of the Nullable&lt;T&gt; type parameter.
        /// </summary>
        void ComputeNullableValueOffset()
        {
            if (!_type.IsNullable)
                return;

            var field = _type.GetKnownField("value");

            // In the definition of Nullable<T>, the first field should be the boolean representing "hasValue"
            Debug.Assert(field.Offset > 0);

            // The contract with the runtime states the Nullable value offset is stored with the boolean "hasValue" size subtracted
            // to get a small encoding size win.
            _optionalFieldsBuilder.SetFieldValue(EETypeOptionalFieldsElement.NullableValueOffset, (uint)field.Offset - 1);
        }

        /// <summary>
        /// ICastable is a special interface whose two methods are not invoked using regular interface dispatch.
        /// Instead, their VTable slots are recorded on the EEType of an object implementing ICastable and are
        /// called directly.
        /// </summary>
        void ComputeICastableVirtualMethodSlots(NodeFactory factory)
        {
            // TODO: This method is untested (we don't support interfaces yet)
            if (_type.IsInterface)
                return;

            foreach (DefType itf in _type.RuntimeInterfaces)
            {
                if (itf == factory.ICastableInterface)
                {
                    var isInstMethod = itf.GetKnownMethod("IsInstanceOfInterface", null);
                    var getImplTypeMethod = itf.GetKnownMethod("GetImplType", null);

                    int isInstMethodSlot = VirtualMethodSlotHelper.GetVirtualMethodSlot(factory, isInstMethod);
                    int getImplTypeMethodSlot = VirtualMethodSlotHelper.GetVirtualMethodSlot(factory, getImplTypeMethod);

                    if (isInstMethodSlot != -1 || getImplTypeMethodSlot != -1)
                    {
                        var rareFlags = _optionalFieldsBuilder.GetFieldValue(EETypeOptionalFieldsElement.RareFlags, 0);
                        rareFlags |= (uint)EETypeRareFlags.ICastableFlag;
                        _optionalFieldsBuilder.SetFieldValue(EETypeOptionalFieldsElement.RareFlags, rareFlags);
                    }

                    if (isInstMethodSlot != -1)
                    {
                        _optionalFieldsBuilder.SetFieldValue(EETypeOptionalFieldsElement.ICastableIsInstSlot, (uint)isInstMethodSlot);
                    }
                    if (getImplTypeMethodSlot != -1)
                    {
                        _optionalFieldsBuilder.SetFieldValue(EETypeOptionalFieldsElement.ICastableGetImplTypeSlot, (uint)getImplTypeMethodSlot);
                    }
                }
            }
        }

        void ComputeValueTypeFieldPadding()
        {
            if (!_type.IsValueType)
                return;

            DefType defType = _type as DefType;
            Debug.Assert(defType != null);

            uint valueTypeFieldPadding = checked((uint)(defType.InstanceByteCount - defType.InstanceByteCountUnaligned));
            uint valueTypeFieldPaddingEncoded = EETypeBuilderHelpers.ComputeValueTypeFieldPaddingFieldValue(valueTypeFieldPadding, (uint)defType.InstanceFieldAlignment);

            if (valueTypeFieldPaddingEncoded != 0)
            {
                _optionalFieldsBuilder.SetFieldValue(EETypeOptionalFieldsElement.ValueTypeFieldPadding, valueTypeFieldPaddingEncoded);
            }
        }

        protected override void OnMarked(NodeFactory context)
        {
            //Debug.Assert(_type.IsTypeDefinition || !_type.HasSameTypeDefinition(context.ArrayOfTClass), "Asking for Array<T> EEType");
        }
    }
}
