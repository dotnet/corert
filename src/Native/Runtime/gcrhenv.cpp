//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

//
// This module provides data storage and implementations needed by gcrhenv.h to help provide an isolated build
// and runtime environment in which GC and HandleTable code can exist with minimal modifications from the CLR
// mainline. See gcrhenv.h for a more detailed explanation of how this all fits together.
//

#include "common.h"

#include "gcenv.h"
#include "gc.h"

#include "RestrictedCallouts.h"

#include "gcrhinterface.h"

#include "slist.h"
#include "varint.h"
#include "regdisplay.h"
#include "StackFrameIterator.h"

#include "thread.h"

#include "module.h"
#include "RWLock.h"
#include "RuntimeInstance.h"
#include "objecthandle.h"
#include "eetype.inl"
#include "RhConfig.h"

#include "threadstore.h"

#include "gcdesc.h"
#include "SyncClean.hpp"

#include "daccess.h"


GPTR_IMPL(EEType, g_pFreeObjectEEType);

#define USE_CLR_CACHE_SIZE_BEHAVIOR


#ifndef DACCESS_COMPILE
bool StartFinalizerThread();

// Undo the definitions of any macros set up for GC code which conflict with our usage of PAL APIs below.
#undef GetCurrentThreadId
#undef DebugBreak

//
// -----------------------------------------------------------------------------------------------------------
//
// Various global data cells the GC and/or HandleTable rely on. Some are just here to enable easy compilation:
// their value doesn't matter since it won't be consumed at runtime. Others we may have to initialize to some
// reasonable value. A few we might have to manage through the lifetime of the runtime. Each is considered on
// a case by case basis.
//

#endif // !DACCESS_COMPILE

#ifndef DACCESS_COMPILE

//
// Simplified EEConfig -- It is just a static member, which statically initializes to the default values and
// has no dynamic initialization.  Some settings may change at runtime, however.  (Example: gcstress is
// enabled via a compiled-in call from a given managed module, not through snooping an environment setting.)
//
static EEConfig s_sDummyConfig;
EEConfig* g_pConfig = &s_sDummyConfig;

int EEConfig::GetHeapVerifyLevel()
{
    return g_pRhConfig->GetHeapVerify();
}

int EEConfig::GetGCconcurrent()
{
    return !g_pRhConfig->GetDisableBGC();
}

// A few settings are now backed by the cut-down version of Redhawk configuration values.
static RhConfig g_sRhConfig;
RhConfig * g_pRhConfig = &g_sRhConfig;


#if defined(FEATURE_ETW) && !defined(USE_PORTABLE_HELPERS)
//
// -----------------------------------------------------------------------------------------------------------
//
// The automatically generated part of the Redhawk ETW infrastructure (EtwEvents.h) calls the following
// function whenever the system enables or disables tracing for this provider.
//

UInt32 EtwCallback(UInt32 IsEnabled, RH_ETW_CONTEXT * pContext)
{
    if (IsEnabled &&
        (pContext->RegistrationHandle == Microsoft_Windows_Redhawk_GC_PrivateHandle) &&
        GCHeap::IsGCHeapInitialized())
    {
        FireEtwGCSettings(GCHeap::GetGCHeap()->GetValidSegmentSize(FALSE),
                          GCHeap::GetGCHeap()->GetValidSegmentSize(TRUE),
                          GCHeap::IsServerHeap());
        GCHeap::GetGCHeap()->TraceGCSegments();
    }

    // Special check for the runtime provider's GCHeapCollectKeyword.  Profilers
    // flick this to force a full GC.
    if (IsEnabled && 
        (pContext->RegistrationHandle == Microsoft_Windows_Redhawk_GC_PublicHandle) &&
        GCHeap::IsGCHeapInitialized() &&
        ((pContext->MatchAnyKeyword & CLR_GCHEAPCOLLECT_KEYWORD) != 0))
    {
        // Profilers may (optionally) specify extra data in the filter parameter
        // to log with the GCStart event.
        LONGLONG l64ClientSequenceNumber = 0;
        if ((pContext->FilterData != NULL) &&
            (pContext->FilterData->Type == 1) &&
            (pContext->FilterData->Size == sizeof(l64ClientSequenceNumber)))
        {
            l64ClientSequenceNumber = *(LONGLONG *) (pContext->FilterData->Ptr);
        }
        ETW::GCLog::ForceGC(l64ClientSequenceNumber);
    }

    return 0;
}
#endif // FEATURE_ETW

//
// -----------------------------------------------------------------------------------------------------------
//
// The rest of Redhawk needs to be able to talk to the GC/HandleTable code (to initialize it, allocate
// objects etc.) without pulling in the entire adaptation layer provided by this file and gcrhenv.h. To this
// end the rest of Redhawk talks to us via a simple interface described in gcrhinterface.h. We provide the
// implementation behind those APIs here.
//

// Perform any runtime-startup initialization needed by the GC, HandleTable or environmental code in gcrhenv.
// The boolean parameter should be true if a server GC is required and false for workstation. Returns true on
// success or false if a subsystem failed to initialize.

#ifndef DACCESS_COMPILE
// static 
bool RedhawkGCInterface::InitializeSubsystems(GCType gcType)
{
    g_pConfig->Construct();

#if defined(FEATURE_ETW) && !defined(USE_PORTABLE_HELPERS)
    MICROSOFT_WINDOWS_REDHAWK_GC_PRIVATE_PROVIDER_Context.IsEnabled = FALSE;
    MICROSOFT_WINDOWS_REDHAWK_GC_PUBLIC_PROVIDER_Context.IsEnabled = FALSE;

    // Register the Redhawk event provider with the system.
    RH_ETW_REGISTER_Microsoft_Windows_Redhawk_GC_Private();
    RH_ETW_REGISTER_Microsoft_Windows_Redhawk_GC_Public();

    MICROSOFT_WINDOWS_REDHAWK_GC_PRIVATE_PROVIDER_Context.RegistrationHandle = Microsoft_Windows_Redhawk_GC_PrivateHandle;
    MICROSOFT_WINDOWS_REDHAWK_GC_PUBLIC_PROVIDER_Context.RegistrationHandle = Microsoft_Windows_Redhawk_GC_PublicHandle;
#endif // FEATURE_ETW

    InitializeSystemInfo();

    // Initialize the special EEType used to mark free list entries in the GC heap.
    EEType *pFreeObjectType = new (nothrow) EEType();      //@TODO: remove 'new'
    pFreeObjectType->InitializeAsGcFreeType();

    // Place the pointer to this type in a global cell (typed as the structurally equivalent MethodTable
    // that the GC understands).
    g_pFreeObjectMethodTable = (MethodTable *)pFreeObjectType;
    g_pFreeObjectEEType = pFreeObjectType;

    // Set the GC heap type.
    bool fUseServerGC = (gcType == GCType_Server);
    GCHeap::InitializeHeapType(fUseServerGC);

    // Create the GC heap itself.
    GCHeap *pGCHeap = GCHeap::CreateGCHeap();
    if (!pGCHeap)
        return false;

    // Initialize the GC subsystem.
    HRESULT hr = pGCHeap->Initialize();
    if (FAILED(hr))
        return false;

    if (!FinalizerThread::Initialize())
        return false;

    // Initialize HandleTable.
    if (!Ref_Initialize())
        return false;

    return true;
}
#endif // !DACCESS_COMPILE

