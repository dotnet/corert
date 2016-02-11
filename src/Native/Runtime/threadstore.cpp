// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"
#include "assert.h"
#include "slist.h"
#include "gcrhinterface.h"
#include "varint.h"
#include "regdisplay.h"
#include "StackFrameIterator.h"
#include "thread.h"
#include "holder.h"
#include "Crst.h"
#include "event.h"
#include "RWLock.h"
#include "threadstore.h"
#include "RuntimeInstance.h"
#include "ObjectLayout.h"
#include "TargetPtrs.h"
#include "eetype.h"

#include "slist.inl"
#include "GCMemoryHelpers.h"

EXTERN_C volatile UInt32 RhpTrapThreads = 0;

GVAL_IMPL_INIT(PTR_Thread, RhpSuspendingThread, 0);

ThreadStore * GetThreadStore()
{
    return GetRuntimeInstance()->GetThreadStore();
}

ThreadStore::Iterator::Iterator() :
    m_readHolder(&GetThreadStore()->m_Lock),
    m_pCurrentPosition(GetThreadStore()->m_ThreadList.GetHead())
{
}

ThreadStore::Iterator::~Iterator()
{
}

PTR_Thread ThreadStore::Iterator::GetNext()
{
    PTR_Thread pResult = m_pCurrentPosition;
    if (NULL != pResult)
        m_pCurrentPosition = pResult->m_pNext;
    return pResult;
}


#ifndef DACCESS_COMPILE


ThreadStore::ThreadStore() : 
    m_ThreadList(),
    m_Lock()
{
}

ThreadStore::~ThreadStore()
{
    // @TODO: For now, the approach will be to cleanup everything we can, even in the face of failure in 
    // individual operations within this method.  We're faced with a difficult situation -- what is the caller
    // supposed to do on failure?  Wait and try again?  Do nothing?  We will assume they do nothing and 
    // attempt to free as many of our resources as we can.  If any of those fail, we only leak those parts.
    // Whereas if we were to fail on the first operation and then return to the caller without doing anymore,
    // we would have leaked much more.

}

// static 
ThreadStore * ThreadStore::Create(RuntimeInstance * pRuntimeInstance)
{
    NewHolder<ThreadStore> pNewThreadStore = new (nothrow) ThreadStore();
    if (NULL == pNewThreadStore)
        return NULL;

    pNewThreadStore->m_SuspendCompleteEvent.CreateManualEvent(TRUE);

    pNewThreadStore->m_pRuntimeInstance = pRuntimeInstance;

    pNewThreadStore.SuppressRelease();
    return pNewThreadStore;
}

void ThreadStore::Destroy()
{
    delete this;
}

#endif //!DACCESS_COMPILE

//static
PTR_Thread ThreadStore::GetSuspendingThread()
{
    return (RhpSuspendingThread);
}
#ifndef DACCESS_COMPILE

GPTR_DECL(RuntimeInstance, g_pTheRuntimeInstance);

extern UInt32 _fls_index;


// static 
void ThreadStore::AttachCurrentThread(bool fAcquireThreadStoreLock)
{
    //
    // step 1: ThreadStore::InitCurrentThread
    // step 2: add this thread to the ThreadStore
    //

    // The thread has been constructed, during which some data is initialized (like which RuntimeInstance the
    // thread belongs to), but it hasn't been added to the thread store because doing so takes a lock, which 
    // we want to avoid at construction time because the loader lock is held then.
    Thread * pAttachingThread = RawGetCurrentThread();

    // On CHK build, validate that our GetThread assembly implementation matches the C++ implementation using
    // TLS.
    CreateCurrentThreadBuffer();

    // The thread was already initialized, so it is already attached
    if (pAttachingThread->IsInitialized())
    {
        return;
    }

    PalAttachThread(pAttachingThread);

    //
    // Init the thread buffer
    //
    pAttachingThread->Construct();
    ASSERT(pAttachingThread->m_ThreadStateFlags == Thread::TSF_Unknown);

    ThreadStore* pTS = GetThreadStore();
    ReaderWriterLock::WriteHolder write(&pTS->m_Lock, fAcquireThreadStoreLock);

    //
    // Set thread state to be attached
    //
    ASSERT(pAttachingThread->m_ThreadStateFlags == Thread::TSF_Unknown);
    pAttachingThread->m_ThreadStateFlags = Thread::TSF_Attached;

    pTS->m_ThreadList.PushHead(pAttachingThread);
}

