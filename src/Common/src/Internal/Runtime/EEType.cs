// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

using Internal.NativeFormat;

using Debug = System.Diagnostics.Debug;

namespace Internal.Runtime
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ObjHeader
    {
        // Contents of the object header
        IntPtr objHeaderContents;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct EEInterfaceInfo
    {
        [StructLayout(LayoutKind.Explicit)]
        private unsafe struct InterfaceTypeUnion
        {
            [FieldOffset(0)]
            public EEType* _pInterfaceEEType;
            [FieldOffset(0)]
            public EEType** _ppInterfaceEETypeViaIAT;
        }

        private InterfaceTypeUnion _interfaceType;

        internal EEType* InterfaceType
        {
            get
            {
                if ((unchecked((uint)_interfaceType._pInterfaceEEType) & 1u) != 0)
                {
#if BIT64
                    EEType** ppInterfaceEETypeViaIAT = (EEType**)(((ulong)_interfaceType._ppInterfaceEETypeViaIAT) & ~1ul);
#else
                    EEType** ppInterfaceEETypeViaIAT = (EEType**)(((uint)_interfaceType._ppInterfaceEETypeViaIAT) & ~1u);
#endif
                    return *ppInterfaceEETypeViaIAT;
                }

                return _interfaceType._pInterfaceEEType;
            }
#if TYPE_LOADER_IMPLEMENTATION
            set
            {
                _interfaceType._pInterfaceEEType = value;
            }
#endif
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct DispatchMap
    {
        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct DispatchMapEntry
        {
            internal UInt16 _usInterfaceIndex;
            internal UInt16 _usInterfaceMethodSlot;
            internal UInt16 _usImplMethodSlot;
        }

        UInt32 _entryCount;
        DispatchMapEntry _dispatchMap; // at least one entry if any interfaces defined

        public bool IsEmpty
        {
            get
            {
                return _entryCount == 0;
            }
        }

        public UInt32 NumEntries
        {
            get
            {
                return _entryCount;
            }
#if TYPE_LOADER_IMPLEMENTATION
            set
            {
                _entryCount = value;
            }
#endif
        }

        public int Size
        {
            get
            {
                return sizeof(UInt32) + sizeof(DispatchMapEntry) * (int)_entryCount;
            }
        }

        public DispatchMapEntry* this[int index]
        {
            get
            {
                fixed (DispatchMap* pThis = &this)
                    return (DispatchMapEntry*)((byte*)pThis + sizeof(UInt32) + (sizeof(DispatchMapEntry) * index));
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe partial struct EEType
    {
        [StructLayout(LayoutKind.Explicit)]
        private unsafe struct RelatedTypeUnion
        {
            // Kinds.CanonicalEEType
            [FieldOffset(0)]
            public EEType* _pBaseType;
            [FieldOffset(0)]
            public EEType** _ppBaseTypeViaIAT;

            // Kinds.ClonedEEType
            [FieldOffset(0)]
            public EEType** _ppCanonicalTypeViaIAT;

            // Kinds.ArrayEEType
            [FieldOffset(0)]
            public EEType* _pRelatedParameterType;
            [FieldOffset(0)]
            public EEType** _ppRelatedParameterTypeViaIAT;
        }

        unsafe static class OptionalFieldsReader
        {
            internal static UInt32 GetInlineField(byte* pFields, EETypeOptionalFieldTag eTag, UInt32 uiDefaultValue)
            {
                if (pFields == null)
                    return uiDefaultValue;

                bool isLastField = false;
                while (!isLastField)
                {
                    byte fieldHeader = NativePrimitiveDecoder.ReadUInt8(ref pFields);
                    isLastField = (fieldHeader & 0x80) != 0;
                    EETypeOptionalFieldTag eCurrentTag = (EETypeOptionalFieldTag)(fieldHeader & 0x7f);
                    UInt32 uiCurrentValue = NativePrimitiveDecoder.DecodeUnsigned(ref pFields);

                    // If we found a tag match return the current value.
                    if (eCurrentTag == eTag)
                        return uiCurrentValue;
                }

                // Reached end of stream without getting a match. Field is not present so return default value.
                return uiDefaultValue;
            }
        }

        private UInt16 _usComponentSize;
        private UInt16 _usFlags;
        private UInt32 _uBaseSize;
        private RelatedTypeUnion _relatedType;
        private UInt16 _usNumVtableSlots;
        private UInt16 _usNumInterfaces;
        private UInt32 _uHashCode;

#if CORERT
        private IntPtr _ppModuleManager;
#endif

        // vtable follows

    // These masks and paddings have been chosen so that the ValueTypePadding field can always fit in a byte of data.
    // if the alignment is 8 bytes or less. If the alignment is higher then there may be a need for more bits to hold
    // the rest of the padding data.
    // If paddings of greater than 7 bytes are necessary, then the high bits of the field represent that padding
        private const UInt32 ValueTypePaddingLowMask = 0x7;
        private const UInt32 ValueTypePaddingHighMask = 0xFFFFFF00;
        private const UInt32 ValueTypePaddingMax = 0x07FFFFFF;
        private const int ValueTypePaddingHighShift = 8;
        private const UInt32 ValueTypePaddingAlignmentMask = 0xF8;
        private const int ValueTypePaddingAlignmentShift = 3;

        internal UInt16 ComponentSize
        {
            get
            {
                return _usComponentSize;
            }
#if TYPE_LOADER_IMPLEMENTATION
            set
            {
                _usComponentSize = value;
            }
#endif
        }

        internal UInt16 GenericArgumentCount
        {
            get
            {
                Debug.Assert(IsGenericTypeDefinition);
                return _usComponentSize;
            }
#if TYPE_LOADER_IMPLEMENTATION
            set
            {
                Debug.Assert(IsGenericTypeDefinition);
                _usComponentSize = value;
            }
#endif
        }

        internal UInt16 Flags
        {
            get
            {
                return _usFlags;
            }
#if TYPE_LOADER_IMPLEMENTATION
            set
            {
                _usFlags = value;
            }
#endif
        }

        internal UInt32 BaseSize
        {
            get
            {
                return _uBaseSize;
            }
#if TYPE_LOADER_IMPLEMENTATION
            set
            {
                _uBaseSize = value;
            }
#endif
        }

        internal UInt16 NumVtableSlots
        {
            get
            {
                return _usNumVtableSlots;
            }
#if TYPE_LOADER_IMPLEMENTATION
            set
            {
                _usNumVtableSlots = value;
            }
#endif
        }

        internal UInt16 NumInterfaces
        {
            get
            {
                return _usNumInterfaces;
            }
#if TYPE_LOADER_IMPLEMENTATION
            set
            {
                _usNumInterfaces = value;
            }
#endif
        }

        internal UInt32 HashCode
        {
            get
            {
                return _uHashCode;
            }
#if TYPE_LOADER_IMPLEMENTATION
            set
            {
                _uHashCode = value;
            }
#endif
        }

        private EETypeKind Kind
        {
            get
            {
                return (EETypeKind)(_usFlags & (UInt16)EETypeFlags.EETypeKindMask);
            }
        }

        internal bool HasOptionalFields
        {
            get
            {
                return ((_usFlags & (UInt16)EETypeFlags.OptionalFieldsFlag) != 0);
            }
        }

        // Mark or determine that a type is generic and one or more of it's type parameters is co- or
        // contra-variant. This only applies to interface and delegate types.
        internal bool HasGenericVariance
        {
            get
            {
                return ((_usFlags & (UInt16)EETypeFlags.GenericVarianceFlag) != 0);
            }
        }

        internal bool IsFinalizable
        {
            get
            {
                return ((_usFlags & (ushort)EETypeFlags.HasFinalizerFlag) != 0);
            }
        }

        internal bool IsNullable
        {
            get
            {
                return (RareFlags & EETypeRareFlags.IsNullableFlag) != 0;
            }
        }

        internal bool IsCloned
        {
            get
            {
                return Kind == EETypeKind.ClonedEEType;
            }
        }

        internal bool IsCanonical
        {
            get
            {
                return Kind == EETypeKind.CanonicalEEType;
            }
        }

        internal bool IsArray
        {
            get
            {
                return IsParameterizedType && ParameterizedTypeShape != 0;
            }
        }
        
        internal bool IsGeneric
        {
            get
            {
                return ((_usFlags & (UInt16)EETypeFlags.IsGenericFlag) != 0);
            }
        }

        internal bool IsGenericTypeDefinition
        {
            get
            {
                return Kind == EETypeKind.GenericTypeDefEEType;
            }
        }

#if CORERT
        internal EEType* GenericDefinition
        {
            get
            {
                Debug.Assert(IsGeneric);
                UInt32 cbOffset = GetFieldOffset(EETypeField.ETF_GenericDefinition);
                fixed (EEType* pThis = &this)
                {
                    return *(EEType**)((byte*)pThis + cbOffset);
                }
            }
        }

        internal uint GenericArity
        {
            get
            {
                Debug.Assert(IsGeneric);
                UInt32 cbOffset = GetFieldOffset(EETypeField.ETF_GenericComposition);
                fixed (EEType* pThis = &this)
                {
                    // Number of generic arguments is the first DWORD of the composition stream.
                    return **(UInt32**)((byte*)pThis + cbOffset);
                }
            }
        }

        internal EEType** GenericArguments
        {
            get
            {
                Debug.Assert(IsGeneric);
                UInt32 cbOffset = GetFieldOffset(EETypeField.ETF_GenericComposition);
                fixed (EEType* pThis = &this)
                {
                    // Generic arguments follow after a (padded) DWORD specifying their count
                    // in the generic composition stream.
                    return ((*(EEType***)((byte*)pThis + cbOffset)) + 1);
                }
            }
        }

        internal GenericVariance* GenericVariance
        {
            get
            {
                Debug.Assert(IsGeneric);
                Debug.Assert(HasGenericVariance);
                UInt32 cbOffset = GetFieldOffset(EETypeField.ETF_GenericComposition);
                fixed (EEType* pThis = &this)
                {
                    // Variance info follows immediatelly after the generic arguments
                    return (GenericVariance*)(GenericArguments + GenericArity);
                }
            }
        }
#endif // CORERT

        internal bool IsPointerType
        {
            get
            {
                return IsParameterizedType && ParameterizedTypeShape == 0;
            }
        }

        internal bool IsInterface
        {
            get
            {
                return ((_usFlags & (UInt16)EETypeFlags.IsInterfaceFlag) != 0);
            }
        }

        internal bool IsDynamicType
        {
            get
            {
                return (RareFlags & EETypeRareFlags.IsDynamicTypeFlag) != 0;
            }
        }

        internal bool HasDynamicallyAllocatedDispatchMap
        {
            get
            {
                return (RareFlags & EETypeRareFlags.HasDynamicallyAllocatedDispatchMapFlag) != 0;
            }
        }

        internal bool IsNullableTypeViaIAT
        {
            get
            {
                return (RareFlags & EETypeRareFlags.NullableTypeViaIATFlag) != 0;
            }
        }

        internal bool IsParameterizedType
        {
            get
            {
                return Kind == EETypeKind.ParameterizedEEType;
            }
        }

        // The parameterized type shape defines the particular form of parameterized type that
        // is being represented.
        // Currently, the meaning is a shape of 0 indicates that this is a Pointer
        // and non-zero indicates that this is an array.
        // Two types are not equivalent if their shapes do not exactly match.
        internal UInt32 ParameterizedTypeShape
        {
            get
            {
                return _uBaseSize;
            }
        }

        internal bool IsRelatedTypeViaIAT
        {
            get
            {
                return ((_usFlags & (UInt16)EETypeFlags.RelatedTypeViaIATFlag) != 0);
            }
        }

        internal bool RequiresAlign8
        {
            get
            {
                return (RareFlags & EETypeRareFlags.RequiresAlign8Flag) != 0;
            }
        }

        internal bool IsICastable
        {
            get
            {
                return (RareFlags & EETypeRareFlags.ICastableFlag) != 0;
            }
        }

        /// <summary>
        /// Gets the pointer to the method that implements ICastable.IsInstanceOfInterface.
        /// </summary>
        internal IntPtr ICastableIsInstanceOfInterfaceMethod
        {
            get
            {
                Debug.Assert(IsICastable);

                byte* optionalFields = OptionalFieldsPtr;
                Debug.Assert(optionalFields != null);

                const UInt16 NoSlot = 0xFFFF;
                UInt16 uiSlot = (UInt16)OptionalFieldsReader.GetInlineField(optionalFields, EETypeOptionalFieldTag.OFT_ICastableIsInstSlot, NoSlot);
                if (uiSlot != NoSlot)
                {
                    if (uiSlot < NumVtableSlots)
                        return GetVTableStartAddress()[uiSlot];
                    else
                        return GetSealedVirtualSlot((UInt16)(uiSlot - NumVtableSlots));
                }

                EEType* baseType = BaseType;
                if (baseType != null)
                    return baseType->ICastableIsInstanceOfInterfaceMethod;

                Debug.Assert(false);
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// Gets the pointer to the method that implements ICastable.GetImplType.
        /// </summary>
        internal IntPtr ICastableGetImplTypeMethod
        {
            get
            {
                Debug.Assert(IsICastable);

                byte* optionalFields = OptionalFieldsPtr;
                Debug.Assert(optionalFields != null);

                const UInt16 NoSlot = 0xFFFF;
                UInt16 uiSlot = (UInt16)OptionalFieldsReader.GetInlineField(optionalFields, EETypeOptionalFieldTag.OFT_ICastableGetImplTypeSlot, NoSlot);
                if (uiSlot != NoSlot)
                {
                    if (uiSlot < NumVtableSlots)
                        return GetVTableStartAddress()[uiSlot];
                    else
                        return GetSealedVirtualSlot((UInt16)(uiSlot - NumVtableSlots));
                }

                EEType* baseType = BaseType;
                if (baseType != null)
                    return baseType->ICastableGetImplTypeMethod;

                Debug.Assert(false);
                return IntPtr.Zero;
            }
        }

        internal bool IsValueType
        {
            get
            {
                return ((_usFlags & (UInt16)EETypeFlags.ValueTypeFlag) != 0);
            }
        }

        internal bool HasGCPointers
        {
            get
            {
                return ((_usFlags & (UInt16)EETypeFlags.HasPointersFlag) != 0);
            }
#if TYPE_LOADER_IMPLEMENTATION
            set
            {
                if (value)
                {
                    _usFlags |= (UInt16)EETypeFlags.HasPointersFlag;
                }
                else
                {
                    _usFlags &= (UInt16)~EETypeFlags.HasPointersFlag;
                }
            }
#endif
        }

        internal bool IsHFA
        {
            get
            {
                return (RareFlags & EETypeRareFlags.IsHFAFlag) != 0;
            }
        }

        internal UInt32 ValueTypeFieldPadding 
        { 
            get 
            {
                byte* optionalFields = OptionalFieldsPtr;

                // If there are no optional fields then the padding must have been the default, 0.
                if (optionalFields == null)
                    return 0;

                // Get the value from the optional fields. The default is zero if that particular field was not included.
                // The low bits of this field is the ValueType field padding, the rest of the byte is the alignment if present
                UInt32 ValueTypeFieldPaddingData = OptionalFieldsReader.GetInlineField(optionalFields, EETypeOptionalFieldTag.OFT_ValueTypeFieldPadding, 0);
                UInt32 padding = ValueTypeFieldPaddingData & ValueTypePaddingLowMask;
                // If there is additional padding, the other bits have that data
                padding |= (ValueTypeFieldPaddingData & ValueTypePaddingHighMask) >> (ValueTypePaddingHighShift - ValueTypePaddingAlignmentShift);
                return padding;
            }
        }

        internal UInt32 ValueTypeSize 
        { 
            get 
            { 
                Debug.Assert(IsValueType); 
                // get_BaseSize returns the GC size including space for the sync block index field, the EEType* and
                // padding for GC heap alignment. Must subtract all of these to get the size used for locals, array
                // elements or fields of another type.
                return BaseSize - ((uint)sizeof(ObjHeader) + (uint)sizeof(EEType*) + ValueTypeFieldPadding);
            }
        }

        // Mark or determine that a type instance was allocated at runtime (currently only used for unification of
        // generic instantiations). This is sometimes important for memory management or debugging purposes.
        internal bool IsRuntimeAllocated
        {
            get
            {
                return ((_usFlags & (UInt16)EETypeFlags.RuntimeAllocatedFlag) != 0);
            }
        }

        internal EEInterfaceInfo* InterfaceMap
        {
            get
            {
                fixed (EEType* start = &this)
                {
                    // interface info table starts after the vtable and has _usNumInterfaces entries
                    return (EEInterfaceInfo*)((byte*)start + sizeof(EEType) + sizeof(void*) * _usNumVtableSlots);
                }
            }
        }

        internal bool HasDispatchMap
        {
            get
            {
                if (NumInterfaces == 0)
                    return false;
                byte* optionalFields = OptionalFieldsPtr;
                if (optionalFields == null)
                    return false;
                UInt32 idxDispatchMap = OptionalFieldsReader.GetInlineField(optionalFields, EETypeOptionalFieldTag.OFT_DispatchMap, 0xffffffff);
                if (idxDispatchMap == 0xffffffff)
                {
                    if (HasDynamicallyAllocatedDispatchMap)
                        return true;
                    else if(IsDynamicType)
                        return DynamicTemplateType->HasDispatchMap;
                    return false;
                }
                return true;
            }
        }

        // Get the address of the finalizer method for finalizable types.
        internal IntPtr FinalizerCode
        {
            get
            {
                Debug.Assert(IsFinalizable);

                // Finalizer code address is stored after the vtable and interface map.
                fixed (EEType* pThis = &this)
                    return *(IntPtr*)((byte*)pThis + sizeof(EEType) + (sizeof(void*) * _usNumVtableSlots) + (sizeof(EEInterfaceInfo) * NumInterfaces));
            }
#if TYPE_LOADER_IMPLEMENTATION
            set
            {
                Debug.Assert(IsDynamicType && IsFinalizable);

                fixed (EEType* pThis = &this)
                    *(IntPtr*)((byte*)pThis + sizeof(EEType) + (sizeof(void*) * _usNumVtableSlots) + (sizeof(EEInterfaceInfo) * NumInterfaces)) = value;
            }
#endif
        }
        
        internal EEType* BaseType
        {
            get
            {
                if (IsCloned)
                {
                    return CanonicalEEType->BaseType;
                }

                if (IsParameterizedType)
                {
                    if (IsArray)
                        return GetArrayEEType();
                    else
                        return null;
                }

                Debug.Assert(IsCanonical);

                if (IsRelatedTypeViaIAT)
                    return *_relatedType._ppBaseTypeViaIAT;
                else
                    return _relatedType._pBaseType;
            }
#if TYPE_LOADER_IMPLEMENTATION
            set
            {
                Debug.Assert(IsDynamicType);
                Debug.Assert(!IsParameterizedType);
                Debug.Assert(!IsCloned);
                Debug.Assert(IsCanonical);
                _usFlags &= (ushort)~EETypeFlags.RelatedTypeViaIATFlag;
                _relatedType._pBaseType = value;
            }
#endif
        }

        internal EEType* NonArrayBaseType
        {
            get
            {
                Debug.Assert(!IsArray, "array type not supported in BaseType");

                if (IsCloned)
                {
                    // Assuming that since this is not an Array, the CanonicalEEType is also not an array
                    return CanonicalEEType->NonArrayBaseType;
                }

                Debug.Assert(IsCanonical, "we expect canonical types here");

                if (IsRelatedTypeViaIAT)
                {
                    return *_relatedType._ppBaseTypeViaIAT;
                }

                return _relatedType._pBaseType;
            }
        }

        internal EEType* NonClonedNonArrayBaseType
        {
            get
            {
                Debug.Assert(!IsArray, "array type not supported in NonArrayBaseType");
                Debug.Assert(!IsCloned, "cloned type not supported in NonClonedNonArrayBaseType");
                Debug.Assert(IsCanonical || IsGenericTypeDefinition, "we expect canonical types here");

                if (IsRelatedTypeViaIAT)
                {
                    return *_relatedType._ppBaseTypeViaIAT;
                }

                return _relatedType._pBaseType;
            }
        }

        internal EEType* RawBaseType
        {
            get
            {
                Debug.Assert(!IsParameterizedType, "array type not supported in NonArrayBaseType");
                Debug.Assert(!IsCloned, "cloned type not supported in NonClonedNonArrayBaseType");
                Debug.Assert(IsCanonical, "we expect canonical types here");
                Debug.Assert(!IsRelatedTypeViaIAT, "Non IAT");

                return _relatedType._pBaseType;
            }
        }

        internal EEType* CanonicalEEType
        {
            get
            {
                // cloned EETypes must always refer to types in other modules
                Debug.Assert(IsCloned);
                Debug.Assert(IsRelatedTypeViaIAT);
                return *_relatedType._ppCanonicalTypeViaIAT;
            }
        }

        internal EEType* NullableType
        {
            get
            {
                Debug.Assert(IsNullable);
                UInt32 cbNullableTypeOffset = GetFieldOffset(EETypeField.ETF_NullableType);
                fixed (EEType* pThis = &this)
                {
                    if(IsNullableTypeViaIAT)
                        return **(EEType***)((byte*)pThis + cbNullableTypeOffset);
                    else
                        return *(EEType**)((byte*)pThis + cbNullableTypeOffset);
                }
            }
#if TYPE_LOADER_IMPLEMENTATION
            set
            {
                Debug.Assert(IsNullable && IsDynamicType && !IsNullableTypeViaIAT);
                UInt32 cbNullableTypeOffset = GetFieldOffset(EETypeField.ETF_NullableType);
                fixed (EEType* pThis = &this)
                    *((EEType**)((byte*)pThis + cbNullableTypeOffset)) = value;
            }
#endif
        }

        /// <summary>
        /// Gets the offset of the value embedded in a Nullable&lt;T&gt;.
        /// </summary>
        internal byte NullableValueOffset
        {
            get
            {
                Debug.Assert(IsNullable);

                // Grab optional fields. If there aren't any then the offset was the default of 1 (immediately after the
                // Nullable's boolean flag).
                byte* optionalFields = OptionalFieldsPtr;
                if (optionalFields == null)
                    return 1;

                // The offset is never zero (Nullable has a boolean there indicating whether the value is valid). So the
                // offset is encoded - 1 to save space. The zero below is the default value if the field wasn't encoded at
                // all.
                return (byte)(OptionalFieldsReader.GetInlineField(optionalFields, EETypeOptionalFieldTag.OFT_NullableValueOffset, 0) + 1);
            }
        }

        internal EEType* RelatedParameterType
        {
            get
            {
                Debug.Assert(IsParameterizedType);

                if (IsRelatedTypeViaIAT)
                    return *_relatedType._ppRelatedParameterTypeViaIAT;
                else
                    return _relatedType._pRelatedParameterType;
            }
#if TYPE_LOADER_IMPLEMENTATION
            set
            {
                Debug.Assert(IsDynamicType && IsParameterizedType);
                _usFlags &= ((UInt16)~EETypeFlags.RelatedTypeViaIATFlag);
                _relatedType._pRelatedParameterType = value;
            }
#endif
        }

        internal unsafe IntPtr* GetVTableStartAddress()
        {
            byte* pResult;

            // EETypes are always in unmanaged memory, so 'leaking' the 'fixed pointer' is safe.
            fixed (EEType* pThis = &this)
                pResult = (byte*)pThis;

            pResult += sizeof(EEType);
            return (IntPtr*)pResult;
        }

        static IntPtr FollowRelativePointer(Int32* pDist)
        {
            Int32 dist = *pDist;
            IntPtr result = (IntPtr)((byte*)pDist + dist);
            return result;
        }
        
        internal IntPtr GetSealedVirtualSlot(UInt16 slotNumber)
        {
            Debug.Assert(!IsNullable);

            fixed (EEType* pThis = &this)
            {
                if (IsDynamicType)
                {
                    UInt32 cbSealedVirtualSlotsTypeOffset = GetFieldOffset(EETypeField.ETF_SealedVirtualSlots);
                    IntPtr* pSealedVirtualsSlotTable = *(IntPtr**)((byte*)pThis + cbSealedVirtualSlotsTypeOffset);
                    return pSealedVirtualsSlotTable[slotNumber];
                }
                else
                {
                    UInt32 cbSealedVirtualSlotsTypeOffset = GetFieldOffset(EETypeField.ETF_SealedVirtualSlots);
                    Int32* pSealedVirtualsSlotTable = (Int32*)FollowRelativePointer((Int32*)((byte*)pThis + cbSealedVirtualSlotsTypeOffset));
                    IntPtr result = FollowRelativePointer(&pSealedVirtualsSlotTable[slotNumber]);
                    return result;
                }
            }
        }

#if TYPE_LOADER_IMPLEMENTATION
        internal void SetSealedVirtualSlot(IntPtr value, UInt16 slotNumber)
        {
            Debug.Assert(IsDynamicType);

            fixed (EEType* pThis = &this)
            {
                UInt32 cbSealedVirtualSlotsTypeOffset = GetFieldOffset(EETypeField.ETF_SealedVirtualSlots);
                IntPtr* pSealedVirtualsSlotTable = *(IntPtr**)((byte*)pThis + cbSealedVirtualSlotsTypeOffset);
                pSealedVirtualsSlotTable[slotNumber] = value;
            }
        }
#endif

        internal byte* OptionalFieldsPtr
        {
            get
            {
                if (!HasOptionalFields)
                    return null;

                // Runtime allocated EETypes don't copy over optional fields. We should be careful to avoid operations
                // that require them on paths that can handle such cases.
                Debug.Assert(!IsRuntimeAllocated);

                UInt32 cbOptionalFieldsOffset = GetFieldOffset(EETypeField.ETF_OptionalFieldsPtr);
                fixed (EEType* pThis = &this)
                {
                    return *(byte**)((byte*)pThis + cbOptionalFieldsOffset);
                }
            }
#if TYPE_LOADER_IMPLEMENTATION
            set
            {
                _usFlags |= (UInt16)EETypeFlags.OptionalFieldsFlag;

                UInt32 cbOptionalFieldsOffset = GetFieldOffset(EETypeField.ETF_OptionalFieldsPtr);
                fixed (EEType* pThis = &this)
                {
                    *(byte**)((byte*)pThis + cbOptionalFieldsOffset) = value;
                }
            }
#endif
        }

        internal EEType* DynamicTemplateType
        {
            get
            {
                Debug.Assert(IsDynamicType);
                UInt32 cbOffset = GetFieldOffset(EETypeField.ETF_DynamicTemplateType);
                fixed (EEType* pThis = &this)
                {
                    return *(EEType**)((byte*)pThis + cbOffset);
                }
            }
#if TYPE_LOADER_IMPLEMENTATION
            set
            {
                Debug.Assert(IsDynamicType);
                UInt32 cbOffset = GetFieldOffset(EETypeField.ETF_DynamicTemplateType);
                fixed (EEType* pThis = &this)
                {
                    *(EEType**)((byte*)pThis + cbOffset) = value;
                }
            }
#endif
        }

        internal unsafe EETypeRareFlags RareFlags
        {
            get
            {
                // If there are no optional fields then none of the rare flags have been set.
                // Get the flags from the optional fields. The default is zero if that particular field was not included.
                return HasOptionalFields ? (EETypeRareFlags)OptionalFieldsReader.GetInlineField(OptionalFieldsPtr, EETypeOptionalFieldTag.OFT_RareFlags, 0) : 0;
            }
        }

        internal int FieldAlignmentRequirement
        {
            get
            {
                byte* optionalFields = OptionalFieldsPtr;

                // If there are no optional fields then the alignment must have been the default, IntPtr.Size. 
                // (This happens for all reference types, and for valuetypes with default alignment and no padding)
                if (optionalFields == null)
                    return IntPtr.Size;

                // Get the value from the optional fields. The default is zero if that particular field was not included.
                // The low bits of this field is the ValueType field padding, the rest of the value is the alignment if present
                UInt32 alignmentValue = (OptionalFieldsReader.GetInlineField(optionalFields, EETypeOptionalFieldTag.OFT_ValueTypeFieldPadding, 0) & ValueTypePaddingAlignmentMask) >> ValueTypePaddingAlignmentShift;

                // Alignment is stored as 1 + the log base 2 of the alignment, except a 0 indicates standard pointer alignment.
                if (alignmentValue == 0)
                    return IntPtr.Size;
                else
                    return 1 << ((int)alignmentValue - 1);
            }
        }

        internal static UInt32 ComputeValueTypeFieldPaddingFieldValue(UInt32 padding, UInt32 alignment)
        {
            // For the default case, return 0
            if ((padding == 0) && (alignment == IntPtr.Size))
                return 0;

            UInt32 alignmentLog2 = 0;
            Debug.Assert(alignment != 0);

            while ((alignment & 1) == 0)
            {
                alignmentLog2++;
                alignment = alignment >> 1;
            }
            Debug.Assert(alignment == 1);

            Debug.Assert(ValueTypePaddingMax >= padding);

            alignmentLog2++; // Our alignment values here are adjusted by one to allow for a default of 0

            UInt32 paddingLowBits = padding & ValueTypePaddingLowMask;
            UInt32 paddingHighBits = ((padding & ~ValueTypePaddingLowMask) >> ValueTypePaddingAlignmentShift) << ValueTypePaddingHighShift;
            UInt32 alignmentLog2Bits = alignmentLog2 << ValueTypePaddingAlignmentShift;
            Debug.Assert((alignmentLog2Bits & ~ValueTypePaddingAlignmentMask) == 0);
            return paddingLowBits | paddingHighBits | alignmentLog2Bits;
        }

        internal CorElementType CorElementType
        {
            get
            {
                return (CorElementType)((_usFlags & (ushort)EETypeFlags.CorElementTypeMask) >> (ushort)EETypeFlags.CorElementTypeShift);
            }
        }

        public bool HasCctor
        {
            get
            {
                return (RareFlags & EETypeRareFlags.HasCctorFlag) != 0;
            }
        }
        
        public UInt32 GetFieldOffset(EETypeField eField)
        {
            // First part of EEType consists of the fixed portion followed by the vtable.
            UInt32 cbOffset = (UInt32)(sizeof(EEType) + (IntPtr.Size * _usNumVtableSlots));

            // Then we have the interface map.
            if (eField == EETypeField.ETF_InterfaceMap)
            {
                Debug.Assert(NumInterfaces > 0);
                return cbOffset;
            }
            cbOffset += (UInt32)(sizeof(EEInterfaceInfo) * NumInterfaces);

            // Followed by the pointer to the finalizer method.
            if (eField == EETypeField.ETF_Finalizer)
            {
                Debug.Assert(IsFinalizable);
                return cbOffset;
            }
            if (IsFinalizable)
                cbOffset += (UInt32)IntPtr.Size;

            // Followed by the pointer to the optional fields.
            if (eField == EETypeField.ETF_OptionalFieldsPtr)
            {
                Debug.Assert(HasOptionalFields);
                return cbOffset;
            }
            if (HasOptionalFields)
                cbOffset += (UInt32)IntPtr.Size;

            // Followed by the pointer to the type target of a Nullable<T>.
            if (eField == EETypeField.ETF_NullableType)
            {
                Debug.Assert(IsNullable);
                return cbOffset;
            }

            // OR, followed by the pointer to the sealed virtual slots
            if (eField == EETypeField.ETF_SealedVirtualSlots)
                return cbOffset;

            if (IsNullable || (RareFlags & EETypeRareFlags.IsDynamicTypeWithSealedVTableEntriesFlag) != 0)
                cbOffset += (UInt32)IntPtr.Size;

            if (eField == EETypeField.ETF_DynamicDispatchMap)
            {
                Debug.Assert(IsDynamicType);
                return cbOffset;
            }
            if ((RareFlags & EETypeRareFlags.HasDynamicallyAllocatedDispatchMapFlag) != 0)
                cbOffset += (UInt32)IntPtr.Size;

#if CORERT
            if (eField == EETypeField.ETF_GenericDefinition)
            {
                Debug.Assert(IsGeneric);
                return cbOffset;
            }
            if (IsGeneric)
                cbOffset += (UInt32)IntPtr.Size;

            if (eField == EETypeField.ETF_GenericComposition)
            {
                Debug.Assert(IsGeneric);
                return cbOffset;
            }
            if (IsGeneric)
                cbOffset += (UInt32)IntPtr.Size;
#endif

            if (eField == EETypeField.ETF_DynamicTemplateType)
            {
                Debug.Assert(IsDynamicType);
                return cbOffset;
            }

            Debug.Assert(false, "Unknown EEType field type");
            return 0;
        }

#if TYPE_LOADER_IMPLEMENTATION
        static internal UInt32 GetSizeofEEType(
            UInt16 cVirtuals,
            UInt16 cInterfaces,
            bool fHasFinalizer,
            bool fRequiresOptionalFields,
            bool fRequiresNullableType,
            bool fHasSealedVirtuals)
        {
            // We don't support nullables with sealed virtuals at this time -
            // the issue is that if both the nullable eetype and the sealed virtuals may be present,
            // we need to detect the presence of at least one of them by looking at the EEType.
            // In the case of nullable, we'd need to fetch the rare flags, which is annoying,
            // an in the case of the sealed virtual slots, the information is implicit in the dispatch
            // map, which is even more annoying. 
            // So as long as nullables don't have sealed virtual slots, it's better to make that
            // an invariant and *not* test for nullable at run time.
            Debug.Assert(!(fRequiresNullableType && fHasSealedVirtuals), "nullables with sealed virtuals are not supported at this time");

            return (UInt32)(sizeof(EEType) +
                (IntPtr.Size * cVirtuals) +
                (sizeof(EEInterfaceInfo) * cInterfaces) +
                (fHasFinalizer ? sizeof(UIntPtr) : 0) +
                (fRequiresOptionalFields ? sizeof(IntPtr) : 0) +
                (fRequiresNullableType ? sizeof(IntPtr) : 0) +
                (fHasSealedVirtuals ? sizeof(IntPtr) : 0));
        }
#endif
    }
}
