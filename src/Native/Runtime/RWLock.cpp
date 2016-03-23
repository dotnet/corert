// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// RWLock.cpp -- adapted from CLR SimpleRWLock.cpp
//
#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"
#include "rhassert.h"
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

// Configurable constants used across our spin locks
// Initialization here is necessary so that we have meaningful values before the runtime is started
// These initial values were selected to match the defaults, but anything reasonable is close enough
struct SpinConstants
{
    UInt32 uInitialDuration;
    UInt32 uMaximumDuration;
    UInt32 uBackoffFactor;
    UInt32 uRepetitions;
} g_SpinConstants = { 
    50,        // dwInitialDuration 
    40000,     // dwMaximumDuration - ideally (20000 * max(2, numProc))
    3,         // dwBackoffFactor
    10         // dwRepetitions
};

ReaderWriterLock::ReadHolder::ReadHolder(ReaderWriterLock * pLock, bool fAcquireLock) :
    m_pLock(pLock)
{
#ifndef DACCESS_COMPILE
    m_fLockAcquired = fAcquireLock;
    if (fAcquireLock)
        m_pLock->AcquireReadLock();
#else
    UNREFERENCED_PARAMETER(fAcquireLock);
#endif // !DACCESS_COMPILE
}

ReaderWriterLock::ReadHolder::~ReadHolder()
{
#ifndef DACCESS_COMPILE
    if (m_fLockAcquired)
        m_pLock->ReleaseReadLock();
#endif // !DACCESS_COMPILE
}

ReaderWriterLock::WriteHolder::WriteHolder(ReaderWriterLock * pLock, bool fAcquireLock) :
    m_pLock(pLock)
{
#ifndef DACCESS_COMPILE
    m_fLockAcquired = fAcquireLock;
    if (fAcquireLock)
        m_pLock->AcquireWriteLock();
#else
    UNREFERENCED_PARAMETER(fAcquireLock);
#endif // !DACCESS_COMPILE
}

ReaderWriterLock::WriteHolder::~WriteHolder()
{
#ifndef DACCESS_COMPILE
    if (m_fLockAcquired)
        m_pLock->ReleaseWriteLock();
#endif // !DACCESS_COMPILE
}

ReaderWriterLock::ReaderWriterLock() : 
    m_RWLock(0)
#if 0
    , m_WriterWaiting(false)
#endif
{
    m_spinCount = (
#ifndef DACCESS_COMPILE
        (PalGetProcessCpuCount() == 1) ? 0 : 
#endif
        4000);
}


#ifndef DACCESS_COMPILE

// Attempt to take the read lock, but do not wait if a writer has the lock.
// Release the lock if successfully acquired.  Returns true if the lock was
// taken and released.  Returns false if a writer had the lock.  
//
// BEWARE: Because this method returns after releasing the lock, you can't 
// infer the state of the lock based on the return value.  This is currently
// only used to detect if a suspended thread owns the write lock to prevent
// deadlock with the Hijack logic during GC suspension.
//
bool ReaderWriterLock::DangerousTryPulseReadLock()
{
    if (TryAcquireReadLock())
    {
        ReleaseReadLock();
        return true;
    }
    return false;
}

bool ReaderWriterLock::TryAcquireReadLock()
{
    Int32 RWLock;

    do 
    {
        RWLock = m_RWLock;
        if (RWLock == -1)
            return false;
        ASSERT(RWLock >= 0);
    }
    while (RWLock != PalInterlockedCompareExchange(&m_RWLock, RWLock+1, RWLock));

    return true;
}

void ReaderWriterLock::AcquireReadLock()
{
    if (TryAcquireReadLock())
        return;

    AcquireReadLockWorker();
}

