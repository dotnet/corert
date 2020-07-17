// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// common.h : include file for standard system include files,
// or project specific include files that are used frequently, but
// are changed infrequently
//

#ifndef __COMMON_H
#define __COMMON_H

#define _CRT_SECURE_NO_WARNINGS

#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <wchar.h>
#include <assert.h>
#include <stdarg.h>
#include <stddef.h>
#include <math.h>

#include <new>

#ifndef _WIN32
#include <pthread.h>
#endif

using namespace std;

class MethodTable;
class Object;

#ifdef _MSC_VER
#define __NORETURN __declspec(noreturn)
#else
#define __NORETURN __attribute((noreturn))
#endif

int __initialize_runtime();
void __shutdown_runtime();

extern "C" Object * __allocate_object(MethodTable * pMT);
extern "C" Object * __allocate_array(size_t elements, MethodTable * pMT);
extern "C" Object * __castclass(MethodTable * pMT, void * obj);
extern "C" Object * __isinst(MethodTable * pMT, void * obj);
extern "C" __NORETURN void __throw_exception(void * pEx);
extern "C" void __debug_break();

Object * __load_string_literal(const char * string);

extern "C" void __range_check_fail();

inline void __range_check(void * a, size_t elem)
{
    if (elem >= *((size_t*)a + 1))
        __range_check_fail();
}

Object * __get_commandline_args(int argc, char * argv[]);

// POD version of EEType to use for static initialization
struct RawEEType
{
    uint16_t    m_componentSize;
    uint16_t    m_flags;
    uint32_t    m_baseSize;
    MethodTable * m_pBaseType;
    uint16_t    m_usNumVtableSlots;
    uint16_t    m_usNumInterfaces;
    uint32_t    m_uHashCode;
};

struct ReversePInvokeFrame;

void __reverse_pinvoke(ReversePInvokeFrame* pRevFrame);
void __reverse_pinvoke_return(ReversePInvokeFrame* pRevFrame);

struct PInvokeTransitionFrame;

void __pinvoke(PInvokeTransitionFrame* pFrame);
void __pinvoke_return(PInvokeTransitionFrame* pFrame);

typedef size_t UIntNative;

inline bool IS_ALIGNED(UIntNative val, UIntNative alignment)
{
    //ASSERT(0 == (alignment & (alignment - 1)));
    return 0 == (val & (alignment - 1));
}

template <typename T>
inline bool IS_ALIGNED(T* val, UIntNative alignment)
{
    //ASSERT(0 == (alignment & (alignment - 1)));
    return IS_ALIGNED(reinterpret_cast<UIntNative>(val), alignment);
}

#define RAW_MIN_OBJECT_SIZE (3*sizeof(void*))

#define AlignBaseSize(s) ((s < RAW_MIN_OBJECT_SIZE) ? RAW_MIN_OBJECT_SIZE : ((s + (sizeof(void*)-1) & ~(sizeof(void*)-1))))

#define ARRAY_BASE (2*sizeof(void*))

#endif // __COMMON_H
