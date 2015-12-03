//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

// Fundamental runtime type representation

#pragma warning(push)
#pragma warning(disable:4200) // nonstandard extension used : zero-sized array in struct/union
//-------------------------------------------------------------------------------------------------
// Forward declarations

class MdilModule;
class EEType;
class OptionalFields;

//-------------------------------------------------------------------------------------------------
// Array of these represents the interfaces implemented by a type

class EEInterfaceInfo
{
    friend class MdilModule;

  public:
    EEType * GetInterfaceEEType()
    {
        return ((UIntTarget)m_pInterfaceEEType & ((UIntTarget)1)) ?
               *(EEType**)((UIntTarget)m_ppInterfaceEETypeViaIAT & ~((UIntTarget)1)) :
               m_pInterfaceEEType;
    }

    // If the interface type is referenced indirectly (via the IAT) update this info to a direct reference.
    // This is only possible at runtime once the IAT has been updated and is currently used only for generics,
    // when unifying a generic instantiation and cutting any arbitrary dependencies to the module which first
    // published this instantiation.
    void Flatten()
    {
        m_pInterfaceEEType = GetInterfaceEEType();
    }

#ifndef RHDUMP
  private:
#endif
    union
    {
        EEType *    m_pInterfaceEEType;         // m_uFlags == InterfaceFlagNormal
        EEType **   m_ppInterfaceEETypeViaIAT;  // m_uFlags == InterfaceViaIATFlag
#if defined(RHDUMP) || defined(BINDER)
        UIntTarget  m_ptrVal;  // ensure this structure is the right size in cross-build scenarios
#endif // defined(RHDUMP) || defined(BINDER)
    };
};

//-------------------------------------------------------------------------------------------------
class EEInterfaceInfoMap
{
    friend class EEType;

  public:
    EEInterfaceInfoMap(EEInterfaceInfoMap const & other)
        : m_pMap(NULL), m_cMap(0)
    {
        UNREFERENCED_PARAMETER(other);
    }

    EEInterfaceInfo & operator[](UInt16 idx);

    typedef EEInterfaceInfo * Iterator;

    UIntNative GetLength()
        { return m_cMap; }

    Iterator Begin()
        { return Iterator(m_pMap); }

    Iterator BeginAt(UInt16 idx)
        { return Iterator(&operator[](idx)); }

    Iterator End()
        { return Iterator(m_pMap + m_cMap); }

    EEInterfaceInfo * GetRawPtr()
        { return m_pMap; }

  private:
    EEInterfaceInfoMap(EEInterfaceInfo * pMap, UInt16 cMap)
        : m_pMap(pMap), m_cMap(cMap)
        {}

    EEInterfaceInfo * m_pMap;
    UInt16            m_cMap;
};

//-------------------------------------------------------------------------------------------------
// use a non-compressed encoding for easier debugging for now...

struct DispatchMapEntry
{
    UInt16    m_usInterfaceIndex;
    UInt16    m_usInterfaceMethodSlot;
    UInt16    m_usImplMethodSlot;
};

//-------------------------------------------------------------------------------------------------
// Represents the contributions that a type makes to its interface implementations.
class DispatchMap
{
    friend class CompactTypeBuilder;
    friend class MdilModule;
#ifdef RHDUMP
    friend struct Image;
#endif
private:
    UInt32           m_entryCount;
    DispatchMapEntry m_dispatchMap[0]; // at least one entry if any interfaces defined
public:
    bool IsEmpty()
        { return m_entryCount == 0; }

    size_t ComputeSize()
        { return sizeof(m_entryCount) + sizeof(m_dispatchMap[0])*m_entryCount; }

    typedef DispatchMapEntry * Iterator;

    Iterator Begin()
        { return &m_dispatchMap[0]; }

    Iterator End()
        { return &m_dispatchMap[m_entryCount]; }
};

