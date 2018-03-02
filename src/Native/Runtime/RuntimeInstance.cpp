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
#include "Crst.h"
#include "rhbinder.h"
#include "RWLock.h"
#include "RuntimeInstance.h"
#include "event.h"
#include "regdisplay.h"
#include "StackFrameIterator.h"
#include "thread.h"
#include "threadstore.h"
#include "threadstore.inl"
#include "gcrhinterface.h"
#include "shash.h"
#include "module.h"
#include "eetype.h"
#include "varint.h"
#include "DebugEventSource.h"
#include "GenericUnification.h"

#include "CommonMacros.inl"
#include "slist.inl"
#include "eetype.inl"
#include "OptionalFields.inl"

#ifdef  FEATURE_GC_STRESS
enum HijackType { htLoop, htCallsite };
bool ShouldHijackForGcStress(UIntNative CallsiteIP, HijackType ht);
#endif // FEATURE_GC_STRESS

#include "shash.inl"

#ifndef DACCESS_COMPILE
COOP_PINVOKE_HELPER(UInt8 *, RhSetErrorInfoBuffer, (UInt8 * pNewBuffer))
{
    return (UInt8 *) PalSetWerDataBuffer(pNewBuffer);
}
#endif // DACCESS_COMPILE

RuntimeInstance::ModuleIterator::ModuleIterator() : 
    m_readHolder(&GetRuntimeInstance()->m_ModuleListLock),
    m_pCurrentPosition(GetRuntimeInstance()->GetModuleList()->GetHead())
{
}

SList<Module>* RuntimeInstance::GetModuleList()
{
    return dac_cast<DPTR(SList<Module>)>( dac_cast<TADDR>(this) + offsetof(RuntimeInstance, m_ModuleList));
}

RuntimeInstance::ModuleIterator::~ModuleIterator()
{
}

PTR_Module RuntimeInstance::ModuleIterator::GetNext()
{
    PTR_Module pResult = m_pCurrentPosition;
    if (NULL != pResult)
        m_pCurrentPosition = pResult->m_pNext;
    return pResult;
}


ThreadStore *   RuntimeInstance::GetThreadStore()
{
    return m_pThreadStore;
}

Module * RuntimeInstance::FindModuleByAddress(PTR_VOID pvAddress)
{
    FOREACH_MODULE(pModule)
    {
        if (pModule->ContainsCodeAddress(pvAddress) ||
            pModule->ContainsDataAddress(pvAddress) ||
            pModule->ContainsReadOnlyDataAddress(pvAddress) ||
            pModule->ContainsStubAddress(pvAddress))
        {
            return pModule;
        }
    }
    END_FOREACH_MODULE;

    return NULL;
}

Module * RuntimeInstance::FindModuleByCodeAddress(PTR_VOID pvAddress)
{
    FOREACH_MODULE(pModule)
    {
        if (pModule->ContainsCodeAddress(pvAddress))
            return pModule;
    }
    END_FOREACH_MODULE;

    return NULL;
}

Module * RuntimeInstance::FindModuleByDataAddress(PTR_VOID pvAddress)
{
    FOREACH_MODULE(pModule)
    {
        if (pModule->ContainsDataAddress(pvAddress))
            return pModule;
    }
    END_FOREACH_MODULE;

    return NULL;
}

Module * RuntimeInstance::FindModuleByReadOnlyDataAddress(PTR_VOID pvAddress)
{
    FOREACH_MODULE(pModule)
    {
        if (pModule->ContainsReadOnlyDataAddress(pvAddress))
            return pModule;
    }
    END_FOREACH_MODULE;

    return NULL;
}

void RuntimeInstance::EnumerateModulesUnderLock(EnumerateModulesCallbackPFN pCallback, void *pvContext)
{
    ASSERT(pCallback != NULL);

    FOREACH_MODULE(pModule)
    {
        (*pCallback)(pModule, pvContext);
    }
    END_FOREACH_MODULE;
}

COOP_PINVOKE_HELPER(UInt8 *, RhFindMethodStartAddress, (void * codeAddr))
{
    return dac_cast<UInt8 *>(GetRuntimeInstance()->FindMethodStartAddress(dac_cast<PTR_VOID>(codeAddr)));
}

PTR_UInt8 RuntimeInstance::FindMethodStartAddress(PTR_VOID ControlPC)
{
    ICodeManager * pCodeManager = FindCodeManagerByAddress(ControlPC);
    MethodInfo methodInfo;
    if (pCodeManager != NULL && pCodeManager->FindMethodInfo(ControlPC, &methodInfo))
    {
        return (PTR_UInt8)pCodeManager->GetMethodStartAddress(&methodInfo);
    }

    return NULL;
}

