// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#include "common.h"

#include <windows.h>

#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "PalRedhawkCommon.h"
#include "regdisplay.h"
#include "ICodeManager.h"
#include "CoffNativeCodeManager.h"
#include "varint.h"

#include "CommonMacros.inl"

#define UBF_FUNC_KIND_MASK      0x03
#define UBF_FUNC_KIND_ROOT      0x00
#define UBF_FUNC_KIND_HANDLER   0x01
#define UBF_FUNC_KIND_FILTER    0x02

#define UBF_FUNC_HAS_EHINFO     0x04

#if defined(_TARGET_AMD64_)

//
// The following structures are defined in Windows x64 unwind info specification
// http://www.bing.com/search?q=msdn+Exception+Handling+x64
//

typedef union _UNWIND_CODE {
    struct {
        uint8_t CodeOffset;
        uint8_t UnwindOp : 4;
        uint8_t OpInfo : 4;
    };

    uint16_t FrameOffset;
} UNWIND_CODE, *PUNWIND_CODE;

#define UNW_FLAG_NHANDLER 0x0
#define UNW_FLAG_EHANDLER 0x1
#define UNW_FLAG_UHANDLER 0x2
#define UNW_FLAG_CHAININFO 0x4

typedef struct _UNWIND_INFO {
    uint8_t Version : 3;
    uint8_t Flags : 5;
    uint8_t SizeOfProlog;
    uint8_t CountOfUnwindCodes;
    uint8_t FrameRegister : 4;
    uint8_t FrameOffset : 4;
    UNWIND_CODE UnwindCode[1];
} UNWIND_INFO, *PUNWIND_INFO;

typedef DPTR(struct _UNWIND_INFO)      PTR_UNWIND_INFO;
typedef DPTR(union _UNWIND_CODE)       PTR_UNWIND_CODE;

#endif // _TARGET_AMD64_

static PTR_VOID GetUnwindDataBlob(TADDR moduleBase, PTR_RUNTIME_FUNCTION pRuntimeFunction, /* out */ size_t * pSize)
{
#if defined(_TARGET_AMD64_)
    PTR_UNWIND_INFO pUnwindInfo(dac_cast<PTR_UNWIND_INFO>(moduleBase + pRuntimeFunction->UnwindInfoAddress));

    size_t size = offsetof(UNWIND_INFO, UnwindCode) + sizeof(UNWIND_CODE) * pUnwindInfo->CountOfUnwindCodes;

    // Chained unwind info is not supported at this time
    ASSERT((pUnwindInfo->Flags & UNW_FLAG_CHAININFO) == 0);

    if (pUnwindInfo->Flags & (UNW_FLAG_EHANDLER | UNW_FLAG_UHANDLER))
    {
        // Personality routine
        size = ALIGN_UP(size, sizeof(DWORD)) + sizeof(DWORD);
    }

    *pSize = size;

    return pUnwindInfo;

#elif defined(_TARGET_ARM_)

    // if this function uses packed unwind data then at least one of the two least significant bits
    // will be non-zero.  if this is the case then there will be no xdata record to enumerate.
    ASSERT((pRuntimeFunction->UnwindData & 0x3) == 0);

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

    if ((xdata[0] & (1 << 20)) != 0)
    {
        // Personality routine
        size += 4;
    }

    *pSize = size;
    return xdata;
#else
    #error unexpected target architecture
#endif
}


CoffNativeCodeManager::CoffNativeCodeManager(TADDR moduleBase, 
                                             PTR_RUNTIME_FUNCTION pRuntimeFunctionTable, UInt32 nRuntimeFunctionTable,
                                             PTR_PTR_VOID pClasslibFunctions, UInt32 nClasslibFunctions)
    : m_moduleBase(moduleBase), 
      m_pRuntimeFunctionTable(pRuntimeFunctionTable), m_nRuntimeFunctionTable(nRuntimeFunctionTable),
      m_pClasslibFunctions(pClasslibFunctions), m_nClasslibFunctions(nClasslibFunctions)
{
}

CoffNativeCodeManager::~CoffNativeCodeManager()
{
}