#if !defined(BINDER)
//-------------------------------------------------------------------------------------------------
// The subset of CLR-style CorElementTypes that Redhawk knows about at runtime (just the primitives and a
// special case for ELEMENT_TYPE_ARRAY used to mark the System.Array EEType).
enum CorElementType : UInt8
{
    ELEMENT_TYPE_END        = 0x0,

    ELEMENT_TYPE_BOOLEAN    = 0x2,
    ELEMENT_TYPE_CHAR       = 0x3,
    ELEMENT_TYPE_I1         = 0x4,
    ELEMENT_TYPE_U1         = 0x5,
    ELEMENT_TYPE_I2         = 0x6,
    ELEMENT_TYPE_U2         = 0x7,
    ELEMENT_TYPE_I4         = 0x8,
    ELEMENT_TYPE_U4         = 0x9,
    ELEMENT_TYPE_I8         = 0xa,
    ELEMENT_TYPE_U8         = 0xb,
    ELEMENT_TYPE_R4         = 0xc,
    ELEMENT_TYPE_R8         = 0xd,

    ELEMENT_TYPE_ARRAY      = 0x14,

    ELEMENT_TYPE_I          = 0x18,
    ELEMENT_TYPE_U          = 0x19,
};
#endif // !BINDER

//-------------------------------------------------------------------------------------------------
// Support for encapsulating the location of fields in the EEType that have variable offsets or may be
// optional.
//
// The following enumaration gives symbolic names for these fields and is used with the GetFieldPointer() and
// GetFieldOffset() APIs.
enum EETypeField
{
    ETF_InterfaceMap,
    ETF_Finalizer,
    ETF_OptionalFieldsPtr,
    ETF_NullableType,
    ETF_SealedVirtualSlots,
    ETF_DynamicTemplateType,
    ETF_DynamicDispatchMap,
};

//-------------------------------------------------------------------------------------------------
// Fundamental runtime type representation
#ifndef RHDUMP
typedef DPTR(class EEType) PTR_EEType;
typedef DPTR(PTR_EEType) PTR_PTR_EEType;
typedef DPTR(class OptionalFields) PTR_OptionalFields;
typedef DPTR(PTR_OptionalFields) PTR_PTR_OptionalFields;
#endif // !RHDUMP
class EEType
{
    friend class AsmOffsets;
    friend class MdilModule;
    friend class MetaDataEngine;
    friend class LimitedEEType;

#ifdef RHDUMP
public:
#else
private:
#endif
    struct RelatedTypeUnion
    {
        union 
        {
            // Kinds.CanonicalEEType
            EEType*     m_pBaseType;
            EEType**    m_ppBaseTypeViaIAT;

            // Kinds.ClonedEEType
            EEType** m_ppCanonicalTypeViaIAT;

            // Kinds.ParameterizedEEType
            EEType*  m_pRelatedParameterType;
            EEType** m_ppRelatedParameterTypeViaIAT;

#if defined(RHDUMP) || defined(BINDER)
            UIntTarget m_ptrVal;  // ensure this structure is the right size in cross-build scenarios
#endif // defined(RHDUMP) || defined(BINDER)
        };
    };

    UInt16              m_usComponentSize;
    UInt16              m_usFlags;
    UInt32              m_uBaseSize;
    RelatedTypeUnion    m_RelatedType;
    UInt16              m_usNumVtableSlots;
    UInt16              m_usNumInterfaces;
    UInt32              m_uHashCode;

    TgtPTR_Void         m_VTable[];  // make this explicit so the binder gets the right alignment

    // after the m_usNumVtableSlots vtable slots, we have m_usNumInterfaces slots of 
    // EEInterfaceInfo, and after that a couple of additional pointers based on whether the type is
    // finalizable (the address of the finalizer code) or has optional fields (pointer to the compacted
    // fields).

    enum Flags
    {
        // There are four kinds of EETypes, the three of them regular types that use the full EEType encoding
        // plus a fourth kind used as a grab bag of unusual edge cases which are encoded in a smaller,
        // simplified version of EEType. See LimitedEEType definition below.
        EETypeKindMask = 0x0003,