ICodeManager * RuntimeInstance::FindCodeManagerByAddress(PTR_VOID pvAddress)
{
    ReaderWriterLock::ReadHolder read(&m_ModuleListLock);

    for (Module * pModule = m_ModuleList.GetHead(); pModule != NULL; pModule = pModule->m_pNext)
    {
        if (pModule->ContainsCodeAddress(pvAddress))
            return pModule;
    }

    // TODO: ICodeManager support in DAC
#ifndef DACCESS_COMPILE
    for (CodeManagerEntry * pEntry = m_CodeManagerList.GetHead(); pEntry != NULL; pEntry = pEntry->m_pNext)
    {
        if (dac_cast<TADDR>(pvAddress) - dac_cast<TADDR>(pEntry->m_pvStartRange) < pEntry->m_cbRange)
            return pEntry->m_pCodeManager;
    }
#endif

    return NULL;
}

#ifndef DACCESS_COMPILE

// Find the code manager containing the given address, which might be a return address from a managed function. The
// address may be to another managed function, or it may be to an unmanaged function. The address may also refer to 
// an EEType.
ICodeManager * RuntimeInstance::FindCodeManagerForClasslibFunction(PTR_VOID address)
{
    // Try looking up the code manager assuming the address is for code first. This is expected to be most common.
    ICodeManager * pCodeManager = FindCodeManagerByAddress(address);
    if (pCodeManager != NULL)
        return pCodeManager;

    // Less common, we will look for the address in any of the sections of the module.  This is slower, but is 
    // necessary for EEType pointers and jump stubs.
    Module * pModule = FindModuleByAddress(address);
    if (pModule != NULL)
        return pModule;

    ASSERT_MSG(!Thread::IsHijackTarget(address), "not expected to be called with hijacked return address");

    return NULL;
}

void * RuntimeInstance::GetClasslibFunctionFromCodeAddress(PTR_VOID address, ClasslibFunctionId functionId)
{
    // Find the code manager for the given address, which is an address into some managed module. It could
    // be code, or it could be an EEType. No matter what, it's an address into a managed module in some non-Rtm
    // type system.
    ICodeManager * pCodeManager = FindCodeManagerForClasslibFunction(address);

    // If the address isn't in a managed module then we have no classlib function.
    if (pCodeManager == NULL)
    {
        return NULL;
    }

    return pCodeManager->GetClasslibFunction(functionId);
}

#endif // DACCESS_COMPILE

PTR_UInt8 RuntimeInstance::GetTargetOfUnboxingAndInstantiatingStub(PTR_VOID ControlPC)
{
    ICodeManager * pCodeManager = FindCodeManagerByAddress(ControlPC);
    if (pCodeManager != NULL)
    {
        PTR_UInt8 pData = (PTR_UInt8)pCodeManager->GetAssociatedData(ControlPC);
        if (pData != NULL)
        {
            UInt8 flags = *pData++;

            if ((flags & (UInt8)AssociatedDataFlags::HasUnboxingStubTarget) != 0)
                return pData + *dac_cast<PTR_Int32>(pData);
        }
    }

    return NULL;
}

GPTR_IMPL_INIT(RuntimeInstance, g_pTheRuntimeInstance, NULL);

PTR_RuntimeInstance GetRuntimeInstance()
{
    return g_pTheRuntimeInstance;
}

void RuntimeInstance::EnumStaticGCRefDescs(void * pfnCallback, void * pvCallbackData)
{
    for (StaticGCRefsDescChunk *pChunk = m_pStaticGCRefsDescChunkList; pChunk != nullptr; pChunk = pChunk->m_pNextChunk)
    {
        UInt32 uiDescCount = pChunk->m_uiDescCount;
        for (UInt32 i = 0; i < uiDescCount; i++)
        {
            Module::EnumStaticGCRefsBlock(pfnCallback, pvCallbackData, 
                pChunk->m_rgDesc[i].m_pStaticGcInfo, pChunk->m_rgDesc[i].m_pbStaticData);
        }
    }
}

