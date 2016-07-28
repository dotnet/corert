// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

#include "shash.h"
#include "RWLock.h"
#include "module.h"
#include "RuntimeInstance.h"
#include "objecthandle.h"
#include "eetype.inl"
#include "RhConfig.h"

#include "threadstore.h"
#include "threadstore.inl"
#include "thread.inl"

#include "gcdesc.h"
#include "SyncClean.hpp"

#include "daccess.h"

#include "GCMemoryHelpers.h"

#include "holder.h"

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

#if defined(ENABLE_PERF_COUNTERS) || defined(FEATURE_EVENT_TRACE)
DWORD g_dwHandles = 0;
#endif // ENABLE_PERF_COUNTERS || FEATURE_EVENT_TRACE

#ifdef FEATURE_ETW
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
CrstStatic g_SuspendEELock;
#ifdef _MSC_VER
#pragma warning(disable:4815) // zero-sized array in stack object will have no elements
#endif // _MSC_VER
EEType g_FreeObjectEEType;

// static 
bool RedhawkGCInterface::InitializeSubsystems(GCType gcType)
{
    g_pConfig->Construct();

#ifdef FEATURE_ETW
    MICROSOFT_WINDOWS_REDHAWK_GC_PRIVATE_PROVIDER_Context.IsEnabled = FALSE;
    MICROSOFT_WINDOWS_REDHAWK_GC_PUBLIC_PROVIDER_Context.IsEnabled = FALSE;

    // Register the Redhawk event provider with the system.
    RH_ETW_REGISTER_Microsoft_Windows_Redhawk_GC_Private();
    RH_ETW_REGISTER_Microsoft_Windows_Redhawk_GC_Public();

    MICROSOFT_WINDOWS_REDHAWK_GC_PRIVATE_PROVIDER_Context.RegistrationHandle = Microsoft_Windows_Redhawk_GC_PrivateHandle;
    MICROSOFT_WINDOWS_REDHAWK_GC_PUBLIC_PROVIDER_Context.RegistrationHandle = Microsoft_Windows_Redhawk_GC_PublicHandle;
#endif // FEATURE_ETW

    if (!InitializeSystemInfo())
    {
        return false;
    }

    // Initialize the special EEType used to mark free list entries in the GC heap.
    g_FreeObjectEEType.InitializeAsGcFreeType();

    // Place the pointer to this type in a global cell (typed as the structurally equivalent MethodTable
    // that the GC understands).
    g_pFreeObjectMethodTable = (MethodTable *)&g_FreeObjectEEType;
    g_pFreeObjectEEType = &g_FreeObjectEEType;

    if (!g_SuspendEELock.InitNoThrow(CrstSuspendEE))
        return false;

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
//  pEEType         -  type of the object
//  uFlags          -  GC type flags (see gc.h GC_ALLOC_*)
//  cbSize          -  size in bytes of the final object
//  pTransitionFrame-  transition frame to make stack crawable
// Returns a pointer to the object allocated or NULL on failure.

COOP_PINVOKE_HELPER(void*, RhpGcAlloc, (EEType *pEEType, UInt32 uFlags, UIntNative cbSize, void * pTransitionFrame))
{
    Thread * pThread = ThreadStore::GetCurrentThread();

    pThread->SetCurrentThreadPInvokeTunnelForGcAlloc(pTransitionFrame);

    ASSERT(GCHeap::UseAllocationContexts());
    ASSERT(!pThread->IsDoNotTriggerGcSet());

    // Save the EEType for instrumentation purposes.
    RedhawkGCInterface::SetLastAllocEEType(pEEType);

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
    GCHeap::GetGCHeap()->PublishObject((uint8_t*)pObject);
    return pObject;
}

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

bool IsOnReadablePortionOfThread(EnumGcRefScanContext * pSc, PTR_VOID pointer)
{
    if (!pSc->thread_under_crawl->IsWithinStackBounds(pointer))
    {
        return false;
    }
    
    // If the stack_limit is 0, then it wasn't set properly, and the check below will not
    // operate correctly.
    ASSERT(pSc->stack_limit != 0);

    // This ensures that the pointer is not in a currently-unused portion of the stack
    // because the above check is only verifying against the entire stack bounds,
    // but stack_limit is describing the current bound of the stack
    if (PTR_TO_TADDR(pointer) < pSc->stack_limit)
    {
        return false;
    }
    return true;
}

#ifdef BIT64
#define CONSERVATIVE_REGION_MAGIC_NUMBER 0x87DF7A104F09E0A9ULL
#else
#define CONSERVATIVE_REGION_MAGIC_NUMBER 0x4F09E0A9
#endif

// This is a structure that is created by executing runtime code in order to report a conservative 
// region. In managed code if there is a pinned byref pointer to one of this (with the appropriate
// magic number set in it, and a hash that matches up) then the region from regionPointerLow to 
// regionPointerHigh will be reported conservatively. This can only be used to report memory regions
// on the current stack and the structure must itself be located on the stack.
struct ConservativelyReportedRegionDesc
{
    // If this is really a ConservativelyReportedRegionDesc then the magic value will be
    // CONSERVATIVE_REGION_MAGIC_NUMBER, and the hash will be the result of CalculateHash
    // across magic, regionPointerLow, and regionPointerHigh
    uintptr_t magic;
    PTR_VOID regionPointerLow;
    PTR_VOID regionPointerHigh;
    uintptr_t hash;
    
    static uintptr_t CalculateHash(uintptr_t h1, uintptr_t h2, uintptr_t h3)
    {
        uintptr_t hash = h1;
        hash = ((hash << 13) ^ hash) ^ h2;
        hash = ((hash << 13) ^ hash) ^ h3;
        return hash;
    }
};

typedef DPTR(ConservativelyReportedRegionDesc) PTR_ConservativelyReportedRegionDesc;

bool IsPtrAligned(TADDR value)
{
    return (value & (POINTER_SIZE - 1)) == 0;
}

// Logic to actually conservatively report a ConservativelyReportedRegionDesc
// This logic is to be used when attempting to promote a pinned, interior pointer.
// It will attempt to heuristically identify ConservativelyReportedRegionDesc structures
// and if they exist, it will conservatively report a memory region.
static void ReportExplicitConservativeReportedRegionIfValid(EnumGcRefContext * pCtx, PTR_PTR_VOID pObject)
{
    // If the stack_limit isn't set (which can only happen for frames which make a p/invoke call
    // there cannot be a ConservativelyReportedRegionDesc
    if (pCtx->sc->stack_limit == 0)
        return;

    PTR_ConservativelyReportedRegionDesc conservativeRegionDesc = (PTR_ConservativelyReportedRegionDesc)(*pObject);

    // Ensure that conservativeRegionDesc pointer points at a readable memory region 
    if (!IsPtrAligned(PTR_TO_TADDR(conservativeRegionDesc)))
    {
        return;
    }

    if (!IsOnReadablePortionOfThread(pCtx->sc, conservativeRegionDesc))
    {
        return;
    }
    if (!IsOnReadablePortionOfThread(pCtx->sc, conservativeRegionDesc + 1))
    {
        return;
    }

    // Now, check to see if what we're pointing at is actually a ConservativeRegionDesc
    // First: check the magic number. If that doesn't match, it cannot be one
    if (conservativeRegionDesc->magic != CONSERVATIVE_REGION_MAGIC_NUMBER)
    {
        return;
    }

    // Second: check to see that the region pointers point at memory which is aligned
    // such that the pointers could be pointers to object references
    if (!IsPtrAligned(PTR_TO_TADDR(conservativeRegionDesc->regionPointerLow)))
    {
        return;
    }
    if (!IsPtrAligned(PTR_TO_TADDR(conservativeRegionDesc->regionPointerHigh)))
    {
        return;
    }

    // Third: check that start is before end.
    if (conservativeRegionDesc->regionPointerLow >= conservativeRegionDesc->regionPointerHigh)
    {
        return;
    }

#ifndef DACCESS_COMPILE
    // This fails for cross-bitness dac compiles and isn't really needed in the DAC anyways.

    // Fourth: Compute a hash of the above numbers. Check to see that the hash matches the hash
    // value stored
    if (ConservativelyReportedRegionDesc::CalculateHash(CONSERVATIVE_REGION_MAGIC_NUMBER, 
                                                        (uintptr_t)PTR_TO_TADDR(conservativeRegionDesc->regionPointerLow),
                                                        (uintptr_t)PTR_TO_TADDR(conservativeRegionDesc->regionPointerHigh)) 
        != conservativeRegionDesc->hash)
    {
        return;
    }
#endif // DACCESS_COMPILE

    // Fifth: Check to see that the region pointed at is within the bounds of the thread
    if (!IsOnReadablePortionOfThread(pCtx->sc, conservativeRegionDesc->regionPointerLow))
    {
        return;
    }
    if (!IsOnReadablePortionOfThread(pCtx->sc, ((PTR_OBJECTREF)conservativeRegionDesc->regionPointerHigh) - 1))
    {
        return;
    }

    // At this point we're most likely working with a ConservativeRegionDesc. We'll assume
    // that's true, and perform conservative reporting. (We've done enough checks to ensure that
    // this conservative reporting won't itself cause an AV, even if our heuristics are wrong
    // with the second and fifth set of checks)
    GcEnumObjectsConservatively((PTR_OBJECTREF)conservativeRegionDesc->regionPointerLow, (PTR_OBJECTREF)conservativeRegionDesc->regionPointerHigh, pCtx->f, pCtx->sc);
}

static void EnumGcRefsCallback(void * hCallback, PTR_PTR_VOID pObject, UInt32 flags)
{
    EnumGcRefContext * pCtx = (EnumGcRefContext *)hCallback;

    GcEnumObject((PTR_OBJECTREF)pObject, flags, pCtx->f, pCtx->sc);
    
    const UInt32 interiorPinned = GC_CALL_INTERIOR | GC_CALL_PINNED;
    // If this is an interior pinned pointer, check to see if we're working with a ConservativeRegionDesc
    // and if so, report a conservative region. NOTE: do this only during promotion as conservative
    // reporting has no value during other GC phases.
    if (((flags & interiorPinned) == interiorPinned) && (pCtx->sc->promotion))
    {
        ReportExplicitConservativeReportedRegionIfValid(pCtx, pObject);
    }
}

// static 
void RedhawkGCInterface::EnumGcRefs(ICodeManager * pCodeManager,
                                    MethodInfo * pMethodInfo, 
                                    PTR_VOID safePointAddress,
                                    REGDISPLAY * pRegisterSet,
                                    void * pfnEnumCallback,
                                    void * pvCallbackData)
{
    EnumGcRefContext ctx;
    ctx.pCallback = EnumGcRefsCallback;
    ctx.f  = (EnumGcRefCallbackFunc *)pfnEnumCallback;
    ctx.sc = (EnumGcRefScanContext *)pvCallbackData;
    ctx.sc->stack_limit = pRegisterSet->GetSP();

    pCodeManager->EnumGcRefs(pMethodInfo, 
                             safePointAddress,
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

    GCHeap::GetGCHeap()->GarbageCollect();
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
    while (Interlocked::CompareExchangePointer(&g_pfnHeapScan, pfnScanCallback, NULL) != NULL)
    {
        // Wait in pre-emptive mode to avoid stalling another thread that's attempting a collection.
        Thread * pCurThread = GetThread();
        ASSERT(pCurThread->IsCurrentThreadInCooperativeMode());
        pCurThread->EnablePreemptiveMode();

        // Give the other thread some time to get the collection going.
        if (PalSwitchToThread() == 0)
            PalSleep(1);

        // Wait for the collection to complete (if the other thread didn't manage to schedule it yet we'll
        // just end up going round the loop again).
        WaitForGCCompletion();

        // Come back into co-operative mode.
        pCurThread->DisablePreemptiveMode();
    }

    // We should never end up overwriting someone else's callback context when we won the race to set the
    // callback function pointer.
    ASSERT(g_pvHeapScanContext == NULL);
    g_pvHeapScanContext = pContext;

    // Initiate a full garbage collection
    GCHeap::GetGCHeap()->GarbageCollect();
    WaitForGCCompletion();

    // Release our hold on the global scanning pointers.
    g_pvHeapScanContext = NULL;
    Interlocked::ExchangePointer(&g_pfnHeapScan, NULL);
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

    pThread->GcScanRoots(reinterpret_cast<void*>(ScanRootsCallbackWrapper), &sContext);
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

    GetRuntimeInstance()->EnumAllStaticGCRefs(reinterpret_cast<void*>(ScanRootsCallbackWrapper), &sContext);
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
    ASSERT(GetThread() == NULL || GetThread()->IsCurrentThreadInCooperativeMode());
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
    size_t cbDest = pobjDest->GetSize() - sizeof(ObjHeader);
    size_t cbSrc = pobjSrc->GetSize() - sizeof(ObjHeader);
    if (cbSrc != cbDest)
        return;

    ASSERT(pobjDest->get_EEType()->HasReferenceFields() == pobjSrc->get_EEType()->HasReferenceFields());

    if (pobjDest->get_EEType()->HasReferenceFields())
    {
        GCSafeCopyMemoryWithWriteBarrier(pobjDest, pobjSrc, cbDest);
    }
    else
    {
        memcpy(pobjDest, pobjSrc, cbDest);
    }
}

COOP_PINVOKE_HELPER(void, RhpBox, (Object * pObj, void * pData))
{
    EEType * pEEType = pObj->get_EEType();

    // Can box value types only (which also implies no finalizers).
    ASSERT(pEEType->get_IsValueType() && !pEEType->HasFinalizer());

    // cbObject includes ObjHeader (sync block index) and the EEType* field from Object and is rounded up to
    // suit GC allocation alignment requirements. cbFields on the other hand is just the raw size of the field
    // data.
    size_t cbFieldPadding = pEEType->get_ValueTypeFieldPadding();
    size_t cbObject = pEEType->get_BaseSize();
    size_t cbFields = cbObject - (sizeof(ObjHeader) + sizeof(EEType*) + cbFieldPadding);
    
    UInt8 * pbFields = (UInt8*)pObj + sizeof(EEType*);

    // Copy the unboxed value type data into the new object.
    // Perform any write barriers necessary for embedded reference fields.
    if (pEEType->HasReferenceFields())
    {
        GCSafeCopyMemoryWithWriteBarrier(pbFields, pData, cbFields);
    }
    else
    {
        memcpy(pbFields, pData, cbFields);
    }
}

bool EETypesEquivalentEnoughForUnboxing(EEType *pObjectEEType, EEType *pUnboxToEEType)
{
    if (pObjectEEType->IsEquivalentTo(pUnboxToEEType))
        return true;

    if (pObjectEEType->GetCorElementType() == pUnboxToEEType->GetCorElementType())
    {
        // Enums and primitive types can unbox if their CorElementTypes exactly match
        switch (pObjectEEType->GetCorElementType())
        {
        case ELEMENT_TYPE_I1:
        case ELEMENT_TYPE_U1:
        case ELEMENT_TYPE_I2:
        case ELEMENT_TYPE_U2:
        case ELEMENT_TYPE_I4:
        case ELEMENT_TYPE_U4:
        case ELEMENT_TYPE_I8:
        case ELEMENT_TYPE_U8:
        case ELEMENT_TYPE_I:
        case ELEMENT_TYPE_U:
            return true;
        default:
            break;
        }
    }

    return false;
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
        size_t cbFieldPadding = pEEType->get_ValueTypeFieldPadding();
        size_t cbFields = pEEType->get_BaseSize() - (sizeof(ObjHeader) + sizeof(EEType*) + cbFieldPadding);
        GCSafeZeroMemory((UInt8*)pData + pUnboxToEEType->GetNullableValueOffset(), cbFields);

        return;
    }

    EEType * pEEType = pObj->get_EEType();

    // Can unbox value types only.
    ASSERT(pEEType->get_IsValueType());

    // A special case is that we can unbox a value type T into a Nullable<T>. It's the only case where
    // pUnboxToEEType is useful.
    ASSERT((pUnboxToEEType == NULL) || EETypesEquivalentEnoughForUnboxing(pEEType, pUnboxToEEType) || pUnboxToEEType->IsNullable());
    if (pUnboxToEEType && pUnboxToEEType->IsNullable())
    {
        ASSERT(pUnboxToEEType->GetNullableType()->IsEquivalentTo(pEEType));

        // Set the first field of the Nullable to true to indicate the value is present.
        *(Boolean*)pData = TRUE;

        // Adjust the data pointer so that it points at the value field in the Nullable.
        pData = (UInt8*)pData + pUnboxToEEType->GetNullableValueOffset();
    }

    size_t cbFieldPadding = pEEType->get_ValueTypeFieldPadding();
    size_t cbFields = pEEType->get_BaseSize() - (sizeof(ObjHeader) + sizeof(EEType*) + cbFieldPadding);
    UInt8 * pbFields = (UInt8*)pObj + sizeof(EEType*);

    if (pEEType->HasReferenceFields())
    {
        // Copy the boxed fields into the new location in a GC safe manner
        GCSafeCopyMemoryWithWriteBarrier(pData, pbFields, cbFields);
    }
    else
    {
        // Copy the boxed fields into the new location.
        memcpy(pData, pbFields, cbFields);
    }
}

Thread * GetThread()
{
    return ThreadStore::GetCurrentThread();
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

    g_SuspendEELock.Enter();

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

    g_TrapReturningThreads = FALSE;

    g_SuspendEELock.Leave();

    FireEtwGCRestartEEEnd_V1(GetClrInstanceId());
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
    return pThread->IsCurrentThreadInCooperativeMode();
}

void GCToEEInterface::EnablePreemptiveGC(Thread * pThread)
{
#ifndef DACCESS_COMPILE
    pThread->EnablePreemptiveMode();
#else
    UNREFERENCED_PARAMETER(pThread);
#endif
}

void GCToEEInterface::DisablePreemptiveGC(Thread * pThread)
{
#ifndef DACCESS_COMPILE
    pThread->DisablePreemptiveMode();
#else
    UNREFERENCED_PARAMETER(pThread);
#endif
}

#ifndef DACCESS_COMPILE

// Context passed to the above.
struct GCBackgroundThreadContext
{
    GCBackgroundThreadFunction  m_pRealStartRoutine;
    void *                      m_pRealContext;
    Thread *                    m_pThread;
    CLREventStatic              m_ThreadStartedEvent;
};

// Helper used to wrap the start routine of background GC threads so we can do things like initialize the
// Redhawk thread state which requires running in the new thread's context.
static uint32_t WINAPI BackgroundGCThreadStub(void * pContext)
{
    GCBackgroundThreadContext * pStartContext = (GCBackgroundThreadContext*)pContext;

    // Initialize the Thread for this thread. The false being passed indicates that the thread store lock
    // should not be acquired as part of this operation. This is necessary because this thread is created in
    // the context of a garbage collection and the lock is already held by the GC.
    ASSERT(GCHeap::GetGCHeap()->IsGCInProgress());
    ThreadStore::AttachCurrentThread(false);

    Thread * pThread = GetThread();
    pThread->SetGCSpecial(true);

    // Inform the GC which Thread* we are.
    pStartContext->m_pThread = pThread;

    GCBackgroundThreadFunction realStartRoutine = pStartContext->m_pRealStartRoutine;
    void* realContext = pStartContext->m_pRealContext;

    pStartContext->m_ThreadStartedEvent.Set();

    STRESS_LOG_RESERVE_MEM (GC_STRESSLOG_MULTIPLY);

    // Run the real start procedure and capture its return code on exit.
    return realStartRoutine(realContext);
}

Thread* GCToEEInterface::CreateBackgroundThread(GCBackgroundThreadFunction threadStart, void* arg)
{
    GCBackgroundThreadContext threadStubArgs;

    threadStubArgs.m_pThread = NULL;
    threadStubArgs.m_pRealStartRoutine = threadStart;
    threadStubArgs.m_pRealContext = arg;

    if (!threadStubArgs.m_ThreadStartedEvent.CreateAutoEventNoThrow(false))
    {
        return NULL;
    }

    if (!PalStartBackgroundGCThread(BackgroundGCThreadStub, &threadStubArgs))
    {
        threadStubArgs.m_ThreadStartedEvent.CloseEvent();
        return NULL;
    }

    uint32_t res = threadStubArgs.m_ThreadStartedEvent.Wait(INFINITE, FALSE);
    threadStubArgs.m_ThreadStartedEvent.CloseEvent();
    ASSERT(res == WAIT_OBJECT_0);

    ASSERT(threadStubArgs.m_pThread != NULL);
    return threadStubArgs.m_pThread;
}

#endif // !DACCESS_COMPILE

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

CLREventStatic g_FinalizerEvent;
CLREventStatic g_FinalizerDoneEvent;

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

    if (Interlocked::Exchange(&fFinalizerThreadCreated, 1) != 1)
    {
        if (!PalStartFinalizerThread(FinalizerStart, (void*)FinalizerThread::GetFinalizerEvent()))
        {
            // Need to try again another time...
            Interlocked::Exchange(&fFinalizerThreadCreated, 0);
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
    // Allocate the events the GC expects the finalizer thread to have. The g_FinalizerEvent event is signalled
    // by the GC whenever it completes a collection where it found otherwise unreachable finalizable objects.
    // The g_FinalizerDoneEvent is set by the finalizer thread every time it wakes up and drains the
    // queue of finalizable objects. It's mainly used by GC.WaitForPendingFinalizers().
    if (!g_FinalizerEvent.CreateAutoEventNoThrow(false))
        return false;
    if (!g_FinalizerDoneEvent.CreateManualEventNoThrow(false))
        return false;

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
    g_FinalizerEvent.Set();
}

void FinalizerThread::SignalFinalizationDone(bool /*fFinalizer*/)
{
    g_FinalizerDoneEvent.Set();
}

bool FinalizerThread::HaveExtraWorkForFinalizer()
{
    return false; // Nothing to do
}

bool FinalizerThread::IsCurrentThreadFinalizer()
{
    return GetThread() == g_pFinalizerThread;
}

HANDLE FinalizerThread::GetFinalizerEvent()
{
    return g_FinalizerEvent.GetOSEvent();
}

void FinalizerThread::Wait(DWORD timeout, bool allowReentrantWait)
{
    // Can't call this from the finalizer thread itself.
    if (!IsCurrentThreadFinalizer())
    {
        // Clear any current indication that a finalization pass is finished and wake the finalizer thread up
        // (if there's no work to do it'll set the done event immediately).
        g_FinalizerDoneEvent.Reset();
        EnableFinalization();

#ifdef APP_LOCAL_RUNTIME
        // We may have failed to create the finalizer thread at startup.  
        // Try again now.
        StartFinalizerThread();
#endif

        // Wait for the finalizer thread to get back to us.
        g_FinalizerDoneEvent.Wait(timeout, false, allowReentrantWait);
    }
}
#endif // !DACCESS_COMPILE
#endif // FEATURE_PREMORTEM_FINALIZATION

#ifndef DACCESS_COMPILE

bool __SwitchToThread(uint32_t dwSleepMSec, uint32_t /*dwSwitchCount*/)
{
    if (dwSleepMSec > 0)
    {
        PalSleep(dwSleepMSec);
        return true;
    }
    return !!PalSwitchToThread();
}

#endif // DACCESS_COMPILE

MethodTable * g_pFreeObjectMethodTable;
int32_t g_TrapReturningThreads;
bool g_fFinalizerRunOnShutDown;

void StompWriteBarrierEphemeral(bool /* isRuntimeSuspended */)
{
}

void StompWriteBarrierResize(bool /* isRuntimeSuspended */, bool /*bReqUpperBoundsCheck*/)
{
}

bool IsGCThread()
{
    return false;
}

void LogSpewAlways(const char * /*fmt*/, ...)
{
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

HRESULT CLRConfig::GetConfigValue(ConfigStringInfo /*eType*/, __out_z TCHAR * * outVal)
{
    *outVal = NULL;
    return 0;
}

bool NumaNodeInfo::CanEnableGCNumaAware() 
{ 
    // @TODO: enable NUMA node support
    return false; 
}

void NumaNodeInfo::GetGroupForProcessor(uint16_t /*processor_number*/, uint16_t * /*group_number*/, uint16_t * /*group_processor_number*/)
{
    ASSERT_UNCONDITIONALLY("NYI: NumaNodeInfo::GetGroupForProcessor");
}

bool NumaNodeInfo::GetNumaProcessorNodeEx(PPROCESSOR_NUMBER /*proc_no*/, uint16_t * /*node_no*/)
{
    ASSERT_UNCONDITIONALLY("NYI: NumaNodeInfo::GetNumaProcessorNodeEx");
    return false;
}

bool CPUGroupInfo::CanEnableGCCPUGroups()
{
    // @TODO: enable CPU group support
    return false;
}

uint32_t CPUGroupInfo::GetNumActiveProcessors() 
{ 
    // @TODO: enable CPU group support
    // NOTE: this API shouldn't be called unless CanEnableGCCPUGroups() returns true
    ASSERT_UNCONDITIONALLY("NYI: CPUGroupInfo::GetNumActiveProcessors");
    return 0;
}

void CPUGroupInfo::GetGroupForProcessor(uint16_t /*processor_number*/, uint16_t * /*group_number*/, uint16_t * /*group_processor_number*/)
{
    ASSERT_UNCONDITIONALLY("NYI: CPUGroupInfo::GetGroupForProcessor");
}
