//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

//
// Implementation of the Redhawk Platform Abstraction Layer (PAL) library when Unix is the platform. 
//

#include <stdio.h>
#include <errno.h>
#include <cwchar>
#include "CommonTypes.h"
#include <PalRedhawkCommon.h>
#include "CommonMacros.h"
#include <sal.h>
#include "assert.h"
#include "config.h"
#include "UnixHandle.h"

#include <unistd.h>
#include <sched.h>
#include <sys/mman.h>
#include <pthread.h>
#include <sys/types.h>
#include <iconv.h>
#include <dlfcn.h>
#include <dirent.h>
#include <string.h>
#include <ctype.h>
#include <sys/stat.h>
#include <fcntl.h>
#include <sys/time.h>

#if !HAVE_SYSCONF && !HAVE_SYSCTL
#error Neither sysconf nor sysctl is present on the current system
#endif

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

#if HAVE_MACH_ABSOLUTE_TIME
#include <mach/mach_time.h>
static mach_timebase_info_data_t s_TimebaseInfo;
#endif

using std::nullptr_t;

#include "gcenv.structs.h"

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

typedef void * LPSECURITY_ATTRIBUTES;
typedef void* PCONTEXT;
typedef void* PEXCEPTION_RECORD;
typedef void* PEXCEPTION_POINTERS;

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

static const int tccSecondsToMilliSeconds = 1000;
static const int tccSecondsToMicroSeconds = 1000000;
static const int tccSecondsToNanoSeconds = 1000000000;
static const int tccMilliSecondsToMicroSeconds = 1000;
static const int tccMilliSecondsToNanoSeconds = 1000000;
static const int tccMicroSecondsToNanoSeconds = 1000;

static const uint32_t INFINITE = 0xFFFFFFFF;

extern "C" UInt32 __stdcall NtGetCurrentProcessorNumber();

static uint32_t g_dwPALCapabilities;
static UInt32 g_cLogicalCpus = 0;
static size_t g_cbLargestOnDieCache = 0;
static size_t g_cbLargestOnDieCacheAdjusted = 0;

extern bool PalQueryProcessorTopology();
REDHAWK_PALEXPORT void __cdecl PalPrintf(_In_z_ _Printf_format_string_ const char * szFormat, ...);

void TimeSpecAdd(timespec* time, uint32_t milliseconds)
{
    time->tv_nsec += milliseconds * tccMilliSecondsToNanoSeconds;
    if (time->tv_nsec > tccSecondsToNanoSeconds)
    {
        time->tv_sec += (time->tv_nsec - tccSecondsToNanoSeconds) / tccSecondsToNanoSeconds;
        time->tv_nsec %= tccSecondsToNanoSeconds;
    }
}

class UnixEvent
{
    pthread_cond_t m_condition;
    pthread_mutex_t m_mutex;
    bool m_manualReset;
    bool m_state;

    void Update(bool state)
    {
        pthread_mutex_lock(&m_mutex);
        m_state = state;
        // Unblock all threads waiting for the condition variable
        pthread_cond_broadcast(&m_condition);
        pthread_mutex_unlock(&m_mutex);
    }

public:

    UnixEvent(bool manualReset, bool initialState)
    : m_manualReset(manualReset),
      m_state(initialState)
    {
        int st = pthread_mutex_init(&m_mutex, NULL);
        ASSERT(st == NULL);

        pthread_condattr_t attrs;
        st = pthread_condattr_init(&attrs);
        ASSERT(st == NULL);

#if HAVE_CLOCK_MONOTONIC
        // Ensure that the pthread_cond_timedwait will use CLOCK_MONOTONIC
        st = pthread_condattr_setclock(&attrs, CLOCK_MONOTONIC);
        ASSERT(st == NULL);
#endif // HAVE_CLOCK_MONOTONIC

        st = pthread_cond_init(&m_condition, &attrs);
        ASSERT(st == NULL);

        st = pthread_condattr_destroy(&attrs);
        ASSERT(st == NULL);
    }

    ~UnixEvent()
    {
        int st = pthread_mutex_destroy(&m_mutex);
        ASSERT(st == NULL);

        st = pthread_cond_destroy(&m_condition);
        ASSERT(st == NULL);
    }

    uint32_t Wait(uint32_t milliseconds)
    {
        timespec endTime;

        if (milliseconds != INFINITE)
        {
#if HAVE_CLOCK_MONOTONIC
            clock_gettime(CLOCK_MONOTONIC, &endTime);
            TimeSpecAdd(&endTime, milliseconds);
#else // HAVE_CLOCK_MONOTONIC
            // TODO: fix this. The time of day can be changed by the user and then the timeout
            // would change. So we will need to use pthread_cond_timedwait_relative_np and
            // update the relative time each time pthread_cond_timedwait gets waked.
            // on OSX and other systems that don't support the monotonic clock
            timeval now;
            gettimeofday(&now, NULL);
            endTime.tv_sec = now.tv_sec;
            endTime.tv_nsec = now.tv_usec * tccMicroSecondsToNanoSeconds;
            TimeSpecAdd(&endTime, milliseconds);
#endif // HAVE_CLOCK_MONOTONIC
        }

        int st = 0;

        pthread_mutex_lock(&m_mutex);
        while (!m_state)
        {
            if (milliseconds == INFINITE)
            {
                st = pthread_cond_wait(&m_condition, &m_mutex);
            }
            else
            {
                st = pthread_cond_timedwait(&m_condition, &m_mutex, &endTime);
            }

            if (st != 0)
            {
                // wait failed or timed out
                break;
            }

        }
        pthread_mutex_unlock(&m_mutex);

        uint32_t waitStatus;

        if (st == 0)
        {
            waitStatus = WAIT_OBJECT_0;
        }
        else if (st == ETIMEDOUT)
        {
            waitStatus = WAIT_TIMEOUT;
        }
        else
        {
            waitStatus = WAIT_FAILED;
        }

        return waitStatus;
    }