static int LookupUnwindInfoForMethod(UInt32 relativePc,
                                     PTR_RUNTIME_FUNCTION pRuntimeFunctionTable,
                                     int low,
                                     int high)
{
#ifdef TARGET_ARM
    relativePc |= THUMB_CODE;
#endif 

    // Entries are sorted and terminated by sentinel value (DWORD)-1

    // Binary search the RUNTIME_FUNCTION table
    // Use linear search once we get down to a small number of elements
    // to avoid Binary search overhead.
    while (high - low > 10) 
    {
       int middle = low + (high - low) / 2;

       PTR_RUNTIME_FUNCTION pFunctionEntry = pRuntimeFunctionTable + middle;
       if (relativePc < pFunctionEntry->BeginAddress) 
       {
           high = middle - 1;
       } 
       else 
       {
           low = middle;
       }
    }

    for (int i = low; i <= high; ++i)
    {
        // This is safe because of entries are terminated by sentinel value (DWORD)-1
        PTR_RUNTIME_FUNCTION pNextFunctionEntry = pRuntimeFunctionTable + (i + 1);

        if (relativePc < pNextFunctionEntry->BeginAddress)
        {
            PTR_RUNTIME_FUNCTION pFunctionEntry = pRuntimeFunctionTable + i;
            if (relativePc >= pFunctionEntry->BeginAddress)
            {
                return i;
            }
            break;
        }
    }

    return -1;
}

struct CoffNativeMethodInfo
{
    PTR_RUNTIME_FUNCTION mainRuntimeFunction;
    PTR_RUNTIME_FUNCTION runtimeFunction;
    bool executionAborted;
};

// Ensure that CoffNativeMethodInfo fits into the space reserved by MethodInfo
static_assert(sizeof(CoffNativeMethodInfo) <= sizeof(MethodInfo), "CoffNativeMethodInfo too big");

bool CoffNativeCodeManager::FindMethodInfo(PTR_VOID        ControlPC, 
                                           MethodInfo *    pMethodInfoOut)
{
    CoffNativeMethodInfo * pMethodInfo = (CoffNativeMethodInfo *)pMethodInfoOut;

    TADDR relativePC = dac_cast<TADDR>(ControlPC) - m_moduleBase;

    int MethodIndex = LookupUnwindInfoForMethod((UInt32)relativePC, m_pRuntimeFunctionTable,
        0, m_nRuntimeFunctionTable - 1);
    if (MethodIndex < 0)
        return false;

    PTR_RUNTIME_FUNCTION pRuntimeFunction = m_pRuntimeFunctionTable + MethodIndex;

    pMethodInfo->runtimeFunction = pRuntimeFunction;

    // The runtime function could correspond to a funclet.  We need to get to the 
    // runtime function of the main method.
    for (;;)
    {
        size_t unwindDataBlobSize;
        PTR_VOID pUnwindDataBlob = GetUnwindDataBlob(m_moduleBase, pRuntimeFunction, &unwindDataBlobSize);

        uint8_t unwindBlockFlags = *(dac_cast<DPTR(uint8_t)>(pUnwindDataBlob) + unwindDataBlobSize);
        if ((unwindBlockFlags & UBF_FUNC_KIND_MASK) == UBF_FUNC_KIND_ROOT)
            break;

        pRuntimeFunction--;
    }

    pMethodInfo->mainRuntimeFunction = pRuntimeFunction;

    pMethodInfo->executionAborted = false;

    return true;
}

bool CoffNativeCodeManager::IsFunclet(MethodInfo * pMethInfo)
{
    CoffNativeMethodInfo * pMethodInfo = (CoffNativeMethodInfo *)pMethInfo;

    size_t unwindDataBlobSize;
    PTR_VOID pUnwindDataBlob = GetUnwindDataBlob(m_moduleBase, pMethodInfo->runtimeFunction, &unwindDataBlobSize);

    uint8_t unwindBlockFlags = *(dac_cast<DPTR(uint8_t)>(pUnwindDataBlob) + unwindDataBlobSize);

    // A funclet will have an entry in funclet to main method map
    return (unwindBlockFlags & UBF_FUNC_KIND_MASK) != UBF_FUNC_KIND_ROOT;
}

PTR_VOID CoffNativeCodeManager::GetFramePointer(MethodInfo *   pMethInfo,
                                         REGDISPLAY *   pRegisterSet)
{
    CoffNativeMethodInfo * pMethodInfo = (CoffNativeMethodInfo *)pMethInfo;

    size_t unwindDataBlobSize;
    PTR_VOID pUnwindDataBlob = GetUnwindDataBlob(m_moduleBase, pMethodInfo->runtimeFunction, &unwindDataBlobSize);

    uint8_t unwindBlockFlags = *(dac_cast<DPTR(uint8_t)>(pUnwindDataBlob) + unwindDataBlobSize);

    // Return frame pointer for methods with EH and funclets
    if ((unwindBlockFlags & UBF_FUNC_HAS_EHINFO) != 0 || (unwindBlockFlags & UBF_FUNC_KIND_MASK) != UBF_FUNC_KIND_ROOT)
    {
        return (PTR_VOID)pRegisterSet->GetFP();
    }

    return NULL;
}

