//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

//
// Implementation of the Redhawk Platform Abstraction Layer (PAL) library when MinWin is the platform. In this
// case most or all of the import requirements which Redhawk has can be satisfied via a forwarding export to
// some native MinWin library. Therefore most of the work is done in the .def file and there is very little
// code here.
//
// Note that in general we don't want to assume that Windows and Redhawk global definitions can co-exist.
// Since this code must include Windows headers to do its job we can't therefore safely include general
// Redhawk header files.
//

#include <banned.h>
#include <stdio.h>
#include <errno.h>
#include <cwchar>
#include "CommonTypes.h"
#include "daccess.h"
#include <PalRedhawkCommon.h>
#include "CommonMacros.h"
#include "assert.h"
#include "config.h"

#include <unistd.h>
#include <sched.h>
#include <sys/mman.h>
#include <pthread.h>
#include <sys/types.h>

#if HAVE_SYSCTL
#include <sys/sysctl.h>
#endif

#if HAVE_SYS_VMPARAM_H
#include <sys/vmparam.h>
#endif  // HAVE_SYS_VMPARAM_H

#if HAVE_MACH_VM_TYPES_H
#include <mach/vm_types.h>
#endif // HAVE_MACH_VM_TYPES_H

#if HAVE_MACH_VM_PARAM_H
#include <mach/vm_param.h>
#endif  // HAVE_MACH_VM_PARAM_H

#ifdef __APPLE__
#include <mach/vm_statistics.h>
#include <mach/mach_types.h>
#include <mach/mach_init.h>
#include <mach/mach_host.h>
#include <mach/mach_port.h>
#endif // __APPLE__

#ifdef USE_PORTABLE_HELPERS
#define assert(expr) ASSERT(expr)
#endif

#define REDHAWK_PALEXPORT extern "C"
#define REDHAWK_PALAPI

#ifndef __APPLE__
#if HAVE_SYSCONF && HAVE__SC_AVPHYS_PAGES
#define SYSCONF_PAGES _SC_AVPHYS_PAGES
#elif HAVE_SYSCONF && HAVE__SC_PHYS_PAGES
#define SYSCONF_PAGES _SC_PHYS_PAGES
#else
#error Dont know how to get page-size on this architecture!
#endif
#endif // __APPLE__

// TODO: what to do with these?
///////////////////////////////
struct MEMORYSTATUSEX
{
  uint32_t  dwLength;
  uint32_t  dwMemoryLoad;
  uint64_t ullTotalPhys;
  uint64_t ullAvailPhys;
  uint64_t ullTotalPageFile;
  uint64_t ullAvailPageFile;
  uint64_t ullTotalVirtual;
  uint64_t ullAvailVirtual;
  uint64_t ullAvailExtendedVirtual;
};

typedef void * LPSECURITY_ATTRIBUTES;

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

///////////////////////////

extern "C" UInt32 __stdcall NtGetCurrentProcessorNumber();

static uint32_t g_dwPALCapabilities;

extern bool PalQueryProcessorTopology();
REDHAWK_PALEXPORT void __cdecl PalPrintf(_In_z_ _Printf_format_string_ const char * szFormat, ...);

// The Redhawk PAL must be initialized before any of its exports can be called. Returns true for a successful
// initialization and false on failure.
REDHAWK_PALEXPORT bool REDHAWK_PALAPI PalInit()
{
    g_dwPALCapabilities = GetCurrentProcessorNumberCapability;

    if (!PalQueryProcessorTopology())
        return false;

    return true;
}

// Given a mask of capabilities return true if all of them are supported by the current PAL.
REDHAWK_PALEXPORT bool REDHAWK_PALAPI PalHasCapability(PalCapability capability)
{
    return (g_dwPALCapabilities & (uint32_t)capability) == (uint32_t)capability;
}

REDHAWK_PALEXPORT unsigned int REDHAWK_PALAPI PalGetCurrentProcessorNumber()
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

REDHAWK_PALEXPORT UInt32_BOOL REDHAWK_PALAPI PalAllocateThunksFromTemplate(HANDLE hTemplateModule, uint32_t templateRva, size_t templateSize, void** newThunksOut)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

REDHAWK_PALEXPORT uint32_t REDHAWK_PALAPI PalCompatibleWaitAny(UInt32_BOOL alertable, uint32_t timeout, uint32_t handleCount, HANDLE* pHandles, UInt32_BOOL allowReentrantWait)
{
    // UNIXTODO: Implement this function
    return WAIT_OBJECT_0;
}

