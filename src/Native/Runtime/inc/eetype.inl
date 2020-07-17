// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef __eetype_inl__
#define __eetype_inl__
//-----------------------------------------------------------------------------------------------------------
inline UInt32 EEType::GetHashCode()
{
    return m_uHashCode;
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

#if !defined(DACCESS_COMPILE)
inline PTR_UInt8 FollowRelativePointer(const Int32* pDist)
{
    Int32 dist = *pDist;

    PTR_UInt8 result = (PTR_UInt8)pDist + dist;

    return result;
}

// Retrieve optional fields associated with this EEType. May be NULL if no such fields exist.
inline PTR_OptionalFields EEType::get_OptionalFields()
{
    if ((m_usFlags & OptionalFieldsFlag) == 0)
        return NULL;

    UInt32 cbOptionalFieldsOffset = GetFieldOffset(ETF_OptionalFieldsPtr);

#if !defined(USE_PORTABLE_HELPERS)
    if (!IsDynamicType())
    {
        return (OptionalFields*)FollowRelativePointer((Int32*)((UInt8*)this + cbOptionalFieldsOffset));
    }
    else
#endif
    {
        return *(OptionalFields**)((UInt8*)this + cbOptionalFieldsOffset);
    }
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

inline TypeManagerHandle* EEType::GetTypeManagerPtr()
{
    UInt32 cbOffset = GetFieldOffset(ETF_TypeManagerIndirection);

#if !defined(USE_PORTABLE_HELPERS)
    if (!IsDynamicType())
    {
        return (TypeManagerHandle*)FollowRelativePointer((Int32*)((UInt8*)this + cbOffset));
    }
    else
#endif
    {
        return *(TypeManagerHandle**)((UInt8*)this + cbOffset);
    }
}
#endif // !defined(DACCESS_COMPILE)

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

    const UInt32 relativeOrFullPointerOffset =
#if USE_PORTABLE_HELPERS
        sizeof(UIntTarget);
#else
        IsDynamicType() ? sizeof(UIntTarget) : sizeof(UInt32);
#endif

    // Followed by the type manager indirection cell.
    if (eField == ETF_TypeManagerIndirection)
    {
        return cbOffset;
    }
    cbOffset += relativeOrFullPointerOffset;

#if SUPPORTS_WRITABLE_DATA
    // Followed by writable data.
    if (eField == ETF_WritableData)
    {
        return cbOffset;
    }
    cbOffset += relativeOrFullPointerOffset;
#endif

    // Followed by the pointer to the finalizer method.
    if (eField == ETF_Finalizer)
    {
        ASSERT(HasFinalizer());
        return cbOffset;
    }
    if (HasFinalizer())
        cbOffset += relativeOrFullPointerOffset;

    // Followed by the pointer to the optional fields.
    if (eField == ETF_OptionalFieldsPtr)
    {
        ASSERT(HasOptionalFields());
        return cbOffset;
    }
    if (HasOptionalFields())
        cbOffset += relativeOrFullPointerOffset;

    // Followed by the pointer to the sealed virtual slots
    if (eField == ETF_SealedVirtualSlots)
        return cbOffset;

    UInt32 rareFlags = get_RareFlags();

    // in the case of sealed vtable entries on static types, we have a UInt sized relative pointer
    if (rareFlags & HasSealedVTableEntriesFlag)
        cbOffset += relativeOrFullPointerOffset;

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
        cbOffset += relativeOrFullPointerOffset;

    if (eField == ETF_GenericComposition)
    {
        ASSERT(IsGeneric());
        return cbOffset;
    }
    if (IsGeneric())
        cbOffset += relativeOrFullPointerOffset;

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
#endif // __eetype_inl__
