// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#include "common.h"

#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "PalRedhawkCommon.h"
#include "regdisplay.h"
#include "ICodeManager.h"
#include "UnixNativeCodeManager.h"
#include "varint.h"

#include "CommonMacros.inl"

#include "UnixContext.h"

#define UBF_FUNC_KIND_MASK      0x03
#define UBF_FUNC_KIND_ROOT      0x00
#define UBF_FUNC_KIND_HANDLER   0x01
#define UBF_FUNC_KIND_FILTER    0x02

#define UBF_FUNC_HAS_EHINFO     0x04

struct UnixNativeMethodInfo
{
    PTR_VOID pMethodStartAddress;
    PTR_UInt8 pEhInfo;
    uint8_t funcFlags;
    bool executionAborted;
};

// Ensure that UnixNativeMethodInfo fits into the space reserved by MethodInfo
static_assert(sizeof(UnixNativeMethodInfo) <= sizeof(MethodInfo), "UnixNativeMethodInfo too big");

UnixNativeCodeManager::UnixNativeCodeManager(TADDR moduleBase, 
                                             PTR_PTR_VOID pClasslibFunctions, UInt32 nClasslibFunctions)
    : m_moduleBase(moduleBase), 
      m_pClasslibFunctions(pClasslibFunctions), m_nClasslibFunctions(nClasslibFunctions)
{
}

UnixNativeCodeManager::~UnixNativeCodeManager()
{
}

bool UnixNativeCodeManager::FindMethodInfo(PTR_VOID        ControlPC, 
                                           MethodInfo *    pMethodInfoOut)
{
    UnixNativeMethodInfo * pMethodInfo = (UnixNativeMethodInfo *)pMethodInfoOut;
    UIntNative startAddress;
    UIntNative lsda;

    if (!FindProcInfo((UIntNative)ControlPC, &startAddress, &lsda))
    {
        return false;
    }

    PTR_UInt8 lsdaPtr = dac_cast<PTR_UInt8>(lsda);

    int offsetFromMainFunction = *dac_cast<PTR_Int32>(lsdaPtr);
    lsdaPtr += sizeof(int);
    pMethodInfo->funcFlags = *lsdaPtr;
    ++lsdaPtr;

    pMethodInfo->pMethodStartAddress = (PTR_VOID)(startAddress - offsetFromMainFunction);
    pMethodInfo->pEhInfo = (pMethodInfo->funcFlags & UBF_FUNC_HAS_EHINFO) ? lsdaPtr : NULL;
    pMethodInfo->executionAborted = false;

    return true;
}

bool UnixNativeCodeManager::IsFunclet(MethodInfo * pMethodInfo)
{
    UnixNativeMethodInfo * pNativeMethodInfo = (UnixNativeMethodInfo *)pMethodInfo;

    return (pNativeMethodInfo->funcFlags & UBF_FUNC_KIND_MASK) != UBF_FUNC_KIND_ROOT;
}

PTR_VOID UnixNativeCodeManager::GetFramePointer(MethodInfo *   pMethodInfo,
                                                REGDISPLAY *   pRegisterSet)
{
    UnixNativeMethodInfo * pNativeMethodInfo = (UnixNativeMethodInfo *)pMethodInfo;

    // Return frame pointer for methods with EH and funclets
    uint8_t unwindBlockFlags = pNativeMethodInfo->funcFlags;
    if ((unwindBlockFlags & UBF_FUNC_HAS_EHINFO) != 0 || (unwindBlockFlags & UBF_FUNC_KIND_MASK) != UBF_FUNC_KIND_ROOT)
    {
        return (PTR_VOID)pRegisterSet->GetFP();
    }

    return NULL;
}

void UnixNativeCodeManager::EnumGcRefs(MethodInfo *    pMethodInfo, 
                                       PTR_VOID        safePointAddress,
                                       REGDISPLAY *    pRegisterSet,
                                       GCEnumContext * hCallback)
{
    // @TODO: CORERT: EnumGcRefs
}

UIntNative UnixNativeCodeManager::GetConservativeUpperBoundForOutgoingArgs(MethodInfo * pMethodInfo, REGDISPLAY * pRegisterSet)
{
    // @TODO: CORERT: GetConservativeUpperBoundForOutgoingArgs
    assert(false);
    return false;
}

bool UnixNativeCodeManager::UnwindStackFrame(MethodInfo *    pMethodInfo,
                                             REGDISPLAY *    pRegisterSet,                 // in/out
                                             PTR_VOID *      ppPreviousTransitionFrame)    // out
{
    if (!VirtualUnwind(pRegisterSet))
    {
        return false;
    }

    // @TODO: CORERT: PInvoke transitions
    *ppPreviousTransitionFrame = NULL;

    return true;
}