REDHAWK_PALEXPORT UInt32_BOOL REDHAWK_PALAPI PalGlobalMemoryStatusEx(_Inout_ MEMORYSTATUSEX* pBuffer)
{
    assert(pBuffer->dwLength == sizeof(MEMORYSTATUSEX));

    pBuffer->dwMemoryLoad = 0;
    pBuffer->ullTotalPhys = 0;
    pBuffer->ullAvailPhys = 0;
    pBuffer->ullTotalPageFile = 0;
    pBuffer->ullAvailPageFile = 0;
    pBuffer->ullTotalVirtual = 0;
    pBuffer->ullAvailVirtual = 0;
    pBuffer->ullAvailExtendedVirtual = 0;

    UInt32_BOOL fRetVal = UInt32_FALSE;

    // Get the physical memory size
#if HAVE_SYSCONF && HAVE__SC_PHYS_PAGES
    int64_t physical_memory;

    // Get the Physical memory size
    physical_memory = sysconf( _SC_PHYS_PAGES ) * sysconf( _SC_PAGE_SIZE );
    pBuffer->ullTotalPhys = (uint64_t)physical_memory;
    fRetVal = UInt32_TRUE;
#elif HAVE_SYSCTL
    int mib[2];
    int64_t physical_memory;
    size_t length;

    // Get the Physical memory size
    mib[0] = CTL_HW;
    mib[1] = HW_MEMSIZE;
    length = sizeof(int64_t);
    int rc = sysctl(mib, 2, &physical_memory, &length, NULL, 0);
    if (rc != 0)
    {
        ASSERT_UNCONDITIONALLY("sysctl failed for HW_MEMSIZE\n");
    }
    else
    {
        pBuffer->ullTotalPhys = (uint64_t)physical_memory;
        fRetVal = UInt32_TRUE;
    }
#elif // HAVE_SYSINFO
    // TODO: implement getting memory details via sysinfo. On Linux, it provides swap file details that
    // we can use to fill in the xxxPageFile members.

#endif // HAVE_SYSCONF

    // Get the physical memory in use - from it, we can get the physical memory available.
    // We do this only when we have the total physical memory available.
    if (pBuffer->ullTotalPhys > 0)
    {
#ifndef __APPLE__
        pBuffer->ullAvailPhys = sysconf(SYSCONF_PAGES) * sysconf(_SC_PAGE_SIZE);
        int64_t used_memory = pBuffer->ullTotalPhys - pBuffer->ullAvailPhys;
        pBuffer->dwMemoryLoad = (uint32_t)((used_memory * 100) / pBuffer->ullTotalPhys);
#else
        vm_size_t page_size;
        mach_port_t mach_port;
        mach_msg_type_number_t count;
        vm_statistics_data_t vm_stats;
        mach_port = mach_host_self();
        count = sizeof(vm_stats) / sizeof(natural_t);
        if (KERN_SUCCESS == host_page_size(mach_port, &page_size))
        {
            if (KERN_SUCCESS == host_statistics(mach_port, HOST_VM_INFO, (host_info_t)&vm_stats, &count))
            {
                pBuffer->ullAvailPhys = (int64_t)vm_stats.free_count * (int64_t)page_size;
                int64_t used_memory = ((int64_t)vm_stats.active_count + (int64_t)vm_stats.inactive_count + (int64_t)vm_stats.wire_count) *  (int64_t)page_size;
                pBuffer->dwMemoryLoad = (uint32_t)((used_memory * 100) / pBuffer->ullTotalPhys);
            }
        }
        mach_port_deallocate(mach_task_self(), mach_port);
#endif // __APPLE__
    }

    // There is no API to get the total virtual address space size on 
    // Unix, so we use a constant value representing 128TB, which is 
    // the approximate size of total user virtual address space on
    // the currently supported Unix systems.
    static const uint64_t _128TB = (1ull << 47); 
    pBuffer->ullTotalVirtual = _128TB;
    pBuffer->ullAvailVirtual = pBuffer->ullAvailPhys;

    return fRetVal;
}

REDHAWK_PALEXPORT void REDHAWK_PALAPI PalSleep(uint32_t milliseconds)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

REDHAWK_PALEXPORT UInt32_BOOL REDHAWK_PALAPI __stdcall PalSwitchToThread()
{
    // sched_yield yields to another thread in the current process. This implementation 
    // won't work well for cross-process synchronization.
    return sched_yield() == 0;
}

REDHAWK_PALEXPORT HANDLE REDHAWK_PALAPI PalCreateMutexW(_In_opt_ LPSECURITY_ATTRIBUTES pMutexAttributes, UInt32_BOOL initialOwner, _In_opt_z_ const wchar_t* pName)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

REDHAWK_PALEXPORT HANDLE REDHAWK_PALAPI PalCreateEventW(_In_opt_ LPSECURITY_ATTRIBUTES pEventAttributes, UInt32_BOOL manualReset, UInt32_BOOL initialState, _In_opt_z_ const wchar_t* pName)
{
    // UNIXTODO: Implement this function
    return (HANDLE)1;
}

REDHAWK_PALEXPORT _Success_(return) bool REDHAWK_PALAPI PalGetThreadContext(HANDLE hThread, _Out_ PAL_LIMITED_CONTEXT * pCtx)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

typedef UInt32(__stdcall *BackgroundCallback)(_In_opt_ void* pCallbackContext);

REDHAWK_PALEXPORT HANDLE REDHAWK_PALAPI PalStartBackgroundWork(_In_ BackgroundCallback callback, _In_opt_ void* pCallbackContext, UInt32_BOOL highPriority)
{
    // UNIXTODO: Implement this function
    return NULL;
}

REDHAWK_PALEXPORT UInt32_BOOL REDHAWK_PALAPI PalStartBackgroundGCThread(_In_ BackgroundCallback callback, _In_opt_ void* pCallbackContext)
{
    return PalStartBackgroundWork(callback, pCallbackContext, UInt32_FALSE) != NULL;
}