        // This flag is set when m_pRelatedType is in a different module.  In that case, m_pRelatedType
        // actually points to a 'fake' EEType whose m_pRelatedType field lines up with an IAT slot in this
        // module, which then points to the desired EEType.  In other words, there is an extra indirection
        // through m_pRelatedType to get to the related type in the other module.
        RelatedTypeViaIATFlag   = 0x0004,

        // This EEType represents a value type
        ValueTypeFlag           = 0x0008,

        // This EEType represents a type which requires finalization
        HasFinalizerFlag        = 0x0010,

        // This type contain gc pointers
        HasPointersFlag         = 0x0020,

        // This type instance was allocated at runtime (rather than being embedded in a module image)
        RuntimeAllocatedFlag    = 0x0040,

        // This type is generic and one or more of it's type parameters is co- or contra-variant. This only
        // applies to interface and delegate types.
        GenericVarianceFlag     = 0x0080,

        // This type has optional fields present.
        OptionalFieldsFlag      = 0x0100,

        // This EEType represents an interface.
        IsInterfaceFlag         = 0x0200,

        // This type is generic.
        IsGenericFlag           = 0x0400,

        // We are storing a CorElementType in the upper bits for unboxing enums
        CorElementTypeMask      = 0xf800,
        CorElementTypeShift     = 11,
    };

public:

    // These are flag values that are rarely set for types. If any of them are set then an optional field will
    // be associated with the EEType to represent them.
    enum RareFlags
    {
        // This type requires 8-byte alignment for its fields on certain platforms (only ARM currently).
        RequiresAlign8Flag      = 0x00000001,

        // Type implements ICastable to allow dynamic resolution of interface casts.
        ICastableFlag           = 0x00000002,

        // Type is an instantiation of Nullable<T>.
        IsNullableFlag          = 0x00000004,

        // Nullable target type stashed in the EEType is indirected via the IAT.
        NullableTypeViaIATFlag  = 0x00000008,

        // This EEType was created by dynamic type loader
        IsDynamicTypeFlag       = 0x00000010,

        // This EEType has a Class Constructor
        HasCctorFlag            = 0x0000020,

        // This EEType has sealed vtable entries (note that this flag is only used for
        // dynamically created types because they always have an optional field (hence the
        // very explicit flag name).
        IsDynamicTypeWithSealedVTableEntriesFlag    = 0x00000040,

        // This EEType was constructed from a universal canonical template, and has
        // its own dynamically created DispatchMap (does not use the DispatchMap of its template type)
        HasDynamicallyAllocatedDispatchMapFlag = 0x00000080,

        // This EEType represents a structure that is an HFA (only ARM currently)
        IsHFAFlag               = 0x00000100,
    };

    // These masks and paddings have been chosen so that the ValueTypePadding field can always fit in a byte of data.
    // if the alignment is 8 bytes or less. If the alignment is higher then there may be a need for more bits to hold
    // the rest of the padding data.
    // If paddings of greater than 7 bytes are necessary, then the high bits of the field represent that padding
    enum ValueTypePaddingConstants
    {
        ValueTypePaddingLowMask = 0x7,
        ValueTypePaddingHighMask = 0xFFFFFF00ul,
        ValueTypePaddingMax = 0x07FFFFFF,
        ValueTypePaddingHighShift = 8,
        ValueTypePaddingAlignmentMask = 0xF8,
        ValueTypePaddingAlignmentShift = 3,
    };

public:

    enum Kinds
    {
        CanonicalEEType         = 0x0000,
        ClonedEEType            = 0x0001,
        ParameterizedEEType     = 0x0002,
        GenericTypeDefEEType    = 0x0003,
    };

#ifndef RHDUMP
    UInt32 get_BaseSize()
        { return m_uBaseSize; }

