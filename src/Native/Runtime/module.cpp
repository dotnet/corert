// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"
#include "rhassert.h"
#include "slist.h"
#include "holder.h"
#include "gcrhinterface.h"
#include "shash.h"
#include "RWLock.h"
#include "module.h"
#include "varint.h"
#include "rhbinder.h"
#include "Crst.h"
#include "regdisplay.h"
#include "StackFrameIterator.h"
#include "thread.h"
#include "event.h"
#include "RuntimeInstance.h"
#include "eetype.h"
#include "ObjectLayout.h"
#include "threadstore.h"

#include "CommonMacros.inl"
#include "slist.inl"
#include "shash.inl"

#include "gcinfo.h"
#include "RHCodeMan.h"

#include "rheventtrace.h"

// #define LOG_MODULE_LOAD_VERIFICATION

#ifndef DACCESS_COMPILE

EXTERN_C UInt32_BOOL g_fGcStressStarted;

Module::Module(ModuleHeader *pModuleHeader) : 
    m_pNext(),
    m_pbDeltaShortcutTable(NULL),
    m_pModuleHeader(pModuleHeader),
    m_MethodList(),
    m_fFinalizerInitComplete(false)
{
}

Module * Module::Create(ModuleHeader *pModuleHeader)
{
    // There's only one module header version for now. If we ever need to change it in a breaking fashion this
    // is where we could put some code to try and handle downlevel modules with some form of compatibility
    // mode (or just fail the module creation).
    ASSERT(pModuleHeader->Version == ModuleHeader::CURRENT_VERSION);

    NewHolder<Module> pNewModule = new (nothrow) Module(pModuleHeader);
    if (NULL == pNewModule)
        return NULL;

    if (!pNewModule->m_MethodList.Init(pModuleHeader))
        return NULL;

    pNewModule->m_pEHTypeTable = pModuleHeader->GetEHInfo();
    pNewModule->m_pbDeltaShortcutTable = pNewModule->m_MethodList.GetDeltaShortcutTablePtr();
    pNewModule->m_pStaticsGCInfo = dac_cast<PTR_StaticGcDesc>(pModuleHeader->GetStaticsGCInfo());
    pNewModule->m_pStaticsGCDataSection = pModuleHeader->GetStaticsGCDataSection();
    pNewModule->m_pThreadStaticsGCInfo = dac_cast<PTR_StaticGcDesc>(pModuleHeader->GetThreadStaticsGCInfo());

    if (pModuleHeader->RraFrozenObjects != ModuleHeader::NULL_RRA)
    {
        ASSERT(pModuleHeader->SizeFrozenObjects != 0);
        pNewModule->m_FrozenSegment = RedhawkGCInterface::RegisterFrozenSection(
                                        pModuleHeader->GetFrozenObjects(), pModuleHeader->SizeFrozenObjects);
        if (pNewModule->m_FrozenSegment == NULL)
            return NULL;
    }

    // Determine OS module handle. This assumes that only one Redhawk module can exist in a given PE image,
    // which is true for now. It's also exposed by a number of exports (RhGetModuleFromEEType etc.) so if 
    // we ever rethink this then the public contract needs to change as well.
    pNewModule->m_hOsModuleHandle = PalGetModuleHandleFromPointer(pModuleHeader);
    if (!pNewModule->m_hOsModuleHandle)
    {
        ASSERT_UNCONDITIONALLY("Failed to locate our own module handle");
        return NULL;
    }

#ifdef FEATURE_CUSTOM_IMPORTS
    Module::DoCustomImports(pModuleHeader);
#endif // FEATURE_CUSTOM_IMPORTS

    // do generic unification
    if (pModuleHeader->CountOfGenericUnificationDescs > 0)
    {
        if (!GetRuntimeInstance()->UnifyGenerics((GenericUnificationDesc *)pModuleHeader->GetGenericUnificationDescs(),
                                                 pModuleHeader->CountOfGenericUnificationDescs,
                                                 (void **)pModuleHeader->GetGenericUnificationIndirCells(),
                                                 pModuleHeader->CountOfGenericUnificationIndirCells))
        {
            return NULL;
        }
    }

#ifdef _DEBUG
#ifdef LOG_MODULE_LOAD_VERIFICATION
    printf("\nModule: 0x%p\n", pNewModule->m_hOsModuleHandle);
#endif // LOG_MODULE_LOAD_VERIFICATION
    //
    // Run through every byte of every method in the module and do some sanity-checking. Exclude stub code.
    //
    UInt32 textLength = pModuleHeader->RegionSize[ModuleHeader::TEXT_REGION] - pModuleHeader->SizeStubCode;
    UInt8 * pbText = pModuleHeader->RegionPtr[ModuleHeader::TEXT_REGION];

    UInt32 uMethodSize = 0;
    UInt32 uMethodIndex = 0;
    UInt32 uMethodStartSectionOffset = 0;
    UInt32 uExpectedMethodIndex = 0;
    UInt32 uExpectedMethodStartSectionOffset = 0;
    UInt32 uTextSectionOffset = 0;
    UInt32 nMethods = pNewModule->m_MethodList.GetNumMethodsDEBUG();

    UInt32 nIndirCells = pNewModule->m_pModuleHeader->CountOfLoopIndirCells;
    UIntNative * pShadowBuffer = new (nothrow) UIntNative[nIndirCells];
    UIntNative * pIndirCells = (UIntNative *)pNewModule->m_pModuleHeader->GetLoopIndirCells();
    memcpy(pShadowBuffer, pIndirCells, nIndirCells * sizeof(UIntNative));

    EEMethodInfo methodInfo;

    for (; uTextSectionOffset < textLength; uTextSectionOffset += uMethodSize)
    {
        pNewModule->m_MethodList.GetMethodInfo(
                                uTextSectionOffset, &uMethodIndex, &uMethodStartSectionOffset, &uMethodSize);
    

#ifdef LOG_MODULE_LOAD_VERIFICATION
        printf("0x%08x: %3d 0x%08x 0x%08x\n", 
            uTextSectionOffset, uMethodIndex, uMethodStartSectionOffset, uMethodSize);
#endif // LOG_MODULE_LOAD_VERIFICATION

        ASSERT(uExpectedMethodStartSectionOffset == uMethodStartSectionOffset);
        uExpectedMethodStartSectionOffset += uMethodSize;

        ASSERT(uExpectedMethodIndex == uMethodIndex);
        uExpectedMethodIndex++;

        //
        // verify that every offset in the method gives the same result
        // *every* offsets turns out to be too slow - try 10 offsets in the method
        //
        UInt32 step = max(uMethodSize/10, 1);
        for (UInt32 i = 0; i < uMethodSize; i += step)
        {
            UInt32 uMI;
            UInt32 uMSSO;
            UInt32 uMS;

            pNewModule->m_MethodList.GetMethodInfo(uTextSectionOffset + i, &uMI, &uMSSO, &uMS);

            ASSERT(uMI == uMethodIndex);
            ASSERT(uMSSO == uMethodStartSectionOffset);
            ASSERT(uMS == uMethodSize);
        }

        //
        // calculate the method info
        //

        UInt8 * pbMethod = pbText + uMethodStartSectionOffset;
        UInt8 * pbGCInfo = pNewModule->m_MethodList.GetGCInfo(uMethodIndex);
        void *  pvEHInfo = pNewModule->m_MethodList.GetEHInfo(uMethodIndex);

        methodInfo.Init(pbMethod, uMethodSize, pbGCInfo, pvEHInfo);

        methodInfo.DecodeGCInfoHeader(0, pNewModule->GetUnwindInfoBlob());

        //
        // do some verifications..
        //
#ifdef LOG_MODULE_LOAD_VERIFICATION
        EECodeManager::DumpGCInfo(&methodInfo,
            pNewModule->GetDeltaShortcutTable(), 
            pNewModule->GetUnwindInfoBlob(), 
            pNewModule->GetCallsiteStringBlob());
#endif // LOG_MODULE_LOAD_VERIFICATION

        EECodeManager::VerifyProlog(&methodInfo);
        EECodeManager::VerifyEpilog(&methodInfo);

        pNewModule->UnsynchronizedHijackMethodLoops((MethodInfo *)&methodInfo);

        if (uExpectedMethodIndex >= nMethods)
            break;
    }

    for (UInt32 i = 0; i < nIndirCells; i++)
    {
        ASSERT(pShadowBuffer[i] != pIndirCells[i]); // make sure we hijacked all of them
    }

    pNewModule->UnsynchronizedResetHijackedLoops();

    if (!g_fGcStressStarted) // UnsynchronizedResetHijackedLoops won't do anything under gcstress
    {
        for (UInt32 i = 0; i < nIndirCells; i++)
        {
            ASSERT(pShadowBuffer[i] == pIndirCells[i]); // make sure we reset them properly
        }
    }

    delete[] pShadowBuffer;

    if (g_fGcStressStarted)
        pNewModule->UnsynchronizedHijackAllLoops();

#ifdef LOG_MODULE_LOAD_VERIFICATION
    printf("0x%08x: --- 0x%08x \n", (uTextSectionOffset + uMethodSize), 
                                         (uMethodStartSectionOffset + uMethodSize));
#endif // LOG_MODULE_LOAD_VERIFICATION
#endif // _DEBUG

#ifdef FEATURE_ETW
    ETW::LoaderLog::SendModuleEvent(pNewModule);
#endif // FEATURE_ETW

    // Run any initialization functions for native code that was linked into the image using the binder's
    // /nativelink option.
    if (pNewModule->m_pModuleHeader->RraNativeInitFunctions != ModuleHeader::NULL_RRA)
    {
        typedef void (* NativeInitFunctionPtr)();
        UInt32 cInitFunctions = pNewModule->m_pModuleHeader->CountNativeInitFunctions;
        NativeInitFunctionPtr * pInitFunctions = (NativeInitFunctionPtr*)(pNewModule->m_pModuleHeader->RegionPtr[ModuleHeader::RDATA_REGION] +
                                                                          pNewModule->m_pModuleHeader->RraNativeInitFunctions);
        for (UInt32 i = 0; i < cInitFunctions; i++)
            pInitFunctions[i]();
    }

    pNewModule.SuppressRelease();
    return pNewModule;
}