void RuntimeInstance::EnumThreadStaticGCRefDescs(void * pfnCallback, void * pvCallbackData)
{
    for (ThreadStaticGCRefsDescChunk *pChunk = m_pThreadStaticGCRefsDescChunkList; pChunk != nullptr; pChunk = pChunk->m_pNextChunk)
    {
        UInt32 uiDescCount = pChunk->m_uiDescCount;
        for (UInt32 i = 0; i < uiDescCount; i++)
        {
            UInt32              uiFieldsStartOffset = pChunk->m_rgDesc[i].m_uiFieldStartOffset;
            PTR_StaticGcDesc    pThreadStaticGcInfo = pChunk->m_rgDesc[i].m_pThreadStaticGcInfo;

            // Special case for dynamic types: TLS storage managed manually by runtime
            if (uiFieldsStartOffset & DYNAMIC_TYPE_TLS_OFFSET_FLAG)
            {
                FOREACH_THREAD(pThread)
                {
                    PTR_UInt8 pTLSStorage = pThread->GetThreadLocalStorageForDynamicType(uiFieldsStartOffset);
                    if (pTLSStorage != NULL)
                    {
                        Module::EnumStaticGCRefsBlock(pfnCallback, pvCallbackData, pThreadStaticGcInfo, pTLSStorage);
                    }
                }
                END_FOREACH_THREAD
            }
            else
            {
                // See RhGetThreadStaticFieldAddress for details on where TLS fields live.

                UInt32 uiTlsIndex = pChunk->m_rgDesc[i].m_uiTlsIndex;
                FOREACH_THREAD(pThread)
                {
                    Module::EnumStaticGCRefsBlock(pfnCallback, pvCallbackData,
                                                  pThreadStaticGcInfo,
                                                  pThread->GetThreadLocalStorage(uiTlsIndex, uiFieldsStartOffset));
                }
                END_FOREACH_THREAD
            }
        }
    }
}

void RuntimeInstance::EnumAllStaticGCRefs(void * pfnCallback, void * pvCallbackData)
{
    FOREACH_MODULE(pModule)
    {
        pModule->EnumStaticGCRefs(pfnCallback, pvCallbackData);
    }
    END_FOREACH_MODULE

    for (TypeManagerList::Iterator iter = m_TypeManagerList.Begin(); iter != m_TypeManagerList.End(); iter++)
    {
        iter->m_pTypeManager->EnumStaticGCRefs(pfnCallback, pvCallbackData);
    }

    EnumStaticGCRefDescs(pfnCallback, pvCallbackData);
    EnumThreadStaticGCRefDescs(pfnCallback, pvCallbackData);
}

void RuntimeInstance::SetLoopHijackFlags(UInt32 flag)
{
    for (TypeManagerList::Iterator iter = m_TypeManagerList.Begin(); iter != m_TypeManagerList.End(); iter++)
    {
        iter->m_pTypeManager->SetLoopHijackFlag(flag);
    }
}

RuntimeInstance::OsModuleList* RuntimeInstance::GetOsModuleList()
{
    return dac_cast<DPTR(OsModuleList)>(dac_cast<TADDR>(this) + offsetof(RuntimeInstance, m_OsModuleList));
}

ReaderWriterLock& RuntimeInstance::GetTypeManagerLock()
{
    return m_ModuleListLock;
}

#ifndef DACCESS_COMPILE

Module * RuntimeInstance::FindModuleByOsHandle(HANDLE hOsHandle)
{
    FOREACH_MODULE(pModule)
    {
        if (pModule->IsContainedBy(hOsHandle))
            return pModule;
    }
    END_FOREACH_MODULE;

    return NULL;
}

RuntimeInstance::RuntimeInstance() : 
    m_pThreadStore(NULL),
    m_pStaticGCRefsDescChunkList(NULL),
    m_pThreadStaticGCRefsDescChunkList(NULL),
    m_pGenericUnificationHashtable(NULL),
    m_conservativeStackReportingEnabled(false),
    m_pUnboxingStubsRegion(NULL)
{
}

RuntimeInstance::~RuntimeInstance()
{
    if (NULL != m_pThreadStore)
    {
        delete m_pThreadStore;
        m_pThreadStore = NULL;
    }
}

HANDLE  RuntimeInstance::GetPalInstance()
{
    return m_hPalInstance;
}

void RuntimeInstance::EnableConservativeStackReporting()
{
    m_conservativeStackReportingEnabled = true;
}

EXTERN_C void REDHAWK_CALLCONV RhpSetHaveNewClasslibs();

bool RuntimeInstance::RegisterModule(ModuleHeader *pModuleHeader)
{
    CreateHolder<Module> pModule = Module::Create(pModuleHeader);

    if (NULL == pModule)
        return false;

    {
        // WARNING: This region must be kept small and must not callout 
        // to arbitrary code.  See Thread::Hijack for more details.
        ReaderWriterLock::WriteHolder write(&m_ModuleListLock);
        m_ModuleList.PushHead(pModule);
    }

    if (pModule->IsClasslibModule())
        RhpSetHaveNewClasslibs();

#ifdef FEATURE_PROFILING
    InitProfiling(pModuleHeader);
#endif // FEATURE_PROFILING

    pModule.SuppressRelease();
    // This event must occur after the module is added to the enumeration
    DebugEventSource::SendModuleLoadEvent(pModule);
    return true;
}

