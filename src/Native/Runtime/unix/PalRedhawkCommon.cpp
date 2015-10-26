//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

//
// Implementation of the portions of the Redhawk Platform Abstraction Layer (PAL) library that are common among
// multiple PAL variants.
//
// Note that in general we don't want to assume that Windows and Redhawk global definitions can co-exist.
// Since this code must include Windows headers to do its job we can't therefore safely include general
// Redhawk header files.
//

#include <banned.h>
#include <stdio.h>
#include <errno.h>
#include <cstdarg>
#include "CommonTypes.h"
#include "daccess.h"
#include <PalRedhawkCommon.h>
#include "CommonMacros.h"
#include "assert.h"
#include <pthread.h>
#include "config.h"

#ifdef USE_PORTABLE_HELPERS
#define assert(expr) ASSERT(expr)
#endif

#define REDHAWK_PALEXPORT extern "C"
#define REDHAWK_PALAPI

REDHAWK_PALEXPORT void __cdecl PalPrintf(_In_z_ _Printf_format_string_ const char * szFormat, ...)
{
#if defined(_DEBUG)
    va_list args;
    va_start(args, szFormat);
    vprintf(szFormat, args);
#endif
}

REDHAWK_PALEXPORT void __cdecl PalFlushStdout()
{
#if defined(_DEBUG)
    fflush(stdout);
#endif
}

REDHAWK_PALEXPORT int __cdecl PalSprintf(_Out_writes_z_(cchBuffer) char * szBuffer, size_t cchBuffer, _In_z_ _Printf_format_string_ const char * szFormat, ...)
{
    va_list args;
    va_start(args, szFormat);
    return vsnprintf(szBuffer, cchBuffer, szFormat, args);
}

REDHAWK_PALEXPORT int __cdecl PalVSprintf(_Out_writes_z_(cchBuffer) char * szBuffer, size_t cchBuffer, _In_z_ _Printf_format_string_ const char * szFormat, va_list args)
{
    return vsnprintf(szBuffer, cchBuffer, szFormat, args);
}

// Given the OS handle of a loaded module, compute the upper and lower virtual address bounds (inclusive).
REDHAWK_PALEXPORT void REDHAWK_PALAPI PalGetModuleBounds(HANDLE hOsHandle, _Out_ UInt8 ** ppLowerBound, _Out_ UInt8 ** ppUpperBound)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

REDHAWK_PALEXPORT Int32 PalGetProcessCpuCount()
{
    // UNIXTODO: Implement this function
    return 1;
}

//Reads the entire contents of the file into the specified buffer, buff
//returns the number of bytes read if the file is successfully read
//returns 0 if the file is not found, size is greater than maxBytesToRead or the file couldn't be opened or read
REDHAWK_PALEXPORT UInt32 PalReadFileContents(_In_z_ WCHAR* fileName, _Out_writes_all_(cchBuff) char* buff, _In_ UInt32 cchBuff)
{
    // TODO: Implement this function
    return 0;
}

__thread void* pStackHighOut = NULL;
__thread void* pStackLowOut = NULL;

// Retrieves the entire range of memory dedicated to the calling thread's stack.  This does
// not get the current dynamic bounds of the stack, which can be significantly smaller than 
// the maximum bounds.
REDHAWK_PALEXPORT bool PalGetMaximumStackBounds(_Out_ void** ppStackLowOut, _Out_ void** ppStackHighOut)
{
    if (pStackHighOut == NULL)
    {
#ifdef __APPLE__
        // This is a Mac specific method
        pStackHighOut = pthread_get_stackaddr_np(pthread_self());
        pStackLowOut = ((uint8_t *)pStackHighOut - pthread_get_stacksize_np(pthread_self()));
#else // __APPLE__
        pthread_attr_t attr;
        size_t stackSize;
        int status;

        pthread_t thread = pthread_self();

        status = pthread_attr_init(&attr);
        ASSERT_MSG(status == 0, "pthread_attr_init call failed");

#if HAVE_PTHREAD_ATTR_GET_NP
        status = pthread_attr_get_np(thread, &attr);
#elif HAVE_PTHREAD_GETATTR_NP
        status = pthread_getattr_np(thread, &attr);
#else
#error Dont know how to get thread attributes on this platform!
#endif
        ASSERT_MSG(status == 0, "pthread_getattr_np call failed");

        status = pthread_attr_getstack(&attr, &pStackLowOut, &stackSize);
        ASSERT_MSG(status == 0, "pthread_attr_getstack call failed");

        status = pthread_attr_destroy(&attr);
        ASSERT_MSG(status == 0, "pthread_attr_destroy call failed");

        pStackHighOut = (uint8_t*)pStackLowOut + stackSize;
#endif // __APPLE__
    }

    *ppStackLowOut = pStackLowOut;
    *ppStackHighOut = pStackHighOut;

    return true;
}

// retrieves the full path to the specified module, if moduleBase is NULL retreieves the full path to the 
// executable module of the current process.
//
// Return value:  number of characters in name string
//
//NOTE:  This implementation exists because calling GetModuleFileName is not wack compliant.  if we later decide
//       that the framework package containing mrt100_app no longer needs to be wack compliant, this should be 
//       removed and the windows implementation of GetModuleFileName should be substitued on windows.
REDHAWK_PALEXPORT Int32 PalGetModuleFileName(_Out_ wchar_t** pModuleNameOut, HANDLE moduleBase)
{
    // UNIXTODO: Implement this function!
    *pModuleNameOut = NULL;
    return 0;
}