// Allocate an object on the GC heap.
//  pThread         -  current Thread
//  cbSize          -  size in bytes of the final object
//  uFlags          -  GC type flags (see gc.h GC_ALLOC_*)
//  pEEType         -  type of the object
// Returns a pointer to the object allocated or NULL on failure.

// static
void* RedhawkGCInterface::Alloc(Thread *pThread, UIntNative cbSize, UInt32 uFlags, EEType *pEEType)
{
    ASSERT(GCHeap::UseAllocationContexts());
    ASSERT(!pThread->IsDoNotTriggerGcSet());

    // Save the EEType for instrumentation purposes.
    SetLastAllocEEType(pEEType);

    Object * pObject;
#ifdef FEATURE_64BIT_ALIGNMENT
    if (uFlags & GC_ALLOC_ALIGN8)
        pObject = GCHeap::GetGCHeap()->AllocAlign8(pThread->GetAllocContext(), cbSize, uFlags);
    else
#endif // FEATURE_64BIT_ALIGNMENT
        pObject = GCHeap::GetGCHeap()->Alloc(pThread->GetAllocContext(), cbSize, uFlags);

    // NOTE: we cannot call PublishObject here because the object isn't initialized!

    return pObject;
}

// returns the object pointer for caller's convenience
COOP_PINVOKE_HELPER(void*, RhpPublishObject, (void* pObject, UIntNative cbSize))
{
    UNREFERENCED_PARAMETER(cbSize);
    ASSERT(cbSize >= LARGE_OBJECT_SIZE);
    GCHeap::GetGCHeap()->PublishObject((BYTE*)pObject);
    return pObject;
}

#if 0 // @TODO: This is unused, why is it here?

// Allocate an object on the large GC heap. Used when you want to force an allocation on the large heap
// that wouldn't normally go there (e.g. objects containing double fields).
//  cbSize          -  size in bytes of the final object
//  uFlags          -  GC type flags (see gc.h GC_ALLOC_*)
// Returns a pointer to the object allocated or NULL on failure.

// static 
void* RedhawkGCInterface::AllocLarge(UIntNative cbSize, UInt32 uFlags)
{
    ASSERT(!GetThread()->IsDoNotTriggerGcSet());
    Object * pObject = GCHeap::GetGCHeap()->AllocLHeap(cbSize, uFlags);
    // NOTE: we cannot call PublishObject here because the object isn't initialized!
    return pObject;
}
#endif

// static
void RedhawkGCInterface::InitAllocContext(alloc_context * pAllocContext)
{
    // NOTE: This method is currently unused because the thread's alloc_context is initialized via
    // static initialization of tls_CurrentThread.  If the initial contents of the alloc_context
    // ever change, then a matching change will need to be made to the tls_CurrentThread static
    // initializer.

    pAllocContext->init();
}

// static
void RedhawkGCInterface::ReleaseAllocContext(alloc_context * pAllocContext)
{
    GCHeap::GetGCHeap()->FixAllocContext(pAllocContext, FALSE, NULL, NULL);
}

// static 
void RedhawkGCInterface::WaitForGCCompletion()
{
    ASSERT(GCHeap::IsGCHeapInitialized());

    GCHeap::GetGCHeap()->WaitUntilGCComplete();
}

#endif // !DACCESS_COMPILE

//
// -----------------------------------------------------------------------------------------------------------
//
// AppDomain emulation. The we don't have these in Redhawk so instead we emulate the bare minimum of the API
// touched by the GC/HandleTable and pretend we have precisely one (default) appdomain.
//

// Used by DAC, but since this just exposes [System|App]Domain::GetIndex we can just keep a local copy.

SystemDomain g_sSystemDomain;
AppDomain g_sDefaultDomain;

#ifndef DACCESS_COMPILE

//
// -----------------------------------------------------------------------------------------------------------
//
// Trivial sync block cache. Will no doubt be replaced with a real implementation soon.
//

#ifdef VERIFY_HEAP
SyncBlockCache g_sSyncBlockCache;
#endif // VERIFY_HEAP

//-------------------------------------------------------------------------------------------------
// Used only by GC initialization, this initializes the EEType used to mark free entries in the GC heap. It
// should be an array type with a component size of one (so the GC can easily size it as appropriate) and
// should be marked as not containing any references. The rest of the fields don't matter: the GC does not
// query them and the rest of the runtime will never hold a reference to free object.

void EEType::InitializeAsGcFreeType()
{
    m_usComponentSize = 1;
    m_usFlags = ParameterizedEEType;
    m_uBaseSize = sizeof(Array) + SYNC_BLOCK_SKEW;
}

#endif // !DACCESS_COMPILE

extern void GcEnumObject(PTR_OBJECTREF pObj, UInt32 flags, EnumGcRefCallbackFunc * fnGcEnumRef, EnumGcRefScanContext * pSc);
extern void GcEnumObjectsConservatively(PTR_OBJECTREF pLowerBound, PTR_OBJECTREF pUpperBound, EnumGcRefCallbackFunc * fnGcEnumRef, EnumGcRefScanContext * pSc);
extern void GcBulkEnumObjects(PTR_OBJECTREF pObjs, DWORD cObjs, EnumGcRefCallbackFunc * fnGcEnumRef, EnumGcRefScanContext * pSc);

struct EnumGcRefContext : GCEnumContext
{
    EnumGcRefCallbackFunc * f;
    EnumGcRefScanContext * sc;
};

