    // Licensed to the .NET Foundation under one or more agreements.
    // The .NET Foundation licenses this file to you under the MIT license.
    // See the LICENSE file in the project root for more information.

    //
    // **** This is an automatically generated file. Do not edit by hand. ****
    // Instead see GenericInstanceDescFields.src and the WriteOptionalFieldsCode.pl script.
    //
    // This file defines accessors for various optional fields. Each field has three methods defined:
    //    UInt32 Get<field>Offset()
    //    T Get<field>()
    //    void Set<field>(T value)
    //
    // If the field is an array an additional UInt32 index parameter is added to each of these.
    //
    // The following fields are handled in this header:
    //
    //   If GID_Instantiation flag is set:
    //     EEType                       : TgtPTR_EEType
    //     Arity                        : UInt32
    //     GenericTypeDef               : EETypeRef
    //     ParameterType                : EETypeRef[Arity]
    //
    //   If GID_Variance flag is set:
    //     ParameterVariance            : GenericVarianceType[Arity]
    //
    //   If GID_GcStaticFields flag is set:
    //     GcStaticFieldData            : TgtPTR_UInt8
    //     GcStaticFieldDesc            : TgtPTR_StaticGcDesc
    //
    //   If GID_GcRoots flag is set:
    //     NextGidWithGcRoots           : TgtPTR_GenericInstanceDesc
    //
    //   If GID_Unification flag is set:
    //     SizeOfNonGcStaticFieldData   : UInt32
    //     SizeOfGcStaticFieldData      : UInt32
    //
    //   If GID_ThreadStaticFields flag is set:
    //     ThreadStaticFieldTlsIndex    : UInt32
    //     ThreadStaticFieldStartOffset : UInt32
    //     ThreadStaticFieldDesc        : TgtPTR_StaticGcDesc
    //
    //   If GID_NonGcStaticFields flag is set:
    //     NonGcStaticFieldData         : TgtPTR_UInt8
    //
    // Additionally two variants of a method to calculate the byte size of a GenericInstanceDesc are provided.
    // A static version which determines the size from its arguments and an instance version which needs no
    // arguments.
    //

    enum _OptionalFieldTypes : UInt8
    {
        GID_NoFields           = 0x0,
        GID_Instantiation      = 0x1,
        GID_Variance           = 0x2,
        GID_GcStaticFields     = 0x4,
        GID_GcRoots            = 0x8,
        GID_Unification        = 0x10,
        GID_ThreadStaticFields = 0x20,
        GID_NonGcStaticFields  = 0x40,
        GID_AllFields          = 0x7f
    };
    typedef UInt8 OptionalFieldTypes;

    OptionalFieldTypes m_Flags;

    void Init(OptionalFieldTypes flags) { m_Flags = flags; }
    OptionalFieldTypes GetFlags() { return m_Flags; }

    bool HasInstantiation() { return (m_Flags & GID_Instantiation) != 0; }
    bool HasVariance() { return (m_Flags & GID_Variance) != 0; }
    bool HasGcStaticFields() { return (m_Flags & GID_GcStaticFields) != 0; }
    bool HasGcRoots() { return (m_Flags & GID_GcRoots) != 0; }
    bool HasUnification() { return (m_Flags & GID_Unification) != 0; }
    bool HasThreadStaticFields() { return (m_Flags & GID_ThreadStaticFields) != 0; }
    bool HasNonGcStaticFields() { return (m_Flags & GID_NonGcStaticFields) != 0; }

    static UInt32 GetSize(OptionalFieldTypes flags, UInt32 arity)
    {
        return GetBaseSize(flags) + ((flags & GID_Instantiation) ? ((sizeof(EETypeRef) * arity)) : 0) + ((flags & GID_Variance) ? ((sizeof(GenericVarianceType) * arity)) : 0);
    }

    UInt32 GetSize()
    {
        return GetBaseSize(m_Flags) + ((m_Flags & GID_Instantiation) ? ((sizeof(EETypeRef) * GetArity())) : 0) + ((m_Flags & GID_Variance) ? ((sizeof(GenericVarianceType) * GetArity())) : 0);
    }

    UInt32 GetEETypeOffset()
    {
        ASSERT(HasInstantiation());
        return sizeof(UInt8);
    }

    TgtPTR_EEType GetEEType()
    {
        ASSERT(HasInstantiation());
        return *dac_cast<DPTR(TgtPTR_EEType)>(dac_cast<TADDR>(this) + GetEETypeOffset());
    }

