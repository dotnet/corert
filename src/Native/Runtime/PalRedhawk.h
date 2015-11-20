//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

//
// Provides declarations for external resources consumed by Redhawk. This comprises functionality
// normally exported from Win32 libraries such as KERNEL32 and MSVCRT. When hosted on Win32 calls to these
// functions become simple pass throughs to the native implementation via export forwarding entries in a PAL
// (Platform Abstraction Layer) library. On other platforms the PAL library has actual code to emulate the
// functionality of these same APIs.
//
// In order to make it both obvious and intentional where Redhawk consumes an external API, such functions are
// decorated with an 'Pal' prefix. Ideally the associated supporting types, constants etc. would be
// similarly isolated from their concrete Win32 definitions, making the extent of platform dependence within
// the core explicit. For now that is too big a work item and we'll settle for manually restricting the use of
// external header files to within this header.
//

#include <sal.h>
#include <stdarg.h>

#ifndef PAL_REDHAWK_INCLUDED
#define PAL_REDHAWK_INCLUDED

/* Adapted from intrin.h - For compatibility with <winnt.h>, some intrinsics are __cdecl except on x64 */
#if defined (_M_X64)
#define __PN__MACHINECALL_CDECL_OR_DEFAULT
#else
#define __PN__MACHINECALL_CDECL_OR_DEFAULT __cdecl
#endif

#ifndef _INC_WINDOWS
//#ifndef DACCESS_COMPILE 

// There are some fairly primitive type definitions below but don't pull them into the rest of Redhawk unless
// we have to (in which case these definitions will move to CommonTypes.h).
typedef WCHAR *             LPWSTR;
typedef const WCHAR *       LPCWSTR;
typedef void *              HINSTANCE;

typedef void *              LPSECURITY_ATTRIBUTES;
typedef void *              LPOVERLAPPED;

typedef void(__stdcall *PFLS_CALLBACK_FUNCTION) (void* lpFlsData);
#define FLS_OUT_OF_INDEXES ((UInt32)0xFFFFFFFF)

#ifndef GCENV_INCLUDED
#define CALLBACK            __stdcall
#define WINAPI              __stdcall
#define WINBASEAPI          __declspec(dllimport)

union LARGE_INTEGER
{
    struct
    {
        UInt32  LowPart;
        Int32   HighPart;
    } u;
    Int64       QuadPart;
};

typedef struct _GUID {
    unsigned long  Data1;
    unsigned short Data2;
    unsigned short Data3;
    unsigned char  Data4[8];
} GUID;

struct CRITICAL_SECTION
{
    void *      DebugInfo;
    Int32       LockCount;
    Int32       RecursionCount;
    HANDLE      OwningThread;
    HANDLE      LockSemaphore;
    UIntNative  SpinCount;
};
#endif //!GCENV_INCLUDED

#define DECLARE_HANDLE(_name) typedef HANDLE _name

struct SYSTEM_INFO
{
    union
    {
        UInt32  dwOemId;
        struct {
            UInt16 wProcessorArchitecture;
            UInt16 wReserved;
        } DUMMYSTRUCTNAME;
    } DUMMYUNIONNAME;
    UInt32      dwPageSize;
    void *      lpMinimumApplicationAddress;
    void *      lpMaximumApplicationAddress;
    UIntNative  dwActiveProcessorMask;
    UInt32      dwNumberOfProcessors;
    UInt32      dwProcessorType;
    UInt32      dwAllocationGranularity;
    UInt16      wProcessorLevel;
    UInt16      wProcessorRevision;
};

// defined in gcrhenv.cpp
bool __SwitchToThread(uint32_t dwSleepMSec, uint32_t dwSwitchCount);

struct OSVERSIONINFOEXW
{
    UInt32 dwOSVersionInfoSize;
    UInt32 dwMajorVersion;
    UInt32 dwMinorVersion;
    UInt32 dwBuildNumber;
    UInt32 dwPlatformId;
    WCHAR  szCSDVersion[128];
    UInt16 wServicePackMajor;
    UInt16 wServicePackMinor;
    UInt16 wSuiteMask;
    UInt8 wProductType;
    UInt8 wReserved;
};

struct FILETIME
{
    UInt32 dwLowDateTime;
    UInt32 dwHighDateTime;
};

