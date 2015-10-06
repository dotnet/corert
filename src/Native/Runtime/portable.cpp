//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
#include "rhcommon.h"

#include "commontypes.h"
#include "daccess.h"
#include "commonmacros.h"
#include "palredhawkcommon.h"
#include "palredhawk.h"
#include "assert.h"

#include "slist.h"
#include "gcrhinterface.h"
#include "module.h"
#include "varint.h"
#include "holder.h"
#include "rhbinder.h"
#include "crst.h"
#include "rwlock.h"
#include "runtimeinstance.h"
#include "event.h"
#include "regdisplay.h"
#include "stackframeiterator.h"
#include "thread.h"
#include "threadstore.h"

#include "eetype.h"
#include "objectlayout.h"

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

//
// Write barriers
//

// WriteBarriers.asm
COOP_PINVOKE_HELPER(void, RhpBulkWriteBarrier, (void* pMemStart, UInt32 cbMemSize))
{
    ASSERT_UNCONDITIONALLY("NYI");
}

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