REDHAWK_PALEXPORT UInt32_BOOL REDHAWK_PALAPI PalStartFinalizerThread(_In_ BackgroundCallback callback, _In_opt_ void* pCallbackContext)
{
    return PalStartBackgroundWork(callback, pCallbackContext, UInt32_TRUE) != NULL;
}

REDHAWK_PALEXPORT uint32_t REDHAWK_PALAPI PalGetTickCount()
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

#if 0
REDHAWK_PALEXPORT UInt32_BOOL REDHAWK_PALAPI PalEventEnabled(REGHANDLE regHandle, _In_ const EVENT_DESCRIPTOR* eventDescriptor)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}
#endif

REDHAWK_PALEXPORT HANDLE REDHAWK_PALAPI PalCreateFileW(_In_z_ wchar_t pFileName, uint32_t desiredAccess, uint32_t shareMode, _In_opt_ LPSECURITY_ATTRIBUTES pSecurityAttributes, uint32_t creationDisposition, uint32_t flagsAndAttributes, HANDLE hTemplateFile)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

REDHAWK_PALEXPORT _Success_(return == 0)
uint32_t REDHAWK_PALAPI PalGetWriteWatch(_In_ uint32_t flags, _In_ void* pBaseAddress, _In_ size_t regionSize, _Out_writes_to_opt_(*pCount, *pCount) void** pAddresses, _Inout_opt_ uintptr_t* pCount, _Out_opt_ uint32_t* pGranularity)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

REDHAWK_PALEXPORT uint32_t REDHAWK_PALAPI PalResetWriteWatch(_In_ void* pBaseAddress, size_t regionSize)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

REDHAWK_PALEXPORT HANDLE REDHAWK_PALAPI PalCreateLowMemoryNotification()
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

REDHAWK_PALEXPORT HANDLE REDHAWK_PALAPI PalGetModuleHandleFromPointer(_In_ void* pointer)
{
    // UNIXTODO: Implement this function
    return (HANDLE)1;
}

REDHAWK_PALEXPORT void* REDHAWK_PALAPI PalAddVectoredExceptionHandler(uint32_t firstHandler, _In_ PVECTORED_EXCEPTION_HANDLER vectoredHandler)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

//
// -----------------------------------------------------------------------------------------------------------
//
// Some more globally initialized data (in InitializeSubsystems), this time internal and used to cache
// information returned by various GC support routines.
//
static UInt32 g_cLogicalCpus = 0;
static size_t g_cbLargestOnDieCache = 0;
static size_t g_cbLargestOnDieCacheAdjusted = 0;



#if (defined(TARGET_AMD64) || defined (TARGET_X86)) && !defined(USE_PORTABLE_HELPERS)

uint32_t getcpuid(uint32_t arg, unsigned char result[16])
{
    uint32_t eax;
    __asm("  xor %%ecx, %%ecx\n" \
          "  cpuid\n" \
          "  mov %%eax, 0(%[result])\n" \
          "  mov %%ebx, 4(%[result])\n" \
          "  mov %%ecx, 8(%[result])\n" \
          "  mov %%edx, 12(%[result])\n" \
        : "=a"(eax) /*output in eax*/\
        : "a"(arg), [result]"r"(result) /*inputs - arg in eax, result in any register*/\
        : "eax", "rbx", "ecx", "edx" /* registers that are clobbered*/
      );
    return eax;
}

uint32_t getextcpuid(uint32_t arg1, uint32_t arg2, unsigned char result[16])
{
    uint32_t eax;
    __asm("  cpuid\n" \
          "  mov %%eax, 0(%[result])\n" \
          "  mov %%ebx, 4(%[result])\n" \
          "  mov %%ecx, 8(%[result])\n" \
          "  mov %%edx, 12(%[result])\n" \
        : "=a"(eax) /*output in eax*/\
        : "c"(arg1), "a"(arg2), [result]"r"(result) /*inputs - arg1 in ecx, arg2 in eax, result in any register*/\
        : "eax", "rbx", "ecx", "edx" /* registers that are clobbered*/
      );
    return eax;
}

