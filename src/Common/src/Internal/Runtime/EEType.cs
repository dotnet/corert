﻿// Licensed to the .NET Foundation under one or more agreements.
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
        private IntPtr _objHeaderContents;
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
                if ((unchecked((uint)_interfaceType._pInterfaceEEType) & IndirectionConstants.IndirectionCellPointer) != 0)
                {
#if BIT64
                    EEType** ppInterfaceEETypeViaIAT = (EEType**)(((ulong)_interfaceType._ppInterfaceEETypeViaIAT) - IndirectionConstants.IndirectionCellPointer);
#else
                    EEType** ppInterfaceEETypeViaIAT = (EEType**)(((uint)_interfaceType._ppInterfaceEETypeViaIAT) - IndirectionConstants.IndirectionCellPointer);
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

        private UInt32 _entryCount;
        private DispatchMapEntry _dispatchMap; // at least one entry if any interfaces defined

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
#if BIT64
        private const int POINTER_SIZE = 8;
        private const int PADDING = 1; // _numComponents is padded by one Int32 to make the first element pointer-aligned
#else
        private const int POINTER_SIZE = 4;
        private const int PADDING = 0;
#endif
        internal const int SZARRAY_BASE_SIZE = POINTER_SIZE + POINTER_SIZE + (1 + PADDING) * 4;

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
            public EEType* _pCanonicalType;
            [FieldOffset(0)]
            public EEType** _ppCanonicalTypeViaIAT;

            // Kinds.ArrayEEType
            [FieldOffset(0)]
            public EEType* _pRelatedParameterType;
            [FieldOffset(0)]
            public EEType** _ppRelatedParameterTypeViaIAT;
        }

        private static unsafe class OptionalFieldsReader
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

