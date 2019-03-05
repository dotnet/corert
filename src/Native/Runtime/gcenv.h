// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#define FEATURE_PREMORTEM_FINALIZATION

#ifdef _MSC_VER
#pragma warning( disable: 4189 )  // 'hp': local variable is initialized but not referenced -- common in GC
#pragma warning( disable: 4127 )  // conditional expression is constant -- common in GC
#endif

#include "sal.h"
#include "gcenv.structs.h"
#include "gcenv.os.h"
#include "gcenv.interlocked.h"
#include "gcenv.base.h"

#include "Crst.h"
#include "event.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "TargetPtrs.h"
#include "eetype.h"
#include "ObjectLayout.h"
#include "rheventtrace.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"
#include "gcrhinterface.h"
#include "gcenv.interlocked.inl"

#include "slist.h"
#include "RWLock.h"
#include "shash.h"
#include "module.h"
#include "RuntimeInstance.h"
#include "eetype.inl"

#include "stressLog.h"
#ifdef FEATURE_ETW

    #ifndef _INC_WINDOWS
        typedef void* LPVOID;
        typedef uint32_t UINT;
        typedef void* PVOID;
        typedef uint64_t ULONGLONG;
        typedef uint32_t ULONG;
        typedef int64_t LONGLONG;
        typedef uint8_t BYTE;
        typedef uint16_t UINT16;
    #endif // _INC_WINDOWS

    #include "etwevents.h"
    #include "eventtrace.h"

#else // FEATURE_ETW

    #include "etmdummy.h"
    #define ETW_EVENT_ENABLED(e,f) false

#endif // FEATURE_ETW

#define MAX_LONGPATH 1024
#define LOG(x)

#ifndef YieldProcessor
#define YieldProcessor PalYieldProcessor
#endif

// Adapter for GC's view of Array
class ArrayBase : Array
{
public:
    DWORD GetNumComponents()
    {
        return m_Length;
    }

    static size_t GetOffsetOfNumComponents()
    {
        return offsetof(ArrayBase, m_Length);
    }
};

//
// -----------------------------------------------------------------------------------------------------------
//
// Bridge GC/HandleTable's version of MethodTable to Redhawk's EEType. Neither component tries to access any
// fields of MethodTable directly so this is mostly just a case of providing all the CLR-style accessors they
// need implemented on top of EEType functionality (we can simply recast the 'this' pointer into an EEType
// pointer).
//
// ****** NOTE: Do NOT attempt to add fields or virtual methods to this class! The pointer passed in 'this'
// ****** really does point to an EEType (there's no such thing as a MethodTable structure in RH).
//
class MethodTable
{
public:
    UInt32 GetBaseSize() { return ((EEType*)this)->get_BaseSize(); }
    UInt16 GetComponentSize() { return ((EEType*)this)->get_ComponentSize(); }
    UInt16 RawGetComponentSize() { return ((EEType*)this)->get_ComponentSize(); }
    UInt32 ContainsPointers() { return ((EEType*)this)->HasReferenceFields(); }
    UInt32 ContainsPointersOrCollectible() { return ((EEType*)this)->HasReferenceFields(); }
    UInt32_BOOL HasComponentSize() const { return TRUE; }
#ifdef FEATURE_PREMORTEM_FINALIZATION
    UInt32_BOOL HasFinalizer() { return ((EEType*)this)->HasFinalizer(); }
    UInt32_BOOL HasCriticalFinalizer() { return FALSE; }
#endif // FEATURE_PREMORTEM_FINALIZATION
#ifdef FEATURE_STRUCTALIGN
#ifdef FEATURE_BARTOK
    UInt32 GetRequiredAlignment() const { return ((EEType*)this)->get_BaseAlignment(); }
#else // FEATURE_BARTOK
    UInt32 GetRequiredAlignment() const { return sizeof(void*); }
#endif // FEATURE_BARTOK
#endif // FEATURE_STRUCTALIGN
    bool RequiresAlign8() { return ((EEType*)this)->RequiresAlign8(); }
    bool IsValueType() { return ((EEType*)this)->get_IsValueType(); }
    UInt32_BOOL SanityCheck() { return ((EEType*)this)->Validate(); }
};

class EEConfig
{
    UInt8 m_gcStressMode;

public:
    enum HeapVerifyFlags {
        HEAPVERIFY_NONE             = 0,
        HEAPVERIFY_GC               = 1,   // Verify the heap at beginning and end of GC
        HEAPVERIFY_BARRIERCHECK     = 2,   // Verify the brick table
        HEAPVERIFY_SYNCBLK          = 4,   // Verify sync block scanning

        // the following options can be used to mitigate some of the overhead introduced
        // by heap verification.  some options might cause heap verifiction to be less
        // effective depending on the scenario.

        HEAPVERIFY_NO_RANGE_CHECKS  = 0x10,   // Excludes checking if an OBJECTREF is within the bounds of the managed heap
        HEAPVERIFY_NO_MEM_FILL      = 0x20,   // Excludes filling unused segment portions with fill pattern
        HEAPVERIFY_POST_GC_ONLY     = 0x40,   // Performs heap verification post-GCs only (instead of before and after each GC)
        HEAPVERIFY_DEEP_ON_COMPACT  = 0x80    // Performs deep object verfication only on compacting GCs.
    };