void Module::Destroy()
{
    delete this;
}

Module::~Module()
{
}

#endif // !DACCESS_COMPILE


PTR_ModuleHeader Module::GetModuleHeader()
{
    return m_pModuleHeader;
}


// We have three separate range checks for the data regions we might be interested in. We do this rather than
// have a single, all-in-one, method to force callers to consider which ranges are applicable. In many cases
// the caller knows an address can only legally lie in one specific range and we'd rather force them to
// specify that than pay for redundant range checks in many cases.
bool Module::ContainsCodeAddress(PTR_VOID pvAddr)
{
    // We explicitly omit the stub code from this check. Use ContainsStubAddress to determine if
    // an address belongs to the stub portion of the module's TEXT_REGION.
    TADDR pAddr = dac_cast<TADDR>(pvAddr);
    TADDR pSectionStart = dac_cast<TADDR>(m_pModuleHeader->RegionPtr[ModuleHeader::TEXT_REGION]);
    TADDR pSectionLimit = pSectionStart + m_pModuleHeader->RegionSize[ModuleHeader::TEXT_REGION]
                                        - m_pModuleHeader->SizeStubCode;
    return (pAddr >= pSectionStart) && (pAddr < pSectionLimit);
}

bool Module::ContainsDataAddress(PTR_VOID pvAddr)
{
    TADDR pAddr = dac_cast<TADDR>(pvAddr);
    TADDR pSectionStart = dac_cast<TADDR>(m_pModuleHeader->RegionPtr[ModuleHeader::DATA_REGION]);
    TADDR pSectionLimit = pSectionStart + m_pModuleHeader->RegionSize[ModuleHeader::DATA_REGION];
    return (pAddr >= pSectionStart) && (pAddr < pSectionLimit);
}

bool Module::ContainsReadOnlyDataAddress(PTR_VOID pvAddr)
{
    TADDR pAddr = dac_cast<TADDR>(pvAddr);
    TADDR pSectionStart = dac_cast<TADDR>(m_pModuleHeader->RegionPtr[ModuleHeader::RDATA_REGION]);
    TADDR pSectionLimit = pSectionStart + m_pModuleHeader->RegionSize[ModuleHeader::RDATA_REGION];
    return (pAddr >= pSectionStart) && (pAddr < pSectionLimit);
}

bool Module::ContainsStubAddress(PTR_VOID pvAddr)
{
    // Determines if the address belongs to the stub portion of the TEXT_REGION section.
    TADDR pAddr = dac_cast<TADDR>(pvAddr);
    TADDR pSectionStart = dac_cast<TADDR>(m_pModuleHeader->RegionPtr[ModuleHeader::TEXT_REGION])
                        + m_pModuleHeader->RegionSize[ModuleHeader::TEXT_REGION]
                        - m_pModuleHeader->SizeStubCode;
    TADDR pSectionLimit = pSectionStart + m_pModuleHeader->SizeStubCode;
    return (pAddr >= pSectionStart) && (pAddr < pSectionLimit);
}

PTR_UInt8 Module::FindMethodStartAddress(PTR_VOID ControlPC)
{
    if (!ContainsCodeAddress(ControlPC))
        return NULL;

    PTR_UInt8 pbControlPC = dac_cast<PTR_UInt8>(ControlPC);

    UInt32 uMethodSize;
    UInt32 uMethodIndex;
    UInt32 uMethodStartSectionOffset;

    PTR_UInt8 pbTextSectionStart = m_pModuleHeader->RegionPtr[ModuleHeader::TEXT_REGION];
    UInt32 uTextSectionOffset = (UInt32)(pbControlPC - pbTextSectionStart);
    m_MethodList.GetMethodInfo(uTextSectionOffset, &uMethodIndex, &uMethodStartSectionOffset, &uMethodSize);

    PTR_UInt8 methodStartAddr = pbTextSectionStart + uMethodStartSectionOffset;
    return methodStartAddr;
}