void QueryAMDCacheInfo(_Out_ UInt32* pcbCache, _Out_ UInt32* pcbCacheAdjusted)
{
    unsigned char buffer[16];

    if (getcpuid(0x80000000, buffer) >= 0x80000006)
    {
        UInt32* pdwBuffer = (UInt32*)buffer;

        getcpuid(0x80000006, buffer);

        UInt32 dwL2CacheBits = pdwBuffer[2];
        UInt32 dwL3CacheBits = pdwBuffer[3];

        *pcbCache = (size_t)((dwL2CacheBits >> 16) * 1024);    // L2 cache size in ECX bits 31-16

        getcpuid(0x1, buffer);
        UInt32 dwBaseFamily = (pdwBuffer[0] & (0xF << 8)) >> 8;
        UInt32 dwExtFamily = (pdwBuffer[0] & (0xFF << 20)) >> 20;
        UInt32 dwFamily = dwBaseFamily >= 0xF ? dwBaseFamily + dwExtFamily : dwBaseFamily;

        if (dwFamily >= 0x10)
        {
            UInt32_BOOL bSkipAMDL3 = UInt32_FALSE;

            if (dwFamily == 0x10)   // are we running on a Barcelona (Family 10h) processor?
            {
                // check model
                UInt32 dwBaseModel = (pdwBuffer[0] & (0xF << 4)) >> 4;
                UInt32 dwExtModel = (pdwBuffer[0] & (0xF << 16)) >> 16;
                UInt32 dwModel = dwBaseFamily >= 0xF ? (dwExtModel << 4) | dwBaseModel : dwBaseModel;

                switch (dwModel)
                {
                case 0x2:
                    // 65nm parts do not benefit from larger Gen0
                    bSkipAMDL3 = UInt32_TRUE;
                    break;

                case 0x4:
                default:
                    bSkipAMDL3 = UInt32_FALSE;
                }
            }

            if (!bSkipAMDL3)
            {
                // 45nm Greyhound parts (and future parts based on newer northbridge) benefit
                // from increased gen0 size, taking L3 into account
                getcpuid(0x80000008, buffer);
                UInt32 dwNumberOfCores = (pdwBuffer[2] & (0xFF)) + 1;       // NC is in ECX bits 7-0

                UInt32 dwL3CacheSize = (size_t)((dwL3CacheBits >> 18) * 512 * 1024);  // L3 size in EDX bits 31-18 * 512KB
                                                                                      // L3 is shared between cores
                dwL3CacheSize = dwL3CacheSize / dwNumberOfCores;
                *pcbCache += dwL3CacheSize;       // due to exclusive caches, add L3 size (possibly zero) to L2
                                                  // L1 is too small to worry about, so ignore it
            }
        }
    }
    *pcbCacheAdjusted = *pcbCache;
}

#ifdef _DEBUG
#define CACHE_WAY_BITS          0xFFC00000      // number of cache WAYS-Associativity is returned in EBX[31:22] (10 bits) using cpuid function 4
#define CACHE_PARTITION_BITS    0x003FF000      // number of cache Physical Partitions is returned in EBX[21:12] (10 bits) using cpuid function 4
#define CACHE_LINESIZE_BITS     0x00000FFF      // Linesize returned in EBX[11:0] (12 bits) using cpuid function 4
#define LIMITED_METHOD_CONTRACT 

size_t CLR_GetIntelDeterministicCacheEnum()
{
    LIMITED_METHOD_CONTRACT;
    size_t retVal = 0;
    unsigned char buffer[16];

    uint32_t maxCpuid = getextcpuid(0, 0, buffer);

    uint32_t* dwBuffer = (uint32_t*)buffer;

    if ((maxCpuid > 3) && (maxCpuid < 0x80000000)) // Deterministic Cache Enum is Supported
    {
        uint32_t dwCacheWays, dwCachePartitions, dwLineSize, dwSets;
        uint32_t retEAX = 0;
        uint32_t loopECX = 0;
        size_t maxSize = 0;
        size_t curSize = 0;

        // Make First call  to getextcpuid with loopECX=0. loopECX provides an index indicating which level to return information about.
        // The second parameter is input EAX=4, to specify we want deterministic cache parameter leaf information. 
        // getextcpuid with EAX=4 should be executed with loopECX = 0,1, ... until retEAX [4:0] contains 00000b, indicating no more
        // cache levels are supported.

        getextcpuid(loopECX, 4, buffer);
        retEAX = dwBuffer[0];       // get EAX

        int i = 0;
        while (retEAX & 0x1f)       // Crack cache enums and loop while EAX > 0
        {

            dwCacheWays = (dwBuffer[1] & CACHE_WAY_BITS) >> 22;
            dwCachePartitions = (dwBuffer[1] & CACHE_PARTITION_BITS) >> 12;
            dwLineSize = dwBuffer[1] & CACHE_LINESIZE_BITS;
            dwSets = dwBuffer[2];    // ECX

            curSize = (dwCacheWays + 1)*(dwCachePartitions + 1)*(dwLineSize + 1)*(dwSets + 1);

            if (maxSize < curSize)
                maxSize = curSize;

            loopECX++;
            getextcpuid(loopECX, 4, buffer);
            retEAX = dwBuffer[0];      // get EAX[4:0];        
            i++;
            if (i > 16)                // prevent infinite looping
                return 0;
        }
        retVal = maxSize;
    }

    return retVal;
}

// The following function uses CPUID function 2 with descriptor values to determine the cache size.  This requires a-priori 
// knowledge of the descriptor values. This works on gallatin and prior processors (already released processors).
// If successful, this function returns the cache size in bytes of the highest level on-die cache. Returns 0 on failure.