    void Set()
    {
        Update(true);
    }

    void Reset()
    {
        Update(false);
    }
};

class UnixMutex
{
    pthread_mutex_t m_mutex;

public:

    UnixMutex()
    {
        int st = pthread_mutex_init(&m_mutex, NULL);
        ASSERT(st == NULL);
    }

    ~UnixMutex()
    {
        int st = pthread_mutex_destroy(&m_mutex);
        ASSERT(st == NULL);
    }

    bool Release()
    {
        return pthread_mutex_unlock(&m_mutex) == 0;
    }

    uint32_t Wait(uint32_t milliseconds)
    {
        // TODO: implement timed wait if needed
        ASSERT(milliseconds == INFINITE);
        int st = pthread_mutex_lock(&m_mutex);
        return (st == 0) ? WAIT_OBJECT_0 : WAIT_FAILED;
    }
};

typedef UnixHandle<UnixHandleType::Event, UnixEvent> EventUnixHandle;
typedef UnixHandle<UnixHandleType::Thread, pthread_t> ThreadUnixHandle;
typedef UnixHandle<UnixHandleType::Mutex, UnixMutex> MutexUnixHandle;

// The Redhawk PAL must be initialized before any of its exports can be called. Returns true for a successful
// initialization and false on failure.
__attribute__((constructor))
bool PalInit()
{
    g_dwPALCapabilities = GetCurrentProcessorNumberCapability;

    if (!PalQueryProcessorTopology())
        return false;

#if HAVE_MACH_ABSOLUTE_TIME
    kern_return_t machRet;
    if ((machRet = mach_timebase_info(&s_TimebaseInfo)) != KERN_SUCCESS)
    {
        return false;
    }
#endif

    return true;
}

// Given a mask of capabilities return true if all of them are supported by the current PAL.
REDHAWK_PALEXPORT bool REDHAWK_PALAPI PalHasCapability(PalCapability capability)
{
    return (g_dwPALCapabilities & (uint32_t)capability) == (uint32_t)capability;
}

static const char* const WCharEncoding = "UTF-32LE";

int UTF8ToWideChar(char* bytes, int len, wchar_t* buffer, int bufLen)
{
    iconv_t cd = iconv_open(WCharEncoding, "UTF-8");
    if (cd == (iconv_t)-1)
    {
        fprintf(stderr, "iconv_open failed with %d\n", errno);
        return 0;
    }

    char* inbuf = bytes;
    char* outbuf = (char*)buffer;
    size_t inbufbytesleft = len;
    size_t outbufbytesleft = bufLen;

    int rc = iconv(cd, &inbuf, &inbufbytesleft, &outbuf, &outbufbytesleft);
    if (rc == -1)
    {
        fprintf(stderr, "iconv_open failed with %d\n", errno);
        return 0;
    }

    iconv_close(cd);

    return (bufLen - outbufbytesleft) / sizeof(wchar_t);
}

int WideCharToUTF8(wchar_t* chars, int len, char* buffer, int bufLen)
{
    iconv_t cd = iconv_open("UTF-8", WCharEncoding);
    if (cd == (iconv_t)-1)
    {
        fprintf(stderr, "iconv_open failed with %d\n", errno);
        return 0;
    }

    char* inbuf = (char*)chars;
    char* outbuf = buffer;
    size_t inbufbytesleft = len;
    size_t outbufbytesleft = bufLen;

    int rc = iconv(cd, &inbuf, &inbufbytesleft, &outbuf, &outbufbytesleft);
    if (rc == -1)
    {
        fprintf(stderr, "iconv_open failed with %d\n", errno);
        return 0;
    }

    iconv_close(cd);

    return bufLen - outbufbytesleft;
}

REDHAWK_PALEXPORT unsigned int REDHAWK_PALAPI PalGetCurrentProcessorNumber()
{
#ifdef __LINUX__
    int processorNumber = sched_getcpu();
    ASSERT(processorNumber != -1);

    return (unsigned int)processorNumber;
#else
    // TODO: implement for OSX / FreeBSD
    return 0;
#endif
}

REDHAWK_PALEXPORT UInt32_BOOL REDHAWK_PALAPI PalAllocateThunksFromTemplate(HANDLE hTemplateModule, uint32_t templateRva, size_t templateSize, void** newThunksOut)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

