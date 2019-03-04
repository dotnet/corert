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
#include "gcheaputilities.h"
#include "profheapwalkhelper.h"

#ifdef FEATURE_STANDALONE_GC
#include "gcenv.ee.h"
#else
#include "../gc/env/gcenv.ee.h"
#endif // FEATURE_STANDALONE_GC

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

#ifdef FEATURE_ETW
    #ifndef _INC_WINDOWS
        typedef void* LPVOID;
        typedef uint32_t UINT;
        typedef void* PVOID;
        typedef uint64_t ULONGLONG;
        typedef uint32_t ULONG;
        typedef int64_t LONGLONG;
        typedef uint8_t BYTE;
        typedef uint16_t UINT16;
    #endif // _INC_WINDOWS

    #include "etwevents.h"
    #include "eventtrace.h"
#else // FEATURE_ETW
    #include "etmdummy.h"
    #define ETW_EVENT_ENABLED(e,f) false
#endif // FEATURE_ETW

GPTR_IMPL(EEType, g_pFreeObjectEEType);

#include "DebuggerHook.h"

#ifndef DACCESS_COMPILE

bool RhInitializeFinalization();
bool RhStartFinalizerThread();
void RhEnableFinalization();

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
        GCHeapUtilities::IsGCHeapInitialized())
    {
        FireEtwGCSettings(GCHeapUtilities::GetGCHeap()->GetValidSegmentSize(FALSE),
                          GCHeapUtilities::GetGCHeap()->GetValidSegmentSize(TRUE),
                          GCHeapUtilities::IsServerHeap());
        GCHeapUtilities::GetGCHeap()->DiagTraceGCSegments();
    }

    // Special check for the runtime provider's GCHeapCollectKeyword.  Profilers
    // flick this to force a full GC.
    if (IsEnabled && 
        (pContext->RegistrationHandle == Microsoft_Windows_Redhawk_GC_PublicHandle) &&
        GCHeapUtilities::IsGCHeapInitialized() &&
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
    InitializeHeapType(fUseServerGC);

    // Create the GC heap itself.
#ifdef FEATURE_STANDALONE_GC
    IGCToCLR* gcToClr = new (nothrow) GCToEEInterface();
    if (!gcToClr)
        return false;
#else
    IGCToCLR* gcToClr = nullptr;
#endif // FEATURE_STANDALONE_GC

    IGCHeap *pGCHeap = InitializeGarbageCollector(gcToClr);
    if (!pGCHeap)
        return false;

    g_pGCHeap = pGCHeap;

    // Initialize the GC subsystem.
    HRESULT hr = pGCHeap->Initialize();
    if (FAILED(hr))
        return false;

    if (!RhInitializeFinalization())
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

    ASSERT(!pThread->IsDoNotTriggerGcSet());

    size_t max_object_size;
#ifdef BIT64
    if (g_pConfig->GetGCAllowVeryLargeObjects())
    {
        max_object_size = (INT64_MAX - 7 - min_obj_size);
    }
    else
#endif // BIT64
    {
        max_object_size = (INT32_MAX - 7 - min_obj_size);
    }

    if (cbSize >= max_object_size)
        return NULL;

    const int MaxArrayLength = 0x7FEFFFFF;
    const int MaxByteArrayLength = 0x7FFFFFC7;

    // Impose limits on maximum array length in each dimension to allow efficient
    // implementation of advanced range check elimination in future. We have to allow
    // higher limit for array of bytes (or one byte structs) for backward compatibility.
    // Keep in sync with Array.MaxArrayLength in BCL.
    if (cbSize > MaxByteArrayLength /* note: comparing allocation size with element count */)
    {
        // Ensure the above if check covers the minimal interesting size
        static_assert(MaxByteArrayLength < (uint64_t)MaxArrayLength * 2, "");

        if (pEEType->IsArray())
        {
            if (pEEType->get_ComponentSize() != 1)
            {
                size_t elementCount = (cbSize - pEEType->get_BaseSize()) / pEEType->get_ComponentSize();
                if (elementCount > MaxArrayLength)
                    return NULL;
            }
            else
            {
                size_t elementCount = cbSize - pEEType->get_BaseSize();
                if (elementCount > MaxByteArrayLength)
                    return NULL;
            }
        }
    }

    // Save the EEType for instrumentation purposes.
    RedhawkGCInterface::SetLastAllocEEType(pEEType);

    Object * pObject;
#ifdef FEATURE_64BIT_ALIGNMENT
    if (uFlags & GC_ALLOC_ALIGN8)
        pObject = GCHeapUtilities::GetGCHeap()->AllocAlign8(pThread->GetAllocContext(), cbSize, uFlags);
    else
#endif // FEATURE_64BIT_ALIGNMENT
        pObject = GCHeapUtilities::GetGCHeap()->Alloc(pThread->GetAllocContext(), cbSize, uFlags);

    // NOTE: we cannot call PublishObject here because the object isn't initialized!

    return pObject;
}