size_t CLR_GetIntelDescriptorValuesCache()
{
    LIMITED_METHOD_CONTRACT;
    size_t size = 0;
    size_t maxSize = 0;
    unsigned char buffer[16];

    getextcpuid(0, 2, buffer);         // call CPUID with EAX function 2H to obtain cache descriptor values 

    for (int i = buffer[0]; --i >= 0;)
    {
        int j;
        for (j = 3; j < 16; j += 4)
        {
            // if the information in a register is marked invalid, set to null descriptors
            if (buffer[j] & 0x80)
            {
                buffer[j - 3] = 0;
                buffer[j - 2] = 0;
                buffer[j - 1] = 0;
                buffer[j - 0] = 0;
            }
        }

        for (j = 1; j < 16; j++)
        {
            switch (buffer[j])    // need to add descriptor values for 8M and 12M when they become known
            {
            case    0x41:
            case    0x79:
                size = 128 * 1024;
                break;

            case    0x42:
            case    0x7A:
            case    0x82:
                size = 256 * 1024;
                break;

            case    0x22:
            case    0x43:
            case    0x7B:
            case    0x83:
            case    0x86:
                size = 512 * 1024;
                break;

            case    0x23:
            case    0x44:
            case    0x7C:
            case    0x84:
            case    0x87:
                size = 1024 * 1024;
                break;

            case    0x25:
            case    0x45:
            case    0x85:
                size = 2 * 1024 * 1024;
                break;

            case    0x29:
                size = 4 * 1024 * 1024;
                break;
            }
            if (maxSize < size)
                maxSize = size;
        }

        if (i > 0)
            getextcpuid(0, 2, buffer);
    }
    return     maxSize;
}

size_t CLR_GetLargestOnDieCacheSizeX86(UInt32_BOOL bTrueSize)
{

    static size_t maxSize;
    static size_t maxTrueSize;

    if (maxSize)
    {
        // maxSize and maxTrueSize cached
        if (bTrueSize)
        {
            return maxTrueSize;
        }
        else
        {
            return maxSize;
        }
    }

    __try
    {
        unsigned char buffer[16];
        uint32_t* dwBuffer = (uint32_t*)buffer;

        uint32_t maxCpuId = getcpuid(0, buffer);

        if (dwBuffer[1] == 'uneG')
        {
            if (dwBuffer[3] == 'Ieni')
            {
                if (dwBuffer[2] == 'letn')
                {
                    size_t tempSize = 0;
                    if (maxCpuId >= 2)         // cpuid support for cache size determination is available
                    {
                        tempSize = CLR_GetIntelDeterministicCacheEnum();          // try to use use deterministic cache size enumeration
                        if (!tempSize)
                        {                    // deterministic enumeration failed, fallback to legacy enumeration using descriptor values            
                            tempSize = CLR_GetIntelDescriptorValuesCache();
                        }
                    }

                    // update maxSize once with final value
                    maxTrueSize = tempSize;

#ifdef _WIN64
                    if (maxCpuId >= 2)
                    {
                        // If we're running on a Prescott or greater core, EM64T tests
                        // show that starting with a gen0 larger than LLC improves performance.
                        // Thus, start with a gen0 size that is larger than the cache.  The value of
                        // 3 is a reasonable tradeoff between workingset and performance.
                        maxSize = maxTrueSize * 3;
                    }
                    else
#endif
                    {
                        maxSize = maxTrueSize;
                    }
                }
            }
        }

        if (dwBuffer[1] == 'htuA') {
            if (dwBuffer[3] == 'itne') {
                if (dwBuffer[2] == 'DMAc') {

                    if (getcpuid(0x80000000, buffer) >= 0x80000006)
                    {
                        getcpuid(0x80000006, buffer);

                        uint32_t dwL2CacheBits = dwBuffer[2];
                        uint32_t dwL3CacheBits = dwBuffer[3];

                        maxTrueSize = (size_t)((dwL2CacheBits >> 16) * 1024);    // L2 cache size in ECX bits 31-16

                        getcpuid(0x1, buffer);
                        uint32_t dwBaseFamily = (dwBuffer[0] & (0xF << 8)) >> 8;
                        uint32_t dwExtFamily = (dwBuffer[0] & (0xFF << 20)) >> 20;
                        uint32_t dwFamily = dwBaseFamily >= 0xF ? dwBaseFamily + dwExtFamily : dwBaseFamily;

                        if (dwFamily >= 0x10)
                        {
                            UInt32_BOOL bSkipAMDL3 = UInt32_FALSE;

                            if (dwFamily == 0x10)   // are we running on a Barcelona (Family 10h) processor?
                            {
                                // check model
                                uint32_t dwBaseModel = (dwBuffer[0] & (0xF << 4)) >> 4;
                                uint32_t dwExtModel = (dwBuffer[0] & (0xF << 16)) >> 16;
                                uint32_t dwModel = dwBaseFamily >= 0xF ? (dwExtModel << 4) | dwBaseModel : dwBaseModel;

                                switch (dwModel)
                                {
                                case 0x2:
                                    // 65nm parts do not benefit from larger Gen0
                                    bSkipAMDL3 = UInt32_TRUE;
                                    break;

                                case 0x4:
                                default:
                                    bSkipAMDL3 = UInt32_FALSE;
                                }
                            }

                            if (!bSkipAMDL3)
                            {
                                // 45nm Greyhound parts (and future parts based on newer northbridge) benefit
                                // from increased gen0 size, taking L3 into account
                                getcpuid(0x80000008, buffer);
                                uint32_t dwNumberOfCores = (dwBuffer[2] & (0xFF)) + 1;        // NC is in ECX bits 7-0

                                uint32_t dwL3CacheSize = (size_t)((dwL3CacheBits >> 18) * 512 * 1024);  // L3 size in EDX bits 31-18 * 512KB
                                                                                                     // L3 is shared between cores
                                dwL3CacheSize = dwL3CacheSize / dwNumberOfCores;
                                maxTrueSize += dwL3CacheSize;       // due to exclusive caches, add L3 size (possibly zero) to L2
                                                                    // L1 is too small to worry about, so ignore it
                            }
                        }


                        maxSize = maxTrueSize;
                    }
                }
            }
        }
    }
    __except (1)
    {
    }

    if (bTrueSize)
        return maxTrueSize;
    else
        return maxSize;
}

