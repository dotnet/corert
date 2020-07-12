// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma once

#include "CodeHeap.h"

#include <list>
#include <vector>
#include <unordered_map>
#include <mutex>

#if defined(TARGET_AMD64)
//
// ToDo - this should eventually come from windows header file.
//
// Define unwind code structure.
//

typedef union _UNWIND_CODE {
    struct {
        UCHAR CodeOffset;
        UCHAR UnwindOp : 4;
        UCHAR OpInfo : 4;
    };

    USHORT FrameOffset;
} UNWIND_CODE, *PUNWIND_CODE;

//
// Define unwind information flags.
//

#define UNW_FLAG_NHANDLER 0x0
#define UNW_FLAG_EHANDLER 0x1
#define UNW_FLAG_UHANDLER 0x2
#define UNW_FLAG_CHAININFO 0x4

typedef struct _UNWIND_INFO {
    UCHAR Version : 3;
    UCHAR Flags : 5;
    UCHAR SizeOfProlog;
    UCHAR CountOfUnwindCodes;
    UCHAR FrameRegister : 4;
    UCHAR FrameOffset : 4;
    UNWIND_CODE UnwindCode[1];
} UNWIND_INFO, *PUNWIND_INFO;

typedef DPTR(struct _UNWIND_INFO)      PTR_UNWIND_INFO;
typedef DPTR(union _UNWIND_CODE)       PTR_UNWIND_CODE;
#endif // target_amd64

class SlimReaderWriterLock : private SRWLOCK
{
public:
    SlimReaderWriterLock()
    {
        ::InitializeSRWLock(this);
    }

    class ReadHolder
    {
        SlimReaderWriterLock * m_pLock;
    public:
        ReadHolder(SlimReaderWriterLock * pLock)
            : m_pLock(pLock)
        {
            ::AcquireSRWLockShared(m_pLock);
        }

        ~ReadHolder()
        {
            ::ReleaseSRWLockShared(m_pLock);
        }
    };

    class WriteHolder
    {
        SlimReaderWriterLock * m_pLock;

    public:
        WriteHolder(SlimReaderWriterLock * pLock)
            : m_pLock(pLock)
        {
            ::AcquireSRWLockExclusive(m_pLock);
        }

        ~WriteHolder()
        {
            ::ReleaseSRWLockExclusive(m_pLock);
        }
    };
};

typedef DPTR(RUNTIME_FUNCTION) PTR_RUNTIME_FUNCTION;

// TODO: Not compatible with Windows 7
// #ifdef TARGET_AMD64
// #define USE_GROWABLE_FUNCTION_TABLE 1
// #endif

class CodeHeader
{
public:
    CodeHeader(void *m_heapBase, DWORD codeOffs);
    ~CodeHeader();

    void *operator new (size_t sz, void *mem)
    {
        return mem;
    }

    inline void *GetCode() const
    {
        return m_heapBase + m_codeOffset;
    }

    inline DWORD GetCodeOffset() const
    {
        return m_codeOffset;
    }

    inline void *GetHeapBase() const
    {
        return m_heapBase;
    }
    
    inline void SetEHInfo(void *ehInfo)
    {
        m_ehInfo = ehInfo;
    }

    inline void *GetEHInfo() const
    {
        return m_ehInfo;
    }

    inline size_t GetEHCount() const
    {
        size_t *ptr = (size_t*)GetEHInfo();
        assert(ptr != NULL);
        return *(ptr - 1);
    }

    inline EHClause *GetEHClause(unsigned i)
    {
        assert(i < GetEHCount());
        EHClause *ehInfo = (EHClause*)GetEHInfo();
        return &ehInfo[i];
    }

private:
    BYTE *m_heapBase;
    DWORD m_codeOffset;

    // Exception handling clauses
    // Storage layout: <Number of EH clauses><Clause1>...<ClauseN>
    // m_ehInfo will be pointing to the first clause.
    // Number of EH clauses = *((size_t*)((byte*) m_ehInfo - sizeof(size_t)))
    void *m_ehInfo;
};

class JITCodeManager : ICodeManager
{
    PTR_VOID m_pvStartRange;
    UInt32 m_cbRange;

    // lock to protect m_runtimeFunctions and m_FuncletToMainMethodMap
    SlimReaderWriterLock m_lock;