// void EnumGCRefs(PTR_VOID pGCInfo, UINT32 curOffs, REGDISPLAY * pRD, GCEnumContext * hCallback, bool executionAborted);

void CoffNativeCodeManager::EnumGcRefs(MethodInfo *    pMethodInfo, 
                                       PTR_VOID        safePointAddress,
                                       REGDISPLAY *    pRegisterSet,
                                       GCEnumContext * hCallback)
{
    // @TODO: CORERT: PInvoke transitions

#if 0
    CoffNativeMethodInfo * pNativeMethodInfo = (CoffNativeMethodInfo *)pMethodInfo;

    SIZE_T nUnwindDataSize;
    PTR_VOID pUnwindData = GetUnwindDataBlob(dac_cast<TADDR>(m_pvStartRange), &pNativeMethodInfo->mainRuntimeFunction, &nUnwindDataSize);

    // GCInfo immediatelly follows unwind data
    PTR_VOID pGCInfo = dac_cast<PTR_VOID>(dac_cast<TADDR>(pUnwindData) + nUnwindDataSize + 1);

    ::EnumGCRefs(pGCInfo, codeOffset, pRegisterSet, hCallback, pNativeMethodInfo->executionAborted);
#endif
}

UIntNative CoffNativeCodeManager::GetConservativeUpperBoundForOutgoingArgs(MethodInfo * pMethodInfo, REGDISPLAY * pRegisterSet)
{
    // @TODO: CORERT: GetConservativeUpperBoundForOutgoingArgs
    assert(false);
    return false;
}

