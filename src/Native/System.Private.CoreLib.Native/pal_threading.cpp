// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pal_common.h"
#include "pal_threading.h"
#include "pal_time.h"

#include <sched.h>

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// LowLevelMutex

extern "C" void CoreLibNative_LowLevelMutex_Acquire(LowLevelMutex *mutex)
{
    assert(mutex != nullptr);
    mutex->Acquire();
}

extern "C" void CoreLibNative_LowLevelMutex_Release(LowLevelMutex *mutex)
{
    assert(mutex != nullptr);
    mutex->Release();
}

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// LowLevelMonitor

#if !(HAVE_MACH_ABSOLUTE_TIME || HAVE_PTHREAD_CONDATTR_SETCLOCK && HAVE_CLOCK_MONOTONIC)
#error Don't know how to perfom timed wait on this platform
#endif

LowLevelMonitor::LowLevelMonitor()
{
#if HAVE_MACH_ABSOLUTE_TIME
    // Older versions of OSX don't support CLOCK_MONOTONIC, so we don't use pthread_condattr_setclock. See
    // Wait(int32_t timeoutMilliseconds).
    int initError = pthread_cond_init(&m_condition, nullptr);
#else
    int error;

    pthread_condattr_t conditionAttributes;
    error = pthread_condattr_init(&conditionAttributes);
    if (error != 0)
    {
        throw OutOfMemoryException();
    }

    error = pthread_condattr_setclock(&conditionAttributes, CLOCK_MONOTONIC);
    assert(error == 0);

    int initError = pthread_cond_init(&m_condition, &conditionAttributes);

    error = pthread_condattr_destroy(&conditionAttributes);
    assert(error == 0);
#endif

    if (initError != 0)
    {
        throw OutOfMemoryException();
    }
}

// Returns false upon timeout or unexpected error, and true when the thread is woken up (could be a spurious wakeup, depending
// on implementation)
bool LowLevelMonitor::Wait(int32_t timeoutMilliseconds)
{
    assert(timeoutMilliseconds >= -1);

    if (timeoutMilliseconds < 0)
    {
        Wait();
        return true;
    }

    SetIsLocked(false);

    int error;

    // Calculate the time at which a timeout should occur, and wait. Older versions of OSX don't support clock_gettime with
    // CLOCK_MONOTONIC, so we instead compute the relative timeout duration, and use a relative variant of the timed wait.
    timespec timeoutTimeSpec;
#if HAVE_MACH_ABSOLUTE_TIME
    MillisecondsToTimeSpec(timeoutMilliseconds, &timeoutTimeSpec);
    error = pthread_cond_timedwait_relative_np(&m_condition, &m_mutex, &timeoutTimeSpec);
#else
    error = clock_gettime(CLOCK_MONOTONIC, &timeoutTimeSpec);
    assert(error == 0);
    AddMillisecondsToTimeSpec(timeoutMilliseconds, &timeoutTimeSpec);
    error = pthread_cond_timedwait(&m_condition, &m_mutex, &timeoutTimeSpec);
#endif
    assert(error == 0 || error == ETIMEDOUT);

    SetIsLocked(true);
    return error == 0;
}

extern "C" LowLevelMonitor *CoreLibNative_LowLevelMonitor_New()
{
    try
    {
        return new(nothrow) LowLevelMonitor();
    }
    catch (OutOfMemoryException)
    {
        return nullptr;
    }
}

extern "C" void CoreLibNative_LowLevelMonitor_Delete(LowLevelMonitor *monitor)
{
    assert(monitor != nullptr);
    delete monitor;
}

extern "C" void CoreLibNative_LowLevelMonitor_Wait(LowLevelMonitor *monitor)
{
    assert(monitor != nullptr);
    monitor->Wait();
}

extern "C" int CoreLibNative_LowLevelMonitor_TimedWait(LowLevelMonitor *monitor, int32_t timeoutMilliseconds)
{
    assert(monitor != nullptr);
    return static_cast<int>(monitor->Wait(timeoutMilliseconds));
}

extern "C" void CoreLibNative_LowLevelMonitor_Signal_Release(LowLevelMonitor *monitor)
{
    assert(monitor != nullptr);

    monitor->Signal();
    monitor->Release();
}
