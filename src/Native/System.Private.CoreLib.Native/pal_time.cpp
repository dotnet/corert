// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pal_common.h"
#include "pal_threading.h"
#include "pal_time.h"

#include <pthread.h>

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
