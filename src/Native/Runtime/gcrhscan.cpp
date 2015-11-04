//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
#include "common.h"

#include "gcenv.h"
#include "gcscan.h"
#include "gc.h"
#include "objecthandle.h"

#include "RestrictedCallouts.h"


#include "PalRedhawkCommon.h"

#include "gcrhinterface.h"

#include "slist.h"
#include "varint.h"
#include "regdisplay.h"
#include "StackFrameIterator.h"

#include "thread.h"

#include "module.h"
#include "RWLock.h"
#include "RuntimeInstance.h"
#include "threadstore.h"


// todo: remove this hack (brain-dead logging).
#define PalPrintf __noop

//#define CATCH_GC  //catches exception during GC
#ifdef DACCESS_COMPILE
SVAL_IMPL_INIT(LONG, CNameSpace, m_GcStructuresInvalidCnt, 1);
#else //DACCESS_COMPILE
VOLATILE(LONG) CNameSpace::m_GcStructuresInvalidCnt = 1;
#endif //DACCESS_COMPILE

bool CNameSpace::GetGcRuntimeStructuresValid ()
{
    _ASSERTE ((LONG)m_GcStructuresInvalidCnt >= 0);
    return (LONG)m_GcStructuresInvalidCnt == 0;
}

#ifndef DACCESS_COMPILE

VOID CNameSpace::GcStartDoWork()
{
    PalPrintf("CNameSpace::GcStartDoWork\n");
}

/*
 * Scan for dead weak pointers
 */

VOID CNameSpace::GcWeakPtrScan( EnumGcRefCallbackFunc* fn, int condemned, int max_gen, EnumGcRefScanContext* sc )
{
    PalPrintf("CNameSpace::GcWeakPtrScan\n");
    Ref_CheckReachable(condemned, max_gen, (LPARAM)sc);
    Ref_ScanDependentHandlesForClearing(condemned, max_gen, sc, fn);
}

static void CALLBACK CheckPromoted(_UNCHECKED_OBJECTREF *pObjRef, LPARAM * /*pExtraInfo*/, LPARAM /*lp1*/, LPARAM /*lp2*/)
{
    LOG((LF_GC, LL_INFO100000, LOG_HANDLE_OBJECT_CLASS("Checking referent of Weak-", pObjRef, "to ", *pObjRef)));

    Object **pRef = (Object **)pObjRef;
    if (!GCHeap::GetGCHeap()->IsPromoted(*pRef))
    {
        LOG((LF_GC, LL_INFO100, LOG_HANDLE_OBJECT_CLASS("Severing Weak-", pObjRef, "to unreachable ", *pObjRef)));

        *pRef = NULL;
    }
    else
    {
        LOG((LF_GC, LL_INFO1000000, "reachable " LOG_OBJECT_CLASS(*pObjRef)));
    }
}

VOID CNameSpace::GcWeakPtrScanBySingleThread( int /*condemned*/, int /*max_gen*/, EnumGcRefScanContext* sc )
{
    PalPrintf("CNameSpace::GcWeakPtrScanBySingleThread\n");
#ifdef VERIFY_HEAP    
    SyncBlockCache::GetSyncBlockCache()->GCWeakPtrScan(&CheckPromoted, (LPARAM)sc, 0);
#endif // VERIFY_HEAP
}

VOID CNameSpace::GcShortWeakPtrScan(EnumGcRefCallbackFunc* /*fn*/,  int condemned, int max_gen, EnumGcRefScanContext* sc)
{
    PalPrintf("CNameSpace::GcShortWeakPtrScan\n");
    Ref_CheckAlive(condemned, max_gen, (LPARAM)sc);
}


void EnumAllStaticGCRefs(EnumGcRefCallbackFunc * fn, EnumGcRefScanContext * sc)
{
    GetRuntimeInstance()->EnumAllStaticGCRefs(fn, sc);
}


/*
 * Scan all stack roots in this 'namespace'
 */
 
