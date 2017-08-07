// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// Unmanaged GC memory helpers
//

// This function fills a piece of memory in a GC safe way.  It makes the guarantee
// that it will fill memory in at least pointer sized chunks whenever possible.
// Unaligned memory at the beginning and remaining bytes at the end are written bytewise.
// We must make this guarantee whenever we clear memory in the GC heap that could contain 
// object references.  The GC or other user threads can read object references at any time, 
// clearing them bytewise can result in a read on another thread getting incorrect data.  
FORCEINLINE void InlineGCSafeFillMemory(void * mem, size_t size, size_t pv)
{
    UInt8 * memBytes = (UInt8 *)mem;
    UInt8 * endBytes = &memBytes[size];

    // handle unaligned bytes at the beginning
    while (!IS_ALIGNED(memBytes, sizeof(void *)) && (memBytes < endBytes))
        *memBytes++ = (UInt8)pv;

    // now write pointer sized pieces
    // volatile ensures that this doesn't get optimized back into a memset call
    size_t nPtrs = (endBytes - memBytes) / sizeof(void *);
    volatile UIntNative* memPtr = (UIntNative*)memBytes;
    for (size_t i = 0; i < nPtrs; i++)
        *memPtr++ = pv;

    // handle remaining bytes at the end
    memBytes = (UInt8*)memPtr;
    while (memBytes < endBytes)
        *memBytes++ = (UInt8)pv;
}

// These functions copy memory in a GC safe way.  They makes the guarantee
// that the memory is copies in at least pointer sized chunks.

FORCEINLINE void InlineForwardGCSafeCopy(void * dest, const void *src, size_t len)
{
    // All parameters must be pointer-size-aligned
    ASSERT(IS_ALIGNED(dest, sizeof(size_t)));
    ASSERT(IS_ALIGNED(src, sizeof(size_t)));
    ASSERT(IS_ALIGNED(len, sizeof(size_t)));

    size_t size = len;
    UInt8 * dmem = (UInt8 *)dest;
    UInt8 * smem = (UInt8 *)src;

    // regions must be non-overlapping
    ASSERT(dmem <= smem || smem + size <= dmem);

    // copy 4 pointers at a time 
    while (size >= 4 * sizeof(size_t))
    {
        size -= 4 * sizeof(size_t);
        ((size_t *)dmem)[0] = ((size_t *)smem)[0];
        ((size_t *)dmem)[1] = ((size_t *)smem)[1];
        ((size_t *)dmem)[2] = ((size_t *)smem)[2];
        ((size_t *)dmem)[3] = ((size_t *)smem)[3];
        smem += 4 * sizeof(size_t);
        dmem += 4 * sizeof(size_t);
    }

    // copy 2 trailing pointers, if needed
    if ((size & (2 * sizeof(size_t))) != 0)
    {
        ((size_t *)dmem)[0] = ((size_t *)smem)[0];
        ((size_t *)dmem)[1] = ((size_t *)smem)[1];
        smem += 2 * sizeof(size_t);
        dmem += 2 * sizeof(size_t);
    }

    // finish with one pointer, if needed
    if ((size & sizeof(size_t)) != 0)
    {
        ((size_t *)dmem)[0] = ((size_t *)smem)[0];
    }
}

FORCEINLINE void InlineBackwardGCSafeCopy(void * dest, const void *src, size_t len)
{
    // All parameters must be pointer-size-aligned
    ASSERT(IS_ALIGNED(dest, sizeof(size_t)));
    ASSERT(IS_ALIGNED(src, sizeof(size_t)));
    ASSERT(IS_ALIGNED(len, sizeof(size_t)));

    size_t size = len;
    UInt8 * dmem = (UInt8 *)dest + len;
    UInt8 * smem = (UInt8 *)src + len;

    // regions must be non-overlapping
    ASSERT(smem <= dmem || dmem + size <= smem);

    // copy 4 pointers at a time 
    while (size >= 4 * sizeof(size_t))
    {
        size -= 4 * sizeof(size_t);
        smem -= 4 * sizeof(size_t);
        dmem -= 4 * sizeof(size_t);
        ((size_t *)dmem)[3] = ((size_t *)smem)[3];
        ((size_t *)dmem)[2] = ((size_t *)smem)[2];
        ((size_t *)dmem)[1] = ((size_t *)smem)[1];
        ((size_t *)dmem)[0] = ((size_t *)smem)[0];
    }

    // copy 2 trailing pointers, if needed
    if ((size & (2 * sizeof(size_t))) != 0)
    {
        smem -= 2 * sizeof(size_t);
        dmem -= 2 * sizeof(size_t);
        ((size_t *)dmem)[1] = ((size_t *)smem)[1];
        ((size_t *)dmem)[0] = ((size_t *)smem)[0];
    }

    // finish with one pointer, if needed
    if ((size & sizeof(size_t)) != 0)
    {
        smem -= sizeof(size_t);
        dmem -= sizeof(size_t);
        ((size_t *)dmem)[0] = ((size_t *)smem)[0];
    }
}


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

FORCEINLINE void InlineWriteBarrier(void * dst, void * ref)
{
    if (((uint8_t*)ref >= g_ephemeral_low) && ((uint8_t*)ref < g_ephemeral_high))
    {
        // volatile is used here to prevent fetch of g_card_table from being reordered 
        // with g_lowest/highest_address check above. See comment in code:gc_heap::grow_brick_card_tables.
        uint8_t* pCardByte = (uint8_t *)VolatileLoadWithoutBarrier(&g_card_table) + ((size_t)dst >> LOG2_CLUMP_SIZE);
        if (*pCardByte != 0xFF)
            *pCardByte = 0xFF;
    }
}

FORCEINLINE void InlineCheckedWriteBarrier(void * dst, void * ref)
{
    // if the dst is outside of the heap (unboxed value classes) then we
    //      simply exit
    if (((uint8_t*)dst < g_lowest_address) || ((uint8_t*)dst >= g_highest_address))
        return;

    InlineWriteBarrier(dst, ref);
}

FORCEINLINE void InlinedBulkWriteBarrier(void* pMemStart, size_t cbMemSize)
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
