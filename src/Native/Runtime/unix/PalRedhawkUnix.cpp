// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// Implementation of the Redhawk Platform Abstraction Layer (PAL) library when Unix is the platform.
//

#include <stdio.h>
#include <errno.h>
#include <cwchar>
#include <sal.h>
#include "config.h"
#include "UnixHandle.h"
#include <pthread.h>
#include "gcenv.h"
#include "holder.h"
#include "HardwareExceptions.h"

#include <unistd.h>
#include <sched.h>
#include <sys/mman.h>
#include <sys/types.h>
#include <sys/syscall.h>
#include <dlfcn.h>
#include <dirent.h>
#include <string.h>
#include <ctype.h>
#include <sys/stat.h>
#include <fcntl.h>
#include <sys/time.h>
#include <cstdarg>
#include <signal.h>

#if HAVE_PTHREAD_GETTHREADID_NP
#include <pthread_np.h>
#endif

#if HAVE_LWP_SELF
#include <lwp.h>
#endif

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

#ifndef __APPLE__
#if HAVE_SYSCONF && HAVE__SC_AVPHYS_PAGES
#define SYSCONF_PAGES _SC_AVPHYS_PAGES
#elif HAVE_SYSCONF && HAVE__SC_PHYS_PAGES
#define SYSCONF_PAGES _SC_PHYS_PAGES
#else
#error Dont know how to get page-size on this architecture!
#endif
#endif // __APPLE__

#if defined(_ARM_) || defined(_ARM64_)
#define SYSCONF_GET_NUMPROCS       _SC_NPROCESSORS_CONF
#define SYSCONF_GET_NUMPROCS_NAME "_SC_NPROCESSORS_CONF"
#else
#define SYSCONF_GET_NUMPROCS       _SC_NPROCESSORS_ONLN
#define SYSCONF_GET_NUMPROCS_NAME "_SC_NPROCESSORS_ONLN"
#endif

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

#define INVALID_HANDLE_VALUE    ((HANDLE)(IntNative)-1)

#define PAGE_NOACCESS           0x01
#define PAGE_READWRITE          0x04
#define PAGE_EXECUTE_READ       0x20
#define PAGE_EXECUTE_READWRITE  0x40
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

static uint32_t g_dwPALCapabilities;
static UInt32 g_cLogicalCpus = 0;
static size_t g_cbLargestOnDieCache = 0;
static size_t g_cbLargestOnDieCacheAdjusted = 0;

// HACK: the gcenv.h declares OS_PAGE_SIZE as a call instead of a constant, but we need a constant
#undef OS_PAGE_SIZE
#define OS_PAGE_SIZE 0x1000

// Helper memory page used by the FlushProcessWriteBuffers
static uint8_t g_helperPage[OS_PAGE_SIZE] __attribute__((aligned(OS_PAGE_SIZE)));

// Mutex to make the FlushProcessWriteBuffersMutex thread safe
pthread_mutex_t g_flushProcessWriteBuffersMutex;

extern bool PalQueryProcessorTopology();
bool InitializeFlushProcessWriteBuffers();

extern "C" void RaiseFailFastException(PEXCEPTION_RECORD arg1, PCONTEXT arg2, UInt32 arg3)
{
    // Abort aborts the process and causes creation of a crash dump
    abort();
}

static void TimeSpecAdd(timespec* time, uint32_t milliseconds)
{
    uint64_t nsec = time->tv_nsec + (uint64_t)milliseconds * tccMilliSecondsToNanoSeconds;
    if (nsec >= tccSecondsToNanoSeconds)
    {
        time->tv_sec += nsec / tccSecondsToNanoSeconds;
        nsec %= tccSecondsToNanoSeconds;
    }

    time->tv_nsec = nsec;
}

// Convert nanoseconds to the timespec structure
// Parameters:
//  nanoseconds - time in nanoseconds to convert
//  t           - the target timespec structure
static void NanosecondsToTimeSpec(uint64_t nanoseconds, timespec* t)
{
    t->tv_sec = nanoseconds / tccSecondsToNanoSeconds;
    t->tv_nsec = nanoseconds % tccSecondsToNanoSeconds;
}