// static 
void ThreadStore::AttachCurrentThread()
{
    AttachCurrentThread(true);
}

void ThreadStore::DetachCurrentThread()
{
    // The thread may not have been initialized because it may never have run managed code before.
    Thread * pDetachingThread = RawGetCurrentThread();

    // The thread was not initialized yet, so it was not attached
    if (!pDetachingThread->IsInitialized())
    {
        return;
    }

    if (!PalDetachThread(pDetachingThread))
    {
        return;
    }
        
#ifdef STRESS_LOG
    ThreadStressLog * ptsl = reinterpret_cast<ThreadStressLog *>(
        pDetachingThread->GetThreadStressLog());
    StressLog::ThreadDetach(ptsl);
#endif // STRESS_LOG

    ThreadStore* pTS = GetThreadStore();
    ReaderWriterLock::WriteHolder write(&pTS->m_Lock);
    ASSERT(rh::std::count(pTS->m_ThreadList.Begin(), pTS->m_ThreadList.End(), pDetachingThread) == 1);
    pTS->m_ThreadList.RemoveFirst(pDetachingThread);
    pDetachingThread->Destroy();
}

// Used by GC to prevent new threads during a GC.  New threads must take a write lock to 
// modify the list, but they won't be allowed to until all outstanding read locks are 
// released.  This way, the GC always enumerates a consistent set of threads each time 
// it enumerates threads between SuspendAllThreads and ResumeAllThreads.
//
// @TODO: Investigate if this requirement is actually necessary.  Threads already may 
// not enter managed code during GC, so if new threads are added to the thread store, 
// but haven't yet entered managed code, is that really a problem?
//
// @TODO: Investigate the suspend/resume algorithm's dependence on this lock's side-
// effect of being a memory barrier.
void ThreadStore::LockThreadStore()
{
    m_Lock.AcquireReadLock();
}
void ThreadStore::UnlockThreadStore()
{ 
    m_Lock.ReleaseReadLock();
}

void ThreadStore::SuspendAllThreads(CLREventStatic* pCompletionEvent)
{
    Thread * pThisThread = GetCurrentThreadIfAvailable();

    LockThreadStore();

    RhpSuspendingThread = pThisThread;

    pCompletionEvent->Reset();
    m_SuspendCompleteEvent.Reset();

    // set the global trap for pinvoke leave and return
    RhpTrapThreads = 1;

    // Our lock-free algorithm depends on flushing write buffers of all processors running RH code.  The
    // reason for this is that we essentially implement Decker's algorithm, which requires write ordering.
    PalFlushProcessWriteBuffers();

    bool keepWaiting;
    do
    {
        keepWaiting = false;
        FOREACH_THREAD(pTargetThread)
        {
            if (pTargetThread == pThisThread)
                continue;

            if (!pTargetThread->CacheTransitionFrameForSuspend())
            {
                // We drive all threads to preemptive mode by hijacking them with both a
                // return-address hijack and loop hijacks.
                keepWaiting = true;
                pTargetThread->Hijack();
            }
            else if (pTargetThread->DangerousCrossThreadIsHijacked())
            {
                // Once a thread is safely in preemptive mode, we must wait until it is also 
                // unhijacked.  This is done because, otherwise, we might race on into the 
                // stackwalk and find the hijack still on the stack, which will cause the 
                // stackwalking code to crash.
                keepWaiting = true;
            }
        }
        END_FOREACH_THREAD

        if (keepWaiting)
        {
            if (PalSwitchToThread() == 0 && g_SystemInfo.dwNumberOfProcessors > 1)
            {
                // No threads are scheduled on this processor.  Perhaps we're waiting for a thread
                // that's scheduled on another processor.  If so, let's give it a little time
                // to make forward progress.  
                // Note that we do not call Sleep, because the minimum granularity of Sleep is much
                // too long (we probably don't need a 15ms wait here).  Instead, we'll just burn some
                // cycles.
    	        // @TODO: need tuning for spin
                for (int i = 0; i < 10000; i++)
                    PalYieldProcessor();
            }
        }

    } while (keepWaiting);

    m_SuspendCompleteEvent.Set();
}