REDHAWK_PALEXPORT UInt32_BOOL REDHAWK_PALAPI PalGlobalMemoryStatusEx(_Inout_ GCMemoryStatus* pBuffer)
{
    pBuffer->dwMemoryLoad = 0;
    pBuffer->ullTotalPhys = 0;
    pBuffer->ullAvailPhys = 0;
    pBuffer->ullTotalPageFile = 0;
    pBuffer->ullAvailPageFile = 0;
    pBuffer->ullTotalVirtual = 0;
    pBuffer->ullAvailVirtual = 0;

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
#if HAVE_CLOCK_MONOTONIC
    timespec endTime;
    clock_gettime(CLOCK_MONOTONIC, &endTime);
    TimeSpecAdd(&endTime, milliseconds);
    while (clock_nanosleep(CLOCK_MONOTONIC, TIMER_ABSTIME, &endTime, NULL) == EINTR)
    {
    }
#else // HAVE_CLOCK_MONOTONIC
    timespec requested;
    requested.tv_sec = milliseconds / tccSecondsToMilliSeconds;
    requested.tv_nsec = (milliseconds - requested.tv_sec * tccSecondsToMilliSeconds) * tccMilliSecondsToNanoSeconds;

    timespec remaining;
    while (nanosleep(&requested, &remaining) == EINTR)
    {
        requested = remaining;
    }
#endif // HAVE_CLOCK_MONOTONIC
}

REDHAWK_PALEXPORT UInt32_BOOL REDHAWK_PALAPI __stdcall PalSwitchToThread()
{
    // sched_yield yields to another thread in the current process. This implementation 
    // won't work well for cross-process synchronization.
    return sched_yield() == 0;
}

extern "C" UInt32_BOOL CloseHandle(HANDLE handle)
{
    UnixHandleBase* handleBase = (UnixHandleBase*)handle;

    delete handleBase;

    return UInt32_TRUE;
}

REDHAWK_PALEXPORT HANDLE REDHAWK_PALAPI PalCreateMutexW(_In_opt_ LPSECURITY_ATTRIBUTES pMutexAttributes, UInt32_BOOL initialOwner, _In_opt_z_ const wchar_t* pName)
{
    return new MutexUnixHandle(UnixMutex());
}


REDHAWK_PALEXPORT HANDLE REDHAWK_PALAPI PalCreateEventW(_In_opt_ LPSECURITY_ATTRIBUTES pEventAttributes, UInt32_BOOL manualReset, UInt32_BOOL initialState, _In_opt_z_ const wchar_t* pName)
{
    return new EventUnixHandle(UnixEvent(manualReset, initialState));
}

// This is not needed in the PAL
#if 0
REDHAWK_PALEXPORT _Success_(return) bool REDHAWK_PALAPI PalGetThreadContext(HANDLE hThread, _Out_ PAL_LIMITED_CONTEXT * pCtx)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}
#endif

typedef UInt32(__stdcall *BackgroundCallback)(_In_opt_ void* pCallbackContext);

REDHAWK_PALEXPORT bool REDHAWK_PALAPI PalStartBackgroundWork(_In_ BackgroundCallback callback, _In_opt_ void* pCallbackContext, UInt32_BOOL highPriority)
{
    pthread_attr_t attrs;

    int st = pthread_attr_init(&attrs);
    ASSERT(st == 0);

    static const int NormalPriority = 0;
    static const int HighestPriority = -20;

    // TODO: Figure out which scheduler to use, the default one doesn't seem to
    // support per thread priorities.
#if 0
    sched_param params;
    memset(&params, 0, sizeof(params));

    params.sched_priority = highPriority ? HighestPriority : NormalPriority;

    // Set the priority of the thread
    st = pthread_attr_setschedparam(&attrs, &params);
    ASSERT(st == 0);
#endif
    // Create the thread as detached, that means not joinable
    st = pthread_attr_setdetachstate(&attrs, PTHREAD_CREATE_DETACHED);
    ASSERT(st == 0);

    pthread_t threadId;
    st = pthread_create(&threadId, &attrs, (void *(*)(void*))callback, pCallbackContext);

    int st2 = pthread_attr_destroy(&attrs);
    ASSERT(st2 == 0);

    return st == 0;
}

REDHAWK_PALEXPORT bool REDHAWK_PALAPI PalStartBackgroundGCThread(_In_ BackgroundCallback callback, _In_opt_ void* pCallbackContext)
{
    return PalStartBackgroundWork(callback, pCallbackContext, UInt32_FALSE);
}

REDHAWK_PALEXPORT bool REDHAWK_PALAPI PalStartFinalizerThread(_In_ BackgroundCallback callback, _In_opt_ void* pCallbackContext)
{
    return PalStartBackgroundWork(callback, pCallbackContext, UInt32_TRUE);
}