void ReleaseCondAttr(pthread_condattr_t* condAttr)
{
    int st = pthread_condattr_destroy(condAttr);
    ASSERT_MSG(st == 0, "Failed to destroy pthread_condattr_t object");
}

class PthreadCondAttrHolder : public Wrapper<pthread_condattr_t*, DoNothing, ReleaseCondAttr, nullptr>
{
public:
    PthreadCondAttrHolder(pthread_condattr_t* attrs)
    : Wrapper<pthread_condattr_t*, DoNothing, ReleaseCondAttr, nullptr>(attrs)
    {
    }
};

class UnixEvent
{
    pthread_cond_t m_condition;
    pthread_mutex_t m_mutex;
    bool m_manualReset;
    bool m_state;
    bool m_isValid;

public:

    UnixEvent(bool manualReset, bool initialState)
    : m_manualReset(manualReset),
      m_state(initialState),
      m_isValid(false)
    {
    }

    bool Initialize()
    {
        pthread_condattr_t attrs;
        int st = pthread_condattr_init(&attrs);
        if (st != 0)
        {
            ASSERT_UNCONDITIONALLY("Failed to initialize UnixEvent condition attribute");
            return false;
        }

        PthreadCondAttrHolder attrsHolder(&attrs);

#if HAVE_PTHREAD_CONDATTR_SETCLOCK && !HAVE_MACH_ABSOLUTE_TIME
        // Ensure that the pthread_cond_timedwait will use CLOCK_MONOTONIC
        st = pthread_condattr_setclock(&attrs, CLOCK_MONOTONIC);
        if (st != 0)
        {
            ASSERT_UNCONDITIONALLY("Failed to set UnixEvent condition variable wait clock");
            return false;
        }
#endif // HAVE_PTHREAD_CONDATTR_SETCLOCK && !HAVE_MACH_ABSOLUTE_TIME

        st = pthread_mutex_init(&m_mutex, NULL);
        if (st != 0)
        {
            ASSERT_UNCONDITIONALLY("Failed to initialize UnixEvent mutex");
            return false;
        }

        st = pthread_cond_init(&m_condition, &attrs);
        if (st != 0)
        {
            ASSERT_UNCONDITIONALLY("Failed to initialize UnixEvent condition variable");

            st = pthread_mutex_destroy(&m_mutex);
            ASSERT_MSG(st == 0, "Failed to destroy UnixEvent mutex");
            return false;
        }

        m_isValid = true;

        return true;
    }

    bool Destroy()
    {
        bool success = true;

        if (m_isValid)
        {
            int st = pthread_mutex_destroy(&m_mutex);
            ASSERT_MSG(st == 0, "Failed to destroy UnixEvent mutex");
            success = success && (st == 0);

            st = pthread_cond_destroy(&m_condition);
            ASSERT_MSG(st == 0, "Failed to destroy UnixEvent condition variable");
            success = success && (st == 0);
        }

        return success;
    }

    uint32_t Wait(uint32_t milliseconds)
    {
        timespec endTime;
#if HAVE_MACH_ABSOLUTE_TIME
        uint64_t endMachTime;
        if (milliseconds != INFINITE)
        {
            uint64_t nanoseconds = (uint64_t)milliseconds * tccMilliSecondsToNanoSeconds;
            NanosecondsToTimeSpec(nanoseconds, &endTime);
            endMachTime = mach_absolute_time() + nanoseconds * s_TimebaseInfo.denom / s_TimebaseInfo.numer;
        }
#elif HAVE_PTHREAD_CONDATTR_SETCLOCK
        if (milliseconds != INFINITE)
        {
            clock_gettime(CLOCK_MONOTONIC, &endTime);
            TimeSpecAdd(&endTime, milliseconds);
        }
#else
#error Don't know how to perform timed wait on this platform
#endif

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
#if HAVE_MACH_ABSOLUTE_TIME
                // Since OSX doesn't support CLOCK_MONOTONIC, we use relative variant of the 
                // timed wait and we need to handle spurious wakeups properly.
                st = pthread_cond_timedwait_relative_np(&m_condition, &m_mutex, &endTime);
                if ((st == 0) && !m_state)
                {
                    uint64_t machTime = mach_absolute_time();
                    if (machTime < endMachTime)
                    {
                        // The wake up was spurious, recalculate the relative endTime
                        uint64_t remainingNanoseconds = (endMachTime - machTime) * s_TimebaseInfo.numer / s_TimebaseInfo.denom;
                        NanosecondsToTimeSpec(remainingNanoseconds, &endTime);
                    }
                    else
                    {
                        // Although the timed wait didn't report a timeout, time calculated from the
                        // mach time shows we have already reached the end time. It can happen if
                        // the wait was spuriously woken up right before the timeout.
                        st = ETIMEDOUT;
                    }
                }
#else // HAVE_MACH_ABSOLUTE_TIME
                st = pthread_cond_timedwait(&m_condition, &m_mutex, &endTime);
#endif // HAVE_MACH_ABSOLUTE_TIME
                // Verify that if the wait timed out, the event was not set
                ASSERT((st != ETIMEDOUT) || !m_state);
            }

