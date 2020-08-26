// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//
// Unmanaged helpers exposed by the System.GC managed class.
//

#include "common.h"
#include "gcenv.h"
#include "gcenv.ee.h"
#include "gcheaputilities.h"
#include "RestrictedCallouts.h"

#include "gcrhinterface.h"

#include "PalRedhawkCommon.h"
#include "slist.h"
#include "varint.h"
#include "regdisplay.h"
#include "StackFrameIterator.h"

#include "thread.h"
#include "RWLock.h"
#include "threadstore.h"
#include "threadstore.inl"
#include "thread.inl"

EXTERN_C REDHAWK_API void __cdecl RhpCollect(UInt32 uGeneration, UInt32 uMode)
{
    // This must be called via p/invoke rather than RuntimeImport to make the stack crawlable.

    Thread * pCurThread = ThreadStore::GetCurrentThread();

    pCurThread->SetupHackPInvokeTunnel();
    pCurThread->DisablePreemptiveMode();

    ASSERT(!pCurThread->IsDoNotTriggerGcSet());
    GCHeapUtilities::GetGCHeap()->GarbageCollect(uGeneration, FALSE, uMode);

    pCurThread->EnablePreemptiveMode();
}

EXTERN_C REDHAWK_API Int64 __cdecl RhpGetGcTotalMemory()
{
    // This must be called via p/invoke rather than RuntimeImport to make the stack crawlable.

    Thread * pCurThread = ThreadStore::GetCurrentThread();

    pCurThread->SetupHackPInvokeTunnel();
    pCurThread->DisablePreemptiveMode();

    Int64 ret = GCHeapUtilities::GetGCHeap()->GetTotalBytesInUse();

    pCurThread->EnablePreemptiveMode();

    return ret;
}

EXTERN_C REDHAWK_API Int32 __cdecl RhpStartNoGCRegion(Int64 totalSize, Boolean hasLohSize, Int64 lohSize, Boolean disallowFullBlockingGC)
{
    Thread *pCurThread = ThreadStore::GetCurrentThread();
    ASSERT(!pCurThread->IsCurrentThreadInCooperativeMode());

    pCurThread->SetupHackPInvokeTunnel();
    pCurThread->DisablePreemptiveMode();

    int result = GCHeapUtilities::GetGCHeap()->StartNoGCRegion(totalSize, hasLohSize, lohSize, disallowFullBlockingGC);

    pCurThread->EnablePreemptiveMode();

    return result;
}

EXTERN_C REDHAWK_API Int32 __cdecl RhpEndNoGCRegion()
{
    ASSERT(!ThreadStore::GetCurrentThread()->IsCurrentThreadInCooperativeMode());

    return GCHeapUtilities::GetGCHeap()->EndNoGCRegion();
}

COOP_PINVOKE_HELPER(void, RhSuppressFinalize, (OBJECTREF refObj))
{
    if (!refObj->get_EEType()->HasFinalizer())
        return;
    GCHeapUtilities::GetGCHeap()->SetFinalizationRun(refObj);
}

COOP_PINVOKE_HELPER(Boolean, RhReRegisterForFinalize, (OBJECTREF refObj))
{
    if (!refObj->get_EEType()->HasFinalizer())
        return Boolean_true;
    return GCHeapUtilities::GetGCHeap()->RegisterForFinalization(-1, refObj) ? Boolean_true : Boolean_false;
}

COOP_PINVOKE_HELPER(Int32, RhGetMaxGcGeneration, ())
{
    return GCHeapUtilities::GetGCHeap()->GetMaxGeneration();
}

COOP_PINVOKE_HELPER(Int32, RhGetGcCollectionCount, (Int32 generation, Boolean getSpecialGCCount))
{
    return GCHeapUtilities::GetGCHeap()->CollectionCount(generation, getSpecialGCCount);
}

COOP_PINVOKE_HELPER(Int32, RhGetGeneration, (OBJECTREF obj))
{
    return GCHeapUtilities::GetGCHeap()->WhichGeneration(obj);
}

COOP_PINVOKE_HELPER(Int32, RhGetGcLatencyMode, ())
{
    return GCHeapUtilities::GetGCHeap()->GetGcLatencyMode();
}

COOP_PINVOKE_HELPER(Int32, RhSetGcLatencyMode, (Int32 newLatencyMode))
{
    return GCHeapUtilities::GetGCHeap()->SetGcLatencyMode(newLatencyMode);
}

