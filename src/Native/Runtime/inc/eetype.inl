// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#ifndef __eetype_inl__
#define __eetype_inl__
//-----------------------------------------------------------------------------------------------------------
inline UInt32 EEType::GetHashCode()
{
    return m_uHashCode;
}

//-----------------------------------------------------------------------------------------------------------
inline EEInterfaceInfo & EEInterfaceInfoMap::operator[](UInt16 idx)
{
    ASSERT(idx < m_cMap);
    return m_pMap[idx];
}

//-----------------------------------------------------------------------------------------------------------
inline PTR_Code EEType::get_Slot(UInt16 slotNumber)
{
    ASSERT(slotNumber < m_usNumVtableSlots);
    return *get_SlotPtr(slotNumber);
}

//-----------------------------------------------------------------------------------------------------------
inline PTR_PTR_Code EEType::get_SlotPtr(UInt16 slotNumber)
{
    ASSERT(slotNumber < m_usNumVtableSlots);
    return dac_cast<PTR_PTR_Code>(dac_cast<TADDR>(this) + offsetof(EEType, m_VTable)) + slotNumber;
}

#if !defined(DACCESS_COMPILE)
inline PTR_UInt8 FollowRelativePointer(const Int32 *pDist)
{
    Int32 dist = *pDist;

    PTR_UInt8 result = (PTR_UInt8)pDist + dist;

    return result;
}

inline PTR_Code EEType::get_SealedVirtualSlot(UInt16 slotNumber)
{
    ASSERT((get_RareFlags() & HasSealedVTableEntriesFlag) != 0);

    if (IsDynamicType())
    {
        UInt32 cbSealedVirtualSlotsTypeOffset = GetFieldOffset(ETF_SealedVirtualSlots);
        PTR_PTR_Code pSealedVirtualsSlotTable = *(PTR_PTR_Code*)((PTR_UInt8)this + cbSealedVirtualSlotsTypeOffset);
        return pSealedVirtualsSlotTable[slotNumber];
    }
    else
    {
        UInt32 cbSealedVirtualSlotsTypeOffset = GetFieldOffset(ETF_SealedVirtualSlots);
        PTR_Int32 pSealedVirtualsSlotTable = (PTR_Int32)FollowRelativePointer((PTR_Int32)((PTR_UInt8)this + cbSealedVirtualSlotsTypeOffset));
        PTR_Code result = FollowRelativePointer(&pSealedVirtualsSlotTable[slotNumber]);
        return result;
    }
}
#endif // !DACCESS_COMPILE

#if !defined(DACCESS_COMPILE)
//-----------------------------------------------------------------------------------------------------------
inline bool EEType::HasDispatchMap()
{
    if (!HasInterfaces())
        return false;
    OptionalFields *optionalFields = get_OptionalFields();
    if (optionalFields == NULL)
        return false;
    UInt32 idxDispatchMap = optionalFields->GetDispatchMap(0xffffffff);
    if (idxDispatchMap == 0xffffffff)
    {
        if (HasDynamicallyAllocatedDispatchMap())
            return true;
        else if (IsDynamicType())
            return get_DynamicTemplateType()->HasDispatchMap();
        return false;
    }
    return true;
}

inline DispatchMap * EEType::GetDispatchMap()
{
    if (!HasInterfaces())
        return NULL;

    // Get index of DispatchMap pointer in the lookup table stored in this EEType's module.
    OptionalFields *optionalFields = get_OptionalFields();
    if (optionalFields == NULL)
        return NULL;
    UInt32 idxDispatchMap = optionalFields->GetDispatchMap(0xffffffff);
    if ((idxDispatchMap == 0xffffffff) && IsDynamicType())
    {
        if (HasDynamicallyAllocatedDispatchMap())
            return *(DispatchMap **)((UInt8*)this + GetFieldOffset(ETF_DynamicDispatchMap));
        else
            return get_DynamicTemplateType()->GetDispatchMap();
    }

    return GetTypeManagerPtr()->AsTypeManager()->GetDispatchMapLookupTable()[idxDispatchMap];
}
#endif // !DACCESS_COMPILE

