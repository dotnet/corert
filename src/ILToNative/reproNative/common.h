// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// common.h : include file for standard system include files,
// or project specific include files that are used frequently, but
// are changed infrequently
//

#ifndef __COMMON_H
#define __COMMON_H

#define _CRT_SECURE_NO_WARNINGS

#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include <wchar.h>
#include <assert.h>
#include <stdarg.h>
#include <stddef.h>

#include <new>

#ifndef WIN32
#include <pthread.h>
#endif

using namespace std;

class MethodTable;
class Object;

template <typename T>
class Array
{
};

int __initialize_runtime();
void __shutdown_runtime();

extern "C" Object * __allocate_object(MethodTable * pMT);
extern "C" Object * __allocate_array(MethodTable * pMT, size_t elements);
__declspec(noreturn) void __throw_exception(void * pEx);
Object * __load_string_literal(const char * string);

Object * __castclass_class(void * p, MethodTable * pMT);
Object * __isinst_class(void * p, MethodTable * pMT);

void __range_check(void * a, size_t elem);

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

struct ReversePInvokeFrame
{
    void*   m_savedPInvokeTransitionFrame;
    void*   m_savedThread;
};

void __reverse_pinvoke(ReversePInvokeFrame* pRevFrame);
void __reverse_pinvoke_return(ReversePInvokeFrame* pRevFrame);

struct StaticGcDesc
{
    struct GCSeries
    {
        uint32_t m_size;
        uint32_t m_startOffset;
    };

    uint32_t m_numSeries;
    GCSeries m_series[0];
};

struct SimpleModuleHeader
{
    void* m_pStaticsGcDataSection;
    StaticGcDesc* m_pStaticsGcInfo;
    StaticGcDesc* m_pThreadStaticsGcInfo;
};

void __register_module(SimpleModuleHeader* pModule);


#pragma warning(disable:4102)

#define AlignBaseSize(s) ((s < MIN_OBJECT_SIZE) ? MIN_OBJECT_SIZE : ((s + (sizeof(intptr_t)-1) & ~(sizeof(intptr_t)-1))))

#define ARRAY_BASE (2*sizeof(void*))

#endif // __COMMON_H
