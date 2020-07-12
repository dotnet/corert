// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//
// Redhawk-specific ETW helper code.
// 
// When Redhawk does stuff substantially different from desktop CLR, the
// Redhawk-specific implementations should go here.
//
#include "common.h"
#include "gcenv.h"
#include "rheventtrace.h"
#include "eventtrace.h"
#include "rhbinder.h"
#include "slist.h"
#include "rwlock.h"
#include "runtimeinstance.h"
#include "shash.h"
#include "eventtracepriv.h"
#include "shash.inl"
#include "palredhawk.h"

#if defined(FEATURE_EVENT_TRACE)

//---------------------------------------------------------------------------------------
// BulkTypeEventLogger is a helper class to batch up type information and then flush to
// ETW once the event reaches its max # descriptors


//---------------------------------------------------------------------------------------
//
// Batches up ETW information for a type and pops out to recursively call
// ETW::TypeSystemLog::LogTypeAndParametersIfNecessary for any
// "type parameters".  Generics info is not reliably available, so "type parameter"
// really just refers to the type of array elements if thAsAddr is an array.
//
// Arguments:
//      * thAsAddr - EEType to log
//      * typeLogBehavior - Ignored in Redhawk builds
//

void BulkTypeEventLogger::LogTypeAndParameters(UInt64 thAsAddr, ETW::TypeSystemLog::TypeLogBehavior typeLogBehavior)
{
    if (!ETW_TRACING_CATEGORY_ENABLED(
        MICROSOFT_WINDOWS_DOTNETRUNTIME_PROVIDER_Context, 
        TRACE_LEVEL_INFORMATION, 
        CLR_TYPE_KEYWORD))
    {
        return;
    }

    EEType * pEEType = (EEType *) thAsAddr;

    // Batch up this type.  This grabs useful info about the type, including any
    // type parameters it may have, and sticks it in m_rgBulkTypeValues
    int iBulkTypeEventData = LogSingleType(pEEType);
    if (iBulkTypeEventData == -1)
    {
        // There was a failure trying to log the type, so don't bother with its type
        // parameters
        return;
    }

    // Look at the type info we just batched, so we can get the type parameters
    BulkTypeValue * pVal = &m_rgBulkTypeValues[iBulkTypeEventData];

    // We're about to recursively call ourselves for the type parameters, so make a
    // local copy of their type handles first (else, as we log them we could flush
    // and clear out m_rgBulkTypeValues, thus trashing pVal)
    NewArrayHolder<ULONGLONG> rgTypeParameters;
    DWORD cTypeParams = pVal->cTypeParameters;
    if (cTypeParams == 1)
    {
        ETW::TypeSystemLog::LogTypeAndParametersIfNecessary(this, pVal->ullSingleTypeParameter, typeLogBehavior);
    }
    else if (cTypeParams > 1)
    {
        rgTypeParameters = new (nothrow) ULONGLONG[cTypeParams];
        for (DWORD i=0; i < cTypeParams; i++)
        {
            rgTypeParameters[i] = pVal->rgTypeParameters[i];
        }

        // Recursively log any referenced parameter types
        for (DWORD i=0; i < cTypeParams; i++)
        {
            ETW::TypeSystemLog::LogTypeAndParametersIfNecessary(this, rgTypeParameters[i], typeLogBehavior);
        }
    }
}

// We keep a hash of these to keep track of:
//     * Which types have been logged through ETW (so we can avoid logging dupe Type
//         events), and
//     * GCSampledObjectAllocation stats to help with "smart sampling" which
//         dynamically adjusts sampling rate of objects by type.
// See code:LoggedTypesFromModuleTraits

class LoggedTypesTraits : public  DefaultSHashTraits<EEType*>
{
public:

    // explicitly declare local typedefs for these traits types, otherwise 
    // the compiler may get confused
    typedef EEType* key_t;

    static key_t GetKey(const element_t &e)
    {
        LIMITED_METHOD_CONTRACT;
        return e;
    }

    static BOOL Equals(key_t k1, key_t k2)
    {
        LIMITED_METHOD_CONTRACT;
        return (k1 == k2);
    }

    static count_t Hash(key_t k)
    {
        LIMITED_METHOD_CONTRACT;
        return (count_t) (UIntNative) k;
    }

    static bool IsNull(const element_t &e)
    {
        LIMITED_METHOD_CONTRACT;
        return (e == NULL);
    }

