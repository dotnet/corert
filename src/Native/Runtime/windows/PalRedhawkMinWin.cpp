// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

#include "holder.h"

#define PalRaiseFailFastException RaiseFailFastException

uint32_t PalEventWrite(REGHANDLE arg1, const EVENT_DESCRIPTOR * arg2, uint32_t arg3, EVENT_DATA_DESCRIPTOR * arg4)
{
    return EventWrite(arg1, arg2, arg3, arg4);
}

#include "gcenv.h"


#define REDHAWK_PALEXPORT extern "C"
#define REDHAWK_PALAPI __stdcall

// Index for the fiber local storage of the attached thread pointer
static UInt32 g_flsIndex = FLS_OUT_OF_INDEXES;

static DWORD g_dwPALCapabilities;

GCSystemInfo g_RhSystemInfo;

bool InitializeSystemInfo()
{
    SYSTEM_INFO systemInfo;
    GetSystemInfo(&systemInfo);

    g_RhSystemInfo.dwNumberOfProcessors = systemInfo.dwNumberOfProcessors;
    g_RhSystemInfo.dwPageSize = systemInfo.dwPageSize;
    g_RhSystemInfo.dwAllocationGranularity = systemInfo.dwAllocationGranularity;

    return true;
}

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

// The Redhawk PAL must be initialized before any of its exports can be called. Returns true for a successful
// initialization and false on failure.
REDHAWK_PALEXPORT bool REDHAWK_PALAPI PalInit()
{
    g_dwPALCapabilities = WriteWatchCapability | LowMemoryNotificationCapability;

    // We use fiber detach callbacks to run our thread shutdown code because the fiber detach
    // callback is made without the OS loader lock
    g_flsIndex = FlsAlloc(FiberDetachCallback);
    if (g_flsIndex == FLS_OUT_OF_INDEXES)
    {
        return false;
    }

    return true;
}

// Given a mask of capabilities return true if all of them are supported by the current PAL.
REDHAWK_PALEXPORT bool REDHAWK_PALAPI PalHasCapability(PalCapability capability)
{
    return (g_dwPALCapabilities & (DWORD)capability) == (DWORD)capability;
}

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

#ifdef HOST_X86
    pCtx->IP = win32ctx.Eip;
    pCtx->Rsp = win32ctx.Esp;
    pCtx->Rbp = win32ctx.Ebp;
    pCtx->Rdi = win32ctx.Edi;
    pCtx->Rsi = win32ctx.Esi;
    pCtx->Rax = win32ctx.Eax;
    pCtx->Rbx = win32ctx.Ebx;
#elif defined(HOST_AMD64)
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
#elif defined(HOST_ARM)
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
#elif defined(HOST_ARM64)
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
    // CoreRT is not designed to be unloadable today. Use GET_MODULE_HANDLE_EX_FLAG_PIN to prevent
    // the module from ever unloading.

    HMODULE module;
    if (!GetModuleHandleExW(
        GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_PIN,
        (LPCWSTR)pointer,
        &module))
    {
        return NULL;
    }

    return (HANDLE)module;
}

REDHAWK_PALEXPORT bool REDHAWK_PALAPI PalIsAvxEnabled()
{
    typedef DWORD64(WINAPI* PGETENABLEDXSTATEFEATURES)();
    PGETENABLEDXSTATEFEATURES pfnGetEnabledXStateFeatures = NULL;

    HMODULE hMod = LoadLibraryExW(L"kernel32", NULL, LOAD_LIBRARY_SEARCH_SYSTEM32);
    if (hMod == NULL)
        return FALSE;

    pfnGetEnabledXStateFeatures = (PGETENABLEDXSTATEFEATURES)GetProcAddress(hMod, "GetEnabledXStateFeatures");

    if (pfnGetEnabledXStateFeatures == NULL)
    {
        return FALSE;
    }

    DWORD64 FeatureMask = pfnGetEnabledXStateFeatures();
    if ((FeatureMask & XSTATE_MASK_AVX) == 0)
    {
        return FALSE;
    }

    return TRUE;
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

#if defined(HOST_ARM64)

#include "IntrinsicConstants.h"

REDHAWK_PALIMPORT void REDHAWK_PALAPI PAL_GetCpuCapabilityFlags(int* flags)
{
    *flags = 0;

    // FP and SIMD support are enabled by default
    *flags |= ARM64IntrinsicConstants_ArmBase;
    *flags |= ARM64IntrinsicConstants_ArmBase_Arm64;
    *flags |= ARM64IntrinsicConstants_AdvSimd;
    *flags |= ARM64IntrinsicConstants_AdvSimd_Arm64;
    
    if (IsProcessorFeaturePresent(PF_ARM_V8_CRYPTO_INSTRUCTIONS_AVAILABLE))
    {
        *flags |= ARM64IntrinsicConstants_Aes;
        *flags |= ARM64IntrinsicConstants_Sha1;
        *flags |= ARM64IntrinsicConstants_Sha256;
    }

    if (IsProcessorFeaturePresent(PF_ARM_V8_CRC32_INSTRUCTIONS_AVAILABLE))
    {
        *flags |= ARM64IntrinsicConstants_Crc32;
        *flags |= ARM64IntrinsicConstants_Crc32_Arm64;
    }

    if (IsProcessorFeaturePresent(PF_ARM_V81_ATOMIC_INSTRUCTIONS_AVAILABLE))
    {
        *flags |= ARM64IntrinsicConstants_Atomics;
    }
}

#endif
