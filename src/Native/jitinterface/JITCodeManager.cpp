// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "common.h"

#include "JITCodeManager.h"

#include "CodeHeap.h"
#include <mutex>

#include "../Runtime/coreclr/GCInfoDecoder.h"

#ifdef USE_GROWABLE_FUNCTION_TABLE
#include <Windows.h>
#endif

void EnumGCRefs(PTR_VOID pGCInfo, UINT32 curOffs, REGDISPLAY * pRD, GCEnumContext * hCallback, bool executionAborted)
{
    GcInfoDecoder gcInfoDecoder(
                        GCInfoToken(pGCInfo),
                        GcInfoDecoderFlags (DECODE_GC_LIFETIMES | DECODE_SECURITY_OBJECT | DECODE_VARARG),
                        curOffs
                        );

    if (!gcInfoDecoder.EnumerateLiveSlots(
                        pRD,
                        false /* reportScratchSlots */,
                        executionAborted ? ICodeManagerFlags::ExecutionAborted : 0, // TODO: Flags?
                        hCallback->pCallback,
                        hCallback
                        ))
    {
        // TODO: Conservative GC?
        assert(false);
    }
}

#define NYI(name) { wprintf(L"Not yet implemented: %S\n", name); DebugBreak(); ExitProcess(1); }

typedef bool (__stdcall *pfnRegisterCodeManager)(ICodeManager * pCodeManager, PTR_VOID pvStartRange, UInt32 cbRange);
typedef void (__stdcall *pfnUnregisterCodeManager)(ICodeManager * pCodeManager);

std::once_flag s_RuntimeInit;
HMODULE s_hRuntime = NULL;
pfnRegisterCodeManager s_pfnRegisterCodeManager;
pfnUnregisterCodeManager s_pfnUnregisterCodeManager;

#if FEATURE_SINGLE_MODULE_RUNTIME
extern "C" bool RegisterCodeManager(ICodeManager * pCodeManager, PTR_VOID pvStartRange, UInt32 cbRange);
extern "C" void UnregisterCodeManager(ICodeManager * pCodeManager);
#endif

bool InitializeCodeManagerRuntime()
{
    std::call_once(s_RuntimeInit, []()
    {
        if (s_hRuntime != NULL)
        {
#if FEATURE_SINGLE_MODULE_RUNTIME
            s_pfnRegisterCodeManager = &RegisterCodeManager;
            s_pfnUnregisterCodeManager = &UnregisterCodeManager;
#else
            s_pfnRegisterCodeManager = (pfnRegisterCodeManager)GetProcAddress(s_hRuntime, "RegisterCodeManager");
            s_pfnUnregisterCodeManager = (pfnUnregisterCodeManager)GetProcAddress(s_hRuntime, "UnregisterCodeManager");
#endif
        }
    });

    return (s_pfnRegisterCodeManager != NULL) && (s_pfnUnregisterCodeManager != NULL);
}

// These are the flags set on an CORINFO_EH_CLAUSE
enum CORINFO_EH_CLAUSE_FLAGS
{
    CORINFO_EH_CLAUSE_NONE      = 0,
    CORINFO_EH_CLAUSE_FILTER    = 0x0001, // If this bit is on, then this EH entry is for a filter
    CORINFO_EH_CLAUSE_FINALLY   = 0x0002, // This clause is a finally clause
    CORINFO_EH_CLAUSE_FAULT     = 0x0004, // This clause is a fault clause
    CORINFO_EH_CLAUSE_DUPLICATE = 0x0008, // Duplicated clause. This clause was duplicated to a funclet which was pulled out of line
    CORINFO_EH_CLAUSE_SAMETRY   = 0x0010, // This clause covers same try block as the previous one. (Used by CoreRT ABI.)
};

extern "C" __declspec(dllexport) void __stdcall InitJitCodeManager(HMODULE mrtModule);
extern "C" __declspec(dllexport) void* __stdcall AllocJittedCode(UInt32 cbCode, UInt32 align, JITCodeManager** pCodeManager);
extern "C" __declspec(dllexport) void __stdcall SetEHInfoPtr(JITCodeManager* pCodeManager, uint8_t *pbCode, void* ehInfo);