    UInt16 get_ComponentSize()
        { return m_usComponentSize; }

    PTR_Code get_Slot(UInt16 slotNumber);

    PTR_PTR_Code get_SlotPtr(UInt16 slotNumber);

    PTR_Code get_SealedVirtualSlot(UInt16 slotNumber);
    void set_SealedVirtualSlot(PTR_Code pValue, UInt16 slotNumber);

    Kinds get_Kind();

    bool IsCloned()
        { return get_Kind() == ClonedEEType; }

    bool IsRelatedTypeViaIAT()
        { return ((m_usFlags & (UInt16)RelatedTypeViaIATFlag) != 0); }

    bool IsArray()
        { return IsParameterizedType() && get_ParameterizedTypeShape() != 0; }

    bool IsPointerType()
        { return IsParameterizedType() && get_ParameterizedTypeShape() == 0; }

    bool IsParameterizedType()
        { return (get_Kind() == ParameterizedEEType); }

    bool IsGenericTypeDefinition()
        { return (get_Kind() == GenericTypeDefEEType); }

    bool IsCanonical()
        { return get_Kind() == CanonicalEEType; }

    bool IsInterface()
        { return ((m_usFlags & (UInt16)IsInterfaceFlag) != 0); }

    EEType * get_CanonicalEEType();

    EEType * get_BaseType();

    EEType * get_RelatedParameterType();

    // A parameterized type shape is 0 to indicate that it is a pointer type, 
    // and non-zero to indicate that it is an array type
    UInt32 get_ParameterizedTypeShape() { return m_uBaseSize; }

    void set_RelatedParameterType(EEType * pParameterType);

    bool get_IsValueType()
        { return ((m_usFlags & (UInt16)ValueTypeFlag) != 0); }

    bool HasFinalizer()
    {
        return (m_usFlags & HasFinalizerFlag) != 0;
    }

    bool HasReferenceFields()
    {
        return (m_usFlags & HasPointersFlag) != 0;
    }

    bool HasOptionalFields()
    {
        return (m_usFlags & OptionalFieldsFlag) != 0;
    }

    bool IsEquivalentTo(EEType * pOtherEEType)
    {
        if (this == pOtherEEType)
            return true;

        EEType * pThisEEType = this;

        if (pThisEEType->IsCloned())
            pThisEEType = pThisEEType->get_CanonicalEEType();

        if (pOtherEEType->IsCloned())
            pOtherEEType = pOtherEEType->get_CanonicalEEType();

        if (pThisEEType == pOtherEEType)
            return true;

        if (pThisEEType->IsParameterizedType() && pOtherEEType->IsParameterizedType())
        {
            return pThisEEType->get_RelatedParameterType()->IsEquivalentTo(pOtherEEType->get_RelatedParameterType()) &&
                pThisEEType->get_ParameterizedTypeShape() == pOtherEEType->get_ParameterizedTypeShape();
        }

        return false;
    }

    // How many vtable slots are there?
    UInt16 GetNumVtableSlots()
        { return m_usNumVtableSlots; }
    void SetNumVtableSlots(UInt16 usNumSlots)
        { m_usNumVtableSlots = usNumSlots; }

    // How many entries are in the interface map after the vtable slots?
    UInt16 GetNumInterfaces()
        { return m_usNumInterfaces; }

    // Does this class (or its base classes) implement any interfaces?
    bool HasInterfaces()
        { return GetNumInterfaces() != 0; }
        
    EEInterfaceInfoMap GetInterfaceMap();

    bool HasDispatchMap();

    bool IsGeneric()
        { return (m_usFlags & IsGenericFlag) != 0; }

#ifndef BINDER
    DispatchMap *GetDispatchMap();

#endif // !BINDER

    // Used only by GC initialization, this initializes the EEType used to mark free entries in the GC heap.
    // It should be an array type with a component size of one (so the GC can easily size it as appropriate)
    // and should be marked as not containing any references. The rest of the fields don't matter: the GC does
    // not query them and the rest of the runtime will never hold a reference to free object.
    inline void InitializeAsGcFreeType();