static void EnumGcRefsCallback(void * hCallback, PTR_PTR_VOID pObject, UInt32 flags)
{
    EnumGcRefContext * pCtx = (EnumGcRefContext *)hCallback;

    GcEnumObject((PTR_OBJECTREF)pObject, flags, pCtx->f, pCtx->sc);
}

// static 
void RedhawkGCInterface::EnumGcRefs(ICodeManager * pCodeManager,
                                    MethodInfo * pMethodInfo, 
                                    UInt32 codeOffset,
                                    REGDISPLAY * pRegisterSet,
                                    void * pfnEnumCallback,
                                    void * pvCallbackData)
{
    EnumGcRefContext ctx;
    ctx.pCallback = EnumGcRefsCallback;
    ctx.f  = (EnumGcRefCallbackFunc *)pfnEnumCallback;
    ctx.sc = (EnumGcRefScanContext *)pvCallbackData;

    pCodeManager->EnumGcRefs(pMethodInfo, 
                             codeOffset,
                             pRegisterSet,
                             &ctx);
}

// static
void RedhawkGCInterface::EnumGcRefsInRegionConservatively(PTR_RtuObjectRef pLowerBound,
                                                          PTR_RtuObjectRef pUpperBound,
                                                          void * pfnEnumCallback,
                                                          void * pvCallbackData)
{
    GcEnumObjectsConservatively((PTR_OBJECTREF)pLowerBound, (PTR_OBJECTREF)pUpperBound, (EnumGcRefCallbackFunc *)pfnEnumCallback, (EnumGcRefScanContext *)pvCallbackData);
}

// static 
void RedhawkGCInterface::EnumGcRef(PTR_RtuObjectRef pRef, GCRefKind kind, void * pfnEnumCallback, void * pvCallbackData)
{
    ASSERT((GCRK_Object == kind) || (GCRK_Byref == kind));

    DWORD flags = 0;

    if (kind == GCRK_Byref)
    {
        flags |= GC_CALL_INTERIOR;
    }

    GcEnumObject((PTR_OBJECTREF)pRef, flags, (EnumGcRefCallbackFunc *)pfnEnumCallback, (EnumGcRefScanContext *)pvCallbackData);
}

#ifndef DACCESS_COMPILE

// static
void RedhawkGCInterface::BulkEnumGcObjRef(PTR_RtuObjectRef pRefs, UInt32 cRefs, void * pfnEnumCallback, void * pvCallbackData)
{
    GcBulkEnumObjects((PTR_OBJECTREF)pRefs, cRefs, (EnumGcRefCallbackFunc *)pfnEnumCallback, (EnumGcRefScanContext *)pvCallbackData);
}

// static
void RedhawkGCInterface::GarbageCollect(UInt32 uGeneration, UInt32 uMode)
{
    ASSERT(!GetThread()->IsDoNotTriggerGcSet());
    GCHeap::GetGCHeap()->GarbageCollect(uGeneration, FALSE, uMode);
}

// static 
GcSegmentHandle RedhawkGCInterface::RegisterFrozenSection(void * pSection, UInt32 SizeSection)
{
#ifdef FEATURE_BASICFREEZE
    segment_info seginfo;

    seginfo.pvMem           = pSection;
    seginfo.ibFirstObject   = sizeof(ObjHeader);
    seginfo.ibAllocated     = SizeSection;
    seginfo.ibCommit        = seginfo.ibAllocated;
    seginfo.ibReserved      = seginfo.ibAllocated;

    return (GcSegmentHandle)GCHeap::GetGCHeap()->RegisterFrozenSegment(&seginfo);
#else // FEATURE_BASICFREEZE
    return NULL;
#endif // FEATURE_BASICFREEZE    
}

// static 
void RedhawkGCInterface::UnregisterFrozenSection(GcSegmentHandle segment)
{
    GCHeap::GetGCHeap()->UnregisterFrozenSegment((segment_handle)segment);
}

EXTERN_C UInt32_BOOL g_fGcStressStarted = UInt32_FALSE; // UInt32_BOOL because asm code reads it
#ifdef FEATURE_GC_STRESS
// static 
void RedhawkGCInterface::StressGc()
{
    if (!g_fGcStressStarted || GetThread()->IsSuppressGcStressSet() || GetThread()->IsDoNotTriggerGcSet())
    {
        return;
    }

    GarbageCollect((UInt32) -1, collection_blocking);
}
#endif // FEATURE_GC_STRESS


#ifdef FEATURE_GC_STRESS
COOP_PINVOKE_HELPER(void, RhpInitializeGcStress, ())
{
    g_fGcStressStarted = UInt32_TRUE;
    g_pConfig->SetGCStressLevel(EEConfig::GCSTRESS_INSTR_NGEN);   // this is the closest CLR equivalent to what we do.
    GetRuntimeInstance()->EnableGcPollStress();
}
#endif // FEATURE_GC_STRESS

#endif // !DACCESS_COMPILE

//
// Support for scanning the GC heap, objects and roots.
//

// The value of the following globals determines whether a callback is made for every live object at the end
// of a garbage collection. Only one callback/context pair can be active for any given collection, so setting
// these has to be co-ordinated carefully, see RedhawkGCInterface::ScanHeap below.
GcScanObjectFunction g_pfnHeapScan = NULL;  // Function to call for every live object at the end of a GC
void * g_pvHeapScanContext = NULL;          // User context passed on each call to the function above

