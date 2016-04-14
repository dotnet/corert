// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#include <stdlib.h>
#include <stdint.h>
#include <string.h>
#include <unistd.h>
#include <assert.h>
#include <config.h>
#include <sys/time.h>
#include <time.h>

#if HAVE_MACH_ABSOLUTE_TIME
#include <mach/mach_time.h>
static mach_timebase_info_data_t s_timebaseInfo = {};
#endif

extern "C" int32_t CoreLibNative_GetEnvironmentVariable(const char* variable, char** result)
{
    assert(result != NULL);

    // Read the environment variable
    *result = getenv(variable);

    if (*result == NULL)
    {
        return 0;
    }

    size_t resultSize = strlen(*result);

    // Return -1 if the size overflows an integer so that we can throw on managed side
    if ((size_t)(int32_t)resultSize != resultSize)
    {
        *result = NULL;
        return -1;
    }

    return (int32_t)resultSize;
}

extern "C" int32_t CoreLibNative_GetMachineName(char* hostNameBuffer, int32_t hostNameBufferLength)
{
    assert(hostNameBuffer != NULL && hostNameBufferLength > 0);

    int32_t res = gethostname(hostNameBuffer, hostNameBufferLength);
    if (res < 0)
        return res;

    // If the hostname is truncated, it is unspecified whether the returned buffer includes a terminating null byte.
    hostNameBuffer[hostNameBufferLength - 1] = '\0';

    // truncate the domain from the host name if it exist    
    char *pDot = strchr(hostNameBuffer, '.');
    if (pDot != NULL)
    {
        *pDot = '\0';    
    }
    
    return strlen(hostNameBuffer);
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

#if HAVE_CLOCK_MONOTONIC_COARSE || HAVE_CLOCK_MONOTONIC
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
        retval = (ts.tv_sec * SECONDS_TO_MILLISECONDS)+(ts.tv_nsec / MILLISECONDS_TO_NANOSECONDS);
    }
#elif HAVE_MACH_ABSOLUTE_TIME
    {
        // use denom == 0 to indicate that s_timebaseInfo is uninitialised.
        if (s_timebaseInfo.denom == 0)
        {
            kern_return_t machRet;
            if ((machRet = mach_timebase_info(&s_timebaseInfo)) != KERN_SUCCESS)
            {
                assert(false);
                return retval;
            }            
        }
        retval = (mach_absolute_time() * s_timebaseInfo.numer / s_timebaseInfo.denom) / MILLISECONDS_TO_NANOSECONDS;
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
#endif // HAVE_CLOCK_MONOTONIC 
    return retval;
}

extern "C" void CoreLibNative_ExitProcess(int32_t exitCode)
{
    exit(exitCode);
}