uint32_t CLR_GetLogicalCpuCountFromOS(_In_reads_opt_(nEntries) SYSTEM_LOGICAL_PROCESSOR_INFORMATION * pslpi, uint32_t nEntries);

// This function returns the number of logical processors on a given physical chip.  If it cannot
// determine the number of logical cpus, or the machine is not populated uniformly with the same
// type of processors, this function returns 1.
uint32_t CLR_GetLogicalCpuCountX86(_In_reads_opt_(nEntries) SYSTEM_LOGICAL_PROCESSOR_INFORMATION * pslpi, uint32_t nEntries)
{
    // No CONTRACT possible because GetLogicalCpuCount uses SEH

    static uint32_t val = 0;

    // cache value for later re-use
    if (val)
    {
        return val;
    }

    uint32_t retVal = 1;

    __try
    {
        unsigned char buffer[16];

        uint32_t maxCpuId = getcpuid(0, buffer);

        if (maxCpuId < 1)
            goto lDone;

        uint32_t* dwBuffer = (uint32_t*)buffer;

        if (dwBuffer[1] == 'uneG') {
            if (dwBuffer[3] == 'Ieni') {
                if (dwBuffer[2] == 'letn') {  // get SMT/multicore enumeration for Intel EM64T 

                                              // TODO: Currently GetLogicalCpuCountFromOS() and GetLogicalCpuCountFallback() are broken on 
                                              // multi-core processor, but we never call into those two functions since we don't halve the
                                              // gen0size when it's prescott and above processor. We keep the old version here for earlier
                                              // generation system(Northwood based), perf data suggests on those systems, halve gen0 size 
                                              // still boost the performance(ex:Biztalk boosts about 17%). So on earlier systems(Northwood) 
                                              // based, we still go ahead and halve gen0 size.  The logic in GetLogicalCpuCountFromOS() 
                                              // and GetLogicalCpuCountFallback() works fine for those earlier generation systems. 
                                              // If it's a Prescott and above processor or Multi-core, perf data suggests not to halve gen0 
                                              // size at all gives us overall better performance. 
                                              // This is going to be fixed with a new version in orcas time frame. 

                    if ((maxCpuId > 3) && (maxCpuId < 0x80000000))
                        goto lDone;

                    val = CLR_GetLogicalCpuCountFromOS(pslpi, nEntries); //try to obtain HT enumeration from OS API
                    if (val)
                    {
                        retVal = val;     // OS API HT enumeration successful, we are Done        
                        goto lDone;
                    }

                    // val = GetLogicalCpuCountFallback();    // OS API failed, Fallback to HT enumeration using CPUID
                    // if( val )
                    //     retVal = val;
                }
            }
        }
    lDone:;
    }
    __except (1)
    {
    }

    if (val == 0)
    {
        val = retVal;
    }

    return retVal;
}

#endif // _DEBUG
#endif // (defined(TARGET_AMD64) || defined (TARGET_X86)) && !defined(USE_PORTABLE_HELPERS)


#ifdef _DEBUG
uint32_t CLR_GetLogicalCpuCountFromOS(_In_reads_opt_(nEntries) SYSTEM_LOGICAL_PROCESSOR_INFORMATION * pslpi, uint32_t nEntries)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

// This function returns the size of highest level cache on the physical chip.   If it cannot
// determine the cachesize this function returns 0.
size_t CLR_GetLogicalProcessorCacheSizeFromOS(_In_reads_opt_(nEntries) SYSTEM_LOGICAL_PROCESSOR_INFORMATION * pslpi, uint32_t nEntries)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

uint32_t CLR_GetLogicalCpuCount(_In_reads_opt_(nEntries) SYSTEM_LOGICAL_PROCESSOR_INFORMATION * pslpi, uint32_t nEntries)
{
#if (defined(TARGET_AMD64) || defined (TARGET_X86)) && !defined(USE_PORTABLE_HELPERS)
    return CLR_GetLogicalCpuCountX86(pslpi, nEntries);
#else
    return CLR_GetLogicalCpuCountFromOS(pslpi, nEntries);
#endif
}

size_t CLR_GetLargestOnDieCacheSize(UInt32_BOOL bTrueSize, _In_reads_opt_(nEntries) SYSTEM_LOGICAL_PROCESSOR_INFORMATION * pslpi, uint32_t nEntries)
{
#if (defined(TARGET_AMD64) || defined (TARGET_X86)) && !defined(USE_PORTABLE_HELPERS)
    return CLR_GetLargestOnDieCacheSizeX86(bTrueSize);
#else
    return CLR_GetLogicalProcessorCacheSizeFromOS(pslpi, nEntries);
#endif
}
#endif // _DEBUG



enum CpuVendor
{
    CpuUnknown,
    CpuIntel,
    CpuAMD,
};