void RuntimeInstance::UnregisterModule(Module *pModule)
{
    {
        // WARNING: This region must be kept small and must not callout 
        // to arbitrary code.  See Thread::Hijack for more details.
        ReaderWriterLock::WriteHolder write(&m_ModuleListLock);
        ASSERT(rh::std::count(m_ModuleList.Begin(), m_ModuleList.End(), pModule) == 1);
        m_ModuleList.RemoveFirst(pModule);
    }

    // This event needs to occur after the module has been removed from enumeration.
    // However it should come before the data is destroyed to make certain the pointer doesn't get recycled.
    DebugEventSource::SendModuleUnloadEvent(pModule);

    pModule->Destroy();
}

bool RuntimeInstance::RegisterCodeManager(ICodeManager * pCodeManager, PTR_VOID pvStartRange, UInt32 cbRange)
{
    CodeManagerEntry * pEntry = new (nothrow) CodeManagerEntry();
    if (NULL == pEntry)
        return false;

    pEntry->m_pvStartRange = pvStartRange;
    pEntry->m_cbRange = cbRange;
    pEntry->m_pCodeManager = pCodeManager;

    {
        ReaderWriterLock::WriteHolder write(&m_ModuleListLock);

        m_CodeManagerList.PushHead(pEntry);
    }

    return true;
}

void RuntimeInstance::UnregisterCodeManager(ICodeManager * pCodeManager)
{
    CodeManagerEntry * pEntry = NULL;

    {
        ReaderWriterLock::WriteHolder write(&m_ModuleListLock);

        for (CodeManagerList::Iterator i = m_CodeManagerList.Begin(), end = m_CodeManagerList.End(); i != end; i++)
        {
            if (i->m_pCodeManager == pCodeManager)
            {
                pEntry = *i;

                m_CodeManagerList.Remove(i);
                break;
            }
        }
    }

    ASSERT(pEntry != NULL);
    delete pEntry;
}

extern "C" bool __stdcall RegisterCodeManager(ICodeManager * pCodeManager, PTR_VOID pvStartRange, UInt32 cbRange)
{
    return GetRuntimeInstance()->RegisterCodeManager(pCodeManager, pvStartRange, cbRange);
}

extern "C" void __stdcall UnregisterCodeManager(ICodeManager * pCodeManager)
{
    return GetRuntimeInstance()->UnregisterCodeManager(pCodeManager);
}

bool RuntimeInstance::RegisterUnboxingStubs(PTR_VOID pvStartRange, UInt32 cbRange)
{
    ASSERT(pvStartRange != NULL && cbRange > 0);

    UnboxingStubsRegion * pEntry = new (nothrow) UnboxingStubsRegion();
    if (NULL == pEntry)
        return false;

    pEntry->m_pRegionStart = pvStartRange;
    pEntry->m_cbRegion = cbRange;

    do
    {
        pEntry->m_pNextRegion = m_pUnboxingStubsRegion;
    } 
    while (PalInterlockedCompareExchangePointer((void *volatile *)&m_pUnboxingStubsRegion, pEntry, pEntry->m_pNextRegion) != pEntry->m_pNextRegion);

    return true;
}

bool RuntimeInstance::IsUnboxingStub(UInt8* pCode)
{
    UnboxingStubsRegion * pCurrent = m_pUnboxingStubsRegion;
    while (pCurrent != NULL)
    {
        UInt8* pUnboxingStubsRegion = dac_cast<UInt8*>(pCurrent->m_pRegionStart);
        if (pCode >= pUnboxingStubsRegion && pCode < (pUnboxingStubsRegion + pCurrent->m_cbRegion))
            return true;

        pCurrent = pCurrent->m_pNextRegion;
    }

    return false;
}

extern "C" bool __stdcall RegisterUnboxingStubs(PTR_VOID pvStartRange, UInt32 cbRange)
{
    return GetRuntimeInstance()->RegisterUnboxingStubs(pvStartRange, cbRange);
}

bool RuntimeInstance::RegisterTypeManager(TypeManager * pTypeManager)
{
    TypeManagerEntry * pEntry = new (nothrow) TypeManagerEntry();
    if (NULL == pEntry)
        return false;

    pEntry->m_pTypeManager = pTypeManager;

    {
        ReaderWriterLock::WriteHolder write(&m_ModuleListLock);

        m_TypeManagerList.PushHead(pEntry);
    }

    return true;
}