    static const element_t Null()
    {
        LIMITED_METHOD_CONTRACT;
        return NULL;
    }
};

enum class CorElementType : UInt8
{
    ELEMENT_TYPE_END = 0x0,

    ELEMENT_TYPE_BOOLEAN = 0x2,
    ELEMENT_TYPE_CHAR = 0x3,
    ELEMENT_TYPE_I1 = 0x4,
    ELEMENT_TYPE_U1 = 0x5,
    ELEMENT_TYPE_I2 = 0x6,
    ELEMENT_TYPE_U2 = 0x7,
    ELEMENT_TYPE_I4 = 0x8,
    ELEMENT_TYPE_U4 = 0x9,
    ELEMENT_TYPE_I8 = 0xa,
    ELEMENT_TYPE_U8 = 0xb,
    ELEMENT_TYPE_R4 = 0xc,
    ELEMENT_TYPE_R8 = 0xd,

    ELEMENT_TYPE_I = 0x18,
    ELEMENT_TYPE_U = 0x19,
};

static CorElementType ElementTypeToCorElementType(EETypeElementType elementType)
{
    switch (elementType)
    {
    case EETypeElementType::ElementType_Boolean:
        return CorElementType::ELEMENT_TYPE_BOOLEAN;
    case EETypeElementType::ElementType_Char:
        return CorElementType::ELEMENT_TYPE_CHAR;
    case EETypeElementType::ElementType_SByte:
        return CorElementType::ELEMENT_TYPE_I1;
    case EETypeElementType::ElementType_Byte:
        return CorElementType::ELEMENT_TYPE_U1;
    case EETypeElementType::ElementType_Int16:
        return CorElementType::ELEMENT_TYPE_I2;
    case EETypeElementType::ElementType_UInt16:
        return CorElementType::ELEMENT_TYPE_U2;
    case EETypeElementType::ElementType_Int32:
        return CorElementType::ELEMENT_TYPE_I4;
    case EETypeElementType::ElementType_UInt32:
        return CorElementType::ELEMENT_TYPE_U4;
    case EETypeElementType::ElementType_Int64:
        return CorElementType::ELEMENT_TYPE_I8;
    case EETypeElementType::ElementType_UInt64:
        return CorElementType::ELEMENT_TYPE_U8;
    case EETypeElementType::ElementType_Single:
        return CorElementType::ELEMENT_TYPE_R4;
    case EETypeElementType::ElementType_Double:
        return CorElementType::ELEMENT_TYPE_R8;
    case EETypeElementType::ElementType_IntPtr:
        return CorElementType::ELEMENT_TYPE_I;
    case EETypeElementType::ElementType_UIntPtr:
        return CorElementType::ELEMENT_TYPE_U;
    }
    return CorElementType::ELEMENT_TYPE_END;
}

// Avoid reporting the same type twice by keeping a hash of logged types.
SHash<LoggedTypesTraits>* s_loggedTypesHash = NULL;

//---------------------------------------------------------------------------------------
//
// Interrogates EEType for the info that's interesting to include in the BulkType ETW
// event.  Does not recursively call self for type parameters.
//
// Arguments:
//      * pEEType - EEType to log info about
//
// Return Value:
//      Index into internal array where the info got batched.  Or -1 if there was a
//      failure.
//