VOID CNameSpace::GcScanRoots(EnumGcRefCallbackFunc * fn,  int condemned, int max_gen, EnumGcRefScanContext * sc)
{
    PalPrintf("CNameSpace::GcScanRoots\n");

    // STRESS_LOG1(LF_GCROOTS, LL_INFO10, "GCScan: Phase = %s\n", sc->promotion ? "promote" : "relocate");

    FOREACH_THREAD(pThread)
    {
        // Skip "GC Special" threads which are really background workers that will never have any roots.
        if (pThread->IsGCSpecial())
            continue;

#if !defined (ISOLATED_HEAPS)
        // @TODO: it is very bizarre that this IsThreadUsingAllocationContextHeap takes a copy of the
        // allocation context instead of a reference or a pointer to it. This seems very wasteful given how
        // large the alloc_context is.
        if (!GCHeap::GetGCHeap()->IsThreadUsingAllocationContextHeap(pThread->GetAllocContext(), 
                                                                     sc->thread_number))
        {
            // STRESS_LOG2(LF_GC|LF_GCROOTS, LL_INFO100, "{ Scan of Thread %p (ID = %x) declined by this heap\n", 
            //             pThread, pThread->GetThreadId());
        }
        else
#endif
        {
            STRESS_LOG1(LF_GC|LF_GCROOTS, LL_INFO100, "{ Starting scan of Thread %p\n", pThread);
            sc->thread_under_crawl = pThread;
#if defined(FEATURE_EVENT_TRACE) && !defined(DACCESS_COMPILE)
            sc->dwEtwRootKind = kEtwGCRootKindStack;
#endif
            pThread->GcScanRoots(fn, sc);

#if defined(FEATURE_EVENT_TRACE) && !defined(DACCESS_COMPILE)
            sc->dwEtwRootKind = kEtwGCRootKindOther;
#endif
            STRESS_LOG1(LF_GC|LF_GCROOTS, LL_INFO100, "Ending scan of Thread %p }\n", pThread);
        }
    }
    END_FOREACH_THREAD

    sc->thread_under_crawl = NULL;

    if ((!GCHeap::IsServerHeap() || sc->thread_number == 0) ||(condemned == max_gen && sc->promotion))
    {
#if defined(FEATURE_EVENT_TRACE) && !defined(DACCESS_COMPILE)
        sc->dwEtwRootKind = kEtwGCRootStatic;
#endif 
        EnumAllStaticGCRefs(fn, sc);
    }
}

/*
 * Scan all handle roots in this 'namespace'
 */


VOID CNameSpace::GcScanHandles (EnumGcRefCallbackFunc* fn,  int condemned, int max_gen, EnumGcRefScanContext* sc)
{
    PalPrintf("CNameSpace::GcScanHandles\n");

    STRESS_LOG1(LF_GC|LF_GCROOTS, LL_INFO10, "GcScanHandles (Promotion Phase = %d)\n", sc->promotion);
    if (sc->promotion)
    {
        Ref_TracePinningRoots(condemned, max_gen, sc, fn);
        Ref_TraceNormalRoots(condemned, max_gen, sc, fn);
    }
    else
    {
        Ref_UpdatePointers(condemned, max_gen, sc, fn);
        Ref_UpdatePinnedPointers(condemned, max_gen, sc, fn);
        Ref_ScanDependentHandlesForRelocation(condemned, max_gen, sc, fn);
    }
}
    
#if defined(GC_PROFILING) || defined(FEATURE_EVENT_TRACE)

/*
 * Scan all handle roots in this 'namespace' for profiling
 */

VOID CNameSpace::GcScanHandlesForProfilerAndETW (int max_gen, EnumGcRefScanContext* sc)
{
        LOG((LF_GC|LF_GCROOTS, LL_INFO10, "Profiler Root Scan Phase, Handles\n"));
        Ref_ScanPointersForProfilerAndETW(max_gen, (LPARAM)sc);
}
    
/*
 * Scan dependent handles in this 'namespace' for profiling
 */
void CNameSpace::GcScanDependentHandlesForProfilerAndETW (int max_gen, ProfilingScanContext* sc)
{
    LIMITED_METHOD_CONTRACT;

    LOG((LF_GC|LF_GCROOTS, LL_INFO10, "Profiler Root Scan Phase, DependentHandles\n"));
    Ref_ScanDependentHandlesForProfilerAndETW(max_gen, sc);
}

#endif // defined(GC_PROFILING) || defined(FEATURE_EVENT_TRACE)

void CNameSpace::GcRuntimeStructuresValid (BOOL bValid)
{
    if (!bValid)
    {
        LONG result;
        result = FastInterlockIncrement(&m_GcStructuresInvalidCnt);
        _ASSERTE (result > 0);
    }
    else
    {
        LONG result;
        result = FastInterlockDecrement(&m_GcStructuresInvalidCnt);
        _ASSERTE (result >= 0);
    }
}

void CNameSpace::GcDemote (int condemned, int max_gen, EnumGcRefScanContext* sc)
{
    PalPrintf("CNameSpace::GcDemote\n");
    Ref_RejuvenateHandles (condemned, max_gen, (LPARAM)sc);
#ifdef VERIFY_HEAP    
    if (!GCHeap::IsServerHeap() || sc->thread_number == 0)
        SyncBlockCache::GetSyncBlockCache()->GCDone(TRUE, max_gen);
#endif // VERIFY_HEAP    
}