            if (st != 0)
            {
                // wait failed or timed out
                break;
            }
        }

        if ((st == 0) && !m_manualReset)
        {
            // Clear the state for auto-reset events so that only one waiter gets released
            m_state = false;
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
        pthread_mutex_lock(&m_mutex);
        m_state = true;
        pthread_mutex_unlock(&m_mutex);

        // Unblock all threads waiting for the condition variable
        pthread_cond_broadcast(&m_condition);
    }

    void Reset()
    {
        pthread_mutex_lock(&m_mutex);
        m_state = false;
        pthread_mutex_unlock(&m_mutex);
    }
};

class EventUnixHandle : public UnixHandle<UnixHandleType::Event, UnixEvent>
{
public:
    EventUnixHandle(UnixEvent event)
    : UnixHandle<UnixHandleType::Event, UnixEvent>(event)
    {
    }

    virtual bool Destroy()
    {
        return m_object.Destroy();
    }
};

typedef UnixHandle<UnixHandleType::Thread, pthread_t> ThreadUnixHandle;

#if !HAVE_THREAD_LOCAL
extern "C" int __cxa_thread_atexit(void (*)(void*), void*, void *);
extern "C" void *__dso_handle;
#endif

// This functions configures behavior of the signals that are not
// related to hardware exception handling.
void ConfigureSignals()
{
    // The default action for SIGPIPE is process termination.
    // Since SIGPIPE can be signaled when trying to write on a socket for which
    // the connection has been dropped, we need to tell the system we want
    // to ignore this signal.
    // Instead of terminating the process, the system call which would had
    // issued a SIGPIPE will, instead, report an error and set errno to EPIPE.
    signal(SIGPIPE, SIG_IGN);
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
#ifndef USE_PORTABLE_HELPERS
    if (!InitializeHardwareExceptionHandling())
    {
        return false;
    }
#endif // !USE_PORTABLE_HELPERS

    ConfigureSignals();

    return true;
}

// Given a mask of capabilities return true if all of them are supported by the current PAL.
REDHAWK_PALEXPORT bool REDHAWK_PALAPI PalHasCapability(PalCapability capability)
{
    return (g_dwPALCapabilities & (uint32_t)capability) == (uint32_t)capability;
}

#if HAVE_THREAD_LOCAL

struct TlsDestructionMonitor
{
    void* m_thread = nullptr;

    void SetThread(void* thread)
    {
        m_thread = thread;
    }

    ~TlsDestructionMonitor()
    {
        if (m_thread != nullptr)
        {
            RuntimeThreadShutdown(m_thread);
        }
    }
};

// This thread local object is used to detect thread shutdown. Its destructor
// is called when a thread is being shut down.
thread_local TlsDestructionMonitor tls_destructionMonitor;

#endif // HAVE_THREAD_LOCAL

// This thread local variable is used for delegate marshalling
DECLSPEC_THREAD intptr_t tls_thunkData;

// Attach thread to PAL. 
// It can be called multiple times for the same thread.
// It fails fast if a different thread was already registered.
// Parameters:
//  thread        - thread to attach
extern "C" void PalAttachThread(void* thread)
{
#if HAVE_THREAD_LOCAL
    tls_destructionMonitor.SetThread(thread);
#else
    __cxa_thread_atexit(RuntimeThreadShutdown, thread, &__dso_handle);
#endif
}