enum MEMORY_RESOURCE_NOTIFICATION_TYPE
{
    LowMemoryResourceNotification,
    HighMemoryResourceNotification
};

enum LOGICAL_PROCESSOR_RELATIONSHIP
{
    RelationProcessorCore,
    RelationNumaNode,
    RelationCache,
    RelationProcessorPackage
};

#define LTP_PC_SMT 0x1

enum PROCESSOR_CACHE_TYPE
{
    CacheUnified,
    CacheInstruction,
    CacheData,
    CacheTrace
};

struct CACHE_DESCRIPTOR
{
    UInt8   Level;
    UInt8   Associativity;
    UInt16  LineSize;
    UInt32  Size;
    PROCESSOR_CACHE_TYPE Type;
};

struct SYSTEM_LOGICAL_PROCESSOR_INFORMATION
{
    UIntNative   ProcessorMask;
    LOGICAL_PROCESSOR_RELATIONSHIP Relationship;
    union
    {
        struct
        {
            UInt8  Flags;
        } ProcessorCore;
        struct
        {
            UInt32 NodeNumber;
        } NumaNode;
        CACHE_DESCRIPTOR Cache;
        UInt64  Reserved[2];
    };
};

#ifdef _AMD64_

typedef struct DECLSPEC_ALIGN(16) _XSAVE_FORMAT {
    UInt16  ControlWord;
    UInt16  StatusWord;
    UInt8   TagWord;
    UInt8   Reserved1;
    UInt16  ErrorOpcode;
    UInt32  ErrorOffset;
    UInt16  ErrorSelector;
    UInt16  Reserved2;
    UInt32  DataOffset;
    UInt16  DataSelector;
    UInt16  Reserved3;
    UInt32  MxCsr;
    UInt32  MxCsr_Mask;
    Fp128   FloatRegisters[8];
#if defined(_WIN64)
    Fp128   XmmRegisters[16];
    UInt8   Reserved4[96];
#else
    Fp128   XmmRegisters[8];
    UInt8   Reserved4[220];
    UInt32  Cr0NpxState;
#endif
} XSAVE_FORMAT, *PXSAVE_FORMAT;


typedef XSAVE_FORMAT XMM_SAVE_AREA32, *PXMM_SAVE_AREA32;

typedef struct DECLSPEC_ALIGN(16) _CONTEXT {
    UInt64 P1Home;
    UInt64 P2Home;
    UInt64 P3Home;
    UInt64 P4Home;
    UInt64 P5Home;
    UInt64 P6Home;
    UInt32 ContextFlags;
    UInt32 MxCsr;
    UInt16 SegCs;
    UInt16 SegDs;
    UInt16 SegEs;
    UInt16 SegFs;
    UInt16 SegGs;
    UInt16 SegSs;
    UInt32 EFlags;
    UInt64 Dr0;
    UInt64 Dr1;
    UInt64 Dr2;
    UInt64 Dr3;
    UInt64 Dr6;
    UInt64 Dr7;
    UInt64 Rax;
    UInt64 Rcx;
    UInt64 Rdx;
    UInt64 Rbx;
    UInt64 Rsp;
    UInt64 Rbp;
    UInt64 Rsi;
    UInt64 Rdi;
    UInt64 R8;
    UInt64 R9;
    UInt64 R10;
    UInt64 R11;
    UInt64 R12;
    UInt64 R13;
    UInt64 R14;
    UInt64 R15;
    UInt64 Rip;
    union {
        XMM_SAVE_AREA32 FltSave;
        struct {
            Fp128 Header[2];
            Fp128 Legacy[8];
            Fp128 Xmm0;
            Fp128 Xmm1;
            Fp128 Xmm2;
            Fp128 Xmm3;
            Fp128 Xmm4;
            Fp128 Xmm5;
            Fp128 Xmm6;
            Fp128 Xmm7;
            Fp128 Xmm8;
            Fp128 Xmm9;
            Fp128 Xmm10;
            Fp128 Xmm11;
            Fp128 Xmm12;
            Fp128 Xmm13;
            Fp128 Xmm14;
            Fp128 Xmm15;
        } DUMMYSTRUCTNAME;
    } DUMMYUNIONNAME;
    Fp128 VectorRegister[26];
    UInt64 VectorControl;
    UInt64 DebugControl;
    UInt64 LastBranchToRip;
    UInt64 LastBranchFromRip;
    UInt64 LastExceptionToRip;
    UInt64 LastExceptionFromRip;

    void SetIP(UIntNative ip) { Rip = ip; }
    void SetSP(UIntNative sp) { Rsp = sp; }
    void SetArg0Reg(UIntNative val) { Rcx = val; }
    void SetArg1Reg(UIntNative val) { Rdx = val; }
    UIntNative GetIP() { return Rip; }
    UIntNative GetSP() { return Rsp; }
} CONTEXT, *PCONTEXT;
#elif defined(_ARM_)