COOP_PINVOKE_HELPER(Boolean, RhIsServerGc, ())
{
    return GCHeapUtilities::IsServerHeap();
}

COOP_PINVOKE_HELPER(Boolean, RhRegisterGcCallout, (GcRestrictedCalloutKind eKind, void * pCallout))
{
    return RestrictedCallouts::RegisterGcCallout(eKind, pCallout);
}

COOP_PINVOKE_HELPER(void, RhUnregisterGcCallout, (GcRestrictedCalloutKind eKind, void * pCallout))
{
    RestrictedCallouts::UnregisterGcCallout(eKind, pCallout);
}

COOP_PINVOKE_HELPER(Boolean, RhIsPromoted, (OBJECTREF obj))
{
    return GCHeapUtilities::GetGCHeap()->IsPromoted(obj) ? Boolean_true : Boolean_false;
}

COOP_PINVOKE_HELPER(Int32, RhGetLohCompactionMode, ())
{
    return GCHeapUtilities::GetGCHeap()->GetLOHCompactionMode();
}

COOP_PINVOKE_HELPER(void, RhSetLohCompactionMode, (Int32 newLohCompactionMode))
{
    GCHeapUtilities::GetGCHeap()->SetLOHCompactionMode(newLohCompactionMode);
}

COOP_PINVOKE_HELPER(Int64, RhGetCurrentObjSize, ())
{
    return GCHeapUtilities::GetGCHeap()->GetCurrentObjSize();
}

COOP_PINVOKE_HELPER(Int64, RhGetGCNow, ())
{
    return GCHeapUtilities::GetGCHeap()->GetNow();
}

COOP_PINVOKE_HELPER(Int64, RhGetLastGCStartTime, (Int32 generation))
{
    return GCHeapUtilities::GetGCHeap()->GetLastGCStartTime(generation);
}

COOP_PINVOKE_HELPER(Int64, RhGetLastGCDuration, (Int32 generation))
{
    return GCHeapUtilities::GetGCHeap()->GetLastGCDuration(generation);
}

COOP_PINVOKE_HELPER(Boolean, RhRegisterForFullGCNotification, (Int32 maxGenerationThreshold, Int32 largeObjectHeapThreshold))
{
    ASSERT(maxGenerationThreshold >= 1 && maxGenerationThreshold <= 99);
    ASSERT(largeObjectHeapThreshold >= 1 && largeObjectHeapThreshold <= 99);
    return GCHeapUtilities::GetGCHeap()->RegisterForFullGCNotification(maxGenerationThreshold, largeObjectHeapThreshold)
        ? Boolean_true : Boolean_false;
}

COOP_PINVOKE_HELPER(Boolean, RhCancelFullGCNotification, ())
{
    return GCHeapUtilities::GetGCHeap()->CancelFullGCNotification() ? Boolean_true : Boolean_false;
}

COOP_PINVOKE_HELPER(Int32, RhWaitForFullGCApproach, (Int32 millisecondsTimeout))
{
    ASSERT(millisecondsTimeout >= -1);
    ASSERT(ThreadStore::GetCurrentThread()->IsCurrentThreadInCooperativeMode());

    int timeout = millisecondsTimeout == -1 ? INFINITE : millisecondsTimeout;
    return GCHeapUtilities::GetGCHeap()->WaitForFullGCApproach(millisecondsTimeout);
}

COOP_PINVOKE_HELPER(Int32, RhWaitForFullGCComplete, (Int32 millisecondsTimeout))
{
    ASSERT(millisecondsTimeout >= -1);
    ASSERT(ThreadStore::GetCurrentThread()->IsCurrentThreadInCooperativeMode());

    int timeout = millisecondsTimeout == -1 ? INFINITE : millisecondsTimeout;
    return GCHeapUtilities::GetGCHeap()->WaitForFullGCComplete(millisecondsTimeout);
}

COOP_PINVOKE_HELPER(Int64, RhGetGCSegmentSize, ())
{
    size_t first = GCHeapUtilities::GetGCHeap()->GetValidSegmentSize(Boolean_true);
    size_t second = GCHeapUtilities::GetGCHeap()->GetValidSegmentSize(Boolean_false);

    return (first > second) ? first : second;
}