#ifndef DACCESS_COMPILE
    void SetEEType(TgtPTR_EEType value)
    {
        ASSERT(HasInstantiation());
        *(TgtPTR_EEType*)((UInt8*)this + GetEETypeOffset()) = value;
    }
#endif

    UInt32 GetArityOffset()
    {
        ASSERT(HasInstantiation());
        return sizeof(UInt8) + sizeof(TgtPTR_EEType);
    }

    UInt32 GetArity()
    {
        ASSERT(HasInstantiation());
        return *dac_cast<DPTR(UInt32)>(dac_cast<TADDR>(this) + GetArityOffset());
    }

#ifndef DACCESS_COMPILE
    void SetArity(UInt32 value)
    {
        ASSERT(HasInstantiation());
        *(UInt32*)((UInt8*)this + GetArityOffset()) = value;
    }
#endif

    UInt32 GetGenericTypeDefOffset()
    {
        ASSERT(HasInstantiation());
        return sizeof(UInt8) + sizeof(TgtPTR_EEType) + sizeof(UInt32);
    }

    EETypeRef GetGenericTypeDef()
    {
        ASSERT(HasInstantiation());
        return *dac_cast<DPTR(EETypeRef)>(dac_cast<TADDR>(this) + GetGenericTypeDefOffset());
    }

#ifndef DACCESS_COMPILE
    void SetGenericTypeDef(EETypeRef value)
    {
        ASSERT(HasInstantiation());
        *(EETypeRef*)((UInt8*)this + GetGenericTypeDefOffset()) = value;
    }
#endif

    UInt32 GetParameterTypeOffset(UInt32 index)
    {
        ASSERT(HasInstantiation());
        return sizeof(UInt8) + sizeof(TgtPTR_EEType) + sizeof(UInt32) + sizeof(EETypeRef) + (index * sizeof(EETypeRef));
    }

    EETypeRef GetParameterType(UInt32 index)
    {
        ASSERT(HasInstantiation());
        return *dac_cast<DPTR(EETypeRef)>(dac_cast<TADDR>(this) + GetParameterTypeOffset(index));
    }

#ifndef DACCESS_COMPILE
    void SetParameterType(UInt32 index, EETypeRef value)
    {
        ASSERT(HasInstantiation());
        *(EETypeRef*)((UInt8*)this + GetParameterTypeOffset(index)) = value;
    }
#endif

    UInt32 GetParameterVarianceOffset(UInt32 index)
    {
        ASSERT(HasVariance());
        return sizeof(UInt8) + ((m_Flags & GID_Instantiation) ? (sizeof(TgtPTR_EEType) + sizeof(UInt32) + sizeof(EETypeRef) + (sizeof(EETypeRef) * GetArity())) : 0) + (index * sizeof(GenericVarianceType));
    }

    GenericVarianceType GetParameterVariance(UInt32 index)
    {
        ASSERT(HasVariance());
        return *dac_cast<DPTR(GenericVarianceType)>(dac_cast<TADDR>(this) + GetParameterVarianceOffset(index));
    }

#ifndef DACCESS_COMPILE
    void SetParameterVariance(UInt32 index, GenericVarianceType value)
    {
        ASSERT(HasVariance());
        *(GenericVarianceType*)((UInt8*)this + GetParameterVarianceOffset(index)) = value;
    }