#define ARM_MAX_BREAKPOINTS     8
#define ARM_MAX_WATCHPOINTS     1

typedef struct DECLSPEC_ALIGN(8) _CONTEXT {
    UInt32 ContextFlags;
    UInt32 R0;
    UInt32 R1;
    UInt32 R2;
    UInt32 R3;
    UInt32 R4;
    UInt32 R5;
    UInt32 R6;
    UInt32 R7;
    UInt32 R8;
    UInt32 R9;
    UInt32 R10;
    UInt32 R11;
    UInt32 R12;
    UInt32 Sp;
    UInt32 Lr;
    UInt32 Pc;
    UInt32 Cpsr;
    UInt32 Fpscr;
    UInt32 Padding;
    union {
        Fp128  Q[16];
        UInt64 D[32];
        UInt32 S[32];
    } DUMMYUNIONNAME;
    UInt32 Bvr[ARM_MAX_BREAKPOINTS];
    UInt32 Bcr[ARM_MAX_BREAKPOINTS];
    UInt32 Wvr[ARM_MAX_WATCHPOINTS];
    UInt32 Wcr[ARM_MAX_WATCHPOINTS];
    UInt32 Padding2[2];

    void SetIP(UIntNative ip) { Pc = ip; }
    void SetArg0Reg(UIntNative val) { R0 = val; }
    void SetArg1Reg(UIntNative val) { R1 = val; }
    UIntNative GetIP() { return Pc; }
    UIntNative GetLR() { return Lr; }
} CONTEXT, *PCONTEXT;

#elif defined(_X86_)
#define SIZE_OF_80387_REGISTERS      80
#define MAXIMUM_SUPPORTED_EXTENSION  512

typedef struct _FLOATING_SAVE_AREA {
    UInt32 ControlWord;
    UInt32 StatusWord;
    UInt32 TagWord;
    UInt32 ErrorOffset;
    UInt32 ErrorSelector;
    UInt32 DataOffset;
    UInt32 DataSelector;
    UInt8  RegisterArea[SIZE_OF_80387_REGISTERS];
    UInt32 Cr0NpxState;
} FLOATING_SAVE_AREA;

#include "pshpack4.h"
typedef struct _CONTEXT {
    UInt32 ContextFlags;
    UInt32 Dr0;
    UInt32 Dr1;
    UInt32 Dr2;
    UInt32 Dr3;
    UInt32 Dr6;
    UInt32 Dr7;
    FLOATING_SAVE_AREA FloatSave;
    UInt32 SegGs;
    UInt32 SegFs;
    UInt32 SegEs;
    UInt32 SegDs;
    UInt32 Edi;
    UInt32 Esi;
    UInt32 Ebx;
    UInt32 Edx;
    UInt32 Ecx;
    UInt32 Eax;
    UInt32 Ebp;
    UInt32 Eip;
    UInt32 SegCs;
    UInt32 EFlags;
    UInt32 Esp;
    UInt32 SegSs;
    UInt8  ExtendedRegisters[MAXIMUM_SUPPORTED_EXTENSION];

    void SetIP(UIntNative ip) { Eip = ip; }
    void SetSP(UIntNative sp) { Esp = sp; }
    void SetArg0Reg(UIntNative val) { Ecx = val; }
    void SetArg1Reg(UIntNative val) { Edx = val; }
    UIntNative GetIP() { return Eip; }
    UIntNative GetSP() { return Esp; }
} CONTEXT, *PCONTEXT;
#include "poppack.h"

#endif 


