//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
#include "common.h"

#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "PalRedhawkCommon.h"
#include "CommonMacros.inl"
#include "Volatile.h"
#include "PalRedhawk.h"
#include "assert.h"

#include "slist.h"
#include "gcrhinterface.h"
#include "module.h"
#include "varint.h"
#include "holder.h"
#include "rhbinder.h"
#include "Crst.h"
#include "RWLock.h"
#include "RuntimeInstance.h"
#include "event.h"
#include "regdisplay.h"
#include "StackFrameIterator.h"
#include "thread.h"
#include "threadstore.h"

#include "eetype.h"
#include "ObjectLayout.h"

#include "GCMemoryHelpers.h"
#include "GCMemoryHelpers.inl"

EXTERN_C REDHAWK_API void* REDHAWK_CALLCONV RhpPublishObject(void* pObject, UIntNative cbSize);

#if defined(FEATURE_SVR_GC)
namespace SVR {
    class GCHeap;
}
#endif // defined(FEATURE_SVR_GC)

struct alloc_context
{
    UInt8*         alloc_ptr;
    UInt8*         alloc_limit;
    __int64        alloc_bytes; //Number of bytes allocated on SOH by this context
    __int64        alloc_bytes_loh; //Number of bytes allocated on LOH by this context
#if defined(FEATURE_SVR_GC)
    SVR::GCHeap*   alloc_heap;
    SVR::GCHeap*   home_heap;
#endif // defined(FEATURE_SVR_GC)
    int            alloc_count;
};

//
// PInvoke
//
COOP_PINVOKE_HELPER(void, RhpReversePInvoke2, (ReversePInvokeFrame* pFrame))
{
    Thread* pCurThread = ThreadStore::RawGetCurrentThread();
    pFrame->m_savedThread = pCurThread;
    if (pCurThread->TryFastReversePInvoke(pFrame))
        return;

    pCurThread->ReversePInvoke(pFrame);
}

COOP_PINVOKE_HELPER(void, RhpReversePInvokeReturn, (ReversePInvokeFrame* pFrame))
{
    pFrame->m_savedThread->ReversePInvokeReturn(pFrame);
}

//
// Allocations
//
COOP_PINVOKE_HELPER(Object *, RhpNewFast, (EEType* pEEType))
{
    ASSERT(!pEEType->RequiresAlign8());
    ASSERT(!pEEType->HasFinalizer());

    Thread * pCurThread = ThreadStore::GetCurrentThread();
    alloc_context * acontext = pCurThread->GetAllocContext();
    Object * pObject;

    size_t size = pEEType->get_BaseSize();

    UInt8* result = acontext->alloc_ptr;
    UInt8* advance = result + size;
    if (advance <= acontext->alloc_limit)
    {
        acontext->alloc_ptr = advance;
        pObject = (Object *)result;
        pObject->set_EEType(pEEType);
        return pObject;
    }

    pObject = (Object *)RedhawkGCInterface::Alloc(pCurThread, size, 0, pEEType);
    if (pObject == nullptr)
    {
        ASSERT_UNCONDITIONALLY("NYI");  // TODO: Throw OOM
    }
    pObject->set_EEType(pEEType);

    if (size >= RH_LARGE_OBJECT_SIZE)
        RhpPublishObject(pObject, size);

    return pObject;
}

#define GC_ALLOC_FINALIZE 0x1 // TODO: Defined in gc.h

COOP_PINVOKE_HELPER(Object *, RhpNewFinalizable, (EEType* pEEType))
{
    ASSERT(!pEEType->RequiresAlign8());
    ASSERT(pEEType->HasFinalizer());

    Thread * pCurThread = ThreadStore::GetCurrentThread();
    Object * pObject;

    size_t size = pEEType->get_BaseSize();

    pObject = (Object *)RedhawkGCInterface::Alloc(pCurThread, size, GC_ALLOC_FINALIZE, pEEType);
    if (pObject == nullptr)
    {
        ASSERT_UNCONDITIONALLY("NYI");  // TODO: Throw OOM
    }
    pObject->set_EEType(pEEType);

    if (size >= RH_LARGE_OBJECT_SIZE)
        RhpPublishObject(pObject, size);

    return pObject;
}

COOP_PINVOKE_HELPER(Array *, RhpNewArray, (EEType * pArrayEEType, int numElements))
{
    ASSERT_MSG(!pArrayEEType->RequiresAlign8(), "NYI");

    Thread * pCurThread = ThreadStore::GetCurrentThread();
    alloc_context * acontext = pCurThread->GetAllocContext();
    Array * pObject;

    // TODO: Overflow checks
    size_t size = 3 * sizeof(UIntNative) + (numElements * pArrayEEType->get_ComponentSize());
    // Align up
    size = (size + (sizeof(UIntNative) - 1)) & ~(sizeof(UIntNative) - 1);

    UInt8* result = acontext->alloc_ptr;
    UInt8* advance = result + size;
    if (advance <= acontext->alloc_limit)
    {
        acontext->alloc_ptr = advance;
        pObject = (Array *)result;
        pObject->set_EEType(pArrayEEType);
        pObject->InitArrayLength((UInt32)numElements);
        return pObject;
    }

    pObject = (Array *)RedhawkGCInterface::Alloc(pCurThread, size, 0, pArrayEEType);
    if (pObject == nullptr)
    {
        ASSERT_UNCONDITIONALLY("NYI");  // TODO: Throw OOM
    }
    pObject->set_EEType(pArrayEEType);
    pObject->InitArrayLength((UInt32)numElements);

    if (size >= RH_LARGE_OBJECT_SIZE)
        RhpPublishObject(pObject, size);

    return pObject;
}