#endif

    UInt32 GetGcStaticFieldDataOffset()
    {
        ASSERT(HasGcStaticFields());
        return sizeof(UInt8) + ((m_Flags & GID_Instantiation) ? (sizeof(TgtPTR_EEType) + sizeof(UInt32) + sizeof(EETypeRef) + (sizeof(EETypeRef) * GetArity())) : 0) + ((m_Flags & GID_Variance) ? ((sizeof(GenericVarianceType) * GetArity())) : 0);
    }

    TgtPTR_UInt8 GetGcStaticFieldData()
    {
        ASSERT(HasGcStaticFields());
        return *dac_cast<DPTR(TgtPTR_UInt8)>(dac_cast<TADDR>(this) + GetGcStaticFieldDataOffset());
    }

#ifndef DACCESS_COMPILE
    void SetGcStaticFieldData(TgtPTR_UInt8 value)
    {
        ASSERT(HasGcStaticFields());
        *(TgtPTR_UInt8*)((UInt8*)this + GetGcStaticFieldDataOffset()) = value;
    }
#endif

    UInt32 GetGcStaticFieldDescOffset()
    {
        ASSERT(HasGcStaticFields());
        return sizeof(UInt8) + ((m_Flags & GID_Instantiation) ? (sizeof(TgtPTR_EEType) + sizeof(UInt32) + sizeof(EETypeRef) + (sizeof(EETypeRef) * GetArity())) : 0) + ((m_Flags & GID_Variance) ? ((sizeof(GenericVarianceType) * GetArity())) : 0) + sizeof(TgtPTR_UInt8);
    }

    TgtPTR_StaticGcDesc GetGcStaticFieldDesc()
    {
        ASSERT(HasGcStaticFields());
        return *dac_cast<DPTR(TgtPTR_StaticGcDesc)>(dac_cast<TADDR>(this) + GetGcStaticFieldDescOffset());
    }

#ifndef DACCESS_COMPILE
    void SetGcStaticFieldDesc(TgtPTR_StaticGcDesc value)
    {
        ASSERT(HasGcStaticFields());
        *(TgtPTR_StaticGcDesc*)((UInt8*)this + GetGcStaticFieldDescOffset()) = value;
    }
#endif

    UInt32 GetNextGidWithGcRootsOffset()
    {
        ASSERT(HasGcRoots());
        return sizeof(UInt8) + ((m_Flags & GID_Instantiation) ? (sizeof(TgtPTR_EEType) + sizeof(UInt32) + sizeof(EETypeRef) + (sizeof(EETypeRef) * GetArity())) : 0) + ((m_Flags & GID_Variance) ? ((sizeof(GenericVarianceType) * GetArity())) : 0) + ((m_Flags & GID_GcStaticFields) ? (sizeof(TgtPTR_UInt8) + sizeof(TgtPTR_StaticGcDesc)) : 0);
    }

    TgtPTR_GenericInstanceDesc GetNextGidWithGcRoots()
    {
        ASSERT(HasGcRoots());
        return *dac_cast<DPTR(TgtPTR_GenericInstanceDesc)>(dac_cast<TADDR>(this) + GetNextGidWithGcRootsOffset());
    }

#ifndef DACCESS_COMPILE
    void SetNextGidWithGcRoots(TgtPTR_GenericInstanceDesc value)
    {
        ASSERT(HasGcRoots());
        *(TgtPTR_GenericInstanceDesc*)((UInt8*)this + GetNextGidWithGcRootsOffset()) = value;
    }
