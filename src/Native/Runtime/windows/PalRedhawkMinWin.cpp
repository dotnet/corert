// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
#include "common.h"
#include <windows.h>
#include <stdio.h>
#include <errno.h>
#include <evntprov.h>
#ifdef PROJECTN
#include <roapi.h>
#endif

#include "holder.h"

#define PalRaiseFailFastException RaiseFailFastException

uint32_t PalEventWrite(REGHANDLE arg1, const EVENT_DESCRIPTOR * arg2, uint32_t arg3, EVENT_DATA_DESCRIPTOR * arg4)
{
    return EventWrite(arg1, arg2, arg3, arg4);
}

#include "gcenv.h"


#define REDHAWK_PALEXPORT extern "C"
#define REDHAWK_PALAPI __stdcall

#ifndef RUNTIME_SERVICES_ONLY
// Index for the fiber local storage of the attached thread pointer
static UInt32 g_flsIndex = FLS_OUT_OF_INDEXES;
#endif

static DWORD g_dwPALCapabilities;

GCSystemInfo g_SystemInfo;

bool InitializeSystemInfo()
{
    SYSTEM_INFO systemInfo;
    GetSystemInfo(&systemInfo);

    g_SystemInfo.dwNumberOfProcessors = systemInfo.dwNumberOfProcessors;
    g_SystemInfo.dwPageSize = systemInfo.dwPageSize;
    g_SystemInfo.dwAllocationGranularity = systemInfo.dwAllocationGranularity;

    return true;
}

extern bool PalQueryProcessorTopology();

#ifndef RUNTIME_SERVICES_ONLY
// This is called when each *fiber* is destroyed. When the home fiber of a thread is destroyed,
// it means that the thread itself is destroyed.
// Since we receive that notification outside of the Loader Lock, it allows us to safely acquire
// the ThreadStore lock in the RuntimeThreadShutdown.
void __stdcall FiberDetachCallback(void* lpFlsData)
{
    ASSERT(g_flsIndex != FLS_OUT_OF_INDEXES);
    ASSERT(lpFlsData == FlsGetValue(g_flsIndex));

    if (lpFlsData != NULL)
    {
        // The current fiber is the home fiber of a thread, so the thread is shutting down
        RuntimeThreadShutdown(lpFlsData);
    }
}
#endif

// The Redhawk PAL must be initialized before any of its exports can be called. Returns true for a successful
// initialization and false on failure.
REDHAWK_PALEXPORT bool REDHAWK_PALAPI PalInit()
{
    g_dwPALCapabilities = WriteWatchCapability | GetCurrentProcessorNumberCapability | LowMemoryNotificationCapability;

    if (!PalQueryProcessorTopology())
        return false;

#ifndef RUNTIME_SERVICES_ONLY
    // We use fiber detach callbacks to run our thread shutdown code because the fiber detach
    // callback is made without the OS loader lock
    g_flsIndex = FlsAlloc(FiberDetachCallback);
    if (g_flsIndex == FLS_OUT_OF_INDEXES)
    {
        return false;
    }
#endif

    return true;
}

// Given a mask of capabilities return true if all of them are supported by the current PAL.
REDHAWK_PALEXPORT bool REDHAWK_PALAPI PalHasCapability(PalCapability capability)
{
    return (g_dwPALCapabilities & (DWORD)capability) == (DWORD)capability;
}

#ifndef RUNTIME_SERVICES_ONLY
// Attach thread to PAL. 
// It can be called multiple times for the same thread.
// It fails fast if a different thread was already registered with the current fiber
// or if the thread was already registered with a different fiber.
// Parameters:
//  thread        - thread to attach
REDHAWK_PALEXPORT void REDHAWK_PALAPI PalAttachThread(void* thread)
{
    void* threadFromCurrentFiber = FlsGetValue(g_flsIndex);

    if (threadFromCurrentFiber != NULL)
    {
        ASSERT_UNCONDITIONALLY("Multiple threads encountered from a single fiber");
        RhFailFast();
    }

    // Associate the current fiber with the current thread.  This makes the current fiber the thread's "home"
    // fiber.  This fiber is the only fiber allowed to execute managed code on this thread.  When this fiber
    // is destroyed, we consider the thread to be destroyed.
    FlsSetValue(g_flsIndex, thread);
}

// Detach thread from PAL.
// It fails fast if some other thread value was attached to PAL.
// Parameters:
//  thread        - thread to detach
// Return:
//  true if the thread was detached, false if there was no attached thread
REDHAWK_PALEXPORT bool REDHAWK_PALAPI PalDetachThread(void* thread)
{
    ASSERT(g_flsIndex != FLS_OUT_OF_INDEXES);
    void* threadFromCurrentFiber = FlsGetValue(g_flsIndex);

    if (threadFromCurrentFiber == NULL)
    {
        // we've seen this thread, but not this fiber.  It must be a "foreign" fiber that was 
        // borrowing this thread.
        return false;
    }

    if (threadFromCurrentFiber != thread)
    {
        ASSERT_UNCONDITIONALLY("Detaching a thread from the wrong fiber");
        RhFailFast();
    }

    FlsSetValue(g_flsIndex, NULL);
    return true;
}
#endif // RUNTIME_SERVICES_ONLY

extern "C" UInt64 PalGetCurrentThreadIdForLogging()
{
    return GetCurrentThreadId();
}