bool UnixNativeCodeManager::GetReturnAddressHijackInfo(MethodInfo *    pMethodInfo,
                                                       REGDISPLAY *    pRegisterSet,       // in
                                                       PTR_PTR_VOID *  ppvRetAddrLocation, // out
                                                       GCRefKind *     pRetValueKind)      // out
{
    // @TODO: CORERT: GetReturnAddressHijackInfo
    return false;
}

void UnixNativeCodeManager::UnsynchronizedHijackMethodLoops(MethodInfo * pMethodInfo)
{
    // @TODO: CORERT: UnsynchronizedHijackMethodLoops
}

PTR_VOID UnixNativeCodeManager::RemapHardwareFaultToGCSafePoint(MethodInfo * pMethodInfo, PTR_VOID controlPC)
{
    // GCInfo decoder needs to know whether execution of the method is aborted 
    // while querying for gc-info.  But ICodeManager::EnumGCRef() doesn't receive any
    // flags from mrt. Call to this method is used as a cue to mark the method info
    // as execution aborted. Note - if pMethodInfo was cached, this scheme would not work.
    //
    // If the method has EH, then JIT will make sure the method is fully interruptible
    // and we will have GC-info available at the faulting address as well.

    UnixNativeMethodInfo * pNativeMethodInfo = (UnixNativeMethodInfo *)pMethodInfo;
    pNativeMethodInfo->executionAborted = true;

    return controlPC;
}

struct UnixEHEnumState
{
    PTR_UInt8 pMethodStartAddress;
    PTR_UInt8 pEHInfo;
    UInt32 uClause;
    UInt32 nClauses;
};

// Ensure that UnixEHEnumState fits into the space reserved by EHEnumState
static_assert(sizeof(UnixEHEnumState) <= sizeof(EHEnumState), "UnixEHEnumState too big");

bool UnixNativeCodeManager::EHEnumInit(MethodInfo * pMethodInfo, PTR_VOID * pMethodStartAddress, EHEnumState * pEHEnumStateOut)
{
    assert(pMethodInfo != NULL);
    assert(pMethodStartAddress != NULL);
    assert(pEHEnumStateOut != NULL);

    UnixNativeMethodInfo * pNativeMethodInfo = (UnixNativeMethodInfo *)pMethodInfo;

    // return if there is no EH info associated with this method
    if ((pNativeMethodInfo->funcFlags & UBF_FUNC_HAS_EHINFO) == 0)
    {
        return false;
    }
   
    UnixEHEnumState * pEnumState = (UnixEHEnumState *)pEHEnumStateOut;

    *pMethodStartAddress = pNativeMethodInfo->pMethodStartAddress;

    pEnumState->pMethodStartAddress = dac_cast<PTR_UInt8>(pNativeMethodInfo->pMethodStartAddress);
    pEnumState->pEHInfo = pNativeMethodInfo->pEhInfo;
    pEnumState->uClause = 0;
    pEnumState->nClauses = VarInt::ReadUnsigned(pEnumState->pEHInfo);

    return true;
}

bool UnixNativeCodeManager::EHEnumNext(EHEnumState * pEHEnumState, EHClause * pEHClauseOut)
{
    assert(pEHEnumState != NULL);
    assert(pEHClauseOut != NULL);

    UnixEHEnumState * pEnumState = (UnixEHEnumState *)pEHEnumState;
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
            pEHClauseOut->m_pTargetType = dac_cast<PTR_VOID>(pEnumState->pEHInfo + typeRelAddr);
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

void * UnixNativeCodeManager::GetClasslibFunction(ClasslibFunctionId functionId)
{
    uint32_t id = (uint32_t)functionId;

    if (id >= m_nClasslibFunctions)
    {
        return nullptr;
    }

    return m_pClasslibFunctions[id];
}

extern "C" bool __stdcall RegisterCodeManager(ICodeManager * pCodeManager, PTR_VOID pvStartRange, UInt32 cbRange);

extern "C"
bool RhpRegisterUnixModule(void * pModule, 
                           void * pvStartRange, UInt32 cbRange,
                           void ** pClasslibFunctions, UInt32 nClasslibFunctions)
{
    UnixNativeCodeManager * pUnixNativeCodeManager = new (nothrow) UnixNativeCodeManager((TADDR)pModule,
        pClasslibFunctions, nClasslibFunctions);
    if (pUnixNativeCodeManager == nullptr)
    {
        return false;
    }

    if (!RegisterCodeManager(pUnixNativeCodeManager, pvStartRange, cbRange))
    {
        delete pUnixNativeCodeManager;
        return false;
    }

    return true;
}