//
// Initiate a full garbage collection and call the speficied function with the given context for each object
// that remians alive on the heap at the end of the collection (note that the function will be called while
// the GC still has cooperative threads suspended).
//
// If a GC is in progress (or another caller is in the process of scheduling a similar scan) we'll wait our
// turn and then initiate a further collection.
//
// static
void RedhawkGCInterface::ScanHeap(GcScanObjectFunction pfnScanCallback, void *pContext)
{
#ifndef DACCESS_COMPILE
    // Carefully attempt to set the global callback function (careful in that we won't overwrite another scan
    // that's being scheduled or in-progress). If someone beat us to it back off and wait for the
    // corresponding GC to complete.
    while (FastInterlockCompareExchangePointer(&g_pfnHeapScan, pfnScanCallback, NULL) != NULL)
    {
        // Wait in pre-emptive mode to avoid stalling another thread that's attempting a collection.
        Thread * pCurThread = GetThread();
        ASSERT(pCurThread->PreemptiveGCDisabled());
        pCurThread->EnablePreemptiveGC();

        // Give the other thread some time to get the collection going.
        if (PalSwitchToThread() == 0)
            PalSleep(1);

        // Wait for the collection to complete (if the other thread didn't manage to schedule it yet we'll
        // just end up going round the loop again).
        WaitForGCCompletion();

        // Come back into co-operative mode.
        pCurThread->DisablePreemptiveGC();
    }

    // We should never end up overwriting someone else's callback context when we won the race to set the
    // callback function pointer.
    ASSERT(g_pvHeapScanContext == NULL);
    g_pvHeapScanContext = pContext;

    // Initiate a full garbage collection (0xffffffff == all generations).
    GarbageCollect(0xffffffff, collection_blocking);
    WaitForGCCompletion();

    // Release our hold on the global scanning pointers.
    g_pvHeapScanContext = NULL;
    FastInterlockExchangePointer(&g_pfnHeapScan, NULL);
#else
    UNREFERENCED_PARAMETER(pfnScanCallback);
    UNREFERENCED_PARAMETER(pContext);
#endif // DACCESS_COMPILE
}

// Enumerate every reference field in an object, calling back to the specified function with the given context
// for each such reference found.
// static
void RedhawkGCInterface::ScanObject(void *pObject, GcScanObjectFunction pfnScanCallback, void *pContext)
{
#if !defined(DACCESS_COMPILE) && (defined(GC_PROFILING) || defined(FEATURE_EVENT_TRACE))
    GCHeap::GetGCHeap()->WalkObject((Object*)pObject, (walk_fn)pfnScanCallback, pContext);
#else
    UNREFERENCED_PARAMETER(pObject);
    UNREFERENCED_PARAMETER(pfnScanCallback);
    UNREFERENCED_PARAMETER(pContext);
#endif // DACCESS_COMPILE
}

// When scanning for object roots we use existing GC APIs used for object promotion and moving. We use an
// adapter callback to transform the promote function signature used for these methods into something simpler
// that avoids exposing unnecessary implementation details. The pointer to a ScanContext normally passed to
// promotion functions is actually a pointer to the structure below which serves to recall the actual function
// pointer and context for the real context.
struct ScanRootsContext
{
    GcScanRootFunction  m_pfnCallback;
    void *              m_pContext;
};

// Callback with a EnumGcRefCallbackFunc signature that forwards the call to a callback with a GcScanFunction signature
// and its own context.
void ScanRootsCallbackWrapper(Object** pObject, EnumGcRefScanContext* pContext, DWORD dwFlags)
{
    UNREFERENCED_PARAMETER(dwFlags);

    ScanRootsContext * pRealContext = (ScanRootsContext*)pContext;

    (*pRealContext->m_pfnCallback)((void**)&pObject, pRealContext->m_pContext);
}

// Enumerate all the object roots located on the specified thread's stack. It is only safe to call this from
// the context of a GC.
//
// static
void RedhawkGCInterface::ScanStackRoots(Thread *pThread, GcScanRootFunction pfnScanCallback, void *pContext)
{
#ifndef DACCESS_COMPILE
    ScanRootsContext sContext;
    sContext.m_pfnCallback = pfnScanCallback;
    sContext.m_pContext = pContext;

    pThread->GcScanRoots(ScanRootsCallbackWrapper, &sContext);
#else
    UNREFERENCED_PARAMETER(pThread);
    UNREFERENCED_PARAMETER(pfnScanCallback);
    UNREFERENCED_PARAMETER(pContext);
#endif // !DACCESS_COMPILE
}

// Enumerate all the object roots located in statics. It is only safe to call this from the context of a GC.
//
// static
void RedhawkGCInterface::ScanStaticRoots(GcScanRootFunction pfnScanCallback, void *pContext)
{
#ifndef DACCESS_COMPILE
    ScanRootsContext sContext;
    sContext.m_pfnCallback = pfnScanCallback;
    sContext.m_pContext = pContext;

    GetRuntimeInstance()->EnumAllStaticGCRefs(ScanRootsCallbackWrapper, &sContext);
#else
    UNREFERENCED_PARAMETER(pfnScanCallback);
    UNREFERENCED_PARAMETER(pContext);
#endif // !DACCESS_COMPILE
}

// Enumerate all the object roots located in handle tables. It is only safe to call this from the context of a
// GC.
//
// static
void RedhawkGCInterface::ScanHandleTableRoots(GcScanRootFunction pfnScanCallback, void *pContext)
{
#if !defined(DACCESS_COMPILE) && (defined(GC_PROFILING) || defined(FEATURE_EVENT_TRACE))
    ScanRootsContext sContext;
    sContext.m_pfnCallback = pfnScanCallback;
    sContext.m_pContext = pContext;
    Ref_ScanPointers(2, 2, (EnumGcRefScanContext*)&sContext, ScanRootsCallbackWrapper);
#else
    UNREFERENCED_PARAMETER(pfnScanCallback);
    UNREFERENCED_PARAMETER(pContext);
#endif // !DACCESS_COMPILE
}

#ifndef DACCESS_COMPILE

// This may only be called from a point at which the runtime is suspended. Currently, this
// is used by the VSD infrastructure on a SyncClean::CleanUp callback from the GC when
// a collection is complete.
bool RedhawkGCInterface::IsScanInProgress()
{
    // Only allow callers that have no RH thread or are in cooperative mode; i.e., don't
    // call this in preemptive mode, as the result would not be reliable in multi-threaded
    // environments.
    ASSERT(GetThread() == NULL || GetThread()->PreemptiveGCDisabled());
    return g_pfnHeapScan != NULL;
}

// This may only be called from a point at which the runtime is suspended. Currently, this
// is used by the VSD infrastructure on a SyncClean::CleanUp callback from the GC when
// a collection is complete.
GcScanObjectFunction RedhawkGCInterface::GetCurrentScanCallbackFunction()
{
    ASSERT(IsScanInProgress());
    return g_pfnHeapScan;
}

// This may only be called from a point at which the runtime is suspended. Currently, this
// is used by the VSD infrastructure on a SyncClean::CleanUp callback from the GC when
// a collection is complete.
void* RedhawkGCInterface::GetCurrentScanContext()
{
    ASSERT(IsScanInProgress());
    return g_pvHeapScanContext;
}

