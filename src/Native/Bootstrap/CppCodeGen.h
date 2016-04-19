// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// CppCodeGen.h : Facilities for the C++ code generation backend

#ifndef __CPP_CODE_GEN_H
#define __CPP_CODE_GEN_H

#define _CRT_SECURE_NO_WARNINGS

#ifdef _MSC_VER
// Warnings disabled for generated cpp code
#pragma warning(disable:4200) // zero-sized array
#pragma warning(disable:4102) // unreferenced label
#pragma warning(disable:4244) // possible loss of data
#pragma warning(disable:4717) // recursive on all control paths
#endif

#ifdef _MSC_VER
#define INT64VAL(x) (x##i64)
#else
#define INT64VAL(x) (x##LL)
#endif

#ifdef _MSC_VER
#define CORERT_UNREACHABLE  __assume(0)
#else
#define CORERT_UNREACHABLE  __builtin_unreachable()
#endif

// Use the bit representation of uint64_t `v` as the bit representation of a double.
inline double __uint64_to_double(uint64_t v)
{
    union
    {
        uint64_t u64;
        double d;
    } val;
    val.u64 = v;
    return val.d;
}

#endif // __CPP_CODE_GEN_H