extern "C" __declspec(dllexport) PTR_RUNTIME_FUNCTION __stdcall PublishRuntimeFunction(
    JITCodeManager *pCodeManager, 
    uint8_t *pbCode, 
    PTR_RUNTIME_FUNCTION pMainRuntimeFunction, 
    UInt32 startOffset, 
    UInt32 endOffset, 
    uint8_t* pUnwindInfo, 
    UInt32 cbUnwindInfo,
    uint8_t* pGCData, 
    UInt32 cbGCData);

extern "C" __declspec(dllexport) void __stdcall UpdateRuntimeFunctionTable(JITCodeManager *pCodeManager);

__declspec(dllexport) void __stdcall InitJitCodeManager(HMODULE mrtModule)
{
    s_hRuntime = mrtModule;
}

__declspec(dllexport) void* __stdcall AllocJittedCode(UInt32 cbCode, UInt32 align, JITCodeManager** pCodeManager)
{
    void *pCode;
    JITCodeManager::AllocCode(cbCode, align, &pCode, pCodeManager);
    return pCode;
}

CodeHeader *GetCodeHeader(uint8_t* pbCode)
{
    return (CodeHeader*)((BYTE*)pbCode - sizeof(CodeHeader));
}

__declspec(dllexport) void __stdcall SetEHInfoPtr(JITCodeManager* pCodeManager, uint8_t *pbCode, void* ehInfo)
{
    CodeHeader *hdr = GetCodeHeader(pbCode);
    hdr->SetEHInfo(ehInfo);
}

__declspec(dllexport) PTR_RUNTIME_FUNCTION __stdcall PublishRuntimeFunction(
    JITCodeManager *pCodeManager, 
    uint8_t *pbCode, 
    PTR_RUNTIME_FUNCTION pMainRuntimeFunction, 
    UInt32 startOffset, 
    UInt32 endOffset, 
    uint8_t* pUnwindInfo, 
    UInt32 cbUnwindInfo,
    uint8_t* pGCData, 
    UInt32 cbGCData)
{
    CodeHeader *hdr = GetCodeHeader(pbCode);
    DWORD codeOffset = hdr->GetCodeOffset();
    BYTE *pdataBase = (BYTE*)hdr->GetHeapBase();

    DWORD beginAddr = codeOffset + startOffset;
    DWORD endAddr = codeOffset + endOffset;

    uint8_t* pUnwindData = (uint8_t*)pCodeManager->AllocPData(cbUnwindInfo + cbGCData);
    if (pUnwindData == nullptr)
        return nullptr;

    memcpy(pUnwindData, pUnwindInfo, cbUnwindInfo);
    memcpy(pUnwindData + cbUnwindInfo, pGCData, cbGCData);
    assert(pUnwindData > pdataBase);
    assert((LONGLONG)pUnwindData - (LONGLONG)pdataBase < (LONGLONG)INT_MAX);
    DWORD unwindData = (DWORD)((PBYTE)pUnwindData - pdataBase);
    return pCodeManager->AllocRuntimeFunction(pMainRuntimeFunction, beginAddr, endAddr, unwindData);
}

__declspec(dllexport) void __stdcall UpdateRuntimeFunctionTable(JITCodeManager *pCodeManager)
{
    pCodeManager->UpdateRuntimeFunctionTable();
}

CodeHeader::CodeHeader(void *heapBase, DWORD codeOffs)
: m_heapBase((BYTE*)heapBase), m_codeOffset(codeOffs), m_ehInfo(NULL)
{
    assert(m_heapBase != nullptr);
    assert(codeOffs > 0);
}

CodeHeader::~CodeHeader()
{
}

std::list<JITCodeManager*> JITCodeManager::s_instances;
JITCodeManager * volatile JITCodeManager::s_pLastCodeManager = nullptr;
std::mutex JITCodeManager::s_instanceLock;

