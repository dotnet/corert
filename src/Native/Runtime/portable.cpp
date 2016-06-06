// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#include "common.h"

#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "PalRedhawkCommon.h"
#include "CommonMacros.inl"
#include "Volatile.h"
#include "PalRedhawk.h"
#include "rhassert.h"

#include "slist.h"
#include "gcrhinterface.h"
#include "shash.h"
#include "RWLock.h"
#include "module.h"
#include "varint.h"
#include "holder.h"
#include "rhbinder.h"
#include "Crst.h"
#include "RuntimeInstance.h"
#include "event.h"
#include "regdisplay.h"
#include "StackFrameIterator.h"
#include "thread.h"
#include "threadstore.h"
#include "threadstore.inl"

#include "eetype.h"
#include "ObjectLayout.h"

#include "GCMemoryHelpers.h"
#include "GCMemoryHelpers.inl"

#ifdef USE_PORTABLE_HELPERS

EXTERN_C REDHAWK_API void* REDHAWK_CALLCONV RhpGcAlloc(EEType *pEEType, UInt32 uFlags, UIntNative cbSize, void * pTransitionFrame);
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

    pObject = (Object *)RhpGcAlloc(pEEType, 0, size, NULL);
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

    size_t size = pEEType->get_BaseSize();

    Object * pObject = (Object *)RhpGcAlloc(pEEType, GC_ALLOC_FINALIZE, size, NULL);
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

    if (numElements < 0)
    {
        ASSERT_UNCONDITIONALLY("NYI");  // TODO: Throw overflow
    }

    size_t size;
#ifndef BIT64
    // if the element count is <= 0x10000, no overflow is possible because the component size is
    // <= 0xffff, and thus the product is <= 0xffff0000, and the base size is only ~12 bytes
    if (numElements > 0x10000)
    {
        // Perform the size computation using 64-bit integeres to detect overflow
        uint64_t size64 = (uint64_t)pArrayEEType->get_BaseSize() + ((uint64_t)numElements * (uint64_t)pArrayEEType->get_ComponentSize());
        size64 = (size64 + (sizeof(UIntNative)-1)) & ~(sizeof(UIntNative)-1);

        size = (size_t)size64;
        if (size != size64)
        {
            ASSERT_UNCONDITIONALLY("NYI");  // TODO: Throw overflow
        }
    }
    else
#endif // !BIT64
    {
        size = (size_t)pArrayEEType->get_BaseSize() + ((size_t)numElements * (size_t)pArrayEEType->get_ComponentSize());
        size = ALIGN_UP(size, sizeof(UIntNative));
    }

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

    pObject = (Array *)RhpGcAlloc(pArrayEEType, 0, size, NULL);
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

//
// PInvoke
//
COOP_PINVOKE_HELPER(void, RhpPInvoke, (void* pFrame))
{
    // TODO: RhpPInvoke
}

COOP_PINVOKE_HELPER(void, RhpPInvokeReturn, (void* pFrame))
{
    // TODO: RhpPInvokeReturn
}

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

typedef UIntTarget (*TargetFunc2)(UIntTarget, UIntTarget);
COOP_PINVOKE_HELPER(UIntTarget, ManagedCallout2, (UIntTarget argument1, UIntTarget argument2, void *pTargetMethod, void *pPreviousManagedFrame))
{
    TargetFunc2 target = (TargetFunc2)pTargetMethod;
    return (*target)(argument1, argument2);
}
#endif // USE_PORTABLE_HELPERS

// @TODO Implement UniversalTransition
EXTERN_C void * ReturnFromUniversalTransition;
void * ReturnFromUniversalTransition;

// @TODO Implement CallDescrThunk
EXTERN_C void * ReturnFromCallDescrThunk;
void * ReturnFromCallDescrThunk;

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

COOP_PINVOKE_HELPER(void, Native_GetThunksBase, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
}

COOP_PINVOKE_HELPER(void, Native_GetNumThunksPerMapping, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
}

COOP_PINVOKE_HELPER(void, Native_GetThunkSize, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
}

COOP_PINVOKE_HELPER(void, RhCallDescrWorker, (void * callDescr))
{
    ASSERT_UNCONDITIONALLY("NYI");
}

COOP_PINVOKE_HELPER(void, RhpETWLogLiveCom, (Int32 eventType, void * ccwHandle, void * objectId, void * typeRawValue, void * iUnknown, void * vTable, Int32 comRefCount, Int32 jupiterRefCount, Int32 flags))
{
    ASSERT_UNCONDITIONALLY("NYI");
}

COOP_PINVOKE_HELPER(bool, RhpETWShouldWalkCom, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
    return false;
}