void CNameSpace::GcPromotionsGranted (int condemned, int max_gen, EnumGcRefScanContext* sc)
{
    PalPrintf("CNameSpace::GcPromotionsGranted\n");
    Ref_AgeHandles(condemned, max_gen, (LPARAM)sc);
#ifdef VERIFY_HEAP    
    if (!GCHeap::IsServerHeap() || sc->thread_number == 0)
        SyncBlockCache::GetSyncBlockCache()->GCDone(FALSE, max_gen);
#endif // VERIFY_HEAP    
}


void CNameSpace::GcFixAllocContexts (void* arg, void *heap)
{
    PalPrintf("CNameSpace::GcFixAllocContexts\n");
    if (GCHeap::UseAllocationContexts())
    {
        FOREACH_THREAD(thread)
        {
            GCHeap::GetGCHeap()->FixAllocContext(thread->GetAllocContext(), FALSE, arg, heap);
        }
        END_FOREACH_THREAD
    }
}

void CNameSpace::GcEnumAllocContexts (enum_alloc_context_func* fn)
{
    PalPrintf("CNameSpace::GcEnumAllocContexts\n");

    if (GCHeap::UseAllocationContexts())
    {
        FOREACH_THREAD(thread)
        {
            (*fn) (thread->GetAllocContext());
        }
        END_FOREACH_THREAD
    }
}

size_t CNameSpace::AskForMoreReservedMemory (size_t old_size, size_t need_size)
{
    PalPrintf("CNameSpace::AskForMoreReservedMemory\n");

    return old_size + need_size;
}

void CNameSpace::VerifyHandleTable(int condemned, int max_gen, EnumGcRefScanContext *sc)
{
    PalPrintf("CNameSpace::VerifyHandleTable\n");

    Ref_VerifyHandleTable(condemned, max_gen, sc);
}

#endif //!DACCESS_COMPILE

void PromoteCarefully(PTR_PTR_Object obj, UInt32 flags, EnumGcRefCallbackFunc * fnGcEnumRef, EnumGcRefScanContext * pSc)
{
    //
    // Sanity check that the flags contain only these three values
    //
    assert((flags & ~(GC_CALL_INTERIOR|GC_CALL_PINNED|GC_CALL_CHECK_APP_DOMAIN)) == 0);

    //
    // Sanity check that GC_CALL_INTERIOR FLAG is set
    //
    assert(flags & GC_CALL_INTERIOR);

    // If the object reference points into the stack, we 
    // must not promote it, the GC cannot handle these.
    if (pSc->thread_under_crawl->IsWithinStackBounds(*obj))
        return;

    fnGcEnumRef(obj, pSc, flags);
}

void GcEnumObject(PTR_PTR_Object ppObj, UInt32 flags, EnumGcRefCallbackFunc * fnGcEnumRef, EnumGcRefScanContext * pSc)
{
    //
    // Sanity check that the flags contain only these three values
    //
    assert((flags & ~(GC_CALL_INTERIOR|GC_CALL_PINNED|GC_CALL_CHECK_APP_DOMAIN)) == 0);

    // for interior pointers, we optimize the case in which
    //  it points into the current threads stack area
    //
    if (flags & GC_CALL_INTERIOR)
        PromoteCarefully (ppObj, flags, fnGcEnumRef, pSc);
    else
        fnGcEnumRef(ppObj, pSc, flags);
}

void GcBulkEnumObjects(PTR_PTR_Object pObjs, UInt32 cObjs, EnumGcRefCallbackFunc * fnGcEnumRef, EnumGcRefScanContext * pSc)
{
    PTR_PTR_Object ppObj = pObjs;

    for (UInt32 i = 0; i < cObjs; i++)
        fnGcEnumRef(ppObj++, pSc, 0);
}

