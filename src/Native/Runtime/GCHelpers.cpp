//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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

COOP_PINVOKE_HELPER(void, RhSuppressFinalize, (OBJECTREF refObj))
{
    if (!refObj->get_EEType()->HasFinalizer())
        return;
    GCHeap::GetGCHeap()->SetFinalizationRun(refObj);
}

EXTERN_C REDHAWK_API void __cdecl RhWaitForPendingFinalizers(BOOL allowReentrantWait)
{
    // This must be called via p/invoke rather than RuntimeImport since it blocks and could starve the GC if
    // called in cooperative mode.
    ASSERT(!GetThread()->PreemptiveGCDisabled());

    FinalizerThread::Wait(INFINITE, allowReentrantWait);
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

COOP_PINVOKE_HELPER(void, RhReRegisterForFinalizeHelper, (OBJECTREF obj))
{
    if (obj->get_EEType()->HasFinalizer())
        GCHeap::GetGCHeap()->RegisterForFinalization(-1, obj);
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

COOP_PINVOKE_HELPER(Int64, RhGetGcTotalMemoryHelper, ())
{
    return GCHeap::GetGCHeap()->GetTotalBytesInUse();
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