CpuVendor GetCpuVendor(_Out_ UInt32* puMaxCpuId)
{
#if (defined(TARGET_AMD64) || defined (TARGET_X86)) && !defined(USE_PORTABLE_HELPERS)
    unsigned char buffer[16];
    *puMaxCpuId = getcpuid(0, buffer);

    UInt32* pdwBuffer = (UInt32*)buffer;

    if (pdwBuffer[1] == 'uneG'
        && pdwBuffer[3] == 'Ieni'
        && pdwBuffer[2] == 'letn')
    {
        return CpuIntel;
    }
    else if (pdwBuffer[1] == 'htuA'
        && pdwBuffer[3] == 'itne'
        && pdwBuffer[2] == 'DMAc')
    {
        return CpuAMD;
    }
#else
    *puMaxCpuId = 0;
#endif
    return CpuUnknown;
}

// Count set bits in a bitfield.
UInt32 CountBits(size_t bfBitfield)
{
    UInt32 cBits = 0;

    // This is not the fastest algorithm possible but it's simple and the performance is not critical.
    for (UInt32 i = 0; i < (sizeof(size_t) * 8); i++)
    {
        cBits += (bfBitfield & 1) ? 1 : 0;
        bfBitfield >>= 1;
    }

    return cBits;
}

//
// Enable TRACE_CACHE_TOPOLOGY to get a dump of the info provided by the OS as well as a comparison of the
// 'answers' between the current implementation and the CLR implementation.
//
//#define TRACE_CACHE_TOPOLOGY
#ifdef _DEBUG
void DumpCacheTopology(_In_reads_(cRecords) SYSTEM_LOGICAL_PROCESSOR_INFORMATION * pProcInfos, UInt32 cRecords)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}
void DumpCacheTopologyResults(UInt32 maxCpuId, CpuVendor cpuVendor, _In_reads_(cRecords) SYSTEM_LOGICAL_PROCESSOR_INFORMATION * pProcInfos, UInt32 cRecords)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}
#endif // _DEBUG

// Method used to initialize the above values.
bool PalQueryProcessorTopology()
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

// Functions called by the GC to obtain our cached values for number of logical processors and cache size.
REDHAWK_PALEXPORT uint32_t REDHAWK_PALAPI PalGetLogicalCpuCount()
{
    return g_cLogicalCpus;
}

REDHAWK_PALEXPORT size_t REDHAWK_PALAPI PalGetLargestOnDieCacheSize(UInt32_BOOL bTrueSize)
{
    return bTrueSize ? g_cbLargestOnDieCache
        : g_cbLargestOnDieCacheAdjusted;
}

static int W32toUnixAccessControl(uint32_t flProtect)
{
    int prot = 0;

    switch (flProtect & 0xff)
    {
    case PAGE_NOACCESS:
        prot = PROT_NONE;
        break;
    case PAGE_READWRITE:
        prot = PROT_READ | PROT_WRITE;
        break;
    default:
        ASSERT(false);
        break;
    }
    return prot;
}

REDHAWK_PALEXPORT _Ret_maybenull_ _Post_writable_byte_size_(size) void* REDHAWK_PALAPI PalVirtualAlloc(_In_opt_ void* pAddress, size_t size, uint32_t allocationType, uint32_t protect)
{
    // TODO: thread safety!

    if ((allocationType & ~(MEM_RESERVE | MEM_COMMIT)) != 0)
    {
        // TODO: Implement
        return NULL;
    }

    ASSERT(((size_t)pAddress & (OS_PAGE_SIZE - 1)) == 0);

    // Align size to whole pages
    size = (size + (OS_PAGE_SIZE - 1)) & ~(OS_PAGE_SIZE - 1);
    int unixProtect = W32toUnixAccessControl(protect);
    
    if (allocationType & MEM_RESERVE)
    {
        void * pRetVal = mmap(pAddress, size, unixProtect, MAP_ANON | MAP_PRIVATE, -1, 0);
         
        return pRetVal;
    }

    if (allocationType & MEM_COMMIT)
    {
        int ret = mprotect(pAddress, size, unixProtect);
        return (ret == 0) ? pAddress : NULL;
    }

    return NULL;
}

REDHAWK_PALEXPORT UInt32_BOOL REDHAWK_PALAPI PalVirtualFree(_In_ void* pAddress, size_t size, uint32_t freeType)
{
    assert(((freeType & MEM_RELEASE) != MEM_RELEASE) || size == 0);
    assert((freeType & (MEM_RELEASE | MEM_DECOMMIT)) != (MEM_RELEASE | MEM_DECOMMIT));
    assert(freeType != 0);

    // UNIXTODO: Implement this function
    return UInt32_TRUE;
}

REDHAWK_PALEXPORT _Ret_maybenull_ void* REDHAWK_PALAPI PalSetWerDataBuffer(_In_ void* pNewBuffer)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

extern "C" UInt32 _InterlockedOr(_Inout_ _Interlocked_operand_ UInt32 volatile *pDst, UInt32 iValue)
{
    return __sync_fetch_and_or(pDst, iValue);
}

extern "C" UInt32 _InterlockedAnd(_Inout_ _Interlocked_operand_ UInt32 volatile *pDst, UInt32 iValue)
{
    return __sync_fetch_and_and(pDst, iValue);
}

extern "C" HANDLE GetCurrentProcess()
{
    // UNIXTODO: Implement this function;
    return (HANDLE)1;
}

