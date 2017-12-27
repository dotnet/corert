// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"
#include "rhassert.h"
#include "slist.h"
#include "gcrhinterface.h"
#include "varint.h"
#include "regdisplay.h"
#include "StackFrameIterator.h"
#include "thread.h"
#include "holder.h"
#include "Crst.h"
#include "event.h"
#include "RWLock.h"
#include "threadstore.h"
#include "threadstore.inl"
#include "RuntimeInstance.h"
#include "rhbinder.h"
#include "CachedInterfaceDispatch.h"
#include "RhConfig.h"
#include "stressLog.h"
#include "RestrictedCallouts.h"

#ifndef DACCESS_COMPILE

#ifdef PROFILE_STARTUP
unsigned __int64 g_startupTimelineEvents[NUM_STARTUP_TIMELINE_EVENTS] = { 0 };
#endif // PROFILE_STARTUP

#ifdef PLATFORM_UNIX
Int32 RhpHardwareExceptionHandler(UIntNative faultCode, UIntNative faultAddress, PAL_LIMITED_CONTEXT* palContext, UIntNative* arg0Reg, UIntNative* arg1Reg);
#else
Int32 __stdcall RhpVectoredExceptionHandler(PEXCEPTION_POINTERS pExPtrs);
#endif

static void CheckForPalFallback();
static void DetectCPUFeatures();

extern RhConfig * g_pRhConfig;

EXTERN_C bool g_fHasFastFxsave = false;

CrstStatic g_CastCacheLock;
CrstStatic g_ThunkPoolLock;

static bool InitDLL(HANDLE hPalInstance)
{
    CheckForPalFallback();

#ifdef FEATURE_CACHED_INTERFACE_DISPATCH
    //
    // Initialize interface dispatch.
    //
    if (!InitializeInterfaceDispatch())
        return false;
#endif

    //
    // Initialize support for registering GC and HandleTable callouts.
    //
    if (!RestrictedCallouts::Initialize())
        return false;

#if !defined(APP_LOCAL_RUNTIME) && !defined(USE_PORTABLE_HELPERS)
#ifndef PLATFORM_UNIX
    PalAddVectoredExceptionHandler(1, RhpVectoredExceptionHandler);
#else
    PalSetHardwareExceptionHandler(RhpHardwareExceptionHandler);
#endif
#endif // !APP_LOCAL_RUNTIME && !USE_PORTABLE_HELPERS

    //
    // init per-instance state
    //
    if (!RuntimeInstance::Initialize(hPalInstance))
        return false;

    STARTUP_TIMELINE_EVENT(NONGC_INIT_COMPLETE);

    RedhawkGCInterface::GCType gcType = g_pRhConfig->GetUseServerGC()
        ? RedhawkGCInterface::GCType_Server
        : RedhawkGCInterface::GCType_Workstation;

    if (!RedhawkGCInterface::InitializeSubsystems(gcType))
        return false;

    STARTUP_TIMELINE_EVENT(GC_INIT_COMPLETE);

#ifdef STRESS_LOG
    UInt32 dwTotalStressLogSize = g_pRhConfig->GetTotalStressLogSize();
    UInt32 dwStressLogLevel = g_pRhConfig->GetStressLogLevel();

    unsigned facility = (unsigned)LF_ALL;
    unsigned dwPerThreadChunks = (dwTotalStressLogSize / 24) / STRESSLOG_CHUNK_SIZE;
    if (dwTotalStressLogSize != 0)
    {
        StressLog::Initialize(facility, dwStressLogLevel, 
                              dwPerThreadChunks * STRESSLOG_CHUNK_SIZE, 
                              (unsigned)dwTotalStressLogSize, hPalInstance);
    }
#endif // STRESS_LOG

    DetectCPUFeatures();

    if (!g_CastCacheLock.InitNoThrow(CrstType::CrstCastCache))
        return false;

    if (!g_ThunkPoolLock.InitNoThrow(CrstType::CrstCastCache))
        return false;

    return true;
}