bool Module::FindMethodInfo(PTR_VOID        ControlPC, 
                            MethodInfo *    pMethodInfoOut)
{
    if (!ContainsCodeAddress(ControlPC))
        return false;

    PTR_UInt8 pbControlPC = dac_cast<PTR_UInt8>(ControlPC);

    UInt32 uMethodSize;
    UInt32 uMethodIndex;
    UInt32 uMethodStartSectionOffset;

    PTR_UInt8 pbTextSectionStart = m_pModuleHeader->RegionPtr[ModuleHeader::TEXT_REGION];
    UInt32 uTextSectionOffset = (UInt32)(pbControlPC - pbTextSectionStart);
    m_MethodList.GetMethodInfo(uTextSectionOffset, &uMethodIndex, &uMethodStartSectionOffset, &uMethodSize);

    PTR_UInt8 pbGCInfo = (PTR_UInt8) m_MethodList.GetGCInfo(uMethodIndex);
    PTR_VOID  pvEHInfo = m_MethodList.GetEHInfo(uMethodIndex);

    EEMethodInfo * pEEMethodInfo = (EEMethodInfo *)pMethodInfoOut;

    pEEMethodInfo->Init(pbTextSectionStart + uMethodStartSectionOffset, uMethodSize, pbGCInfo, pvEHInfo);

    UInt32 codeOffset = (UInt32)(pbControlPC - (PTR_UInt8)pEEMethodInfo->GetCode());
#ifdef _ARM_
    codeOffset &= ~1;
#endif

    pEEMethodInfo->DecodeGCInfoHeader(codeOffset, GetUnwindInfoBlob());

    return true;
}

PTR_UInt8 Module::GetUnwindInfoBlob()
{
    return m_pModuleHeader->GetUnwindInfoBlob();
}

PTR_UInt8 Module::GetCallsiteStringBlob()
{
    return m_pModuleHeader->GetCallsiteInfoBlob();
}

PTR_UInt8 Module::GetDeltaShortcutTable()
{
    return m_pbDeltaShortcutTable;
}

void Module::EnumStaticGCRefsBlock(void * pfnCallback, void * pvCallbackData, PTR_StaticGcDesc pStaticGcInfo, PTR_UInt8 pbStaticData)
{
    if (pStaticGcInfo == NULL)
        return;

    for (UInt32 idxSeries = 0; idxSeries < pStaticGcInfo->m_numSeries; idxSeries++)
    {
        PTR_StaticGcDescGCSeries pSeries = dac_cast<PTR_StaticGcDescGCSeries>(dac_cast<TADDR>(pStaticGcInfo) +
                                                                              offsetof(StaticGcDesc, m_series) +
                                                                              (idxSeries * sizeof(StaticGcDesc::GCSeries)));

        ASSERT(IS_ALIGNED(dac_cast<TADDR>(pbStaticData), sizeof(RtuObjectRef)));
        ASSERT(IS_ALIGNED(pSeries->m_startOffset, sizeof(RtuObjectRef)));
        ASSERT(IS_ALIGNED(pSeries->m_size, sizeof(RtuObjectRef)));

        PTR_RtuObjectRef    pRefLocation = dac_cast<PTR_RtuObjectRef>(pbStaticData + pSeries->m_startOffset);
        UInt32              numObjects = pSeries->m_size / sizeof(RtuObjectRef);

        RedhawkGCInterface::BulkEnumGcObjRef(pRefLocation, numObjects, pfnCallback, pvCallbackData);
    }
}

void Module::EnumStaticGCRefs(void * pfnCallback, void * pvCallbackData)
{
    // Regular statics.
    EnumStaticGCRefsBlock(pfnCallback, pvCallbackData, m_pStaticsGCInfo, m_pStaticsGCDataSection);

    // Thread local statics.
    if (m_pThreadStaticsGCInfo != NULL)
    {
        FOREACH_THREAD(pThread)
        {
            // To calculate the address of the data for each thread's TLS fields we need two values:
            //  1) The TLS slot index allocated for this module by the OS loader. We keep a pointer to this
            //     value in the module header.
            //  2) The offset into the TLS block at which Redhawk-specific data begins. This is zero for
            //     modules generated by the binder in PE mode, but maybe something else for COFF-mode modules
            //     (if some of the native code we're linked with also uses thread locals). We keep this offset
            //     in the module header as well.
            EnumStaticGCRefsBlock(pfnCallback, pvCallbackData, m_pThreadStaticsGCInfo,
                                  pThread->GetThreadLocalStorage(*m_pModuleHeader->PointerToTlsIndex,
                                                                 m_pModuleHeader->TlsStartOffset));
        }
        END_FOREACH_THREAD
    }
}

bool Module::IsFunclet(MethodInfo * pMethodInfo)
{
    return GetEEMethodInfo(pMethodInfo)->GetGCInfoHeader()->IsFunclet();
}

PTR_VOID Module::GetFramePointer(MethodInfo *   pMethodInfo, 
                                 REGDISPLAY *   pRegisterSet)
{
    return EECodeManager::GetFramePointer(GetEEMethodInfo(pMethodInfo)->GetGCInfoHeader(), pRegisterSet);
}

void Module::EnumGcRefs(MethodInfo *    pMethodInfo,
                        PTR_VOID        safePointAddress,
                        REGDISPLAY *    pRegisterSet,
                        GCEnumContext * hCallback)
{

    MethodGcInfoPointers infoPtrs;
    infoPtrs.m_pGCInfoHeader            = GetEEMethodInfo(pMethodInfo)->GetGCInfoHeader();
    infoPtrs.m_pbEncodedSafePointList   = GetEEMethodInfo(pMethodInfo)->GetGCInfo();
    infoPtrs.m_pbCallsiteStringBlob     = GetCallsiteStringBlob();
    infoPtrs.m_pbDeltaShortcutTable     = GetDeltaShortcutTable();

    UInt32 codeOffset = (UInt32)(dac_cast<TADDR>(safePointAddress) - dac_cast<TADDR>(GetEEMethodInfo(pMethodInfo)->GetCode()));
    ASSERT(codeOffset < GetEEMethodInfo(pMethodInfo)->GetCodeSize())
    EECodeManager::EnumGcRefs(&infoPtrs, codeOffset, pRegisterSet, hCallback);
}

bool Module::UnwindStackFrame(MethodInfo *  pMethodInfo,
                              REGDISPLAY *  pRegisterSet,
                              PTR_VOID *    ppPreviousTransitionFrame)
{
    EEMethodInfo * pEEMethodInfo = GetEEMethodInfo(pMethodInfo);

    *ppPreviousTransitionFrame = EECodeManager::GetReversePInvokeSaveFrame(pEEMethodInfo->GetGCInfoHeader(), pRegisterSet);
    if (*ppPreviousTransitionFrame != NULL)
        return true;

    return EECodeManager::UnwindStackFrame(pEEMethodInfo->GetGCInfoHeader(), pRegisterSet);
}

