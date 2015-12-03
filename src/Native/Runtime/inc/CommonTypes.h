//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
typedef signed char         Int8;
typedef signed short        Int16;
typedef signed int          Int32;
typedef signed __int64      Int64;
typedef unsigned char       UInt8;
typedef unsigned short      UInt16;
typedef unsigned int        UInt32;
typedef unsigned __int64    UInt64;
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