void ThreadStore::ResumeAllThreads(CLREventStatic* pCompletionEvent)
{
    m_pRuntimeInstance->UnsychronizedResetHijackedLoops();

    FOREACH_THREAD(pTargetThread)
    {
        pTargetThread->ResetCachedTransitionFrame();
    }
    END_FOREACH_THREAD

    RhpTrapThreads = 0;
    RhpSuspendingThread = NULL;
    pCompletionEvent->Set();
    UnlockThreadStore();
} // ResumeAllThreads

// static
bool ThreadStore::IsTrapThreadsRequested()
{
    return (RhpTrapThreads != 0);
}

void ThreadStore::WaitForSuspendComplete()
{
    UInt32 waitResult = m_SuspendCompleteEvent.Wait(INFINITE, false);
    if (waitResult == WAIT_FAILED)
        RhFailFast();
}

C_ASSERT(sizeof(Thread) == sizeof(ThreadBuffer));

EXTERN_C Thread * FASTCALL RhpGetThread();

DECLSPEC_THREAD ThreadBuffer tls_CurrentThread =
{ 
    { 0 },                              // m_rgbAllocContextBuffer
    Thread::TSF_Unknown,                // m_ThreadStateFlags
    TOP_OF_STACK_MARKER,                // m_pTransitionFrame
    TOP_OF_STACK_MARKER,                // m_pHackPInvokeTunnel
    0,                                  // m_pCachedTransitionFrame
    0,                                  // m_pNext
    INVALID_HANDLE_VALUE,               // m_hPalThread
    0,                                  // m_ppvHijackedReturnAddressLocation
    0,                                  // m_pvHijackedReturnAddress
    0,                                  // m_pStackLow
    0,                                  // m_pStackHigh
    0,                                  // m_uPalThreadId
};

// static
void * ThreadStore::CreateCurrentThreadBuffer()
{
    void * pvBuffer = &tls_CurrentThread;

#if !defined(CORERT) // @TODO: CORERT: No assembly routine defined to verify against.
    ASSERT(RhpGetThread() == pvBuffer);
#endif // !defined(CORERT)

    return pvBuffer;
}

// static
Thread * ThreadStore::RawGetCurrentThread()
{
    return (Thread *) &tls_CurrentThread;
}

// static
Thread * ThreadStore::GetCurrentThread()
{
    Thread * pCurThread = RawGetCurrentThread();

    // If this assert fires, and you only need the Thread pointer if the thread has ever previously
    // entered the runtime, then you should be using GetCurrentThreadIfAvailable instead.
    ASSERT(pCurThread->IsInitialized());    
    return pCurThread;
};

// static
Thread * ThreadStore::GetCurrentThreadIfAvailable()
{
    Thread * pCurThread = RawGetCurrentThread();
    if (pCurThread->IsInitialized())
        return pCurThread;

    return NULL;
}
#endif // !DACCESS_COMPILE