    // Initialize an existing EEType as an array type with specific element type. This is another specialized
    // method used only during the unification of generic instantiation types. It might need modification if
    // needed in any other scenario.
    inline void InitializeAsArrayType(EEType * pElementType, UInt32 baseSize);

#ifdef DACCESS_COMPILE
    bool DacVerify();
    static bool DacVerifyWorker(EEType* pThis);
#endif // DACCESS_COMPILE


    // Transform a canonical type into a cloned type pointing to the given type as the canonical type. Used
    // when unifying generic instantiation types.
    inline void MakeClonedType(EEType ** ppCanonicalType);

    // If any part of this type is referenced indirectly (via IAT entries) resolve these references to direct
    // pointers. This is only possible at runtime once the IAT has been updated and is currently used only for generics,
    // when unifying a generic instantiation and cutting any arbitrary dependencies to the module which first
    // published this instantiation.
    inline void Flatten();

    // Mark or determine that a type instance was allocated at runtime (currently only used for unification of
    // generic instantiations). This is sometimes important for memory management or debugging purposes.
    bool IsRuntimeAllocated()
        { return (m_usFlags & RuntimeAllocatedFlag) != 0; }
    void SetRuntimeAllocated()
        { m_usFlags |= RuntimeAllocatedFlag; }

    // Mark or determine that a type is generic and one or more of it's type parameters is co- or
    // contra-variant. This only applies to interface and delegate types.
    bool HasGenericVariance()
        { return (m_usFlags & GenericVarianceFlag) != 0; }
    void SetHasGenericVariance()
        { m_usFlags |= GenericVarianceFlag; }

    // Is this type specialized System.Object? We use the fact that only System.Object and interfaces have no
    // parent type.
    bool IsSystemObject()
        { return !IsParameterizedType() && !IsInterface() && get_BaseType() == NULL; }

    CorElementType GetCorElementType()
        { return (CorElementType)((m_usFlags & CorElementTypeMask) >> CorElementTypeShift); }

    // Is this type specifically System.Array?
    bool IsSystemArray()
        { return GetCorElementType() == ELEMENT_TYPE_ARRAY; }

#ifndef BINDER
    // Determine whether a type requires 8-byte alignment for its fields (required only on certain platforms,
    // only ARM so far).
    bool RequiresAlign8()
        { return (get_RareFlags() & RequiresAlign8Flag) != 0; }

    // Determine whether a type supports ICastable.
    bool IsICastable()
        { return (get_RareFlags() & ICastableFlag) != 0; }

    // Retrieve the address of the method that implements ICastable.IsInstanceOfInterface for
    // ICastable types.
    inline PTR_Code get_ICastableIsInstanceOfInterfaceMethod();

    // Retrieve the vtable slot number of the method that implements ICastable.GetImplType for ICastable
    // types.
    inline PTR_Code get_ICastableGetImplTypeMethod();

    // Determine whether a type is an instantiation of Nullable<T>.
    bool IsNullable()
        { return (get_RareFlags() & IsNullableFlag) != 0; }

    // Indicates whether the target type associated with a Nullable<T> instantiation is indirected via the
    // IAT.
    bool IsNullableTypeViaIAT()
        { return (get_RareFlags() & NullableTypeViaIATFlag) != 0; }

    // Retrieve the value type T from a Nullable<T>.
    EEType * GetNullableType();

    // Set the value of type T for dynamic instantiations of Nullable<T>
    void SetNullableType(EEType * pEEType);

    // Retrieve the offset of the value embedded in a Nullable<T>.
    UInt8 GetNullableValueOffset();

    // Determine whether a type was created by dynamic type loader
    bool IsDynamicType()
        { return (get_RareFlags() & IsDynamicTypeFlag) != 0; }

