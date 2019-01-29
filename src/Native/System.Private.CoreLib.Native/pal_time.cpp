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

extern "C" uint64_t CoreLibNative_GetHighPrecisionCount()
{
    uint64_t counts = 0;

#if HAVE_MACH_ABSOLUTE_TIME
    {
        counts = mach_absolute_time();
    }
#elif HAVE_CLOCK_MONOTONIC
    {
        clockid_t clockType = CLOCK_MONOTONIC;
        struct timespec ts;
        if (clock_gettime(clockType, &ts) != 0)
        {
            assert(false);
            return counts;
        }
        counts = ts.tv_sec * NanosecondsPerSecond + ts.tv_nsec;
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

#if HAVE_MACH_ABSOLUTE_TIME
static uint64_t s_highPrecisionCounterFrequency = 0;
#endif
extern "C" uint64_t CoreLibNative_GetHighPrecisionCounterFrequency()
{
#if HAVE_MACH_ABSOLUTE_TIME
    if (s_highPrecisionCounterFrequency != 0)
    {
        return s_highPrecisionCounterFrequency;
    }
    {
        mach_timebase_info_data_t *machTimebaseInfo = GetMachTimebaseInfo();
        s_highPrecisionCounterFrequency = NanosecondsPerSecond * static_cast<uint64_t>(machTimebaseInfo->denom) / machTimebaseInfo->numer;
    }
    return s_highPrecisionCounterFrequency;
#elif HAVE_CLOCK_MONOTONIC_COARSE || HAVE_CLOCK_MONOTONIC
    {
        return NanosecondsPerSecond;
    }
#else
    {
        return MicrosecondsPerSecond;
    }
#endif
    return 0;
}

#define SECONDS_TO_MILLISECONDS 1000
#define MILLISECONDS_TO_MICROSECONDS 1000
#define MILLISECONDS_TO_NANOSECONDS 1000000 // 10^6

// Returns a 64-bit tick count with a millisecond resolution. It tries its best
// to return monotonically increasing counts and avoid being affected by changes
// to the system clock (either due to drift or due to explicit changes to system
// time).
extern "C" uint64_t CoreLibNative_GetTickCount64()
{
    uint64_t retval = 0;

#if HAVE_MACH_ABSOLUTE_TIME
    {
        mach_timebase_info_data_t *machTimebaseInfo = GetMachTimebaseInfo();
        retval = (mach_absolute_time() * machTimebaseInfo->numer / machTimebaseInfo->denom) / MILLISECONDS_TO_NANOSECONDS;
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
            return retval;
        }
        retval = (ts.tv_sec * SECONDS_TO_MILLISECONDS) + (ts.tv_nsec / MILLISECONDS_TO_NANOSECONDS);
    }
#else
    {
        struct timeval tv;
        if (gettimeofday(&tv, NULL) == -1)
        {
            assert(false);
            return retval;
        }
        retval = (tv.tv_sec * SECONDS_TO_MILLISECONDS) + (tv.tv_usec / MILLISECONDS_TO_MICROSECONDS);
    }
#endif
    return retval;
}