COOP_PINVOKE_HELPER(TypeManagerHandle, RhpCreateTypeManager, (HANDLE osModule, void* pModuleHeader, PTR_PTR_VOID pClasslibFunctions, UInt32 nClasslibFunctions))
{
    TypeManager * typeManager = TypeManager::Create(osModule, pModuleHeader, pClasslibFunctions, nClasslibFunctions);
    GetRuntimeInstance()->RegisterTypeManager(typeManager);

    // This event must occur after the module is added to the enumeration
    if (osModule != nullptr)
        DebugEventSource::SendModuleLoadEvent(osModule);

    return TypeManagerHandle::Create(typeManager);
}

COOP_PINVOKE_HELPER(HANDLE, RhGetOSModuleForMrt, ())
{
    return GetRuntimeInstance()->GetPalInstance();
}

COOP_PINVOKE_HELPER(void*, RhpRegisterOsModule, (HANDLE hOsModule))
{
    RuntimeInstance::OsModuleEntry * pEntry = new (nothrow) RuntimeInstance::OsModuleEntry();
    if (NULL == pEntry)
        return nullptr; // Return null on failure.

    pEntry->m_osModule = hOsModule;

    {
        RuntimeInstance *pRuntimeInstance = GetRuntimeInstance();
        ReaderWriterLock::WriteHolder write(&pRuntimeInstance->GetTypeManagerLock());

        pRuntimeInstance->GetOsModuleList()->PushHead(pEntry);
    }

    return hOsModule; // Return non-null on success
}

RuntimeInstance::TypeManagerList& RuntimeInstance::GetTypeManagerList() 
{
    return m_TypeManagerList;
}

// static 
bool RuntimeInstance::Initialize(HANDLE hPalInstance)
{
    NewHolder<RuntimeInstance> pRuntimeInstance = new (nothrow) RuntimeInstance();
    if (NULL == pRuntimeInstance)
        return false;

    CreateHolder<ThreadStore>  pThreadStore = ThreadStore::Create(pRuntimeInstance);
    if (NULL == pThreadStore)
        return false;

    pThreadStore.SuppressRelease();
    pRuntimeInstance.SuppressRelease();

    pRuntimeInstance->m_pThreadStore = pThreadStore;
    pRuntimeInstance->m_hPalInstance = hPalInstance;

#ifdef FEATURE_PROFILING
    pRuntimeInstance->m_fProfileThreadCreated = false;
#endif

    ASSERT_MSG(g_pTheRuntimeInstance == NULL, "multi-instances are not supported");
    g_pTheRuntimeInstance = pRuntimeInstance;

    return true;
}

void RuntimeInstance::Destroy()
{
    delete this;
}

bool RuntimeInstance::ShouldHijackLoopForGcStress(UIntNative CallsiteIP)
{
#ifdef FEATURE_GC_STRESS
    return ShouldHijackForGcStress(CallsiteIP, htLoop);
#else // FEATURE_GC_STRESS
    UNREFERENCED_PARAMETER(CallsiteIP);
    return false;
#endif // FEATURE_GC_STRESS
}

bool RuntimeInstance::ShouldHijackCallsiteForGcStress(UIntNative CallsiteIP)
{
#ifdef FEATURE_GC_STRESS
    return ShouldHijackForGcStress(CallsiteIP, htCallsite);
#else // FEATURE_GC_STRESS
    UNREFERENCED_PARAMETER(CallsiteIP);
    return false;
#endif // FEATURE_GC_STRESS
}

// This method should only be called during DllMain for modules with GcStress enabled.  The locking done by 
// the loader is used to make it OK to call UnsynchronizedHijackAllLoops.
void RuntimeInstance::EnableGcPollStress()
{
    FOREACH_MODULE(pModule)
    {
        pModule->UnsynchronizedHijackAllLoops();
    }
    END_FOREACH_MODULE;
}

// Only called from thread suspension code while all threads are still synchronized.
void RuntimeInstance::UnsychronizedResetHijackedLoops()
{
    FOREACH_MODULE(pModule)
    {
        pModule->UnsynchronizedResetHijackedLoops();
    }
    END_FOREACH_MODULE;
}