// Returns a 64-bit tick count with a millisecond resolution. It tries its best
// to return monotonically increasing counts and avoid being affected by changes
// to the system clock (either due to drift or due to explicit changes to system
// time).
REDHAWK_PALEXPORT UInt64 REDHAWK_PALAPI GetTickCount64()
{
    UInt64 retval = 0;

#if HAVE_CLOCK_MONOTONIC
    {
        struct timespec ts;
        if (clock_gettime(CLOCK_MONOTONIC, &ts) == 0)
        {
            retval = (ts.tv_sec * tccSecondsToMilliSeconds) + (ts.tv_nsec / tccMilliSecondsToNanoSeconds);
        }
        else
        {
            ASSERT_UNCONDITIONALLY("clock_gettime(CLOCK_MONOTONIC) failed\n");
        }
    }
#elif HAVE_MACH_ABSOLUTE_TIME
    {
        // use denom == 0 to indicate that s_TimebaseInfo is uninitialised.
        if (s_TimebaseInfo.denom != 0)
        {
            retval = (mach_absolute_time() * s_TimebaseInfo.numer / s_TimebaseInfo.denom) / tccMilliSecondsToNanoSeconds;
        }
        else
        {
            ASSERT_UNCONDITIONALLY("s_TimebaseInfo is uninitialized.\n");
        }
    }
#elif HAVE_GETHRTIME
    {
        retval = (UInt64)(gethrtime() / tccMilliSecondsToNanoSeconds);
    }
#elif HAVE_READ_REAL_TIME
    {
        timebasestruct_t tb;
        read_real_time(&tb, TIMEBASE_SZ);
        if (time_base_to_time(&tb, TIMEBASE_SZ) == 0)
        {
            retval = (tb.tb_high * tccSecondsToMilliSeconds)+(tb.tb_low / tccMilliSecondsToNanoSeconds);
        }
        else
        {
            ASSERT_UNCONDITIONALLY("time_base_to_time() failed\n");
        }
    }
#else
    {
        struct timeval tv;    
        if (gettimeofday(&tv, NULL) == 0)
        {
            retval = (tv.tv_sec * tccSecondsToMilliSeconds) + (tv.tv_usec / tccMilliSecondsToMicroSeconds);

        }
        else
        {
            ASSERT_UNCONDITIONALLY("gettimeofday() failed\n");
        }
    }
#endif // HAVE_CLOCK_MONOTONIC 

    return retval;    
}

REDHAWK_PALEXPORT uint32_t REDHAWK_PALAPI PalGetTickCount()
{
    return (uint32_t)GetTickCount64();
}

#if 0
REDHAWK_PALEXPORT UInt32_BOOL REDHAWK_PALAPI PalEventEnabled(REGHANDLE regHandle, _In_ const EVENT_DESCRIPTOR* eventDescriptor)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}
#endif

REDHAWK_PALEXPORT HANDLE REDHAWK_PALAPI PalCreateFileW(_In_z_ const WCHAR* pFileName, uint32_t desiredAccess, uint32_t shareMode, _In_opt_ LPSECURITY_ATTRIBUTES pSecurityAttributes, uint32_t creationDisposition, uint32_t flagsAndAttributes, HANDLE hTemplateFile)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

REDHAWK_PALEXPORT _Success_(return == 0)
uint32_t REDHAWK_PALAPI PalGetWriteWatch(_In_ uint32_t flags, _In_ void* pBaseAddress, _In_ size_t regionSize, _Out_writes_to_opt_(*pCount, *pCount) void** pAddresses, _Inout_opt_ uintptr_t* pCount, _Out_opt_ uint32_t* pGranularity)
{
    // There is no write watching feature available on Unix other than a possibility to emulate 
    // it using read only pages and page fault handler. 
    *pAddresses = NULL;
    *pCount = 0;
    // Return non-zero value as an indicator of failure
    return 1;
}

REDHAWK_PALEXPORT uint32_t REDHAWK_PALAPI PalResetWriteWatch(_In_ void* pBaseAddress, size_t regionSize)
{
    // There is no write watching feature available on Unix.
    // Return non-zero value as an indicator of failure. 
    return 1;
}

REDHAWK_PALEXPORT HANDLE REDHAWK_PALAPI PalGetModuleHandleFromPointer(_In_ void* pointer)
{
    HANDLE moduleHandle = NULL;
    Dl_info info;
    int st = dladdr(pointer, &info);
    if (st != 0)
    {
        moduleHandle = info.dli_fbase;
    }

    return moduleHandle;
}

REDHAWK_PALEXPORT void* REDHAWK_PALAPI PalAddVectoredExceptionHandler(uint32_t firstHandler, _In_ PVECTORED_EXCEPTION_HANDLER vectoredHandler)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

