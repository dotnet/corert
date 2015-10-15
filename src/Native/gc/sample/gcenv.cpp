//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information. 
//

#include "common.h"

#include "windows.h"

#include "gcenv.h"
#include "gc.h"

EEConfig * g_pConfig;

void CLREventStatic::CreateManualEvent(bool bInitialState)
{
    m_hEvent = CreateEventW(NULL, TRUE, bInitialState, NULL);
    m_fInitialized = true;
}

void CLREventStatic::CreateAutoEvent(bool bInitialState)
{
    m_hEvent = CreateEventW(NULL, FALSE, bInitialState, NULL);
    m_fInitialized = true;
}

void CLREventStatic::CreateOSManualEvent(bool bInitialState)
{
    m_hEvent = CreateEventW(NULL, TRUE, bInitialState, NULL);
    m_fInitialized = true;
}

void CLREventStatic::CreateOSAutoEvent(bool bInitialState)
{
    m_hEvent = CreateEventW(NULL, FALSE, bInitialState, NULL);
    m_fInitialized = true;
}

void CLREventStatic::CloseEvent()
{
    if (m_fInitialized && m_hEvent != INVALID_HANDLE_VALUE)
    {
        CloseHandle(m_hEvent);
        m_hEvent = INVALID_HANDLE_VALUE;
    }
}

bool CLREventStatic::IsValid() const
{
    return m_fInitialized && m_hEvent != INVALID_HANDLE_VALUE;
}

bool CLREventStatic::Set()
{
    if (!m_fInitialized)
        return false;
    return !!SetEvent(m_hEvent);
}

bool CLREventStatic::Reset()
{
    if (!m_fInitialized)
        return false;
    return !!ResetEvent(m_hEvent);
}

uint32_t CLREventStatic::Wait(uint32_t dwMilliseconds, bool bAlertable)
{
    DWORD result = WAIT_FAILED;

    if (m_fInitialized)
    {
        bool        disablePreemptive = false;
        Thread *    pCurThread = GetThread();

        if (NULL != pCurThread)
        {
            if (GCToEEInterface::IsPreemptiveGCDisabled(pCurThread))
            {
                GCToEEInterface::EnablePreemptiveGC(pCurThread);
                disablePreemptive = true;
            }
        }

        result = WaitForSingleObjectEx(m_hEvent, dwMilliseconds, bAlertable);

        if (disablePreemptive)
        {
            GCToEEInterface::DisablePreemptiveGC(pCurThread);
        }
    }

    return result;
}

__declspec(thread) Thread * pCurrentThread;

Thread * GetThread()
{
    return pCurrentThread;
}

Thread * g_pThreadList = NULL;

Thread * ThreadStore::GetThreadList(Thread * pThread)
{
    if (pThread == NULL)
        return g_pThreadList;

    return pThread->m_pNext;
}

void ThreadStore::AttachCurrentThread(bool fAcquireThreadStoreLock)
{
    // TODO: Locks

    Thread * pThread = new Thread();
    pThread->GetAllocContext()->init();
    pCurrentThread = pThread;

    pThread->m_pNext = g_pThreadList;
    g_pThreadList = pThread;
}

void GCToEEInterface::SuspendEE(GCToEEInterface::SUSPEND_REASON reason)
{
    GCHeap::GetGCHeap()->SetGCInProgress(TRUE);

    // TODO: Implement
}

void GCToEEInterface::RestartEE(bool bFinishedGC)
{
    // TODO: Implement

    GCHeap::GetGCHeap()->SetGCInProgress(FALSE);
}

void GCToEEInterface::ScanStackRoots(Thread * pThread, promote_func* fn, ScanContext* sc)
{
    // TODO: Implement - Scan stack roots on given thread
}

void GCToEEInterface::ScanStaticGCRefsOpportunistically(promote_func* fn, ScanContext* sc)
{
}

void GCToEEInterface::GcStartWork(int condemned, int max_gen)
{
}

void GCToEEInterface::AfterGcScanRoots(int condemned, int max_gen, ScanContext* sc)
{
}

void GCToEEInterface::GcBeforeBGCSweepWork()
{
}

void GCToEEInterface::GcDone(int condemned)
{
}

bool GCToEEInterface::RefCountedHandleCallbacks(Object * pObject)
{
    return false;
}

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

// does not acquire thread store lock
void GCToEEInterface::AttachCurrentThread()
{
    ThreadStore::AttachCurrentThread(false);
}

Thread * GCToEEInterface::GetThreadList(Thread * pThread)
{
    return ThreadStore::GetThreadList(pThread);
}

void FinalizerThread::EnableFinalization()
{
    // Signal to finalizer thread that there are objects to finalize
    // TODO: Implement for finalization
}

bool FinalizerThread::HaveExtraWorkForFinalizer()
{
    return false;
}

bool PalStartBackgroundGCThread(BackgroundCallback callback, void* pCallbackContext)
{
    // TODO: Implement for background GC
    return false;
}

bool IsGCSpecialThread()
{
    // TODO: Implement for background GC
    return false;
}
