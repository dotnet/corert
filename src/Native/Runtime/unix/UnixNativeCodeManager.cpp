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
#include "holder.h"

#include "CommonMacros.inl"

#define GCINFODECODER_NO_EE
#include "coreclr/gcinfodecoder.cpp"

#include "UnixContext.h"

#define UBF_FUNC_KIND_MASK      0x03
#define UBF_FUNC_KIND_ROOT      0x00
#define UBF_FUNC_KIND_HANDLER   0x01
#define UBF_FUNC_KIND_FILTER    0x02

#define UBF_FUNC_HAS_EHINFO             0x04
#define UBF_FUNC_REVERSE_PINVOKE        0x08
#define UBF_FUNC_HAS_ASSOCIATED_DATA    0x10

struct UnixNativeMethodInfo
{
    PTR_VOID pMethodStartAddress;
    PTR_UInt8 pMainLSDA;
    PTR_UInt8 pLSDA;
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

    PTR_UInt8 p = dac_cast<PTR_UInt8>(lsda);

    pMethodInfo->pLSDA = p;

    uint8_t unwindBlockFlags = *p++;

    if ((unwindBlockFlags & UBF_FUNC_KIND_MASK) != UBF_FUNC_KIND_ROOT)
    {
        // Funclets just refer to the main function's blob
        pMethodInfo->pMainLSDA = p + *dac_cast<PTR_Int32>(p);
        p += sizeof(int32_t);

        pMethodInfo->pMethodStartAddress = dac_cast<PTR_VOID>(startAddress - *dac_cast<PTR_Int32>(p));
    }
    else
    {
        pMethodInfo->pMainLSDA = dac_cast<PTR_UInt8>(lsda);
        pMethodInfo->pMethodStartAddress = dac_cast<PTR_VOID>(startAddress);
    }

    pMethodInfo->executionAborted = false;

    return true;
}

bool UnixNativeCodeManager::IsFunclet(MethodInfo * pMethodInfo)
{
    UnixNativeMethodInfo * pNativeMethodInfo = (UnixNativeMethodInfo *)pMethodInfo;

    uint8_t unwindBlockFlags = *(pNativeMethodInfo->pLSDA);
    return (unwindBlockFlags & UBF_FUNC_KIND_MASK) != UBF_FUNC_KIND_ROOT;
}

bool UnixNativeCodeManager::IsFilter(MethodInfo * pMethodInfo)
{
    UnixNativeMethodInfo * pNativeMethodInfo = (UnixNativeMethodInfo *)pMethodInfo;

    uint8_t unwindBlockFlags = *(pNativeMethodInfo->pLSDA);
    return (unwindBlockFlags & UBF_FUNC_KIND_MASK) == UBF_FUNC_KIND_FILTER;
}