#ifdef _MSC_VER
// Keep a global variable in the target process which contains
// the address of _tls_index.  This is the breadcrumb needed
// by DAC to read _tls_index since we don't control the 
// declaration of _tls_index directly.
EXTERN_C UInt32 _tls_index;
GPTR_IMPL_INIT(UInt32, p_tls_index, &_tls_index);
#endif // _MSC_VER

#ifndef DACCESS_COMPILE

// We must prevent the linker from removing the unused global variable 
// that DAC will be looking at to find _tls_index.
#ifdef _MSC_VER
#ifdef _WIN64
#pragma comment(linker, "/INCLUDE:?p_tls_index@@3PEAIEA")
#else
#pragma comment(linker, "/INCLUDE:?p_tls_index@@3PAIA")
#endif
#endif // _MSC_VER

#else // DACCESS_COMPILE

#if defined(BIT64)
#define OFFSETOF__TLS__tls_CurrentThread            0x20
#elif defined(_ARM_)
#define OFFSETOF__TLS__tls_CurrentThread            0x10
#else
#define OFFSETOF__TLS__tls_CurrentThread            0x08
#endif

//
// This routine supports the !Thread debugger extension routine
//
typedef DPTR(TADDR) PTR_TADDR;
// static
PTR_Thread ThreadStore::GetThreadFromTEB(TADDR pTEB)
{
    if (pTEB == NULL)
        return NULL;

    UInt32 tlsIndex = *p_tls_index;
    TADDR pTls = *(PTR_TADDR)(pTEB + OFFSETOF__TEB__ThreadLocalStoragePointer);
    if (pTls == NULL)
        return NULL;

    TADDR pOurTls = *(PTR_TADDR)(pTls + (tlsIndex * sizeof(void*)));
    if (pOurTls == NULL)
        return NULL;

    return (PTR_Thread)(pOurTls + OFFSETOF__TLS__tls_CurrentThread);
}

#endif // DACCESS_COMPILE

#ifndef DACCESS_COMPILE

// internal static extern unsafe bool RhGetExceptionsForCurrentThread(Exception[] outputArray, out int writtenCountOut);
COOP_PINVOKE_HELPER(Boolean, RhGetExceptionsForCurrentThread, (Array* pOutputArray, Int32* pWrittenCountOut))
{
    return GetThreadStore()->GetExceptionsForCurrentThread(pOutputArray, pWrittenCountOut);
}

Boolean ThreadStore::GetExceptionsForCurrentThread(Array* pOutputArray, Int32* pWrittenCountOut)
{
    Int32 countWritten = 0;
    Object** pArrayElements;
    Thread * pThread = GetCurrentThread();
    
    for (PTR_ExInfo pInfo = pThread->m_pExInfoStackHead; pInfo != NULL; pInfo = pInfo->m_pPrevExInfo)
    {
        if (pInfo->m_exception == NULL)
            continue;

        countWritten++;
    }

    // No input array provided, or it was of the wrong kind.  We'll fill out the count and return false.
    if ((pOutputArray == NULL) || (pOutputArray->get_EEType()->get_ComponentSize() != POINTER_SIZE))
        goto Error;

    // Input array was not big enough.  We don't even partially fill it.
    if (pOutputArray->GetArrayLength() < (UInt32)countWritten)
        goto Error;

    *pWrittenCountOut = countWritten;

    // Success, but nothing to report.
    if (countWritten == 0)
        return Boolean_true;

    pArrayElements = (Object**)pOutputArray->GetArrayData();
    for (PTR_ExInfo pInfo = pThread->m_pExInfoStackHead; pInfo != NULL; pInfo = pInfo->m_pPrevExInfo)
    {
        if (pInfo->m_exception == NULL)
            continue;

        *pArrayElements = pInfo->m_exception;
        pArrayElements++;
    }

    RhpBulkWriteBarrier(pArrayElements, countWritten * POINTER_SIZE);
    return Boolean_true;

Error:
    *pWrittenCountOut = countWritten;
    return Boolean_false;
}
#endif // DACCESS_COMPILE