bool RuntimeInstance::AddDynamicGcStatics(UInt8 *pGcStaticData, StaticGcDesc *pGcStaticsDesc)
{
    ReaderWriterLock::WriteHolder write(&m_StaticGCRefLock);

    StaticGCRefsDescChunk *pChunk = m_pStaticGCRefsDescChunkList;
    if (pChunk == NULL || pChunk->m_uiDescCount >= StaticGCRefsDescChunk::MAX_DESC_COUNT)
    {
        pChunk = new (nothrow) StaticGCRefsDescChunk();
        if (pChunk == NULL)
            return false;
        pChunk->m_pNextChunk = m_pStaticGCRefsDescChunkList;
        m_pStaticGCRefsDescChunkList = pChunk;
    }
    UInt32 uiDescCount = pChunk->m_uiDescCount++;
    pChunk->m_rgDesc[uiDescCount].m_pStaticGcInfo = pGcStaticsDesc;
    pChunk->m_rgDesc[uiDescCount].m_pbStaticData = pGcStaticData;

    return true;
}

bool RuntimeInstance::AddDynamicThreadStaticGcData(UInt32 uiTlsIndex, UInt32 uiThreadStaticOffset, StaticGcDesc *pThreadStaticsDesc)
{
    ReaderWriterLock::WriteHolder write(&m_StaticGCRefLock);

    ThreadStaticGCRefsDescChunk *pChunk = m_pThreadStaticGCRefsDescChunkList;
    if (pChunk == NULL || pChunk->m_uiDescCount >= ThreadStaticGCRefsDescChunk::MAX_DESC_COUNT)
    {
        pChunk = new (nothrow) ThreadStaticGCRefsDescChunk();
        if (pChunk == NULL)
            return false;
        pChunk->m_pNextChunk = m_pThreadStaticGCRefsDescChunkList;
        m_pThreadStaticGCRefsDescChunkList = pChunk;
    }
    UInt32 uiDescCount = pChunk->m_uiDescCount++;
    pChunk->m_rgDesc[uiDescCount].m_pThreadStaticGcInfo = pThreadStaticsDesc;
    pChunk->m_rgDesc[uiDescCount].m_uiTlsIndex = uiTlsIndex;
    pChunk->m_rgDesc[uiDescCount].m_uiFieldStartOffset = uiThreadStaticOffset;

    return true;
}

bool RuntimeInstance::CreateGenericAndStaticInfo(EEType *             pEEType,
                                                 EEType *             pTemplateType,
                                                 UInt32               arity,
                                                 UInt32               nonGcStaticDataSize,
                                                 UInt32               nonGCStaticDataOffset,
                                                 UInt32               gcStaticDataSize,
                                                 UInt32               threadStaticOffset,
                                                 StaticGcDesc *       pGcStaticsDesc,
                                                 StaticGcDesc *       pThreadStaticsDesc,
                                                 UInt32*              pGenericVarianceFlags)
{
    NewArrayHolder<UInt8> pGenericCompositionMemory;
    if (arity != 0)
    {
        // Note: arity is limited to a maximum value of 65535 on the managed layer
        ASSERT(arity == (UInt16)arity);
        assert(pEEType->IsGeneric());

        // prepare generic composition
        size_t cbGenericCompositionSize = GenericComposition::GetSize((UInt16)arity, pTemplateType->HasGenericVariance());
        pGenericCompositionMemory = new (nothrow) UInt8[cbGenericCompositionSize];
        if (pGenericCompositionMemory == NULL)
            return false;

        GenericComposition *pGenericComposition = (GenericComposition *)(UInt8 *)pGenericCompositionMemory;
        pGenericComposition->Init((UInt16)arity, pTemplateType->HasGenericVariance());

        // fill in variance flags
        if (pTemplateType->HasGenericVariance())
        {
            ASSERT(pEEType->HasGenericVariance() && pGenericVarianceFlags != NULL);

            for (UInt32 i = 0; i < arity; i++)
            {
                GenericVarianceType variance = (GenericVarianceType)pGenericVarianceFlags[i];
                pGenericComposition->SetVariance(i, variance);
            }
        }
        pEEType->set_GenericComposition(pGenericComposition);
    }

    NewArrayHolder<UInt8> pNonGcStaticData;
    if (nonGcStaticDataSize > 0)
    {
        // The value of nonGcStaticDataSize is read from native layout info in the managed layer, where
        // there is also a check that it does not exceed the max value of a signed Int32
        ASSERT(nonGCStaticDataOffset <= nonGcStaticDataSize);
        pNonGcStaticData = new (nothrow) UInt8[nonGcStaticDataSize];
        if (pNonGcStaticData == NULL)
            return false;
        memset(pNonGcStaticData, 0, nonGcStaticDataSize);
        pEEType->set_DynamicNonGcStatics(pNonGcStaticData + nonGCStaticDataOffset);
    }

    NewArrayHolder<UInt8> pGcStaticData;
#ifdef PROJECTN
    if (gcStaticDataSize > 0)
    {
        // The value of gcStaticDataSize is read from native layout info in the managed layer, where
        // there is also a check that it does not exceed the max value of a signed Int32
        pGcStaticData = new (nothrow) UInt8[gcStaticDataSize];
        if (pGcStaticData == NULL)
            return false;
        memset(pGcStaticData, 0, gcStaticDataSize);
        pEEType->set_DynamicGcStatics(pGcStaticData);
        if (!AddDynamicGcStatics(pGcStaticData, pGcStaticsDesc))
            return false;
    }
#endif

    if (threadStaticOffset != 0)
    {
        // Note: TLS index not used for dynamically created types
        pEEType->set_DynamicThreadStaticOffset(threadStaticOffset);
        if (pThreadStaticsDesc != NULL)
        {
            if (!AddDynamicThreadStaticGcData(0, threadStaticOffset, pThreadStaticsDesc))
                return false;
        }
    }

    pGenericCompositionMemory.SuppressRelease();
    pNonGcStaticData.SuppressRelease();
    pGcStaticData.SuppressRelease();
    return true;
}

