// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#ifndef __eetype_inl__
#define __eetype_inl__
//-----------------------------------------------------------------------------------------------------------
#ifndef BINDER
inline UInt32 EEType::GetHashCode()
{
    return m_uHashCode;
}
#endif

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

#if !defined(BINDER) && !defined(DACCESS_COMPILE)
inline PTR_UInt8 FollowRelativePointer(const Int32 *pDist)
{
    Int32 dist = *pDist;

    PTR_UInt8 result = (PTR_UInt8)pDist + dist;

    return result;
}

inline PTR_Code EEType::get_SealedVirtualSlot(UInt16 slotNumber)
{
    ASSERT(!IsNullable());

    if (IsDynamicType())
    {
        if ((get_RareFlags() & IsDynamicTypeWithSealedVTableEntriesFlag) != 0)
        {
            UInt32 cbSealedVirtualSlotsTypeOffset = GetFieldOffset(ETF_SealedVirtualSlots);

            PTR_PTR_Code pSealedVirtualsSlotTable = *(PTR_PTR_Code*)((PTR_UInt8)this + cbSealedVirtualSlotsTypeOffset);

            return pSealedVirtualsSlotTable[slotNumber];
        }
        else
        {
            return get_DynamicTemplateType()->get_SealedVirtualSlot(slotNumber);
        }
    }

    UInt32 cbSealedVirtualSlotsTypeOffset = GetFieldOffset(ETF_SealedVirtualSlots);

    PTR_Int32 pSealedVirtualsSlotTable = (PTR_Int32)FollowRelativePointer((PTR_Int32)((PTR_UInt8)this + cbSealedVirtualSlotsTypeOffset));

    PTR_Code result = FollowRelativePointer(&pSealedVirtualsSlotTable[slotNumber]);

    return result;
}
#endif // !BINDER && !DACCESS_COMPILE

//-----------------------------------------------------------------------------------------------------------
inline EEType * EEType::get_BaseType()
{
#ifdef DACCESS_COMPILE
    // Easy way to cope with the get_BaseType calls throughout the DACCESS code; better than chasing down
    // all uses and changing them to check for array.
    if (IsParameterizedType())
        return NULL;
#endif

#if defined(BINDER)
    // Does not yet handle arrays properly.
    ASSERT(!IsParameterizedType());
#endif

    if (IsCloned())
    {
        return get_CanonicalEEType()->get_BaseType();
    }

#if !defined(BINDER) && !defined(DACCESS_COMPILE)
    if (IsParameterizedType())
    {
        if (IsArray())
            return GetArrayBaseType();
        else
            return NULL;
    }
#endif

    ASSERT(IsCanonical());

    if (IsRelatedTypeViaIAT())
    {
        return *PTR_PTR_EEType(reinterpret_cast<TADDR>(m_RelatedType.m_ppBaseTypeViaIAT));
    }

    return PTR_EEType(reinterpret_cast<TADDR>(m_RelatedType.m_pBaseType));
}

#if !defined(BINDER) && !defined(DACCESS_COMPILE)
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
    UInt32 idxDispatchMap = get_OptionalFields()->GetDispatchMap(0xffffffff);
    if ((idxDispatchMap == 0xffffffff) && IsDynamicType())
    {
        if (HasDynamicallyAllocatedDispatchMap())
            return *(DispatchMap **)((UInt8*)this + GetFieldOffset(ETF_DynamicDispatchMap));
        else 
            return get_DynamicTemplateType()->GetDispatchMap();
    }

    // Determine this EEType's module.
    RuntimeInstance * pRuntimeInstance = GetRuntimeInstance();

#ifdef CORERT
    return GetModuleManager()->GetDispatchMapLookupTable()[idxDispatchMap];
#endif

    Module * pModule = pRuntimeInstance->FindModuleByReadOnlyDataAddress(this);
    if (pModule == NULL)
        pModule = pRuntimeInstance->FindModuleByDataAddress(this);
    ASSERT(pModule != NULL);

    return pModule->GetDispatchMapLookupTable()[idxDispatchMap];
}
#endif // !BINDER && !DACCESS_COMPILE

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