//-----------------------------------------------------------------------------------------------------------
inline EEInterfaceInfoMap EEType::GetInterfaceMap()
{
    UInt32 cbInterfaceMapOffset = GetFieldOffset(ETF_InterfaceMap);

    return EEInterfaceInfoMap(reinterpret_cast<EEInterfaceInfo *>((UInt8*)this + cbInterfaceMapOffset),
                              GetNumInterfaces());
}

#ifdef DACCESS_COMPILE
inline bool EEType::DacVerify()
{
    // Use a separate static worker because the worker validates
    // the whole chain of EETypes and we don't want to accidentally
    // answer questions from 'this' that should have come from the
    // 'current' EEType.
    return DacVerifyWorker(this);
}
// static
inline bool EEType::DacVerifyWorker(EEType* pThis)  
{
    //*********************************************************************
    //**** ASSUMES MAX TYPE HIERARCHY DEPTH OF 1024 TYPES              ****
    //*********************************************************************
    const int MAX_SANE_RELATED_TYPES = 1024;
    //*********************************************************************
    //**** ASSUMES MAX OF 200 INTERFACES IMPLEMENTED ON ANY GIVEN TYPE ****
    //*********************************************************************
    const int MAX_SANE_NUM_INSTANCES = 200;


    PTR_EEType pCurrentType = dac_cast<PTR_EEType>(pThis);
    for (int i = 0; i < MAX_SANE_RELATED_TYPES; i++)
    {
        // Verify interface map
        if (pCurrentType->GetNumInterfaces() > MAX_SANE_NUM_INSTANCES)
            return false;

        // Validate the current type
        if (!pCurrentType->Validate(false))
            return false;

        //
        // Now on to the next type in the hierarchy.
        //

        if (pCurrentType->IsRelatedTypeViaIAT())
            pCurrentType = *dac_cast<PTR_PTR_EEType>(reinterpret_cast<TADDR>(pCurrentType->m_RelatedType.m_ppBaseTypeViaIAT));
        else
            pCurrentType = dac_cast<PTR_EEType>(reinterpret_cast<TADDR>(pCurrentType->m_RelatedType.m_pBaseType));

        if (pCurrentType == NULL)
            break;
    }
    
    if (pCurrentType != NULL)
        return false;   // assume we found an infinite loop

    return true;
}
#endif

/* static */
inline UInt32 EEType::ComputeValueTypeFieldPaddingFieldValue(UInt32 padding, UInt32 alignment)
{
    // For the default case, return 0
    if ((padding == 0) && (alignment == POINTER_SIZE))
        return 0;

    UInt32 alignmentLog2 = 0;
    ASSERT(alignment != 0);

    while ((alignment & 1) == 0)
    {
        alignmentLog2++;
        alignment = alignment >> 1;
    }
    ASSERT(alignment == 1);

    ASSERT(ValueTypePaddingMax >= padding);

    alignmentLog2++; // Our alignment values here are adjusted by one to allow for a default of 0

    UInt32 paddingLowBits = padding & ValueTypePaddingLowMask;
    UInt32 paddingHighBits = ((padding & ~ValueTypePaddingLowMask) >> ValueTypePaddingAlignmentShift) << ValueTypePaddingHighShift;
    UInt32 alignmentLog2Bits = alignmentLog2 << ValueTypePaddingAlignmentShift;
    ASSERT((alignmentLog2Bits & ~ValueTypePaddingAlignmentMask) == 0);
    return paddingLowBits | paddingHighBits | alignmentLog2Bits;
}

// Retrieve optional fields associated with this EEType. May be NULL if no such fields exist.
inline PTR_OptionalFields EEType::get_OptionalFields()
{
    if ((m_usFlags & OptionalFieldsFlag) == 0)
        return NULL;

    UInt32 cbOptionalFieldsOffset = GetFieldOffset(ETF_OptionalFieldsPtr);
#if defined(DACCESS_COMPILE)
    // this construct creates a "host address" for the optional field blob
    return *(PTR_PTR_OptionalFields)((dac_cast<TADDR>(this)) + cbOptionalFieldsOffset);
#else
    return *(OptionalFields**)((UInt8*)this + cbOptionalFieldsOffset);

#endif
}