#define EXCEPTION_MAXIMUM_PARAMETERS 15 // maximum number of exception parameters

typedef struct _EXCEPTION_RECORD32 {
    UInt32      ExceptionCode;
    UInt32      ExceptionFlags;
    UIntNative  ExceptionRecord;
    UIntNative  ExceptionAddress;
    UInt32      NumberParameters;
    UIntNative  ExceptionInformation[EXCEPTION_MAXIMUM_PARAMETERS];
} EXCEPTION_RECORD, *PEXCEPTION_RECORD;

typedef struct _EXCEPTION_POINTERS {
    PEXCEPTION_RECORD   ExceptionRecord;
    PCONTEXT            ContextRecord;
} EXCEPTION_POINTERS, *PEXCEPTION_POINTERS;

typedef Int32 (__stdcall *PVECTORED_EXCEPTION_HANDLER)(
    PEXCEPTION_POINTERS ExceptionInfo
    );

#define EXCEPTION_CONTINUE_EXECUTION (-1)
#define EXCEPTION_CONTINUE_SEARCH (0)
#define EXCEPTION_EXECUTE_HANDLER (1)

typedef enum _EXCEPTION_DISPOSITION {
    ExceptionContinueExecution,
    ExceptionContinueSearch,
    ExceptionNestedException,
    ExceptionCollidedUnwind
} EXCEPTION_DISPOSITION;

#define STATUS_ACCESS_VIOLATION          ((UInt32   )0xC0000005L)    
#define STATUS_STACK_OVERFLOW            ((UInt32   )0xC00000FDL)    
#define STATUS_REDHAWK_NULL_REFERENCE    ((UInt32   )0x00000000L)    

#define NULL_AREA_SIZE                   (64*1024)

#define GetExceptionCode            _exception_code
#define GetExceptionInformation     (struct _EXCEPTION_POINTERS *)_exception_info
EXTERN_C unsigned long __cdecl _exception_code(void);
EXTERN_C void *        __cdecl _exception_info(void);

//#endif // !DACCESS_COMPILE
#endif // !_INC_WINDOWS



#ifndef DACCESS_COMPILE 
#ifndef _INC_WINDOWS

typedef UInt32 (WINAPI *PTHREAD_START_ROUTINE)(_In_opt_ void* lpThreadParameter);
typedef IntNative (WINAPI *FARPROC)();

#ifndef GCENV_INCLUDED

#define TRUE                    1
#define FALSE                   0

#define INVALID_HANDLE_VALUE    ((HANDLE)(IntNative)-1)

#endif // !GCENV_INCLUDED

#define DLL_PROCESS_ATTACH      1
#define DLL_THREAD_ATTACH       2
#define DLL_THREAD_DETACH       3
#define DLL_PROCESS_DETACH      0
#define DLL_PROCESS_VERIFIER    4

#define INFINITE                0xFFFFFFFF

#define DUPLICATE_CLOSE_SOURCE  0x00000001
#define DUPLICATE_SAME_ACCESS   0x00000002

#define GENERIC_READ            0x80000000
#define GENERIC_WRITE           0x40000000
#define GENERIC_EXECUTE         0x20000000
#define GENERIC_ALL             0x10000000

#define FILE_SHARE_READ         0x00000001
#define FILE_SHARE_WRITE        0x00000002
#define FILE_SHARE_DELETE       0x00000004

#define FILE_ATTRIBUTE_READONLY             0x00000001
#define FILE_ATTRIBUTE_HIDDEN               0x00000002
#define FILE_ATTRIBUTE_SYSTEM               0x00000004
#define FILE_ATTRIBUTE_DIRECTORY            0x00000010
#define FILE_ATTRIBUTE_ARCHIVE              0x00000020
#define FILE_ATTRIBUTE_DEVICE               0x00000040
#define FILE_ATTRIBUTE_NORMAL               0x00000080
#define FILE_ATTRIBUTE_TEMPORARY            0x00000100
#define FILE_ATTRIBUTE_SPARSE_FILE          0x00000200
#define FILE_ATTRIBUTE_REPARSE_POINT        0x00000400
#define FILE_ATTRIBUTE_COMPRESSED           0x00000800
#define FILE_ATTRIBUTE_OFFLINE              0x00001000
#define FILE_ATTRIBUTE_NOT_CONTENT_INDEXED  0x00002000
#define FILE_ATTRIBUTE_ENCRYPTED            0x00004000

