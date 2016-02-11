// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// Implementation of the Redhawk Platform Abstraction Layer (PAL) library when Unix is the platform.
//

#include <stdio.h>
#include <errno.h>
#include <cwchar>
#include "CommonTypes.h"
#include "PalRedhawkCommon.h"
#include "CommonMacros.h"
#include <sal.h>
#include "config.h"
#include "UnixHandle.h"
#include <pthread.h>
#include "gcenv.structs.h"
#include "gcenv.os.h"
#include "holder.h"

#include <unistd.h>
#include <sched.h>
#include <sys/mman.h>
#include <sys/types.h>
#include <dlfcn.h>
#include <dirent.h>
#include <string.h>
#include <ctype.h>
#include <sys/stat.h>
#include <fcntl.h>
#include <sys/time.h>
#include <cstdarg>

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

#ifdef CORERT // @TODO: Collisions between assert.h headers
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

#define PalRaiseFailFastException RaiseFailFastException

#define FATAL_ASSERT(e, msg) \
    do \
    { \
        if (!(e)) \
        { \
            fprintf(stderr, "FATAL ERROR: " msg); \
            RhFailFast(); \
        } \
    } \
    while(0)

typedef void * LPSECURITY_ATTRIBUTES;
typedef void* PCONTEXT;
typedef void* PEXCEPTION_RECORD;
typedef void* PEXCEPTION_POINTERS;

typedef Int32 (__stdcall *PVECTORED_EXCEPTION_HANDLER)(
    PEXCEPTION_POINTERS ExceptionInfo
    );

#define PAGE_NOACCESS           0x01
#define PAGE_READWRITE          0x04
#define MEM_COMMIT              0x1000
#define MEM_RESERVE             0x2000
#define MEM_DECOMMIT            0x4000
#define MEM_RELEASE             0x8000

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

static uint32_t g_dwPALCapabilities;
static UInt32 g_cLogicalCpus = 0;
static size_t g_cbLargestOnDieCache = 0;
static size_t g_cbLargestOnDieCacheAdjusted = 0;

// Helper memory page used by the FlushProcessWriteBuffers
static uint8_t g_helperPage[OS_PAGE_SIZE] __attribute__((aligned(OS_PAGE_SIZE)));

// Mutex to make the FlushProcessWriteBuffersMutex thread safe
pthread_mutex_t g_flushProcessWriteBuffersMutex;

// Key for the thread local storage of the attached thread pointer
static pthread_key_t g_threadKey;

extern bool PalQueryProcessorTopology();
bool InitializeFlushProcessWriteBuffers();

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

typedef UnixHandle<UnixHandleType::Event, UnixEvent> EventUnixHandle;
typedef UnixHandle<UnixHandleType::Thread, pthread_t> ThreadUnixHandle;

// Destructor of the thread local object represented by the g_threadKey,
// called when a thread is shut down
void TlsObjectDestructor(void* data)
{
    ASSERT(data == pthread_getspecific(g_threadKey));

    RuntimeThreadShutdown(data);
}

// The Redhawk PAL must be initialized before any of its exports can be called. Returns true for a successful
// initialization and false on failure.
REDHAWK_PALEXPORT bool REDHAWK_PALAPI PalInit()
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

    if (!InitializeFlushProcessWriteBuffers())
    {
        return false;
    }

    int status = pthread_key_create(&g_threadKey, TlsObjectDestructor);
    if (status != 0)
    {
        return false;
    }

    return true;
}

// Given a mask of capabilities return true if all of them are supported by the current PAL.
REDHAWK_PALEXPORT bool REDHAWK_PALAPI PalHasCapability(PalCapability capability)
{
    return (g_dwPALCapabilities & (uint32_t)capability) == (uint32_t)capability;
}

extern "C" void RaiseFailFastException(PEXCEPTION_RECORD arg1, PCONTEXT arg2, UInt32 arg3)
{
    // Abort aborts the process and causes creation of a crash dump
    abort();
}