UInt32 RedhawkGCInterface::GetGCDescSize(void * pType)
{
    MethodTable * pMT = (MethodTable *)pType;

    if (!pMT->ContainsPointersOrCollectible())
        return 0;

    return (UInt32)CGCDesc::GetCGCDescFromMT(pMT)->GetSize();
}

COOP_PINVOKE_HELPER(void, RhpCopyObjectContents, (Object* pobjDest, Object* pobjSrc))
{
    SIZE_T cbDest = pobjDest->GetSize() - sizeof(ObjHeader);
    SIZE_T cbSrc = pobjSrc->GetSize() - sizeof(ObjHeader);
    if (cbSrc != cbDest)
        return;

    memcpy(pobjDest, pobjSrc, cbDest);
    GCHeap::GetGCHeap()->SetCardsAfterBulkCopy((Object**) pobjDest, cbDest);
}

// Move memory, in a way that is compatible with a move onto the heap, but
// does not require the destination pointer to be on the heap.
EXTERN_C void REDHAWK_CALLCONV RhpBulkWriteBarrier(void* pMemStart, UInt32 cbMemSize);

COOP_PINVOKE_HELPER(void, RhBulkMoveWithWriteBarrier, (BYTE* pDest, BYTE* pSrc, int cbDest))
{
    memmove(pDest, pSrc, cbDest);
    // Use RhpBulkWriteBarrier here instead of SetCardsAfterBulkCopy as RhpBulkWriteBarrier
    // is both faster, and is compatible with a destination that isn't the GC heap.
    RhpBulkWriteBarrier(pDest, cbDest);
}

COOP_PINVOKE_HELPER(void, RhpBox, (Object * pObj, void * pData))
{
    EEType * pEEType = pObj->get_EEType();

    // Can box value types only (which also implies no finalizers).
    ASSERT(pEEType->get_IsValueType() && !pEEType->HasFinalizer());

    // cbObject includes ObjHeader (sync block index) and the EEType* field from Object and is rounded up to
    // suit GC allocation alignment requirements. cbFields on the other hand is just the raw size of the field
    // data.
    SIZE_T cbFieldPadding = pEEType->get_ValueTypeFieldPadding();
    SIZE_T cbObject = pEEType->get_BaseSize();
    SIZE_T cbFields = cbObject - (sizeof(ObjHeader) + sizeof(EEType*) + cbFieldPadding);
    
    UInt8 * pbFields = (UInt8*)pObj + sizeof(EEType*);

    // Copy the unboxed value type data into the new object.
    memcpy(pbFields, pData, cbFields);

    // Perform any write barriers necessary for embedded reference fields.
    if (pEEType->HasReferenceFields())
        GCHeap::GetGCHeap()->SetCardsAfterBulkCopy((Object**)pbFields, cbFields);
}

COOP_PINVOKE_HELPER(void, RhUnbox, (Object * pObj, void * pData, EEType * pUnboxToEEType))
{
    // When unboxing to a Nullable the input object may be null.
    if (pObj == NULL)
    {
        ASSERT(pUnboxToEEType && pUnboxToEEType->IsNullable());

        // The first field of the Nullable is a Boolean which we must set to false in this case to indicate no
        // value is present.
        *(Boolean*)pData = FALSE;

        // Clear the value (in case there were GC references we wish to stop reporting).
        EEType * pEEType = pUnboxToEEType->GetNullableType();
        SIZE_T cbFieldPadding = pEEType->get_ValueTypeFieldPadding();
        SIZE_T cbFields = pEEType->get_BaseSize() - (sizeof(ObjHeader) + sizeof(EEType*) + cbFieldPadding);
        memset((UInt8*)pData + pUnboxToEEType->GetNullableValueOffset(), 0, cbFields);

        return;
    }

    EEType * pEEType = pObj->get_EEType();

    // Can unbox value types only.
    ASSERT(pEEType->get_IsValueType());

    // A special case is that we can unbox a value type T into a Nullable<T>. It's the only case where
    // pUnboxToEEType is useful.
    ASSERT((pUnboxToEEType == NULL) || pEEType->IsEquivalentTo(pUnboxToEEType) || pUnboxToEEType->IsNullable());
    if (pUnboxToEEType && pUnboxToEEType->IsNullable())
    {
        ASSERT(pUnboxToEEType->GetNullableType()->IsEquivalentTo(pEEType));

        // Set the first field of the Nullable to true to indicate the value is present.
        *(Boolean*)pData = TRUE;

        // Adjust the data pointer so that it points at the value field in the Nullable.
        pData = (UInt8*)pData + pUnboxToEEType->GetNullableValueOffset();
    }

    SIZE_T cbFieldPadding = pEEType->get_ValueTypeFieldPadding();
    SIZE_T cbFields = pEEType->get_BaseSize() - (sizeof(ObjHeader) + sizeof(EEType*) + cbFieldPadding);
    UInt8 * pbFields = (UInt8*)pObj + sizeof(EEType*);

    // Copy the boxed fields into the new location.
    memcpy(pData, pbFields, cbFields);

    // Perform any write barriers necessary for embedded reference fields. SetCardsAfterBulkCopy doesn't range
    // check the address we pass it and in this case we don't know whether pData really points into the GC
    // heap or not. If we call it with an address outside of the GC range we could end up setting a card
    // outside of the allocated range of the card table, i.e. corrupt memory.
    if (pEEType->HasReferenceFields() && (pData >= g_lowest_address) && (pData < g_highest_address))
        GCHeap::GetGCHeap()->SetCardsAfterBulkCopy((Object**)pData, cbFields);
}

#endif // !DACCESS_COMPILE

//
// -----------------------------------------------------------------------------------------------------------
//
// Support for shutdown finalization, which is off by default but can be enabled by the class library.
//

// If true runtime shutdown will attempt to finalize all finalizable objects (even those still rooted).
bool g_fPerformShutdownFinalization = false;

// Time to wait (in milliseconds) for the above finalization to complete before giving up and proceeding with
// shutdown. Can specify INFINITE for no timeout. 
UInt32 g_uiShutdownFinalizationTimeout = 0;

// Flag set to true once we've begun shutdown (and before shutdown finalization begins). This is exported to
// the class library so that managed code can tell when it is safe to access other objects from finalizers.
bool g_fShutdownHasStarted = false;