UIntNative Module::GetConservativeUpperBoundForOutgoingArgs(MethodInfo *   pMethodInfo,
                                                            REGDISPLAY *   pRegisterSet)
{
    return EECodeManager::GetConservativeUpperBoundForOutgoingArgs(
        GetEEMethodInfo(pMethodInfo)->GetGCInfoHeader(), pRegisterSet);
}

bool Module::GetReturnAddressHijackInfo(MethodInfo *    pMethodInfo,
                                        REGDISPLAY *    pRegisterSet,
                                        PTR_PTR_VOID *  ppvRetAddrLocation,
                                        GCRefKind *     pRetValueKind)
{
#ifdef DACCESS_COMPILE
    UNREFERENCED_PARAMETER(pMethodInfo);
    UNREFERENCED_PARAMETER(pRegisterSet);
    UNREFERENCED_PARAMETER(ppvRetAddrLocation);
    UNREFERENCED_PARAMETER(pRetValueKind);
    return false;
#else
    EEMethodInfo * pEEMethodInfo = GetEEMethodInfo(pMethodInfo);
    GCInfoHeader * pInfoHeader = pEEMethodInfo->GetGCInfoHeader();

    PTR_UInt8 controlPC = (PTR_UInt8)pRegisterSet->GetIP();
    UInt32 codeOffset = (UInt32)(controlPC - (PTR_UInt8)pEEMethodInfo->GetCode());
    PTR_PTR_VOID pRetAddr = EECodeManager::GetReturnAddressLocationForHijack(
        pInfoHeader,
        pEEMethodInfo->GetCodeSize(),
        pEEMethodInfo->GetEpilogTable(),
        codeOffset, 
        pRegisterSet);

    if (pRetAddr == NULL)
        return false;

    *ppvRetAddrLocation = pRetAddr;
    *pRetValueKind = EECodeManager::GetReturnValueKind(pInfoHeader);

    return true;
#endif
}

struct EEEHEnumState
{
    PTR_UInt8 pMethodStartAddress;
    PTR_UInt8 pEHInfo;
    UInt32 uClause;
    UInt32 nClauses;
};

// Ensure that EEEHEnumState fits into the space reserved by EHEnumState
static_assert(sizeof(EEEHEnumState) <= sizeof(EHEnumState), "EEEHEnumState does not fit into EHEnumState");

bool Module::EHEnumInit(MethodInfo * pMethodInfo, PTR_VOID * pMethodStartAddressOut, EHEnumState * pEHEnumStateOut)
{
    EEMethodInfo * pInfo = GetEEMethodInfo(pMethodInfo);

    PTR_VOID pEHInfo = pInfo->GetEHInfo();
    if (pEHInfo == NULL)
        return false;

    *pMethodStartAddressOut = pInfo->GetCode();

    EEEHEnumState * pEnumState = (EEEHEnumState *)pEHEnumStateOut;
    pEnumState->pMethodStartAddress = (PTR_UInt8)pInfo->GetCode();
    pEnumState->pEHInfo = (PTR_UInt8)pEHInfo;
    pEnumState->uClause = 0;
    pEnumState->nClauses = VarInt::ReadUnsigned(pEnumState->pEHInfo);

    return true;
}

bool Module::EHEnumNext(EHEnumState * pEHEnumState, EHClause * pEHClauseOut)
{
    EEEHEnumState * pEnumState = (EEEHEnumState *)pEHEnumState;

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
    //      4a) if (typed)                       { index into type table }
    //      4b) if (filter)                      { filter start offset }
    //
    // The first two integers have already been decoded
    UInt8* methodStartAddress = dac_cast<UInt8*>(pEnumState->pMethodStartAddress);
    switch (pEHClauseOut->m_clauseKind)
    {
    case EH_CLAUSE_TYPED:
        pEHClauseOut->m_handlerAddress = methodStartAddress + VarInt::ReadUnsigned(pEnumState->pEHInfo);

        {
            UInt32 typeIndex = VarInt::ReadUnsigned(pEnumState->pEHInfo);

            void * pvTargetType = ((void **) m_pEHTypeTable)[typeIndex];

            // We distinguish between these two cases by inspecting the low bit 
            // of the EHTypeTable entry.  If it is set, the entry points to an 
            // indirection cell.
            if ((((TADDR)pvTargetType) & 1) == 1)
                pvTargetType = *(void**)(((UInt8*)pvTargetType) - 1);

            pEHClauseOut->m_pTargetType = pvTargetType;
        }
        break;
    case EH_CLAUSE_FAULT:
        pEHClauseOut->m_handlerAddress = methodStartAddress + VarInt::ReadUnsigned(pEnumState->pEHInfo);
        break;
    case EH_CLAUSE_FILTER:
        pEHClauseOut->m_handlerAddress = methodStartAddress + VarInt::ReadUnsigned(pEnumState->pEHInfo);
        pEHClauseOut->m_filterAddress = methodStartAddress + VarInt::ReadUnsigned(pEnumState->pEHInfo);
        break;
    default:
        ASSERT_UNCONDITIONALLY("Unexpected EHClauseKind");
        break;
    }

    return true;
}

PTR_VOID Module::GetMethodStartAddress(MethodInfo * pMethodInfo)
{
    EEMethodInfo * pInfo = GetEEMethodInfo(pMethodInfo);
    PTR_VOID pvStartAddress = pInfo->GetCode();
#ifndef DACCESS_COMPILE
    // this may be the start of the cold section of a method -
    // we really want to obtain the start of the hot section instead

    // obtain the mapping information - if there is none, return what we have
    ColdToHotMapping *pColdToHotMapping = (ColdToHotMapping *)m_pModuleHeader->GetColdToHotMappingInfo();
    if (pColdToHotMapping == nullptr)
        return pvStartAddress;

    // this start address better be in this module
    ASSERT(ContainsCodeAddress(pvStartAddress));

    PTR_UInt8 pbStartAddress = dac_cast<PTR_UInt8>(pvStartAddress);

    UInt32 uMethodSize;
    UInt32 uMethodIndex;
    UInt32 uMethodStartSectionOffset;

    // repeat the lookup of the method index - this is a bit inefficient, but probably
    // better than burdening the EEMethodInfo with storing the rarely required index
    PTR_UInt8 pbTextSectionStart = m_pModuleHeader->RegionPtr[ModuleHeader::TEXT_REGION];
    UInt32 uTextSectionOffset = (UInt32)(pbStartAddress - pbTextSectionStart);
    m_MethodList.GetMethodInfo(uTextSectionOffset, &uMethodIndex, &uMethodStartSectionOffset, &uMethodSize);

    // we should have got the start of this body already, whether hot or cold
    ASSERT(uMethodStartSectionOffset == uTextSectionOffset);

    UInt32 uSubSectionCount = pColdToHotMapping->subSectionCount;
    SubSectionDesc *pSubSection = (SubSectionDesc *)pColdToHotMapping->subSection;
    UInt32 *pHotRVA = (UInt32 *)(pSubSection + uSubSectionCount);

    // iterate over the subsections, trying to find the correct range
    for (UInt32 uSubSectionIndex = 0; uSubSectionIndex < uSubSectionCount; uSubSectionIndex++)
    {
        // is the method index in the hot range? If so, we are done
        if (uMethodIndex < pSubSection->hotMethodCount)
            return pvStartAddress;
        uMethodIndex -= pSubSection->hotMethodCount;
        
        // is the method index in the cold range?
        if (uMethodIndex < pSubSection->coldMethodCount)
        {
            UInt32 hotRVA = pHotRVA[uMethodIndex];
            pvStartAddress = GetBaseAddress() + hotRVA;

            // this start address better be in this module
            ASSERT(ContainsCodeAddress(pvStartAddress));

            return pvStartAddress;
        }
        uMethodIndex -= pSubSection->coldMethodCount;
        pHotRVA += pSubSection->coldMethodCount;
        pSubSection += 1;
    }
    ASSERT_UNCONDITIONALLY("MethodIndex not found");
#endif // DACCESS_COMPILE
    return pvStartAddress;
}