JITCodeManager *JITCodeManager::FindCodeManager(PTR_VOID addr)
{
    JITCodeManager *curr = s_pLastCodeManager;
    if (curr != nullptr && curr->Contains(addr))
        return curr;

    MutexHolder lock(s_instanceLock);
    for (auto instance : s_instances)
        if (instance->Contains(addr))
            return instance;

    return nullptr;
}

void JITCodeManager::AllocCode(size_t size, DWORD align, void **ppCode, JITCodeManager **ppManager)
{
    assert(ppCode != nullptr);
    JITCodeManager *curr = s_pLastCodeManager;
    
    // In practice we will only go around this loop once, and hopefully not take a lock.
    while (true)
    {
        if (curr != nullptr)
        {
            void *result = curr->m_codeHeap.AllocMemoryWithCodeHeader_NoThrow(size, align);
            if (result != nullptr)
            {
                *ppCode = result;
                if (ppManager)
                    *ppManager = curr;

                return;
            }
        }

        // Couldn't allocate with the last code manager, we now have to take a lock.
        MutexHolder lock(s_instanceLock);

        // Whoops, another thread came along and allocated a code manager.  Try again.
        if (s_pLastCodeManager != curr)
        {
            curr = s_pLastCodeManager;
            continue;
        }

        // Create a new code manager, and use it to allocate.
        JITCodeManager *pCodeMgr = new JITCodeManager();
        if (!pCodeMgr->Initialize())
            DebugBreak();  // TODO: We need to clean up error handling in mrtjit.dll.

        s_instances.push_back(pCodeMgr);
        curr = s_pLastCodeManager = pCodeMgr;
    }
}


// 8 meg code heap should be fine for bringup.
#define DEFAULT_JIT_CODE_SIZE 0x800000

JITCodeManager::JITCodeManager() : 
    m_pvStartRange(0), m_cbRange(0), 
    m_pRuntimeFunctionTable(NULL), m_nRuntimeFunctionTable(0)
{
#ifdef USE_GROWABLE_FUNCTION_TABLE
    m_hGrowableFunctionTable = NULL;
#endif

    // TODO: Clean up error handling.  This will only fail due to OOM.
    if (!m_codeHeap.Init(DEFAULT_JIT_CODE_SIZE))
        DebugBreak();

    m_pvStartRange = m_codeHeap.GetBase();
    m_cbRange = (UInt32)m_codeHeap.GetSize();
}

JITCodeManager::~JITCodeManager()
{
#ifdef USE_GROWABLE_FUNCTION_TABLE
    if (m_hGrowableFunctionTable != NULL)
        RtlDeleteGrowableFunctionTable(m_hGrowableFunctionTable);
#endif

    s_pfnUnregisterCodeManager(this);
}

bool JITCodeManager::Initialize()
{
    if (!InitializeCodeManagerRuntime())
        return false;

    return s_pfnRegisterCodeManager(this, m_pvStartRange, m_cbRange);
}

// Allocates RUNTIME_FUNCTION entry. If it corresponds to a funclet also adds a mapping
// from funclet's RUNTIME_FUNCTION to its main method's RUNTIME_FUNCTION.
// Note that main method bodies will not have an entry in the map.
PTR_RUNTIME_FUNCTION JITCodeManager::AllocRuntimeFunction(PTR_RUNTIME_FUNCTION mainMethod, DWORD beginAddr, DWORD endAddr, DWORD unwindData)
{
    SlimReaderWriterLock::WriteHolder lh(&m_lock);

    m_runtimeFunctions.push_back(RUNTIME_FUNCTION());
    PTR_RUNTIME_FUNCTION method = &m_runtimeFunctions.back();

    method->BeginAddress = beginAddr;
    method->EndAddress = endAddr;
    method->UnwindData = unwindData;

    // also add an entry to map funclet to its main method
    if (mainMethod != NULL)
        m_FuncletToMainMethodMap[method->BeginAddress] = mainMethod->BeginAddress;

    return method;
}