// returns the object pointer for caller's convenience
COOP_PINVOKE_HELPER(void*, RhpPublishObject, (void* pObject, UIntNative cbSize))
{
    UNREFERENCED_PARAMETER(cbSize);
    ASSERT(cbSize >= LARGE_OBJECT_SIZE);
    GCHeapUtilities::GetGCHeap()->PublishObject((uint8_t*)pObject);
    return pObject;
}

// static
void RedhawkGCInterface::InitAllocContext(gc_alloc_context * pAllocContext)
{
    // NOTE: This method is currently unused because the thread's alloc_context is initialized via
    // static initialization of tls_CurrentThread.  If the initial contents of the alloc_context
    // ever change, then a matching change will need to be made to the tls_CurrentThread static
    // initializer.

    pAllocContext->init();
}

// static
void RedhawkGCInterface::ReleaseAllocContext(gc_alloc_context * pAllocContext)
{
    GCHeapUtilities::GetGCHeap()->FixAllocContext(pAllocContext, FALSE, NULL, NULL);
}

// static 
void RedhawkGCInterface::WaitForGCCompletion()
{
    GCHeapUtilities::GetGCHeap()->WaitUntilGCComplete();
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

    return (GcSegmentHandle)GCHeapUtilities::GetGCHeap()->RegisterFrozenSegment(&seginfo);
#else // FEATURE_BASICFREEZE
    return NULL;
#endif // FEATURE_BASICFREEZE    
}

// static 
void RedhawkGCInterface::UnregisterFrozenSection(GcSegmentHandle segment)
{
    GCHeapUtilities::GetGCHeap()->UnregisterFrozenSegment((segment_handle)segment);
}