static PTR_VOID GetFuncletSafePointForIncomingLiveReferences(Module * pModule, EEMethodInfo * pInfo, UInt32 funcletStart)
{
    // The binder will encode a GC safe point (as appropriate) at the first code offset after the 
    // prolog to represent the "incoming" GC references.  This safe point is 'special' because it 
    // doesn't occur at an offset that would otherwise be a safe point.  Additionally, it doesn't 
    // report any scratch registers that might actually be live at that point in the funclet code
    // (namely the incoming Exception object).  In other words, this is just a convenient way to reuse
    // the existing infrastructure to get our GC roots reported for a hardware fault at a non-GC-safe
    // point.

    // N.B. - we cannot side-effect the current m_methodInfo or other state variables other than 
    // m_ControlPC and m_codeOffset because, although we've remapped the control PC, it's not really
    // where we are unwinding from.  We're just pretending that we're in the funclet for GC reporting
    // purposes, but the unwind needs to happen from the original location.

    EEMethodInfo tempInfo;

    PTR_UInt8 methodStart = (PTR_UInt8)pInfo->GetCode();
    tempInfo.Init(methodStart, pInfo->GetCodeSize(), pInfo->GetRawGCInfo(), pInfo->GetEHInfo());

    tempInfo.DecodeGCInfoHeader(funcletStart, pModule->GetUnwindInfoBlob());

    GCInfoHeader * pHeader = tempInfo.GetGCInfoHeader();
    UInt32 cbProlog = pHeader->GetPrologSize();
    UInt32 codeOffset = funcletStart + cbProlog;
#ifdef _ARM_
    codeOffset &= ~1;
#endif

    return methodStart + codeOffset;
}

PTR_VOID Module::RemapHardwareFaultToGCSafePoint(MethodInfo * pMethodInfo, PTR_VOID controlPC)
{
    EEMethodInfo * pInfo = GetEEMethodInfo(pMethodInfo);

    EHEnumState ehEnum;
    PTR_VOID pMethodStartAddress;
    if (!EHEnumInit(pMethodInfo, &pMethodStartAddress, &ehEnum))
        return controlPC;

    PTR_UInt8 methodStart = (PTR_UInt8)pInfo->GetCode();
    UInt32 codeOffset = (UInt32)((PTR_UInt8)controlPC - methodStart);
    EHClause ehClause;
    while (EHEnumNext(&ehEnum, &ehClause))
    {
        if ((ehClause.m_tryStartOffset <= codeOffset) && (codeOffset < ehClause.m_tryEndOffset))
        {
            UInt32 handlerOffset = (UInt32)(dac_cast<PTR_UInt8>(ehClause.m_handlerAddress) - methodStart);
            return GetFuncletSafePointForIncomingLiveReferences(this, pInfo, handlerOffset);
        }
    }

    // We didn't find a try region covering our PC.  However, if the PC is in a funclet, we must do more work.
    GCInfoHeader * pThisFuncletUnwindInfo = pInfo->GetGCInfoHeader();
    if (!pThisFuncletUnwindInfo->IsFunclet())
        return controlPC;

    // For funclets, we must correlate the funclet to its corresponding try region and check for enclosing try
    // regions that might catch the exception as it "escapes" the funclet.

    UInt32 thisFuncletOffset = pThisFuncletUnwindInfo->GetFuncletOffset();

    UInt32 tryRegionStart = 0;
    UInt32 tryRegionEnd = 0;
    bool foundTryRegion = false;

    EHEnumInit(pMethodInfo, &pMethodStartAddress, &ehEnum);

    while (EHEnumNext(&ehEnum, &ehClause))
    {
        UInt32 handlerOffset = (UInt32)(dac_cast<PTR_UInt8>(ehClause.m_handlerAddress) - methodStart);
        if (foundTryRegion && (ehClause.m_tryStartOffset <= tryRegionStart) && (tryRegionEnd <= ehClause.m_tryEndOffset))
        {
            // the regions aren't nested if they have exactly the same range.
            if ((ehClause.m_tryStartOffset != tryRegionStart) || (tryRegionEnd != ehClause.m_tryEndOffset))
            {
                return GetFuncletSafePointForIncomingLiveReferences(this, pInfo, handlerOffset);
            }
        }

        if (handlerOffset == thisFuncletOffset)
        {
            tryRegionStart = ehClause.m_tryStartOffset;
            tryRegionEnd = ehClause.m_tryEndOffset;
            foundTryRegion = true;
            // After we find the target region, we can just keep looking without reseting our iterator.  This
            // is because the clauses are emitted in an "inside-out" order, so we know that enclosing clauses
            // may only appear after the target clause.
        }
    }
    ASSERT(foundTryRegion);
    return controlPC;
}

#ifndef DACCESS_COMPILE

//------------------------------------------------------------------------------------------------------------
// @TODO: the following functions are related to throwing exceptions out of Rtm. If we did not have to throw
// out of Rtm, then we would note have to have the code below to get a classlib exception object given
// an exception id, or the special functions to back up the MDIL THROW_* instructions, or the allocation
// failure helper. If we could move to a world where we never throw out of Rtm, perhaps by moving parts
// of Rtm that do need to throw out to Bartok- or Binder-generated functions, then we could remove all of this.
//------------------------------------------------------------------------------------------------------------