#define CREATE_NEW              1
#define CREATE_ALWAYS           2
#define OPEN_EXISTING           3
#define OPEN_ALWAYS             4
#define TRUNCATE_EXISTING       5

#define FILE_BEGIN              0
#define FILE_CURRENT            1
#define FILE_END                2

#define PAGE_NOACCESS           0x01
#define PAGE_READONLY           0x02
#define PAGE_READWRITE          0x04
#define PAGE_WRITECOPY          0x08
#define PAGE_EXECUTE            0x10
#define PAGE_EXECUTE_READ       0x20
#define PAGE_EXECUTE_READWRITE  0x40
#define PAGE_EXECUTE_WRITECOPY  0x80
#define PAGE_GUARD              0x100
#define PAGE_NOCACHE            0x200
#define PAGE_WRITECOMBINE       0x400
#define MEM_COMMIT              0x1000
#define MEM_RESERVE             0x2000
#define MEM_DECOMMIT            0x4000
#define MEM_RELEASE             0x8000
#define MEM_FREE                0x10000
#define MEM_PRIVATE             0x20000
#define MEM_MAPPED              0x40000
#define MEM_RESET               0x80000
#define MEM_TOP_DOWN            0x100000
#define MEM_WRITE_WATCH         0x200000
#define MEM_PHYSICAL            0x400000
#define MEM_LARGE_PAGES         0x20000000
#define MEM_4MB_PAGES           0x80000000

#define WAIT_OBJECT_0           0
#define WAIT_TIMEOUT            258
#define WAIT_FAILED             0xFFFFFFFF

#define CREATE_SUSPENDED        0x00000004
#define THREAD_PRIORITY_NORMAL  0
#define THREAD_PRIORITY_HIGHEST 2

#define NOERROR                 0x0

#define TLS_OUT_OF_INDEXES      0xFFFFFFFF
#define TLS_NUM_INLINE_SLOTS    64

#define SUSPENDTHREAD_FAILED    0xFFFFFFFF
#define RESUMETHREAD_FAILED     0xFFFFFFFF

#define ERROR_INSUFFICIENT_BUFFER 122
#define ERROR_TIMEOUT             1460
#define ERROR_ALREADY_EXISTS      183

#define GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT    0x00000002
#define GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS          0x00000004

#endif // !_INC_WINDOWS
#endif // !DACCESS_COMPILE

typedef UInt64 REGHANDLE;
typedef UInt64 TRACEHANDLE;

#ifndef EVENT_PROVIDER_INCLUDED
struct EVENT_DATA_DESCRIPTOR
{
    UInt64  Ptr;
    UInt32  Size;
    UInt32  Reserved;
};

struct EVENT_DESCRIPTOR
{
    UInt16  Id;
    UInt8   Version;
    UInt8   Channel;
    UInt8   Level;
    UInt8   Opcode;
    UInt16  Task;
    UInt64  Keyword;

};

struct EVENT_FILTER_DESCRIPTOR
{
    UInt64  Ptr;
    UInt32  Size;
    UInt32  Type;
};

__forceinline
void
EventDataDescCreate(_Out_ EVENT_DATA_DESCRIPTOR * EventDataDescriptor, _In_opt_ const void * DataPtr, UInt32 DataSize)
{
    EventDataDescriptor->Ptr = (UInt64)DataPtr;
    EventDataDescriptor->Size = DataSize;
    EventDataDescriptor->Reserved = 0;
}
#endif // EVENT_PROVIDER_INCLUDED

#ifndef GCENV_INCLUDED
struct GCSystemInfo
{
    uint32_t dwNumberOfProcessors;
    uint32_t dwPageSize;
    uint32_t dwAllocationGranularity;
};
extern GCSystemInfo g_SystemInfo;

#define REDHAWK_PALIMPORT EXTERN_C
#define REDHAWK_PALAPI __stdcall
#endif // GCENV_INCLUDED


#ifndef DACCESS_COMPILE

#ifdef _DEBUG
#define CaptureStackBackTrace RtlCaptureStackBackTrace
#endif