// Attach thread to PAL. 
// It can be called multiple times for the same thread.
// It fails fast if a different thread was already registered.
// Parameters:
//  thread        - thread to attach
extern "C" void PalAttachThread(void* thread)
{
    void* attachedThread = pthread_getspecific(g_threadKey);

    ASSERT_MSG(attachedThread == NULL, "PalAttachThread called multiple times for the same thread");

    int status = pthread_setspecific(g_threadKey, thread);
    if (status != 0)
    {
        ASSERT_UNCONDITIONALLY("PalAttachThread failed to store thread pointer in thread local storage");
        RhFailFast();
    }
}

// Detach thread from PAL.
// It fails fast if some other thread value was attached to PAL.
// Parameters:
//  thread        - thread to detach
// Return:
//  true if the thread was detached, false if there was no attached thread
extern "C" bool PalDetachThread(void* thread)
{
    void* attachedThread = pthread_getspecific(g_threadKey);

    if (attachedThread == thread)
    {
        int status = pthread_setspecific(g_threadKey, NULL);
        if (status != 0)
        {
            ASSERT_UNCONDITIONALLY("PalDetachThread failed to clear thread pointer in thread local storage");
            RhFailFast();
        }
        return true;
    }

    if (attachedThread != NULL)
    {
        ASSERT_UNCONDITIONALLY("PalDetachThread called with different thread pointer than PalAttachThread");
        RhFailFast();
    }

    return false;
}

REDHAWK_PALEXPORT unsigned int REDHAWK_PALAPI PalGetCurrentProcessorNumber()
{
#if HAVE_SCHED_GETCPU
    int processorNumber = sched_getcpu();
    ASSERT(processorNumber != -1);
    return processorNumber;
#else //HAVE_SCHED_GETCPU
    return 0;
#endif //HAVE_SCHED_GETCPU    
}

REDHAWK_PALEXPORT UInt32_BOOL REDHAWK_PALAPI PalAllocateThunksFromTemplate(HANDLE hTemplateModule, uint32_t templateRva, size_t templateSize, void** newThunksOut)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
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

REDHAWK_PALEXPORT HANDLE REDHAWK_PALAPI PalCreateEventW(_In_opt_ LPSECURITY_ATTRIBUTES pEventAttributes, UInt32_BOOL manualReset, UInt32_BOOL initialState, _In_opt_z_ const wchar_t* pName)
{
    return new (nothrow) EventUnixHandle(UnixEvent(manualReset, initialState));
}

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
    // UNIXTODO: Implement this function
    return NULL;
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

    if (allocationType & (MEM_RESERVE | MEM_COMMIT))
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

