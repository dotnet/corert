// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pal_common.h"
#include "pal_time.h"

#include <stdlib.h>
#include <string.h>
#if HAVE_SCHED_GETCPU
#include <sched.h>
#endif


extern "C" char* CoreLibNative_GetEnv(const char* variable)
{
    return getenv(variable);
}

extern "C" int32_t CoreLibNative_SchedGetCpu()
{
#if HAVE_SCHED_GETCPU
    return sched_getcpu();
#else
    return -1;
#endif
}

extern "C" void CoreLibNative_Exit(int32_t exitCode)
{
    exit(exitCode);
}