#ifndef _INC_WINDOWS
// Include the list of external functions we wish to access. If we do our job 100% then it will be
// possible to link without any direct reference to any Win32 library.
#include "PalRedhawkFunctions.h"
#endif // !_INC_WINDOWS
#endif // !DACCESS_COMPILE

// The Redhawk PAL must be initialized before any of its exports can be called. Returns true for a successful
// initialization and false on failure.
REDHAWK_PALIMPORT bool REDHAWK_PALAPI PalInit();

// Given a mask of capabilities return true if all of them are supported by the current PAL.
REDHAWK_PALIMPORT bool REDHAWK_PALAPI PalHasCapability(PalCapability capability);

#ifndef APP_LOCAL_RUNTIME
// Have to define this manually because Win32 doesn't always define this API (and the automatic inclusion
// above will generate direct forwarding entries to the OS libraries on Win32). Use the capability interface
// defined above to determine whether this API is callable on your platform.
REDHAWK_PALIMPORT UInt32 REDHAWK_PALAPI PalGetCurrentProcessorNumber();
#endif

// Given the OS handle of a loaded module, compute the upper and lower virtual address bounds (inclusive).
REDHAWK_PALIMPORT void REDHAWK_PALAPI PalGetModuleBounds(HANDLE hOsHandle, _Out_ UInt8 ** ppLowerBound, _Out_ UInt8 ** ppUpperBound);

typedef struct _GUID GUID;
REDHAWK_PALIMPORT void REDHAWK_PALAPI PalGetPDBInfo(HANDLE hOsHandle, _Out_ GUID * pGuidSignature, _Out_ UInt32 * pdwAge, _Out_writes_z_(cchPath) WCHAR * wszPath, Int32 cchPath);

#ifndef APP_LOOCAL_RUNTIME
REDHAWK_PALIMPORT bool REDHAWK_PALAPI PalGetThreadContext(HANDLE hThread, _Out_ PAL_LIMITED_CONTEXT * pCtx);
#endif

REDHAWK_PALIMPORT Int32 REDHAWK_PALAPI PalGetProcessCpuCount();

REDHAWK_PALIMPORT UInt32 REDHAWK_PALAPI PalReadFileContents(_In_ const WCHAR *, _Out_ char * buff, _In_ UInt32 maxBytesToRead);

REDHAWK_PALIMPORT void REDHAWK_PALAPI PalYieldProcessor();

// Retrieves the entire range of memory dedicated to the calling thread's stack.  This does
// not get the current dynamic bounds of the stack, which can be significantly smaller than 
// the maximum bounds.
REDHAWK_PALIMPORT bool REDHAWK_PALAPI PalGetMaximumStackBounds(_Out_ void** ppStackLowOut, _Out_ void** ppStackHighOut);

// Return value:  number of characters in name string
REDHAWK_PALIMPORT Int32 PalGetModuleFileName(_Out_ wchar_t** pModuleNameOut, HANDLE moduleBase);

// Various intrinsic declarations needed for the PalGetCurrentTEB implementation below.
#if defined(_X86_)
EXTERN_C unsigned long __readfsdword(unsigned long Offset);
#pragma intrinsic(__readfsdword)
#elif defined(_AMD64_)
EXTERN_C unsigned __int64  __readgsqword(unsigned long Offset);
#pragma intrinsic(__readgsqword)
#elif defined(_ARM_)
EXTERN_C unsigned int _MoveFromCoprocessor(unsigned int, unsigned int, unsigned int, unsigned int, unsigned int);
#pragma intrinsic(_MoveFromCoprocessor)
#else
#error Unsupported architecture
#endif

// Retrieves the OS TEB for the current thread.
inline UInt8 * PalNtCurrentTeb()
{
#if defined(_X86_)
    return (UInt8*)__readfsdword(0x18); 
#elif defined(_AMD64_)
    return (UInt8*)__readgsqword(0x30);
#elif defined(_ARM_)
    return (UInt8*)_MoveFromCoprocessor(15, 0, 13,  0, 2);
#else
#error Unsupported architecture
#endif
}

// Offsets of ThreadLocalStoragePointer in the TEB.
#if defined(_X86_) || defined(_ARM_)
#define OFFSETOF__TEB__ThreadLocalStoragePointer 0x2c
#elif defined(_AMD64_)
#define OFFSETOF__TEB__ThreadLocalStoragePointer 0x58
#else
#error Unsupported architecture
#endif