int BulkTypeEventLogger::LogSingleType(EEType * pEEType)
{
#ifdef MULTIPLE_HEAPS
    // We need to add a lock to protect the types hash for Server GC.
    ASSERT_UNCONDITIONALLY("Add a lock to protect s_loggedTypesHash access!");
#endif 
    //Avoid logging the same type twice, but using the hash of loggged types.
    if (s_loggedTypesHash == NULL)
        s_loggedTypesHash = new SHash<LoggedTypesTraits>();
    EEType* preexistingType = s_loggedTypesHash->Lookup(pEEType);
    if (preexistingType != NULL)
    {
        return -1;
    }
    else
    {
        s_loggedTypesHash->Add(pEEType);
    }

    // If there's no room for another type, flush what we've got
    if (m_nBulkTypeValueCount == _countof(m_rgBulkTypeValues))
    {
        FireBulkTypeEvent();
    }
    
    _ASSERTE(m_nBulkTypeValueCount < _countof(m_rgBulkTypeValues));

    BulkTypeValue * pVal = &m_rgBulkTypeValues[m_nBulkTypeValueCount];
    
    // Clear out pVal before filling it out (array elements can get reused if there
    // are enough types that we need to flush to multiple events).
    pVal->Clear();

    pVal->fixedSizedData.TypeID = (ULONGLONG) pEEType;
    pVal->fixedSizedData.Flags = kEtwTypeFlagsModuleBaseAddress;
    pVal->fixedSizedData.CorElementType = (BYTE)ElementTypeToCorElementType(pEEType->GetElementType());

    ULONGLONG * rgTypeParamsForEvent = NULL;
    ULONGLONG typeParamForNonGenericType = 0;

    // Determine this EEType's module.
    RuntimeInstance * pRuntimeInstance = GetRuntimeInstance();

    ULONGLONG osModuleHandle = (ULONGLONG) pEEType->GetTypeManagerPtr()->AsTypeManager()->GetOsModuleHandle();

    pVal->fixedSizedData.ModuleID = osModuleHandle;

    if (pEEType->IsParameterizedType())
    {
        ASSERT(pEEType->IsArray());
        // Array
        pVal->fixedSizedData.Flags |= kEtwTypeFlagsArray;
        pVal->cTypeParameters = 1;
        pVal->ullSingleTypeParameter = (ULONGLONG) pEEType->get_RelatedParameterType();
    }
    else
    {
        // Note: if pEEType->IsCloned(), then no special handling is necessary.  All the
        // functionality we need from the EEType below work just as well from cloned types.

        // Note: For generic types, we do not necessarily know the generic parameters. 
        // So we leave it to the profiler at post-processing time to determine that via
        // the PDBs.  We'll leave pVal->cTypeParameters as 0, even though there could be
        // type parameters.

        // Flags
        if (pEEType->HasFinalizer())
        {
            pVal->fixedSizedData.Flags |= kEtwTypeFlagsFinalizable;
        }

        // Note: Pn runtime knows nothing about delegates, and there are no CCWs/RCWs. 
        // So no other type flags are applicable to set
    }

    ULONGLONG rvaType = osModuleHandle == 0 ? 0 : (ULONGLONG(pEEType) - osModuleHandle);
    pVal->fixedSizedData.TypeNameID = (DWORD) rvaType;

    // Now that we know the full size of this type's data, see if it fits in our
    // batch or whether we need to flush

    int cbVal = pVal->GetByteCountInEvent();
    if (cbVal > kMaxBytesTypeValues)
    {
        // This type is apparently so huge, it's too big to squeeze into an event, even
        // if it were the only type batched in the whole event.  Bail
        ASSERT(!"Type too big to log via ETW");
        return -1;
    }

    if (m_nBulkTypeValueByteCount + cbVal > kMaxBytesTypeValues)
    {
        // Although this type fits into the array, its size is so big that the entire
        // array can't be logged via ETW. So flush the array, and start over by
        // calling ourselves--this refetches the type info and puts it at the
        // beginning of the array.  Since we know this type is small enough to be
        // batched into an event on its own, this recursive call will not try to
        // call itself again.
        FireBulkTypeEvent();
        return LogSingleType(pEEType);
    }

    // The type fits into the batch, so update our state
    m_nBulkTypeValueCount++;
    m_nBulkTypeValueByteCount += cbVal;
    return m_nBulkTypeValueCount - 1;       // Index of type we just added
}


void BulkTypeEventLogger::Cleanup()
{
    if (s_loggedTypesHash != NULL)
    {
        delete s_loggedTypesHash;
        s_loggedTypesHash = NULL;
    }
}

#endif // defined(FEATURE_EVENT_TRACE)


//---------------------------------------------------------------------------------------
//
// Outermost level of ETW-type-logging.  Clients outside (rh)eventtrace.cpp call this to log
// an EETypes and (recursively) its type parameters when present.  This guy then calls
// into the appropriate BulkTypeEventLogger to do the batching and logging
//
// Arguments:
//      * pBulkTypeEventLogger - If our caller is keeping track of batched types, it
//          passes this to us so we can use it to batch the current type (GC heap walk
//          does this).  In Redhawk builds this should not be NULL.
//      * thAsAddr - EEType to batch
//      * typeLogBehavior - Unused in Redhawk builds
//

