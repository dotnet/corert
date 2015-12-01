//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

//
// Unmanaged GC memory helpers
//

#ifndef DACCESS_COMPILE
#ifdef WRITE_BARRIER_CHECK
extern uint8_t* g_GCShadow;
extern uint8_t* g_GCShadowEnd;
typedef DPTR(uint8_t)   PTR_uint8_t;
extern "C" {
    GPTR_DECL(uint8_t, g_lowest_address);
    GPTR_DECL(uint8_t, g_highest_address);
}
#endif

typedef DPTR(uint32_t)   PTR_uint32_t;
extern "C" {
    GPTR_DECL(uint32_t, g_card_table);
}
static const UInt32 INVALIDGCVALUE = 0xcccccccd;

FORCEINLINE void InlinedBulkWriteBarrier(void* pMemStart, UInt32 cbMemSize)
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
    // VolatileLoadWithoutBarrier() is used here to prevent fetch of g_card_table from being reordered 
    // with g_lowest/highest_address check at the beginning of this function. 
    uint8_t* card = ((uint8_t*)VolatileLoadWithoutBarrier(&g_card_table)) + startingClump;

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
#endif // DACCESS_COMPILE