extern "C" HANDLE GetCurrentThread()
{
    // UNIXTODO: Implement this function;
    return (HANDLE)1;
}

extern "C" UInt32_BOOL DuplicateHandle(HANDLE arg1, HANDLE arg2, HANDLE arg3, HANDLE * arg4, UInt32 arg5, UInt32_BOOL arg6, UInt32 arg7)
{
    // UNIXTODO: Implement this function;
    *arg4 = (HANDLE)1;
    return UInt32_TRUE;
}

// TODO: what to do with this? The CRITICAL_SECTION at the other side is different!
typedef struct _RTL_CRITICAL_SECTION {
    pthread_mutex_t mutex;
} CRITICAL_SECTION, RTL_CRITICAL_SECTION, *PRTL_CRITICAL_SECTION;

extern "C" UInt32_BOOL InitializeCriticalSectionEx(CRITICAL_SECTION * lpCriticalSection, UInt32 arg2, UInt32 arg3)
{
    return pthread_mutex_init(&lpCriticalSection->mutex, NULL) == 0;
}

extern "C" void DeleteCriticalSection(CRITICAL_SECTION * lpCriticalSection)
{
    pthread_mutex_destroy(&lpCriticalSection->mutex);
}

extern "C" void EnterCriticalSection(CRITICAL_SECTION * lpCriticalSection)
{
    pthread_mutex_lock(&lpCriticalSection->mutex);;
}

extern "C" void LeaveCriticalSection(CRITICAL_SECTION * lpCriticalSection)
{
    pthread_mutex_unlock(&lpCriticalSection->mutex);
}

extern "C" void RaiseFailFastException(PEXCEPTION_RECORD arg1, PCONTEXT arg2, UInt32 arg3)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

typedef void(__stdcall *PFLS_CALLBACK_FUNCTION) (void* lpFlsData);

extern "C" UInt32 FlsAlloc(PFLS_CALLBACK_FUNCTION arg1)
{
    // UNIXTODO: The FLS stuff needs to be abstracted, the only usage of the FLS is to get a callback
    // when a thread is terminating.
    return 1;
}

__thread void* flsValue;

extern "C" void * FlsGetValue(UInt32 arg1)
{
    // UNIXTODO: The FLS stuff needs to be abstracted, the only usage of the FLS is to get a callback
    // when a thread is terminating.
    return flsValue;
}

extern "C" UInt32_BOOL FlsSetValue(UInt32 arg1, void * arg2)
{
    // UNIXTODO: The FLS stuff needs to be abstracted, the only usage of the FLS is to get a callback
    // when a thread is terminating.
    flsValue = arg2;

    return UInt32_TRUE;
}

extern "C" unsigned __int64  __readgsqword(unsigned long Offset)
{
    // UNIXTODO: The same as PalFlsAlloc is true here.
    return 0;
}

extern "C" UInt32_BOOL IsDebuggerPresent()
{
    // UNIXTODO: Implement this function
    return UInt32_FALSE;
}

extern "C" HANDLE LoadLibraryExW(const WCHAR * arg1, HANDLE arg2, UInt32 arg3)
{
    // Refactor assert.h to not to use it explicitly
    PORTABILITY_ASSERT("UNIXTODO: Remove this function");
}

extern "C" void* GetProcAddress(HANDLE arg1, const char * arg2)
{
    // Refactor assert.h to not to use it explicitly
    PORTABILITY_ASSERT("UNIXTODO: Remove this function");
}

extern "C" void TerminateProcess(HANDLE arg1, UInt32 arg2)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

extern "C" UInt32_BOOL SetEvent(HANDLE arg1)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

extern "C" UInt32_BOOL ResetEvent(HANDLE arg1)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

extern "C" UInt32 GetEnvironmentVariableW(const wchar_t* arg1, wchar_t* arg2, UInt32 arg3)
{
    // UNIXTODO: Implement this function
    *arg2 = '\0';
    return 0;
}

extern "C" UInt16 RtlCaptureStackBackTrace(UInt32 arg1, UInt32 arg2, void* arg3, UInt32* arg4)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

extern "C" void* HeapAlloc(HANDLE arg1, UInt32 arg2, UIntNative arg3)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

extern "C" UInt32_BOOL HeapFree(HANDLE arg1, UInt32 arg2, void * arg3)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

typedef UInt32 (__stdcall *HijackCallback)(HANDLE hThread, _In_ PAL_LIMITED_CONTEXT* pThreadContext, _In_opt_ void* pCallbackContext);

REDHAWK_PALEXPORT uint32_t REDHAWK_PALAPI PalHijack(HANDLE hThread, _In_ HijackCallback callback, _In_opt_ void* pCallbackContext)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

extern "C" UInt32 WaitForSingleObjectEx(HANDLE arg1, UInt32 arg2, UInt32_BOOL arg3)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

extern "C" void _mm_pause()
{
  __asm__ volatile ("pause");
}

extern "C" Int32 _wcsicmp(const wchar_t *string1, const wchar_t *string2)
{
    // HACK: there is no case insensitive wide char string API on Unix
    return wcscmp(string1, string2);
}

// NOTE: this is needed by main.cpp of the compiled app
int UTF8ToWideChar(char* bytes, int len, uint16_t* buffer, int bufLen)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}