// Initialize an existing EEType as an array type with specific element type. This is another specialized
// method used only during the unification of generic instantiation types. It might need modification if
// needed in any other scenario.
inline void EEType::InitializeAsArrayType(EEType * pElementType, UInt32 baseSize)
{
    // This type will never appear in an object header on the heap (or otherwise be made available to the GC).
    // It is used only when signature matching generic type instantiations. Only a subset of the type fields
    // need to be filled in correctly as a result.
    m_usComponentSize = 0;
    m_usFlags = ParameterizedEEType;
    m_uBaseSize = baseSize;
    m_RelatedType.m_pRelatedParameterType = pElementType;
    m_usNumVtableSlots = 0;
    m_usNumInterfaces = 0;
}

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

#ifndef BINDER
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

// Retrieve the vtable slot number of the method that implements ICastableFlag.IsInstanceOfInterface for
// ICastable types.
inline PTR_Code EEType::get_ICastableIsInstanceOfInterfaceMethod()
{
    EEType * eeType = this;
    do
    {
        ASSERT(eeType->IsICastable());

        OptionalFields * pOptFields = eeType->get_OptionalFields();
        ASSERT(pOptFields);

        UInt16 uiSlot = pOptFields->GetICastableIsInstSlot(0xffff);
        if (uiSlot != 0xffff)
        {
            if (uiSlot < eeType->m_usNumVtableSlots)
                return this->get_Slot(uiSlot);
            else
                return eeType->get_SealedVirtualSlot(uiSlot - eeType->m_usNumVtableSlots);
        }
        eeType = eeType->get_BaseType();
    }
    while (eeType != NULL);

    ASSERT(!"get_ICastableIsInstanceOfInterfaceMethod");

    return NULL;
}

// Retrieve the vtable slot number of the method that implements ICastableFlag.GetImplType for ICastable
// types.
inline PTR_Code EEType::get_ICastableGetImplTypeMethod()
{
    EEType * eeType = this;

    do
    {
        ASSERT(eeType->IsICastable());

        OptionalFields * pOptFields = eeType->get_OptionalFields();
        ASSERT(pOptFields);

        UInt16 uiSlot = pOptFields->GetICastableGetImplTypeSlot(0xffff);
        if (uiSlot != 0xffff)
        {
            if (uiSlot < eeType->m_usNumVtableSlots)
                return this->get_Slot(uiSlot);
            else
                return eeType->get_SealedVirtualSlot(uiSlot - eeType->m_usNumVtableSlots);
        }
        eeType = eeType->get_BaseType();
    }
    while (eeType != NULL);

    ASSERT(!"get_ICastableGetImplTypeMethod");

    return NULL;
}