void JITCodeManager::UpdateRuntimeFunctionTable()
{
    SlimReaderWriterLock::WriteHolder lh(&m_lock);
    
    PTR_RUNTIME_FUNCTION pFunctionTable = &m_runtimeFunctions[0];
    DWORD nEntryCount = (DWORD)m_runtimeFunctions.size();
    DWORD nMaximumEntryCount = (DWORD)m_runtimeFunctions.capacity();

#ifdef USE_GROWABLE_FUNCTION_TABLE
    if (m_pRuntimeFunctionTable == pFunctionTable)
    {
        if (m_hGrowableFunctionTable != NULL)
            RtlGrowFunctionTable(m_hGrowableFunctionTable, nEntryCount);
    }
    else
    {
        if (m_hGrowableFunctionTable != NULL)
        {
            RtlDeleteGrowableFunctionTable(m_hGrowableFunctionTable);
            m_hGrowableFunctionTable = NULL;
        }

        // Note that there is a short time when the table is not published...

        DWORD ret = RtlAddGrowableFunctionTable(&m_hGrowableFunctionTable, pFunctionTable, nEntryCount, nMaximumEntryCount,
                                                dac_cast<TADDR>(m_pvStartRange), dac_cast<TADDR>(m_pvStartRange) + m_cbRange);
        if (ret != 0)
        {
            OutputDebugString(L"Failed to register unwindinfo");
            m_hGrowableFunctionTable = NULL;
        }
    }
#endif

    m_pRuntimeFunctionTable = pFunctionTable;
    m_nRuntimeFunctionTable = nEntryCount;
}

static int LookupUnwindInfoForMethod(UInt32 RelativePc,
                                     PTR_RUNTIME_FUNCTION pRuntimeFunctionTable,
                                     int Low,
                                     int High)
{
#ifdef TARGET_ARM
    RelativePc |= THUMB_CODE;
#endif 

    // Entries are sorted and terminated by sentinel value (DWORD)-1

    // Binary search the RUNTIME_FUNCTION table
    // Use linear search once we get down to a small number of elements
    // to avoid Binary search overhead.
    while (High - Low > 10) 
    {
       int Middle = Low + (High - Low) / 2;

       PTR_RUNTIME_FUNCTION pFunctionEntry = pRuntimeFunctionTable + Middle;
       if (RelativePc < pFunctionEntry->BeginAddress) 
       {
           High = Middle - 1;
       } 
       else 
       {
           Low = Middle;
       }
    }

    for (int i = Low; i <= High; ++i)
    {
        // This is safe because of entries are terminated by sentinel value (DWORD)-1
        PTR_RUNTIME_FUNCTION pNextFunctionEntry = pRuntimeFunctionTable + (i + 1);

        if (RelativePc < pNextFunctionEntry->BeginAddress)
        {
            PTR_RUNTIME_FUNCTION pFunctionEntry = pRuntimeFunctionTable + i;
            if (RelativePc >= pFunctionEntry->BeginAddress)
            {
                return i;
            }
            break;
        }
    }

    return -1;
}

struct JITMethodInfo
{
    RUNTIME_FUNCTION mainRuntimeFunction;
    RUNTIME_FUNCTION runtimeFunction;
    bool executionAborted;
};

static_assert(sizeof(JITMethodInfo) <= sizeof(MethodInfo), "Ensure that EEMethodInfo fits into the space reserved by MethodInfo");

