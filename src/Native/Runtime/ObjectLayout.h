// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//
// Low-level types describing GC object layouts.
//

// Bits stolen from the sync block index that the GC/HandleTable knows about (currently these are at the same
// positions as the mainline runtime but we can change this below when it becomes apparent how Redhawk will
// handle sync blocks).
#define BIT_SBLK_GC_RESERVE                 0x20000000
#define BIT_SBLK_FINALIZER_RUN              0x40000000

// The sync block index header (small structure that immediately precedes every object in the GC heap). Only
// the GC uses this so far, and only to store a couple of bits of information.
class ObjHeader
{
private:
#if defined(HOST_64BIT)
    UInt32   m_uAlignpad;
#endif // HOST_64BIT
    UInt32   m_uSyncBlockValue;

public:
    UInt32 GetBits() { return m_uSyncBlockValue; }
    void SetBit(UInt32 uBit);
    void ClrBit(UInt32 uBit);
    void SetGCBit() { m_uSyncBlockValue |= BIT_SBLK_GC_RESERVE; }
    void ClrGCBit() { m_uSyncBlockValue &= ~BIT_SBLK_GC_RESERVE; }
};

//-------------------------------------------------------------------------------------------------
static UIntNative const SYNC_BLOCK_SKEW  = sizeof(void *);

class EEType;
typedef DPTR(class EEType) PTR_EEType;
class MethodTable;

//-------------------------------------------------------------------------------------------------
class Object
{
    friend class AsmOffsets;

    PTR_EEType  m_pEEType;
public:  
    EEType * get_EEType() const
        { return m_pEEType; }
    EEType * get_SafeEEType() const
        { return dac_cast<PTR_EEType>((dac_cast<TADDR>(m_pEEType)) & ~((UIntNative)3)); }
    ObjHeader * GetHeader() { return dac_cast<DPTR(ObjHeader)>(dac_cast<TADDR>(this) - SYNC_BLOCK_SKEW); }
#ifndef DACCESS_COMPILE
    void set_EEType(EEType * pEEType)
        { m_pEEType = pEEType; }
    void InitEEType(EEType * pEEType);

    size_t GetSize();
#endif

    //
    // Adapter methods for GC code so that GC and runtime code can use the same type.  
    // These methods are deprecated -- only use from existing GC code.
    //
    MethodTable * RawGetMethodTable() const
    {
        return (MethodTable*)get_EEType();
    }
    MethodTable * GetGCSafeMethodTable() const
    {
        return (MethodTable *)get_SafeEEType();
    }
    void RawSetMethodTable(MethodTable * pMT)
    {
        m_pEEType = PTR_EEType((EEType *)pMT);
    }
    ////// End adaptor methods 
};
typedef DPTR(Object) PTR_Object;
typedef DPTR(PTR_Object) PTR_PTR_Object;

//-------------------------------------------------------------------------------------------------
static UIntNative const MIN_OBJECT_SIZE  = (2 * sizeof(void*)) + sizeof(ObjHeader);

//-------------------------------------------------------------------------------------------------
static UIntNative const REFERENCE_SIZE   = sizeof(Object *);

//-------------------------------------------------------------------------------------------------
class Array : public Object
{
    friend class ArrayBase;
    friend class AsmOffsets;

    UInt32       m_Length;
#if defined(HOST_64BIT)
    UInt32       m_uAlignpad;
#endif // HOST_64BIT
public:  
    UInt32 GetArrayLength();
    void InitArrayLength(UInt32 length);
    void* GetArrayData();
};
typedef DPTR(Array) PTR_Array;

//-------------------------------------------------------------------------------------------------
class String : public Object
{
    friend class AsmOffsets;
    friend class StringConstants;

    UInt32       m_Length;
    UInt16       m_FirstChar;
};
typedef DPTR(String) PTR_String;

//-------------------------------------------------------------------------------------------------
class StringConstants
{
public:
    static UIntNative const ComponentSize = sizeof(((String*)0)->m_FirstChar);
    static UIntNative const BaseSize = sizeof(ObjHeader) + offsetof(String, m_FirstChar) + ComponentSize;
};

//-------------------------------------------------------------------------------------------------
static UIntNative const STRING_COMPONENT_SIZE = StringConstants::ComponentSize;

//-------------------------------------------------------------------------------------------------
static UIntNative const STRING_BASE_SIZE = StringConstants::BaseSize;

//-------------------------------------------------------------------------------------------------
static UIntNative const MAX_STRING_LENGTH = 0x3FFFFFDF;
