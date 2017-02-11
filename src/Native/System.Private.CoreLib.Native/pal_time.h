// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include "pal_common.h"

#include <time.h>

#include <sys/time.h>

#if HAVE_MACH_ABSOLUTE_TIME
#include <mach/mach_time.h>
#endif

const int32_t MillisecondsToNanoseconds = 1000 * 1000;
const int32_t SecondsToNanoseconds = 1000 * 1000 * 1000;

inline void MillisecondsToTimeSpec(uint32_t milliseconds, timespec *t)
{
    if (milliseconds == 0)
    {
        t->tv_sec = 0;
        t->tv_nsec = 0;
        return;
    }

    uint64_t nanoseconds = milliseconds * static_cast<uint64_t>(MillisecondsToNanoseconds);
    t->tv_sec = static_cast<time_t>(nanoseconds / SecondsToNanoseconds);
    t->tv_nsec = static_cast<int32_t>(nanoseconds % SecondsToNanoseconds);
}

inline void AddMillisecondsToTimeSpec(uint32_t milliseconds, timespec *t)
{
    if (milliseconds == 0)
    {
        return;
    }

    uint64_t nanoseconds = milliseconds * static_cast<uint64_t>(MillisecondsToNanoseconds) + t->tv_nsec;
    if (nanoseconds >= SecondsToNanoseconds)
    {
        t->tv_sec += static_cast<time_t>(nanoseconds / SecondsToNanoseconds);
        nanoseconds %= SecondsToNanoseconds;
    }
    t->tv_nsec = static_cast<int32_t>(nanoseconds);
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