#endif

    UInt32 GetSizeOfNonGcStaticFieldDataOffset()
    {
        ASSERT(HasUnification());
        return sizeof(UInt8) + ((m_Flags & GID_Instantiation) ? (sizeof(TgtPTR_EEType) + sizeof(UInt32) + sizeof(EETypeRef) + (sizeof(EETypeRef) * GetArity())) : 0) + ((m_Flags & GID_Variance) ? ((sizeof(GenericVarianceType) * GetArity())) : 0) + ((m_Flags & GID_GcStaticFields) ? (sizeof(TgtPTR_UInt8) + sizeof(TgtPTR_StaticGcDesc)) : 0) + ((m_Flags & GID_GcRoots) ? (sizeof(TgtPTR_GenericInstanceDesc)) : 0);
    }

    UInt32 GetSizeOfNonGcStaticFieldData()
    {
        ASSERT(HasUnification());
        return *dac_cast<DPTR(UInt32)>(dac_cast<TADDR>(this) + GetSizeOfNonGcStaticFieldDataOffset());
    }

#ifndef DACCESS_COMPILE
    void SetSizeOfNonGcStaticFieldData(UInt32 value)
    {
        ASSERT(HasUnification());
        *(UInt32*)((UInt8*)this + GetSizeOfNonGcStaticFieldDataOffset()) = value;
    }
#endif

    UInt32 GetSizeOfGcStaticFieldDataOffset()
    {
        ASSERT(HasUnification());
        return sizeof(UInt8) + ((m_Flags & GID_Instantiation) ? (sizeof(TgtPTR_EEType) + sizeof(UInt32) + sizeof(EETypeRef) + (sizeof(EETypeRef) * GetArity())) : 0) + ((m_Flags & GID_Variance) ? ((sizeof(GenericVarianceType) * GetArity())) : 0) + ((m_Flags & GID_GcStaticFields) ? (sizeof(TgtPTR_UInt8) + sizeof(TgtPTR_StaticGcDesc)) : 0) + ((m_Flags & GID_GcRoots) ? (sizeof(TgtPTR_GenericInstanceDesc)) : 0) + sizeof(UInt32);
    }

    UInt32 GetSizeOfGcStaticFieldData()
    {
        ASSERT(HasUnification());
        return *dac_cast<DPTR(UInt32)>(dac_cast<TADDR>(this) + GetSizeOfGcStaticFieldDataOffset());
    }

#ifndef DACCESS_COMPILE
    void SetSizeOfGcStaticFieldData(UInt32 value)
    {
        ASSERT(HasUnification());
        *(UInt32*)((UInt8*)this + GetSizeOfGcStaticFieldDataOffset()) = value;
    }
#endif

    UInt32 GetThreadStaticFieldTlsIndexOffset()
    {
        ASSERT(HasThreadStaticFields());
        return sizeof(UInt8) + ((m_Flags & GID_Instantiation) ? (sizeof(TgtPTR_EEType) + sizeof(UInt32) + sizeof(EETypeRef) + (sizeof(EETypeRef) * GetArity())) : 0) + ((m_Flags & GID_Variance) ? ((sizeof(GenericVarianceType) * GetArity())) : 0) + ((m_Flags & GID_GcStaticFields) ? (sizeof(TgtPTR_UInt8) + sizeof(TgtPTR_StaticGcDesc)) : 0) + ((m_Flags & GID_GcRoots) ? (sizeof(TgtPTR_GenericInstanceDesc)) : 0) + ((m_Flags & GID_Unification) ? (sizeof(UInt32) + sizeof(UInt32)) : 0);
    }

    UInt32 GetThreadStaticFieldTlsIndex()
    {
        ASSERT(HasThreadStaticFields());
        return *dac_cast<DPTR(UInt32)>(dac_cast<TADDR>(this) + GetThreadStaticFieldTlsIndexOffset());
    }

#ifndef DACCESS_COMPILE
    void SetThreadStaticFieldTlsIndex(UInt32 value)
    {
        ASSERT(HasThreadStaticFields());
        *(UInt32*)((UInt8*)this + GetThreadStaticFieldTlsIndexOffset()) = value;
    }