bool JITCodeManager::FindMethodInfo(PTR_VOID        ControlPC, 
                                    MethodInfo *    pMethodInfoOut)
{
    JITMethodInfo * pMethodInfo = (JITMethodInfo *)pMethodInfoOut;

    TADDR RelativePC = dac_cast<TADDR>(ControlPC) - dac_cast<TADDR>(m_pvStartRange);

    if (RelativePC >= m_cbRange)
        return false;

    SlimReaderWriterLock::ReadHolder lh(&m_lock);

    int MethodIndex = LookupUnwindInfoForMethod((UInt32)RelativePC, m_pRuntimeFunctionTable, 
        0, m_nRuntimeFunctionTable - 1);
    if (MethodIndex < 0)
        return false;

    pMethodInfo->runtimeFunction = m_pRuntimeFunctionTable[MethodIndex];

    // The runtime function could correspond to a funclet.  We need to get to the 
    // runtime function of the main method. Note that main method bodies will not
    // have an entry in the map.
    int mainMethodIndex;
    std::unordered_map<DWORD, DWORD>::const_iterator iter = m_FuncletToMainMethodMap.find(pMethodInfo->runtimeFunction.BeginAddress);
    if (iter != m_FuncletToMainMethodMap.end())
    {
        DWORD mainMethodBeginAddr = iter->second;
        
        mainMethodIndex = LookupUnwindInfoForMethod(mainMethodBeginAddr, m_pRuntimeFunctionTable,
                                                    0, m_nRuntimeFunctionTable - 1);
        if (MethodIndex < 0)
            return false;
    }
    else
    {
        // only main methods will not have an entry in this map
        mainMethodIndex = MethodIndex;
    }
    pMethodInfo->mainRuntimeFunction = m_pRuntimeFunctionTable[mainMethodIndex];
    pMethodInfo->executionAborted = false;

    return true;
}

bool JITCodeManager::IsFunclet(MethodInfo * pMethInfo)
{    
    JITMethodInfo * pMethodInfo = (JITMethodInfo *)pMethInfo;
    
    // A funclet will have an entry in funclet to main method map
    SlimReaderWriterLock::ReadHolder lh(&m_lock);
    return m_FuncletToMainMethodMap.find(pMethodInfo->runtimeFunction.BeginAddress) != m_FuncletToMainMethodMap.end();
}

PTR_VOID JITCodeManager::GetFramePointer(MethodInfo *   pMethodInfo,
                                         REGDISPLAY *   pRegisterSet)
{
    // If the method has EHinfo then it is guaranteed to have a frame pointer.
    JITMethodInfo * pJITMethodInfo = (JITMethodInfo *)pMethodInfo;

    // Get to unwind info
    PUNWIND_INFO pUnwindInfo = (PUNWIND_INFO)(dac_cast<TADDR>(m_pvStartRange) + dac_cast<TADDR>(pJITMethodInfo->mainRuntimeFunction.UnwindData));

    if (pUnwindInfo->FrameRegister != 0)
    {
        (PTR_VOID)pRegisterSet->GetFP();
    }

    return NULL;
}

static PTR_VOID GetUnwindDataBlob(TADDR moduleBase, RUNTIME_FUNCTION * pRuntimeFunction, /* out */ SIZE_T * pSize)
{
#if defined(TARGET_AMD64)
    PTR_UNWIND_INFO pUnwindInfo(dac_cast<PTR_UNWIND_INFO>(moduleBase + pRuntimeFunction->UnwindData));

    SIZE_T size = offsetof(UNWIND_INFO, UnwindCode) + sizeof(UNWIND_CODE) * pUnwindInfo->CountOfUnwindCodes;

    // TODO: Personality routine
    // size = ALIGN_UP(size, sizeof(DWORD))+ sizeof(DWORD);

    *pSize = size;

    return pUnwindInfo;

#elif defined(TARGET_ARM)

    // if this function uses packed unwind data then at least one of the two least significant bits
    // will be non-zero.  if this is the case then there will be no xdata record to enumerate.
    _ASSERTE((pRuntimeFunction->UnwindData & 0x3) == 0);

    // compute the size of the unwind info
    PTR_TADDR xdata = dac_cast<PTR_TADDR>(pRuntimeFunction->UnwindData + moduleBase);

    ULONG epilogScopes = 0;
    ULONG unwindWords = 0;
    ULONG size = 0;

    if ((xdata[0] >> 23) != 0)
    {
        size = 4;
        epilogScopes = (xdata[0] >> 23) & 0x1f;
        unwindWords = (xdata[0] >> 28) & 0x0f;
    }
    else
    {
        size = 8;
        epilogScopes = xdata[1] & 0xffff;
        unwindWords = (xdata[1] >> 16) & 0xff;
    }

    if (!(xdata[0] & (1 << 21)))
        size += 4 * epilogScopes;

    size += 4 * unwindWords;

    _ASSERTE(xdata[0] & (1 << 20)); // personality routine should be always present
    size += 4;

    *pSize = size;
    return xdata;
#else
    POTABILITY_ASSERT("GetUnwindDataBlob");
    return NULL;
#endif
}

