//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
#ifdef _MSC_VER
#define ASSUME(expr) __assume(expr)
#else  // _MSC_VER
#define ASSUME(expr)
#endif // _MSC_VER

#if defined(_DEBUG) && !defined(DACCESS_COMPILE)

#define ASSERT(expr) \
    { \
    if (!(expr)) { Assert(#expr, __FILE__, __LINE__, NULL); } \
    } \

#define ASSERT_MSG(expr, msg) \
    { \
    if (!(expr)) { Assert(#expr, __FILE__, __LINE__, msg); } \
    } \

#define VERIFY(expr) ASSERT((expr))

#define ASSERT_UNCONDITIONALLY(message) \
    Assert("ASSERT_UNCONDITIONALLY", __FILE__, __LINE__, message); \

void Assert(const char * expr, const char * file, UInt32 line_num, const char * message);

#else

#define ASSERT(expr)

#define ASSERT_MSG(expr, msg)

#define VERIFY(expr) (expr)

#define ASSERT_UNCONDITIONALLY(message)

#endif 

#define UNREACHABLE() \
    ASSERT_UNCONDITIONALLY("UNREACHABLE"); \
    ASSUME(0); \

#define UNREACHABLE_MSG(message) \
    ASSERT_UNCONDITIONALLY(message); \
    ASSUME(0);  \

#define FAIL_FAST_GENERATE_EXCEPTION_ADDRESS 0x1

#define RhFailFast()  RhFailFast2(NULL, NULL)

#define RhFailFast2(pExRec, pExCtx) \
{ \
    ASSERT_UNCONDITIONALLY("FailFast"); \
    PalRaiseFailFastException((pExRec), (pExCtx), (pExRec)==NULL ? FAIL_FAST_GENERATE_EXCEPTION_ADDRESS : 0); \
}