// Detach thread from PAL.
// It fails fast if some other thread value was attached to PAL.
// Parameters:
//  thread        - thread to detach
// Return:
//  true if the thread was detached, false if there was no attached thread
extern "C" bool PalDetachThread(void* thread)
{
    UNREFERENCED_PARAMETER(thread);
    if (g_threadExitCallback != nullptr)
    {
        g_threadExitCallback();
    }
    return true;
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

#if !defined(USE_PORTABLE_HELPERS) && !defined(FEATURE_RX_THUNKS)
REDHAWK_PALEXPORT UInt32_BOOL REDHAWK_PALAPI PalAllocateThunksFromTemplate(HANDLE hTemplateModule, uint32_t templateRva, size_t templateSize, void** newThunksOut)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}

REDHAWK_PALEXPORT UInt32_BOOL REDHAWK_PALAPI PalFreeThunksFromTemplate(void *pBaseAddress)
{
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
}
#endif // !USE_PORTABLE_HELPERS && !FEATURE_RX_THUNKS

REDHAWK_PALEXPORT UInt32_BOOL REDHAWK_PALAPI PalMarkThunksAsValidCallTargets(
    void *virtualAddress,
    int thunkSize,
    int thunksPerBlock,
    int thunkBlockSize,
    int thunkBlocksPerMapping)
{
    return UInt32_TRUE;
}

REDHAWK_PALEXPORT void REDHAWK_PALAPI PalSleep(uint32_t milliseconds)
{
#if HAVE_CLOCK_NANOSLEEP
    timespec endTime;
    clock_gettime(CLOCK_MONOTONIC, &endTime);
    TimeSpecAdd(&endTime, milliseconds);
    while (clock_nanosleep(CLOCK_MONOTONIC, TIMER_ABSTIME, &endTime, NULL) == EINTR)
    {
    }
#else // HAVE_CLOCK_NANOSLEEP
    timespec requested;
    requested.tv_sec = milliseconds / tccSecondsToMilliSeconds;
    requested.tv_nsec = (milliseconds - requested.tv_sec * tccSecondsToMilliSeconds) * tccMilliSecondsToNanoSeconds;

    timespec remaining;
    while (nanosleep(&requested, &remaining) == EINTR)
    {
        requested = remaining;
    }
#endif // HAVE_CLOCK_NANOSLEEP
}

REDHAWK_PALEXPORT UInt32_BOOL REDHAWK_PALAPI __stdcall PalSwitchToThread()
{
    // sched_yield yields to another thread in the current process. This implementation
    // won't work well for cross-process synchronization.
    return sched_yield() == 0;
}

extern "C" UInt32_BOOL CloseHandle(HANDLE handle)
{
    if ((handle == NULL) || (handle == INVALID_HANDLE_VALUE))
    {
        return UInt32_FALSE;
    }

    UnixHandleBase* handleBase = (UnixHandleBase*)handle;

    bool success = handleBase->Destroy();

    delete handleBase;

    return success ? UInt32_TRUE : UInt32_FALSE;
}

REDHAWK_PALEXPORT HANDLE REDHAWK_PALAPI PalCreateEventW(_In_opt_ LPSECURITY_ATTRIBUTES pEventAttributes, UInt32_BOOL manualReset, UInt32_BOOL initialState, _In_opt_z_ const wchar_t* pName)
{
    UnixEvent event = UnixEvent(manualReset, initialState);
    if (!event.Initialize())
    {
        return INVALID_HANDLE_VALUE;
    }

    EventUnixHandle* handle = new (nothrow) EventUnixHandle(event);

    if (handle == NULL)
    {
        return INVALID_HANDLE_VALUE;
    }

    return handle;
}

typedef UInt32(__stdcall *BackgroundCallback)(_In_opt_ void* pCallbackContext);

REDHAWK_PALEXPORT bool REDHAWK_PALAPI PalStartBackgroundWork(_In_ BackgroundCallback callback, _In_opt_ void* pCallbackContext, UInt32_BOOL highPriority)
{
#ifdef _WASM_
    // No threads, so we can't start one
    ASSERT(false);
#endif // _WASM_
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
#ifdef _WASM_
    // WASMTODO: No threads so we can't start the finalizer thread
    return true;
#else // _WASM_
    return PalStartBackgroundWork(callback, pCallbackContext, UInt32_TRUE);
#endif // _WASM_
}