void EnumGCRefs(PTR_VOID pGCInfo, UINT32 curOffs, REGDISPLAY * pRD, GCEnumContext * hCallback, bool executionAborted);

void JITCodeManager::EnumGcRefs(MethodInfo *    pMethodInfo, 
                                PTR_VOID        safePointAddress,
                                REGDISPLAY *    pRegisterSet,
                                GCEnumContext * hCallback)
{
    JITMethodInfo * pJITMethodInfo = (JITMethodInfo *)pMethodInfo;
    void *methodStartAddr = (BYTE*)m_pvStartRange + pJITMethodInfo->mainRuntimeFunction.BeginAddress;

    UInt32 codeOffset = (UInt32)(dac_cast<TADDR>(safePointAddress) - dac_cast<TADDR>(methodStartAddr));

    SIZE_T nUnwindDataSize;
    PTR_VOID pUnwindData = GetUnwindDataBlob(dac_cast<TADDR>(m_pvStartRange), &pJITMethodInfo->mainRuntimeFunction, &nUnwindDataSize);

    // GCInfo immediatelly follows unwind data
    PTR_VOID pGCInfo = dac_cast<PTR_VOID>(dac_cast<TADDR>(pUnwindData) + nUnwindDataSize);

    ::EnumGCRefs(pGCInfo, codeOffset, pRegisterSet, hCallback, pJITMethodInfo->executionAborted);
}