bool RuntimeInstance::UnifyGenerics(GenericUnificationDesc *descs, UInt32 descCount, void **pIndirCells, UInt32 indirCellCount)
{
    if (m_pGenericUnificationHashtable == nullptr)
    {
        m_pGenericUnificationHashtable = new GenericUnificationHashtable();
        if (m_pGenericUnificationHashtable == nullptr)
            return false;
    }

    return m_pGenericUnificationHashtable->UnifyDescs(descs, descCount, (UIntTarget*)pIndirCells, indirCellCount);
}

COOP_PINVOKE_HELPER(bool, RhCreateGenericInstanceDescForType2, (EEType *        pEEType,
                                                                UInt32          arity,
                                                                UInt32          nonGcStaticDataSize,
                                                                UInt32          nonGCStaticDataOffset,
                                                                UInt32          gcStaticDataSize,
                                                                UInt32          threadStaticsOffset,
                                                                StaticGcDesc *  pGcStaticsDesc,
                                                                StaticGcDesc *  pThreadStaticsDesc,
                                                                UInt32*         pGenericVarianceFlags))
{
    ASSERT(pEEType->IsDynamicType());

    EEType * pTemplateType = pEEType->get_DynamicTemplateType();

    return GetRuntimeInstance()->CreateGenericAndStaticInfo(pEEType, pTemplateType, arity, nonGcStaticDataSize, nonGCStaticDataOffset, gcStaticDataSize,
        threadStaticsOffset, pGcStaticsDesc, pThreadStaticsDesc, pGenericVarianceFlags);
}

COOP_PINVOKE_HELPER(UInt32, RhGetGCDescSize, (EEType* pEEType))
{
    return RedhawkGCInterface::GetGCDescSize(pEEType);
}


// Keep in sync with ndp\fxcore\src\System.Private.CoreLib\system\runtime\runtimeimports.cs
enum RuntimeHelperKind
{
    AllocateObject,
    IsInst,
    CastClass,
    AllocateArray,
    CheckArrayElementType,
};

// The dictionary codegen expects a pointer that points at a memory location that points to the method pointer
// Create indirections for all helpers used below

#define DECLARE_INDIRECTION(HELPER_NAME) \
    EXTERN_C void * HELPER_NAME; \
    const PTR_VOID indirection_##HELPER_NAME = (PTR_VOID)&HELPER_NAME

#define INDIRECTION(HELPER_NAME) ((PTR_VOID)&indirection_##HELPER_NAME)

DECLARE_INDIRECTION(RhpNewFast);
DECLARE_INDIRECTION(RhpNewFinalizable);

DECLARE_INDIRECTION(RhpNewArray);

DECLARE_INDIRECTION(RhTypeCast_IsInstanceOfClass);
DECLARE_INDIRECTION(RhTypeCast_CheckCastClass);
DECLARE_INDIRECTION(RhTypeCast_IsInstanceOfArray);
DECLARE_INDIRECTION(RhTypeCast_CheckCastArray);
DECLARE_INDIRECTION(RhTypeCast_IsInstanceOfInterface);
DECLARE_INDIRECTION(RhTypeCast_CheckCastInterface);

DECLARE_INDIRECTION(RhTypeCast_CheckVectorElemAddr);