#if !defined(USE_PORTABLE_HELPERS) && !defined(FEATURE_RX_THUNKS)
REDHAWK_PALEXPORT UInt32_BOOL REDHAWK_PALAPI PalAllocateThunksFromTemplate(_In_ HANDLE hTemplateModule, UInt32 templateRva, size_t templateSize, _Outptr_result_bytebuffer_(templateSize) void** newThunksOut)
{
#ifdef XBOX_ONE
    return E_NOTIMPL;
#else
    BOOL success = FALSE;
    HANDLE hMap = NULL, hFile = INVALID_HANDLE_VALUE;

    const WCHAR * wszModuleFileName = NULL;
    if (PalGetModuleFileName(&wszModuleFileName, hTemplateModule) == 0 || wszModuleFileName == NULL)
        return FALSE;

    hFile = CreateFileW(wszModuleFileName, GENERIC_READ | GENERIC_EXECUTE, FILE_SHARE_READ, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
    if (hFile == INVALID_HANDLE_VALUE)
        goto cleanup;

    hMap = CreateFileMapping(hFile, NULL, SEC_IMAGE | PAGE_READONLY, 0, 0, NULL);
    if (hMap == NULL)
        goto cleanup;

    *newThunksOut = MapViewOfFile(hMap, 0, 0, templateRva, templateSize);
    success = ((*newThunksOut) != NULL);

cleanup:
    CloseHandle(hMap);
    CloseHandle(hFile);

    return success;
#endif
}

REDHAWK_PALEXPORT UInt32_BOOL REDHAWK_PALAPI PalFreeThunksFromTemplate(_In_ void *pBaseAddress)
{
#ifdef XBOX_ONE
    return TRUE;
#else 
    return UnmapViewOfFile(pBaseAddress);
#endif    
}
#endif // !USE_PORTABLE_HELPERS && !FEATURE_RX_THUNKS

REDHAWK_PALEXPORT UInt32_BOOL REDHAWK_PALAPI PalMarkThunksAsValidCallTargets(
    void *virtualAddress,
    int thunkSize,
    int thunksPerBlock,
    int thunkBlockSize,
    int thunkBlocksPerMapping)
{
    // For CoreRT we are using RWX pages so there is no need for this API for now.
    // Once we have a scenario for non-RWX pages we should be able to put the implementation here
    return TRUE;
}

REDHAWK_PALEXPORT UInt32 REDHAWK_PALAPI PalCompatibleWaitAny(UInt32_BOOL alertable, UInt32 timeout, UInt32 handleCount, HANDLE* pHandles, UInt32_BOOL allowReentrantWait)
{
    if (!allowReentrantWait)
    {
        return WaitForMultipleObjectsEx(handleCount, pHandles, FALSE, timeout, alertable);
    }
    else
    {
        DWORD index;
        SetLastError(ERROR_SUCCESS); // recommended by MSDN.
        HRESULT hr = CoWaitForMultipleHandles(alertable ? COWAIT_ALERTABLE : 0, timeout, handleCount, pHandles, &index);

        switch (hr)
        {
        case S_OK:
            return index;

        case RPC_S_CALLPENDING:
            return WAIT_TIMEOUT;

        default:
            SetLastError(HRESULT_CODE(hr));
            return WAIT_FAILED;
        }
    }
}

REDHAWK_PALEXPORT void REDHAWK_PALAPI PalSleep(UInt32 milliseconds)
{
    return Sleep(milliseconds);
}

REDHAWK_PALEXPORT UInt32_BOOL REDHAWK_PALAPI PalSwitchToThread()
{
    return SwitchToThread();
}

REDHAWK_PALEXPORT HANDLE REDHAWK_PALAPI PalCreateEventW(_In_opt_ LPSECURITY_ATTRIBUTES pEventAttributes, UInt32_BOOL manualReset, UInt32_BOOL initialState, _In_opt_z_ LPCWSTR pName)
{
    return CreateEventW(pEventAttributes, manualReset, initialState, pName);
}

REDHAWK_PALEXPORT _Success_(return) bool REDHAWK_PALAPI PalGetThreadContext(HANDLE hThread, _Out_ PAL_LIMITED_CONTEXT * pCtx)
{
    CONTEXT win32ctx;

    win32ctx.ContextFlags = CONTEXT_CONTROL | CONTEXT_INTEGER | CONTEXT_EXCEPTION_REQUEST;

    if (!GetThreadContext(hThread, &win32ctx))
        return false;

    // The CONTEXT_SERVICE_ACTIVE and CONTEXT_EXCEPTION_ACTIVE output flags indicate we suspended the thread
    // at a point where the kernel cannot guarantee a completely accurate context. We'll fail the request in
    // this case (which should force our caller to resume the thread and try again -- since this is a fairly
    // narrow window we're highly likely to succeed next time).
    // Note: in some cases (x86 WOW64, ARM32 on ARM64) the OS will not set the CONTEXT_EXCEPTION_REPORTING flag
    // if the thread is executing in kernel mode (i.e. in the middle of a syscall or exception handling).
    // Therefore, we should treat the absence of the CONTEXT_EXCEPTION_REPORTING flag as an indication that
    // it is not safe to manipulate with the current state of the thread context.
    if ((win32ctx.ContextFlags & CONTEXT_EXCEPTION_REPORTING) == 0 ||
        (win32ctx.ContextFlags & (CONTEXT_SERVICE_ACTIVE | CONTEXT_EXCEPTION_ACTIVE)))
        return false;

#ifdef _X86_
    pCtx->IP = win32ctx.Eip;
    pCtx->Rsp = win32ctx.Esp;
    pCtx->Rbp = win32ctx.Ebp;
    pCtx->Rdi = win32ctx.Edi;
    pCtx->Rsi = win32ctx.Esi;
    pCtx->Rax = win32ctx.Eax;
    pCtx->Rbx = win32ctx.Ebx;
#elif defined(_AMD64_)
    pCtx->IP = win32ctx.Rip;
    pCtx->Rsp = win32ctx.Rsp;
    pCtx->Rbp = win32ctx.Rbp;
    pCtx->Rdi = win32ctx.Rdi;
    pCtx->Rsi = win32ctx.Rsi;
    pCtx->Rax = win32ctx.Rax;
    pCtx->Rbx = win32ctx.Rbx;
    pCtx->R12 = win32ctx.R12;
    pCtx->R13 = win32ctx.R13;
    pCtx->R14 = win32ctx.R14;
    pCtx->R15 = win32ctx.R15;
#elif defined(_ARM_)
    pCtx->IP = win32ctx.Pc;
    pCtx->R0 = win32ctx.R0;
    pCtx->R4 = win32ctx.R4;
    pCtx->R5 = win32ctx.R5;
    pCtx->R6 = win32ctx.R6;
    pCtx->R7 = win32ctx.R7;
    pCtx->R8 = win32ctx.R8;
    pCtx->R9 = win32ctx.R9;
    pCtx->R10 = win32ctx.R10;
    pCtx->R11 = win32ctx.R11;
    pCtx->SP = win32ctx.Sp;
    pCtx->LR = win32ctx.Lr;
#elif defined(_ARM64_)
    pCtx->IP = win32ctx.Pc;
    pCtx->X0 = win32ctx.X0;
    pCtx->X1 = win32ctx.X1;
    // TODO: Copy X2-X7 when we start supporting HVA's
    pCtx->X19 = win32ctx.X19;
    pCtx->X20 = win32ctx.X20;
    pCtx->X21 = win32ctx.X21;
    pCtx->X22 = win32ctx.X22;
    pCtx->X23 = win32ctx.X23;
    pCtx->X24 = win32ctx.X24;
    pCtx->X25 = win32ctx.X25;
    pCtx->X26 = win32ctx.X26;
    pCtx->X27 = win32ctx.X27;
    pCtx->X28 = win32ctx.X28;
    pCtx->SP = win32ctx.Sp;
    pCtx->LR = win32ctx.Lr;
    pCtx->FP = win32ctx.Fp;
#else
#error Unsupported platform
#endif
    return true;
}


REDHAWK_PALEXPORT UInt32 REDHAWK_PALAPI PalHijack(HANDLE hThread, _In_ PalHijackCallback callback, _In_opt_ void* pCallbackContext)
{
    if (hThread == INVALID_HANDLE_VALUE)
    {
        return (UInt32)E_INVALIDARG;
    }

    if (SuspendThread(hThread) == (DWORD)-1)
    {
        return HRESULT_FROM_WIN32(GetLastError());
    }

    PAL_LIMITED_CONTEXT ctx;
    HRESULT result;
    if (!PalGetThreadContext(hThread, &ctx))
    {
        result = HRESULT_FROM_WIN32(GetLastError());
    }
    else
    {
        result = callback(hThread, &ctx, pCallbackContext) ? S_OK : E_FAIL;
    }

    ResumeThread(hThread);

    return result;
}

REDHAWK_PALEXPORT HANDLE REDHAWK_PALAPI PalStartBackgroundWork(_In_ BackgroundCallback callback, _In_opt_ void* pCallbackContext, BOOL highPriority)
{
    HANDLE hThread = CreateThread(
        NULL,
        0,
        (LPTHREAD_START_ROUTINE)callback,
        pCallbackContext,
        highPriority ? CREATE_SUSPENDED : 0,
        NULL);

    if (hThread == NULL)
        return NULL;

    if (highPriority)
    {
        SetThreadPriority(hThread, THREAD_PRIORITY_HIGHEST);
        ResumeThread(hThread);
    }

    return hThread;
}

REDHAWK_PALEXPORT bool REDHAWK_PALAPI PalStartBackgroundGCThread(_In_ BackgroundCallback callback, _In_opt_ void* pCallbackContext)
{
    return PalStartBackgroundWork(callback, pCallbackContext, FALSE) != NULL;
}

REDHAWK_PALEXPORT bool REDHAWK_PALAPI PalStartFinalizerThread(_In_ BackgroundCallback callback, _In_opt_ void* pCallbackContext)
{
    return PalStartBackgroundWork(callback, pCallbackContext, TRUE) != NULL;
}

REDHAWK_PALEXPORT UInt32 REDHAWK_PALAPI PalGetTickCount()
{
#pragma warning(push)
#pragma warning(disable: 28159) // Consider GetTickCount64 instead
    return GetTickCount();
#pragma warning(pop)
}

REDHAWK_PALEXPORT bool REDHAWK_PALAPI PalEventEnabled(REGHANDLE regHandle, _In_ const EVENT_DESCRIPTOR* eventDescriptor)
{
    return !!EventEnabled(regHandle, eventDescriptor);
}

REDHAWK_PALEXPORT HANDLE REDHAWK_PALAPI PalCreateFileW(
    _In_z_ LPCWSTR pFileName, 
    uint32_t desiredAccess, 
    uint32_t shareMode, 
    _In_opt_ void* pSecurityAttributes, 
    uint32_t creationDisposition, 
    uint32_t flagsAndAttributes, 
    HANDLE hTemplateFile)
{
    return CreateFileW(pFileName, desiredAccess, shareMode, (LPSECURITY_ATTRIBUTES)pSecurityAttributes, 
                       creationDisposition, flagsAndAttributes, hTemplateFile);
}

REDHAWK_PALEXPORT HANDLE REDHAWK_PALAPI PalCreateLowMemoryNotification()
{
    return CreateMemoryResourceNotification(LowMemoryResourceNotification);
}

REDHAWK_PALEXPORT HANDLE REDHAWK_PALAPI PalGetModuleHandleFromPointer(_In_ void* pointer)
{
    HMODULE module;
    if (!GetModuleHandleExW(
        GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
        (LPCWSTR)pointer,
        &module))
    {
        return NULL;
    }

    return (HANDLE)module;
}

REDHAWK_PALEXPORT void* REDHAWK_PALAPI PalAddVectoredExceptionHandler(UInt32 firstHandler, _In_ PVECTORED_EXCEPTION_HANDLER vectoredHandler)
{
    return AddVectoredExceptionHandler(firstHandler, vectoredHandler);
}

REDHAWK_PALEXPORT void PalPrintFatalError(const char* message)
{
    // Write the message using lowest-level OS API available. This is used to print the stack overflow
    // message, so there is not much that can be done here.
    DWORD dwBytesWritten;
    WriteFile(GetStdHandle(STD_ERROR_HANDLE), message, (DWORD)strlen(message), &dwBytesWritten, NULL);
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



#if (defined(_TARGET_AMD64_) || defined (_TARGET_X86_)) && !defined(USE_PORTABLE_HELPERS)
EXTERN_C DWORD __fastcall getcpuid(DWORD arg, unsigned char result[16]);
EXTERN_C DWORD __fastcall getextcpuid(DWORD arg1, DWORD arg2, unsigned char result[16]);

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
            BOOL bSkipAMDL3 = FALSE;

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
                    bSkipAMDL3 = TRUE;
                    break;

                case 0x4:
                default:
                    bSkipAMDL3 = FALSE;
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

    DWORD maxCpuid = getextcpuid(0, 0, buffer);

    DWORD* dwBuffer = (DWORD*)buffer;

    if ((maxCpuid > 3) && (maxCpuid < 0x80000000)) // Deterministic Cache Enum is Supported
    {
        DWORD dwCacheWays, dwCachePartitions, dwLineSize, dwSets;
        DWORD retEAX = 0;
        DWORD loopECX = 0;
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
        DWORD* dwBuffer = (DWORD*)buffer;

        DWORD maxCpuId = getcpuid(0, buffer);

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

                        DWORD dwL2CacheBits = dwBuffer[2];
                        DWORD dwL3CacheBits = dwBuffer[3];

                        maxTrueSize = (size_t)((dwL2CacheBits >> 16) * 1024);    // L2 cache size in ECX bits 31-16

                        getcpuid(0x1, buffer);
                        DWORD dwBaseFamily = (dwBuffer[0] & (0xF << 8)) >> 8;
                        DWORD dwExtFamily = (dwBuffer[0] & (0xFF << 20)) >> 20;
                        DWORD dwFamily = dwBaseFamily >= 0xF ? dwBaseFamily + dwExtFamily : dwBaseFamily;

                        if (dwFamily >= 0x10)
                        {
                            BOOL bSkipAMDL3 = FALSE;

                            if (dwFamily == 0x10)   // are we running on a Barcelona (Family 10h) processor?
                            {
                                // check model
                                DWORD dwBaseModel = (dwBuffer[0] & (0xF << 4)) >> 4;
                                DWORD dwExtModel = (dwBuffer[0] & (0xF << 16)) >> 16;
                                DWORD dwModel = dwBaseFamily >= 0xF ? (dwExtModel << 4) | dwBaseModel : dwBaseModel;

                                switch (dwModel)
                                {
                                case 0x2:
                                    // 65nm parts do not benefit from larger Gen0
                                    bSkipAMDL3 = TRUE;
                                    break;

                                case 0x4:
                                default:
                                    bSkipAMDL3 = FALSE;
                                }
                            }

                            if (!bSkipAMDL3)
                            {
                                // 45nm Greyhound parts (and future parts based on newer northbridge) benefit
                                // from increased gen0 size, taking L3 into account
                                getcpuid(0x80000008, buffer);
                                DWORD dwNumberOfCores = (dwBuffer[2] & (0xFF)) + 1;        // NC is in ECX bits 7-0

                                DWORD dwL3CacheSize = (size_t)((dwL3CacheBits >> 18) * 512 * 1024);  // L3 size in EDX bits 31-18 * 512KB
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

DWORD CLR_GetLogicalCpuCountFromOS(_In_reads_opt_(nEntries) SYSTEM_LOGICAL_PROCESSOR_INFORMATION * pslpi, DWORD nEntries);

// This function returns the number of logical processors on a given physical chip.  If it cannot
// determine the number of logical cpus, or the machine is not populated uniformly with the same
// type of processors, this function returns 1.
DWORD CLR_GetLogicalCpuCountX86(_In_reads_opt_(nEntries) SYSTEM_LOGICAL_PROCESSOR_INFORMATION * pslpi, DWORD nEntries)
{
    // No CONTRACT possible because GetLogicalCpuCount uses SEH

    static DWORD val = 0;

    // cache value for later re-use
    if (val)
    {
        return val;
    }

    DWORD retVal = 1;

    __try
    {
        unsigned char buffer[16];

        DWORD maxCpuId = getcpuid(0, buffer);

        if (maxCpuId < 1)
            goto lDone;

        DWORD* dwBuffer = (DWORD*)buffer;

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
#endif // (defined(_TARGET_AMD64_) || defined (_TARGET_X86_)) && !defined(USE_PORTABLE_HELPERS)


#ifdef _DEBUG
DWORD CLR_GetLogicalCpuCountFromOS(_In_reads_opt_(nEntries) SYSTEM_LOGICAL_PROCESSOR_INFORMATION * pslpi, DWORD nEntries)
{
    // No CONTRACT possible because GetLogicalCpuCount uses SEH

    static DWORD val = 0;
    DWORD retVal = 0;

    if (pslpi == NULL)
    {
        // GetLogicalProcessorInformation no supported
        goto lDone;
    }

    DWORD prevcount = 0;
    DWORD count = 1;

    for (DWORD j = 0; j < nEntries; j++)
    {
        if (pslpi[j].Relationship == RelationProcessorCore)
        {
            // LTP_PC_SMT indicates HT or SMT
            if (pslpi[j].ProcessorCore.Flags == LTP_PC_SMT)
            {
                SIZE_T pmask = pslpi[j].ProcessorMask;

                // Count the processors in the mask
                //
                // These are not the fastest bit counters. There may be processor intrinsics
                // (which would be best), but there are variants faster than these:
                // See http://en.wikipedia.org/wiki/Hamming_weight.
                // This is the naive implementation.
#if !_WIN64
                count = (pmask & 0x55555555) + ((pmask >> 1) & 0x55555555);
                count = (count & 0x33333333) + ((count >> 2) & 0x33333333);
                count = (count & 0x0F0F0F0F) + ((count >> 4) & 0x0F0F0F0F);
                count = (count & 0x00FF00FF) + ((count >> 8) & 0x00FF00FF);
                count = (count & 0x0000FFFF) + ((count >> 16) & 0x0000FFFF);
#else
                pmask = (pmask & 0x5555555555555555ull) + ((pmask >> 1) & 0x5555555555555555ull);
                pmask = (pmask & 0x3333333333333333ull) + ((pmask >> 2) & 0x3333333333333333ull);
                pmask = (pmask & 0x0f0f0f0f0f0f0f0full) + ((pmask >> 4) & 0x0f0f0f0f0f0f0f0full);
                pmask = (pmask & 0x00ff00ff00ff00ffull) + ((pmask >> 8) & 0x00ff00ff00ff00ffull);
                pmask = (pmask & 0x0000ffff0000ffffull) + ((pmask >> 16) & 0x0000ffff0000ffffull);
                pmask = (pmask & 0x00000000ffffffffull) + ((pmask >> 32) & 0x00000000ffffffffull);
                count = static_cast<DWORD>(pmask);
#endif // !_WIN64 else
                assert(count > 0);

                if (prevcount)
                {
                    if (count != prevcount)
                    {
                        retVal = 1;       // masks are not symmetric
                        goto lDone;
                    }
                }

                prevcount = count;
            }
        }
    }

    retVal = count;

lDone:
    return retVal;
}

// This function returns the size of highest level cache on the physical chip.   If it cannot
// determine the cachesize this function returns 0.
size_t CLR_GetLogicalProcessorCacheSizeFromOS(_In_reads_opt_(nEntries) SYSTEM_LOGICAL_PROCESSOR_INFORMATION * pslpi, DWORD nEntries)
{
    size_t cache_size = 0;

    // Try to use GetLogicalProcessorInformation API and get a valid pointer to the SLPI array if successful.  Returns NULL
    // if API not present or on failure.

    if (pslpi == NULL)
    {
        // GetLogicalProcessorInformation not supported or failed.  
        goto Exit;
    }

    // Crack the information. Iterate through all the SLPI array entries for all processors in system.
    // Will return the greatest of all the processor cache sizes or zero

    size_t last_cache_size = 0;

    for (DWORD i = 0; i < nEntries; i++)
    {
        if (pslpi[i].Relationship == RelationCache)
        {
            last_cache_size = max(last_cache_size, pslpi[i].Cache.Size);
        }
    }
    cache_size = last_cache_size;
Exit:

    return cache_size;
}

DWORD CLR_GetLogicalCpuCount(_In_reads_opt_(nEntries) SYSTEM_LOGICAL_PROCESSOR_INFORMATION * pslpi, DWORD nEntries)
{
#if (defined(_TARGET_AMD64_) || defined (_TARGET_X86_)) && !defined(USE_PORTABLE_HELPERS)
    return CLR_GetLogicalCpuCountX86(pslpi, nEntries);
#else
    return CLR_GetLogicalCpuCountFromOS(pslpi, nEntries);
#endif
}

size_t CLR_GetLargestOnDieCacheSize(UInt32_BOOL bTrueSize, _In_reads_opt_(nEntries) SYSTEM_LOGICAL_PROCESSOR_INFORMATION * pslpi, DWORD nEntries)
{
#if (defined(_TARGET_AMD64_) || defined (_TARGET_X86_)) && !defined(USE_PORTABLE_HELPERS)
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
#if (defined(_TARGET_AMD64_) || defined (_TARGET_X86_)) && !defined(USE_PORTABLE_HELPERS)
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
    printf("----------------\n");
    for (UInt32 i = 0; i < cRecords; i++)
    {
        switch (pProcInfos[i].Relationship)
        {
        case RelationProcessorCore:
            printf("    [%2d] Core: %d threads                    0x%04Ix mask, flags = %d\n",
                i, CountBits(pProcInfos[i].ProcessorMask), pProcInfos[i].ProcessorMask,
                pProcInfos[i].ProcessorCore.Flags);
            break;

        case RelationCache:
            char* pszCacheType;
            switch (pProcInfos[i].Cache.Type) {
            case CacheUnified:      pszCacheType = "[Unified]"; break;
            case CacheInstruction:  pszCacheType = "[Instr  ]"; break;
            case CacheData:         pszCacheType = "[Data   ]"; break;
            case CacheTrace:        pszCacheType = "[Trace  ]"; break;
            default:                pszCacheType = "[Unk    ]"; break;
            }
            printf("    [%2d] Cache: %s 0x%08x bytes  0x%04Ix mask\n", i, pszCacheType,
                pProcInfos[i].Cache.Size, pProcInfos[i].ProcessorMask);
            break;

        case RelationNumaNode:
            printf("    [%2d] NumaNode: #%02d                      0x%04Ix mask\n",
                i, pProcInfos[i].NumaNode.NodeNumber, pProcInfos[i].ProcessorMask);
            break;
        case RelationProcessorPackage:
            printf("    [%2d] Package:                           0x%04Ix mask\n",
                i, pProcInfos[i].ProcessorMask);
            break;
        case RelationAll:
        case RelationGroup:
        default:
            printf("    [%2d] unknown: %d\n", i, pProcInfos[i].Relationship);
            break;
        }
    }
    printf("----------------\n");
}

void DumpCacheTopologyResults(UInt32 maxCpuId, CpuVendor cpuVendor, _In_reads_(cRecords) SYSTEM_LOGICAL_PROCESSOR_INFORMATION * pProcInfos, UInt32 cRecords)
{
    DumpCacheTopology(pProcInfos, cRecords);
    printf("maxCpuId: %d, %s\n", maxCpuId, (cpuVendor == CpuIntel) ? "CpuIntel" : ((cpuVendor == CpuAMD) ? "CpuAMD" : "CpuUnknown"));
    printf("               g_cLogicalCpus:          %d %d          :CLR_GetLogicalCpuCount\n", g_cLogicalCpus, CLR_GetLogicalCpuCount(pProcInfos, cRecords));
    printf("        g_cbLargestOnDieCache: 0x%08Ix 0x%08Ix :CLR_LargestOnDieCache(TRUE)\n", g_cbLargestOnDieCache, CLR_GetLargestOnDieCacheSize(TRUE, pProcInfos, cRecords));
    printf("g_cbLargestOnDieCacheAdjusted: 0x%08Ix 0x%08Ix :CLR_LargestOnDieCache(FALSE)\n", g_cbLargestOnDieCacheAdjusted, CLR_GetLargestOnDieCacheSize(FALSE, pProcInfos, cRecords));
}
#endif // _DEBUG

// Method used to initialize the above values.
bool PalQueryProcessorTopology()
{
    SYSTEM_LOGICAL_PROCESSOR_INFORMATION *pProcInfos = NULL;
    DWORD cbBuffer = 0;
    bool fError = false;

    for (;;)
    {
        // Ask for processor information with an insufficient buffer initially. The function will tell us how
        // much memory we need and we'll try again.
        if (!GetLogicalProcessorInformation(pProcInfos, &cbBuffer))
        {
            if (GetLastError() == ERROR_INSUFFICIENT_BUFFER)
            {
                if (pProcInfos)
                    free(pProcInfos);

                pProcInfos = (SYSTEM_LOGICAL_PROCESSOR_INFORMATION*)malloc(cbBuffer);

                if (pProcInfos == NULL)
                {
                    // Ran out of memory.
                    fError = true;
                    break;
                }
            }
            else
            {
                // Unexpected error from GetLogicalProcessorInformation().
                fError = true;
                break;
            }
        }
        else
        {
            // Successfully read processor information, stop looping.
            break;
        }
    }

    // If there was no error retrieving the data parse the result. GetLogicalProcessorInformation() returns an
    // array of structures each of which describes some attribute of a given group of logical processors.
    // Fields in the structure describe which processors and which attributes are being described and the
    // structures come in no particular order. Therefore we just iterate over all of them accumulating the
    // data we're interested in as we go.
    if (!fError && pProcInfos != NULL)
    {
        // Some explanation of the following logic is required. The GC queries information via two APIs:
        //  1) GetLogicalCpuCount()
        //  2) GetLargestOnDieCacheSize()
        //
        // These were once unambiguous queries; logical CPUs only existed when a physical CPU supported
        // threading (e.g. Intel's HyperThreading technology) and caches were always shared across an entire
        // physical processor.
        //
        // Unfortunately for us actual processor topologies are getting ever more complex (and divergent even
        // between otherwise near-identical architectures such as Intel and AMD). A single physical processor
        // (or package, the thing that fits in a socket on the motherboard) can now have multiple classes of
        // logical processors within it with differing relationships between the other logical processors
        // (e.g. which share functional units or caches). It's technically feasible to build systems with
        // non-symmetric topologies as well (where the number of logical processors or cache differs between
        // physical processors for instance).
        //
        // The GetLogicalProcessorInformation() reflects this in the potential complexity of its output. For
        // large-multi CPU systems it can generate quite a few output records effectively drawing a tree of
        // logical processors and their relationships within cores and packages and to various levels of
        // cache.
        //
        // Out of this complexity we have to distill the simple answers required above. It may well prove true
        // in the future that we will have to ask more complex questions, but until then this function will
        // utilize the following semantics for each of the queries:
        //  1) We will report logical processors as the average number of threads per core. (For the likely
        //     case, a symmetric system, this average will be the exact number of threads per core).
        //  2) We will report the largest cache on-die as the average largest cache per-core.
        //
        // We will calculate the first value by counting the number of core records returned and the number of
        // threads running on those cores (each core record supplies a bitmask of processors running on that
        // core and by definition each of those processor sets must be disjoint, so we can simply accumulate a
        // count of processors seen for each core so far). For now we will count all processors on a core as a
        // thread (even if the HT/SMT flag is not set for the core) until we have data that suggests we should
        // treat non-HT processors as cores in their own right. We can then simply divide the thread total by
        // the core total to get a thread per core average.
        //
        // The second is harder since we have to discard caches that are superceded by a larger cache
        // servicing the same logical processor. For instance, on a typical Intel system we wish to sum the
        // sizes of all the L2 caches but ignore all the L1 caches. Since performance is not a huge issue here
        // (this is a one time operation and we cache the results) we'll use a linear algorithm that, when
        // presented with a cache information record, re-scans all the records for another cache entry which
        // is of larger size and has at least one logical processor in common. If found, the current cache
        // record can be ignored.
        //
        // Once we have to total sizes of all the largest level caches on the system we can divide it by the
        // previously computed total cores to get average largest cache size per core.

        // Count info records returned by GetLogicalProcessorInformation().
        UInt32 cRecords = cbBuffer / sizeof(SYSTEM_LOGICAL_PROCESSOR_INFORMATION);

        UInt32 maxCpuId;
        CpuVendor cpuVendor = GetCpuVendor(&maxCpuId);

        bool isAsymmetric = false;
        UInt32 cLogicalCpus = 0;
        UInt32 cbCache = 0;
        UInt32 cbCacheAdjusted = 0;

        for (UInt32 i = 0; i < cRecords; i++)
        {
            switch (pProcInfos[i].Relationship)
            {
            case RelationProcessorCore:
                if (pProcInfos[i].ProcessorCore.Flags == LTP_PC_SMT)
                {
                    UInt32 thisCount = CountBits(pProcInfos[i].ProcessorMask);
                    if (!cLogicalCpus)
                        cLogicalCpus = thisCount;
                    else if (thisCount != cLogicalCpus)
                        isAsymmetric = true;
                }
                break;

            case RelationCache:
                cbCache = max(cbCache, pProcInfos[i].Cache.Size);
                break;

            default:
                break;
            }
        }

        cbCacheAdjusted = cbCache;
        if (cLogicalCpus == 0)
            cLogicalCpus = 1;

#if (defined(_TARGET_AMD64_) || defined (_TARGET_X86_)) && !defined(USE_PORTABLE_HELPERS)
        // Apply some experimentally-derived policy to the number of logical CPUs in the same way CLR does.
        if ((maxCpuId < 1)
            || (cpuVendor != CpuIntel)
            || ((maxCpuId > 3) && (maxCpuId < 0x80000000))  // This is a strange one.
            || isAsymmetric)
        {
            cLogicalCpus = 1;
        }

        // Apply some experimentally-derived policy to the cache size in the same way CLR does.
        if (cpuVendor == CpuIntel)
        {
#ifdef _WIN64
            if (maxCpuId >= 2)
            {
                // If we're running on a Prescott or greater core, EM64T tests
                // show that starting with a gen0 larger than LLC improves performance.
                // Thus, start with a gen0 size that is larger than the cache.  The value of
                // 3 is a reasonable tradeoff between workingset and performance.
                cbCacheAdjusted = cbCache * 3;
            }
#endif // _WIN64
        }
        else if (cpuVendor == CpuAMD)
        {
            QueryAMDCacheInfo(&cbCache, &cbCacheAdjusted);
        }
#else  // (defined(_TARGET_AMD64_) || defined (_TARGET_X86_)) && !defined(USE_PORTABLE_HELPERS)
        cpuVendor; // avoid unused variable warnings.
        maxCpuId;
#endif // (defined(_TARGET_AMD64_) || defined (_TARGET_X86_)) && !defined(USE_PORTABLE_HELPERS)

        g_cLogicalCpus = cLogicalCpus;
        g_cbLargestOnDieCache = cbCache;
        g_cbLargestOnDieCacheAdjusted = cbCacheAdjusted;

#ifdef _DEBUG
#ifdef TRACE_CACHE_TOPOLOGY
        DumpCacheTopologyResults(maxCpuId, cpuVendor, pProcInfos, cRecords);
#endif
        // CLR_GetLargestOnDieCacheSize is implemented for Intel and AMD processors only
        if ((cpuVendor == CpuIntel) || (cpuVendor == CpuAMD))
        {
            if ((CLR_GetLargestOnDieCacheSize(TRUE, pProcInfos, cRecords) != g_cbLargestOnDieCache) ||
                (CLR_GetLargestOnDieCacheSize(FALSE, pProcInfos, cRecords) != g_cbLargestOnDieCacheAdjusted) ||
                (CLR_GetLogicalCpuCount(pProcInfos, cRecords) != g_cLogicalCpus))
            {
#ifndef TRACE_CACHE_TOPOLOGY
                DumpCacheTopologyResults(maxCpuId, cpuVendor, pProcInfos, cRecords);
#endif
                assert(!"QueryProcessorTopology doesn't match CLR's results.  See stdout for more info.");
            }
        }
#endif // _DEBUG
    }

    if (pProcInfos)
        delete[](UInt8*)pProcInfos;

    return !fError;
}

#ifdef RUNTIME_SERVICES_ONLY
// Functions called by the GC to obtain our cached values for number of logical processors and cache size.
REDHAWK_PALEXPORT UInt32 REDHAWK_PALAPI PalGetLogicalCpuCount()
{
    return g_cLogicalCpus;
}

REDHAWK_PALEXPORT size_t REDHAWK_PALAPI PalGetLargestOnDieCacheSize(UInt32_BOOL bTrueSize)
{
    return bTrueSize ? g_cbLargestOnDieCache
        : g_cbLargestOnDieCacheAdjusted;
}
#endif // RUNTIME_SERVICES_ONLY

REDHAWK_PALEXPORT _Ret_maybenull_ _Post_writable_byte_size_(size) void* REDHAWK_PALAPI PalVirtualAlloc(_In_opt_ void* pAddress, UIntNative size, UInt32 allocationType, UInt32 protect)
{
    return VirtualAlloc(pAddress, size, allocationType, protect);
}

#pragma warning (push)
#pragma warning (disable:28160) // warnings about invalid potential parameter combinations that would cause VirtualFree to fail - those are asserted for below
REDHAWK_PALEXPORT UInt32_BOOL REDHAWK_PALAPI PalVirtualFree(_In_ void* pAddress, UIntNative size, UInt32 freeType)
{
    assert(((freeType & MEM_RELEASE) != MEM_RELEASE) || size == 0);
    assert((freeType & (MEM_RELEASE | MEM_DECOMMIT)) != (MEM_RELEASE | MEM_DECOMMIT));
    assert(freeType != 0);

    return VirtualFree(pAddress, size, freeType);
}
#pragma warning (pop)

REDHAWK_PALEXPORT UInt32_BOOL REDHAWK_PALAPI PalVirtualProtect(_In_ void* pAddress, UIntNative size, UInt32 protect)
{
    DWORD oldProtect;
    return VirtualProtect(pAddress, size, protect, &oldProtect);
}

REDHAWK_PALEXPORT _Ret_maybenull_ void* REDHAWK_PALAPI PalSetWerDataBuffer(_In_ void* pNewBuffer)
{
    static void* pBuffer;
    return InterlockedExchangePointer(&pBuffer, pNewBuffer);
}

#ifndef RUNTIME_SERVICES_ONLY

static LARGE_INTEGER g_performanceFrequency;

#ifdef PROJECTN
static bool g_roInitialized;
#endif

// Initialize the interface implementation
// Return:
//  true if it has succeeded, false if it has failed
bool GCToOSInterface::Initialize()
{
    if (!::QueryPerformanceFrequency(&g_performanceFrequency))
    {
        return false;
    }

#ifdef PROJECTN
    // TODO: Remove the RoInitialize call when we implement non-WinRT framework for classic apps
    HRESULT hr = RoInitialize(RO_INIT_MULTITHREADED);

    // RPC_E_CHANGED_MODE indicates this thread has been already initialized with a different
    // concurrency model. That is fine; we just need to skip the RoUninitialize call on shutdown.
    if (SUCCEEDED(hr))
    {
        g_roInitialized = true;
    }
    else if (hr != RPC_E_CHANGED_MODE)
    {
        return false;
    }
#endif

    return true;
}

// Shutdown the interface implementation
// Remarks:
//  Must be called on the same thread as Initialize.
void GCToOSInterface::Shutdown()
{
#ifdef PROJECTN
    if (g_roInitialized)
    {
        RoUninitialize();
        g_roInitialized = false;
    }
#endif
}

// Get numeric id of the current thread if possible on the 
// current platform. It is indended for logging purposes only.
// Return:
//  Numeric id of the current thread or 0 if the 
uint64_t GCToOSInterface::GetCurrentThreadIdForLogging()
{
    return ::GetCurrentThreadId();
}

// Get id of the process
uint32_t GCToOSInterface::GetCurrentProcessId()
{
    return ::GetCurrentProcessId();
}

// Set ideal affinity for the current thread
// Parameters:
//  affinity - ideal processor affinity for the thread
// Return:
//  true if it has succeeded, false if it has failed
bool GCToOSInterface::SetCurrentThreadIdealAffinity(GCThreadAffinity* affinity)
{
    bool success = true;

    PROCESSOR_NUMBER proc;

    if (affinity->Group != -1)
    {
        proc.Group = (WORD)affinity->Group;
        proc.Number = (BYTE)affinity->Processor;
        proc.Reserved = 0;
        
        success = !!SetThreadIdealProcessorEx(GetCurrentThread(), &proc, NULL);
    }
    else
    {
        if (GetThreadIdealProcessorEx(GetCurrentThread(), &proc))
        {
            proc.Number = (BYTE)affinity->Processor;
            success = !!SetThreadIdealProcessorEx(GetCurrentThread(), &proc, NULL);
        }        
    }

    return success;
}

// Get the number of the current processor
uint32_t GCToOSInterface::GetCurrentProcessorNumber()
{
    _ASSERTE(GCToOSInterface::CanGetCurrentProcessorNumber());
    return ::GetCurrentProcessorNumber();
}

// Check if the OS supports getting current processor number
bool GCToOSInterface::CanGetCurrentProcessorNumber()
{
    return true;
}

// Flush write buffers of processors that are executing threads of the current process
void GCToOSInterface::FlushProcessWriteBuffers()
{
    ::FlushProcessWriteBuffers();
}

// Break into a debugger
void GCToOSInterface::DebugBreak()
{
    ::DebugBreak();
}

// Get number of logical processors
uint32_t GCToOSInterface::GetLogicalCpuCount()
{
    return g_cLogicalCpus;
}

// Causes the calling thread to sleep for the specified number of milliseconds
// Parameters:
//  sleepMSec   - time to sleep before switching to another thread
void GCToOSInterface::Sleep(uint32_t sleepMSec)
{
    PalSleep(sleepMSec);
}

// Causes the calling thread to yield execution to another thread that is ready to run on the current processor.
// Parameters:
//  switchCount - number of times the YieldThread was called in a loop
void GCToOSInterface::YieldThread(uint32_t /*switchCount*/)
{
    PalSwitchToThread();
}

// Reserve virtual memory range.
// Parameters:
//  address   - starting virtual address, it can be NULL to let the function choose the starting address
//  size      - size of the virtual memory range
//  alignment - requested memory alignment
//  flags     - flags to control special settings like write watching
// Return:
//  Starting virtual address of the reserved range
void* GCToOSInterface::VirtualReserve(size_t size, size_t alignment, uint32_t flags)
{
    DWORD memFlags = (flags & VirtualReserveFlags::WriteWatch) ? (MEM_RESERVE | MEM_WRITE_WATCH) : MEM_RESERVE;
    return ::VirtualAlloc(0, size, memFlags, PAGE_READWRITE);
}

// Release virtual memory range previously reserved using VirtualReserve
// Parameters:
//  address - starting virtual address
//  size    - size of the virtual memory range
// Return:
//  true if it has succeeded, false if it has failed
bool GCToOSInterface::VirtualRelease(void* address, size_t size)
{
    UNREFERENCED_PARAMETER(size);
    return !!::VirtualFree(address, 0, MEM_RELEASE);
}

// Commit virtual memory range. It must be part of a range reserved using VirtualReserve.
// Parameters:
//  address - starting virtual address
//  size    - size of the virtual memory range
// Return:
//  true if it has succeeded, false if it has failed
bool GCToOSInterface::VirtualCommit(void* address, size_t size)
{
    return ::VirtualAlloc(address, size, MEM_COMMIT, PAGE_READWRITE) != NULL;
}

// Decomit virtual memory range.
// Parameters:
//  address - starting virtual address
//  size    - size of the virtual memory range
// Return:
//  true if it has succeeded, false if it has failed
bool GCToOSInterface::VirtualDecommit(void* address, size_t size)
{
    return !!::VirtualFree(address, size, MEM_DECOMMIT);
}

// Reset virtual memory range. Indicates that data in the memory range specified by address and size is no 
// longer of interest, but it should not be decommitted.
// Parameters:
//  address - starting virtual address
//  size    - size of the virtual memory range
//  unlock  - true if the memory range should also be unlocked
// Return:
//  true if it has succeeded, false if it has failed
bool GCToOSInterface::VirtualReset(void * address, size_t size, bool unlock)
{
    bool success = ::VirtualAlloc(address, size, MEM_RESET, PAGE_READWRITE) != NULL;
    if (success && unlock)
    {
        // Remove the page range from the working set
        ::VirtualUnlock(address, size);
    }

    return success;
}

// Check if the OS supports write watching
bool GCToOSInterface::SupportsWriteWatch()
{
    return PalHasCapability(WriteWatchCapability);
}

// Reset the write tracking state for the specified virtual memory range.
// Parameters:
//  address - starting virtual address
//  size    - size of the virtual memory range
void GCToOSInterface::ResetWriteWatch(void* address, size_t size)
{
    ::ResetWriteWatch(address, size);
}

// Retrieve addresses of the pages that are written to in a region of virtual memory
// Parameters:
//  resetState         - true indicates to reset the write tracking state
//  address            - starting virtual address
//  size               - size of the virtual memory range
//  pageAddresses      - buffer that receives an array of page addresses in the memory region
//  pageAddressesCount - on input, size of the lpAddresses array, in array elements
//                       on output, the number of page addresses that are returned in the array.
// Return:
//  true if it has succeeded, false if it has failed
bool GCToOSInterface::GetWriteWatch(bool resetState, void* address, size_t size, void** pageAddresses, uintptr_t* pageAddressesCount)
{
    uint32_t flags = resetState ? 1 : 0;
    ULONG granularity;

    bool success = ::GetWriteWatch(flags, address, size, pageAddresses, (ULONG_PTR*)pageAddressesCount, &granularity) == 0;
    _ASSERTE (granularity == OS_PAGE_SIZE);

    return success;
}

// Get size of the largest cache on the processor die
// Parameters:
//  trueSize - true to return true cache size, false to return scaled up size based on
//             the processor architecture
// Return:
//  Size of the cache
size_t GCToOSInterface::GetLargestOnDieCacheSize(bool trueSize)
{
    return trueSize ? g_cbLargestOnDieCache : g_cbLargestOnDieCacheAdjusted;
}

// Get affinity mask of the current process
// Parameters:
//  processMask - affinity mask for the specified process
//  systemMask  - affinity mask for the system
// Return:
//  true if it has succeeded, false if it has failed
// Remarks:
//  A process affinity mask is a bit vector in which each bit represents the processors that
//  a process is allowed to run on. A system affinity mask is a bit vector in which each bit
//  represents the processors that are configured into a system.
//  A process affinity mask is a subset of the system affinity mask. A process is only allowed
//  to run on the processors configured into a system. Therefore, the process affinity mask cannot
//  specify a 1 bit for a processor when the system affinity mask specifies a 0 bit for that processor.
bool GCToOSInterface::GetCurrentProcessAffinityMask(uintptr_t* processMask, uintptr_t* systemMask)
{
    return !!::GetProcessAffinityMask(GetCurrentProcess(), (PDWORD_PTR)processMask, (PDWORD_PTR)systemMask);
}

// Get number of processors assigned to the current process
// Return:
//  The number of processors
uint32_t GCToOSInterface::GetCurrentProcessCpuCount()
{
    static int cCPUs = 0;

    if (cCPUs != 0)
        return cCPUs;

    DWORD_PTR pmask, smask;

    if (!GetProcessAffinityMask(GetCurrentProcess(), &pmask, &smask))
        return 1;

    if (pmask == 1)
        return 1;

    pmask &= smask;
        
    int count = 0;
    while (pmask)
    {
        if (pmask & 1)
            count++;
                
        pmask >>= 1;
    }
        
    // GetProcessAffinityMask can return pmask=0 and smask=0 on systems with more
    // than 64 processors, which would leave us with a count of 0.  Since the GC
    // expects there to be at least one processor to run on (and thus at least one
    // heap), we'll return 64 here if count is 0, since there are likely a ton of
    // processors available in that case.  The GC also cannot (currently) handle
    // the case where there are more than 64 processors, so we will return a
    // maximum of 64 here.
    if (count == 0 || count > 64)
        count = 64;

    cCPUs = count;
            
    return count;
}

// Return the size of the user-mode portion of the virtual address space of this process.
// Return:
//  non zero if it has succeeded, 0 if it has failed
size_t GCToOSInterface::GetVirtualMemoryLimit()
{
    MEMORYSTATUSEX memStatus;

    memStatus.dwLength = sizeof(MEMORYSTATUSEX);

    BOOL fRet;
    fRet = GlobalMemoryStatusEx(&memStatus);
    _ASSERTE(fRet);

    return (size_t)memStatus.ullTotalVirtual;
}

// Get the physical memory that this process can use.
// Return:
//  non zero if it has succeeded, 0 if it has failed
uint64_t GCToOSInterface::GetPhysicalMemoryLimit()
{
    MEMORYSTATUSEX memStatus;

    memStatus.dwLength = sizeof(MEMORYSTATUSEX);

    BOOL fRet;
    fRet = GlobalMemoryStatusEx(&memStatus);
    _ASSERTE(fRet);

    return memStatus.ullTotalPhys;
}

// Get memory status
// Parameters:
//  memory_load - A number between 0 and 100 that specifies the approximate percentage of physical memory
//      that is in use (0 indicates no memory use and 100 indicates full memory use).
//  available_physical - The amount of physical memory currently available, in bytes.
//  available_page_file - The maximum amount of memory the current process can commit, in bytes.
void GCToOSInterface::GetMemoryStatus(uint32_t* memory_load, uint64_t* available_physical, uint64_t* available_page_file)
{
    MEMORYSTATUSEX memStatus;

    memStatus.dwLength = sizeof(MEMORYSTATUSEX);

    BOOL fRet;
    fRet = GlobalMemoryStatusEx(&memStatus);
    _ASSERTE(fRet);

    // If the machine has more RAM than virtual address limit, let us cap it.
    // The GC can never use more than virtual address limit.
    if (memStatus.ullAvailPhys > memStatus.ullTotalVirtual)
    {
        memStatus.ullAvailPhys = memStatus.ullAvailVirtual;
    }

    if (memory_load != NULL)
        *memory_load = memStatus.dwMemoryLoad;
    if (available_physical != NULL)
        *available_physical = memStatus.ullAvailPhys;
    if (available_page_file != NULL)
        *available_page_file = memStatus.ullAvailPageFile;
}

// Get a high precision performance counter
// Return:
//  The counter value
int64_t GCToOSInterface::QueryPerformanceCounter()
{
    LARGE_INTEGER ts;
    if (!::QueryPerformanceCounter(&ts))
    {
        ASSERT_UNCONDITIONALLY("Fatal Error - cannot query performance counter.");
        RhFailFast();
    }

    return ts.QuadPart;
}

// Get a frequency of the high precision performance counter
// Return:
//  The counter frequency
int64_t GCToOSInterface::QueryPerformanceFrequency()
{
    return g_performanceFrequency.QuadPart;
}

// Get a time stamp with a low precision
// Return:
//  Time stamp in milliseconds
uint32_t GCToOSInterface::GetLowPrecisionTimeStamp()
{
    return ::GetTickCount();
}

// Parameters of the GC thread stub
struct GCThreadStubParam
{
    GCThreadFunction GCThreadFunction;
    void* GCThreadParam;
};

// GC thread stub to convert GC thread function to an OS specific thread function
static DWORD GCThreadStub(void* param)
{
    GCThreadStubParam *stubParam = (GCThreadStubParam*)param;
    GCThreadFunction function = stubParam->GCThreadFunction;
    void* threadParam = stubParam->GCThreadParam;

    delete stubParam;

    function(threadParam);

    return 0;
}

// Create a new thread for GC use
// Parameters:
//  function - the function to be executed by the thread
//  param    - parameters of the thread
//  affinity - processor affinity of the thread
// Return:
//  true if it has succeeded, false if it has failed
bool GCToOSInterface::CreateThread(GCThreadFunction function, void* param, GCThreadAffinity* affinity)
{
    NewHolder<GCThreadStubParam> stubParam = new (nothrow) GCThreadStubParam();
    if (stubParam == NULL)
    {
        return false;
    }

    stubParam->GCThreadFunction = function;
    stubParam->GCThreadParam = param;

    DWORD thread_id;
    HANDLE gc_thread = ::CreateThread(0, 4096, GCThreadStub, stubParam.GetValue(), CREATE_SUSPENDED, &thread_id);

    if (!gc_thread)
    {
        return false;
    }

    stubParam.SuppressRelease();

    SetThreadPriority(gc_thread, /* THREAD_PRIORITY_ABOVE_NORMAL );*/ THREAD_PRIORITY_HIGHEST );

    if (affinity->Group != GCThreadAffinity::None)
    {
        // @TODO: CPUGroupInfo

        // ASSERT(affinity->Processor != GCThreadAffinity::None);
        // GROUP_AFFINITY ga;
        // ga.Group = (WORD)affinity->Group;
        // ga.Reserved[0] = 0;
        // ga.Reserved[1] = 0;
        // ga.Reserved[2] = 0;
        // ga.Mask = (size_t)1 << affinity->Processor;
        // CPUGroupInfo::SetThreadGroupAffinity(gc_thread, &ga, NULL);
    }
    else if (affinity->Processor != GCThreadAffinity::None)
    {
        SetThreadAffinityMask(gc_thread, (DWORD_PTR)1 << affinity->Processor);
    }

    ResumeThread(gc_thread);
    CloseHandle(gc_thread);

    return true;
}

// Initialize the critical section
void CLRCriticalSection::Initialize()
{
    InitializeCriticalSection(&m_cs);
}

// Destroy the critical section
void CLRCriticalSection::Destroy()
{
    DeleteCriticalSection(&m_cs);
}

// Enter the critical section. Blocks until the section can be entered.
void CLRCriticalSection::Enter()
{
    EnterCriticalSection(&m_cs);
}

// Leave the critical section
void CLRCriticalSection::Leave()
{
    LeaveCriticalSection(&m_cs);
}

#endif // RUNTIME_SERVICES_ONLY