bool JITCodeManager::UnwindStackFrame(MethodInfo *    pMethodInfo,
                                      REGDISPLAY *    pRegisterSet,                 // in/out
                                      PTR_VOID *      ppPreviousTransitionFrame)    // out
{
    JITMethodInfo * pJITMethodInfo = (JITMethodInfo *)pMethodInfo;

    // TODO: PInvoke transitions
    *ppPreviousTransitionFrame = NULL;

    CONTEXT context;
    KNONVOLATILE_CONTEXT_POINTERS contextPointers;

#ifdef _DEBUG
    memset(&context, 0xDD, sizeof(context));
    memset(&contextPointers, 0xDD, sizeof(contextPointers));
#endif

    // TODO: Local copy of the OS unwinder to avoid the CONTEXT copying?

#define FOR_EACH_NONVOLATILE_REGISTER(F) \
    F(Rax) F(Rcx) F(Rdx) F(Rbx) F(Rbp) F(Rsi) F(Rdi) F(R8) F(R9) F(R10) F(R11) F(R12) F(R13) F(R14) F(R15)

#define REGDISPLAY_TO_CONTEXT(reg) \
    contextPointers.reg = (PDWORD64) pRegisterSet->p##reg; \
    if (pRegisterSet->p##reg != NULL) context.reg = *(pRegisterSet->p##reg);

#define CONTEXT_TO_REGDISPLAY(reg) \
    pRegisterSet->p##reg = (PTR_UIntNative) contextPointers.reg;

    FOR_EACH_NONVOLATILE_REGISTER(REGDISPLAY_TO_CONTEXT);

    memcpy(&context.Xmm6, pRegisterSet->Xmm, sizeof(pRegisterSet->Xmm));

    context.Rsp = pRegisterSet->SP;
    context.Rip = pRegisterSet->IP;

    SIZE_T  EstablisherFrame;
    PVOID   HandlerData;

    RtlVirtualUnwind(NULL,
                    dac_cast<TADDR>(m_pvStartRange),
                    pRegisterSet->IP,
                    &pJITMethodInfo->runtimeFunction,
                    &context,
                    &HandlerData,
                    &EstablisherFrame,
                    &contextPointers);

    pRegisterSet->SP = context.Rsp;
    pRegisterSet->IP = context.Rip;

    pRegisterSet->pIP = PTR_PCODE(pRegisterSet->SP - sizeof(TADDR));

    memcpy(pRegisterSet->Xmm, &context.Xmm6, sizeof(pRegisterSet->Xmm));

    FOR_EACH_NONVOLATILE_REGISTER(CONTEXT_TO_REGDISPLAY);

#undef FOR_EACH_NONVOLATILE_REGISTER
#undef REGDISPLAY_TO_CONTEXT
#undef CONTEXT_TO_REGDISPLAY

    return true;
}

bool JITCodeManager::GetReturnAddressHijackInfo(MethodInfo *    pMethodInfo,
                                                REGDISPLAY *    pRegisterSet,       // in
                                                PTR_PTR_VOID *  ppvRetAddrLocation, // out
                                                GCRefKind *     pRetValueKind)      // out
{
    NYI("GetReturnAddressHijackInfo");
}

void JITCodeManager::UnsynchronizedHijackMethodLoops(MethodInfo * pMethodInfo)
{
    NYI("UnsynchronizedHijackMethodLoops");
}

PTR_VOID JITCodeManager::RemapHardwareFaultToGCSafePoint(MethodInfo * pMethodInfo, PTR_VOID controlPC)
{
    // TODO - GCInfo decoder needs to know whether execution of the method is aborted 
    // while querying for gc-info.  But ICodeManager::EnumGCRef() doesn't receive any
    // flags from mrt.  For this reason on short-term, call to this method is used as
    // a cue to mark the method info as execution aborted.  If pMethodInfo is cached 
    // by mrt, this scheme will not work.
    //
    // If the method has EH, then JIT will make sure the method is fully interruptible
    // and we will have GC-info available at the faulting address as well.

    JITMethodInfo * pJITMethodInfo = (JITMethodInfo *)pMethodInfo;
    pJITMethodInfo->executionAborted = true;

    return controlPC;
}

struct EEEHEnumState
{
    PTR_UInt8 pMethodStartAddress;
    PTR_UInt8 pEHInfo;
    UInt32 uClause;
    UInt32 nClauses;
};

static_assert(sizeof(EEEHEnumState) <= sizeof(EHEnumState), "Ensure that EEEHEnumState fits into the space reserved by EHEnumState");

bool JITCodeManager::EHEnumInit(MethodInfo * pMethodInfo, PTR_VOID * pMethodStartAddress, EHEnumState * pEHEnumStateOut)
{
    assert(pMethodInfo != NULL);
    assert(pMethodStartAddress != NULL);
    assert(pEHEnumStateOut != NULL);

    // return if there is no EH info associated with this method
    JITMethodInfo * pJITMethodInfo = (JITMethodInfo *)pMethodInfo;
    void *methodStartAddr = (BYTE*)m_pvStartRange + pJITMethodInfo->mainRuntimeFunction.BeginAddress;
    CodeHeader* hdr = GetCodeHeader(methodStartAddr);
    void *ehInfo = hdr->GetEHInfo();
    if (ehInfo == NULL)
    {
        return false;
    }

    *pMethodStartAddress = methodStartAddr;

    EEEHEnumState * pEnumState = (EEEHEnumState *)pEHEnumStateOut;
    pEnumState->pMethodStartAddress = dac_cast<PTR_UInt8>(methodStartAddr);
    pEnumState->pEHInfo = (PTR_UInt8)ehInfo;
    pEnumState->uClause = 0;
    pEnumState->nClauses = VarInt::ReadUnsigned(pEnumState->pEHInfo);

    return true;
}

bool JITCodeManager::EHEnumNext(EHEnumState * pEHEnumState, EHClause * pEHClauseOut)
{
    assert(pEHEnumState != NULL);
    assert(pEHClauseOut != NULL);

    EEEHEnumState * pEnumState = (EEEHEnumState *)pEHEnumState;
    if (pEnumState->uClause >= pEnumState->nClauses)
    {
        return false;
    }

    if (pEnumState->uClause >= pEnumState->nClauses)
    {
        return false;
    }

    pEnumState->uClause++;

    pEHClauseOut->m_tryStartOffset = VarInt::ReadUnsigned(pEnumState->pEHInfo);

    UInt32 tryEndDeltaAndClauseKind = VarInt::ReadUnsigned(pEnumState->pEHInfo);
    pEHClauseOut->m_clauseKind = (EHClauseKind)(tryEndDeltaAndClauseKind & 0x3);
    pEHClauseOut->m_tryEndOffset = pEHClauseOut->m_tryStartOffset + (tryEndDeltaAndClauseKind >> 2);

    // For each clause, we have up to 4 integers:
    //      1)  try start offset
    //      2)  (try length << 2) | clauseKind
    //      3)  if (typed || fault || filter)    { handler start offset }
    //      4a) if (typed)                       { type RVA }
    //      4b) if (filter)                      { filter start offset }
    //
    // The first two integers have already been decoded

    switch (pEHClauseOut->m_clauseKind)
    {
    case EH_CLAUSE_TYPED:
        pEHClauseOut->m_handlerAddress = pEnumState->pMethodStartAddress + VarInt::ReadUnsigned(pEnumState->pEHInfo);

        // Read target type
        {
            // @TODO: CORERT: Compress EHInfo using type table index scheme
            // https://github.com/dotnet/corert/issues/972
            Int32 typeRelAddr = *((PTR_Int32&)pEnumState->pEHInfo)++;
            pEHClauseOut->m_pTargetType = *dac_cast<PTR_PTR_VOID>(pEnumState->pEHInfo + typeRelAddr);
        }
        break;
    case EH_CLAUSE_FAULT:
        pEHClauseOut->m_handlerAddress = pEnumState->pMethodStartAddress + VarInt::ReadUnsigned(pEnumState->pEHInfo);
        break;
    case EH_CLAUSE_FILTER:
        pEHClauseOut->m_handlerAddress = pEnumState->pMethodStartAddress + VarInt::ReadUnsigned(pEnumState->pEHInfo);
        pEHClauseOut->m_filterAddress = pEnumState->pMethodStartAddress + VarInt::ReadUnsigned(pEnumState->pEHInfo);
        break;
    default:
        assert(!"unexpected EHClauseKind");
    }

    return true;
}

UIntNative JITCodeManager::GetConservativeUpperBoundForOutgoingArgs(MethodInfo *   pMethodInfo, REGDISPLAY *   pRegisterSet)
{
    // @TODO: CORERT: GetConservativeUpperBoundForOutgoingArgs
    assert(false);
    return false;
}

PTR_VOID JITCodeManager::GetOsModuleHandle()
{
    // Should not be called
    assert(false);
    return nullptr;
}

PTR_VOID JITCodeManager::GetMethodStartAddress(MethodInfo * pMethodInfo)
{
    JITMethodInfo * pJITMethodInfo = (JITMethodInfo *)pMethodInfo;
    void *methodStartAddr = (BYTE*)m_pvStartRange + pJITMethodInfo->mainRuntimeFunction.BeginAddress;
    return methodStartAddr;
}

void * JITCodeManager::GetClasslibFunction(ClasslibFunctionId functionId)
{
    // @TODO: CORERT: GetClasslibFunction
    // Implement by delegating to corelib code manager
    assert(false);
    return false;
}

PTR_VOID JITCodeManager::GetAssociatedData(PTR_VOID ControlPC)
{
    // @TODO: CORERT: GetAssociatedData
    assert(false);
    return NULL;
}