// Returns a 64-bit tick count with a millisecond resolution. It tries its best
// to return monotonically increasing counts and avoid being affected by changes
// to the system clock (either due to drift or due to explicit changes to system
// time).
REDHAWK_PALEXPORT UInt64 REDHAWK_PALAPI PalGetTickCount64()
{
    UInt64 retval = 0;

#if HAVE_MACH_ABSOLUTE_TIME
    {
        retval = (mach_absolute_time() * s_TimebaseInfo.numer / s_TimebaseInfo.denom) / tccMilliSecondsToNanoSeconds;
    }
#elif HAVE_CLOCK_MONOTONIC
    {
        clockid_t clockType =
#if HAVE_CLOCK_MONOTONIC_COARSE
            CLOCK_MONOTONIC_COARSE; // good enough resolution, fastest speed
#else
            CLOCK_MONOTONIC;
#endif
        struct timespec ts;
        if (clock_gettime(clockType, &ts) == 0)
        {
            retval = (ts.tv_sec * tccSecondsToMilliSeconds) + (ts.tv_nsec / tccMilliSecondsToNanoSeconds);
        }
        else
        {
            ASSERT_UNCONDITIONALLY("clock_gettime(CLOCK_MONOTONIC) failed\n");
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
#endif

    return retval;
}

REDHAWK_PALEXPORT UInt32 REDHAWK_PALAPI PalGetTickCount()
{
    return (UInt32)PalGetTickCount64();
}

REDHAWK_PALEXPORT HANDLE REDHAWK_PALAPI PalGetModuleHandleFromPointer(_In_ void* pointer)
{
    HANDLE moduleHandle = NULL;

    // Emscripten's implementation of dladdr corrupts memory,
    // but always returns 0 for the module handle, so just skip the call
#if !defined(_WASM_)
    Dl_info info;
    int st = dladdr(pointer, &info);
    if (st != 0)
    {
        moduleHandle = info.dli_fbase;
    }
#endif //!defined(_WASM_)

    return moduleHandle;
}

REDHAWK_PALEXPORT void PalPrintFatalError(const char* message)
{
    // Write the message using lowest-level OS API available. This is used to print the stack overflow
    // message, so there is not much that can be done here.
    write(STDERR_FILENO, message, sizeof(message));
}

#ifdef __linux__
size_t
GetLogicalProcessorCacheSizeFromOS()
{
    size_t cacheSize = 0;

#ifdef _SC_LEVEL1_DCACHE_SIZE
    cacheSize = max(cacheSize, sysconf(_SC_LEVEL1_DCACHE_SIZE));
#endif
#ifdef _SC_LEVEL2_CACHE_SIZE
    cacheSize = max(cacheSize, sysconf(_SC_LEVEL2_CACHE_SIZE));
#endif
#ifdef _SC_LEVEL3_CACHE_SIZE
    cacheSize = max(cacheSize, sysconf(_SC_LEVEL3_CACHE_SIZE));
#endif
#ifdef _SC_LEVEL4_CACHE_SIZE
    cacheSize = max(cacheSize, sysconf(_SC_LEVEL4_CACHE_SIZE));
#endif
    return cacheSize;
}
#endif

bool QueryCacheSize()
{
    bool success = true;
    g_cbLargestOnDieCache = 0;

#ifdef __linux__

    g_cbLargestOnDieCache = GetLogicalProcessorCacheSizeFromOS();

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

#elif defined(_WASM_)
    // Processor cache size not available on WebAssembly, but we can't start up without it, so pick the same default as the GC does
    success = true;
    g_cbLargestOnDieCache = 256 * 1024;
#else
#error Do not know how to get cache size on this platform
#endif // __linux__

    // TODO: implement adjusted cache size
    g_cbLargestOnDieCacheAdjusted = g_cbLargestOnDieCache;

    return success;
}

bool QueryLogicalProcessorCount()
{
#if HAVE_SYSCONF
    g_cLogicalCpus = sysconf(SYSCONF_GET_NUMPROCS);
    if (g_cLogicalCpus < 1)
    {
        ASSERT_UNCONDITIONALLY("sysconf failed for " SYSCONF_GET_NUMPROCS_NAME "\n");
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
    case PAGE_EXECUTE_READ:
        prot = PROT_READ | PROT_EXEC;
        break;
    case PAGE_EXECUTE_READWRITE:
        prot = PROT_READ | PROT_WRITE | PROT_EXEC;
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
    ASSERT(((freeType & MEM_RELEASE) != MEM_RELEASE) || size == 0);
    ASSERT((freeType & (MEM_RELEASE | MEM_DECOMMIT)) != (MEM_RELEASE | MEM_DECOMMIT));
    ASSERT(freeType != 0);

    // UNIXTODO: Implement this function
    return UInt32_TRUE;
}

REDHAWK_PALEXPORT UInt32_BOOL REDHAWK_PALAPI PalVirtualProtect(_In_ void* pAddress, size_t size, uint32_t protect)
{
    int unixProtect = W32toUnixAccessControl(protect);

    return mprotect(pAddress, size, unixProtect) == 0;
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

extern "C" UInt32_BOOL IsDebuggerPresent()
{
#ifdef _WASM_
    // For now always true since the browser will handle it in case of WASM.
    return UInt32_TRUE;
#else
    // UNIXTODO: Implement this function
    return UInt32_FALSE;
#endif
}

extern "C" void TerminateProcess(HANDLE arg1, UInt32 arg2)
{
    // TODO: change it to TerminateCurrentProcess
    // Then if we modified the signature of the DuplicateHandle too, we can
    // get rid of the PalGetCurrentProcess.
    PORTABILITY_ASSERT("UNIXTODO: Implement this function");
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
    // UNIXTODO: Implement this function
    return 0;
}

typedef UInt32 (__stdcall *HijackCallback)(HANDLE hThread, _In_ PAL_LIMITED_CONTEXT* pThreadContext, _In_opt_ void* pCallbackContext);

REDHAWK_PALEXPORT UInt32 REDHAWK_PALAPI PalHijack(HANDLE hThread, _In_ HijackCallback callback, _In_opt_ void* pCallbackContext)
{
    // UNIXTODO: Implement PalHijack
    return E_FAIL;
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

#ifndef __has_builtin
#define __has_builtin(x) 0
#endif

#if !__has_builtin(_mm_pause)
extern "C" void _mm_pause()
// Defined for implementing PalYieldProcessor in PalRedhawk.h
{
#if defined(_AMD64_) || defined(_X86_)
  __asm__ volatile ("pause");
#endif
}
#endif

extern "C" Int32 _stricmp(const char *string1, const char *string2)
{
    return strcasecmp(string1, string2);
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
REDHAWK_PALEXPORT Int32 PalGetModuleFileName(_Out_ const TCHAR** pModuleNameOut, HANDLE moduleBase)
{
#if defined(_WASM_)
    // Emscripten's implementation of dladdr corrupts memory and doesn't have the real name, so make up a name instead
    const TCHAR* wasmModuleName = "WebAssemblyModule";
    *pModuleNameOut = wasmModuleName;
    return strlen(wasmModuleName);
#else // _WASM_
    Dl_info dl;
    if (dladdr(moduleBase, &dl) == 0)
    {
        *pModuleNameOut = NULL;
        return 0;
    }

    *pModuleNameOut = dl.dli_fname;
    return strlen(dl.dli_fname);
#endif // defined(_WASM_)
}

GCSystemInfo g_RhSystemInfo;

// Initialize the g_SystemInfo
bool InitializeSystemInfo()
{
    long pagesize = getpagesize();
    g_RhSystemInfo.dwPageSize = pagesize;
    g_RhSystemInfo.dwAllocationGranularity = pagesize;

    int nrcpus = 0;

#if HAVE_SYSCONF
    nrcpus = sysconf(SYSCONF_GET_NUMPROCS);
    if (nrcpus < 1)
    {
        ASSERT_UNCONDITIONALLY("sysconf failed for " SYSCONF_GET_NUMPROCS_NAME "\n");
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

    g_RhSystemInfo.dwNumberOfProcessors = nrcpus;

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

static const int64_t SECS_BETWEEN_1601_AND_1970_EPOCHS = 11644473600LL;
static const int64_t SECS_TO_100NS = 10000000; /* 10^7 */

extern "C" void GetSystemTimeAsFileTime(FILETIME *lpSystemTimeAsFileTime)
{
    struct timeval time = { 0 };
    gettimeofday(&time, NULL);

    int64_t result = ((int64_t)time.tv_sec + SECS_BETWEEN_1601_AND_1970_EPOCHS) * SECS_TO_100NS +
        (time.tv_usec * 10);

    lpSystemTimeAsFileTime->dwLowDateTime = (uint32_t)result;
    lpSystemTimeAsFileTime->dwHighDateTime = (uint32_t)(result >> 32);
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

extern "C" UInt64 PalGetCurrentThreadIdForLogging()
{
#if defined(__linux__)
    return (uint64_t)syscall(SYS_gettid);
#elif defined(__APPLE__)
    uint64_t tid;
    pthread_threadid_np(pthread_self(), &tid);
    return (uint64_t)tid;
#elif HAVE_PTHREAD_GETTHREADID_NP
    return (uint64_t)pthread_getthreadid_np();
#elif HAVE_LWP_SELF
    return (uint64_t)_lwp_self();
#else
    // Fallback in case we don't know how to get integer thread id on the current platform
    return (uint64_t)pthread_self();
#endif
}

#if defined(_X86_) || defined(_AMD64_)
REDHAWK_PALEXPORT uint32_t REDHAWK_PALAPI getcpuid(uint32_t arg, unsigned char result[16])
{
    DWORD eax;
#if defined(_X86_)
    __asm("  xor %%ecx, %%ecx\n" \
          "  cpuid\n" \
          "  mov %%eax, 0(%[result])\n" \
          "  mov %%ebx, 4(%[result])\n" \
          "  mov %%ecx, 8(%[result])\n" \
          "  mov %%edx, 12(%[result])\n" \
          : "=a"(eax) /*output in eax*/\
          : "a"(arg), [result]"r"(result) /*inputs - arg in eax, result in any register*/\
          : "ebx", "ecx", "edx", "memory" /* registers that are clobbered, *result is clobbered */
        );
#endif // defined(_X86_)
#if defined(_AMD64_)
    __asm("  xor %%ecx, %%ecx\n" \
          "  cpuid\n" \
          "  mov %%eax, 0(%[result])\n" \
          "  mov %%ebx, 4(%[result])\n" \
          "  mov %%ecx, 8(%[result])\n" \
          "  mov %%edx, 12(%[result])\n" \
          : "=a"(eax) /*output in eax*/\
          : "a"(arg), [result]"r"(result) /*inputs - arg in eax, result in any register*/\
          : "rbx", "ecx", "edx", "memory" /* registers that are clobbered, *result is clobbered */
        );
#endif // defined(_AMD64_)
    return eax;
}

REDHAWK_PALEXPORT uint32_t REDHAWK_PALAPI getextcpuid(uint32_t arg1, uint32_t arg2, unsigned char result[16])
{
    DWORD eax;
#if defined(_X86_)
    DWORD ecx;
    __asm("  cpuid\n" \
          "  mov %%eax, 0(%[result])\n" \
          "  mov %%ebx, 4(%[result])\n" \
          "  mov %%ecx, 8(%[result])\n" \
          "  mov %%edx, 12(%[result])\n" \
          : "=a"(eax), "=c"(ecx) /*output in eax, ecx is rewritten*/\
          : "c"(arg1), "a"(arg2), [result]"r"(result) /*inputs - arg1 in ecx, arg2 in eax, result in any register*/\
          : "ebx", "edx", "memory" /* registers that are clobbered, *result is clobbered */
        );
#endif // defined(_X86_)
#if defined(_AMD64_)
    __asm("  cpuid\n" \
          "  mov %%eax, 0(%[result])\n" \
          "  mov %%ebx, 4(%[result])\n" \
          "  mov %%ecx, 8(%[result])\n" \
          "  mov %%edx, 12(%[result])\n" \
          : "=a"(eax) /*output in eax*/\
          : "c"(arg1), "a"(arg2), [result]"r"(result) /*inputs - arg1 in ecx, arg2 in eax, result in any register*/\
          : "rbx", "edx", "memory" /* registers that are clobbered, *result is clobbered */
        );
#endif // defined(_AMD64_)
    return eax;
}

#endif // defined(_X86_) || defined(_AMD64_)