EXTERN_C UInt32_BOOL g_fGcStressStarted = UInt32_FALSE; // UInt32_BOOL because asm code reads it
#ifdef FEATURE_GC_STRESS
// static 
void RedhawkGCInterface::StressGc()
{
    // The GarbageCollect operation below may trash the last win32 error. We save the error here so that it can be
    // restored after the GC operation;
    Int32 lastErrorOnEntry = PalGetLastError();

    if (g_fGcStressStarted && !GetThread()->IsSuppressGcStressSet() && !GetThread()->IsDoNotTriggerGcSet())
    {
        GCHeapUtilities::GetGCHeap()->GarbageCollect();
    }

    // Restore the saved error
    PalSetLastError(lastErrorOnEntry);
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

// Enumerate every reference field in an object, calling back to the specified function with the given context
// for each such reference found.
// static
void RedhawkGCInterface::ScanObject(void *pObject, GcScanObjectFunction pfnScanCallback, void *pContext)
{
#if !defined(DACCESS_COMPILE) && defined(FEATURE_EVENT_TRACE)
    GCHeapUtilities::GetGCHeap()->DiagWalkObject((Object*)pObject, (walk_fn)pfnScanCallback, pContext);
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
#if !defined(DACCESS_COMPILE) && defined(FEATURE_EVENT_TRACE)
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

COOP_PINVOKE_HELPER(Boolean, RhCompareObjectContentsAndPadding, (Object* pObj1, Object* pObj2))
{
    ASSERT(pObj1->get_EEType()->IsEquivalentTo(pObj2->get_EEType()));
    EEType * pEEType = pObj1->get_EEType();
    size_t cbFields = pEEType->get_BaseSize() - (sizeof(ObjHeader) + sizeof(EEType*));

    UInt8 * pbFields1 = (UInt8*)pObj1 + sizeof(EEType*);
    UInt8 * pbFields2 = (UInt8*)pObj2 + sizeof(EEType*);

    return (memcmp(pbFields1, pbFields2, cbFields) == 0) ? Boolean_true : Boolean_false;
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
        *(Boolean*)pData = Boolean_false;

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
        *(Boolean*)pData = Boolean_true;

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

void RedhawkGCInterface::DestroyTypedHandle(void * handle)
{
    ::DestroyTypedHandle((OBJECTHANDLE)handle);
}

void* RedhawkGCInterface::CreateTypedHandle(void* pObject, int type)
{
    return (void*)::CreateTypedHandle(g_HandleTableMap.pBuckets[0]->pTable[GetCurrentThreadHomeHeapNumber()], (Object*)pObject, type);
}

void GCToEEInterface::SuspendEE(SUSPEND_REASON reason)
{
#ifdef FEATURE_EVENT_TRACE
    ETW::GCLog::ETW_GC_INFO Info;
    Info.SuspendEE.Reason = reason;
    Info.SuspendEE.GcCount = (((reason == SUSPEND_FOR_GC) || (reason == SUSPEND_FOR_GC_PREP)) ?
        (UInt32)GCHeapUtilities::GetGCHeap()->GetGcCount() : (UInt32)-1);
#endif // FEATURE_EVENT_TRACE

    FireEtwGCSuspendEEBegin_V1(Info.SuspendEE.Reason, Info.SuspendEE.GcCount, GetClrInstanceId());

    g_SuspendEELock.Enter();

    g_TrapReturningThreads = TRUE;
    GCHeapUtilities::GetGCHeap()->SetGCInProgress(TRUE);

    GetThreadStore()->SuspendAllThreads(GCHeapUtilities::GetGCHeap()->GetWaitForGCEvent());

    FireEtwGCSuspendEEEnd_V1(GetClrInstanceId());

#ifdef APP_LOCAL_RUNTIME
    // now is a good opportunity to retry starting the finalizer thread
    RhStartFinalizerThread();
#endif
}

void GCToEEInterface::RestartEE(bool /*bFinishedGC*/)
{
    FireEtwGCRestartEEBegin_V1(GetClrInstanceId());

    SyncClean::CleanUp();

    GetThreadStore()->ResumeAllThreads(GCHeapUtilities::GetGCHeap()->GetWaitForGCEvent());
    GCHeapUtilities::GetGCHeap()->SetGCInProgress(FALSE);

    g_TrapReturningThreads = FALSE;

    g_SuspendEELock.Leave();

    FireEtwGCRestartEEEnd_V1(GetClrInstanceId());
}

void GCToEEInterface::GcStartWork(int condemned, int /*max_gen*/)
{
    DebuggerHook::OnBeforeGcCollection();
    
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

gc_alloc_context * GCToEEInterface::GetAllocContext(Thread * pThread)
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
    ASSERT(GCHeapUtilities::IsGCInProgress());
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

#ifdef FEATURE_EVENT_TRACE
void ProfScanRootsHelper(Object** ppObject, ScanContext* pSC, uint32_t dwFlags)
{
    Object* pObj = *ppObject;
    if (dwFlags& GC_CALL_INTERIOR)
    {
        pObj = GCHeapUtilities::GetGCHeap()->GetContainingObject(pObj, true);
        if (pObj == nullptr)
            return;
    }
    ScanRootsHelper(pObj, ppObject, pSC, dwFlags);
}

void GcScanRootsForETW(promote_func* fn, int condemned, int max_gen, ScanContext* sc)
{
    UNREFERENCED_PARAMETER(condemned);
    UNREFERENCED_PARAMETER(max_gen);

    FOREACH_THREAD(pThread)
    {
        if (pThread->IsGCSpecial())
            continue;

        if (GCHeapUtilities::GetGCHeap()->IsThreadUsingAllocationContextHeap(pThread->GetAllocContext(), sc->thread_number))
            continue;

        sc->thread_under_crawl = pThread;
        sc->dwEtwRootKind = kEtwGCRootKindStack;
        pThread->GcScanRoots(reinterpret_cast<void*>(fn), sc);
        sc->dwEtwRootKind = kEtwGCRootKindOther;
    }
    END_FOREACH_THREAD
}

void ScanHandleForETW(Object** pRef, Object* pSec, uint32_t flags, ScanContext* context, BOOL isDependent)
{
    ProfilingScanContext* pSC = (ProfilingScanContext*)context;

    // Notify ETW of the handle
    if (ETW::GCLog::ShouldWalkHeapRootsForEtw())
    {
        ETW::GCLog::RootReference(
            pRef,
            *pRef,          // object being rooted
            pSec,           // pSecondaryNodeForDependentHandle
            isDependent,
            pSC,
            0,              // dwGCFlags,
            flags);     // ETW handle flags
    }
}

// This is called only if we've determined that either:
//     a) The Profiling API wants to do a walk of the heap, and it has pinned the
//     profiler in place (so it cannot be detached), and it's thus safe to call into the
//     profiler, OR
//     b) ETW infrastructure wants to do a walk of the heap either to log roots,
//     objects, or both.
// This can also be called to do a single walk for BOTH a) and b) simultaneously.  Since
// ETW can ask for roots, but not objects
void GCProfileWalkHeapWorker(BOOL fShouldWalkHeapRootsForEtw, BOOL fShouldWalkHeapObjectsForEtw)
{
    ProfilingScanContext SC(FALSE);

    // **** Scan roots:  Only scan roots if profiling API wants them or ETW wants them.
    if (fShouldWalkHeapRootsForEtw)
    {
        GcScanRootsForETW(&ProfScanRootsHelper, max_generation, max_generation, &SC);
        SC.dwEtwRootKind = kEtwGCRootKindFinalizer;
        GCHeapUtilities::GetGCHeap()->DiagScanFinalizeQueue(&ProfScanRootsHelper, &SC);

        // Handles are kept independent of wks/svr/concurrent builds
        SC.dwEtwRootKind = kEtwGCRootKindHandle;
        GCHeapUtilities::GetGCHeap()->DiagScanHandles(&ScanHandleForETW, max_generation, &SC);
    }

    // **** Scan dependent handles: only if ETW wants roots
    if (fShouldWalkHeapRootsForEtw)
    {
        // GcScanDependentHandlesForProfiler double-checks
        // CORProfilerTrackConditionalWeakTableElements() before calling into the profiler

        ProfilingScanContext* pSC = &SC;

        // we'll re-use pHeapId (which was either unused (0) or freed by EndRootReferences2
        // (-1)), so reset it to NULL
        _ASSERTE((*((size_t *)(&pSC->pHeapId)) == (size_t)(-1)) ||
                (*((size_t *)(&pSC->pHeapId)) == (size_t)(0)));
        pSC->pHeapId = NULL;

        GCHeapUtilities::GetGCHeap()->DiagScanDependentHandles(&ScanHandleForETW, max_generation, &SC);
    }

    ProfilerWalkHeapContext profilerWalkHeapContext(FALSE, SC.pvEtwContext);

    // **** Walk objects on heap: only if ETW wants them.
    if (fShouldWalkHeapObjectsForEtw)
    {
        GCHeapUtilities::GetGCHeap()->DiagWalkHeap(&HeapWalkHelper, &profilerWalkHeapContext, max_generation, true /* walk the large object heap */);
    }

    #ifdef FEATURE_EVENT_TRACE
    // **** Done! Indicate to ETW helpers that the heap walk is done, so any buffers
    // should be flushed into the ETW stream
    if (fShouldWalkHeapObjectsForEtw || fShouldWalkHeapRootsForEtw)
    {
        ETW::GCLog::EndHeapDump(&profilerWalkHeapContext);
    }
#endif // FEATURE_EVENT_TRACE
}
#endif // defined(FEATURE_EVENT_TRACE)

void GCProfileWalkHeap()
{

#ifdef FEATURE_EVENT_TRACE
    if (ETW::GCLog::ShouldWalkStaticsAndCOMForEtw())
        ETW::GCLog::WalkStaticsAndCOMForETW();

    BOOL fShouldWalkHeapRootsForEtw = ETW::GCLog::ShouldWalkHeapRootsForEtw();
    BOOL fShouldWalkHeapObjectsForEtw = ETW::GCLog::ShouldWalkHeapObjectsForEtw();
#else // !FEATURE_EVENT_TRACE
    BOOL fShouldWalkHeapRootsForEtw = FALSE;
    BOOL fShouldWalkHeapObjectsForEtw = FALSE;
#endif // FEATURE_EVENT_TRACE

#ifdef FEATURE_EVENT_TRACE
    // we need to walk the heap if one of GC_PROFILING or FEATURE_EVENT_TRACE
    // is defined, since both of them make use of the walk heap worker.
    if (fShouldWalkHeapRootsForEtw || fShouldWalkHeapObjectsForEtw)
    {
        GCProfileWalkHeapWorker(fShouldWalkHeapRootsForEtw, fShouldWalkHeapObjectsForEtw);
    }
#endif // defined(FEATURE_EVENT_TRACE)
}


void GCToEEInterface::DiagGCStart(int gen, bool isInduced)
{
    UNREFERENCED_PARAMETER(gen);
    UNREFERENCED_PARAMETER(isInduced);
}

void GCToEEInterface::DiagUpdateGenerationBounds()
{
}

void GCToEEInterface::DiagWalkFReachableObjects(void* gcContext)
{
    UNREFERENCED_PARAMETER(gcContext);
}

void GCToEEInterface::DiagGCEnd(size_t index, int gen, int reason, bool fConcurrent)
{
    UNREFERENCED_PARAMETER(index);
    UNREFERENCED_PARAMETER(gen);
    UNREFERENCED_PARAMETER(reason);

    if (!fConcurrent)
    {
        GCProfileWalkHeap();
    }
}

// Note on last parameter: when calling this for bgc, only ETW
// should be sending these events so that existing profapi profilers
// don't get confused.
void WalkMovedReferences(uint8_t* begin, uint8_t* end, 
                         ptrdiff_t reloc,
                         size_t context, 
                         BOOL fCompacting,
                         BOOL fBGC)
{
    UNREFERENCED_PARAMETER(begin);
    UNREFERENCED_PARAMETER(end);
    UNREFERENCED_PARAMETER(reloc);
    UNREFERENCED_PARAMETER(context);
    UNREFERENCED_PARAMETER(fCompacting);
    UNREFERENCED_PARAMETER(fBGC);
}

//
// Diagnostics code
//

#ifdef FEATURE_EVENT_TRACE
inline BOOL ShouldTrackMovementForProfilerOrEtw()
{
    if (ETW::GCLog::ShouldTrackMovementForEtw())
        return true;

    return false;
}
#endif // FEATURE_EVENT_TRACE

void GCToEEInterface::DiagWalkSurvivors(void* gcContext)
{
#ifdef FEATURE_EVENT_TRACE
    if (ShouldTrackMovementForProfilerOrEtw())
    {
        size_t context = 0;
        ETW::GCLog::BeginMovedReferences(&context);
        GCHeapUtilities::GetGCHeap()->DiagWalkSurvivorsWithType(gcContext, &WalkMovedReferences, context, walk_for_gc);
        ETW::GCLog::EndMovedReferences(context);
    }
#else
    UNREFERENCED_PARAMETER(gcContext);
#endif // FEATURE_EVENT_TRACE
}

void GCToEEInterface::DiagWalkLOHSurvivors(void* gcContext)
{
#ifdef FEATURE_EVENT_TRACE
    if (ShouldTrackMovementForProfilerOrEtw())
    {
        size_t context = 0;
        ETW::GCLog::BeginMovedReferences(&context);
        GCHeapUtilities::GetGCHeap()->DiagWalkSurvivorsWithType(gcContext, &WalkMovedReferences, context, walk_for_loh);
        ETW::GCLog::EndMovedReferences(context);
    }
#else
    UNREFERENCED_PARAMETER(gcContext);
#endif // FEATURE_EVENT_TRACE
}

void GCToEEInterface::DiagWalkBGCSurvivors(void* gcContext)
{
#ifdef FEATURE_EVENT_TRACE
    if (ShouldTrackMovementForProfilerOrEtw())
    {
        size_t context = 0;
        ETW::GCLog::BeginMovedReferences(&context);
        GCHeapUtilities::GetGCHeap()->DiagWalkSurvivorsWithType(gcContext, &WalkMovedReferences, context, walk_for_bgc);
        ETW::GCLog::EndMovedReferences(context);
    }
#else
    UNREFERENCED_PARAMETER(gcContext);
#endif // FEATURE_EVENT_TRACE
}

void GCToEEInterface::StompWriteBarrier(WriteBarrierParameters* args)
{
    // CoreRT doesn't patch the write barrier like CoreCLR does, but it
    // still needs to record the changes in the GC heap.
    assert(args != nullptr);
    switch (args->operation)
    {
    case WriteBarrierOp::StompResize:
        // StompResize requires a new card table, a new lowest address, and
        // a new highest address
        assert(args->card_table != nullptr);
        assert(args->lowest_address != nullptr);
        assert(args->highest_address != nullptr);
        g_card_table = args->card_table;

        // We need to make sure that other threads executing checked write barriers
        // will see the g_card_table update before g_lowest/highest_address updates.
        // Otherwise, the checked write barrier may AV accessing the old card table
        // with address that it does not cover. Write barriers access card table
        // without memory barriers for performance reasons, so we need to flush
        // the store buffers here.
        FlushProcessWriteBuffers();

        g_lowest_address = args->lowest_address;
        VolatileStore(&g_highest_address, args->highest_address);
        return;
    case WriteBarrierOp::StompEphemeral:
        // StompEphemeral requires a new ephemeral low and a new ephemeral high
        assert(args->ephemeral_low != nullptr);
        assert(args->ephemeral_high != nullptr);
        g_ephemeral_low = args->ephemeral_low;
        g_ephemeral_high = args->ephemeral_high;
        return;
    case WriteBarrierOp::Initialize:
        // This operation should only be invoked once, upon initialization.
        assert(g_card_table == nullptr);
        assert(g_lowest_address == nullptr);
        assert(g_highest_address == nullptr);
        assert(args->card_table != nullptr);
        assert(args->lowest_address != nullptr);
        assert(args->highest_address != nullptr);
        assert(args->ephemeral_low != nullptr);
        assert(args->ephemeral_high != nullptr);
        assert(args->is_runtime_suspended && "the runtime must be suspended here!");

        g_card_table = args->card_table;
        g_lowest_address = args->lowest_address;
        g_highest_address = args->highest_address;
        g_ephemeral_low = args->ephemeral_low;
        g_ephemeral_high = args->ephemeral_high;
        return;
    case WriteBarrierOp::SwitchToWriteWatch:
    case WriteBarrierOp::SwitchToNonWriteWatch:
        assert(!"CoreRT does not have an implementation of non-OS WriteWatch");
        return;
    default:
        assert(!"Unknokwn WriteBarrierOp enum");
        return;
    }
}

void GCToEEInterface::EnableFinalization(bool foundFinalizers)
{
    if (foundFinalizers)
        RhEnableFinalization();
}

#endif // !DACCESS_COMPILE

// NOTE: this method is not in thread.cpp because it needs access to the layout of alloc_context for DAC to know the 
// size, but thread.cpp doesn't generally need to include the GC environment headers for any other reason.
gc_alloc_context * Thread::GetAllocContext()
{
    return dac_cast<DPTR(gc_alloc_context)>(dac_cast<TADDR>(this) + offsetof(Thread, m_rgbAllocContextBuffer));
}

#ifndef DACCESS_COMPILE
bool IsGCSpecialThread()
{
    // TODO: Implement for background GC
    return ThreadStore::GetCurrentThread()->IsGCSpecial();
}
#endif // DACCESS_COMPILE

GPTR_IMPL(Thread, g_pFinalizerThread);
GPTR_IMPL(Thread, g_pGcThread);

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

void SetGCSpecialThread(ThreadType threadType)
{
    Thread *pThread = ThreadStore::RawGetCurrentThread();
    pThread->SetGCSpecial(threadType == ThreadType_GC);
}

#endif // DACCESS_COMPILE

MethodTable * g_pFreeObjectMethodTable;
int32_t g_TrapReturningThreads;

#ifndef DACCESS_COMPILE
bool IsGCThread()
{
    return IsGCSpecialThread() || ThreadStore::GetSuspendingThread() == ThreadStore::GetCurrentThread();
}
#endif // DACCESS_COMPILE

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

#if defined(FEATURE_EVENT_TRACE) && !defined(DACCESS_COMPILE)
ProfilingScanContext::ProfilingScanContext(BOOL fProfilerPinnedParam)
    : ScanContext()
{
    pHeapId = NULL;
    fProfilerPinned = fProfilerPinnedParam;
    pvEtwContext = NULL;
#ifdef FEATURE_CONSERVATIVE_GC
    // To not confuse GCScan::GcScanRoots
    promotion = g_pConfig->GetGCConservative();
#endif
}
#endif // defined(FEATURE_EVENT_TRACE) && !defined(DACCESS_COMPILE)