extern "C" uint32_t GetCurrentProcessId()
{
    return getpid();
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
    *lpTargetHandle = new (nothrow) ThreadUnixHandle(pthread_self());

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

extern "C" UInt32 GetEnvironmentVariableA(const char * name, char * buffer, UInt32 size)
{
    // Using std::getenv instead of getenv since it is guaranteed to be thread safe w.r.t. other
    // std::getenv calls in C++11
    const char* value = std::getenv(name);
    if (value == NULL)
    {
        return 0;
    }

    size_t valueLen = strlen(value);

    if (valueLen < size)
    {
        strcpy(buffer, value);
        return valueLen;
    }

    // return required size including the null character or 0 if the size doesn't fit into UInt32
    return (valueLen < UINT32_MAX) ? (valueLen + 1) : 0;
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

extern "C" Int32 _stricmp(const char *string1, const char *string2)
{
    return strcasecmp(string1, string2);
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
REDHAWK_PALEXPORT UInt32 PalReadFileContents(_In_z_ const TCHAR* fileName, _Out_writes_all_(maxBytesToRead) char* buff, _In_ UInt32 maxBytesToRead)
{
    int fd = open(fileName, O_RDONLY);
    if (fd < 0)
    {
        return 0;
    }


    UInt32 bytesRead = 0;
    struct stat fileStats;
    if ((fstat(fd, &fileStats) == 0) && (fileStats.st_size <= maxBytesToRead))
    {
        bytesRead = read(fd, buff, fileStats.st_size);
    }

    close(fd);

    return bytesRead;
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

// Initialize the g_SystemInfo
bool InitializeSystemInfo()
{
    long pagesize = getpagesize();
    g_SystemInfo.dwPageSize = pagesize;
    g_SystemInfo.dwAllocationGranularity = pagesize;

    int nrcpus = 0;

#if HAVE_SYSCONF
    nrcpus = sysconf(_SC_NPROCESSORS_ONLN);
    if (nrcpus < 1)
    {
        ASSERT_UNCONDITIONALLY("sysconf failed for _SC_NPROCESSORS_ONLN\n");
        return false;
    }
#elif HAVE_SYSCTL
    int mib[2];

    size_t sz = sizeof(nrcpus);
    mib[0] = CTL_HW;
    mib[1] = HW_NCPU;
    int rc = sysctl(mib, 2, &nrcpus, &sz, NULL, 0);
    if (rc != 0)
    {
        ASSERT_UNCONDITIONALLY("sysctl failed for HW_NCPU\n");
        return false;
    }
#endif // HAVE_SYSCONF

    g_SystemInfo.dwNumberOfProcessors = nrcpus;

    return true;
}

// This function initializes data structures needed for the FlushProcessWriteBuffers
// Return:
//  true if it succeeded, false otherwise
bool InitializeFlushProcessWriteBuffers()
{
    // Verify that the s_helperPage is really aligned to the g_SystemInfo.dwPageSize
    ASSERT((((size_t)g_helperPage) & (OS_PAGE_SIZE - 1)) == 0);

    // Locking the page ensures that it stays in memory during the two mprotect
    // calls in the FlushProcessWriteBuffers below. If the page was unmapped between
    // those calls, they would not have the expected effect of generating IPI.
    int status = mlock(g_helperPage, OS_PAGE_SIZE);

    if (status != 0)
    {
        return false;
    }

    status = pthread_mutex_init(&g_flushProcessWriteBuffersMutex, NULL);
    if (status != 0)
    {
        munlock(g_helperPage, OS_PAGE_SIZE);
    }

    return status == 0;
}

extern "C" void FlushProcessWriteBuffers()
{
    int status = pthread_mutex_lock(&g_flushProcessWriteBuffersMutex);
    FATAL_ASSERT(status == 0, "Failed to lock the flushProcessWriteBuffersMutex lock");

    // Changing a helper memory page protection from read / write to no access
    // causes the OS to issue IPI to flush TLBs on all processors. This also
    // results in flushing the processor buffers.
    status = mprotect(g_helperPage, OS_PAGE_SIZE, PROT_READ | PROT_WRITE);
    FATAL_ASSERT(status == 0, "Failed to change helper page protection to read / write");

    // Ensure that the page is dirty before we change the protection so that
    // we prevent the OS from skipping the global TLB flush.
    __sync_add_and_fetch((size_t*)g_helperPage, 1);

    status = mprotect(g_helperPage, OS_PAGE_SIZE, PROT_NONE);
    FATAL_ASSERT(status == 0, "Failed to change helper page protection to no access");

    status = pthread_mutex_unlock(&g_flushProcessWriteBuffersMutex);
    FATAL_ASSERT(status == 0, "Failed to unlock the flushProcessWriteBuffersMutex lock");
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

extern "C" UInt32 WaitForMultipleObjectsEx(UInt32, HANDLE *, UInt32_BOOL, UInt32, UInt32_BOOL)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

// Initialize the interface implementation
bool GCToOSInterface::Initialize()
{
    return true;
}

// Shutdown the interface implementation
void GCToOSInterface::Shutdown()
{
}

// Get numeric id of the current thread if possible on the 
// current platform. It is indended for logging purposes only.
// Return:
//  Numeric id of the current thread or 0 if the 
uint32_t GCToOSInterface::GetCurrentThreadIdForLogging()
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
    return false;
}

// Get the number of the current processor
uint32_t GCToOSInterface::GetCurrentProcessorNumber()
{
    return PalGetCurrentProcessorNumber();
}

// Check if the OS supports getting current processor number
bool GCToOSInterface::CanGetCurrentProcessorNumber()
{
    return HAVE_SCHED_GETCPU;
}

// Flush write buffers of processors that are executing threads of the current process
void GCToOSInterface::FlushProcessWriteBuffers()
{
    return ::FlushProcessWriteBuffers();
}

// Break into a debugger
void GCToOSInterface::DebugBreak()
{
    __debugbreak();
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
void GCToOSInterface::YieldThread(uint32_t switchCount)
{
    // UNIXTODO: handle the switchCount
    YieldProcessor();
}

// Reserve virtual memory range.
// Parameters:
//  address   - starting virtual address, it can be NULL to let the function choose the starting address
//  size      - size of the virtual memory range
//  alignment - requested memory alignment, 0 means no specific alignment requested
//  flags     - flags to control special settings like write watching
// Return:
//  Starting virtual address of the reserved range
void* GCToOSInterface::VirtualReserve(void* address, size_t size, size_t alignment, uint32_t flags)
{
    ASSERT_MSG(!(flags & VirtualReserveFlags::WriteWatch), "WriteWatch not supported on Unix");

    if (alignment == 0)
    {
        alignment = OS_PAGE_SIZE;
    }

    size_t alignedSize = size + (alignment - OS_PAGE_SIZE);

    void * pRetVal = mmap(address, alignedSize, PROT_NONE, MAP_ANON | MAP_PRIVATE, -1, 0);

    if (pRetVal != NULL)
    {
        void * pAlignedRetVal = (void *)(((size_t)pRetVal + (alignment - 1)) & ~(alignment - 1));
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

// Release virtual memory range previously reserved using VirtualReserve
// Parameters:
//  address - starting virtual address
//  size    - size of the virtual memory range
// Return:
//  true if it has succeeded, false if it has failed
bool GCToOSInterface::VirtualRelease(void* address, size_t size)
{
    int ret = munmap(address, size);

    return (ret == 0);
}

// Commit virtual memory range. It must be part of a range reserved using VirtualReserve.
// Parameters:
//  address - starting virtual address
//  size    - size of the virtual memory range
// Return:
//  true if it has succeeded, false if it has failed
bool GCToOSInterface::VirtualCommit(void* address, size_t size)
{
    return mprotect(address, size, PROT_WRITE | PROT_READ) == 0;
}

// Decomit virtual memory range.
// Parameters:
//  address - starting virtual address
//  size    - size of the virtual memory range
// Return:
//  true if it has succeeded, false if it has failed
bool GCToOSInterface::VirtualDecommit(void* address, size_t size)
{
    return mprotect(address, size, PROT_NONE) == 0;
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
    // UNIXTODO: Implement this
    return true;
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
    return false;
}

// Get size of the largest cache on the processor die
// Parameters:
//  trueSize - true to return true cache size, false to return scaled up size based on
//             the processor architecture
// Return:
//  Size of the cache
size_t GCToOSInterface::GetLargestOnDieCacheSize(bool trueSize)
{
    // UNIXTODO: implement this
    return 0;
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
    return false;
}

// Get number of processors assigned to the current process
// Return:
//  The number of processors
uint32_t GCToOSInterface::GetCurrentProcessCpuCount()
{
    return ::PalGetProcessCpuCount();
}

// Get global memory status
// Parameters:
//  ms - pointer to the structure that will be filled in with the memory status
void GCToOSInterface::GetMemoryStatus(GCMemoryStatus* ms)
{
    ms->dwMemoryLoad = 0;
    ms->ullTotalPhys = 0;
    ms->ullAvailPhys = 0;
    ms->ullTotalPageFile = 0;
    ms->ullAvailPageFile = 0;
    ms->ullTotalVirtual = 0;
    ms->ullAvailVirtual = 0;

    UInt32_BOOL fRetVal = UInt32_FALSE;

    // Get the physical memory size
#if HAVE_SYSCONF && HAVE__SC_PHYS_PAGES
    int64_t physical_memory;

    // Get the Physical memory size
    physical_memory = sysconf( _SC_PHYS_PAGES ) * sysconf( _SC_PAGE_SIZE );
    ms->ullTotalPhys = (uint64_t)physical_memory;
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
        ms->ullTotalPhys = (uint64_t)physical_memory;
        fRetVal = UInt32_TRUE;
    }
#elif // HAVE_SYSINFO
    // TODO: implement getting memory details via sysinfo. On Linux, it provides swap file details that
    // we can use to fill in the xxxPageFile members.

#endif // HAVE_SYSCONF

    // Get the physical memory in use - from it, we can get the physical memory available.
    // We do this only when we have the total physical memory available.
    if (ms->ullTotalPhys > 0)
    {
#ifndef __APPLE__
        ms->ullAvailPhys = sysconf(SYSCONF_PAGES) * sysconf(_SC_PAGE_SIZE);
        int64_t used_memory = ms->ullTotalPhys - ms->ullAvailPhys;
        ms->dwMemoryLoad = (uint32_t)((used_memory * 100) / ms->ullTotalPhys);
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
                ms->ullAvailPhys = (int64_t)vm_stats.free_count * (int64_t)page_size;
                int64_t used_memory = ((int64_t)vm_stats.active_count + (int64_t)vm_stats.inactive_count + (int64_t)vm_stats.wire_count) *  (int64_t)page_size;
                ms->dwMemoryLoad = (uint32_t)((used_memory * 100) / ms->ullTotalPhys);
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
    ms->ullTotalVirtual = _128TB;
    ms->ullAvailVirtual = ms->ullAvailPhys;

    // UNIXTODO: failfast for !fRetVal?
}

// Get a high precision performance counter
// Return:
//  The counter value
int64_t GCToOSInterface::QueryPerformanceCounter()
{
    LARGE_INTEGER ts;
    if (!::QueryPerformanceCounter(&ts))
    {
        DebugBreak();
        ASSERT_UNCONDITIONALLY("Fatal Error - cannot query performance counter.");
        abort();
    }

    return ts.QuadPart;
}

// Get a frequency of the high precision performance counter
// Return:
//  The counter frequency
int64_t GCToOSInterface::QueryPerformanceFrequency()
{
    LARGE_INTEGER frequency;
    if (!::QueryPerformanceFrequency(&frequency))
    {
        DebugBreak();
        ASSERT_UNCONDITIONALLY("Fatal Error - cannot query performance counter.");
        abort();
    }

    return frequency.QuadPart;
}

// Get a time stamp with a low precision
// Return:
//  Time stamp in milliseconds
uint32_t GCToOSInterface::GetLowPrecisionTimeStamp()
{
    return PalGetTickCount();
}

// Parameters of the GC thread stub
struct GCThreadStubParam
{
    GCThreadFunction GCThreadFunction;
    void* GCThreadParam;
};

// GC thread stub to convert GC thread function to an OS specific thread function
static void* GCThreadStub(void* param)
{
    GCThreadStubParam *stubParam = (GCThreadStubParam*)param;
    GCThreadFunction function = stubParam->GCThreadFunction;
    void* threadParam = stubParam->GCThreadParam;

    delete stubParam;

    function(threadParam);

    return NULL;
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

    pthread_attr_t attrs;

    int st = pthread_attr_init(&attrs);
    ASSERT(st == 0);

    // Create the thread as detached, that means not joinable
    st = pthread_attr_setdetachstate(&attrs, PTHREAD_CREATE_DETACHED);
    ASSERT(st == 0);

    pthread_t threadId;
    st = pthread_create(&threadId, &attrs, GCThreadStub, stubParam);

    if (st == 0)
    {
        stubParam.SuppressRelease();
    }

    int st2 = pthread_attr_destroy(&attrs);
    ASSERT(st2 == 0);

    return (st == 0);
}

// Initialize the critical section
void CLRCriticalSection::Initialize()
{
    int st = pthread_mutex_init(&m_cs.mutex, NULL);
    ASSERT(st == 0);
}

// Destroy the critical section
void CLRCriticalSection::Destroy()
{
    int st = pthread_mutex_destroy(&m_cs.mutex);
    ASSERT(st == 0);
}

// Enter the critical section. Blocks until the section can be entered.
void CLRCriticalSection::Enter()
{
    pthread_mutex_lock(&m_cs.mutex);
}

// Leave the critical section
void CLRCriticalSection::Leave()
{
    pthread_mutex_unlock(&m_cs.mutex);
}