// Retrieve the amount of padding added to value type fields in order to align them for boxed allocation on
// the GC heap. This value to can be used along with the result of get_BaseSize to determine the size of a
// value type embedded in the stack, and array or another type.
inline UInt32 EEType::get_ValueTypeFieldPadding()
{
    OptionalFields * pOptFields = get_OptionalFields();

    // If there are no optional fields then the padding must have been the default, 0.
    if (!pOptFields)
        return 0;

    // Get the value from the optional fields. The default is zero if that particular field was not included.
    // The low bits of this field is the ValueType field padding, the rest of the byte is the alignment if present
    UInt32 ValueTypeFieldPaddingData = pOptFields->GetValueTypeFieldPadding(0);
    UInt32 padding = ValueTypeFieldPaddingData & ValueTypePaddingLowMask;
    // If there is additional padding, the other bits have that data
    padding |= (ValueTypeFieldPaddingData & ValueTypePaddingHighMask) >> (ValueTypePaddingHighShift - ValueTypePaddingAlignmentShift);
    return padding;
}

// Retrieve the alignment of this valuetype
inline UInt32 EEType::get_ValueTypeFieldAlignment()
{
    OptionalFields * pOptFields = get_OptionalFields();

    // If there are no optional fields then the alignment must have been the default, POINTER_SIZE.
    if (!pOptFields)
        return POINTER_SIZE;

    // Get the value from the optional fields. The default is zero if that particular field was not included.
    // The low bits of this field is the ValueType field padding, the rest of the byte is the alignment if present
    UInt32 alignmentValue = (pOptFields->GetValueTypeFieldPadding(0) & ValueTypePaddingAlignmentMask)  >> ValueTypePaddingAlignmentShift;;

    // Alignment is stored as 1 + the log base 2 of the alignment, except a 0 indicates standard pointer alignment.
    if (alignmentValue == 0)
        return POINTER_SIZE;
    else
        return 1 << (alignmentValue - 1);
}

// Get flags that are less commonly set on EETypes.
inline UInt32 EEType::get_RareFlags()
{
    OptionalFields * pOptFields = get_OptionalFields();

    // If there are no optional fields then none of the rare flags have been set.
    if (!pOptFields)
        return 0;

    // Get the flags from the optional fields. The default is zero if that particular field was not included.
    return pOptFields->GetRareFlags(0);
}

// Retrieve the offset of the value embedded in a Nullable<T>.
inline UInt8 EEType::GetNullableValueOffset()
{
    ASSERT(IsNullable());

    // Grab optional fields. If there aren't any then the offset was the default of 1 (immediately after the
    // Nullable's boolean flag).
    OptionalFields * pOptFields = get_OptionalFields();
    if (pOptFields == NULL)
        return 1;

    // The offset is never zero (Nullable has a boolean there indicating whether the value is valid). So the
    // offset is encoded - 1 to save space. The zero below is the default value if the field wasn't encoded at
    // all.
    return pOptFields->GetNullableValueOffset(0) + 1;
}

inline EEType * EEType::get_DynamicTemplateType()
{
    ASSERT(IsDynamicType());

    UInt32 cbOffset = GetFieldOffset(ETF_DynamicTemplateType);

#if defined(DACCESS_COMPILE)
    return *(PTR_PTR_EEType)((dac_cast<TADDR>(this)) + cbOffset);
#else
    return *(EEType**)((UInt8*)this + cbOffset);
#endif
}

inline UInt32 EEType::get_DynamicThreadStaticOffset()
{
    UInt32 cbOffset = GetFieldOffset(ETF_DynamicThreadStaticOffset);

    return *(UInt32*)((UInt8*)this + cbOffset);
}

inline DynamicModule * EEType::get_DynamicModule()
{
    if ((get_RareFlags() & HasDynamicModuleFlag) != 0)
    {
        UInt32 cbOffset = GetFieldOffset(ETF_DynamicModule);

        return *(DynamicModule**)((UInt8*)this + cbOffset);
    }
    else
    {
        return nullptr;
    }
}

