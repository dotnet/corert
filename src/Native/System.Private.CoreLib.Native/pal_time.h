// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma once

#include "pal_common.h"

#include <time.h>

#include <sys/time.h>

#if HAVE_MACH_ABSOLUTE_TIME
#include <mach/mach_time.h>
#endif

const uint64_t MillisecondsPerSecond = 1000;
const uint64_t NanosecondsPerSecond = 1000 * 1000 * 1000;
const uint64_t MicrosecondsPerSecond = 1000 * 1000;
const uint64_t NanosecondsPerMicrosecond = 1000;
const uint64_t NanosecondsPerMillisecond = 1000 * 1000;

inline void MillisecondsToTimeSpec(uint32_t milliseconds, timespec *t)
{
    if (milliseconds == 0)
    {
        t->tv_sec = 0;
        t->tv_nsec = 0;
        return;
    }

    uint64_t nanoseconds = milliseconds * NanosecondsPerMillisecond;
    t->tv_sec = static_cast<time_t>(nanoseconds / NanosecondsPerSecond);
    t->tv_nsec = static_cast<int32_t>(nanoseconds % NanosecondsPerSecond);
}

inline void AddMillisecondsToTimeSpec(uint32_t milliseconds, timespec *t)
{
    if (milliseconds == 0)
    {
        return;
    }

    uint64_t nanoseconds = milliseconds * NanosecondsPerMillisecond + t->tv_nsec;
    if (nanoseconds >= NanosecondsPerSecond)
    {
        t->tv_sec += static_cast<time_t>(nanoseconds / NanosecondsPerSecond);
        nanoseconds %= NanosecondsPerSecond;
    }
    t->tv_nsec = static_cast<int32_t>(nanoseconds);
}

inline uint64_t TimeValToNanoseconds(const struct timeval& t)
{
    return t.tv_sec * NanosecondsPerSecond + t.tv_usec * NanosecondsPerMicrosecond;
}

#if HAVE_MACH_ABSOLUTE_TIME
extern mach_timebase_info_data_t g_machTimebaseInfo;
extern bool g_isMachTimebaseInfoInitialized;

mach_timebase_info_data_t *InitializeTimebaseInfo();

inline mach_timebase_info_data_t *GetMachTimebaseInfo()
{
    if (g_isMachTimebaseInfoInitialized)
    {
        return &g_machTimebaseInfo;
    }
    return InitializeTimebaseInfo();
}
#endif