    typedef enum {
        CONFIG_SYSTEM,
        CONFIG_APPLICATION,
        CONFIG_SYSTEMONLY
    } ConfigSearch;

    enum  GCStressFlags {
        GCSTRESS_NONE               = 0,
        GCSTRESS_ALLOC              = 1,    // GC on all allocs and 'easy' places
        GCSTRESS_TRANSITION         = 2,    // GC on transitions to preemtive GC
        GCSTRESS_INSTR_JIT          = 4,    // GC on every allowable JITed instr
        GCSTRESS_INSTR_NGEN         = 8,    // GC on every allowable NGEN instr
        GCSTRESS_UNIQUE             = 16,   // GC only on a unique stack trace
    };

    // This is treated like a constructor--it is not allowed to fail.  We have it like this because we don't 
    // have a CRT to run a static constructor for us.  For now, at least, we don't want to do any heavy-weight
    // snooping of the environment to control any of these settings, so don't add any code like that here.
    void Construct()
    {
        m_gcStressMode = GCSTRESS_NONE;
    }

    uint32_t ShouldInjectFault(uint32_t faultType) const { UNREFERENCED_PARAMETER(faultType); return FALSE; }
   
    int     GetHeapVerifyLevel();
    bool    IsHeapVerifyEnabled()                 { return GetHeapVerifyLevel() != 0; }

    GCStressFlags GetGCStressLevel()        const { return (GCStressFlags) m_gcStressMode; }
    void    SetGCStressLevel(int val)             { m_gcStressMode = (UInt8) val;}
    bool    IsGCStressMix()                 const { return false; }

    int     GetGCtraceStart()               const { return 0; }
    int     GetGCtraceEnd  ()               const { return 1000000000; }
    int     GetGCtraceFac  ()               const { return 0; }
    int     GetGCprnLvl    ()               const { return 0; }
    bool    IsGCBreakOnOOMEnabled()         const { return false; }
#ifdef USE_PORTABLE_HELPERS
    // CORERT-TODO: remove this
    //              https://github.com/dotnet/corert/issues/2033
    int     GetGCgen0size  ()               const { return 100 * 1024 * 1024; }
#else
    int     GetGCgen0size  ()               const { return 0; }
#endif
    void    SetGCgen0size  (int iSize)            { UNREFERENCED_PARAMETER(iSize); }
    int     GetSegmentSize ()               const { return 0; }
    void    SetSegmentSize (int iSize)            { UNREFERENCED_PARAMETER(iSize); }
    int     GetGCconcurrent();
    void    SetGCconcurrent(int val)              { UNREFERENCED_PARAMETER(val); }
    int     GetGCLatencyMode()              const { return 1; }
    int     GetGCForceCompact()             const { return 0; }
    int     GetGCRetainVM ()                const { return 0; }
    int     GetGCTrimCommit()               const { return 0; }
    int     GetGCLOHCompactionMode()        const { return 0; }

    bool    GetGCAllowVeryLargeObjects ()   const { return true; }

    // We need conservative GC enabled for some edge cases around ICastable support. This doesn't have much
    // impact, it just makes the GC slightly more flexible in dealing with interior references (e.g. we can
    // conservatively report an interior reference inside a GC free object or in the non-valid tail of the
    // heap).
    bool    GetGCConservative()             const { return true; }

    bool    GetGCNoAffinitize()             const { return false; }
    int     GetGCHeapCount()                const { return 0; }
};
extern EEConfig* g_pConfig;

#ifdef VERIFY_HEAP
class SyncBlockCache;

extern SyncBlockCache g_sSyncBlockCache;

class SyncBlockCache
{
public:
    static SyncBlockCache *GetSyncBlockCache() { return &g_sSyncBlockCache; }
    void GCWeakPtrScan(void *pCallback, uintptr_t pCtx, int dummy)
    {
        UNREFERENCED_PARAMETER(pCallback);
        UNREFERENCED_PARAMETER(pCtx);
        UNREFERENCED_PARAMETER(dummy);
    }
    void GCDone(uint32_t demoting, int max_gen)
    {
        UNREFERENCED_PARAMETER(demoting);
        UNREFERENCED_PARAMETER(max_gen);
    }
    void VerifySyncTableEntry() {}

    DWORD GetActiveCount()
    {
        return 0;
    }
};

#endif // VERIFY_HEAP

EXTERN_C UInt32 _tls_index;
inline UInt16 GetClrInstanceId()
{
    return (UInt16)_tls_index;
}

class IGCHeap;
typedef DPTR(IGCHeap) PTR_IGCHeap;
typedef DPTR(uint32_t) PTR_uint32_t;

enum CLRDataEnumMemoryFlags : int;

enum ThreadType
{
    ThreadType_GC = 137,
};

#undef ClrFlsSetThreadType
#define ClrFlsSetThreadType(threadType) SetGCSpecialThread(threadType)
void SetGCSpecialThread(ThreadType threadType);

#if defined(ENABLE_PERF_COUNTERS) || defined(FEATURE_EVENT_TRACE)
// Note this is not updated in a thread safe way so the value may not be accurate. We get
// it accurately in full GCs if the handle count is requested.
extern DWORD g_dwHandles;
#endif // ENABLE_PERF_COUNTERS || FEATURE_EVENT_TRACE