// Retrieve the value type T from a Nullable<T>.
inline EEType * EEType::GetNullableType()
{
    ASSERT(IsNullable());

    UInt32 cbNullableTypeOffset = GetFieldOffset(ETF_NullableType);

    // The type pointer may be indirected via the IAT if the type is defined in another module.
    if (IsNullableTypeViaIAT())
        return **(EEType***)((UInt8*)this + cbNullableTypeOffset);
    else
        return *(EEType**)((UInt8*)this + cbNullableTypeOffset);
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

inline void EEType::set_GenericDefinition(EEType *pTypeDef)
{
    ASSERT(IsGeneric());

    UInt32 cbOffset = GetFieldOffset(ETF_GenericDefinition);

    *(EEType**)((UInt8*)this + cbOffset) = pTypeDef;
}

inline EETypeRef & EEType::get_GenericDefinition()
{
    ASSERT(IsGeneric());

    UInt32 cbOffset = GetFieldOffset(ETF_GenericDefinition);

    return *(EETypeRef *)((UInt8*)this + cbOffset);
}

inline void EEType::set_GenericComposition(GenericComposition *pGenericComposition)
{
    ASSERT(IsGeneric());

    UInt32 cbOffset = GetFieldOffset(ETF_GenericComposition);

    *(GenericComposition **)((UInt8*)this + cbOffset) = pGenericComposition;
}

inline GenericComposition *EEType::get_GenericComposition()
{
    ASSERT(IsGeneric());

    UInt32 cbOffset = GetFieldOffset(ETF_GenericComposition);

    GenericComposition *pGenericComposition = *(GenericComposition **)((UInt8*)this + cbOffset);

    return pGenericComposition;
}

inline UInt32 EEType::get_GenericArity()
{
    GenericComposition *pGenericComposition = get_GenericComposition();

    return pGenericComposition->GetArity();
}

inline EETypeRef* EEType::get_GenericArguments()
{
    GenericComposition *pGenericComposition = get_GenericComposition();

    return pGenericComposition->GetArguments();
}

inline GenericVarianceType* EEType::get_GenericVariance()
{
    GenericComposition *pGenericComposition = get_GenericComposition();

    return pGenericComposition->GetVariance();
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

inline UInt8 ** EEType::get_DynamicGcStaticsPointer()
{
    UInt32 cbOffset = GetFieldOffset(ETF_DynamicGcStatics);

    return (UInt8**)((UInt8*)this + cbOffset);
}

inline void EEType::set_DynamicGcStatics(UInt8 *pStatics)
{
    UInt32 cbOffset = GetFieldOffset(ETF_DynamicGcStatics);

    *(UInt8**)((UInt8*)this + cbOffset) = pStatics;
}

inline UInt8 ** EEType::get_DynamicNonGcStaticsPointer()
{
    UInt32 cbOffset = GetFieldOffset(ETF_DynamicNonGcStatics);

    return (UInt8**)((UInt8*)this + cbOffset);
}

inline void EEType::set_DynamicNonGcStatics(UInt8 *pStatics)
{
    UInt32 cbOffset = GetFieldOffset(ETF_DynamicNonGcStatics);

    *(UInt8**)((UInt8*)this + cbOffset) = pStatics;
}

inline UInt32 EEType::get_DynamicThreadStaticOffset()
{
    UInt32 cbOffset = GetFieldOffset(ETF_DynamicThreadStaticOffset);

    return *(UInt32*)((UInt8*)this + cbOffset);
}

inline void EEType::set_DynamicThreadStaticOffset(UInt32 threadStaticOffset)
{
    UInt32 cbOffset = GetFieldOffset(ETF_DynamicThreadStaticOffset);

    *(UInt32*)((UInt8*)this + cbOffset) = threadStaticOffset;
}
#endif // !BINDER

#ifdef BINDER
// Determine whether a particular EEType will need optional fields. Binder only at the moment since it's
// less useful at runtime and far easier to specify in terms of a binder MethodTable.
/*static*/ inline bool EEType::RequiresOptionalFields(MethodTable * pMT)
{
    MethodTable * pElementMT = pMT->IsArray() ?
        ((ArrayClass*)pMT->GetClass())->GetApproxArrayElementTypeHandle().AsMethodTable() :
        NULL;

    bool fHasSealedVirtuals = pMT->GetNumVirtuals() < (pMT->GetNumVtableSlots() + pMT->GetNumAdditionalVtableSlots());
    return
        // Do we need a padding size for value types that could be unboxed?
        (pMT->IsValueTypeOrEnum() &&
            (((pMT->GetBaseSize() - SYNC_BLOCK_SKEW) - pMT->GetClass()->GetNumInstanceFieldBytes()) > 0)) ||
        // Do we need a alignment for value types?
        (pMT->IsValueTypeOrEnum() &&
            (pMT->GetClass()->GetAlignmentRequirement() != POINTER_SIZE)) ||
#ifdef _TARGET_ARM_
        // Do we need a rare flags field for a class or structure that requires 64-bit alignment on ARM?
        (pMT->GetClass()->GetAlignmentRequirement() > 4) ||
        (pMT->IsArray() && pElementMT->IsValueTypeOrEnum() && (pElementMT->GetClass()->GetAlignmentRequirement() > 4)) ||
        (pMT->IsHFA()) ||
#endif
        // Do we need a DispatchMap?
        (pMT->GetDispatchMap() != NULL && !pMT->GetDispatchMap()->IsEmpty()) ||
        // Do we need to cache ICastable method vtable slots?
        (pMT->IsICastable()) ||
        // Is the class a Nullable<T> instantiation (need to store the flag and possibly a field offset)?
        pMT->IsNullable() ||
        (pMT->HasStaticClassConstructor() && !pMT->HasEagerStaticClassConstructor() ||
        // need a rare flag to indicate presence of sealed virtuals
        fHasSealedVirtuals);
}
#endif

// Calculate the size of an EEType including vtable, interface map and optional pointers (though not any
// optional fields stored out-of-line). Does not include the size of GC series information.
/*static*/ inline UInt32 EEType::GetSizeofEEType(UInt32 cVirtuals,
                                                 UInt32 cInterfaces,
                                                 bool fHasFinalizer,
                                                 bool fRequiresOptionalFields,
                                                 bool fRequiresNullableType,
                                                 bool fHasSealedVirtuals,
                                                 bool fHasGenericInfo)
{
    // We don't support nullables with sealed virtuals at this time -
    // the issue is that if both the nullable eetype and the sealed virtuals may be present,
    // we need to detect the presence of at least one of them by looking at the EEType.
    // In the case of nullable, we'd need to fetch the rare flags, which is annoying,
    // an in the case of the sealed virtual slots, the information is implicit in the dispatch
    // map, which is even more annoying. 
    // So as long as nullables don't have sealed virtual slots, it's better to make that
    // an invariant and *not* test for nullable at run time.
    ASSERT(!(fRequiresNullableType && fHasSealedVirtuals));

    return offsetof(EEType, m_VTable)
        + (sizeof(UIntTarget) * cVirtuals)
        + (sizeof(EEInterfaceInfo) * cInterfaces)
        + (fHasFinalizer ? sizeof(UIntTarget) : 0)
        + (fRequiresOptionalFields ? sizeof(UIntTarget) : 0)
        + (fRequiresNullableType ? sizeof(UIntTarget) : 0)
        + (fHasSealedVirtuals ? sizeof(Int32) : 0)
        + (fHasGenericInfo ? sizeof(UIntTarget)*2 : 0);
}

#if !defined(BINDER) && !defined(DACCESS_COMPILE)
// get the base type of an array EEType - this is special because the base type of arrays is not explicitly
// represented - instead the classlib has a common one for all arrays
inline EEType * EEType::GetArrayBaseType()
{
    RuntimeInstance * pRuntimeInstance = GetRuntimeInstance();
    Module * pModule = NULL;
    if (pRuntimeInstance->IsInStandaloneExeMode())
    {
        // With dynamically created types, there is no home module to use to find System.Array. That's okay
        // for now, but when we support multi-module, we'll have to do something more clever here.
        pModule = pRuntimeInstance->GetStandaloneExeModule();
    }
    else
    {
        EEType *pEEType = this;
        if (pEEType->IsDynamicType())
            pEEType = pEEType->get_DynamicTemplateType();
        pModule = GetRuntimeInstance()->FindModuleByReadOnlyDataAddress(pEEType);
    }
    EEType * pArrayBaseType = pModule->GetArrayBaseType();
    return pArrayBaseType;
}
#endif // !defined(BINDER) && !defined(DACCESS_COMPILE)

#ifdef BINDER
// Version of the above usable from the binder where all the type layout information can be gleaned from a
// MethodTable.
/*static*/ inline UInt32 EEType::GetSizeofEEType(MethodTable *pMT, bool fHasGenericInfo)
{
    bool fHasSealedVirtuals = pMT->GetNumVirtuals() < (pMT->GetNumVtableSlots() + pMT->GetNumAdditionalVtableSlots());
    return GetSizeofEEType(pMT->IsInterface() ? (pMT->HasPerInstInfo() ? 1 : 0) : pMT->GetNumVirtuals(),
                           pMT->GetNumInterfaces(),
                           pMT->HasFinalizer(),
                           EEType::RequiresOptionalFields(pMT),
                           pMT->IsNullable(),
                           fHasSealedVirtuals,
                           fHasGenericInfo);
}
#endif // BINDER

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

    // Followed by the pointer to the type target of a Nullable<T>.
    if (eField == ETF_NullableType)
    {
#ifndef BINDER
        ASSERT(IsNullable());
#endif
        return cbOffset;
    }

    // OR, followed by the pointer to the sealed virtual slots
    if (eField == ETF_SealedVirtualSlots)
        return cbOffset;

    // Binder does not use DynamicTemplateType
#ifndef BINDER
    UInt32 rareFlags = get_RareFlags();
    if (IsNullable() || ((rareFlags & IsDynamicTypeWithSealedVTableEntriesFlag) != 0))
        cbOffset += sizeof(UIntTarget);

    // in the case of sealed vtable entries on static types, we have a UInt sized relative pointer
    if (rareFlags & HasSealedVTableEntriesFlag)
        cbOffset += sizeof(UInt32);

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
        cbOffset += sizeof(UIntTarget);

    if (eField == ETF_GenericComposition)
    {
        ASSERT(IsGeneric());
        return cbOffset;
    }
    if (IsGeneric())
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
#endif // !BINDER

    ASSERT(!"Unknown EEType field type");
    return 0;
}

#ifdef BINDER
// Version of the above usable from the binder where all the type layout information can be gleaned from a
// MethodTable.
/*static*/ inline UInt32 EEType::GetFieldOffset(EETypeField eField,
                                                MethodTable * pMT)
{
    UInt32 numVTableSlots = pMT->IsInterface() ? (pMT->HasPerInstInfo() ? 1 : 0) : pMT->GetNumVirtuals();

    // First part of EEType consists of the fixed portion followed by the vtable.
    UInt32 cbOffset = offsetof(EEType, m_VTable) + (sizeof(UIntTarget) * numVTableSlots);

    // Then we have the interface map.
    if (eField == ETF_InterfaceMap)
    {
        return cbOffset;
    }
    cbOffset += sizeof(EEInterfaceInfo) * pMT->GetNumInterfaces();

    // Followed by the pointer to the finalizer method.
    if (eField == ETF_Finalizer)
    {
        return cbOffset;
    }
    if (pMT->HasFinalizer())
        cbOffset += sizeof(UIntTarget);

    // Followed by the pointer to the optional fields.
    if (eField == ETF_OptionalFieldsPtr)
    {
        return cbOffset;
    }
    if (EEType::RequiresOptionalFields(pMT))
        cbOffset += sizeof(UIntTarget);

    // Followed by the pointer to the type target of a Nullable<T>.
    if (eField == ETF_NullableType)
    {
        return cbOffset;
    }

    // OR, followed by the pointer to the sealed virtual slots
    bool fHasSealedVirtuals = pMT->GetNumVirtuals() < (pMT->GetNumVtableSlots() + pMT->GetNumAdditionalVtableSlots());
    if (eField == ETF_SealedVirtualSlots)
    {
        ASSERT(fHasSealedVirtuals);
        return cbOffset;
    }

    if (fHasSealedVirtuals)
    {
        ASSERT(!pMT->IsNullable());
        cbOffset += sizeof(UInt32);
    }

    if (pMT->IsNullable())
    {
        ASSERT(!fHasSealedVirtuals);
        cbOffset += sizeof(UIntTarget);
    }

    if (pMT->HasPerInstInfo())
    {
        if (eField == ETF_GenericDefinition)
        {
            return cbOffset;
        }
        cbOffset += sizeof(UIntTarget);

        if (eField == ETF_GenericComposition)
        {
            return cbOffset;
        }
    }

    // Binder does not use DynamicTemplateType

    ASSERT(!"Unknown EEType field type");
    return 0;
}
#endif

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

inline void GenericComposition::SetVariance(UInt32 index, GenericVarianceType variance)
{
    ASSERT(index < m_arity);
    GenericVarianceType *pVariance = GetVariance();
    pVariance[index] = variance;
}

#endif // __eetype_inl__