bool CoffNativeCodeManager::UnwindStackFrame(MethodInfo *    pMethodInfo,
                                      REGDISPLAY *    pRegisterSet,                 // in/out
                                      PTR_VOID *      ppPreviousTransitionFrame)    // out
{
    CoffNativeMethodInfo * pNativeMethodInfo = (CoffNativeMethodInfo *)pMethodInfo;

    // @TODO: CORERT: PInvoke transitions
    *ppPreviousTransitionFrame = NULL;

    CONTEXT context;
    KNONVOLATILE_CONTEXT_POINTERS contextPointers;

#ifdef _DEBUG
    memset(&context, 0xDD, sizeof(context));
    memset(&contextPointers, 0xDD, sizeof(contextPointers));
#endif

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
                    dac_cast<TADDR>(m_moduleBase),
                    pRegisterSet->IP,
                    (PRUNTIME_FUNCTION)pNativeMethodInfo->runtimeFunction,
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

bool CoffNativeCodeManager::GetReturnAddressHijackInfo(MethodInfo *    pMethodInfo,
                                                REGDISPLAY *    pRegisterSet,       // in
                                                PTR_PTR_VOID *  ppvRetAddrLocation, // out
                                                GCRefKind *     pRetValueKind)      // out
{
    // @TODO: CORERT: GetReturnAddressHijackInfo
    return false;
}

void CoffNativeCodeManager::UnsynchronizedHijackMethodLoops(MethodInfo * pMethodInfo)
{
    // @TODO: CORERT: UnsynchronizedHijackMethodLoops
}

PTR_VOID CoffNativeCodeManager::RemapHardwareFaultToGCSafePoint(MethodInfo * pMethodInfo, PTR_VOID controlPC)
{
    // GCInfo decoder needs to know whether execution of the method is aborted 
    // while querying for gc-info.  But ICodeManager::EnumGCRef() doesn't receive any
    // flags from mrt. Call to this method is used as a cue to mark the method info
    // as execution aborted. Note - if pMethodInfo was cached, this scheme would not work.
    //
    // If the method has EH, then JIT will make sure the method is fully interruptible
    // and we will have GC-info available at the faulting address as well.

    CoffNativeMethodInfo * pNativeMethodInfo = (CoffNativeMethodInfo *)pMethodInfo;
    pNativeMethodInfo->executionAborted = true;

    return controlPC;
}

struct CoffEHEnumState
{
    PTR_UInt8 pMethodStartAddress;
    PTR_UInt8 pEHInfo;
    UInt32 uClause;
    UInt32 nClauses;
};

// Ensure that CoffEHEnumState fits into the space reserved by EHEnumState
static_assert(sizeof(CoffEHEnumState) <= sizeof(EHEnumState), "CoffEHEnumState too big");

bool CoffNativeCodeManager::EHEnumInit(MethodInfo * pMethodInfo, PTR_VOID * pMethodStartAddress, EHEnumState * pEHEnumStateOut)
{
    assert(pMethodInfo != NULL);
    assert(pMethodStartAddress != NULL);
    assert(pEHEnumStateOut != NULL);

    CoffNativeMethodInfo * pNativeMethodInfo = (CoffNativeMethodInfo *)pMethodInfo;
    CoffEHEnumState * pEnumState = (CoffEHEnumState *)pEHEnumStateOut;

    size_t unwindDataBlobSize;
    PTR_VOID pUnwindDataBlob = GetUnwindDataBlob(m_moduleBase, pNativeMethodInfo->mainRuntimeFunction, &unwindDataBlobSize);

    uint8_t unwindBlockFlags = *(dac_cast<DPTR(uint8_t)>(pUnwindDataBlob) + unwindDataBlobSize);

    // return if there is no EH info associated with this method
    if ((unwindBlockFlags & UBF_FUNC_HAS_EHINFO) == 0)
    {
        return false;
    }

    *pMethodStartAddress = dac_cast<PTR_VOID>(m_moduleBase + pNativeMethodInfo->mainRuntimeFunction->BeginAddress);
    pEnumState->pMethodStartAddress = dac_cast<PTR_UInt8>(*pMethodStartAddress);
    pEnumState->pEHInfo = dac_cast<PTR_UInt8>(pUnwindDataBlob) + unwindDataBlobSize + 1;
    pEnumState->uClause = 0;
    pEnumState->nClauses = VarInt::ReadUnsigned(pEnumState->pEHInfo);

    return true;
}

bool CoffNativeCodeManager::EHEnumNext(EHEnumState * pEHEnumState, EHClause * pEHClauseOut)
{
    assert(pEHEnumState != NULL);
    assert(pEHClauseOut != NULL);

    CoffEHEnumState * pEnumState = (CoffEHEnumState *)pEHEnumState;
    if (pEnumState->uClause >= pEnumState->nClauses)
        return false;
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
            UInt32 typeRVA = *((PTR_UInt32&)pEnumState->pEHInfo)++;
            pEHClauseOut->m_pTargetType = dac_cast<PTR_VOID>(m_moduleBase + typeRVA);
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
        UNREACHABLE_MSG("unexpected EHClauseKind");
    }

    return true;
}

void * CoffNativeCodeManager::GetClasslibFunction(ClasslibFunctionId functionId)
{
    uint32_t id = (uint32_t)functionId;

    if (id >= m_nClasslibFunctions)
        return nullptr;

    return m_pClasslibFunctions[id];
}

extern "C" bool __stdcall RegisterCodeManager(ICodeManager * pCodeManager, PTR_VOID pvStartRange, UInt32 cbRange);

extern "C"
bool RhpRegisterCoffModule(void * pModule, 
                           void * pvStartRange, UInt32 cbRange,
                           void ** pClasslibFunctions, UInt32 nClasslibFunctions)
{
    PIMAGE_DOS_HEADER pDosHeader = (PIMAGE_DOS_HEADER)pModule;
    PIMAGE_NT_HEADERS pNTHeaders = (PIMAGE_NT_HEADERS)((TADDR)pModule + pDosHeader->e_lfanew);

    IMAGE_DATA_DIRECTORY * pRuntimeFunctions = &(pNTHeaders->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_EXCEPTION]);

    CoffNativeCodeManager * pCoffNativeCodeManager = new (nothrow) CoffNativeCodeManager((TADDR)pModule,
        dac_cast<PTR_RUNTIME_FUNCTION>((TADDR)pModule + pRuntimeFunctions->VirtualAddress),
        pRuntimeFunctions->Size / sizeof(RUNTIME_FUNCTION),
        pClasslibFunctions, nClasslibFunctions);
    if (pCoffNativeCodeManager == nullptr)
    {
        return false;
    }

    if (!RegisterCodeManager(pCoffNativeCodeManager, pvStartRange, cbRange))
    {
        delete pCoffNativeCodeManager;
        return false;
    }

    return true;
}