PTR_VOID UnixNativeCodeManager::GetFramePointer(MethodInfo *   pMethodInfo,
                                                REGDISPLAY *   pRegisterSet)
{
    UnixNativeMethodInfo * pNativeMethodInfo = (UnixNativeMethodInfo *)pMethodInfo;

    // Return frame pointer for methods with EH and funclets
    uint8_t unwindBlockFlags = *(pNativeMethodInfo->pLSDA);
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
    UnixNativeMethodInfo * pNativeMethodInfo = (UnixNativeMethodInfo *)pMethodInfo;

    PTR_UInt8 p = pNativeMethodInfo->pMainLSDA;

    uint8_t unwindBlockFlags = *p++;

    if ((unwindBlockFlags & UBF_FUNC_HAS_ASSOCIATED_DATA) != 0)
        p += sizeof(int32_t);

    if ((unwindBlockFlags & UBF_FUNC_HAS_EHINFO) != 0)
        p += sizeof(int32_t);

    UInt32 codeOffset = (UInt32)(dac_cast<TADDR>(safePointAddress) - dac_cast<TADDR>(pNativeMethodInfo->pMethodStartAddress));

    GcInfoDecoder decoder(
        GCInfoToken(p),
        GcInfoDecoderFlags(DECODE_GC_LIFETIMES | DECODE_SECURITY_OBJECT | DECODE_VARARG),
        codeOffset - 1 // TODO: Is this adjustment correct?
    );

    ICodeManagerFlags flags = (ICodeManagerFlags)0;
    if (pNativeMethodInfo->executionAborted)
        flags = ICodeManagerFlags::ExecutionAborted;
    if (IsFilter(pMethodInfo))
        flags = (ICodeManagerFlags)(flags | ICodeManagerFlags::NoReportUntracked);

    if (!decoder.EnumerateLiveSlots(
        pRegisterSet,
        false /* reportScratchSlots */,
        flags,
        hCallback->pCallback,
        hCallback
        ))
    {
        assert(false);
    }
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
    UnixNativeMethodInfo * pNativeMethodInfo = (UnixNativeMethodInfo *)pMethodInfo;

    PTR_UInt8 p = pNativeMethodInfo->pMainLSDA;

    uint8_t unwindBlockFlags = *p++;

    if ((unwindBlockFlags & UBF_FUNC_HAS_ASSOCIATED_DATA) != 0)
        p += sizeof(int32_t);

    if ((unwindBlockFlags & UBF_FUNC_REVERSE_PINVOKE) != 0)
    {
        // Reverse PInvoke transition should on the main function body only
        assert(pNativeMethodInfo->pMainLSDA == pNativeMethodInfo->pLSDA);

        if ((unwindBlockFlags & UBF_FUNC_HAS_EHINFO) != 0)
            p += sizeof(int32_t);

        GcInfoDecoder decoder(GCInfoToken(p), DECODE_REVERSE_PINVOKE_VAR);
        INT32 slot = decoder.GetReversePInvokeFrameStackSlot();
        assert(slot != NO_REVERSE_PINVOKE_FRAME);

        TADDR basePointer = NULL;
        UINT32 stackBasedRegister = decoder.GetStackBaseRegister();
        if (stackBasedRegister == NO_STACK_BASE_REGISTER)
        {
            basePointer = dac_cast<TADDR>(pRegisterSet->GetSP());
        }
        else
        {
            basePointer = dac_cast<TADDR>(pRegisterSet->GetFP());
        }
        *ppPreviousTransitionFrame = *(void**)(basePointer + slot);
        return true;
    }

    *ppPreviousTransitionFrame = NULL;

    if (!VirtualUnwind(pRegisterSet))
    {
        return false;
    }

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

    PTR_UInt8 p = pNativeMethodInfo->pMainLSDA;

    uint8_t unwindBlockFlags = *p++;

    if ((unwindBlockFlags & UBF_FUNC_HAS_ASSOCIATED_DATA) != 0)
        p += sizeof(int32_t);

    // return if there is no EH info associated with this method
    if ((unwindBlockFlags & UBF_FUNC_HAS_EHINFO) == 0)
    {
        return false;
    }
   
    UnixEHEnumState * pEnumState = (UnixEHEnumState *)pEHEnumStateOut;

    *pMethodStartAddress = pNativeMethodInfo->pMethodStartAddress;

    pEnumState->pMethodStartAddress = dac_cast<PTR_UInt8>(pNativeMethodInfo->pMethodStartAddress);
    pEnumState->pEHInfo = dac_cast<PTR_UInt8>(p + *dac_cast<PTR_Int32>(p));
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

PTR_VOID UnixNativeCodeManager::GetMethodStartAddress(MethodInfo * pMethodInfo)
{
    UnixNativeMethodInfo * pNativeMethodInfo = (UnixNativeMethodInfo *)pMethodInfo;
    return pNativeMethodInfo->pMethodStartAddress;
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

PTR_VOID UnixNativeCodeManager::GetAssociatedData(PTR_VOID ControlPC)
{
    UnixNativeMethodInfo methodInfo;
    if (!FindMethodInfo(ControlPC, (MethodInfo*)&methodInfo))
        return NULL;

    PTR_UInt8 p = methodInfo.pMainLSDA;

    uint8_t unwindBlockFlags = *p++;
    if ((unwindBlockFlags & UBF_FUNC_HAS_ASSOCIATED_DATA) == 0)
        return NULL;

    return dac_cast<PTR_VOID>(p + *dac_cast<PTR_Int32>(p));
}

extern "C" bool __stdcall RegisterCodeManager(ICodeManager * pCodeManager, PTR_VOID pvStartRange, UInt32 cbRange);
extern "C" void __stdcall UnregisterCodeManager(ICodeManager * pCodeManager);
extern "C" bool __stdcall RegisterUnboxingStubs(PTR_VOID pvStartRange, UInt32 cbRange);

extern "C"
bool RhRegisterOSModule(void * pModule,
                        void * pvManagedCodeStartRange, UInt32 cbManagedCodeRange,
                        void * pvUnboxingStubsStartRange, UInt32 cbUnboxingStubsRange,
                        void ** pClasslibFunctions, UInt32 nClasslibFunctions)
{
    NewHolder<UnixNativeCodeManager> pUnixNativeCodeManager = new (nothrow) UnixNativeCodeManager((TADDR)pModule,
        pClasslibFunctions, nClasslibFunctions);

    if (pUnixNativeCodeManager == nullptr)
        return false;

    if (!RegisterCodeManager(pUnixNativeCodeManager, pvManagedCodeStartRange, cbManagedCodeRange))
        return false;

    if (!RegisterUnboxingStubs(pvUnboxingStubsStartRange, cbUnboxingStubsRange))
    {
        UnregisterCodeManager(pUnixNativeCodeManager);
        return false;
    }

    pUnixNativeCodeManager.SuppressRelease();

    return true;
}