void ETW::TypeSystemLog::LogTypeAndParametersIfNecessary(BulkTypeEventLogger * pLogger, UInt64 thAsAddr, ETW::TypeSystemLog::TypeLogBehavior typeLogBehavior)
{
#if defined(FEATURE_EVENT_TRACE)

    if (!ETW_TRACING_CATEGORY_ENABLED(
        MICROSOFT_WINDOWS_DOTNETRUNTIME_PROVIDER_Context, 
        TRACE_LEVEL_INFORMATION, 
        CLR_TYPE_KEYWORD))
    {
        return;
    }

    _ASSERTE(pLogger != NULL);
    pLogger->LogTypeAndParameters(thAsAddr, typeLogBehavior);

#endif // defined(FEATURE_EVENT_TRACE)
}


//---------------------------------------------------------------------------------------
// Runtime helpers for ETW logging.
//---------------------------------------------------------------------------------------
typedef enum
{
    EVENT_LOG_CCW = 1,
    EVENT_LOG_RCW,
    EVENT_FLUSH_COM
} COM_ETW_EVENTS;



COOP_PINVOKE_HELPER(void, RhpETWLogLiveCom, (Int32 eventType, void* CCWGCHandle, void* objectID, void* typeRawValue, void* IUnknown, void* VTable, Int32 comRefCount, Int32 jupiterRefCount, Int32 flags))
{
    switch (eventType)
    {
    case EVENT_LOG_CCW:
        BulkComLogger::WriteCCW(CCWGCHandle, objectID, typeRawValue, IUnknown, comRefCount, jupiterRefCount, flags);
        break;
    case EVENT_LOG_RCW:
        BulkComLogger::WriteRCW(objectID, typeRawValue, IUnknown, VTable, comRefCount, flags);
        break;
    case EVENT_FLUSH_COM:
        BulkComLogger::FlushComETW();
        break;
    default:
        ASSERT_UNCONDITIONALLY("unexpected COM ETW Event ID");
    }
}

COOP_PINVOKE_HELPER(bool, RhpETWShouldWalkCom, ())
{
    return BulkComLogger::ShouldReportComForGCHeapEtw();
}

//---------------------------------------------------------------------------------------
// BulkStaticsLogger: Batches up and logs static variable roots
//---------------------------------------------------------------------------------------

BulkComLogger* BulkComLogger::s_comLogger;

BulkComLogger::BulkComLogger()
    : m_currRcw(0), m_currCcw(0), m_etwRcwData(0), m_etwCcwData(0)
{
    CONTRACTL
    {
        THROWS;
        GC_NOTRIGGER;
        MODE_ANY;
    }
    CONTRACTL_END;

    m_etwRcwData = new EventRCWEntry[kMaxRcwCount];
    m_etwCcwData = new EventCCWEntry[kMaxCcwCount];
}


BulkComLogger::~BulkComLogger()
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_ANY;
    } 
    CONTRACTL_END;

    FireBulkComEvent();

    if (m_etwRcwData)
        delete[] m_etwRcwData;

    if (m_etwCcwData)
        delete[] m_etwCcwData;
}

bool BulkComLogger::ShouldReportComForGCHeapEtw()
{
    return ETW::GCLog::ShouldWalkHeapObjectsForEtw();
}

void BulkComLogger::WriteCCW(void* CCWGCHandle, void* objectID, void* typeRawValue, void* IUnknown, long comRefCount, long jupiterRefCount, long flags)
{
    EventCCWEntry ccwEntry;

    ccwEntry.RootID = (UInt64)CCWGCHandle;
    ccwEntry.ObjectID = (UInt64) objectID;
    ccwEntry.TypeID = (UInt64) typeRawValue;
    ccwEntry.IUnk = (UInt64) IUnknown;
    ccwEntry.RefCount = (ULONG) comRefCount;
    ccwEntry.JupiterRefCount = (ULONG) jupiterRefCount;
    ccwEntry.Flags = flags;

    BulkComLogger* comLogger = BulkComLogger::GetInstance();
    if (comLogger != NULL)
    {
        comLogger->WriteCcw(ccwEntry);
    }
}

