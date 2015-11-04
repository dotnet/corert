//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"
#include "assert.h"
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

HANDLE RtuCreateRuntimeInstance(HANDLE hPalInstance);

#ifdef FEATURE_VSD
bool RtuInitializeVSD();
#endif

UInt32 _fls_index = FLS_OUT_OF_INDEXES;


Int32 __stdcall RhpVectoredExceptionHandler(PEXCEPTION_POINTERS pExPtrs);
void __stdcall FiberDetach(void* lpFlsData);
void CheckForPalFallback();

extern RhConfig * g_pRhConfig;

bool InitDLL(HANDLE hPalInstance)
{
    CheckForPalFallback();

#ifdef FEATURE_VSD
    //
    // init VSD
    //
    if (!RtuInitializeVSD())
        return false;
#endif

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

#ifndef APP_LOCAL_RUNTIME
    PalAddVectoredExceptionHandler(1, RhpVectoredExceptionHandler);
#endif

    //
    // init per-instance state
    //
    HANDLE hRuntimeInstance = RtuCreateRuntimeInstance(hPalInstance);
    if (NULL == hRuntimeInstance)
        return false;
    STARTUP_TIMELINE_EVENT(NONGC_INIT_COMPLETE);

    _fls_index = PalFlsAlloc(FiberDetach);
    if (_fls_index == FLS_OUT_OF_INDEXES)
        return false;

    // @TODO: currently we're always forcing a workstation GC.
    // @TODO: GC per-instance vs per-DLL state separation
    if (!RedhawkGCInterface::InitializeSubsystems(RedhawkGCInterface::GCType_Workstation))
        return false;
    STARTUP_TIMELINE_EVENT(GC_INIT_COMPLETE);

#ifdef STRESS_LOG
    UInt32 dwTotalStressLogSize = g_pRhConfig->GetTotalStressLogSize();
    UInt32 dwStressLogLevel = g_pRhConfig->GetStressLogLevel();

    unsigned facility = (unsigned)LF_ALL;
#ifdef _DEBUG
    if (dwTotalStressLogSize == 0)
        dwTotalStressLogSize = 1024 * STRESSLOG_CHUNK_SIZE;
    if (dwStressLogLevel == 0)
        dwStressLogLevel = LL_INFO1000;
#endif
    unsigned dwPerThreadChunks = (dwTotalStressLogSize / 24) / STRESSLOG_CHUNK_SIZE;
    if (dwTotalStressLogSize != 0)
    {
        StressLog::Initialize(facility, dwStressLogLevel, 
                              dwPerThreadChunks * STRESSLOG_CHUNK_SIZE, 
                              (unsigned)dwTotalStressLogSize, hPalInstance);
    }
#endif // STRESS_LOG

    return true;
}

void CheckForPalFallback()
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

void AppendInt64(char * pBuffer, UInt32* pLen, UInt64 value)
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

bool UninitDLL(HANDLE /*hModDLL*/)
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

    buffer[len++] = '\r';
    buffer[len++] = '\n';

    UInt32 cchWritten;
    PalWriteFile(PalGetStdHandle(STD_OUTPUT_HANDLE), buffer, len, &cchWritten, NULL);
#endif // PROFILE_STARTUP
    return true;
}

void DllThreadAttach(HANDLE /*hPalInstance*/)
{
    // We do not call ThreadStore::AttachThread from here because the loader lock is held.  Instead, the 
    // threads themselves will do this on their first reverse pinvoke.
}

void DllThreadDetach()
{
    // BEWARE: loader lock is held here!

    // Should have already received a call to FiberDetach for this thread's "home" fiber.
    Thread* pCurrentThread = ThreadStore::GetCurrentThreadIfAvailable();
    if (pCurrentThread != NULL && !pCurrentThread->IsDetached())
    {
        ASSERT_UNCONDITIONALLY("Detaching thread whose home fiber has not been detached");
        RhFailFast();
    }
}

void __stdcall FiberDetach(void* lpFlsData)
{
    // Note: loader lock is *not* held here!
    UNREFERENCED_PARAMETER(lpFlsData);
    ASSERT(lpFlsData == PalFlsGetValue(_fls_index));

    ThreadStore::DetachCurrentThreadIfHomeFiber();
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


COOP_PINVOKE_HELPER(UInt32_BOOL, RhpRegisterSimpleModule, (SimpleModuleHeader *pModuleHeader))
{
    RuntimeInstance * pInstance = GetRuntimeInstance();

    if (!pInstance->RegisterSimpleModule(pModuleHeader))
        return UInt32_FALSE;

    return UInt32_TRUE;
}

COOP_PINVOKE_HELPER(UInt32_BOOL, RhpEnableConservativeStackReporting, ())
{
    RuntimeInstance * pInstance = GetRuntimeInstance();
    if (!pInstance->EnableConservativeStackReporting())
        return UInt32_FALSE;

    return UInt32_TRUE;
}

#endif // !DACCESS_COMPILE

GPTR_IMPL_INIT(RuntimeInstance, g_pTheRuntimeInstance, NULL);

#ifndef DACCESS_COMPILE

//
// Creates a new runtime instance.
//
// @TODO: EXPORT
HANDLE RtuCreateRuntimeInstance(HANDLE hPalInstance)
{
    CreateHolder<RuntimeInstance> pRuntimeInstance = RuntimeInstance::Create(hPalInstance);
    if (NULL == pRuntimeInstance)
        return NULL;

    ASSERT_MSG(g_pTheRuntimeInstance == NULL, "multi-instances are not supported");
    g_pTheRuntimeInstance = pRuntimeInstance;

    pRuntimeInstance.SuppressRelease();
    return (HANDLE) pRuntimeInstance;
}

//
// Currently called only from a managed executable once Main returns, this routine does whatever is needed to
// cleanup managed state before exiting. There's not a lot here at the moment since we're always about to let
// the OS tear the process down anyway. 
//
// @TODO: Eventually we'll probably have a hosting API and explicit shutdown request. When that happens we'll
// something more sophisticated here since we won't be able to rely on the OS cleaning up after us.
//
COOP_PINVOKE_HELPER(void, RhpShutdownHelper, (UInt32 /*uExitCode*/))
{
    // If the classlib has requested it perform a last pass of the finalizer thread.
    RedhawkGCInterface::ShutdownFinalization();

#ifdef FEATURE_PROFILING
    GetRuntimeInstance()->WriteProfileInfo();
#endif // FEATURE_PROFILING
}

#endif // !DACCESS_COMPILE