void ReaderWriterLock::AcquireReadLockWorker()
{
    UInt32 uSwitchCount = 0;

    for (;;)
    {
#if 0
        // @TODO: Validate that we never re-enter the reader lock from a thread that
        // already holds it.  This scenario will deadlock if there are outstanding
        // writers.

        // prevent writers from being starved. This assumes that writers are rare and 
        // dont hold the lock for a long time. 
        while (m_WriterWaiting)
        {
            Int32 spinCount = m_spinCount;
            while (spinCount > 0) {
                spinCount--;
                PalYieldProcessor();
            }
            __SwitchToThread(0, ++uSwitchCount);
        }
#endif

        if (TryAcquireReadLock())
            return;

        UInt32 uDelay = g_SpinConstants.uInitialDuration;
        do
        {
            if (TryAcquireReadLock())
                return;

            if (g_SystemInfo.dwNumberOfProcessors <= 1)
                break;

            // Delay by approximately 2*i clock cycles (Pentium III).
            // This is brittle code - future processors may of course execute this
            // faster or slower, and future code generators may eliminate the loop altogether.
            // The precise value of the delay is not critical, however, and I can't think
            // of a better way that isn't machine-dependent - petersol.
            int sum = 0;
            for (int delayCount = uDelay; --delayCount; ) 
            {
                sum += delayCount;
                PalYieldProcessor();           // indicate to the processor that we are spining 
            }
            if (sum == 0)
            {
                // never executed, just to fool the compiler into thinking sum is live here,
                // so that it won't optimize away the loop.
                static char dummy;
                dummy++;
            }
            // exponential backoff: wait a factor longer in the next iteration
            uDelay *= g_SpinConstants.uBackoffFactor;
        }
        while (uDelay < g_SpinConstants.uMaximumDuration);

        __SwitchToThread(0, ++uSwitchCount);
    }
}

void ReaderWriterLock::ReleaseReadLock()
{
    Int32 RWLock;
    RWLock = PalInterlockedDecrement(&m_RWLock);
    ASSERT(RWLock >= 0);
}


bool ReaderWriterLock::TryAcquireWriteLock()
{
    Int32 RWLock = PalInterlockedCompareExchange(&m_RWLock, -1, 0);

    ASSERT(RWLock >= 0 || RWLock == -1);
    
    if (RWLock)
        return false;

#if 0
    m_WriterWaiting = false;
#endif

    return true;
}

void ReaderWriterLock::AcquireWriteLock()
{
    UInt32 uSwitchCount = 0;

    for (;;)
    {
        if (TryAcquireWriteLock())
            return;

#if 0
        // Set the writer waiting word, if not already set, to notify potential readers to wait.
        m_WriterWaiting = true;
#endif

        UInt32 uDelay = g_SpinConstants.uInitialDuration;
        do
        {
            if (TryAcquireWriteLock())
                return;

            if (g_SystemInfo.dwNumberOfProcessors <= 1)
            {
                break;
            }
            // Delay by approximately 2*i clock cycles (Pentium III).
            // This is brittle code - future processors may of course execute this
            // faster or slower, and future code generators may eliminate the loop altogether.
            // The precise value of the delay is not critical, however, and I can't think
            // of a better way that isn't machine-dependent - petersol.
            int sum = 0;
            for (int delayCount = uDelay; --delayCount; ) 
            {
                sum += delayCount;
                PalYieldProcessor();           // indicate to the processor that we are spining 
            }
            if (sum == 0)
            {
                // never executed, just to fool the compiler into thinking sum is live here,
                // so that it won't optimize away the loop.
                static char dummy;
                dummy++;
            }
            // exponential backoff: wait a factor longer in the next iteration
            uDelay *= g_SpinConstants.uBackoffFactor;
        }
        while (uDelay < g_SpinConstants.uMaximumDuration);

        __SwitchToThread(0, ++uSwitchCount);
    }
}

void ReaderWriterLock::ReleaseWriteLock()
{
    Int32 RWLock;
    RWLock = PalInterlockedExchange(&m_RWLock, 0);
    ASSERT(RWLock == -1);
}
#endif // DACCESS_COMPILE
