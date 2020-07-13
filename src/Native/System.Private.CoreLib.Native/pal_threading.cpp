// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "pal_common.h"
#include "pal_threading.h"
#include "pal_time.h"

#include <limits.h>
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
#error Don't know how to perform timed wait on this platform
#endif

LowLevelMonitor::LowLevelMonitor(bool abortOnFailure, bool *successRef) : LowLevelMutex(abortOnFailure, successRef)
{
    if (successRef != nullptr && !*successRef)
    {
        assert(!abortOnFailure);
        return;
    }

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
        if (abortOnFailure)
        {
            abort();
        }
        LowLevelMutex::~LowLevelMutex();
        *successRef = false;
        return;
    }

    error = pthread_condattr_setclock(&conditionAttributes, CLOCK_MONOTONIC);
    assert(error == 0);

    int initError = pthread_cond_init(&m_condition, &conditionAttributes);

    error = pthread_condattr_destroy(&conditionAttributes);
    assert(error == 0);
#endif

    if (initError != 0)
    {
        if (abortOnFailure)
        {
            abort();
        }
        LowLevelMutex::~LowLevelMutex();
        *successRef = false;
        return;
    }

    if (successRef != nullptr)
    {
        *successRef = true;
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
    void *monitorBuffer = malloc(sizeof(LowLevelMonitor));
    if (monitorBuffer == nullptr)
    {
        return nullptr;
    }

    bool success;
    LowLevelMonitor *monitor = new(monitorBuffer) LowLevelMonitor(false /* abortOnFailure */, &success);
    assert(monitor == monitorBuffer);
    if (!success)
    {
        free(monitorBuffer);
        return nullptr;
    }
    return monitor;
}

extern "C" void CoreLibNative_LowLevelMonitor_Delete(LowLevelMonitor *monitor)
{
    assert(monitor != nullptr);

    monitor->~LowLevelMonitor();
    free(monitor);
}

extern "C" void CoreLibNative_LowLevelMonitor_Wait(LowLevelMonitor *monitor)
{
    assert(monitor != nullptr);
    monitor->Wait();
}

// TODO: Change return type to 'bool' once marshaling support is added for it
extern "C" int32_t CoreLibNative_LowLevelMonitor_TimedWait(LowLevelMonitor *monitor, int32_t timeoutMilliseconds)
{
    assert(monitor != nullptr);
    return static_cast<int32_t>(monitor->Wait(timeoutMilliseconds));
}

extern "C" void CoreLibNative_LowLevelMonitor_Signal_Release(LowLevelMonitor *monitor)
{
    assert(monitor != nullptr);

    monitor->Signal();
    monitor->Release();
}

extern "C" bool CoreLibNative_RuntimeThread_CreateThread(size_t stackSize, void *(*startAddress)(void*), void *parameter)
{
    bool result = false;
    pthread_attr_t attrs;

    int error = pthread_attr_init(&attrs);
    if (error != 0)
    {
        // Do not call pthread_attr_destroy
        return false;
    }

    error = pthread_attr_setdetachstate(&attrs, PTHREAD_CREATE_DETACHED);
    assert(error == 0);

    if (stackSize > 0)
    {
        if (stackSize < PTHREAD_STACK_MIN)
        {
            stackSize = PTHREAD_STACK_MIN;
        }

        error = pthread_attr_setstacksize(&attrs, stackSize);
        if (error != 0) goto CreateThreadExit;
    }

    pthread_t threadId;
    error = pthread_create(&threadId, &attrs, startAddress, parameter);
    if (error != 0) goto CreateThreadExit;

    result = true;

CreateThreadExit:
    error = pthread_attr_destroy(&attrs);
    assert(error == 0);

    return result;
}