//
// Compiler intrinsic definitions. In the interest of performance the PAL doesn't provide exports of these
// (that would defeat the purpose of having an intrinsic in the first place). Instead we place the necessary
// compiler linkage directly inline in this header. As a result this section may have platform specific
// conditional compilation (upto and including defining an export of functionality that isn't a supported
// intrinsic on that platform).
//

EXTERN_C void * __cdecl _alloca(size_t);
#pragma intrinsic(_alloca)

#if defined(_X86_)

#define PalYieldProcessor() __asm { rep nop }

FORCEINLINE void PalMemoryBarrier()
{
    Int32 Barrier;
    __asm {
        xchg Barrier, eax
    }
}

#elif defined(_AMD64_)

EXTERN_C void _mm_pause();
#pragma intrinsic(_mm_pause)
#define PalYieldProcessor() _mm_pause()

EXTERN_C void __faststorefence();
#pragma intrinsic(__faststorefence)
#define PalMemoryBarrier() __faststorefence()

#elif defined(_ARM_)

FORCEINLINE void PalYieldProcessor() {}

EXTERN_C void __emit(const unsigned __int32 opcode);
#pragma intrinsic(__emit)
#define PalMemoryBarrier() { __emit(0xF3BF); __emit(0x8F5F); }

#else
#error Unsupported architecture
#endif

//
// Export PAL blessed versions of *printf functions we use (mostly debug only).
//
REDHAWK_PALIMPORT void __cdecl PalPrintf(_In_z_ _Printf_format_string_ const char * szFormat, ...);
REDHAWK_PALIMPORT void __cdecl PalFlushStdout();

#ifndef DACCESS_COMPILE
REDHAWK_PALIMPORT int __cdecl PalSprintf(_Out_writes_z_(cchBuffer) char * szBuffer, size_t cchBuffer, _In_z_ _Printf_format_string_ const char * szFormat, ...);
REDHAWK_PALIMPORT int __cdecl PalVSprintf(_Out_writes_z_(cchBuffer) char * szBuffer, size_t cchBuffer, _In_z_ _Printf_format_string_ const char * szFormat, va_list args);
#else
#define PalSprintf sprintf_s
#define PalVSprintf vsprintf_s
#endif

// An annoying side-effect of enabling full compiler warnings is that it complains about constant expressions
// in control flow predicates. These happen to be useful in certain macros, such as the va_start definitions
// below. The following macros will allow the warning to be turned off for the duration of the macro expansion
// only. If this finds broader use we can consider moving them to a more global location.
#define ALLOW_CONSTANT_EXPR_BEGIN __pragma(warning(push)) __pragma(warning(disable:4127))
#define ALLOW_CONSTANT_EXPR_END __pragma(warning(pop))