static void CheckForPalFallback()
{
#ifdef _DEBUG
    UInt32 disallowSetting = g_pRhConfig->GetDisallowRuntimeServicesFallback();
    if (disallowSetting == 0)
        return;

    // The fallback provider doesn't implement write watch, so we check for the write watch capability as a 
    // proxy for whether or not we're using the fallback provider since we don't have direct access to this 
    // information from here.

    if (disallowSetting == 1)
    {
        // If RH_DisallowRuntimeServicesFallback is set to 1, we want to fail fast if we discover that we're 
        // running against the fallback provider.  
        if (!PalHasCapability(WriteWatchCapability))
            RhFailFast();
    }
    else if (disallowSetting == 2)
    {
        // If RH_DisallowRuntimeServicesFallback is set to 2, we want to fail fast if we discover that we're 
        // NOT running against the fallback provider.  
        if (PalHasCapability(WriteWatchCapability))
            RhFailFast();
    }
#endif // _DEBUG
}

void DetectCPUFeatures()
{
#ifdef PROJECTN // @TODO: CORERT: DetectCPUFeatures

#ifdef _X86_
    // We depend on fxsave / fxrstor.  These were added to Pentium II and later, so they're pretty well guaranteed to be
    // available, but we double-check anyway and fail fast if they are not supported.
    CPU_INFO cpuInfo;
    PalCpuIdEx(1, 0, &cpuInfo);
    if (!(cpuInfo.Edx & X86_FXSR))  
        RhFailFast();
#endif

#ifdef _AMD64_
    // AMD has a "fast" mode for fxsave/fxrstor, which omits the saving of xmm registers.  The OS will enable this mode
    // if it is supported.  So if we continue to use fxsave/fxrstor, we must manually save/restore the xmm registers.
    CPU_INFO cpuInfo;
    PalCpuIdEx(0x80000001, 0, &cpuInfo);
    if (cpuInfo.Edx & AMD_FFXSR)
        g_fHasFastFxsave = true;
#endif

#endif // PROJECTN
}

#ifdef PROFILE_STARTUP
#define STD_OUTPUT_HANDLE ((UInt32)-11)

struct RegisterModuleTrace
{
    LARGE_INTEGER Begin;
    LARGE_INTEGER End;
};

const int NUM_REGISTER_MODULE_TRACES = 16;
int g_registerModuleCount = 0;

RegisterModuleTrace g_registerModuleTraces[NUM_REGISTER_MODULE_TRACES] = { 0 };

static void AppendInt64(char * pBuffer, UInt32* pLen, UInt64 value)
{
    char localBuffer[20];
    int cch = 0;

    do
    {
        localBuffer[cch++] = '0' + (value % 10);
        value = value / 10;
    } while (value);

    for (int i = 0; i < cch; i++)
    {
        pBuffer[(*pLen)++] = localBuffer[cch - i - 1];
    }

    pBuffer[(*pLen)++] = ',';
    pBuffer[(*pLen)++] = ' ';
}
#endif // PROFILE_STARTUP

static void UninitDLL()
{
#ifdef PROFILE_STARTUP
    char buffer[1024];

    UInt32 len = 0;

    AppendInt64(buffer, &len, g_startupTimelineEvents[PROCESS_ATTACH_BEGIN]);
    AppendInt64(buffer, &len, g_startupTimelineEvents[NONGC_INIT_COMPLETE]);
    AppendInt64(buffer, &len, g_startupTimelineEvents[GC_INIT_COMPLETE]);
    AppendInt64(buffer, &len, g_startupTimelineEvents[PROCESS_ATTACH_COMPLETE]);

    for (int i = 0; i < g_registerModuleCount; i++)
    {
        AppendInt64(buffer, &len, g_registerModuleTraces[i].Begin.QuadPart);
        AppendInt64(buffer, &len, g_registerModuleTraces[i].End.QuadPart);
    }

    buffer[len++] = '\n';

    fwrite(buffer, len, 1, stdout);
#endif // PROFILE_STARTUP
}

volatile bool g_processShutdownHasStarted = false;

