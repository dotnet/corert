// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#ifndef _TARGETPTRS_H_
#define _TARGETPTRS_H_

typedef DPTR(class EEType) PTR_EEType;
typedef SPTR(struct StaticGcDesc) PTR_StaticGcDesc;

#ifdef TARGET_AMD64
typedef UInt64 UIntTarget;
#elif defined(TARGET_X86)
typedef UInt32 UIntTarget;
#elif defined(TARGET_ARM)
typedef UInt32 UIntTarget;
#elif defined(TARGET_ARM64)
typedef UInt64 UIntTarget;
#elif defined(TARGET_WASM)
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