bool QueryCacheSize()
{
    bool success = true;
    g_cbLargestOnDieCache = 0;

#ifdef __LINUX__
    DIR* cpuDir = opendir("/sys/devices/system/cpu");
    if (cpuDir == nullptr)
    {
        ASSERT_UNCONDITIONALLY("opendir on /sys/devices/system/cpu failed\n");        
        return false;
    }

    dirent* cpuEntry;
    // Process entries starting with "cpu" (cpu0, cpu1, ...) in the directory
    while (success && (cpuEntry = readdir(cpuDir)) != nullptr)
    {
        if ((strncmp(cpuEntry->d_name, "cpu", 3) == 0) && isdigit(cpuEntry->d_name[3]))
        {
            char cpuCachePath[64] = "/sys/devices/system/cpu/";
            strcat(cpuCachePath, cpuEntry->d_name);
            strcat(cpuCachePath, "/cache");
            DIR* cacheDir = opendir(cpuCachePath);
            if (cacheDir == nullptr)
            {
                success = false;
                break;
            }

            strcat(cpuCachePath, "/");
            int cpuCacheBasePathLength = strlen(cpuCachePath);

            dirent* cacheEntry;
            // For all entries in the directory
            while ((cacheEntry = readdir(cacheDir)) != nullptr)
            {
                if (strncmp(cacheEntry->d_name, "index", 5) == 0)
                {
                    cpuCachePath[cpuCacheBasePathLength] = '\0';
                    strcat(cpuCachePath, cacheEntry->d_name);
                    strcat(cpuCachePath, "/size");

                    int fd = open(cpuCachePath, O_RDONLY);
                    if (fd < 0)
                    {
                        success = false;
                        break;
                    }

                    char cacheSizeStr[16];
                    int bytesRead = read(fd, cacheSizeStr, sizeof(cacheSizeStr) - 1);
                    cacheSizeStr[bytesRead] = '\0';

                    // Parse the cache size that is formatted as a number followed by the K letter
                    char* lastChar;
                    int cacheSize = strtol(cacheSizeStr, &lastChar, 10) * 1024;
                    ASSERT(*lastChar == 'K');
                    g_cbLargestOnDieCache = max(g_cbLargestOnDieCache, cacheSize);

                    close(fd);
                }
            }

            closedir(cacheDir);
        }
    }
    closedir(cpuDir);

#elif HAVE_SYSCTL

    int64_t g_cbLargestOnDieCache;
    size_t sz = sizeof(g_cbLargestOnDieCache);

    if (sysctlbyname("hw.l3cachesize", &g_cbLargestOnDieCache, &sz, NULL, 0) != 0)
    {
        // No L3 cache, try the L2 one
        if (sysctlbyname("hw.l2cachesize", &g_cbLargestOnDieCache, &sz, NULL, 0) != 0)
        {
            // No L2 cache, try the L1 one
            if (sysctlbyname("hw.l1dcachesize", &g_cbLargestOnDieCache, &sz, NULL, 0) != 0)
            {
                ASSERT_UNCONDITIONALLY("sysctl failed to get cache size\n");
                return false;
            }
        }
    }
#else
#error Don't know how to get cache size on this platform
#endif // __LINUX__

    // TODO: implement adjusted cache size
    g_cbLargestOnDieCacheAdjusted = g_cbLargestOnDieCache;

    return success;
}

bool QueryLogicalProcessorCount()
{
#if HAVE_SYSCONF
    g_cLogicalCpus = sysconf(_SC_NPROCESSORS_ONLN);
    if (g_cLogicalCpus < 1)
    {
        ASSERT_UNCONDITIONALLY("sysconf failed for _SC_NPROCESSORS_ONLN\n");
        return false;
    }
#elif HAVE_SYSCTL
    size_t sz = sizeof(g_cLogicalCpus);

    int st = 0;
    if (sysctlbyname("hw.logicalcpu_max", &g_cLogicalCpus, &sz, NULL, 0) != 0)
    {
        ASSERT_UNCONDITIONALLY("sysctl failed for hw.logicalcpu_max\n");
        return false;
    }

#endif // HAVE_SYSCONF

    return true;
}

// Method used to initialize the above values.
bool PalQueryProcessorTopology()
{
    if (!QueryLogicalProcessorCount())
    {
        return false;
    }

    return QueryCacheSize();
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
        // For Windows compatibility, let the PalVirtualAlloc reserve memory with 64k alignment.
        static const size_t Alignment = 64 * 1024;

        size_t alignedSize = size + (Alignment - OS_PAGE_SIZE);

        void * pRetVal = mmap(pAddress, alignedSize, unixProtect, MAP_ANON | MAP_PRIVATE, -1, 0);

        if (pRetVal != NULL)
        {
            void * pAlignedRetVal = (void *)(((size_t)pRetVal + (Alignment - 1)) & ~(Alignment - 1));
            size_t startPadding = (size_t)pAlignedRetVal - (size_t)pRetVal;
            if (startPadding != 0)
            {
                int ret = munmap(pRetVal, startPadding);
                ASSERT(ret == 0);
            }

            size_t endPadding = alignedSize - (startPadding + size);
            if (endPadding != 0)
            {
                int ret = munmap((void *)((size_t)pAlignedRetVal + size), endPadding);
                ASSERT(ret == 0);
            }

            pRetVal = pAlignedRetVal;
        }
         
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
    static void* pBuffer;
    return _InterlockedExchangePointer(&pBuffer, pNewBuffer);
}

extern "C" HANDLE GetCurrentProcess()
{
    return (HANDLE)-1;
}

extern "C" HANDLE GetCurrentThread()
{
    return (HANDLE)-2;
}

