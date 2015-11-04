//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
#include "common.h"

#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "PalRedhawkCommon.h"
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
// runtimeexports.cs -- @TODO: use C# implementation
COOP_PINVOKE_HELPER(Object *, RhNewObject, (EEType* pEEType))
{
    ASSERT_MSG(!pEEType->RequiresAlign8() && !pEEType->HasFinalizer(), "NYI");

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
// runtimeexports.cs -- @TODO: use C# implementation
COOP_PINVOKE_HELPER(Array *, RhNewArray, (EEType * pArrayEEType, int numElements))
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

COOP_PINVOKE_HELPER(void, RhBox, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
}

COOP_PINVOKE_HELPER(void, RhpNewFast, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
}

COOP_PINVOKE_HELPER(void, RhpNewFinalizable, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
}

COOP_PINVOKE_HELPER(void, RhpNewArray, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
}

COOP_PINVOKE_HELPER(void, RhpInitialDynamicInterfaceDispatch, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
}


// finalizer.cs
COOP_PINVOKE_HELPER(void, RhpSetHaveNewClasslibs, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
}

// finalizer.cs
COOP_PINVOKE_HELPER(void, ProcessFinalizers, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
}

// runtimeexports.cs
COOP_PINVOKE_HELPER(void, RhpReversePInvokeBadTransition, ())
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

// TODO: The C++ write barrier helpers colide with assembly definitions in full runtime. Re-enable
// once this file is built for portable runtime only.
#if 0

//
// Write barriers
//

#ifdef BIT64
// Card byte shift is different on 64bit.
#define card_byte_shift     11
#else
#define card_byte_shift     10
#endif

#define card_byte(addr) (((size_t)(addr)) >> card_byte_shift)

COOP_PINVOKE_HELPER(void, RhpAssignRef, (Object ** dst, Object * ref))
{
    *dst = ref;

    if ((uint8_t*)ref >= g_ephemeral_low && (uint8_t*)ref < g_ephemeral_high)
    {
        // volatile is used here to prevent fetch of g_card_table from being reordered 
        // with g_lowest/highest_address check above. See comment in code:gc_heap::grow_brick_card_tables.
        uint8_t * pCardByte = (uint8_t *)*(volatile uint8_t **)(&g_card_table) + card_byte((uint8_t *)dst);
        if (*pCardByte != 0xFF)
            *pCardByte = 0xFF;
    }
}

COOP_PINVOKE_HELPER(void, RhpCheckedAssignRef, (Object ** dst, Object * ref))
{
    *dst = ref;

    // if the dst is outside of the heap (unboxed value classes) then we
    //      simply exit
    if (((uint8_t*)dst < g_lowest_address) || ((uint8_t*)dst >= g_highest_address))
        return;

    if ((uint8_t*)ref >= g_ephemeral_low && (uint8_t*)ref < g_ephemeral_high)
    {
        // volatile is used here to prevent fetch of g_card_table from being reordered 
        // with g_lowest/highest_address check above. See comment in code:gc_heap::grow_brick_card_tables.
        uint8_t* pCardByte = (uint8_t *)*(volatile uint8_t **)(&g_card_table) + card_byte((uint8_t *)dst);
        if (*pCardByte != 0xFF)
            *pCardByte = 0xFF;
    }
}

#endif

//
// type cast stuff from TypeCast.cs
//
COOP_PINVOKE_HELPER(void, RhTypeCast_IsInstanceOfClass, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
}
COOP_PINVOKE_HELPER(void, RhTypeCast_CheckCastClass, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
}
COOP_PINVOKE_HELPER(void, RhTypeCast_IsInstanceOfArray, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
}
COOP_PINVOKE_HELPER(void, RhTypeCast_CheckCastArray, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
}
COOP_PINVOKE_HELPER(void, RhTypeCast_IsInstanceOfInterface, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
}
COOP_PINVOKE_HELPER(void, RhTypeCast_CheckCastInterface, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
}
COOP_PINVOKE_HELPER(void, RhTypeCast_CheckVectorElemAddr, ())
{
    ASSERT_UNCONDITIONALLY("NYI");
}