#ifndef DACCESS_COMPILE
Thread * GetThread()
{
    return ThreadStore::GetCurrentThread();
}

// If the class library has requested it, call this method on clean shutdown (i.e. return from Main) to
// perform a final pass of finalization where all finalizable objects are processed regardless of whether
// they are still rooted.
// static
void RedhawkGCInterface::ShutdownFinalization()
{
    FinalizerThread::WatchDog();
}

// Thread static representing the last allocation.
// This is used to log the type information for each slow allocation.
DECLSPEC_THREAD
EEType * RedhawkGCInterface::tls_pLastAllocationEEType = NULL;

// Get the last allocation for this thread.
EEType * RedhawkGCInterface::GetLastAllocEEType()
{
    return tls_pLastAllocationEEType;
}

// Set the last allocation for this thread.
void RedhawkGCInterface::SetLastAllocEEType(EEType * pEEType)
{
    tls_pLastAllocationEEType = pEEType;
}

void GCToEEInterface::SuspendEE(GCToEEInterface::SUSPEND_REASON reason)
{
#ifdef FEATURE_EVENT_TRACE
    ETW::GCLog::ETW_GC_INFO Info;
    Info.SuspendEE.Reason = reason;
    Info.SuspendEE.GcCount = (((reason == SUSPEND_FOR_GC) || (reason == SUSPEND_FOR_GC_PREP)) ?
        (UInt32)GCHeap::GetGCHeap()->GetGcCount() : (UInt32)-1);
#endif // FEATURE_EVENT_TRACE

    FireEtwGCSuspendEEBegin_V1(Info.SuspendEE.Reason, Info.SuspendEE.GcCount, GetClrInstanceId());

    g_TrapReturningThreads = TRUE;
    GCHeap::GetGCHeap()->SetGCInProgress(TRUE);

    GetThreadStore()->SuspendAllThreads(GCHeap::GetGCHeap()->GetWaitForGCEvent());

    FireEtwGCSuspendEEEnd_V1(GetClrInstanceId());

#ifdef APP_LOCAL_RUNTIME
    // now is a good opportunity to retry starting the finalizer thread
    StartFinalizerThread();
#endif
}

void GCToEEInterface::RestartEE(bool /*bFinishedGC*/)
{
    FireEtwGCRestartEEBegin_V1(GetClrInstanceId());

    SyncClean::CleanUp();

    GetThreadStore()->ResumeAllThreads(GCHeap::GetGCHeap()->GetWaitForGCEvent());
    GCHeap::GetGCHeap()->SetGCInProgress(FALSE);

    g_TrapReturningThreads = FALSE; // @TODO: map this to something meaningful in the new algorithm

    FireEtwGCRestartEEEnd_V1(GetClrInstanceId());
}

void GCToEEInterface::ScanStackRoots(Thread * /*pThread*/, promote_func* /*fn*/, ScanContext* /*sc*/)
{
    // TODO: Implement - Scan stack roots on given thread
}

void GCToEEInterface::ScanStaticGCRefsOpportunistically(promote_func* /*fn*/, ScanContext* /*sc*/)
{
}

void GCToEEInterface::GcStartWork(int condemned, int /*max_gen*/)
{
    // Invoke any registered callouts for the start of the collection.
    RestrictedCallouts::InvokeGcCallouts(GCRC_StartCollection, condemned);
}

// EE can perform post stack scanning action, while the user threads are still suspended 
void GCToEEInterface::AfterGcScanRoots(int condemned, int /*max_gen*/, ScanContext* /*sc*/)
{
    // Invoke any registered callouts for the end of the mark phase.
    RestrictedCallouts::InvokeGcCallouts(GCRC_AfterMarkPhase, condemned);
}

void GCToEEInterface::GcBeforeBGCSweepWork()
{
}

void GCToEEInterface::GcDone(int condemned)
{
    // Invoke any registered callouts for the end of the collection.
    RestrictedCallouts::InvokeGcCallouts(GCRC_EndCollection, condemned);
}

bool GCToEEInterface::RefCountedHandleCallbacks(Object * pObject)
{
    return RestrictedCallouts::InvokeRefCountedHandleCallbacks(pObject);
}

void GCToEEInterface::SyncBlockCacheWeakPtrScan(HANDLESCANPROC /*scanProc*/, uintptr_t /*lp1*/, uintptr_t /*lp2*/)
{
}

void GCToEEInterface::SyncBlockCacheDemote(int /*max_gen*/)
{
}

void GCToEEInterface::SyncBlockCachePromotionsGranted(int /*max_gen*/)
{
}

// does not acquire thread store lock
void GCToEEInterface::AttachCurrentThread()
{
    ThreadStore::AttachCurrentThread(false);
}

Thread * GCToEEInterface::GetThreadList(Thread * /*pThread*/)
{
    ASSERT(!"Intentionally not implemented"); // not used on this runtime
    return nullptr;
}

void GCToEEInterface::SetGCSpecial(Thread * pThread)
{
    pThread->SetGCSpecial(true);
}

alloc_context * GCToEEInterface::GetAllocContext(Thread * pThread)
{
    return pThread->GetAllocContext();
}

bool GCToEEInterface::CatchAtSafePoint(Thread * pThread)
{
    return pThread->CatchAtSafePoint();
}
#endif // !DACCESS_COMPILE

bool GCToEEInterface::IsPreemptiveGCDisabled(Thread * pThread)
{
    return pThread->PreemptiveGCDisabled();
}

void GCToEEInterface::EnablePreemptiveGC(Thread * pThread)
{
    return pThread->EnablePreemptiveGC();
}

void GCToEEInterface::DisablePreemptiveGC(Thread * pThread)
{
    pThread->DisablePreemptiveGC();
}


// NOTE: this method is not in thread.cpp because it needs access to the layout of alloc_context for DAC to know the 
// size, but thread.cpp doesn't generally need to include the GC environment headers for any other reason.
alloc_context * Thread::GetAllocContext()
{
    return dac_cast<DPTR(alloc_context)>(dac_cast<TADDR>(this) + offsetof(Thread, m_rgbAllocContextBuffer));
}

bool IsGCSpecialThread()
{
    // TODO: Implement for background GC
    return false;
}

#ifdef FEATURE_PREMORTEM_FINALIZATION
GPTR_IMPL(Thread, g_pFinalizerThread);
GPTR_IMPL(Thread, g_pGcThread);
CLREventStatic* hEventFinalizer = nullptr;
CLREventStatic* hEventFinalizerDone = nullptr;