struct GCMemoryStatus;
REDHAWK_PALIMPORT UInt32_BOOL REDHAWK_PALAPI PalGlobalMemoryStatusEx(_Out_ GCMemoryStatus* pBuffer);
REDHAWK_PALIMPORT _Ret_maybenull_ _Post_writable_byte_size_(size) void* REDHAWK_PALAPI PalVirtualAlloc(_In_opt_ void* pAddress, UIntNative size, UInt32 allocationType, UInt32 protect);
REDHAWK_PALIMPORT UInt32_BOOL REDHAWK_PALAPI PalVirtualFree(_In_ void* pAddress, UIntNative size, UInt32 freeType);
REDHAWK_PALIMPORT void REDHAWK_PALAPI PalSleep(UInt32 milliseconds);
REDHAWK_PALIMPORT UInt32_BOOL REDHAWK_PALAPI PalSwitchToThread();
REDHAWK_PALIMPORT HANDLE REDHAWK_PALAPI PalCreateMutexW(_In_opt_ LPSECURITY_ATTRIBUTES pMutexAttributes, UInt32_BOOL initialOwner, _In_opt_z_ LPCWSTR pName);
REDHAWK_PALIMPORT HANDLE REDHAWK_PALAPI PalCreateEventW(_In_opt_ LPSECURITY_ATTRIBUTES pEventAttributes, UInt32_BOOL manualReset, UInt32_BOOL initialState, _In_opt_z_ LPCWSTR pName);
REDHAWK_PALIMPORT UInt32 REDHAWK_PALAPI PalGetTickCount();
REDHAWK_PALIMPORT HANDLE REDHAWK_PALAPI PalCreateFileW(_In_z_ LPCWSTR pFileName, uint32_t desiredAccess, uint32_t shareMode, _In_opt_ void* pSecurityAttributes, uint32_t creationDisposition, uint32_t flagsAndAttributes, HANDLE hTemplateFile);
REDHAWK_PALIMPORT UInt32 REDHAWK_PALAPI PalGetWriteWatch(UInt32 flags, _In_ void* pBaseAddress, UIntNative regionSize, _Out_writes_to_opt_(*pCount, *pCount) void** pAddresses, _Inout_opt_ UIntNative* pCount, _Inout_opt_ UInt32* pGranularity);
REDHAWK_PALIMPORT UInt32 REDHAWK_PALAPI PalResetWriteWatch(_In_ void* pBaseAddress, UIntNative regionSize);
REDHAWK_PALIMPORT HANDLE REDHAWK_PALAPI PalCreateLowMemoryNotification();
REDHAWK_PALIMPORT void REDHAWK_PALAPI PalTerminateCurrentProcess(UInt32 exitCode);
REDHAWK_PALIMPORT HANDLE REDHAWK_PALAPI PalGetModuleHandleFromPointer(_In_ void* pointer);

#ifndef APP_LOCAL_RUNTIME
REDHAWK_PALIMPORT void* REDHAWK_PALAPI PalAddVectoredExceptionHandler(UInt32 firstHandler, _In_ PVECTORED_EXCEPTION_HANDLER vectoredHandler);
#endif

typedef UInt32 (__stdcall *BackgroundCallback)(_In_opt_ void* pCallbackContext);
REDHAWK_PALIMPORT bool REDHAWK_PALAPI PalStartBackgroundGCThread(_In_ BackgroundCallback callback, _In_opt_ void* pCallbackContext);
REDHAWK_PALIMPORT bool REDHAWK_PALAPI PalStartFinalizerThread(_In_ BackgroundCallback callback, _In_opt_ void* pCallbackContext);

typedef UInt32 (__stdcall *PalHijackCallback)(HANDLE hThread, _In_ PAL_LIMITED_CONTEXT* pThreadContext, _In_opt_ void* pCallbackContext);
REDHAWK_PALIMPORT UInt32 REDHAWK_PALAPI PalHijack(HANDLE hThread, _In_ PalHijackCallback callback, _In_opt_ void* pCallbackContext);

#ifdef FEATURE_ETW
REDHAWK_PALIMPORT bool REDHAWK_PALAPI PalEventEnabled(REGHANDLE regHandle, _In_ const EVENT_DESCRIPTOR* eventDescriptor);
#endif

void PalDebugBreak();

REDHAWK_PALIMPORT UInt32 REDHAWK_PALAPI PalGetLogicalCpuCount();
REDHAWK_PALIMPORT size_t REDHAWK_PALAPI PalGetLargestOnDieCacheSize(UInt32_BOOL bTrueSize);

REDHAWK_PALIMPORT _Ret_maybenull_ void* REDHAWK_PALAPI PalSetWerDataBuffer(_In_ void* pNewBuffer);

REDHAWK_PALIMPORT UInt32_BOOL REDHAWK_PALAPI PalAllocateThunksFromTemplate(_In_ HANDLE hTemplateModule, UInt32 templateRva, size_t templateSize, _Outptr_result_bytebuffer_(templateSize) void** newThunksOut);

REDHAWK_PALIMPORT UInt32 REDHAWK_PALAPI PalCompatibleWaitAny(UInt32_BOOL alertable, UInt32 timeout, UInt32 count, HANDLE* pHandles, UInt32_BOOL allowReentrantWait);

#ifndef _MSC_VER
REDHAWK_PALIMPORT Int32 __cdecl _wcsicmp(const wchar_t *string1, const wchar_t *string2);
#endif // _MSC_VER

#include "PalRedhawkInline.h"

#endif // !PAL_REDHAWK_INCLUDED
