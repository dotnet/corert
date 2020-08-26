// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// common.h : include file for standard system include files,
// or project specific include files that are used frequently, but
// are changed infrequently
//

#pragma once

#define WIN32_LEAN_AND_MEAN             // Exclude rarely-used stuff from Windows headers
// Windows Header Files:

#include <windows.h>

#ifndef _CRT_SECURE_NO_WARNINGS
 #define _CRT_SECURE_NO_WARNINGS
#endif // _CRT_SECURE_NO_WARNINGS

#include <stdint.h>
#include <stddef.h>
#include <stdio.h>
#include <string.h>
#include <wchar.h>
#include <assert.h>
#include <stdarg.h>
#include <memory.h>

#include <new>

#ifdef TARGET_UNIX
#include <pthread.h>
#endif

using namespace std;

#include "..\Runtime\inc\CommonTypes.h"
#include "..\Runtime\inc\daccess.h"
#include "..\Runtime\inc\varint.h"
#include "..\Runtime\PalRedhawkCommon.h" // Fp128
#include "..\Runtime\regdisplay.h"
#include "..\Runtime\ICodemanager.h"

//
// This macro returns val rounded up as necessary to be a multiple of alignment; alignment must be a power of 2
//
inline size_t ALIGN_UP(size_t val, size_t alignment)
{
    // alignment must be a power of 2 for this implementation to work (need modulo otherwise)
    assert(0 == (alignment & (alignment - 1)));
    size_t result = (val + (alignment - 1)) & ~(alignment - 1);
    assert(result >= val);      // check for overflow
    return result;
}
inline void* ALIGN_UP(void* val, size_t alignment)
{
    return (void*)ALIGN_UP((size_t)val, alignment);
}

inline size_t ALIGN_DOWN(size_t val, size_t alignment)
{
    // alignment must be a power of 2 for this implementation to work (need modulo otherwise)
    assert(0 == (alignment & (alignment - 1)));
    size_t result = val & ~(alignment - 1);
    return result;
}
inline void* ALIGN_DOWN(void* val, size_t alignment)
{
    return (void*)ALIGN_DOWN((size_t)val, alignment);
}

inline BOOL IS_ALIGNED(size_t val, size_t alignment)
{
    // alignment must be a power of 2 for this implementation to work (need modulo otherwise)
    assert(0 == (alignment & (alignment - 1)));
    return 0 == (val & (alignment - 1));
}
inline BOOL IS_ALIGNED(const void* val, size_t alignment)
{
    return IS_ALIGNED((size_t)val, alignment);
}

// Rounds a ULONG up to the nearest power of two number.
inline ULONG RoundUpToPower2(ULONG x)
{
    if (x == 0) return 1;

    x = x - 1;
    x = x | (x >> 1);
    x = x | (x >> 2);
    x = x | (x >> 4);
    x = x | (x >> 8);
    x = x | (x >> 16);
    return x + 1;
}

inline DWORD ALIGN_UP(DWORD val, int alignment)
{
    // alignment must be a power of 2 for this implementation to work (need modulo otherwise)
    assert(0 == (alignment & (alignment - 1)));
    DWORD result = (val + (alignment - 1)) & ~(alignment - 1);
    assert(result >= val);      // check for overflow
    return result;
}


#ifdef _DEBUG
extern void TraceImpl(const char *fmt, ...);
extern void TraceImpl(const wchar_t *fmt, ...);
#define TRACE(fmt, ...) TraceImpl((fmt), __VA_ARGS__)
#else
#define TRACE(fmt, ...)
#endif