#ifndef DACCESS_COMPILE
// Finalizer method implemented by redhawkm.
extern "C" void __cdecl ProcessFinalizers();

// Unmanaged front-end to the finalizer thread. We require this because at the point the GC creates the
// finalizer thread we're still executing the DllMain for RedhawkU. At that point we can't run managed code
// successfully (in particular module initialization code has not run for RedhawkM). Instead this method waits
// for the first finalization request (by which time everything must be up and running) and kicks off the
// managed portion of the thread at that point.
UInt32 WINAPI FinalizerStart(void* pContext)
{
    HANDLE hFinalizerEvent = (HANDLE)pContext;

    ThreadStore::AttachCurrentThread();
    Thread * pThread = GetThread();

    // Disallow gcstress on this thread to work around the current implementation's limitation that it will 
    // get into an infinite loop if performed on the finalizer thread.
    pThread->SetSuppressGcStress();

    FinalizerThread::SetFinalizerThread(pThread);

    // Wait for a finalization request.
    UInt32 uResult = PalWaitForSingleObjectEx(hFinalizerEvent, INFINITE, FALSE);
    ASSERT(uResult == WAIT_OBJECT_0);

    // Since we just consumed the request (and the event is auto-reset) we must set the event again so the
    // managed finalizer code will immediately start processing the queue when we run it.
    UInt32_BOOL fResult = PalSetEvent(hFinalizerEvent);
    ASSERT(fResult);

    // Run the managed portion of the finalizer. Until we implement (non-process) shutdown this call will
    // never return.

    ProcessFinalizers();

    ASSERT(!"Finalizer thread should never return");
    return 0;
}

bool StartFinalizerThread()
{
#ifdef APP_LOCAL_RUNTIME

    //
    // On app-local runtimes, if we're running with the fallback PAL code (meaning we don't have IManagedRuntimeServices)
    // then we use the WinRT ThreadPool to create the finalizer thread.  This might fail at startup, if the current thread
    // hasn't been CoInitialized.  So we need to retry this later.  We use fFinalizerThreadCreated to track whether we've
    // successfully created the finalizer thread yet, and also as a sort of lock to make sure two threads don't try
    // to create the finalizer thread at the same time.
    //
    static volatile Int32 fFinalizerThreadCreated;

    if (FastInterlockExchange(&fFinalizerThreadCreated, 1) != 1)
    {
        if (!PalStartFinalizerThread(FinalizerStart, (void*)FinalizerThread::GetFinalizerEvent()))
        {
            // Need to try again another time...
            FastInterlockExchange(&fFinalizerThreadCreated, 0);
        }
    }

    // We always return true, so the GC can start even if we failed. 
    return true;

#else // APP_LOCAL_RUNTIME

    //
    // If this isn't an app-local runtime, then the PAL will just call CreateThread directly, which should succeed
    // under normal circumstances.
    //
    if (PalStartFinalizerThread(FinalizerStart, (void*)FinalizerThread::GetFinalizerEvent()))
        return true;
    else
        return false;

#endif // APP_LOCAL_RUNTIME
}

bool FinalizerThread::Initialize()
{
    // Allocate the events the GC expects the finalizer thread to have. The hEventFinalizer event is signalled
    // by the GC whenever it completes a collection where it found otherwise unreachable finalizable objects.
    // The hEventFinalizerDone event is set by the finalizer thread every time it wakes up and drains the
    // queue of finalizable objects. It's mainly used by GC.WaitForPendingFinalizers(). The
    // hEventFinalizerToShutDown and hEventShutDownToFinalizer are used to synchronize the main thread and the
    // finalizer during the optional final finalization pass at shutdown.
    hEventFinalizerDone = new (nothrow) CLREventStatic();
    hEventFinalizerDone->CreateManualEvent(FALSE);
    hEventFinalizer = new (nothrow) CLREventStatic();
    hEventFinalizer->CreateAutoEvent(FALSE);

    // Create the finalizer thread itself.
    if (!StartFinalizerThread())
        return false;

    return true;
}

void FinalizerThread::SetFinalizerThread(Thread * pThread)
{
    g_pFinalizerThread = PTR_Thread(pThread);
}

void FinalizerThread::EnableFinalization()
{
    // Signal to finalizer thread that there are objects to finalize
    hEventFinalizer->Set();
}

void FinalizerThread::SignalFinalizationDone(bool /*fFinalizer*/)
{
    hEventFinalizerDone->Set();
}

bool FinalizerThread::HaveExtraWorkForFinalizer()
{
    return g_pFinalizerThread->HaveExtraWorkForFinalizer();
}

bool FinalizerThread::IsCurrentThreadFinalizer()
{
    return GetThread() == g_pFinalizerThread;
}

HANDLE FinalizerThread::GetFinalizerEvent()
{
    return hEventFinalizer->GetOSEvent();
}

// This is called during runtime shutdown to perform a final finalization run with all pontentially
// finalizable objects being finalized (as if their roots had all been cleared). The default behaviour is to
// skip this step, the classlib has to make an explicit request for this functionality and also specifies the
// maximum amount of time it will let the finalization take before we will give up and just let the shutdown
// proceed.
bool FinalizerThread::WatchDog()
{
    // Set the flag indicating that shutdown has started. This is only of interest to managed code running
    // finalizers as it lets them know when it is no longer safe to access other objects (which from this
    // point on can be finalized even if you hold a reference to them).
    g_fShutdownHasStarted = true;

    if (g_fPerformShutdownFinalization)
    {
#ifdef BACKGROUND_GC
        // Switch off concurrent GC if necessary.
        gc_heap::gc_can_use_concurrent = FALSE;

        if (pGenGCHeap->settings.concurrent)
            pGenGCHeap->background_gc_wait();
#endif //BACKGROUND_GC

        DWORD dwTimeout = g_uiShutdownFinalizationTimeout;

        // Wait for any outstanding finalization run to complete. Time this initial operation so that it forms
        // part of the overall timeout budget.
        DWORD dwStartTime = GetTickCount();
        Wait(dwTimeout);
        DWORD dwEndTime = GetTickCount();

        // In the exceedingly rare case that the tick count wrapped then we'll just reset the timeout to its
        // initial value. Otherwise we'll subtract the time we waited from the timeout budget (being mindful
        // of the fact that we might have waited slightly longer than the timeout specified).
        if (dwTimeout != INFINITE)
        {
            if (dwEndTime < dwStartTime)
                dwTimeout = g_uiShutdownFinalizationTimeout;
            else
                dwTimeout -= min(dwTimeout, dwEndTime - dwStartTime);

            if (dwTimeout == 0)
                return false;
        }

        // Inform the GC that all finalizable objects should now be placed in the queue for finalization. FALSE
        // here means we don't hold the finalizer lock (so the routine will take it for us).
        GCHeap::GetGCHeap()->SetFinalizeQueueForShutdown(FALSE);

        // Wait for the finalizer to process all of these objects.
        Wait(dwTimeout);

        if (dwTimeout == INFINITE)
            return true;

        // Do a zero timeout wait of the finalizer done event to determine if we timed out above (we don't
        // want to modify the signature of GCHeap::FinalizerThreadWait to return this data since that bleeds
        // into a CLR visible change to gc.h which is not really worth it for this minor case).
        return hEventFinalizerDone->Wait(0, FALSE) == WAIT_OBJECT_0;
    }

    return true;
}

