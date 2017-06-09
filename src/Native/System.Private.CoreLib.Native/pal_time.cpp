// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pal_common.h"
#include "pal_threading.h"
#include "pal_time.h"

#include <unistd.h>
#include <sys/resource.h>

#if HAVE_MACH_ABSOLUTE_TIME
static LowLevelMutex s_lock(true /* abortOnFailure */, nullptr /* successRef */);

mach_timebase_info_data_t g_machTimebaseInfo = {};
bool g_isMachTimebaseInfoInitialized = false;

mach_timebase_info_data_t *InitializeTimebaseInfo()
{
    s_lock.Acquire();

    if (!g_isMachTimebaseInfoInitialized)
    {
        kern_return_t machRet = mach_timebase_info(&g_machTimebaseInfo);
        assert(machRet == KERN_SUCCESS);
        if (machRet == KERN_SUCCESS)
        {
            g_isMachTimebaseInfoInitialized = true;
        }
    }

    s_lock.Release();

    return g_isMachTimebaseInfoInitialized ? &g_machTimebaseInfo : nullptr;
}
#endif

uint64_t GetNanoseconds() {
    return CoreLibNative_GetHighPrecisionCounts() * (NanosecondsPerSecond / CoreLibNative_GetHighPrecisionCounts());
}

extern "C" uint64_t CoreLibNative_GetHighPrecisionCounts()
{
    uint64_t counts = 0;

#if HAVE_MACH_ABSOLUTE_TIME
    {
        mach_timebase_info_data_t *machTimebaseInfo = GetMachTimebaseInfo();
        counts = mach_absolute_time();
    }
#elif HAVE_CLOCK_MONOTONIC_COARSE || HAVE_CLOCK_MONOTONIC
    {
        clockid_t clockType =
#if HAVE_CLOCK_MONOTONIC_COARSE
            CLOCK_MONOTONIC_COARSE; // good enough resolution, fastest speed
#else
            CLOCK_MONOTONIC;
#endif
        struct timespec ts;
        if (clock_gettime(clockType, &ts) != 0)
        {
            assert(false);
            return counts;
        }
        counts = ts.tv_sec * SecondsToNanoseconds + ts.tv_nsec;
    }
#else
    {
        struct timeval tv;
        if (gettimeofday(&tv, nullptr) == -1)
        {
            assert(false);
            return counts;
        }
        counts = tv.tv_sec * MicrosecondsPerSecond + tv.tv_usec;
    }
#endif
    return counts;
}

extern "C" uint64_t CoreLibNative_GetHighPrecisionCounterFrequency()
{
    static uint64_t frequency = 0;
    if (frequency != 0)
    {
        return frequency;
    }

#if HAVE_MACH_ABSOLUTE_TIME
    {
        mach_timebase_info_data_t *machTimebaseInfo = GetMachTimebaseInfo();
        frequency = SecondsToNanoseconds * static_cast<uint64_t>(machTimebaseInfo->denom) / machTimebaseInfo->numer;
    }
#elif HAVE_CLOCK_MONOTONIC_COARSE || HAVE_CLOCK_MONOTONIC
    {
        frequency = SecondsToNanoseconds;
    }
#else
    {
        frequency = MicrosecondsPerSecond;
    }
#endif
    return frequency;
}

// Returns a 64-bit tick count with a millisecond resolution. It tries its best
// to return monotonically increasing counts and avoid being affected by changes
// to the system clock (either due to drift or due to explicit changes to system
// time).
extern "C" uint64_t CoreLibNative_GetTickCount64()
{
    return CoreLibNative_GetHighPrecisionCounts() * (MillisecondsPerSecond / CoreLibNative_GetHighPrecisionCounts());
}


typedef struct _PROCESS_CPU_INFORMATION
{
    uint64_t lastRecordedCurrentTime;
    uint64_t lastRecordedKernelTime;
    uint64_t lastRecordedUserTime;
} PROCESS_CPU_INFORMATION;


/*
Function:
CoreLibNative_GetCpuUtilization

The main purpose of this function is to compute the overall CPU utilization
for the CLR thread pool to regulate the number of worker threads.
Since there is no consistent API on Unix to get the CPU utilization
from a user process, getrusage and gettimeofday are used to
compute the current process's CPU utilization instead.

*/
extern "C" int32_t CoreLibNative_GetCpuUtilization(PROCESS_CPU_INFORMATION* previousCpuInfo)
{
    static long numProcessors = 0;

    if (numProcessors <= 0)
    {
        numProcessors = sysconf(_SC_NPROCESSORS_ONLN);
        if (numProcessors < 0)
        {
            return 0;
        }
    }

    uint64_t kernelTime = 0;
    uint64_t userTime = 0;

    struct rusage resUsage;
    if (getrusage(RUSAGE_SELF, &resUsage) == -1)
    {
        assert(false);
        return 0;
    }
    else
    {
        kernelTime = TimeValToNanoseconds(resUsage.ru_stime);
        userTime = TimeValToNanoseconds(resUsage.ru_utime);
    }

    uint64_t currentTime = GetNanoseconds();

    uint64_t lastRecordedCurrentTime = previousCpuInfo->lastRecordedCurrentTime;
    uint64_t lastRecordedKernelTime = previousCpuInfo->lastRecordedKernelTime;
    uint64_t lastRecordedUserTime = previousCpuInfo->lastRecordedUserTime;

    uint64_t cpuTotalTime = 0;
    if (currentTime > lastRecordedCurrentTime)
    {
        // cpuTotalTime is based on clock time. Since multiple threads can run in parallel,
        // we need to scale cpuTotalTime cover the same amount of total CPU time.
        // rusage time is already scaled across multiple processors.
        cpuTotalTime = (currentTime - lastRecordedCurrentTime);
        cpuTotalTime *= numProcessors;
    }

    uint64_t cpuBusyTime = 0;
    if (userTime >= lastRecordedUserTime && kernelTime >= lastRecordedKernelTime)
    {
        cpuBusyTime = (userTime - lastRecordedUserTime) + (kernelTime - lastRecordedKernelTime);
    }

    int32_t cpuUtilization = 0;
    if (cpuTotalTime > 0 && cpuBusyTime > 0)
    {
        cpuUtilization = static_cast<int32_t>(cpuBusyTime / cpuTotalTime);
    }

    assert(cpuUtilization >= 0 && cpuUtilization <= 100);

    previousCpuInfo->lastRecordedCurrentTime = currentTime;
    previousCpuInfo->lastRecordedUserTime = userTime;
    previousCpuInfo->lastRecordedKernelTime = kernelTime;

    return cpuUtilization;
}