// Return the Module that is the "classlib module" for this Module. This is the module that was supplied as
// the classlib when this module was bound. This module typically defines System.Object and other base types.
// The classlib module is also required to export two functions needed by the runtime to implement exception
// handling and fail fast.
Module * Module::GetClasslibModule()
{
    // Every non-classlib module has a RVA to a IAT entry for System.Object in the classlib module it 
    // was compiled against. Therefore, we can use that address to locate the Module for the classlib module.
    // If this is a classlib module, then we can just return it.
    if (IsClasslibModule())
    {
        return this;
    }

    void ** ppSystemObjectEEType = (void**)(m_pModuleHeader->RegionPtr[ModuleHeader::IAT_REGION] +
                                            m_pModuleHeader->RraSystemObjectEEType);

    return GetRuntimeInstance()->FindModuleByReadOnlyDataAddress(*ppSystemObjectEEType);
}

bool Module::IsClasslibModule()
{
    return (m_pModuleHeader->RraSystemObjectEEType == ModuleHeader::NULL_RRA);
}

// Array eetypes have a common base type defined by the classlib module
EEType * Module::GetArrayBaseType()
{
    // find the class lib module
    Module * pClasslibModule = GetClasslibModule();

    // find the System.Array EEType
    EEType * pArrayBaseType = (EEType *)(pClasslibModule->m_pModuleHeader->RegionPtr[ModuleHeader::RDATA_REGION] +
                                         pClasslibModule->m_pModuleHeader->RraArrayBaseEEType);

    // we expect to find a canonical type (not cloned, not array, not "other")
    ASSERT(pArrayBaseType->IsCanonical());

    return pArrayBaseType;
}

// Return the classlib-defined helper.
void * Module::GetClasslibFunction(ClasslibFunctionId functionId)
{
    // First, delegate the call to the classlib module that this module was compiled against.
    if (!IsClasslibModule())
        return GetClasslibModule()->GetClasslibFunction(functionId);

    // Lookup the method and return it. If we don't find it, we just return NULL.
    void * pMethod;

    switch (functionId)
    {
    case ClasslibFunctionId::GetRuntimeException:
        pMethod = m_pModuleHeader->Get_GetRuntimeException();
        break;
    case ClasslibFunctionId::AppendExceptionStackFrame:
        pMethod = m_pModuleHeader->Get_AppendExceptionStackFrame();
        break;
    case ClasslibFunctionId::FailFast:
        pMethod = m_pModuleHeader->Get_FailFast();
        break;
    case ClasslibFunctionId::UnhandledExceptionHandler:
        pMethod = m_pModuleHeader->Get_UnhandledExceptionHandler();
        break;
    case ClasslibFunctionId::CheckStaticClassConstruction:
        pMethod = m_pModuleHeader->Get_CheckStaticClassConstruction();
        break;
    case ClasslibFunctionId::OnFirstChanceException:
        pMethod = m_pModuleHeader->Get_OnFirstChanceException();
        break;
    case ClasslibFunctionId::DebugFuncEvalHelper:
        pMethod = m_pModuleHeader->Get_DebugFuncEvalHelper();
        break;
    case ClasslibFunctionId::DebugFuncEvalAbortHelper:
        pMethod = m_pModuleHeader->Get_DebugFuncEvalAbortHelper();
        break;
    default:
        pMethod = NULL;
        break;
    }

    return pMethod;
}

PTR_VOID Module::GetAssociatedData(PTR_VOID ControlPC)
{
    UNREFERENCED_PARAMETER(ControlPC);

    // Not supported for ProjectN.
    return NULL;
}

// Get classlib-defined helper for running deferred static class constructors. Returns NULL if this is not the
// classlib module or the classlib doesn't implement this callback.
void * Module::GetClasslibCheckStaticClassConstruction()
{
    return m_pModuleHeader->Get_CheckStaticClassConstruction();
}

// Returns the classlib-defined helper for initializing the finalizer thread.  The contract is that it will be
// run before any object based on that classlib is finalized.
void * Module::GetClasslibInitializeFinalizerThread()
{
    return m_pModuleHeader->Get_InitializeFinalizerThread();
}

// Returns true if this module is part of the OS module specified by hOsHandle.
bool Module::IsContainedBy(HANDLE hOsHandle)
{
    return m_hOsModuleHandle == hOsHandle;
}

void Module::UnregisterFrozenSection()
{
    RedhawkGCInterface::UnregisterFrozenSection(m_FrozenSegment);
}

//
// Hijack the loops within the method referred to by pMethodInfo.
// WARNING: Only one thread may call this at a time (i.e. the thread performing suspension of all others).
void Module::UnsynchronizedHijackMethodLoops(MethodInfo * pMethodInfo)
{
    void ** ppvIndirCells = (void **)m_pModuleHeader->GetLoopIndirCells();
    UInt32  nIndirCells = m_pModuleHeader->CountOfLoopIndirCells;
    if (nIndirCells == 0)
        return;

    EEMethodInfo * pEEMethodInfo = GetEEMethodInfo(pMethodInfo);

    void * pvMethodStart = pEEMethodInfo->GetCode();
    void * pvMethodEnd = ((UInt8 *)pvMethodStart) + pEEMethodInfo->GetCodeSize();

    void * pvRedirStubsStart = m_pModuleHeader->GetLoopRedirTargets();
    void * pvRedirStubsEnd   = ((UInt8 *)pvRedirStubsStart) + GcPollInfo::EntryIndexToStubOffset(nIndirCells);

#ifdef _TARGET_ARM_
    // on ARM, there is just one redir stub, because we can compute the indir cell index 
    // from the indir cell pointer left in r12
    // to make the range tests below work, bump up the end by one byte
    ASSERT(pvRedirStubsStart == pvRedirStubsEnd);
    pvRedirStubsEnd = (void *)(((UInt8 *)pvRedirStubsEnd)+1);
#endif // _TARGET_ARM_


    void ** ppvStart = &ppvIndirCells[0];
    void ** ppvEnd   = &ppvIndirCells[nIndirCells];
    void ** ppvTest;

    while ((ppvStart + 1) < ppvEnd)
    {
        ppvTest = ppvStart + ((ppvEnd - ppvStart)/2);
        void * cellContents = *ppvTest;

        // look to see if the cell has already been hijacked
        if ((pvRedirStubsStart <= cellContents) && (cellContents < pvRedirStubsEnd))
        {
            void ** ppvCur = ppvTest;
            // try incrementing ppvTest until it hits ppvEnd
            while (++ppvCur < ppvEnd)
            {
                cellContents = *ppvCur;
                if ((pvRedirStubsStart > cellContents) || (cellContents >= pvRedirStubsEnd))
                    break;
            }
            if (ppvCur == ppvEnd)
            {
                // We hit the end and didn't find any non-hijacked cells,
                // so let's shrink the range and start over.
                ppvEnd = ppvTest;
                continue;
            }
        }

        if (pvMethodStart >= cellContents)
        {
            ppvStart = ppvTest;
        }
        else if (pvMethodStart < cellContents)
        {
            ppvEnd = ppvTest;
        }
    }
    ppvTest = ppvStart;

    // At this point start and end are pointing to consecutive entries
    ASSERT((ppvStart + 1) == ppvEnd);

    // Reset start and end.
    ppvStart = &ppvIndirCells[0];
    ppvEnd   = &ppvIndirCells[nIndirCells];

    // We shouldn't have walked off the end of the array
    ASSERT((ppvStart <= ppvTest) && (ppvTest < ppvEnd));

    // ppvTest may point the the cell before the first cell in the method or to the first cell in the method.
    // So we must test it separately to see whether or not to hijack it.
    if (*ppvTest < pvMethodStart)
        ppvTest++;

    UInt8 * pbDirtyBitmap = m_pModuleHeader->GetLoopIndirCellChunkBitmap();;

    // now hijack all the entries to the end of the method
    for (;;)
    {
        void * cellContents = *ppvTest;

        // skip already hijacked cells
        while ((pvRedirStubsStart <= cellContents) && (cellContents < pvRedirStubsEnd) && (ppvTest < ppvEnd))
        {
            ppvTest++;
            cellContents = *ppvTest;
        }
        if (ppvTest >= ppvEnd)              // walked off the end of the array
            break;
        if (cellContents >= pvMethodEnd)    // walked off the end of the method
            break;

        UInt32 entryIndex = (UInt32)(ppvTest - ppvIndirCells);

        UnsynchronizedHijackLoop(ppvTest, entryIndex, pvRedirStubsStart, pbDirtyBitmap);

        ppvTest++;
    }
}

