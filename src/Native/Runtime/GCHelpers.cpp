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

#ifdef WRITE_BARRIER_CHECK
extern uint8_t* g_GCShadow;
extern uint8_t* g_GCShadowEnd;
extern uint8_t* g_lowest_address;
extern uint8_t* g_highest_address;
#endif

extern uint32_t* g_card_table;
static UInt32 INVALIDGCVALUE = 0xcccccccd;

extern "C" void RhpBulkWriteBarrier(void* pMemStart, UInt32 cbMemSize)
{
    // Check whether the writes were even into the heap. If not there's no card update required.
    // Also if the size is smaller than a pointer, no write barrier is required.
    // This case can occur with universal shared generic code where the size
    // is not known at compile time.
    if (pMemStart < g_lowest_address || (pMemStart >= g_highest_address) || (cbMemSize < sizeof(UIntNative)))
    {
        return;
    }

#ifdef WRITE_BARRIER_CHECK
    // Perform shadow heap updates corresponding to the gc heap updates that immediately preceded this helper
    // call.

    // If g_GCShadow is 0, don't perform the check.
    if (g_GCShadow != NULL)
    {
        // Compute the shadow heap address corresponding to the beginning of the range of heap addresses modified
        // and in the process range check it to make sure we have the shadow version allocated.
        UIntNative* shadowSlot = (UIntNative*)(g_GCShadow + ((uint8_t*)pMemStart - g_lowest_address));
        if (shadowSlot <= (UIntNative*)g_GCShadowEnd)
        {
            // Iterate over every pointer sized slot in the range, copying data from the real heap to the shadow heap.
            // As we perform each copy we need to recheck the real heap contents with an ordered read to ensure we're
            // not racing with another heap updater. If we discover a race we invalidate the corresponding shadow heap
            // slot using a special well-known value so that this location will not be tested during the next shadow
            // heap validation.

            UIntNative* realSlot = (UIntNative*)pMemStart;
            UIntNative slotCount = cbMemSize / sizeof(UIntNative);
            do
            {
                // Update shadow slot from real slot.
                UIntNative realValue = *realSlot;
                *shadowSlot = realValue;
                // Memory barrier to ensure the next read is ordered wrt to the shadow heap write we just made.
                PalMemoryBarrier();

                // Read the real slot contents again. If they don't agree with what we just wrote then someone just raced
                // with us and updated the heap again. In such cases we invalidate the shadow slot.
                if (*realSlot != realValue)
                {
                    *shadowSlot = INVALIDGCVALUE;
                }

                realSlot++;
                shadowSlot++;
                slotCount--;
            }
            while (slotCount > 0);
        }
    }

#endif // WRITE_BARRIER_CHECK

    // Compute the starting card address and the number of bytes to write (groups of 8 cards). We could try
    // for further optimization here using aligned 32-bit writes but there's some overhead in setup required
    // and additional complexity. It's not clear this is warranted given that a single byte of card table
    // update already covers 1K of object space (2K on 64-bit platforms). It's also not worth probing that
    // 1K/2K range to see if any of the pointers appear to be non-ephemeral GC references. Given the size of
    // the area the chances are high that at least one interesting GC refenence is present.

    size_t startAddress = (size_t)pMemStart;
    size_t endAddress = startAddress + cbMemSize;
    size_t startingClump = startAddress >> LOG2_CLUMP_SIZE;
    size_t endingClump = (endAddress + CLUMP_SIZE - 1) >> LOG2_CLUMP_SIZE;

    // calculate the number of clumps to mark (round_up(end) - start)
    size_t clumpCount = endingClump - startingClump;
    uint8_t* card = ((uint8_t*)g_card_table) + startingClump;

    // Fill the cards. To avoid cache line thrashing we check whether the cards have already been set before
    // writing.
    do
    {
        if (*card != 0xff)
        {
            *card = 0xff;
        }

        card++;
        clumpCount--;
    }
    while (clumpCount != 0);
}