// Calculate the offset of a field of the EEType that has a variable offset.
__forceinline UInt32 EEType::GetFieldOffset(EETypeField eField)
{
    // First part of EEType consists of the fixed portion followed by the vtable.
    UInt32 cbOffset = offsetof(EEType, m_VTable) + (sizeof(UIntTarget) * m_usNumVtableSlots);

    // Then we have the interface map.
    if (eField == ETF_InterfaceMap)
    {
        ASSERT(GetNumInterfaces() > 0);
        return cbOffset;
    }
    cbOffset += sizeof(EEInterfaceInfo) * GetNumInterfaces();

    // Followed by the pointer to the finalizer method.
    if (eField == ETF_Finalizer)
    {
        ASSERT(HasFinalizer());
        return cbOffset;
    }
    if (HasFinalizer())
        cbOffset += sizeof(UIntTarget);

    // Followed by the pointer to the optional fields.
    if (eField == ETF_OptionalFieldsPtr)
    {
        ASSERT(HasOptionalFields());
        return cbOffset;
    }
    if (HasOptionalFields())
        cbOffset += sizeof(UIntTarget);

    // Followed by the pointer to the sealed virtual slots
    if (eField == ETF_SealedVirtualSlots)
        return cbOffset;

    UInt32 rareFlags = get_RareFlags();

    // in the case of sealed vtable entries on static types, we have a UInt sized relative pointer
    if (rareFlags & HasSealedVTableEntriesFlag)
        cbOffset += (IsDynamicType() ? sizeof(UIntTarget) : sizeof(UInt32));

    if (eField == ETF_DynamicDispatchMap)
    {
        ASSERT(IsDynamicType());
        return cbOffset;
    }
    if ((rareFlags & HasDynamicallyAllocatedDispatchMapFlag) != 0)
        cbOffset += sizeof(UIntTarget);

    if (eField == ETF_GenericDefinition)
    {
        ASSERT(IsGeneric());
        return cbOffset;
    }
    if (IsGeneric())
        cbOffset += (IsDynamicType() ? sizeof(UIntTarget) : sizeof(UInt32));

    if (eField == ETF_GenericComposition)
    {
        ASSERT(IsGeneric());
        return cbOffset;
    }
    if (IsGeneric())
        cbOffset += (IsDynamicType() ? sizeof(UIntTarget) : sizeof(UInt32));

    if (eField == ETF_DynamicModule)
    {
        ASSERT((rareFlags & HasDynamicModuleFlag) != 0);
        return cbOffset;
    }

    if ((rareFlags & HasDynamicModuleFlag) != 0)
        cbOffset += sizeof(UIntTarget);

    if (eField == ETF_DynamicTemplateType)
    {
        ASSERT(IsDynamicType());
        return cbOffset;
    }
    if (IsDynamicType())
        cbOffset += sizeof(UIntTarget);

    if (eField == ETF_DynamicGcStatics)
    {
        ASSERT((rareFlags & IsDynamicTypeWithGcStaticsFlag) != 0);
        return cbOffset;
    }
    if ((rareFlags & IsDynamicTypeWithGcStaticsFlag) != 0)
        cbOffset += sizeof(UIntTarget);

    if (eField == ETF_DynamicNonGcStatics)
    {
        ASSERT((rareFlags & IsDynamicTypeWithNonGcStaticsFlag) != 0);
        return cbOffset;
    }
    if ((rareFlags & IsDynamicTypeWithNonGcStaticsFlag) != 0)
        cbOffset += sizeof(UIntTarget);

    if (eField == ETF_DynamicThreadStaticOffset)
    {
        ASSERT((rareFlags & IsDynamicTypeWithThreadStaticsFlag) != 0);
        return cbOffset;
    }
    if ((rareFlags & IsDynamicTypeWithThreadStaticsFlag) != 0)
        cbOffset += sizeof(UInt32);

    ASSERT(!"Unknown EEType field type");
    return 0;
}

inline size_t GenericComposition::GetArgumentOffset(UInt32 index)
{
    ASSERT(index < m_arity);
    return offsetof(GenericComposition, m_arguments[index]);
}

inline GenericVarianceType *GenericComposition::GetVariance()
{
    ASSERT(m_hasVariance);
    return (GenericVarianceType *)(((UInt8 *)this) + offsetof(GenericComposition, m_arguments[m_arity]));
}

#endif // __eetype_inl__