    // Determine whether a *dynamic* type has a dynamically allocated DispatchMap
    bool HasDynamicallyAllocatedDispatchMap()
        { return (get_RareFlags() & HasDynamicallyAllocatedDispatchMapFlag) != 0; }

    // Retrieve template used to create the dynamic type
    EEType * get_DynamicTemplateType();
    void set_DynamicTemplateType(EEType * pTemplate);

    void SetHashCode(UInt32 value);
    UInt32 GetHashCode();

    // Retrieve optional fields associated with this EEType. May be NULL if no such fields exist.
    inline PTR_OptionalFields get_OptionalFields();
    void set_OptionalFields(OptionalFields * pOptionalFields);

    // Retrieve the amount of padding added to value type fields in order to align them for boxed allocation
    // on the GC heap. This value to can be used along with the result of get_BaseSize to determine the size
    // of a value type embedded in the stack, and array or another type.
    inline UInt32 get_ValueTypeFieldPadding();

    // Retrieve the alignment of this valuetype
    inline UInt32 get_ValueTypeFieldAlignment();

    // Get flags that are less commonly set on EETypes.
    inline UInt32 get_RareFlags();
#endif // !BINDER

    static inline UInt32 ComputeValueTypeFieldPaddingFieldValue(UInt32 padding, UInt32 alignment);

    // Helper methods that deal with EEType topology (size and field layout). These are useful since as we
    // optimize for pay-for-play we increasingly want to customize exactly what goes into an EEType on a
    // per-type basis. The rules that govern this can be both complex and volatile and we risk sprinkling
    // various layout rules through the binder and runtime that obscure the basic meaning of the code and are
    // brittle: easy to overlook when one of the rules changes.
    //
    // The following methods can in some cases have fairly complex argument lists of their own and in that way
    // they expose more of the implementation details than we'd ideally like. But regardless they still serve
    // an arguably more useful purpose: they identify all the places that rely on the EEType layout. As we
    // change layout rules we might have to change the arguments to the methods below but in doing so we will
    // instantly identify all the other parts of the binder and runtime that need to be updated.

#ifdef BINDER
    // Determine whether a particular EEType will need optional fields. Binder only at the moment since it's
    // less useful at runtime and far easier to specify in terms of a binder MethodTable.
    static inline bool RequiresOptionalFields(MethodTable * pMT);
#endif

    // Calculate the size of an EEType including vtable, interface map and optional pointers (though not any
    // optional fields stored out-of-line). Does not include the size of GC series information.
    static inline UInt32 GetSizeofEEType(UInt32 cVirtuals,
                                         UInt32 cInterfaces,
                                         bool fHasFinalizer,
                                         bool fRequiresOptionalFields,
                                         bool fRequiresNullableType,
                                         bool fHasSealedVirtuals);

#ifdef BINDER
    // Version of the above usable from the binder where all the type layout information can be gleaned from a
    // MethodTable.
    static inline UInt32 GetSizeofEEType(MethodTable *pMT);
#endif // BINDER

    // Calculate the offset of a field of the EEType that has a variable offset.
    inline UInt32 GetFieldOffset(EETypeField eField);

#ifdef BINDER
    // Version of the above usable from the binder where all the type layout information can be gleaned from a
    // MethodTable.
    static inline UInt32 GetFieldOffset(EETypeField eField,
                                        MethodTable * pMT);
#endif // BINDER

#ifndef BINDER
    // Validate an EEType extracted from an object.
    bool Validate(bool assertOnFail = true);
#endif // !BINDER

#if !defined(BINDER) && !defined(DACCESS_COMPILE)
    // get the base type of an array EEType - this is special because the base type of arrays is not explicitly
    // represented - instead the classlib has a common one for all arrays
    EEType * GetArrayBaseType();
#endif // !defined(BINDER) && !defined(DACCESS_COMPILE)

#endif // !RHDUMP
};

#pragma warning(pop)

#include "OptionalFields.h"
