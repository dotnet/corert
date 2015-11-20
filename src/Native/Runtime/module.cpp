//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"
#include "assert.h"
#include "slist.h"
#include "holder.h"
#include "gcrhinterface.h"
#include "module.h"
#include "varint.h"
#include "rhbinder.h"
#include "Crst.h"
#include "regdisplay.h"
#include "StackFrameIterator.h"
#include "thread.h"
#include "event.h"
#include "RWLock.h"
#include "RuntimeInstance.h"
#include "eetype.h"
#include "ObjectLayout.h"
#include "GenericInstance.h"
#include "threadstore.h"

#include "CommonMacros.inl"
#include "slist.inl"

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

Module * Module::Create(SimpleModuleHeader *pModuleHeader)
{
    NewHolder<Module> pNewModule = new (nothrow) Module(nullptr);
    if (NULL == pNewModule)
        return NULL;

    pNewModule->m_pSimpleModuleHeader = pModuleHeader;
    pNewModule->m_pEHTypeTable = nullptr;
    pNewModule->m_pbDeltaShortcutTable = nullptr;
    pNewModule->m_FrozenSegment = nullptr;
    pNewModule->m_pStaticsGCInfo = dac_cast<PTR_StaticGcDesc>(pModuleHeader->m_pStaticsGcInfo);
    pNewModule->m_pStaticsGCDataSection = dac_cast<PTR_UInt8>((UInt8*)pModuleHeader->m_pStaticsGcDataSection);
    pNewModule->m_pThreadStaticsGCInfo = nullptr;

    pNewModule->m_hOsModuleHandle = PalGetModuleHandleFromPointer(pModuleHeader);

    pNewModule.SuppressRelease();
    return pNewModule;
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
    // which is true for now. It's also exposed by a number of exports (RhCanUnloadModule,
    // RhGetModuleFromEEType etc.) so if we ever rethink this then the public contract needs to change as
    // well.
    pNewModule->m_hOsModuleHandle = PalGetModuleHandleFromPointer(pModuleHeader);
    if (!pNewModule->m_hOsModuleHandle)
    {
        ASSERT_UNCONDITIONALLY("Failed to locate our own module handle");
        return NULL;
    }

#ifdef FEATURE_CUSTOM_IMPORTS
    Module::DoCustomImports(pModuleHeader);
#endif // FEATURE_CUSTOM_IMPORTS

#ifdef FEATURE_VSD
    // VirtualCallStubManager::ApplyPartialPolymorphicCallSiteResetForModule relies on being able to
    // multiply CountVSDIndirectionCells by up to 100. Instead of trying to handle overflow gracefully
    // we reject modules that would cause such an overflow. This limits the number of indirection
    // cells to 1GB in number, which is perfectly reasonable given that this limit implies we'll also
    // hit (or exceed in the 64bit case) the PE image 4GB file size limit.
    if (pModuleHeader->CountVSDIndirectionCells > (UInt32_MAX / 100))
    {
        return NULL;
    }
#endif // FEATURE_VSD

#ifdef _DEBUG
#ifdef LOG_MODULE_LOAD_VERIFICATION
    PalPrintf("\r\nModule: 0x%p\r\n", pNewModule->m_hOsModuleHandle);
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
        PalPrintf("0x%08x: %3d 0x%08x 0x%08x\r\n", 
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
    PalPrintf("0x%08x: --- 0x%08x \r\n", (uTextSectionOffset + uMethodSize), 
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

PTR_GenericInstanceDesc Module::GetGidsWithGcRootsList()
{
    if (m_pModuleHeader == NULL)
        return NULL;

    return dac_cast<PTR_GenericInstanceDesc>(m_pModuleHeader->GetGidsWithGcRootsList());
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
                            MethodInfo *    pMethodInfoOut,
                            UInt32 *        pCodeOffset)
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
    *pCodeOffset = codeOffset;

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
    return EECodeManager::GetFramePointer(GetEEMethodInfo(pMethodInfo), pRegisterSet);
}

void Module::EnumGcRefs(MethodInfo *    pMethodInfo,
                        UInt32          codeOffset,
                        REGDISPLAY *    pRegisterSet,
                        GCEnumContext * hCallback)
{
    EECodeManager::EnumGcRefs(GetEEMethodInfo(pMethodInfo),
                              codeOffset,
                              pRegisterSet,
                              hCallback,
                              GetCallsiteStringBlob(),
                              GetDeltaShortcutTable());
}

bool Module::UnwindStackFrame(MethodInfo *  pMethodInfo,
                              UInt32        codeOffset,
                              REGDISPLAY *  pRegisterSet,
                              PTR_VOID *    ppPreviousTransitionFrame)
{
    EEMethodInfo * pEEMethodInfo = GetEEMethodInfo(pMethodInfo);

    *ppPreviousTransitionFrame = EECodeManager::GetReversePInvokeSaveFrame(pEEMethodInfo, pRegisterSet);
    if (*ppPreviousTransitionFrame != NULL)
        return true;

    return EECodeManager::UnwindStackFrame(pEEMethodInfo, codeOffset, pRegisterSet);
}

bool Module::GetReturnAddressHijackInfo(MethodInfo *    pMethodInfo,
                                        UInt32          codeOffset,
                                        REGDISPLAY *    pRegisterSet,
                                        PTR_PTR_VOID *  ppvRetAddrLocation,
                                        GCRefKind *     pRetValueKind)
{
#ifdef DACCESS_COMPILE
    UNREFERENCED_PARAMETER(pMethodInfo);
    UNREFERENCED_PARAMETER(codeOffset);
    UNREFERENCED_PARAMETER(pRegisterSet);
    UNREFERENCED_PARAMETER(ppvRetAddrLocation);
    UNREFERENCED_PARAMETER(pRetValueKind);
    return false;
#else
    EEMethodInfo * pEEMethodInfo = GetEEMethodInfo(pMethodInfo);

    PTR_PTR_VOID pRetAddr = EECodeManager::GetReturnAddressLocationForHijack(pEEMethodInfo, 
                                                                             codeOffset, 
                                                                             pRegisterSet);
    if (pRetAddr == NULL)
        return false;

    *ppvRetAddrLocation = pRetAddr;
    *pRetValueKind = EECodeManager::GetReturnValueKind(pEEMethodInfo);

    return true;
#endif
}

struct EEEHEnumState
{
    PTR_UInt8 pEHInfo;
    UInt32 uClause;
    UInt32 nClauses;
};

// Ensure that EEEHEnumState fits into the space reserved by EHEnumState
STATIC_ASSERT(sizeof(EEEHEnumState) <= sizeof(EHEnumState));

#if 1 // only needed for local-exception model
bool Module::EHEnumInitFromReturnAddress(PTR_VOID ControlPC, PTR_VOID * pMethodStartAddressOut, EHEnumState * pEHEnumStateOut)
{
    ASSERT(ContainsCodeAddress(ControlPC));

    PTR_UInt8 pbControlPC = dac_cast<PTR_UInt8>(ControlPC);

    UInt32 uMethodIndex;
    UInt32 uMethodStartSectionOffset;

    PTR_UInt8 pbTextSectionStart = m_pModuleHeader->RegionPtr[ModuleHeader::TEXT_REGION];
    UInt32 uTextSectionOffset = (UInt32)(pbControlPC - pbTextSectionStart);

    m_MethodList.GetMethodInfo(uTextSectionOffset, &uMethodIndex, &uMethodStartSectionOffset, NULL);

    *pMethodStartAddressOut = pbTextSectionStart + uMethodStartSectionOffset;

    PTR_VOID pEHInfo = m_MethodList.GetEHInfo(uMethodIndex);
    if (pEHInfo == NULL)
        return false;

    EEEHEnumState * pEnumState = (EEEHEnumState *)pEHEnumStateOut;
    pEnumState->pEHInfo = (PTR_UInt8)pEHInfo;
    pEnumState->uClause = 0;
    pEnumState->nClauses = VarInt::ReadUnsigned(pEnumState->pEHInfo);

    return true;
}
#endif // 1

bool Module::EHEnumInit(MethodInfo * pMethodInfo, PTR_VOID * pMethodStartAddressOut, EHEnumState * pEHEnumStateOut)
{
    EEMethodInfo * pInfo = GetEEMethodInfo(pMethodInfo);

    PTR_VOID pEHInfo = pInfo->GetEHInfo();
    if (pEHInfo == NULL)
        return false;

    *pMethodStartAddressOut = pInfo->GetCode();

    EEEHEnumState * pEnumState = (EEEHEnumState *)pEHEnumStateOut;
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
    //
    //      Local exceptions
    //      3) if (typed || fault) { handler start offset }
    //      4) if (typed)          { index into type table }
    //
    //      CLR exceptions
    //      3)  if (typed || fault || filter)    { handler start offset }
    //      4a) if (typed)                       { index into type table }
    //      4b) if (filter)                      { filter start offset }
    //
    // The first two integers have already been decoded

    switch (pEHClauseOut->m_clauseKind)
    {
    case EH_CLAUSE_TYPED:
        pEHClauseOut->m_handlerOffset = VarInt::ReadUnsigned(pEnumState->pEHInfo);

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
        pEHClauseOut->m_handlerOffset = VarInt::ReadUnsigned(pEnumState->pEHInfo);
        break;
    case EH_CLAUSE_FILTER:
        pEHClauseOut->m_handlerOffset = VarInt::ReadUnsigned(pEnumState->pEHInfo);
        pEHClauseOut->m_filterOffset = VarInt::ReadUnsigned(pEnumState->pEHInfo);
        break;
    case EH_CLAUSE_FAIL_FAST:
        break;
    }

    return true;
}

static void UpdateStateForRemappedGCSafePoint(Module * pModule, EEMethodInfo * pInfo, UInt32 funcletStart, UInt32 * pRemappedCodeOffset)
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

    tempInfo.Init(pInfo->GetCode(), pInfo->GetCodeSize(), pInfo->GetRawGCInfo(), pInfo->GetEHInfo());

    tempInfo.DecodeGCInfoHeader(funcletStart, pModule->GetUnwindInfoBlob());

    GCInfoHeader * pHeader = tempInfo.GetGCInfoHeader();
    UInt32 cbProlog = pHeader->GetPrologSize();
    UInt32 codeOffset = funcletStart + cbProlog;
#ifdef _ARM_
    codeOffset &= ~1;
#endif

    *pRemappedCodeOffset = codeOffset;
}

void Module::RemapHardwareFaultToGCSafePoint(MethodInfo * pMethodInfo, UInt32 * pCodeOffset)
{
    EEMethodInfo * pInfo = GetEEMethodInfo(pMethodInfo);

    EHEnumState ehEnum;
    PTR_VOID pMethodStartAddress;
    if (!EHEnumInit(pMethodInfo, &pMethodStartAddress, &ehEnum))
        return;

    EHClause ehClause;
    while (EHEnumNext(&ehEnum, &ehClause))
    {
        if ((ehClause.m_tryStartOffset <= *pCodeOffset) && (*pCodeOffset < ehClause.m_tryEndOffset))
        {
            UpdateStateForRemappedGCSafePoint(this, pInfo, ehClause.m_handlerOffset, pCodeOffset);
            return;
        }
    }

    // We didn't find a try region covering our PC.  However, if the PC is in a funclet, we must do more work.
    GCInfoHeader * pThisFuncletUnwindInfo = pInfo->GetGCInfoHeader();
    if (!pThisFuncletUnwindInfo->IsFunclet())
        return;

    // For funclets, we must correlate the funclet to its corresponding try region and check for enclosing try
    // regions that might catch the exception as it "escapes" the funclet.

    UInt32 thisFuncletOffset = pThisFuncletUnwindInfo->GetFuncletOffset();

    UInt32 tryRegionStart = 0;
    UInt32 tryRegionEnd = 0;
    bool foundTryRegion = false;

    EHEnumInit(pMethodInfo, &pMethodStartAddress, &ehEnum);

    while (EHEnumNext(&ehEnum, &ehClause))
    {
        if (foundTryRegion && (ehClause.m_tryStartOffset <= tryRegionStart) && (tryRegionEnd <= ehClause.m_tryEndOffset))
        {
            // the regions aren't nested if they have exactly the same range.
            if ((ehClause.m_tryStartOffset != tryRegionStart) || (tryRegionEnd != ehClause.m_tryEndOffset))
            {
                UpdateStateForRemappedGCSafePoint(this, pInfo, ehClause.m_handlerOffset, pCodeOffset);
                return;
            }
        }

        if (ehClause.m_handlerOffset == thisFuncletOffset)
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
}

#ifndef DACCESS_COMPILE

#ifdef FEATURE_VSD

IndirectionCell * Module::GetIndirectionCellArray()
{
    return (IndirectionCell*)m_pModuleHeader->GetVSDIndirectionCells();
}

UInt32 Module::GetIndirectionCellArrayCount()
{
    return m_pModuleHeader->CountVSDIndirectionCells;
}

VSDInterfaceTargetInfo * Module::GetInterfaceTargetInfoArray()
{
    return (VSDInterfaceTargetInfo*)m_pModuleHeader->GetVSDInterfaceTargetInfos();
}

#endif // FEATURE_VSD

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

// Return the classlib-defined GetRuntimeException helper. Returns NULL if this is not a classlib module, or
// if this classlib module fails to export the helper.
void * Module::GetClasslibRuntimeExceptionHelper()
{
    return m_pModuleHeader->Get_GetRuntimeException();
}

// Return the classlib-defined FailFast helper. Returns NULL if this is not a classlib module, or
// if this classlib module fails to export the helper.
void * Module::GetClasslibFailFastHelper()
{
    return m_pModuleHeader->Get_FailFast();
}

void * Module::GetClasslibUnhandledExceptionHandlerHelper()
{
    return m_pModuleHeader->Get_UnhandledExceptionHandler();
}

void * Module::GetClasslibAppendExceptionStackFrameHelper()
{
    return m_pModuleHeader->Get_AppendExceptionStackFrame();
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

// Remove from the system any generic instantiations published by this module and not required by any other
// module currently loaded.
void Module::UnregisterGenericInstances()
{
    RuntimeInstance *pRuntimeInstance = GetRuntimeInstance();

    // There can be up to three segments of GenericInstanceDescs, separated to improve locality.
    const UInt32 cInstSections = 3;
    GenericInstanceDesc * rgInstPointers[cInstSections];
    UInt32 rgInstCounts[cInstSections];
    rgInstPointers[0] = (GenericInstanceDesc*)m_pModuleHeader->GetGenericInstances();
    rgInstCounts[0] = m_pModuleHeader->CountGenericInstances;
    rgInstPointers[1] = (GenericInstanceDesc*)m_pModuleHeader->GetGcRootGenericInstances();
    rgInstCounts[1] = m_pModuleHeader->CountGcRootGenericInstances;
    rgInstPointers[2] = (GenericInstanceDesc*)m_pModuleHeader->GetVariantGenericInstances();
    rgInstCounts[2] = m_pModuleHeader->CountVariantGenericInstances;

    for (UInt32 idxSection = 0; idxSection < cInstSections; idxSection++)
    {
        GenericInstanceDesc *pGid = rgInstPointers[idxSection];

        for (UInt32 i = 0; i < rgInstCounts[idxSection]; i++)
        {
            // Skip GIDs without an instantiation, they're just padding used to avoid base relocs straddling
            // page boundaries (which is bad for perf). They're also not included in the GID count, so adjust
            // that as we see them.
            if (pGid->HasInstantiation())
                pRuntimeInstance->ReleaseGenericInstance(pGid);
            else
            {
                ASSERT(pGid->GetFlags() == GenericInstanceDesc::GID_NoFields);
                rgInstCounts[idxSection]++;
            }

            pGid = (GenericInstanceDesc *)((UInt8*)pGid + pGid->GetSize());
        }
    }
}

bool Module::RegisterGenericInstances()
{
    bool fSuccess = true;

    RuntimeInstance *runtimeInstance = GetRuntimeInstance();

    // There can be up to three segments of GenericInstanceDescs, separated to improve locality.
    const UInt32 cInstSections = 3;
    GenericInstanceDesc * rgInstPointers[cInstSections];
    UInt32 rgInstCounts[cInstSections];
    rgInstPointers[0] = (GenericInstanceDesc*)m_pModuleHeader->GetGenericInstances();
    rgInstCounts[0] = m_pModuleHeader->CountGenericInstances;
    rgInstPointers[1] = (GenericInstanceDesc*)m_pModuleHeader->GetGcRootGenericInstances();
    rgInstCounts[1] = m_pModuleHeader->CountGcRootGenericInstances;
    rgInstPointers[2] = (GenericInstanceDesc*)m_pModuleHeader->GetVariantGenericInstances();
    rgInstCounts[2] = m_pModuleHeader->CountVariantGenericInstances;

    // Registering generic instances with the runtime is performed as a transaction. This allows for some
    // efficiencies (for instance, no need to continually retake hash table locks around each unification).
    if (!runtimeInstance->StartGenericUnification(rgInstCounts[0] + rgInstCounts[1] + rgInstCounts[2]))
        return false;

    UInt32 uiLocalTlsIndex = m_pModuleHeader->PointerToTlsIndex ? *m_pModuleHeader->PointerToTlsIndex : TLS_OUT_OF_INDEXES;

    for (UInt32 idxSection = 0; idxSection < cInstSections; idxSection++)
    {
        GenericInstanceDesc *pGid = rgInstPointers[idxSection];

        for (UInt32 i = 0; i < rgInstCounts[idxSection]; i++)
        {
            // We can get padding GenericInstanceDescs every so often that are inserted to ensure none of the
            // base relocs associated with a GID straddle a page boundary (which is very inefficient). These
            // don't have instantiations. They're also not included in the GID count, so adjust that as we see
            // them.
            if (pGid->HasInstantiation())
            {
                if (!runtimeInstance->UnifyGenericInstance(pGid, uiLocalTlsIndex))
                {
                    fSuccess = false;
                    goto Finished;
                }
            }
            else
            {
                ASSERT(pGid->GetFlags() == GenericInstanceDesc::GID_NoFields);
                rgInstCounts[idxSection]++;
            }

            pGid = (GenericInstanceDesc *)((UInt8*)pGid + pGid->GetSize());
        }
    }

  Finished:
    runtimeInstance->EndGenericUnification();

    return fSuccess;
}


// Returns true if this module is part of the OS module specified by hOsHandle.
bool Module::IsContainedBy(HANDLE hOsHandle)
{
    return m_hOsModuleHandle == hOsHandle;
}

// NULL out any GC references held by statics in this module. Note that this is unsafe unless we know that no
// code is making (or can make) any reference to these statics. Generally this is only true when we are about
// to unload the module.
void Module::ClearStaticRoots()
{
    StaticGcDesc * pStaticGcInfo = (StaticGcDesc*)m_pModuleHeader->GetStaticsGCInfo();
    if (!pStaticGcInfo)
        return;

    UInt8 * pGcStaticsSection = m_pModuleHeader->GetStaticsGCDataSection();

    for (UInt32 idxSeries = 0; idxSeries < pStaticGcInfo->m_numSeries; idxSeries++)
    {
        StaticGcDesc::GCSeries * pSeries = &pStaticGcInfo->m_series[idxSeries];

        Object **   pRefLocation = (Object**)(pGcStaticsSection + pSeries->m_startOffset);
        UInt32      numObjects = pSeries->m_size / sizeof(Object*);

        for (UInt32 idxObj = 0; idxObj < numObjects; idxObj++)
            pRefLocation[idxObj] = NULL;
    }
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

#ifdef TARGET_ARM
    // on ARM, there is just one redir stub, because we can compute the indir cell index 
    // from the indir cell pointer left in r12
    // to make the range tests below work, bump up the end by one byte
    ASSERT(pvRedirStubsStart == pvRedirStubsEnd);
    pvRedirStubsEnd = (void *)(((UInt8 *)pvRedirStubsEnd)+1);
#endif // TARGET_ARM


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

EXTERN_C void * FASTCALL RecoverLoopHijackTarget(UInt32 entryIndex, ModuleHeader * pModuleHeader)
{
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

    return pModuleHeader->RegionPtr[ModuleHeader::TEXT_REGION] + targetOffset;;
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

Module::GenericInstanceDescEnumerator::GenericInstanceDescEnumerator(Module * pModule, GenericInstanceDescKind gidKind)
    : m_pModule(pModule), m_pCurrent(NULL), m_gidEnumKind(gidKind), m_iCurrent(0), m_nCount(0), m_iSection(0)
{
}

GenericInstanceDesc * Module::GenericInstanceDescEnumerator::Next()
{
    m_iCurrent++;

    if (m_iCurrent >= m_nCount)
    {
        ModuleHeader * pModuleHeader = m_pModule->m_pModuleHeader;
        m_nCount = 0;

        for (;;)
        {
            // There can be up to three segments of GenericInstanceDescs, separated to improve locality.
            switch (m_iSection)
            {
            case 0:
                if ((m_gidEnumKind & GenericInstanceDescKind::GenericInstances) != 0)
                {
                    m_pCurrent = (GenericInstanceDesc*)pModuleHeader->GetGenericInstances();
                    m_nCount = pModuleHeader->CountGenericInstances;
                }
                break;
            case 1:
                if ((m_gidEnumKind & GenericInstanceDescKind::GcRootGenericInstances) != 0)
                {
                    m_pCurrent = (GenericInstanceDesc*)pModuleHeader->GetGcRootGenericInstances();
                    m_nCount = pModuleHeader->CountGcRootGenericInstances;
                }
                break;
            case 2:
                if ((m_gidEnumKind & GenericInstanceDescKind::VariantGenericInstances) != 0)
                {
                    m_pCurrent = (GenericInstanceDesc*)pModuleHeader->GetVariantGenericInstances();
                    m_nCount = pModuleHeader->CountVariantGenericInstances;
                }
                break;
            default:
                return NULL;
            }

            m_iSection++;

            if (m_nCount > 0)
                break;
        }

        m_iCurrent = 0;

        if (m_pCurrent->HasInstantiation())
            return m_pCurrent;
    }

    for (;;)
    {
        m_pCurrent = (GenericInstanceDesc *)((UInt8*)m_pCurrent + m_pCurrent->GetSize());

        if (m_pCurrent->HasInstantiation())
            return m_pCurrent;

        // We can get padding GenericInstanceDescs every so often that are inserted to ensure none of the
        // base relocs associated with a GID straddle a page boundary (which is very inefficient). These
        // don't have instantiations. They're also not included in the GID count.
        ASSERT(m_pCurrent->GetFlags() == GenericInstanceDesc::GID_NoFields);
    }
}

UInt32 Module::GetGenericInstanceDescCount(GenericInstanceDescKind gidKind)
{
    UInt32 count = 0;
    if ((gidKind & GenericInstanceDescKind::GenericInstances) != 0)
        count += m_pModuleHeader->CountGenericInstances;
    if ((gidKind & GenericInstanceDescKind::GcRootGenericInstances) != 0)
        count += m_pModuleHeader->CountGcRootGenericInstances;
    if ((gidKind & GenericInstanceDescKind::VariantGenericInstances) != 0)
        count += m_pModuleHeader->CountVariantGenericInstances;
    return count;
}

#ifdef FEATURE_CUSTOM_IMPORTS

#define IMAGE_ORDINAL_FLAG64 0x8000000000000000
#define IMAGE_ORDINAL_FLAG32 0x80000000

#ifdef TARGET_X64
#define TARGET_IMAGE_ORDINAL_FLAG IMAGE_ORDINAL_FLAG64
#else
#define TARGET_IMAGE_ORDINAL_FLAG IMAGE_ORDINAL_FLAG32
#endif

/*static*/
void Module::DoCustomImports(ModuleHeader * pModuleHeader)
{
    CustomImportDescriptor *customImportTable = (CustomImportDescriptor *)pModuleHeader->GetCustomImportDescriptors();
    UInt32 countCustomImports = pModuleHeader->CountCustomImportDescriptors;

    // obtain base address for this module
    PTR_UInt8 thisBaseAddress = (PTR_UInt8)PalGetModuleHandleFromPointer(pModuleHeader);

    for (UInt32 i = 0; i < countCustomImports; i++)
    {
        // obtain address of indirection cell pointing to the EAT for the exporting module
        UInt32 **ptrPtrEAT = (UInt32 **)(thisBaseAddress + customImportTable[i].RvaEATAddr);

        // obtain the EAT by derefencing
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
            ASSERT((UInt32 *)ptrPtrEAT == pFlag);
            
            // the number of IAT entries should be zero
            ASSERT(countIAT == 0);

            // if the flag is set, it means we have fixed up this module already
            // this is our check against infinite recursion
            if (*pFlag == TRUE)
                return;

            // if the flag is not set, it must be clear
            ASSERT(*pFlag == FALSE);

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
                ASSERT((ordinal & TARGET_IMAGE_ORDINAL_FLAG) != 0);

                // the ordinals should be in increasing order, for perf reasons
                ASSERT(j+1 == countIAT || ordinal < ptrIAT[j+1]);

                ordinal &= ~TARGET_IMAGE_ORDINAL_FLAG;

                // sanity check: limit ordinals to < 1 Million
                ASSERT(ordinal < 1024 * 1024);

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

UInt32 GenericInstanceDesc::DacSize(TADDR addr)
{
    STATIC_ASSERT(offsetof(GenericInstanceDesc, m_Flags) == 0);

    GenericInstanceDesc dummyDesc;
    DacReadAll(addr, &dummyDesc, sizeof(GenericInstanceDesc::OptionalFieldTypes), true);

    UInt32 arity = 0;
    UInt32 arityOffset = dummyDesc.GetArityOffset();
    DacReadAll(addr + arityOffset, &arity, sizeof(UInt32), true);
    return GenericInstanceDesc::GetSize(dummyDesc.GetFlags(), arity);
}
#endif // DACCESS_COMPILE