    std::vector<RUNTIME_FUNCTION> m_runtimeFunctions;
    PTR_RUNTIME_FUNCTION m_pRuntimeFunctionTable;
    UInt32 m_nRuntimeFunctionTable;

#ifdef USE_GROWABLE_FUNCTION_TABLE
    PTR_VOID m_hGrowableFunctionTable;
#endif

    // Given BeginAddress of a funclet, this data structure maps to 
    // BeginAddress of its main method.
    std::unordered_map<DWORD, DWORD> m_FuncletToMainMethodMap;

    // For now we are using the desktop concept of multiple CodeManagers for multiple ranges
    // of JIT'ed code.  The current implementation is meant to be the simplest possible so
    // that it will be easy to refactor into a better/more permanent version later.
    ExecutableCodeHeap m_codeHeap;

    static std::list<JITCodeManager*> s_instances;
    static JITCodeManager * volatile s_pLastCodeManager;
    static std::mutex s_instanceLock;
    typedef std::lock_guard<std::mutex> MutexHolder;

    // Get the code header given method's start address
    static inline CodeHeader* GetCodeHeader(void *methodStart)
    {
        return (CodeHeader*)((BYTE*)methodStart - sizeof(CodeHeader));
    }

public:
    // Finds the code manager associated with a particular address.
    static JITCodeManager *FindCodeManager(PTR_VOID addr);

    // Finds a JITCodeManager instance with free space and allocates executable memory.
    // This function throws on failure, and passes out the code address and JIT manager used.
    static void AllocCode(size_t size, DWORD align, void **ppCode, JITCodeManager **ppManager);

public:
    JITCodeManager();
    ~JITCodeManager();

    bool Initialize();

    void *AllocPData(size_t size)
    {
        return m_codeHeap.AllocPData(size);
    }

    void *AllocEHInfo(CodeHeader *hdr, unsigned cEH)
    {
        size_t size = sizeof(size_t)+sizeof(struct EHClause) * cEH;
        size_t *ehInfo = (size_t *)m_codeHeap.AllocEHInfoRaw(size);
        *ehInfo = cEH;
        hdr->SetEHInfo(ehInfo + 1);
        
        return ehInfo;
    }

    PTR_RUNTIME_FUNCTION AllocRuntimeFunction(PTR_RUNTIME_FUNCTION mainMethod, DWORD beginAddr, DWORD endAddr, DWORD unwindData);

    inline bool Contains(void *pCode) const
    {
        return m_pvStartRange <= pCode && pCode < (void*)((BYTE*)m_pvStartRange + m_cbRange);
    }


    void UpdateRuntimeFunctionTable();

    //
    // Code manager methods
    //

    bool FindMethodInfo(PTR_VOID        ControlPC, 
                        MethodInfo *    pMethodInfoOut);

    bool IsFunclet(MethodInfo * pMethodInfo);

    PTR_VOID GetFramePointer(MethodInfo *   pMethodInfo,
                             REGDISPLAY *   pRegisterSet);

    void EnumGcRefs(MethodInfo *    pMethodInfo, 
                    PTR_VOID        safePointAddress,
                    REGDISPLAY *    pRegisterSet,
                    GCEnumContext * hCallback);

    bool UnwindStackFrame(MethodInfo *    pMethodInfo,
                          REGDISPLAY *    pRegisterSet,                 // in/out
                          PTR_VOID *      ppPreviousTransitionFrame);   // out

    bool GetReturnAddressHijackInfo(MethodInfo *    pMethodInfo,
                                    REGDISPLAY *    pRegisterSet,       // in
                                    PTR_PTR_VOID *  ppvRetAddrLocation, // out
                                    GCRefKind *     pRetValueKind);     // out

    void UnsynchronizedHijackMethodLoops(MethodInfo * pMethodInfo);

    PTR_VOID RemapHardwareFaultToGCSafePoint(MethodInfo * pMethodInfo, PTR_VOID controlPC);

    bool EHEnumInit(MethodInfo * pMethodInfo, PTR_VOID * pMethodStartAddress, EHEnumState * pEHEnumState);

    bool EHEnumNext(EHEnumState * pEHEnumState, EHClause * pEHClause);

    UIntNative GetConservativeUpperBoundForOutgoingArgs(MethodInfo *   pMethodInfo,
                                                        REGDISPLAY *   pRegisterSet);

    PTR_VOID GetMethodStartAddress(MethodInfo * pMethodInfo);

    void * GetClasslibFunction(ClasslibFunctionId functionId);

    PTR_VOID GetAssociatedData(PTR_VOID ControlPC);

    PTR_VOID GetOsModuleHandle();
};
