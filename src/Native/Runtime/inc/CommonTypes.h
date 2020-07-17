// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef __COMMON_TYPES_H__
#define __COMMON_TYPES_H__

#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <new>

using std::nothrow;
using std::size_t;
using std::uintptr_t;
using std::intptr_t;

//
// These type names are chosen to match the C# types
//
typedef int8_t              Int8;
typedef int16_t             Int16;
typedef int32_t             Int32;
typedef int64_t             Int64;
typedef uint8_t             UInt8;
typedef uint16_t            UInt16;
typedef uint32_t            UInt32;
typedef uint64_t            UInt64;
typedef intptr_t            IntNative;  // intentional deviation from C# IntPtr
typedef uintptr_t           UIntNative; // intentional deviation from C# UIntPtr
typedef wchar_t             WCHAR;
typedef void *              HANDLE;

typedef unsigned char       Boolean;
#define Boolean_false 0
#define Boolean_true 1

typedef UInt32              UInt32_BOOL;    // windows 4-byte BOOL, 0 -> false, everything else -> true
#define UInt32_FALSE        0
#define UInt32_TRUE         1

#define UInt16_MAX          ((UInt16)0xffffU)
#define UInt16_MIN          ((UInt16)0x0000U)

#define UInt32_MAX          ((UInt32)0xffffffffU)
#define UInt32_MIN          ((UInt32)0x00000000U)

#define Int32_MAX           ((Int32)0x7fffffff)
#define Int32_MIN           ((Int32)0x80000000)

#define UInt64_MAX          ((UInt64)0xffffffffffffffffUL)
#define UInt64_MIN          ((UInt64)0x0000000000000000UL)

#endif // __COMMON_TYPES_H__