// WARNING: Caller must perform synchronization!
void Module::UnsynchronizedResetHijackedLoops()
{
    if (g_fGcStressStarted)
        return; // don't ever reset loop hijacks when GC stress is enabled

    if (m_pModuleHeader == nullptr) // @TODO: simple modules and loop hijacking
        return;

    void ** ppvIndirCells = (void **)m_pModuleHeader->GetLoopIndirCells();
    UInt32  nIndirCells = m_pModuleHeader->CountOfLoopIndirCells;
    if (nIndirCells == 0)
        return;

    UInt8 * pbDirtyBitmapStart  = m_pModuleHeader->GetLoopIndirCellChunkBitmap();
    UInt32  cellsPerByte        = (GcPollInfo::indirCellsPerBitmapBit * 8);
    UInt32  nBitmapBytes        = (nIndirCells + (cellsPerByte - 1)) / cellsPerByte; // round up to the next byte
    UInt8 * pbDirtyBitmapEnd    = pbDirtyBitmapStart + nBitmapBytes;

    void ** ppvCurIndirCell     = &ppvIndirCells[0];
    void ** ppvIndirCellsEnd    = &ppvIndirCells[nIndirCells];

    UInt8 * pbTargetsInfoStart  = m_pModuleHeader->GetLoopTargets();
    UInt8 * pbCurrentChunkPtr   = pbTargetsInfoStart;

    for (UInt8 * pbBitmapCursor = pbDirtyBitmapStart; pbBitmapCursor < pbDirtyBitmapEnd; pbBitmapCursor++)
    {
        UInt8 currentByte = *pbBitmapCursor;

        for (UInt8 mask = 0x80; mask > 0; mask >>= 1)
        {
            if (currentByte & mask)
            {
                UInt32 currentChunkOffset = VarInt::ReadUnsigned(pbCurrentChunkPtr);
                UInt8 * pbChunkInfo = pbTargetsInfoStart + currentChunkOffset;
                UInt32 targetOffset = VarInt::ReadUnsigned(pbChunkInfo);

                for (void ** ppvTemp = ppvCurIndirCell;
                    ppvTemp < (ppvCurIndirCell + GcPollInfo::indirCellsPerBitmapBit);
                    ppvTemp++)
                {
                    if (ppvTemp >= ppvIndirCellsEnd)
                        return; // the last byte was only partially populated

                    *ppvTemp = m_pModuleHeader->RegionPtr[ModuleHeader::TEXT_REGION] + targetOffset;
                    targetOffset += VarInt::ReadUnsigned(pbChunkInfo);
                }

                // WARNING: This not synchronized! -- We expect to perform these actions only when
                // all threads are suspended for GC.
                currentByte ^= mask;    // reset the bit in the bitmap
                ASSERT((currentByte & mask) == 0);
            }
            else
            {
                VarInt::SkipUnsigned(pbCurrentChunkPtr);
            }
            ppvCurIndirCell += GcPollInfo::indirCellsPerBitmapBit;
        }
    }
}

void * Module::RecoverLoopHijackTarget(UInt32 entryIndex, ModuleHeader * pModuleHeader)
{
    // read lock scope
    {
        ReaderWriterLock::ReadHolder readHolder(&m_loopHijackMapLock);
        void * pvLoopTarget;
        if (m_loopHijackIndexToTargetMap.Lookup(entryIndex, &pvLoopTarget))
        {
            return pvLoopTarget;
        }
    }

    UInt8 * pbTargetsInfoStart  = pModuleHeader->GetLoopTargets();
    UInt8 * pbCurrentChunkPtr   = pbTargetsInfoStart;

    UInt32 bitIndex = entryIndex / GcPollInfo::indirCellsPerBitmapBit; 
    for (UInt32 idx = 0; idx < bitIndex; idx++)
    {
        VarInt::SkipUnsigned(pbCurrentChunkPtr);
    }

    UInt32 currentChunkOffset = VarInt::ReadUnsigned(pbCurrentChunkPtr);
    UInt8 * pbCurrentInfo = pbTargetsInfoStart + currentChunkOffset;
    UInt32 targetOffset = VarInt::ReadUnsigned(pbCurrentInfo);

    for (UInt32 chunkSubIndex = entryIndex - (bitIndex * GcPollInfo::indirCellsPerBitmapBit);
         chunkSubIndex > 0;
         chunkSubIndex--)
    {
        targetOffset += VarInt::ReadUnsigned(pbCurrentInfo);
    }

    void * pvLoopTarget = pModuleHeader->RegionPtr[ModuleHeader::TEXT_REGION] + targetOffset;

    // write lock scope
    {
        ReaderWriterLock::WriteHolder writeHolder(&m_loopHijackMapLock);
        KeyValuePair<UInt32, void *> newEntry = { entryIndex, pvLoopTarget };
        m_loopHijackIndexToTargetMap.AddOrReplace(newEntry);
    }

    return pvLoopTarget;
}

void Module::UnsynchronizedHijackAllLoops()
{
    void ** ppvIndirCells = (void **)m_pModuleHeader->GetLoopIndirCells();
    UInt32  nIndirCells = m_pModuleHeader->CountOfLoopIndirCells;
    if (nIndirCells == 0)
        return;

    void * pvRedirStubsStart = m_pModuleHeader->GetLoopRedirTargets();
    UInt8 * pbDirtyBitmap = m_pModuleHeader->GetLoopIndirCellChunkBitmap();

    for (UInt32 idx = 0; idx < nIndirCells; idx++)
    {
        UnsynchronizedHijackLoop(&ppvIndirCells[idx], idx, pvRedirStubsStart, pbDirtyBitmap);
    }
}