extern "C" UInt32_BOOL DuplicateHandle(
    HANDLE hSourceProcessHandle,
    HANDLE hSourceHandle,
    HANDLE hTargetProcessHandle,
    HANDLE * lpTargetHandle,
    UInt32 dwDesiredAccess,
    UInt32_BOOL bInheritHandle,
    UInt32 dwOptions)
{
    // We can only duplicate the current thread handle. That is all that the MRT uses.
    ASSERT(hSourceProcessHandle == GetCurrentProcess());
    ASSERT(hTargetProcessHandle == GetCurrentProcess());
    ASSERT(hSourceHandle == GetCurrentThread());
    *lpTargetHandle = new ThreadUnixHandle(pthread_self());

    return lpTargetHandle != nullptr;
}

extern "C" UInt32_BOOL InitializeCriticalSection(CRITICAL_SECTION * lpCriticalSection)
{
    return pthread_mutex_init(&lpCriticalSection->mutex, NULL) == 0;
}

extern "C" UInt32_BOOL InitializeCriticalSectionEx(CRITICAL_SECTION * lpCriticalSection, UInt32 arg2, UInt32 arg3)
{
    return InitializeCriticalSection(lpCriticalSection);
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
    // Abort aborts the process and causes creation of a crash dump
    abort();
}

typedef void(__stdcall *PFLS_CALLBACK_FUNCTION) (void* lpFlsData);

extern "C" UInt32 FlsAlloc(PFLS_CALLBACK_FUNCTION arg1)
{
    // UNIXTODO: The FLS stuff needs to be abstracted, the only usage of the FLS is to get a callback
    // when a thread is terminating.
    return 0;
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
    // UNIXTODO: The TLS stuff needs to be better abstracted.
    return 0;
}

extern "C" UInt32_BOOL IsDebuggerPresent()
{
    // UNIXTODO: Implement this function
    return UInt32_FALSE;
}