COOP_PINVOKE_HELPER(Int64, RhGetAllocatedBytesForCurrentThread, ())
{
    Thread *pThread = ThreadStore::GetCurrentThread();
    gc_alloc_context *ac = pThread->GetAllocContext();
    Int64 currentAllocated = ac->alloc_bytes + ac->alloc_bytes_uoh - (ac->alloc_limit - ac->alloc_ptr);
    return currentAllocated;
}

struct RH_GC_GENERATION_INFO
{
    UInt64 sizeBefore;
    UInt64 fragmentationBefore;
    UInt64 sizeAfter;
    UInt64 fragmentationAfter;
};

#if defined(TARGET_X86) && !defined(TARGET_UNIX)
#include "pshpack4.h"
#ifdef _MSC_VER 
#pragma warning(push)
#pragma warning(disable:4121) // alignment of a member was sensitive to packing
#endif
#endif
struct RH_GH_MEMORY_INFO
{
public:
    UInt64 highMemLoadThresholdBytes;
    UInt64 totalAvailableMemoryBytes;
    UInt64 lastRecordedMemLoadBytes;
    UInt64 lastRecordedHeapSizeBytes;
    UInt64 lastRecordedFragmentationBytes;
    UInt64 totalCommittedBytes;
    UInt64 promotedBytes;
    UInt64 pinnedObjectCount;
    UInt64 finalizationPendingCount;
    UInt64 index;
    UInt32 generation;
    UInt32 pauseTimePercent;
    UInt8 isCompaction;
    UInt8 isConcurrent;
    RH_GC_GENERATION_INFO generationInfo0;
    RH_GC_GENERATION_INFO generationInfo1;
    RH_GC_GENERATION_INFO generationInfo2;
    RH_GC_GENERATION_INFO generationInfo3;
    RH_GC_GENERATION_INFO generationInfo4;
    UInt64 pauseDuration0;
    UInt64 pauseDuration1;
};
#if defined(TARGET_X86) && !defined(TARGET_UNIX)
#ifdef _MSC_VER
#pragma warning(pop)
#endif
#include "poppack.h"
#endif

COOP_PINVOKE_HELPER(void, RhGetMemoryInfo, (RH_GH_MEMORY_INFO* pData, int kind))
{
    UInt64* genInfoRaw = (UInt64*)&(pData->generationInfo0);
    UInt64* pauseInfoRaw = (UInt64*)&(pData->pauseDuration0);

    return GCHeapUtilities::GetGCHeap()->GetMemoryInfo(
        &(pData->highMemLoadThresholdBytes),
        &(pData->totalAvailableMemoryBytes),
        &(pData->lastRecordedMemLoadBytes),
        &(pData->lastRecordedHeapSizeBytes),
        &(pData->lastRecordedFragmentationBytes),
        &(pData->totalCommittedBytes),
        &(pData->promotedBytes),
        &(pData->pinnedObjectCount),
        &(pData->finalizationPendingCount),
        &(pData->index),
        &(pData->generation),
        &(pData->pauseTimePercent),
        (bool*)&(pData->isCompaction),
        (bool*)&(pData->isConcurrent),
        genInfoRaw,
        pauseInfoRaw,
        kind);
}

COOP_PINVOKE_HELPER(Int64, RhGetTotalAllocatedBytes, ())
{
    uint64_t allocated_bytes = GCHeapUtilities::GetGCHeap()->GetTotalAllocatedBytes() - RedhawkGCInterface::GetDeadThreadsNonAllocBytes();

    // highest reported allocated_bytes. We do not want to report a value less than that even if unused_bytes has increased.
    static uint64_t high_watermark;

    uint64_t current_high = high_watermark;
    while (allocated_bytes > current_high)
    {
        uint64_t orig = PalInterlockedCompareExchange64((Int64*)&high_watermark, allocated_bytes, current_high);
        if (orig == current_high)
            return allocated_bytes;

        current_high = orig;
    }

    return current_high;
}