#endif

    UInt32 GetThreadStaticFieldStartOffsetOffset()
    {
        ASSERT(HasThreadStaticFields());
        return sizeof(UInt8) + ((m_Flags & GID_Instantiation) ? (sizeof(TgtPTR_EEType) + sizeof(UInt32) + sizeof(EETypeRef) + (sizeof(EETypeRef) * GetArity())) : 0) + ((m_Flags & GID_Variance) ? ((sizeof(GenericVarianceType) * GetArity())) : 0) + ((m_Flags & GID_GcStaticFields) ? (sizeof(TgtPTR_UInt8) + sizeof(TgtPTR_StaticGcDesc)) : 0) + ((m_Flags & GID_GcRoots) ? (sizeof(TgtPTR_GenericInstanceDesc)) : 0) + ((m_Flags & GID_Unification) ? (sizeof(UInt32) + sizeof(UInt32)) : 0) + sizeof(UInt32);
    }

    UInt32 GetThreadStaticFieldStartOffset()
    {
        ASSERT(HasThreadStaticFields());
        return *dac_cast<DPTR(UInt32)>(dac_cast<TADDR>(this) + GetThreadStaticFieldStartOffsetOffset());
    }

#ifndef DACCESS_COMPILE
    void SetThreadStaticFieldStartOffset(UInt32 value)
    {
        ASSERT(HasThreadStaticFields());
        *(UInt32*)((UInt8*)this + GetThreadStaticFieldStartOffsetOffset()) = value;
    }
#endif

    UInt32 GetThreadStaticFieldDescOffset()
    {
        ASSERT(HasThreadStaticFields());
        return sizeof(UInt8) + ((m_Flags & GID_Instantiation) ? (sizeof(TgtPTR_EEType) + sizeof(UInt32) + sizeof(EETypeRef) + (sizeof(EETypeRef) * GetArity())) : 0) + ((m_Flags & GID_Variance) ? ((sizeof(GenericVarianceType) * GetArity())) : 0) + ((m_Flags & GID_GcStaticFields) ? (sizeof(TgtPTR_UInt8) + sizeof(TgtPTR_StaticGcDesc)) : 0) + ((m_Flags & GID_GcRoots) ? (sizeof(TgtPTR_GenericInstanceDesc)) : 0) + ((m_Flags & GID_Unification) ? (sizeof(UInt32) + sizeof(UInt32)) : 0) + sizeof(UInt32) + sizeof(UInt32);
    }

    TgtPTR_StaticGcDesc GetThreadStaticFieldDesc()
    {
        ASSERT(HasThreadStaticFields());
        return *dac_cast<DPTR(TgtPTR_StaticGcDesc)>(dac_cast<TADDR>(this) + GetThreadStaticFieldDescOffset());
    }

#ifndef DACCESS_COMPILE
    void SetThreadStaticFieldDesc(TgtPTR_StaticGcDesc value)
    {
        ASSERT(HasThreadStaticFields());
        *(TgtPTR_StaticGcDesc*)((UInt8*)this + GetThreadStaticFieldDescOffset()) = value;
    }
#endif

    UInt32 GetNonGcStaticFieldDataOffset()
    {
        ASSERT(HasNonGcStaticFields());
        return sizeof(UInt8) + ((m_Flags & GID_Instantiation) ? (sizeof(TgtPTR_EEType) + sizeof(UInt32) + sizeof(EETypeRef) + (sizeof(EETypeRef) * GetArity())) : 0) + ((m_Flags & GID_Variance) ? ((sizeof(GenericVarianceType) * GetArity())) : 0) + ((m_Flags & GID_GcStaticFields) ? (sizeof(TgtPTR_UInt8) + sizeof(TgtPTR_StaticGcDesc)) : 0) + ((m_Flags & GID_GcRoots) ? (sizeof(TgtPTR_GenericInstanceDesc)) : 0) + ((m_Flags & GID_Unification) ? (sizeof(UInt32) + sizeof(UInt32)) : 0) + ((m_Flags & GID_ThreadStaticFields) ? (sizeof(UInt32) + sizeof(UInt32) + sizeof(TgtPTR_StaticGcDesc)) : 0);
    }

    TgtPTR_UInt8 GetNonGcStaticFieldData()
    {
        ASSERT(HasNonGcStaticFields());
        return *dac_cast<DPTR(TgtPTR_UInt8)>(dac_cast<TADDR>(this) + GetNonGcStaticFieldDataOffset());
    }