extern "C" void TerminateProcess(HANDLE arg1, UInt32 arg2)
{
    // TODO: change it to TerminateCurrentProcess
    // Then if we modified the signature of the DuplicateHandle too, we can
    // get rid of the PalGetCurrentProcess.
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

extern "C" void ExitProcess(UInt32 exitCode)
{
    exit(exitCode);
}

extern "C" UInt32_BOOL SetEvent(HANDLE event)
{
    EventUnixHandle* unixHandle = (EventUnixHandle*)event;
    unixHandle->GetObject()->Set();

    return UInt32_TRUE;
}

extern "C" UInt32_BOOL ResetEvent(HANDLE event)
{
    EventUnixHandle* unixHandle = (EventUnixHandle*)event;
    unixHandle->GetObject()->Reset();

    return UInt32_TRUE;
}

extern "C" UInt32 GetEnvironmentVariableW(const wchar_t* pName, wchar_t* pBuffer, UInt32 size)
{
    // UNIXTODO: Implement this function
    *pBuffer = '\0';
    return 0;
}

extern "C" UInt16 RtlCaptureStackBackTrace(UInt32 arg1, UInt32 arg2, void* arg3, UInt32* arg4)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

extern "C" HANDLE GetProcessHeap()
{
    // UNIXTODO: Consider using some special value?
    return (HANDLE)1;
}

extern "C" void* HeapAlloc(HANDLE heap, UInt32 flags, UIntNative bytes)
{
    return malloc(bytes);
}

extern "C" UInt32_BOOL HeapFree(HANDLE heap, UInt32 flags, void * mem)
{
    free(mem);

    return UInt32_TRUE;
}

typedef UInt32 (__stdcall *HijackCallback)(HANDLE hThread, _In_ PAL_LIMITED_CONTEXT* pThreadContext, _In_opt_ void* pCallbackContext);

REDHAWK_PALEXPORT uint32_t REDHAWK_PALAPI PalHijack(HANDLE hThread, _In_ HijackCallback callback, _In_opt_ void* pCallbackContext)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

extern "C" UInt32 WaitForSingleObjectEx(HANDLE handle, UInt32 milliseconds, UInt32_BOOL alertable)
{
    // The handle can only represent an event here
    // TODO: encapsulate this stuff
    UnixHandleBase* handleBase = (UnixHandleBase*)handle;
    ASSERT(handleBase->GetType() == UnixHandleType::Event);
    EventUnixHandle* unixHandle = (EventUnixHandle*)handleBase;

    return unixHandle->GetObject()->Wait(milliseconds);
}

REDHAWK_PALEXPORT uint32_t REDHAWK_PALAPI PalCompatibleWaitAny(UInt32_BOOL alertable, uint32_t timeout, uint32_t handleCount, HANDLE* pHandles, UInt32_BOOL allowReentrantWait)
{
    // Only a single handle wait for event is supported
    ASSERT(handleCount == 1);

    return WaitForSingleObjectEx(pHandles[0], timeout, alertable);
}

extern "C" void _mm_pause()
// Defined for implementing PalYieldProcessor in PalRedhawk.h
{
#if defined(_AMD64_) || defined(_X86_)
  __asm__ volatile ("pause");
#endif
}

extern "C" Int32 _wcsicmp(const wchar_t *string1, const wchar_t *string2)
{
    return wcscasecmp(string1, string2);
}

REDHAWK_PALEXPORT void __cdecl PalPrintf(_In_z_ _Printf_format_string_ const char * szFormat, ...)
{
#if defined(_DEBUG)
    va_list args;
    va_start(args, szFormat);
    vprintf(szFormat, args);
#endif
}

REDHAWK_PALEXPORT void __cdecl PalFlushStdout()
{
#if defined(_DEBUG)
    fflush(stdout);
#endif
}

REDHAWK_PALEXPORT int __cdecl PalSprintf(_Out_writes_z_(cchBuffer) char * szBuffer, size_t cchBuffer, _In_z_ _Printf_format_string_ const char * szFormat, ...)
{
    va_list args;
    va_start(args, szFormat);
    return vsnprintf(szBuffer, cchBuffer, szFormat, args);
}

REDHAWK_PALEXPORT int __cdecl PalVSprintf(_Out_writes_z_(cchBuffer) char * szBuffer, size_t cchBuffer, _In_z_ _Printf_format_string_ const char * szFormat, va_list args)
{
    return vsnprintf(szBuffer, cchBuffer, szFormat, args);
}

// Given the OS handle of a loaded module, compute the upper and lower virtual address bounds (inclusive).
REDHAWK_PALEXPORT void REDHAWK_PALAPI PalGetModuleBounds(HANDLE hOsHandle, _Out_ UInt8 ** ppLowerBound, _Out_ UInt8 ** ppUpperBound)
{
    // The module handle is equal to the module base address
    *ppLowerBound = (UInt8*)hOsHandle;
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

REDHAWK_PALEXPORT Int32 PalGetProcessCpuCount()
{
    // The concept of process CPU affinity is going away and so CoreSystem obsoletes the APIs used to
    // fetch this information. Instead we'll just return total cpu count.
    return PalGetLogicalCpuCount();
}

//Reads the entire contents of the file into the specified buffer, buff
//returns the number of bytes read if the file is successfully read
//returns 0 if the file is not found, size is greater than maxBytesToRead or the file couldn't be opened or read
REDHAWK_PALEXPORT UInt32 PalReadFileContents(_In_z_ const WCHAR* fileName, _Out_writes_all_(cchBuff) char* buff, _In_ UInt32 cchBuff)
{
    // UNIXTODO: Implement this function
    return 0;
}

__thread void* pStackHighOut = NULL;
__thread void* pStackLowOut = NULL;

// Retrieves the entire range of memory dedicated to the calling thread's stack.  This does
// not get the current dynamic bounds of the stack, which can be significantly smaller than 
// the maximum bounds.
REDHAWK_PALEXPORT bool PalGetMaximumStackBounds(_Out_ void** ppStackLowOut, _Out_ void** ppStackHighOut)
{
    if (pStackHighOut == NULL)
    {
#ifdef __APPLE__
        // This is a Mac specific method
        pStackHighOut = pthread_get_stackaddr_np(pthread_self());
        pStackLowOut = ((uint8_t *)pStackHighOut - pthread_get_stacksize_np(pthread_self()));
#else // __APPLE__
        pthread_attr_t attr;
        size_t stackSize;
        int status;

        pthread_t thread = pthread_self();

        status = pthread_attr_init(&attr);
        ASSERT_MSG(status == 0, "pthread_attr_init call failed");

#if HAVE_PTHREAD_ATTR_GET_NP
        status = pthread_attr_get_np(thread, &attr);
#elif HAVE_PTHREAD_GETATTR_NP
        status = pthread_getattr_np(thread, &attr);
#else
#error Dont know how to get thread attributes on this platform!
#endif
        ASSERT_MSG(status == 0, "pthread_getattr_np call failed");

        status = pthread_attr_getstack(&attr, &pStackLowOut, &stackSize);
        ASSERT_MSG(status == 0, "pthread_attr_getstack call failed");

        status = pthread_attr_destroy(&attr);
        ASSERT_MSG(status == 0, "pthread_attr_destroy call failed");

        pStackHighOut = (uint8_t*)pStackLowOut + stackSize;
#endif // __APPLE__
    }

    *ppStackLowOut = pStackLowOut;
    *ppStackHighOut = pStackHighOut;

    return true;
}

// retrieves the full path to the specified module, if moduleBase is NULL retreieves the full path to the 
// executable module of the current process.
//
// Return value:  number of characters in name string
//
REDHAWK_PALEXPORT Int32 PalGetModuleFileName(_Out_ wchar_t** pModuleNameOut, HANDLE moduleBase)
{
    // TODO: this function has a signature that expects the module name to be stored somewhere.
    // We should change the signature to actually copy out the module name.
    // Or get rid of this function (it is used by RHConfig to find the config file and the
    // profiling to generate the .profile file name.
    // UNIXTODO: Implement this function!
    *pModuleNameOut = NULL;
    return 0;
}

void PalDebugBreak()
{
    __debugbreak();
}

GCSystemInfo g_SystemInfo;

void InitializeSystemInfo()
{
    // TODO: Implement
    g_SystemInfo.dwNumberOfProcessors = 4;

    g_SystemInfo.dwPageSize = OS_PAGE_SIZE;
    g_SystemInfo.dwAllocationGranularity = OS_PAGE_SIZE;
}

extern "C" void FlushProcessWriteBuffers()
{
    // UNIXTODO: Implement
}

extern "C" uint32_t GetTickCount()
{
    return PalGetTickCount();
}

int32_t FastInterlockIncrement(int32_t volatile *lpAddend)
{
    return __sync_add_and_fetch(lpAddend, 1);
}

int32_t FastInterlockDecrement(int32_t volatile *lpAddend)
{
    return __sync_sub_and_fetch(lpAddend, 1);
}

int32_t FastInterlockExchange(int32_t volatile *Target, int32_t Value)
{
    return __sync_swap(Target, Value);
}

int32_t FastInterlockCompareExchange(int32_t volatile *Destination, int32_t Exchange, int32_t Comperand)
{
    return __sync_val_compare_and_swap(Destination, Comperand, Exchange);
}

int32_t FastInterlockExchangeAdd(int32_t volatile *Addend, int32_t Value)
{
    return __sync_fetch_and_add(Addend, Value);
}

void * _FastInterlockExchangePointer(void * volatile *Target, void * Value)
{
    return __sync_swap(Target, Value);
}

void * _FastInterlockCompareExchangePointer(void * volatile *Destination, void * Exchange, void * Comperand)
{
    return __sync_val_compare_and_swap(Destination, Comperand, Exchange);
}

void FastInterlockOr(uint32_t volatile *p, uint32_t msk)
{
    __sync_fetch_and_or(p, msk);
}

void FastInterlockAnd(uint32_t volatile *p, uint32_t msk)
{
    __sync_fetch_and_and(p, msk);
}

extern "C" UInt32_BOOL QueryPerformanceCounter(LARGE_INTEGER *lpPerformanceCount)
{
    // TODO: More efficient, platform-specific implementation
    struct timeval tv;
    if (gettimeofday(&tv, NULL) == -1)
    {
        ASSERT_UNCONDITIONALLY("gettimeofday() failed");
        return UInt32_FALSE;
    }
    lpPerformanceCount->QuadPart =
        (int64_t) tv.tv_sec * (int64_t) tccSecondsToMicroSeconds + (int64_t) tv.tv_usec;
    return UInt32_TRUE;
}

extern "C" UInt32_BOOL QueryPerformanceFrequency(LARGE_INTEGER *lpFrequency)
{
    lpFrequency->QuadPart = (int64_t) tccSecondsToMicroSeconds;
    return UInt32_TRUE;
}

extern "C" uint32_t GetCurrentThreadId()
{
    // UNIXTODO: Implement
    return 1;
}

extern "C" uint32_t SetFilePointer(
    HANDLE hFile,
    int32_t lDistanceToMove,
    int32_t * lpDistanceToMoveHigh,
    uint32_t dwMoveMethod)
{
    // TODO: Reimplement callers using CRT
    return 0;
}

extern "C" UInt32_BOOL FlushFileBuffers(
    HANDLE hFile)
{
    // TODO: Reimplement callers using CRT
    return UInt32_FALSE;
}

extern "C" UInt32_BOOL WriteFile(
    HANDLE hFile,
    const void* lpBuffer,
    uint32_t nNumberOfBytesToWrite,
    uint32_t * lpNumberOfBytesWritten,
    void* lpOverlapped)
{
    // TODO: Reimplement callers using CRT
    return UInt32_FALSE;
}

extern "C" void YieldProcessor()
{
}

extern "C" void DebugBreak()
{
    PalDebugBreak();
}

extern "C" uint32_t GetLastError()
{
    return 1;
}

extern "C" uint32_t GetWriteWatch(
    uint32_t dwFlags,
    void* lpBaseAddress,
    size_t dwRegionSize,
    void** lpAddresses,
    uintptr_t * lpdwCount,
    uint32_t * lpdwGranularity
    )
{
    // TODO: Implement for background GC
    *lpAddresses = NULL;
    *lpdwCount = 0;
    // Until it is implemented, return non-zero value as an indicator of failure
    return 1;
}

extern "C" uint32_t ResetWriteWatch(
    void* lpBaseAddress,
    size_t dwRegionSize
    )
{
    // TODO: Implement for background GC
    // Until it is implemented, return non-zero value as an indicator of failure
    return 1;
}

extern "C" UInt32_BOOL VirtualUnlock(
    void* lpAddress,
    size_t dwSize
    )
{
    // TODO: Implement
    return UInt32_FALSE;
}

void UnsafeInitializeCriticalSection(CRITICAL_SECTION * lpCriticalSection)
{
    InitializeCriticalSection(lpCriticalSection);
}

void UnsafeEEEnterCriticalSection(CRITICAL_SECTION *lpCriticalSection)
{
    EnterCriticalSection(lpCriticalSection);
}

void UnsafeEELeaveCriticalSection(CRITICAL_SECTION * lpCriticalSection)
{
    LeaveCriticalSection(lpCriticalSection);
}

void UnsafeDeleteCriticalSection(CRITICAL_SECTION *lpCriticalSection)
{
    DeleteCriticalSection(lpCriticalSection);
}

extern "C" UInt32 WaitForMultipleObjectsEx(UInt32, HANDLE *, UInt32_BOOL, UInt32, UInt32_BOOL)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}