EXTERN_C REDHAWK_API Int64 __cdecl RhGetTotalAllocatedBytesPrecise()
{
    Int64 allocated;

    // We need to suspend/restart the EE to get each thread's
    // non-allocated memory from their allocation contexts

    GCToEEInterface::SuspendEE(SUSPEND_REASON::SUSPEND_FOR_GC);
    
    allocated = GCHeapUtilities::GetGCHeap()->GetTotalAllocatedBytes() - RedhawkGCInterface::GetDeadThreadsNonAllocBytes();

    FOREACH_THREAD(pThread)
    {
        gc_alloc_context* ac = pThread->GetAllocContext();
        allocated -= ac->alloc_limit - ac->alloc_ptr;
    }
    END_FOREACH_THREAD

    GCToEEInterface::RestartEE(true);
    
    return allocated;
}

static Array* AllocateNewArrayImpl(Thread* pThread, EEType* pArrayEEType, UInt32 numElements, UInt32 flags)
{
    size_t size;
#ifndef HOST_64BIT
    // if the element count is <= 0x10000, no overflow is possible because the component size is
    // <= 0xffff, and thus the product is <= 0xffff0000, and the base size is only ~12 bytes
    if (numElements > 0x10000)
    {
        // Perform the size computation using 64-bit integeres to detect overflow
        uint64_t size64 = (uint64_t)pArrayEEType->get_BaseSize() + ((uint64_t)numElements * (uint64_t)pArrayEEType->get_ComponentSize());
        size64 = (size64 + (sizeof(UIntNative) - 1)) & ~(sizeof(UIntNative) - 1);

        size = (size_t)size64;
        if (size != size64)
        {
            return NULL;
        }
    }
    else
#endif // !HOST_64BIT
    {
        size = (size_t)pArrayEEType->get_BaseSize() + ((size_t)numElements * (size_t)pArrayEEType->get_ComponentSize());
        size = ALIGN_UP(size, sizeof(UIntNative));
    }

    size_t max_object_size;
#ifdef HOST_64BIT
    if (g_pConfig->GetGCAllowVeryLargeObjects())
    {
        max_object_size = (INT64_MAX - 7 - min_obj_size);
    }
    else
#endif // HOST_64BIT
    {
        max_object_size = (INT32_MAX - 7 - min_obj_size);
    }

    if (size >= max_object_size)
    {
        return NULL;
    }

    const int MaxArrayLength = 0x7FEFFFFF;
    const int MaxByteArrayLength = 0x7FFFFFC7;

    // Impose limits on maximum array length in each dimension to allow efficient
    // implementation of advanced range check elimination in future. We have to allow
    // higher limit for array of bytes (or one byte structs) for backward compatibility.
    // Keep in sync with Array.MaxArrayLength in BCL.
    if (size > MaxByteArrayLength /* note: comparing allocation size with element count */)
    {
        // Ensure the above if check covers the minimal interesting size
        static_assert(MaxByteArrayLength < (uint64_t)MaxArrayLength * 2, "");

        if (pArrayEEType->get_ComponentSize() != 1)
        {
            size_t elementCount = (size - pArrayEEType->get_BaseSize()) / pArrayEEType->get_ComponentSize();
            if (elementCount > MaxArrayLength)
                return NULL;
        }
        else
        {
            size_t elementCount = size - pArrayEEType->get_BaseSize();
            if (elementCount > MaxByteArrayLength)
                return NULL;
        }
    }

    if (size > RH_LARGE_OBJECT_SIZE)
        flags |= GC_ALLOC_LARGE_OBJECT_HEAP;

    // Save the EEType for instrumentation purposes.
    RedhawkGCInterface::SetLastAllocEEType(pArrayEEType);

    Array* pArray = (Array*)GCHeapUtilities::GetGCHeap()->Alloc(pThread->GetAllocContext(), size, flags);
    if (pArray == NULL)
    {
        return NULL;
    }

    pArray->set_EEType(pArrayEEType);
    pArray->InitArrayLength(numElements);

    if (size >= RH_LARGE_OBJECT_SIZE)
        GCHeapUtilities::GetGCHeap()->PublishObject((uint8_t*)pArray);

    return pArray;
}

EXTERN_C REDHAWK_API void RhAllocateNewArray(EEType* pArrayEEType, UInt32 numElements, UInt32 flags, Array** pResult)
{
    Thread* pThread = ThreadStore::GetCurrentThread();

    pThread->SetupHackPInvokeTunnel();
    pThread->DisablePreemptiveMode();

    ASSERT(!pThread->IsDoNotTriggerGcSet());

    *pResult = AllocateNewArrayImpl(pThread, pArrayEEType, numElements, flags);

    pThread->EnablePreemptiveMode();
}