// static
void Module::UnsynchronizedHijackLoop(void ** ppvIndirectionCell, UInt32 cellIndex, 
                                      void * pvRedirStubsStart, UInt8 * pbDirtyBitmap)
{
    //
    // set the dirty bit
    //
    UInt32  bitmapByteIndex =  cellIndex / (GcPollInfo::indirCellsPerBitmapBit  * 8);
    UInt32  bitmapBitIndex  = (cellIndex /  GcPollInfo::indirCellsPerBitmapBit) % 8;
    UInt8   bitMask         = 1 << (7 - bitmapBitIndex);
    UInt8 * pBitmapByte     = &pbDirtyBitmap[bitmapByteIndex];

    // WARNING: The assumption here is that there is only one thread ever updating this bitmap (i.e. the 
    // thread performing the suspension of all other threads).  If this assumption is violated, then this
    // code is broken because it does a read-modify-write which could overwrite other writers' updates.
    UInt8 newByte = (*pBitmapByte) | bitMask;
    *((UInt8 *)pBitmapByte) = newByte;

    //
    // hijack the loop's indirection cell
    //
    *ppvIndirectionCell = ((UInt8 *)pvRedirStubsStart) + GcPollInfo::EntryIndexToStubOffset(cellIndex);
}

DispatchMap ** Module::GetDispatchMapLookupTable()
{
    return (DispatchMap**)(m_pModuleHeader->RegionPtr[ModuleHeader::RDATA_REGION] +
                           m_pModuleHeader->RraDispatchMapLookupTable);
}

HANDLE Module::GetOsModuleHandle()
{
    return m_hOsModuleHandle;
}

BlobHeader * Module::GetReadOnlyBlobs(UInt32 * pcbBlobs)
{
    *pcbBlobs = m_pModuleHeader->SizeReadOnlyBlobs;
    return (BlobHeader*)m_pModuleHeader->GetReadOnlyBlobs();
}

#ifdef FEATURE_CUSTOM_IMPORTS

#define IMAGE_ORDINAL_FLAG64 0x8000000000000000
#define IMAGE_ORDINAL_FLAG32 0x80000000

#ifdef _TARGET_AMD64_
#define TARGET_IMAGE_ORDINAL_FLAG IMAGE_ORDINAL_FLAG64
#else
#define TARGET_IMAGE_ORDINAL_FLAG IMAGE_ORDINAL_FLAG32
#endif

/*static*/
void Module::DoCustomImports(ModuleHeader * pModuleHeader)
{
// Address issue 432987: rather than AV on invalid ordinals, it's better to fail fast, so turn the
// asserts below into conditional failfast calls
#define ASSERT_FAILFAST(cond)  if (!(cond)) RhFailFast()

    CustomImportDescriptor *customImportTable = (CustomImportDescriptor *)pModuleHeader->GetCustomImportDescriptors();
    UInt32 countCustomImports = pModuleHeader->CountCustomImportDescriptors;

    // obtain base address for this module
    PTR_UInt8 thisBaseAddress = (PTR_UInt8)PalGetModuleHandleFromPointer(pModuleHeader);

    for (UInt32 i = 0; i < countCustomImports; i++)
    {
        // obtain address of indirection cell pointing to the EAT for the exporting module
        UInt32 **ptrPtrEAT = (UInt32 **)(thisBaseAddress + customImportTable[i].RvaEATAddr);

        // obtain the EAT by dereferencing
        UInt32 *ptrEAT = *ptrPtrEAT;
 
        // obtain the exporting module
        HANDLE hExportingModule = PalGetModuleHandleFromPointer(ptrEAT);

        // obtain the base address of the exporting module
        PTR_UInt8 targetBaseAddress = (PTR_UInt8)hExportingModule;
 
        // obtain the address of the IAT and the number of entries
        UIntTarget *ptrIAT = (UIntTarget *)(thisBaseAddress + customImportTable[i].RvaIAT);
        UInt32 countIAT = customImportTable[i].CountIAT;
 
        if (i == 0)
        {
            // the first entry is a dummy entry that points to a flag
            UInt32 *pFlag = (UInt32 *)ptrIAT;

            // the ptr to the EAT indirection cell also points to the flag
            ASSERT_FAILFAST((UInt32 *)ptrPtrEAT == pFlag);
            
            // the number of IAT entries should be zero
            ASSERT_FAILFAST(countIAT == 0);

            // if the flag is set, it means we have fixed up this module already
            // this is our check against infinite recursion
            if (*pFlag == TRUE)
                return;

            // if the flag is not set, it must be clear
            ASSERT_FAILFAST(*pFlag == FALSE);

            // set the flag
            *pFlag = TRUE;
        }
        else
        {
            // iterate over the IAT, replacing ordinals with real addresses
            for (UInt32 j = 0; j < countIAT; j++)
            {
                // obtain the ordinal
                UIntTarget ordinal = ptrIAT[j];

                // the ordinals should have the high bit set
                ASSERT_FAILFAST((ordinal & TARGET_IMAGE_ORDINAL_FLAG) != 0);

                // the ordinals should be in increasing order, for perf reasons
                ASSERT_FAILFAST(j+1 == countIAT || ordinal < ptrIAT[j+1]);

                ordinal &= ~TARGET_IMAGE_ORDINAL_FLAG;

                // sanity check: limit ordinals to < 1 Million
                ASSERT_FAILFAST(ordinal < 1024 * 1024);

                // obtain the target RVA
                UInt32 targetRVA = ptrEAT[ordinal];
 
                // obtain the target address by adding the base address of the exporting module
                UIntTarget targetAddr = (UIntTarget)(targetBaseAddress + targetRVA);
 
                // write the target address to the IAT slot, overwriting the ordinal
                ptrIAT[j] = targetAddr;                 
            }
            // find the module header of the target module - this is a bit of a hack
            // as we assume the header is at the start of the first section
            // currently this is true for ProjectN files unless it's built by the native
            // linker from COFF files
            ModuleHeader *pTargetModuleHeader = (ModuleHeader *)(targetBaseAddress + 0x1000);

            // recursively fixup the target module as well - this is because our eager cctors may call
            // methods in the target module, which again may call imports of the target module
            DoCustomImports(pTargetModuleHeader);
        }
    }
#undef ASSERT_FAILFAST
}

#endif // FEATURE_CUSTOM_IMPORTS

#endif // DACCESS_COMPILE

#ifdef DACCESS_COMPILE
UInt32 StaticGcDesc::DacSize(TADDR addr)
{
    uint32_t numSeries = 0;
    DacReadAll(addr + offsetof(StaticGcDesc, m_numSeries), &numSeries, sizeof(numSeries), true);

    return (UInt32)(offsetof(StaticGcDesc, m_series) + (numSeries * sizeof(GCSeries)));
}
#endif // DACCESS_COMPILE
