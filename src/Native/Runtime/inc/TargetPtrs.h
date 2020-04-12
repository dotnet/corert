// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#ifndef _TARGETPTRS_H_
#define _TARGETPTRS_H_

typedef DPTR(class EEType) PTR_EEType;
typedef SPTR(struct StaticGcDesc) PTR_StaticGcDesc;

#ifdef _TARGET_AMD64_
typedef UInt64 UIntTarget;
#elif defined(_TARGET_X86_)
typedef UInt32 UIntTarget;
#elif defined(_TARGET_ARM_)
typedef UInt32 UIntTarget;
#elif defined(_TARGET_ARM64_)
typedef UInt64 UIntTarget;
#elif defined(_TARGET_WASM_)
typedef UInt32 UIntTarget;
#else
#error unexpected target architecture
#endif

typedef PTR_UInt8                       TgtPTR_UInt8;
typedef PTR_UInt32                      TgtPTR_UInt32;
typedef void *                          TgtPTR_Void;
typedef PTR_EEType                      TgtPTR_EEType;
typedef class Thread *                  TgtPTR_Thread;
typedef struct CORINFO_Object *         TgtPTR_CORINFO_Object;
typedef PTR_StaticGcDesc                TgtPTR_StaticGcDesc;

#endif // !_TARGETPTRS_H_