COOP_PINVOKE_HELPER(MDArray *, RhNewMDArray, (EEType * pArrayEEType, UInt32 rank, ...))
{
    ASSERT_MSG(!pArrayEEType->RequiresAlign8(), "NYI");

    Thread * pCurThread = ThreadStore::GetCurrentThread();
    alloc_context * acontext = pCurThread->GetAllocContext();
    MDArray * pObject;

    va_list argp;
    va_start(argp, rank);

    int numElements = va_arg(argp, int);

    for (UInt32 i = 1; i < rank; i++)
    {
        // TODO: Overflow checks
        numElements *= va_arg(argp, Int32);
    }

    // TODO: Overflow checks
    size_t size = 3 * sizeof(UIntNative) + 2 * rank * sizeof(Int32) + (numElements * pArrayEEType->get_ComponentSize());
    // Align up
    size = (size + (sizeof(UIntNative) - 1)) & ~(sizeof(UIntNative) - 1);

    UInt8* result = acontext->alloc_ptr;
    UInt8* advance = result + size;
    bool needsPublish = false;
    if (advance <= acontext->alloc_limit)
    {
        acontext->alloc_ptr = advance;
        pObject = (MDArray *)result;
    }
    else
    {
        needsPublish = true;
        pObject = (MDArray *)RedhawkGCInterface::Alloc(pCurThread, size, 0, pArrayEEType);
        if (pObject == nullptr)
        {
            ASSERT_UNCONDITIONALLY("NYI");  // TODO: Throw OOM
        }
    }

    pObject->set_EEType(pArrayEEType);
    pObject->InitMDArrayLength((UInt32)numElements);

    va_start(argp, rank);
    for (UInt32 i = 0; i < rank; i++)
    {
        pObject->InitMDArrayDimension(i, va_arg(argp, UInt32));
    }

    if (needsPublish && size >= RH_LARGE_OBJECT_SIZE)
        RhpPublishObject(pObject, size);

    return pObject;
}

#if defined(USE_PORTABLE_HELPERS) || !defined(WIN32)
COOP_PINVOKE_HELPER(void, RhpInitialDynamicInterfaceDispatch, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
}

COOP_PINVOKE_HELPER(void, RhpInterfaceDispatch1, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
}

COOP_PINVOKE_HELPER(void, RhpInterfaceDispatch2, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
}

COOP_PINVOKE_HELPER(void, RhpInterfaceDispatch4, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
}

COOP_PINVOKE_HELPER(void, RhpInterfaceDispatch8, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
}

COOP_PINVOKE_HELPER(void, RhpInterfaceDispatch16, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
}

COOP_PINVOKE_HELPER(void, RhpInterfaceDispatch32, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
}

COOP_PINVOKE_HELPER(void, RhpInterfaceDispatch64, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
}

COOP_PINVOKE_HELPER(UIntTarget, ManagedCallout2, (UIntTarget argument1, UIntTarget argument2, void *pTargetMethod, void *pPreviousManagedFrame))
{
    ASSERT_UNCONDITIONALLY("NYI");
    return 0;
}
#endif

// finalizer.cs
COOP_PINVOKE_HELPER(void, ProcessFinalizers, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
}

// 
// Return address hijacking
//
COOP_PINVOKE_HELPER(void, RhpGcProbeHijackScalar, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
}
COOP_PINVOKE_HELPER(void, RhpGcProbeHijackObject, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
}
COOP_PINVOKE_HELPER(void, RhpGcProbeHijackByref, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
}
COOP_PINVOKE_HELPER(void, RhpGcStressHijackScalar, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
}
COOP_PINVOKE_HELPER(void, RhpGcStressHijackObject, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
}
COOP_PINVOKE_HELPER(void, RhpGcStressHijackByref, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
}

#ifdef USE_PORTABLE_HELPERS

COOP_PINVOKE_HELPER(void, RhpAssignRef, (Object ** dst, Object * ref))
{
    // @TODO: USE_PORTABLE_HELPERS - Null check
    *dst = ref;
    InlineWriteBarrier(dst, ref);
}

COOP_PINVOKE_HELPER(void, RhpCheckedAssignRef, (Object ** dst, Object * ref))
{
    // @TODO: USE_PORTABLE_HELPERS - Null check
    *dst = ref;
    InlineCheckedWriteBarrier(dst, ref);
}

COOP_PINVOKE_HELPER(Object *, RhpCheckedLockCmpXchg, (Object ** location, Object * value, Object * comparand))
{
    // @TODO: USE_PORTABLE_HELPERS - Null check
    Object * ret = (Object *)PalInterlockedCompareExchangePointer((void * volatile *)location, value, comparand);
    InlineCheckedWriteBarrier(location, value);
    return ret;
}

COOP_PINVOKE_HELPER(Object *, RhpCheckedXchg, (Object ** location, Object * value))
{
    // @TODO: USE_PORTABLE_HELPERS - Null check
    Object * ret = (Object *)PalInterlockedExchangePointer((void * volatile *)location, value);
    InlineCheckedWriteBarrier(location, value);
    return ret;
}

COOP_PINVOKE_HELPER(Int32, RhpLockCmpXchg32, (Int32 * location, Int32 value, Int32 comparand))
{
    // @TODO: USE_PORTABLE_HELPERS - Null check
    return PalInterlockedCompareExchange(location, value, comparand);
}

COOP_PINVOKE_HELPER(Int64, RhpLockCmpXchg64, (Int64 * location, Int64 value, Int32 comparand))
{
    // @TODO: USE_PORTABLE_HELPERS - Null check
    return PalInterlockedCompareExchange64(location, value, comparand);
}

#endif // USE_PORTABLE_HELPERS

COOP_PINVOKE_HELPER(void, RhpMemoryBarrier, ())
{
    PalMemoryBarrier();
}