#ifndef DACCESS_COMPILE
    void SetNonGcStaticFieldData(TgtPTR_UInt8 value)
    {
        ASSERT(HasNonGcStaticFields());
        *(TgtPTR_UInt8*)((UInt8*)this + GetNonGcStaticFieldDataOffset()) = value;
    }
#endif

    enum _FieldBaseSizes
    {
        kBaseSizeInstantiation = sizeof(TgtPTR_EEType) + sizeof(UInt32) + sizeof(EETypeRef),
        kBaseSizeVariance = 0,
        kBaseSizeGcStaticFields = sizeof(TgtPTR_UInt8) + sizeof(TgtPTR_StaticGcDesc),
        kBaseSizeGcRoots = sizeof(TgtPTR_GenericInstanceDesc),
        kBaseSizeUnification = sizeof(UInt32) + sizeof(UInt32),
        kBaseSizeThreadStaticFields = sizeof(UInt32) + sizeof(UInt32) + sizeof(TgtPTR_StaticGcDesc),
        kBaseSizeNonGcStaticFields = sizeof(TgtPTR_UInt8),
    };

    static inline UInt32 GetBaseSize(OptionalFieldTypes flags)
    {
        static const UInt32 s_rgSizeTable[] =
        {
            sizeof(UInt8), sizeof(UInt8) + kBaseSizeInstantiation, sizeof(UInt8) + kBaseSizeVariance, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeVariance, sizeof(UInt8) + kBaseSizeGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeGcStaticFields, sizeof(UInt8) + kBaseSizeVariance + kBaseSizeGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeVariance + kBaseSizeGcStaticFields, sizeof(UInt8) + kBaseSizeGcRoots, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeGcRoots, sizeof(UInt8) + kBaseSizeVariance + kBaseSizeGcRoots, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeVariance + kBaseSizeGcRoots, sizeof(UInt8) + kBaseSizeGcStaticFields + kBaseSizeGcRoots, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeGcStaticFields + kBaseSizeGcRoots, sizeof(UInt8) + kBaseSizeVariance + kBaseSizeGcStaticFields + kBaseSizeGcRoots, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeVariance + kBaseSizeGcStaticFields + kBaseSizeGcRoots, sizeof(UInt8) + kBaseSizeUnification, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeUnification, sizeof(UInt8) + kBaseSizeVariance + kBaseSizeUnification, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeVariance + kBaseSizeUnification, sizeof(UInt8) + kBaseSizeGcStaticFields + kBaseSizeUnification, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeGcStaticFields + kBaseSizeUnification, sizeof(UInt8) + kBaseSizeVariance + kBaseSizeGcStaticFields + kBaseSizeUnification, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeVariance + kBaseSizeGcStaticFields + kBaseSizeUnification, sizeof(UInt8) + kBaseSizeGcRoots + kBaseSizeUnification, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeGcRoots + kBaseSizeUnification, sizeof(UInt8) + kBaseSizeVariance + kBaseSizeGcRoots + kBaseSizeUnification, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeVariance + kBaseSizeGcRoots + kBaseSizeUnification, sizeof(UInt8) + kBaseSizeGcStaticFields + kBaseSizeGcRoots + kBaseSizeUnification, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeGcStaticFields + kBaseSizeGcRoots + kBaseSizeUnification, sizeof(UInt8) + kBaseSizeVariance + kBaseSizeGcStaticFields + kBaseSizeGcRoots + kBaseSizeUnification, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeVariance + kBaseSizeGcStaticFields + kBaseSizeGcRoots + kBaseSizeUnification, sizeof(UInt8) + kBaseSizeThreadStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeThreadStaticFields, sizeof(UInt8) + kBaseSizeVariance + kBaseSizeThreadStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeVariance + kBaseSizeThreadStaticFields, sizeof(UInt8) + kBaseSizeGcStaticFields + kBaseSizeThreadStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeGcStaticFields + kBaseSizeThreadStaticFields, sizeof(UInt8) + kBaseSizeVariance + kBaseSizeGcStaticFields + kBaseSizeThreadStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeVariance + kBaseSizeGcStaticFields + kBaseSizeThreadStaticFields, sizeof(UInt8) + kBaseSizeGcRoots + kBaseSizeThreadStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeGcRoots + kBaseSizeThreadStaticFields, sizeof(UInt8) + kBaseSizeVariance + kBaseSizeGcRoots + kBaseSizeThreadStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeVariance + kBaseSizeGcRoots + kBaseSizeThreadStaticFields, sizeof(UInt8) + kBaseSizeGcStaticFields + kBaseSizeGcRoots + kBaseSizeThreadStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeGcStaticFields + kBaseSizeGcRoots + kBaseSizeThreadStaticFields, sizeof(UInt8) + kBaseSizeVariance + kBaseSizeGcStaticFields + kBaseSizeGcRoots + kBaseSizeThreadStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeVariance + kBaseSizeGcStaticFields + kBaseSizeGcRoots + kBaseSizeThreadStaticFields, sizeof(UInt8) + kBaseSizeUnification + kBaseSizeThreadStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeUnification + kBaseSizeThreadStaticFields, sizeof(UInt8) + kBaseSizeVariance + kBaseSizeUnification + kBaseSizeThreadStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeVariance + kBaseSizeUnification + kBaseSizeThreadStaticFields, sizeof(UInt8) + kBaseSizeGcStaticFields + kBaseSizeUnification + kBaseSizeThreadStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeGcStaticFields + kBaseSizeUnification + kBaseSizeThreadStaticFields, sizeof(UInt8) + kBaseSizeVariance + kBaseSizeGcStaticFields + kBaseSizeUnification + kBaseSizeThreadStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeVariance + kBaseSizeGcStaticFields + kBaseSizeUnification + kBaseSizeThreadStaticFields, sizeof(UInt8) + kBaseSizeGcRoots + kBaseSizeUnification + kBaseSizeThreadStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeGcRoots + kBaseSizeUnification + kBaseSizeThreadStaticFields, sizeof(UInt8) + kBaseSizeVariance + kBaseSizeGcRoots + kBaseSizeUnification + kBaseSizeThreadStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeVariance + kBaseSizeGcRoots + kBaseSizeUnification + kBaseSizeThreadStaticFields, sizeof(UInt8) + kBaseSizeGcStaticFields + kBaseSizeGcRoots + kBaseSizeUnification + kBaseSizeThreadStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeGcStaticFields + kBaseSizeGcRoots + kBaseSizeUnification + kBaseSizeThreadStaticFields, sizeof(UInt8) + kBaseSizeVariance + kBaseSizeGcStaticFields + kBaseSizeGcRoots + kBaseSizeUnification + kBaseSizeThreadStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeVariance + kBaseSizeGcStaticFields + kBaseSizeGcRoots + kBaseSizeUnification + kBaseSizeThreadStaticFields, sizeof(UInt8) + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeVariance + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeVariance + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeGcStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeGcStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeVariance + kBaseSizeGcStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeVariance + kBaseSizeGcStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeGcRoots + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeGcRoots + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeVariance + kBaseSizeGcRoots + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeVariance + kBaseSizeGcRoots + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeGcStaticFields + kBaseSizeGcRoots + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeGcStaticFields + kBaseSizeGcRoots + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeVariance + kBaseSizeGcStaticFields + kBaseSizeGcRoots + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeVariance + kBaseSizeGcStaticFields + kBaseSizeGcRoots + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeUnification + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeUnification + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeVariance + kBaseSizeUnification + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeVariance + kBaseSizeUnification + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeGcStaticFields + kBaseSizeUnification + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeGcStaticFields + kBaseSizeUnification + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeVariance + kBaseSizeGcStaticFields + kBaseSizeUnification + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeVariance + kBaseSizeGcStaticFields + kBaseSizeUnification + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeGcRoots + kBaseSizeUnification + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeGcRoots + kBaseSizeUnification + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeVariance + kBaseSizeGcRoots + kBaseSizeUnification + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeVariance + kBaseSizeGcRoots + kBaseSizeUnification + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeGcStaticFields + kBaseSizeGcRoots + kBaseSizeUnification + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeGcStaticFields + kBaseSizeGcRoots + kBaseSizeUnification + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeVariance + kBaseSizeGcStaticFields + kBaseSizeGcRoots + kBaseSizeUnification + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeVariance + kBaseSizeGcStaticFields + kBaseSizeGcRoots + kBaseSizeUnification + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeThreadStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeThreadStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeVariance + kBaseSizeThreadStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeVariance + kBaseSizeThreadStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeGcStaticFields + kBaseSizeThreadStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeGcStaticFields + kBaseSizeThreadStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeVariance + kBaseSizeGcStaticFields + kBaseSizeThreadStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeVariance + kBaseSizeGcStaticFields + kBaseSizeThreadStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeGcRoots + kBaseSizeThreadStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeGcRoots + kBaseSizeThreadStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeVariance + kBaseSizeGcRoots + kBaseSizeThreadStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeVariance + kBaseSizeGcRoots + kBaseSizeThreadStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeGcStaticFields + kBaseSizeGcRoots + kBaseSizeThreadStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeGcStaticFields + kBaseSizeGcRoots + kBaseSizeThreadStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeVariance + kBaseSizeGcStaticFields + kBaseSizeGcRoots + kBaseSizeThreadStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeVariance + kBaseSizeGcStaticFields + kBaseSizeGcRoots + kBaseSizeThreadStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeUnification + kBaseSizeThreadStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeUnification + kBaseSizeThreadStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeVariance + kBaseSizeUnification + kBaseSizeThreadStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeVariance + kBaseSizeUnification + kBaseSizeThreadStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeGcStaticFields + kBaseSizeUnification + kBaseSizeThreadStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeGcStaticFields + kBaseSizeUnification + kBaseSizeThreadStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeVariance + kBaseSizeGcStaticFields + kBaseSizeUnification + kBaseSizeThreadStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeVariance + kBaseSizeGcStaticFields + kBaseSizeUnification + kBaseSizeThreadStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeGcRoots + kBaseSizeUnification + kBaseSizeThreadStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeGcRoots + kBaseSizeUnification + kBaseSizeThreadStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeVariance + kBaseSizeGcRoots + kBaseSizeUnification + kBaseSizeThreadStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeVariance + kBaseSizeGcRoots + kBaseSizeUnification + kBaseSizeThreadStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeGcStaticFields + kBaseSizeGcRoots + kBaseSizeUnification + kBaseSizeThreadStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeGcStaticFields + kBaseSizeGcRoots + kBaseSizeUnification + kBaseSizeThreadStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeVariance + kBaseSizeGcStaticFields + kBaseSizeGcRoots + kBaseSizeUnification + kBaseSizeThreadStaticFields + kBaseSizeNonGcStaticFields, sizeof(UInt8) + kBaseSizeInstantiation + kBaseSizeVariance + kBaseSizeGcStaticFields + kBaseSizeGcRoots + kBaseSizeUnification + kBaseSizeThreadStaticFields + kBaseSizeNonGcStaticFields, 
        };
        ASSERT(flags <= GID_AllFields);
        return s_rgSizeTable[flags];
    }