#ifdef _ARM_
DECLARE_INDIRECTION(RhpNewFinalizableAlign8);
DECLARE_INDIRECTION(RhpNewFastMisalign);
DECLARE_INDIRECTION(RhpNewFastAlign8);

DECLARE_INDIRECTION(RhpNewArrayAlign8);
#endif

COOP_PINVOKE_HELPER(PTR_VOID, RhGetRuntimeHelperForType, (EEType * pEEType, int helperKind))
{
    // This implementation matches what the binder does (MetaDataEngine::*() in rh\src\tools\rhbind\MetaDataEngine.cpp)
    // If you change the binder's behavior, change this implementation too

    switch (helperKind)
    {
    case RuntimeHelperKind::AllocateObject:
#ifdef _ARM_
        if ((pEEType->get_RareFlags() & EEType::RareFlags::RequiresAlign8Flag) == EEType::RareFlags::RequiresAlign8Flag)
        {
            if (pEEType->HasFinalizer())
                return INDIRECTION(RhpNewFinalizableAlign8);
            else if (pEEType->get_IsValueType())            // returns true for enum types as well
                return INDIRECTION(RhpNewFastMisalign);
            else
                return INDIRECTION(RhpNewFastAlign8);
        }
#endif
        if (pEEType->HasFinalizer())
            return INDIRECTION(RhpNewFinalizable);
        else
            return INDIRECTION(RhpNewFast);

    case RuntimeHelperKind::IsInst:
        if (pEEType->IsArray())
            return INDIRECTION(RhTypeCast_IsInstanceOfArray);
        else if (pEEType->IsInterface())
            return INDIRECTION(RhTypeCast_IsInstanceOfInterface);
        else
            return INDIRECTION(RhTypeCast_IsInstanceOfClass);

    case RuntimeHelperKind::CastClass:
        if (pEEType->IsArray())
            return INDIRECTION(RhTypeCast_CheckCastArray);

        else if (pEEType->IsInterface())
            return INDIRECTION(RhTypeCast_CheckCastInterface);
        else
            return INDIRECTION(RhTypeCast_CheckCastClass);

    case RuntimeHelperKind::AllocateArray:
#ifdef _ARM_
        if (pEEType->RequiresAlign8())
            return INDIRECTION(RhpNewArrayAlign8);
#endif
        return INDIRECTION(RhpNewArray);

    case RuntimeHelperKind::CheckArrayElementType:
        return INDIRECTION(RhTypeCast_CheckVectorElemAddr);

    default:
        UNREACHABLE();
    }
}

#undef DECLARE_INDIRECTION
#undef INDIRECTION

#ifdef FEATURE_CACHED_INTERFACE_DISPATCH
EXTERN_C void * RhpInitialDynamicInterfaceDispatch;

COOP_PINVOKE_HELPER(void *, RhNewInterfaceDispatchCell, (EEType * pInterface, Int32 slotNumber))
{
    InterfaceDispatchCell * pCell = new (nothrow) InterfaceDispatchCell[2];
    if (pCell == NULL)
        return NULL;

    // Due to the synchronization mechanism used to update this indirection cell we must ensure the cell's alignment is twice that of a pointer.
    // Fortunately, Windows heap guarantees this alignment.
    ASSERT(IS_ALIGNED(pCell, 2 * POINTER_SIZE));
    ASSERT(IS_ALIGNED(pInterface, (InterfaceDispatchCell::IDC_CachePointerMask + 1)));

    pCell[0].m_pStub = (UIntNative)&RhpInitialDynamicInterfaceDispatch;
    pCell[0].m_pCache = ((UIntNative)pInterface) | InterfaceDispatchCell::IDC_CachePointerIsInterfacePointerOrMetadataToken;
    pCell[1].m_pStub = 0;
    pCell[1].m_pCache = (UIntNative)slotNumber;

    return pCell;
}
#endif // FEATURE_CACHED_INTERFACE_DISPATCH

COOP_PINVOKE_HELPER(PTR_UInt8, RhGetThreadLocalStorageForDynamicType, (UInt32 uOffset, UInt32 tlsStorageSize, UInt32 numTlsCells))
{
    Thread * pCurrentThread = ThreadStore::GetCurrentThread();

    PTR_UInt8 pResult = pCurrentThread->GetThreadLocalStorageForDynamicType(uOffset);
    if (pResult != NULL || tlsStorageSize == 0 || numTlsCells == 0)
        return pResult;

    ASSERT(tlsStorageSize > 0 && numTlsCells > 0);
    return pCurrentThread->AllocateThreadLocalStorageForDynamicType(uOffset, tlsStorageSize, numTlsCells);
}

#endif // DACCESS_COMPILE
