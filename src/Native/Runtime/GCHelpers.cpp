// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// Unmanaged helpers exposed by the System.GC managed class.
//

#include "common.h"
#include "gcenv.h"
#include "gc.h"
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

EXTERN_C REDHAWK_API void __cdecl RhWaitForPendingFinalizers(UInt32_BOOL allowReentrantWait)
{
    // This must be called via p/invoke rather than RuntimeImport since it blocks and could starve the GC if
    // called in cooperative mode.
    ASSERT(!GetThread()->IsCurrentThreadInCooperativeMode());

    FinalizerThread::Wait(INFINITE, allowReentrantWait);
}

EXTERN_C REDHAWK_API void __cdecl RhpCollect(UInt32 uGeneration, UInt32 uMode)
{
    // This must be called via p/invoke rather than RuntimeImport to make the stack crawlable.

    Thread * pCurThread = GetThread();

    pCurThread->SetupHackPInvokeTunnel();
    pCurThread->DisablePreemptiveMode();

    ASSERT(!pCurThread->IsDoNotTriggerGcSet());
    GCHeap::GetGCHeap()->GarbageCollect(uGeneration, FALSE, uMode);

    pCurThread->EnablePreemptiveMode();
}

EXTERN_C REDHAWK_API Int64 __cdecl RhpGetGcTotalMemory()
{
    // This must be called via p/invoke rather than RuntimeImport to make the stack crawlable.

    Thread * pCurThread = GetThread();

    pCurThread->SetupHackPInvokeTunnel();
    pCurThread->DisablePreemptiveMode();

    Int64 ret = GCHeap::GetGCHeap()->GetTotalBytesInUse();

    pCurThread->EnablePreemptiveMode();

    return ret;
}

COOP_PINVOKE_HELPER(void, RhSuppressFinalize, (OBJECTREF refObj))
{
    if (!refObj->get_EEType()->HasFinalizer())
        return;
    GCHeap::GetGCHeap()->SetFinalizationRun(refObj);
}

COOP_PINVOKE_HELPER(Boolean, RhReRegisterForFinalize, (OBJECTREF refObj))
{
    if (!refObj->get_EEType()->HasFinalizer())
        return Boolean_true;
    return GCHeap::GetGCHeap()->RegisterForFinalization(-1, refObj) ? Boolean_true : Boolean_false;
}

COOP_PINVOKE_HELPER(Int32, RhGetMaxGcGeneration, ())
{
    return GCHeap::GetGCHeap()->GetMaxGeneration();
}

COOP_PINVOKE_HELPER(Int32, RhGetGcCollectionCount, (Int32 generation, Boolean getSpecialGCCount))
{
    return GCHeap::GetGCHeap()->CollectionCount(generation, getSpecialGCCount);
}

COOP_PINVOKE_HELPER(Int32, RhGetGeneration, (OBJECTREF obj))
{
    return GCHeap::GetGCHeap()->WhichGeneration(obj);
}

COOP_PINVOKE_HELPER(Int32, RhGetGcLatencyMode, ())
{
    return GCHeap::GetGCHeap()->GetGcLatencyMode();
}

COOP_PINVOKE_HELPER(void, RhSetGcLatencyMode, (Int32 newLatencyMode))
{
    GCHeap::GetGCHeap()->SetGcLatencyMode(newLatencyMode);
}

COOP_PINVOKE_HELPER(Boolean, RhIsServerGc, ())
{
    return GCHeap::IsServerHeap();
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
    return GCHeap::GetGCHeap()->IsPromoted(obj) ? Boolean_true : Boolean_false;
}

COOP_PINVOKE_HELPER(Int32, RhGetLohCompactionMode, ())
{
    return GCHeap::GetGCHeap()->GetLOHCompactionMode();
}

COOP_PINVOKE_HELPER(void, RhSetLohCompactionMode, (Int32 newLohCompactionMode))
{
    GCHeap::GetGCHeap()->SetLOHCompactionMode(newLohCompactionMode);
}

COOP_PINVOKE_HELPER(Int64, RhGetCurrentObjSize, ())
{
    return GCHeap::GetGCHeap()->GetCurrentObjSize();
}

COOP_PINVOKE_HELPER(Int64, RhGetGCNow, ())
{
    return GCHeap::GetGCHeap()->GetNow();
}

COOP_PINVOKE_HELPER(Int64, RhGetLastGCStartTime, (Int32 generation))
{
    return GCHeap::GetGCHeap()->GetLastGCStartTime(generation);
}

COOP_PINVOKE_HELPER(Int64, RhGetLastGCDuration, (Int32 generation))
{
    return GCHeap::GetGCHeap()->GetLastGCDuration(generation);
}
