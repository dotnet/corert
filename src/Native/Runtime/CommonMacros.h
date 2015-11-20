//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

// Some of our header files are shared with the binder, which needs the TARGET_* macros defined
#if defined(TARGET_X64)
#elif defined(TARGET_X86)
#elif defined(TARGET_ARM)
#else
#error Unsupported architecture
#endif

#define EXTERN_C extern "C"
#define FASTCALL __fastcall
#define STDCALL __stdcall
#define REDHAWK_API
#define REDHAWK_CALLCONV __fastcall

#ifdef _MSC_VER

#define MSVC_SAVE_WARNING_STATE() __pragma(warning(push))
#define MSVC_DISABLE_WARNING(warn_num) __pragma(warning(disable: warn_num))
#define MSVC_RESTORE_WARNING_STATE() __pragma(warning(pop))

#else

#define MSVC_SAVE_WARNING_STATE()
#define MSVC_DISABLE_WARNING(warn_num)
#define MSVC_RESTORE_WARNING_STATE()

#endif // _MSC_VER

#ifndef COUNTOF
template <typename _CountofType, size_t _SizeOfArray>
char (*COUNTOF_helper(_CountofType (&_Array)[_SizeOfArray]))[_SizeOfArray];
#define COUNTOF(_Array) sizeof(*COUNTOF_helper(_Array))
#endif // COUNTOF

#ifndef offsetof
#define offsetof(s,m)   (UIntNative)( (IntNative)&reinterpret_cast<const volatile char&>((((s *)0)->m)) )
#endif // offsetof

#ifndef GCENV_INCLUDED
#define FORCEINLINE __forceinline

inline UIntNative ALIGN_UP(UIntNative val, UIntNative alignment);
template <typename T>
inline T* ALIGN_UP(T* val, UIntNative alignment);

inline UIntNative ALIGN_DOWN(UIntNative val, UIntNative alignment);
template <typename T>
inline T* ALIGN_DOWN(T* val, UIntNative alignment);

inline bool IS_ALIGNED(UIntNative val, UIntNative alignment);
template <typename T>
inline bool IS_ALIGNED(T* val, UIntNative alignment);
#endif // GCENV_INCLUDED

#ifndef DACCESS_COMPILE
//
// Basic memory copying/clearing functionality (rather than depend on the CRT). All our current compilers
// actually provide these as intrinsics so use those for now (and provide non-CRT names for them as well).
//

EXTERN_C void * __cdecl memcpy(void *, const void *, size_t);
#pragma intrinsic(memcpy)
#ifndef CopyMemory
#define CopyMemory(_dst, _src, _size) memcpy((_dst), (_src), (_size))
#endif

EXTERN_C void * __cdecl memset(void *, int, size_t);
#pragma intrinsic(memset)
#ifndef FillMemory
#define FillMemory(_dst, _size, _fill) memset((_dst), (_fill), (_size))
#endif
#ifndef ZeroMemory
#define ZeroMemory(_dst, _size) memset((_dst), 0, (_size))
#endif

EXTERN_C int __cdecl memcmp(const void *,const void *,size_t);
#pragma intrinsic(memcmp)

//-------------------------------------------------------------------------------------------------
// min/max

#ifndef min
#define min(_a, _b) ((_a) < (_b) ? (_a) : (_b))
#endif
#ifndef max
#define max(_a, _b) ((_a) < (_b) ? (_b) : (_a))
#endif

#endif // !DACCESS_COMPILE

//-------------------------------------------------------------------------------------------------
// Platform-specific defines

#if defined(_AMD64_)

#define VIRTUAL_ALLOC_RESERVE_GRANULARITY (64*1024)    // 0x10000  (64 KB)
#define LOG2_PTRSIZE 3
#define POINTER_SIZE 8

#elif defined(_X86_)

#define VIRTUAL_ALLOC_RESERVE_GRANULARITY (64*1024)    // 0x10000  (64 KB)
#define LOG2_PTRSIZE 2
#define POINTER_SIZE 4

#elif defined(_ARM_)

#define VIRTUAL_ALLOC_RESERVE_GRANULARITY (64*1024)    // 0x10000  (64 KB)
#define LOG2_PTRSIZE 2
#define POINTER_SIZE 4

#else
#error Unsupported target architecture
#endif

#ifndef GCENV_INCLUDED
#if defined(_AMD64_)

#define DATA_ALIGNMENT  8
#define OS_PAGE_SIZE    0x1000

#elif defined(_X86_)

#define DATA_ALIGNMENT  4
#ifndef OS_PAGE_SIZE
#define OS_PAGE_SIZE    0x1000
#endif

#elif defined(_ARM_)

#define DATA_ALIGNMENT  4
#ifndef OS_PAGE_SIZE
#define OS_PAGE_SIZE    0x1000
#endif

#else
#error Unsupported target architecture
#endif
#endif // GCENV_INCLUDED

//
// Define an unmanaged function called from managed code that needs to execute in co-operative GC mode. (There
// should be very few of these, most such functions will be simply p/invoked).
//
#define COOP_PINVOKE_HELPER(_rettype, _method, _args) EXTERN_C REDHAWK_API _rettype __fastcall _method _args
#ifdef _X86_
// We have helpers that act like memcpy and memset from the CRT, so they need to be __cdecl.
#define COOP_PINVOKE_CDECL_HELPER(_rettype, _method, _args) EXTERN_C REDHAWK_API _rettype __cdecl _method _args
#else
#define COOP_PINVOKE_CDECL_HELPER COOP_PINVOKE_HELPER
#endif

#ifndef DACCESS_COMPILE
#define IN_DAC(x)
#define NOT_IN_DAC(x) x
#else
#define IN_DAC(x) x
#define NOT_IN_DAC(x)
#endif

#define INLINE inline

enum STARTUP_TIMELINE_EVENT_ID
{
    PROCESS_ATTACH_BEGIN = 0,
    NONGC_INIT_COMPLETE,
    GC_INIT_COMPLETE,
    PROCESS_ATTACH_COMPLETE,

    NUM_STARTUP_TIMELINE_EVENTS
};

#ifdef PROFILE_STARTUP
extern unsigned __int64 g_startupTimelineEvents[NUM_STARTUP_TIMELINE_EVENTS];
#define STARTUP_TIMELINE_EVENT(eventid) PalQueryPerformanceCounter((LARGE_INTEGER*)&g_startupTimelineEvents[eventid]);
#else // PROFILE_STARTUP
#define STARTUP_TIMELINE_EVENT(eventid)
#endif // PROFILE_STARTUP

bool inline FitsInI4(__int64 val)
{
    return val == (__int64)(__int32)val;
}

#ifndef C_ASSERT
#define C_ASSERT(e) static_assert(e, #e)
#endif // C_ASSERT

#ifdef __llvm__
#define DECLSPEC_THREAD __thread
#else // __llvm__
#define DECLSPEC_THREAD __declspec(thread)
#endif // !__llvm__

#ifndef GCENV_INCLUDED
#if !defined(_INC_WINDOWS) && !defined(BINDER)
#ifdef WIN32
// this must exactly match the typedef used by windows.h
typedef long HRESULT;
#else
typedef int32_t HRESULT;
#endif

#define S_OK  0x0
#define E_FAIL 0x80004005

#define UNREFERENCED_PARAMETER(P)          (void)(P)
#endif // !defined(_INC_WINDOWS) && !defined(BINDER)
#endif // GCENV_INCLUDED