static void DllThreadDetach()
{
    // BEWARE: loader lock is held here!

    // Should have already received a call to FiberDetach for this thread's "home" fiber.
    Thread* pCurrentThread = ThreadStore::GetCurrentThreadIfAvailable();
    if (pCurrentThread != NULL && !pCurrentThread->IsDetached())
    {
        // Once shutdown starts, RuntimeThreadShutdown callbacks are ignored, implying that
        // it is no longer guaranteed that exiting threads will be detached.
        if (!g_processShutdownHasStarted)
        {
            ASSERT_UNCONDITIONALLY("Detaching thread whose home fiber has not been detached");
            RhFailFast();
        }
    }
}

void RuntimeThreadShutdown(void* thread)
{
    // Note: loader lock is normally *not* held here!
    // The one exception is that the loader lock may be held during the thread shutdown callback
    // that is made for the single thread that runs the final stages of orderly process
    // shutdown (i.e., the thread that delivers the DLL_PROCESS_DETACH notifications when the
    // process is being torn down via an ExitProcess call).

    UNREFERENCED_PARAMETER(thread);

    ASSERT((Thread*)thread == ThreadStore::GetCurrentThread());

    if (!g_processShutdownHasStarted)
    {
        ThreadStore::DetachCurrentThread();
    }
}

COOP_PINVOKE_HELPER(UInt32_BOOL, RhpRegisterModule, (ModuleHeader *pModuleHeader))
{
#ifdef PROFILE_STARTUP
    if (g_registerModuleCount < NUM_REGISTER_MODULE_TRACES)
    {
        PalQueryPerformanceCounter(&g_registerModuleTraces[g_registerModuleCount].Begin);
    }
#endif // PROFILE_STARTUP

    RuntimeInstance * pInstance = GetRuntimeInstance();

    if (!pInstance->RegisterModule(pModuleHeader))
        return UInt32_FALSE;

#ifdef PROFILE_STARTUP
    if (g_registerModuleCount < NUM_REGISTER_MODULE_TRACES)
    {
        PalQueryPerformanceCounter(&g_registerModuleTraces[g_registerModuleCount].End);
        g_registerModuleCount++;
    }
#endif // PROFILE_STARTUP

    return UInt32_TRUE;
}

extern "C" bool RhInitialize()
{
    if (!PalInit())
        return false;

    if (!InitDLL(PalGetModuleHandleFromPointer((void*)&RhInitialize)))
        return false;

    return true;
}

COOP_PINVOKE_HELPER(void, RhpEnableConservativeStackReporting, ())
{
    GetRuntimeInstance()->EnableConservativeStackReporting();
}

//
// Currently called only from a managed executable once Main returns, this routine does whatever is needed to
// cleanup managed state before exiting. There's not a lot here at the moment since we're always about to let
// the OS tear the process down anyway. 
//
// @TODO: Eventually we'll probably have a hosting API and explicit shutdown request. When that happens we'll
// something more sophisticated here since we won't be able to rely on the OS cleaning up after us.
//
COOP_PINVOKE_HELPER(void, RhpShutdown, ())
{
#ifdef FEATURE_PROFILING
    GetRuntimeInstance()->WriteProfileInfo();
#endif // FEATURE_PROFILING
    // Indicate that runtime shutdown is complete and that the caller is about to start shutting down the entire process.
    g_processShutdownHasStarted = true;
}

#ifdef _WIN32
EXTERN_C UInt32_BOOL WINAPI RtuDllMain(HANDLE hPalInstance, UInt32 dwReason, void* /*pvReserved*/)
{
    switch (dwReason)
    {
    case DLL_PROCESS_ATTACH:
    {
        STARTUP_TIMELINE_EVENT(PROCESS_ATTACH_BEGIN);

        if (!InitDLL(hPalInstance))
            return FALSE;

        STARTUP_TIMELINE_EVENT(PROCESS_ATTACH_COMPLETE);
    }
    break;

    case DLL_PROCESS_DETACH:
        UninitDLL();
        break;

    case DLL_THREAD_DETACH:
        DllThreadDetach();
        break;
    }

    return TRUE;
}
#endif // _WIN32

#endif // !DACCESS_COMPILE