#if EETYPE_TYPE_MANAGER
        private IntPtr _ppTypeManager;
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

        internal bool IsString
        {
            get
            {
                // String is currently the only non-array type with a non-zero component size.
                return ComponentSize == StringComponentSize.Value && !IsArray && !IsGenericTypeDefinition;
            }
        }

        internal bool IsArray
        {
            get
            {
                return IsParameterizedType && ParameterizedTypeShape >= SZARRAY_BASE_SIZE;
            }
        }


        internal int ArrayRank
        {
            get
            {
                Debug.Assert(this.IsArray);

                int boundsSize = (int)this.ParameterizedTypeShape - SZARRAY_BASE_SIZE;
                if (boundsSize > 0)
                {
                    // Multidim array case: Base size includes space for two Int32s
                    // (upper and lower bound) per each dimension of the array.
                    return boundsSize / (2 * sizeof(int));
                }
                return 1;
            }
        }

        internal bool IsSzArray
        {
            get
            {
                return IsArray && ParameterizedTypeShape == SZARRAY_BASE_SIZE;
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

        internal EEType* GenericDefinition
        {
            get
            {
                Debug.Assert(IsGeneric);
                UInt32 cbOffset = GetFieldOffset(EETypeField.ETF_GenericDefinition);
                fixed (EEType* pThis = &this)
                {
                    return ((EETypeRef*)((byte*)pThis + cbOffset))->Value;
                }
            }
#if TYPE_LOADER_IMPLEMENTATION
            set
            {
                Debug.Assert(IsGeneric && IsDynamicType);
                UInt32 cbOffset = GetFieldOffset(EETypeField.ETF_GenericDefinition);
                fixed (EEType* pThis = &this)
                {
                    *((EEType**)((byte*)pThis + cbOffset)) = value;
                }
            }
#endif
        }

        internal uint GenericArity
        {
            get
            {
                Debug.Assert(IsGeneric);
                UInt32 cbOffset = GetFieldOffset(EETypeField.ETF_GenericComposition);
                fixed (EEType* pThis = &this)
                {
                    // Number of generic arguments is the first UInt16 of the composition stream.
                    return **(UInt16**)((byte*)pThis + cbOffset);
                }
            }
        }

        internal EETypeRef* GenericArguments
        {
            get
            {
                Debug.Assert(IsGeneric);
                UInt32 cbOffset = GetFieldOffset(EETypeField.ETF_GenericComposition);
                fixed (EEType* pThis = &this)
                {
                    // Generic arguments follow after a (padded) UInt16 specifying their count
                    // in the generic composition stream.
                    return ((*(EETypeRef**)((byte*)pThis + cbOffset)) + 1);
                }
            }
        }

        internal GenericVariance* GenericVariance
        {
            get
            {
                Debug.Assert(IsGeneric);

                if (!HasGenericVariance)
                    return null;

                UInt32 cbOffset = GetFieldOffset(EETypeField.ETF_GenericComposition);
                fixed (EEType* pThis = &this)
                {
                    // Variance info follows immediatelly after the generic arguments
                    return (GenericVariance*)(GenericArguments + GenericArity);
                }
            }
        }

        internal bool IsPointerType
        {
            get
            {
                return IsParameterizedType &&
                    ParameterizedTypeShape == ParameterizedTypeShapeConstants.Pointer;
            }
        }

        internal bool IsByRefType
        {
            get
            {
                return IsParameterizedType &&
                    ParameterizedTypeShape == ParameterizedTypeShapeConstants.ByRef;
            }
        }

        internal bool IsInterface
        {
            get
            {
                return ((_usFlags & (UInt16)EETypeFlags.IsInterfaceFlag) != 0);
            }
        }

        internal bool IsAbstract
        {
            get
            {
                return IsInterface || (RareFlags & EETypeRareFlags.IsAbstractClassFlag) != 0;
            }
        }

        internal bool IsByRefLike
        {
            get
            {
                return (RareFlags & EETypeRareFlags.IsByRefLikeFlag) != 0;
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
        // Currently, the meaning is a shape of 0 indicates that this is a Pointer,
        // shape of 1 indicates a ByRef, and >=SZARRAY_BASE_SIZE indicates that this is an array.
        // Two types are not equivalent if their shapes do not exactly match.
        internal UInt32 ParameterizedTypeShape
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
                return ((_usFlags & (UInt16)EETypeFlags.ICastableFlag) != 0);
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
                if(optionalFields != null)
                {
                    const UInt16 NoSlot = 0xFFFF;
                    UInt16 uiSlot = (UInt16)OptionalFieldsReader.GetInlineField(optionalFields, EETypeOptionalFieldTag.ICastableIsInstSlot, NoSlot);
                    if (uiSlot != NoSlot)
                    {
                        if (uiSlot < NumVtableSlots)
                            return GetVTableStartAddress()[uiSlot];
                        else
                            return GetSealedVirtualSlot((UInt16)(uiSlot - NumVtableSlots));
                    }
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
                if(optionalFields != null)
                {
                    const UInt16 NoSlot = 0xFFFF;
                    UInt16 uiSlot = (UInt16)OptionalFieldsReader.GetInlineField(optionalFields, EETypeOptionalFieldTag.ICastableGetImplTypeSlot, NoSlot);
                    if (uiSlot != NoSlot)
                    {
                        if (uiSlot < NumVtableSlots)
                            return GetVTableStartAddress()[uiSlot];
                        else
                            return GetSealedVirtualSlot((UInt16)(uiSlot - NumVtableSlots));
                    }
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
                UInt32 ValueTypeFieldPaddingData = OptionalFieldsReader.GetInlineField(optionalFields, EETypeOptionalFieldTag.ValueTypeFieldPadding, 0);
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

        internal UInt32 FieldByteCountNonGCAligned
        {
            get
            {
                // This api is designed to return correct results for EETypes which can be derived from
                // And results indistinguishable from correct for DefTypes which cannot be derived from (sealed classes)
                // (For sealed classes, this should always return BaseSize-((uint)sizeof(ObjHeader));
                Debug.Assert(!IsInterface && !IsParameterizedType);

                // get_BaseSize returns the GC size including space for the sync block index field, the EEType* and
                // padding for GC heap alignment. Must subtract all of these to get the size used for the fields of
                // the type (where the fields of the type includes the EEType*)
                return BaseSize - ((uint)sizeof(ObjHeader) + ValueTypeFieldPadding);
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
                UInt32 idxDispatchMap = OptionalFieldsReader.GetInlineField(optionalFields, EETypeOptionalFieldTag.DispatchMap, 0xffffffff);
                if (idxDispatchMap == 0xffffffff)
                {
                    if (HasDynamicallyAllocatedDispatchMap)
                        return true;
                    else if (IsDynamicType)
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
                if (IsRelatedTypeViaIAT)
                    return *_relatedType._ppCanonicalTypeViaIAT;
                else
                    return _relatedType._pCanonicalType;
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
                    if (IsNullableTypeViaIAT)
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
                return (byte)(OptionalFieldsReader.GetInlineField(optionalFields, EETypeOptionalFieldTag.NullableValueOffset, 0) + 1);
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

        private static IntPtr FollowRelativePointer(Int32* pDist)
        {
            Int32 dist = *pDist;
            IntPtr result = (IntPtr)((byte*)pDist + dist);
            return result;
        }

        internal IntPtr GetSealedVirtualSlot(UInt16 slotNumber)
        {
            Debug.Assert(!IsNullable);
            Debug.Assert((RareFlags & EETypeRareFlags.HasSealedVTableEntriesFlag) != 0);

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

        internal IntPtr DynamicGcStaticsData
        {
            get
            {
                Debug.Assert((RareFlags & EETypeRareFlags.IsDynamicTypeWithGcStatics) != 0);
                UInt32 cbOffset = GetFieldOffset(EETypeField.ETF_DynamicGcStatics);
                fixed (EEType* pThis = &this)
                {
                    return (IntPtr)((byte*)pThis + cbOffset);
                }
            }
#if TYPE_LOADER_IMPLEMENTATION
            set
            {
                Debug.Assert((RareFlags & EETypeRareFlags.IsDynamicTypeWithGcStatics) != 0);
                UInt32 cbOffset = GetFieldOffset(EETypeField.ETF_DynamicGcStatics);
                fixed (EEType* pThis = &this)
                {
                    *(IntPtr*)((byte*)pThis + cbOffset) = value;
                }
            }
#endif
        }

        internal IntPtr DynamicNonGcStaticsData
        {
            get
            {
                Debug.Assert((RareFlags & EETypeRareFlags.IsDynamicTypeWithNonGcStatics) != 0);
                UInt32 cbOffset = GetFieldOffset(EETypeField.ETF_DynamicNonGcStatics);
                fixed (EEType* pThis = &this)
                {
                    return (IntPtr)((byte*)pThis + cbOffset);
                }
            }
#if TYPE_LOADER_IMPLEMENTATION
            set
            {
                Debug.Assert((RareFlags & EETypeRareFlags.IsDynamicTypeWithNonGcStatics) != 0);
                UInt32 cbOffset = GetFieldOffset(EETypeField.ETF_DynamicNonGcStatics);
                fixed (EEType* pThis = &this)
                {
                    *(IntPtr*)((byte*)pThis + cbOffset) = value;
                }
            }
#endif
        }

        internal DynamicModule* DynamicModule
        {
            get
            {
                if ((RareFlags & EETypeRareFlags.HasDynamicModuleFlag) != 0)
                {
                    UInt32 cbOffset = GetFieldOffset(EETypeField.ETF_DynamicModule);
                    fixed (EEType* pThis = &this)
                    {
                        return *(DynamicModule**)((byte*)pThis + cbOffset);
                    }
                }
                else
                {
                    return null;
                }
            }
#if TYPE_LOADER_IMPLEMENTATION
            set
            {
                Debug.Assert(RareFlags.HasFlag(EETypeRareFlags.HasDynamicModuleFlag));
                UInt32 cbOffset = GetFieldOffset(EETypeField.ETF_DynamicModule);
                fixed (EEType* pThis = &this)
                {
                    *(DynamicModule**)((byte*)pThis + cbOffset) = value;
                }
            }
#endif
        }

#if EETYPE_TYPE_MANAGER
        internal IntPtr TypeManager
        {
            get
            {
                // This is always a pointer to a pointer to a type manager
                return *(IntPtr*)_ppTypeManager;
            }
        }
#if TYPE_LOADER_IMPLEMENTATION
        internal IntPtr PointerToTypeManager
        {
            get
            {
                // This is always a pointer to a pointer to a type manager
                return _ppTypeManager;
            }

            set
            {
                _ppTypeManager = value;
            }
        }
#endif
#endif // EETYPE_TYPE_MANAGER

        internal unsafe EETypeRareFlags RareFlags
        {
            get
            {
                // If there are no optional fields then none of the rare flags have been set.
                // Get the flags from the optional fields. The default is zero if that particular field was not included.
                return HasOptionalFields ? (EETypeRareFlags)OptionalFieldsReader.GetInlineField(OptionalFieldsPtr, EETypeOptionalFieldTag.RareFlags, 0) : 0;
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
                UInt32 alignmentValue = (OptionalFieldsReader.GetInlineField(optionalFields, EETypeOptionalFieldTag.ValueTypeFieldPadding, 0) & ValueTypePaddingAlignmentMask) >> ValueTypePaddingAlignmentShift;

                // Alignment is stored as 1 + the log base 2 of the alignment, except a 0 indicates standard pointer alignment.
                if (alignmentValue == 0)
                    return IntPtr.Size;
                else
                    return 1 << ((int)alignmentValue - 1);
            }
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

            if (IsNullable)
                cbOffset += (UInt32)IntPtr.Size;

            EETypeRareFlags rareFlags = RareFlags;

            // in the case of sealed vtable entries on static types, we have a UInt sized relative pointer
            if ((rareFlags & EETypeRareFlags.HasSealedVTableEntriesFlag) != 0)
                cbOffset += (IsDynamicType ? (UInt32)IntPtr.Size : 4);

            if (eField == EETypeField.ETF_DynamicDispatchMap)
            {
                Debug.Assert(IsDynamicType);
                return cbOffset;
            }
            if ((rareFlags & EETypeRareFlags.HasDynamicallyAllocatedDispatchMapFlag) != 0)
                cbOffset += (UInt32)IntPtr.Size;

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

            if (eField == EETypeField.ETF_DynamicModule)
            {
                return cbOffset;
            }

            if ((rareFlags & EETypeRareFlags.HasDynamicModuleFlag) != 0)
                cbOffset += (UInt32)IntPtr.Size;

            if (eField == EETypeField.ETF_DynamicTemplateType)
            {
                Debug.Assert(IsDynamicType);
                return cbOffset;
            }
            if (IsDynamicType)
                cbOffset += (UInt32)IntPtr.Size;

            if (eField == EETypeField.ETF_DynamicGcStatics)
            {
                Debug.Assert((rareFlags & EETypeRareFlags.IsDynamicTypeWithGcStatics) != 0);
                return cbOffset;
            }
            if ((rareFlags & EETypeRareFlags.IsDynamicTypeWithGcStatics) != 0)
                cbOffset += (UInt32)IntPtr.Size;

            if (eField == EETypeField.ETF_DynamicNonGcStatics)
            {
                Debug.Assert((rareFlags & EETypeRareFlags.IsDynamicTypeWithNonGcStatics) != 0);
                return cbOffset;
            }
            if ((rareFlags & EETypeRareFlags.IsDynamicTypeWithNonGcStatics) != 0)
                cbOffset += (UInt32)IntPtr.Size;

            if (eField == EETypeField.ETF_DynamicThreadStaticOffset)
            {
                Debug.Assert((rareFlags & EETypeRareFlags.IsDynamicTypeWithThreadStatics) != 0);
                return cbOffset;
            }
            if ((rareFlags & EETypeRareFlags.IsDynamicTypeWithThreadStatics) != 0)
                cbOffset += 4;

            Debug.Assert(false, "Unknown EEType field type");
            return 0;
        }

#if TYPE_LOADER_IMPLEMENTATION
        internal static UInt32 GetSizeofEEType(
            UInt16 cVirtuals,
            UInt16 cInterfaces,
            bool fHasFinalizer,
            bool fRequiresOptionalFields,
            bool fRequiresNullableType,
            bool fHasSealedVirtuals,
            bool fHasGenericInfo,
            bool fHasNonGcStatics,
            bool fHasGcStatics,
            bool fHasThreadStatics)
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
                (fHasSealedVirtuals ? sizeof(IntPtr) : 0) +
                (fHasGenericInfo ? sizeof(IntPtr)*2 : 0) + // pointers to GenericDefinition and GenericComposition
                (fHasNonGcStatics ? sizeof(IntPtr) : 0) + // pointer to data
                (fHasGcStatics ? sizeof(IntPtr) : 0) +  // pointer to data
                (fHasThreadStatics ? sizeof(UInt32) : 0)); // tls offset
        }
#endif
    }

    // Wrapper around EEType pointers that may be indirected through the IAT if their low bit is set.
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct EETypeRef
    {
        private byte* _value;

        public EEType* Value
        {
            get
            {
                if (((int)_value & IndirectionConstants.IndirectionCellPointer) == 0)
                    return (EEType*)_value;
                return *(EEType**)(_value - IndirectionConstants.IndirectionCellPointer);
            }
#if TYPE_LOADER_IMPLEMENTATION
            set
            {
                _value = (byte*)value;
            }
#endif
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DynamicModule
    {
        // Size field used to indicate the number of bytes of this structure that are defined in Runtime Known ways
        // This is used to drive versioning of this field
        private int _cbSize;

        // Pointer to interface dispatch resolver that works off of a type/slot pair
        // This is a function pointer with the following signature IntPtr()(IntPtr targetType, IntPtr interfaceType, ushort slot)
        private IntPtr _dynamicTypeSlotDispatchResolve;

        // Starting address for the the binary module corresponding to this dynamic module.
        private IntPtr _getRuntimeException;

#if TYPE_LOADER_IMPLEMENTATION
        public int CbSize
        {
            get
            {
                return _cbSize;
            }
            set
            {
                _cbSize = value;
            }
        }
#endif

        public IntPtr DynamicTypeSlotDispatchResolve
        {
            get
            {
                unsafe
                {
                    if (_cbSize >= sizeof(IntPtr) * 2)
                    {
                        return _dynamicTypeSlotDispatchResolve;
                    }
                    else
                    {
                        return IntPtr.Zero;
                    }
                }
            }
#if TYPE_LOADER_IMPLEMENTATION
            set
            {
                _dynamicTypeSlotDispatchResolve = value;
            }
#endif
        }

        public IntPtr GetRuntimeException
        {
            get
            {
                unsafe
                {
                    if (_cbSize >= sizeof(IntPtr) * 3)
                    {
                        return _getRuntimeException;
                    }
                    else
                    {
                        return IntPtr.Zero;
                    }
                }
            }
#if TYPE_LOADER_IMPLEMENTATION
            set
            {
                _getRuntimeException = value;
            }
#endif
        }

        /////////////////////// END OF FIELDS KNOWN TO THE MRT RUNTIME ////////////////////////
#if TYPE_LOADER_IMPLEMENTATION
        public static readonly int DynamicModuleSize = IntPtr.Size * 3; // We have three fields here.

        // We can put non-low level runtime fields that are module level, that need quick access from a type here
        // For instance, we may choose to put a pointer to the metadata reader or the like here in the future.
#endif
    }
}