void BulkComLogger::WriteRCW(void* objectID, void* typeRawValue, void* IUnknown, void* VTable, long comRefCount, long flags)
{
    EventRCWEntry rcwEntry;

    rcwEntry.ObjectID = (UInt64) objectID;
    rcwEntry.TypeID = (UInt64) typeRawValue;
    rcwEntry.IUnk = (UInt64) IUnknown;
    rcwEntry.VTable = (UInt64) VTable;
    rcwEntry.RefCount = comRefCount;
    rcwEntry.Flags = flags;

    BulkComLogger* comLogger = BulkComLogger::GetInstance();
    if (comLogger != NULL)
    {
        comLogger->WriteRcw(rcwEntry);
    }
}

void BulkComLogger::FlushComETW()
{
    BulkComLogger* comLogger = BulkComLogger::GetInstance();
    if (comLogger != NULL)
        comLogger->Cleanup();
}

void BulkComLogger::FireBulkComEvent()
{
    WRAPPER_NO_CONTRACT;

    FlushRcw();
    FlushCcw();
}


BulkComLogger* BulkComLogger::GetInstance()
{
    if (s_comLogger == NULL)
    {
        s_comLogger = new BulkComLogger();
    }

    return s_comLogger;
}

void BulkComLogger::Cleanup()
{
    if (s_comLogger != NULL)
    {
        delete s_comLogger;
        s_comLogger = NULL;
    }
}

void BulkComLogger::WriteCcw(const EventCCWEntry& ccw)
{
    CONTRACTL
    {
        THROWS;
        GC_NOTRIGGER;
        MODE_ANY;
    }
    CONTRACTL_END;

    _ASSERTE(m_currCcw < kMaxCcwCount);

    EventCCWEntry &mccw = m_etwCcwData[m_currCcw++];
    mccw = ccw;

    if (m_currCcw >= kMaxCcwCount)
        FlushCcw();
}

void BulkComLogger::FlushCcw()
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_ANY;
    }
    CONTRACTL_END;

    _ASSERTE(m_currCcw <= kMaxCcwCount);

    if (m_currCcw == 0)
        return;

    unsigned short instance = GetClrInstanceId();

    EVENT_DATA_DESCRIPTOR eventData[3];
    EventDataDescCreate(&eventData[0], &m_currCcw, sizeof(const unsigned int));
    EventDataDescCreate(&eventData[1], &instance, sizeof(const unsigned short));
    EventDataDescCreate(&eventData[2], m_etwCcwData, sizeof(EventCCWEntry) * m_currCcw);

    ULONG result = PalEventWrite(Microsoft_Windows_DotNETRuntimeHandle, &GCBulkRootCCW, _countof(eventData), eventData);
    _ASSERTE(result == ERROR_SUCCESS);

    m_currCcw = 0;
}

void BulkComLogger::WriteRcw(const EventRCWEntry& rcw)
{
    CONTRACTL
    {
        THROWS;
        GC_NOTRIGGER;
        MODE_ANY;
    }
    CONTRACTL_END;

    _ASSERTE(m_currRcw < kMaxRcwCount);

    EventRCWEntry &mrcw = m_etwRcwData[m_currRcw];
    mrcw = rcw;

    if (++m_currRcw >= kMaxRcwCount)
        FlushRcw();
}

void BulkComLogger::FlushRcw()
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_ANY;
    }
    CONTRACTL_END;

    _ASSERTE(m_currRcw <= kMaxRcwCount);

    if (m_currRcw == 0)
        return;

    unsigned short instance = GetClrInstanceId();

    EVENT_DATA_DESCRIPTOR eventData[3];
    EventDataDescCreate(&eventData[0], &m_currRcw, sizeof(const unsigned int));
    EventDataDescCreate(&eventData[1], &instance, sizeof(const unsigned short));
    EventDataDescCreate(&eventData[2], m_etwRcwData, sizeof(EventRCWEntry) * m_currRcw);

    ULONG result = PalEventWrite(Microsoft_Windows_DotNETRuntimeHandle, &GCBulkRCW, _countof(eventData), eventData);
    _ASSERTE(result == ERROR_SUCCESS);

    m_currRcw = 0;
}

COOP_PINVOKE_HELPER(void, RhpEtwExceptionThrown, (LPCWSTR exceptionTypeName, LPCWSTR exceptionMessage, void* faultingIP, HRESULT hresult))
{
    FireEtwExceptionThrown_V1(exceptionTypeName,
        exceptionMessage,
        faultingIP,
        hresult,
        0,
        GetClrInstanceId());
}