void FinalizerThread::Wait(DWORD timeout, bool allowReentrantWait)
{
    // Can't call this from the finalizer thread itself.
    if (!IsCurrentThreadFinalizer())
    {
        // Clear any current indication that a finalization pass is finished and wake the finalizer thread up
        // (if there's no work to do it'll set the done event immediately).
        hEventFinalizerDone->Reset();
        EnableFinalization();

#ifdef APP_LOCAL_RUNTIME
        // We may have failed to create the finalizer thread at startup.  
        // Try again now.
        StartFinalizerThread();
#endif

        // Wait for the finalizer thread to get back to us.
        hEventFinalizerDone->Wait(timeout, false, allowReentrantWait);
    }
}
#endif // !DACCESS_COMPILE
#endif // FEATURE_PREMORTEM_FINALIZATION

#ifndef DACCESS_COMPILE
void GetProcessMemoryLoad(GCMemoryStatus* pGCMemStatus)
{
    // @TODO: no way to communicate failure
    PalGlobalMemoryStatusEx(pGCMemStatus);
}

bool __SwitchToThread(uint32_t /*dwSleepMSec*/, uint32_t /*dwSwitchCount*/)
{
    return !!PalSwitchToThread();
}

void * ClrVirtualAlloc(
    void * lpAddress,
    size_t dwSize,
    uint32_t flAllocationType,
    uint32_t flProtect)
{
    return PalVirtualAlloc(lpAddress, dwSize, flAllocationType, flProtect);
}

void * ClrVirtualAllocAligned(
    void * lpAddress,
    size_t dwSize,
    uint32_t flAllocationType,
    uint32_t flProtect,
    size_t /*dwAlignment*/)
{
    return PalVirtualAlloc(lpAddress, dwSize, flAllocationType, flProtect);
}

bool ClrVirtualFree(
    void * lpAddress,
    size_t dwSize,
    uint32_t dwFreeType)
{
    return !!PalVirtualFree(lpAddress, dwSize, dwFreeType);
}
#endif // DACCESS_COMPILE

bool
ClrVirtualProtect(
    void * lpAddress,
    size_t dwSize,
    uint32_t flNewProtect,
    uint32_t * lpflOldProtect)
{
    UNREFERENCED_PARAMETER(lpAddress);
    UNREFERENCED_PARAMETER(dwSize);
    UNREFERENCED_PARAMETER(flNewProtect);
    UNREFERENCED_PARAMETER(lpflOldProtect);
    ASSERT(!"ClrVirtualProtect");
    return false;
}

MethodTable * g_pFreeObjectMethodTable;
int32_t g_TrapReturningThreads;
bool g_fFinalizerRunOnShutDown;

void DestroyThread(Thread * /*pThread*/)
{
    // TODO: Implement
}
void StompWriteBarrierEphemeral()
{
}

void StompWriteBarrierResize(bool /*bReqUpperBoundsCheck*/)
{
}

VOID LogSpewAlways(const char * /*fmt*/, ...)
{
}

CLR_MUTEX_COOKIE ClrCreateMutex(CLR_MUTEX_ATTRIBUTES lpMutexAttributes, bool bInitialOwner, LPCWSTR lpName)
{
    UNREFERENCED_PARAMETER(lpMutexAttributes);
    UNREFERENCED_PARAMETER(bInitialOwner);
    UNREFERENCED_PARAMETER(lpName);
    ASSERT(!"ClrCreateMutex");
    return NULL;
}

void ClrCloseMutex(CLR_MUTEX_COOKIE mutex)
{
    UNREFERENCED_PARAMETER(mutex);
    ASSERT(!"ClrCloseMutex");
}

bool ClrReleaseMutex(CLR_MUTEX_COOKIE mutex)
{
    UNREFERENCED_PARAMETER(mutex);
    ASSERT(!"ClrReleaseMutex");
    return true;
}

uint32_t ClrWaitForMutex(CLR_MUTEX_COOKIE mutex, uint32_t dwMilliseconds, bool bAlertable)
{
    UNREFERENCED_PARAMETER(mutex);
    UNREFERENCED_PARAMETER(dwMilliseconds);
    UNREFERENCED_PARAMETER(bAlertable);
    ASSERT(!"ClrWaitForMutex");
    return WAIT_OBJECT_0;
}


uint32_t CLRConfig::GetConfigValue(ConfigDWORDInfo eType)
{
    switch (eType)
    {
    case UNSUPPORTED_BGCSpinCount:
        return 140;

    case UNSUPPORTED_BGCSpin:
        return 2;

    case UNSUPPORTED_GCLogEnabled:
    case UNSUPPORTED_GCLogFile:
    case UNSUPPORTED_GCLogFileSize:
    case EXTERNAL_GCStressStart:
    case INTERNAL_GCStressStartAtJit:
    case INTERNAL_DbgDACSkipVerifyDlls:
        return 0;

    case Config_COUNT:
    default:
#ifdef _MSC_VER
#pragma warning(suppress:4127) // Constant conditional expression in ASSERT below
#endif
        ASSERT(!"Unknown config value type");
        return 0;
    }
}

HRESULT CLRConfig::GetConfigValue(ConfigStringInfo /*eType*/, wchar_t * * outVal)
{
    *outVal = NULL;
    return 0;
}