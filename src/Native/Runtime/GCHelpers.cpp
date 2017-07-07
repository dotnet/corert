// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// Unmanaged helpers exposed by the System.GC managed class.
//

#include "common.h"
#include "gcenv.h"
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

    Thread * pCurThread = GetThread();

    pCurThread->SetupHackPInvokeTunnel();
    pCurThread->DisablePreemptiveMode();

    ASSERT(!pCurThread->IsDoNotTriggerGcSet());
    GCHeapUtilities::GetGCHeap()->GarbageCollect(uGeneration, FALSE, uMode);

    pCurThread->EnablePreemptiveMode();
}

EXTERN_C REDHAWK_API Int64 __cdecl RhpGetGcTotalMemory()
{
    // This must be called via p/invoke rather than RuntimeImport to make the stack crawlable.

    Thread * pCurThread = GetThread();

    pCurThread->SetupHackPInvokeTunnel();
    pCurThread->DisablePreemptiveMode();

    Int64 ret = GCHeapUtilities::GetGCHeap()->GetTotalBytesInUse();

    pCurThread->EnablePreemptiveMode();

    return ret;
}

EXTERN_C REDHAWK_API Int32 __cdecl RhpStartNoGCRegion(Int64 totalSize, Boolean hasLohSize, Int64 lohSize, Boolean disallowFullBlockingGC)
{
    Thread *pCurThread = GetThread();
    ASSERT(!pCurThread->IsCurrentThreadInCooperativeMode());

    pCurThread->SetupHackPInvokeTunnel();
    pCurThread->DisablePreemptiveMode();

    int result = GCHeapUtilities::GetGCHeap()->StartNoGCRegion(totalSize, hasLohSize, lohSize, disallowFullBlockingGC);

    pCurThread->EnablePreemptiveMode();

    return result;
}

EXTERN_C REDHAWK_API Int32 __cdecl RhpEndNoGCRegion()
{
    ASSERT(!GetThread()->IsCurrentThreadInCooperativeMode());

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
    ASSERT(GetThread()->IsCurrentThreadInCooperativeMode());

    int timeout = millisecondsTimeout == -1 ? INFINITE : millisecondsTimeout;
    return GCHeapUtilities::GetGCHeap()->WaitForFullGCApproach(millisecondsTimeout);
}

COOP_PINVOKE_HELPER(Int32, RhWaitForFullGCComplete, (Int32 millisecondsTimeout))
{
    ASSERT(millisecondsTimeout >= -1);
    ASSERT(GetThread()->IsCurrentThreadInCooperativeMode());

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
    Thread *pThread = GetThread();
    gc_alloc_context *ac = pThread->GetAllocContext();
    Int64 currentAllocated = ac->alloc_bytes + ac->alloc_bytes_loh - (ac->alloc_limit - ac->alloc_ptr);
    return currentAllocated;
}