// Scan a contiguous range of memory and report everything that looks like it could be a GC reference as a
// pinned interior reference. Pinned in case we are wrong (so the GC won't try to move the object and thus
// corrupt the original memory value by relocating it). Interior since we (a) can't easily tell whether a
// real reference is interior or not and interior is the more conservative choice that will work for both and
// (b) because it might not be a real GC reference at all and in that case falsely listing the reference as
// non-interior will cause the GC to make assumptions and crash quite quickly.
void GcEnumObjectsConservatively(PTR_PTR_Object ppLowerBound, PTR_PTR_Object ppUpperBound, EnumGcRefCallbackFunc * fnGcEnumRef, EnumGcRefScanContext * pSc)
{
    // Only report potential references in the promotion phase. Since we report everything as pinned there
    // should be no work to do in the relocation phase.
    if (pSc->promotion)
    {
        for (PTR_PTR_Object ppObj = ppLowerBound; ppObj < ppUpperBound; ppObj++)
        {
            // Only report values that lie in the GC heap range. This doesn't conclusively guarantee that the
            // value is a GC heap reference but it's a cheap check that weeds out a lot of spurious values.
            PTR_Object pObj = *ppObj;
            if (((PTR_UInt8)pObj >= g_lowest_address) && ((PTR_UInt8)pObj <= g_highest_address))
                fnGcEnumRef(ppObj, pSc, GC_CALL_INTERIOR|GC_CALL_PINNED);
        }
    }
}

#ifndef DACCESS_COMPILE

//
// Dependent handle promotion scan support
//

// This method is called first during the mark phase. It's job is to set up the context for further scanning
// (remembering the scan parameters the GC gives us and initializing some state variables we use to determine
// whether further scans will be required or not).
//
// This scan is not guaranteed to return complete results due to the GC context in which we are called. In
// particular it is possible, due to either a mark stack overflow or unsynchronized operation in server GC
// mode, that not all reachable objects will be reported as promoted yet. However, the operations we perform
// will still be correct and this scan allows us to spot a common optimization where no dependent handles are
// due for retirement in this particular GC. This is an important optimization to take advantage of since
// synchronizing the GC to calculate complete results is a costly operation.
void CNameSpace::GcDhInitialScan(EnumGcRefCallbackFunc* fn, int condemned, int max_gen, EnumGcRefScanContext* sc)
{
    // We allocate space for dependent handle scanning context during Ref_Initialize. Under server GC there
    // are actually as many contexts as heaps (and CPUs). Ref_GetDependentHandleContext() retrieves the
    // correct context for the current GC thread based on the ScanContext passed to us by the GC.
    DhContext *pDhContext = Ref_GetDependentHandleContext(sc);

    // Record GC callback parameters in the DH context so that the GC doesn't continually have to pass the
    // same data to each call.
    pDhContext->m_pfnPromoteFunction = fn;
    pDhContext->m_iCondemned = condemned;
    pDhContext->m_iMaxGen = max_gen;
    pDhContext->m_pScanContext = sc;

    // Look for dependent handle whose primary has been promoted but whose secondary has not. Promote the
    // secondary in those cases. Additionally this scan sets the m_fUnpromotedPrimaries and m_fPromoted state
    // flags in the DH context. The m_fUnpromotedPrimaries flag is the most interesting here: if this flag is
    // false after the scan then it doesn't matter how many object promotions might currently be missing since
    // there are no secondary objects that are currently unpromoted anyway. This is the (hopefully common)
    // circumstance under which we don't have to perform any costly additional re-scans.
    Ref_ScanDependentHandlesForPromotion(pDhContext);
}

// This method is called after GcDhInitialScan and before each subsequent scan (GcDhReScan below). It
// determines whether any handles are left that have unpromoted secondaries.
bool CNameSpace::GcDhUnpromotedHandlesExist(EnumGcRefScanContext* sc)
{
    // Locate our dependent handle context based on the GC context.
    DhContext *pDhContext = Ref_GetDependentHandleContext(sc);

    return pDhContext->m_fUnpromotedPrimaries;
}

// Perform a re-scan of dependent handles, promoting secondaries associated with newly promoted primaries as
// above. We may still need to call this multiple times since promotion of a secondary late in the table could
// promote a primary earlier in the table. Also, GC graph promotions are not guaranteed to be complete by the
// time the promotion callback returns (the mark stack can overflow). As a result the GC might have to call
// this method in a loop. The scan records state that let's us know when to terminate (no further handles to
// be promoted or no promotions in the last scan). Returns true if at least one object was promoted as a
// result of the scan.
bool CNameSpace::GcDhReScan(EnumGcRefScanContext* sc)
{
    // Locate our dependent handle context based on the GC context.
    DhContext *pDhContext = Ref_GetDependentHandleContext(sc);

    return Ref_ScanDependentHandlesForPromotion(pDhContext);
}

//
// Sized refs support (not supported on Redhawk)
//

void CNameSpace::GcScanSizedRefs(EnumGcRefCallbackFunc* /*fn*/, int /*condemned*/, int /*max_gen*/, EnumGcRefScanContext* /*sc*/)
{
}

#endif // !DACCESS_COMPILE
