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
#include "GenericInstance.h"
#include "varint.h"
#include "DebugEventSource.h"

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
    FOREACH_MODULE(pModule)
    {
        if (pModule->ContainsCodeAddress(ControlPC))
        {
            return pModule->FindMethodStartAddress(ControlPC);
        }
    }
    END_FOREACH_MODULE;

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

    // TODO: JIT support in DAC
#ifndef DACCESS_COMPILE
#ifdef FEATURE_DYNAMIC_CODE
    for (CodeManagerEntry * pEntry = m_CodeManagerList.GetHead(); pEntry != NULL; pEntry = pEntry->m_pNext)
    {
        if (dac_cast<TADDR>(pvAddress) - dac_cast<TADDR>(pEntry->m_pvStartRange) < pEntry->m_cbRange)
            return pEntry->m_pCodeManager;
    }
#endif
#endif

    return NULL;
}

GPTR_DECL(RuntimeInstance, g_pTheRuntimeInstance);
PTR_RuntimeInstance GetRuntimeInstance()
{
    return g_pTheRuntimeInstance;
}

void RuntimeInstance::EnumGenericStaticGCRefs(PTR_GenericInstanceDesc pInst, void * pfnCallback, void * pvCallbackData, Module *pModule)
{
    while (pInst)
    {
        if (pInst->HasGcStaticFields())
            Module::EnumStaticGCRefsBlock(pfnCallback, pvCallbackData,
                                          pInst->GetGcStaticFieldDesc(), pInst->GetGcStaticFieldData());

        // Thread local statics.
        if (pInst->HasThreadStaticFields())
        {
            // Special case for dynamic types: TLS storage managed manually by runtime
            UInt32 uiFieldsStartOffset = pInst->GetThreadStaticFieldStartOffset();
            if (uiFieldsStartOffset & DYNAMIC_TYPE_TLS_OFFSET_FLAG)
            {
                FOREACH_THREAD(pThread)
                {
                    PTR_UInt8 pTLSStorage = pThread->GetThreadLocalStorageForDynamicType(uiFieldsStartOffset);
                    if (pTLSStorage != NULL)
                    {
                        Module::EnumStaticGCRefsBlock(pfnCallback, pvCallbackData, pInst->GetThreadStaticFieldDesc(), pTLSStorage);
                    }
                }
                END_FOREACH_THREAD
            }
            else
            {
                // See RhGetThreadStaticFieldAddress for details on where TLS fields live.

                UInt32 uiTlsIndex;
                UInt32 uiFieldOffset;

                if (pModule != NULL)
                {
                    ModuleHeader * pModuleHeader = pModule->GetModuleHeader();
                    uiTlsIndex = *pModuleHeader->PointerToTlsIndex;
                    uiFieldOffset = pModuleHeader->TlsStartOffset + uiFieldsStartOffset;
                }
                else
                {
                    uiTlsIndex = pInst->GetThreadStaticFieldTlsIndex();
                    uiFieldOffset = uiFieldsStartOffset;
                }

            FOREACH_THREAD(pThread)
            {
                Module::EnumStaticGCRefsBlock(pfnCallback, pvCallbackData,
                                              pInst->GetThreadStaticFieldDesc(),
                                              pThread->GetThreadLocalStorage(uiTlsIndex, uiFieldOffset));
            }
            END_FOREACH_THREAD
        }
        }

        pInst = pInst->GetNextGidWithGcRoots();
    }
}

void RuntimeInstance::EnumAllStaticGCRefs(void * pfnCallback, void * pvCallbackData)
{
    FOREACH_MODULE(pModule)
    {
        pModule->EnumStaticGCRefs(pfnCallback, pvCallbackData);

        EnumGenericStaticGCRefs(pModule->GetGidsWithGcRootsList(), pfnCallback, pvCallbackData, pModule);
    }
    END_FOREACH_MODULE

    EnumGenericStaticGCRefs(m_genericInstReportList, pfnCallback, pvCallbackData, NULL);
}

static UInt32 HashEETypeByPointerValue(PTR_EEType pEEType) 
{ 
    return (UInt32)dac_cast<TADDR>(pEEType) >> 3;
}

struct GenericTypeTraits : public DefaultSHashTraits<PTR_GenericInstanceDesc>
{
    typedef PTR_EEType key_t;

    static key_t GetKey(const element_t e)
    {
        return e->GetEEType();
    }

    static bool Equals(key_t k1, key_t k2)
    {
        return (k1 == k2);
    }

    static count_t Hash(key_t k)
    {
        return HashEETypeByPointerValue(k);
    }

    static bool IsNull(const element_t e)
    {
        return (e == NULL);
    }

    static const element_t Null()
    {
        return NULL; 
    }
};

class GenericTypeHashTable : public SHash< NoRemoveSHashTraits < GenericTypeTraits > >
{
};

#ifndef DACCESS_COMPILE

bool RuntimeInstance::BuildGenericTypeHashTable()
{
    UInt32 nTotalCount = 0;

    FOREACH_MODULE(pModule)
    {
        nTotalCount += pModule->GetGenericInstanceDescCount(Module::GenericInstanceDescKind::VariantGenericInstances);
    }
    END_FOREACH_MODULE;

    GenericTypeHashTable * pTable = new (nothrow) GenericTypeHashTable();
    if (pTable == NULL)
        return false;

    // Preallocate the table to make rehashing unnecessary
    if(!pTable->CheckGrowth(nTotalCount))
    {
        delete pTable;
        return false;
    }

    FOREACH_MODULE(pModule)
    {
        Module::GenericInstanceDescEnumerator gidEnumerator(pModule, Module::GenericInstanceDescKind::VariantGenericInstances);
        GenericInstanceDesc * pGid;
        while ((pGid = gidEnumerator.Next()) != NULL)
        {
            if (!pTable->Add(pGid))
            {
                delete pTable;
                return false;
            }
        }
    }
    END_FOREACH_MODULE;

    // The hash table is initialized. Attempt to publish this version of the table to other threads. If we
    // lose (another thread has already updated m_pGenericTypeHashTable) then we deallocate our version and
    // use theirs for the lookup.
    if (PalInterlockedCompareExchangePointer((void**)&m_pGenericTypeHashTable,
                                                pTable,
                                                NULL) != NULL)
    {
        delete pTable;
    }

    return true;
}

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
    m_fStandaloneExeMode(false),
    m_pStandaloneExeModule(NULL),
    m_pGenericTypeHashTable(NULL),
    m_conservativeStackReportingEnabled(false)
{
}

RuntimeInstance::~RuntimeInstance()
{
    if (m_pGenericTypeHashTable != NULL)
    {
        delete m_pGenericTypeHashTable;
        m_pGenericTypeHashTable = NULL;
    }

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

bool RuntimeInstance::EnableConservativeStackReporting()
{
    m_conservativeStackReportingEnabled = true;
    return true;
}

EXTERN_C void REDHAWK_CALLCONV RhpSetHaveNewClasslibs();

bool RuntimeInstance::RegisterModule(ModuleHeader *pModuleHeader)
{
    // Determine whether we're in standalone exe mode. If we are we'll see the runtime module load followed by
    // exactly one additional module (the exe itself). The exe module will have a standalone flag set in its
    // header.
    ASSERT(m_fStandaloneExeMode == false);
    if (pModuleHeader->Flags & ModuleHeader::StandaloneExe)
        m_fStandaloneExeMode = true;

    CreateHolder<Module> pModule = Module::Create(pModuleHeader);

    if (NULL == pModule)
        return false;

    {
        // WARNING: This region must be kept small and must not callout 
        // to arbitrary code.  See Thread::Hijack for more details.
        ReaderWriterLock::WriteHolder write(&m_ModuleListLock);
        m_ModuleList.PushHead(pModule);
    }

    if (m_fStandaloneExeMode)
        m_pStandaloneExeModule = pModule;

    if (pModule->IsClasslibModule())
        RhpSetHaveNewClasslibs();

#ifdef FEATURE_PROFILING
    InitProfiling(pModuleHeader);
#endif // FEATURE_PROFILING

    {
        // Support for late-loaded modules: flush the generic hashtable to force its regeneration
        // including the new module.
        // @TODO: This is obviously not ideal, we would be better of by incrementally adding
        //        types in the new module to the existing hashtable. Unfortunately today implementation
        //        doesn't expect the table to be growable.
        ReaderWriterLock::WriteHolder write(&m_GenericHashTableLock);
        if (m_pGenericTypeHashTable != nullptr)
        {
            delete m_pGenericTypeHashTable;
            m_pGenericTypeHashTable = nullptr;
        }
    }

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

#ifdef FEATURE_DYNAMIC_CODE
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
#endif

// static 
RuntimeInstance * RuntimeInstance::Create(HANDLE hPalInstance)
{
    NewHolder<RuntimeInstance> pRuntimeInstance = new (nothrow) RuntimeInstance();
    if (NULL == pRuntimeInstance)
        return NULL;

    CreateHolder<ThreadStore>  pThreadStore = ThreadStore::Create(pRuntimeInstance);
    if (NULL == pThreadStore)
        return NULL;

    pThreadStore.SuppressRelease();
    pRuntimeInstance->m_pThreadStore = pThreadStore;
    pRuntimeInstance->m_hPalInstance = hPalInstance;

    pRuntimeInstance->m_genericInstReportList = NULL;

#ifdef FEATURE_PROFILING
    pRuntimeInstance->m_fProfileThreadCreated = false;
#endif

    pRuntimeInstance.SuppressRelease();

    return pRuntimeInstance;
}


void RuntimeInstance::Destroy()
{
    delete this;
}

bool RuntimeInstance::ShouldHijackLoopForGcStress(UIntNative CallsiteIP)
{
#if defined(FEATURE_GC_STRESS) & !defined(DACCESS_COMPILE)
    return ShouldHijackForGcStress(CallsiteIP, htLoop);
#else // FEATURE_GC_STRESS & !DACCESS_COMPILE
    UNREFERENCED_PARAMETER(CallsiteIP);
    return false;
#endif // FEATURE_GC_STRESS & !DACCESS_COMPILE
}

bool RuntimeInstance::ShouldHijackCallsiteForGcStress(UIntNative CallsiteIP)
{
#if defined(FEATURE_GC_STRESS) & !defined(DACCESS_COMPILE)
    return ShouldHijackForGcStress(CallsiteIP, htCallsite);
#else // FEATURE_GC_STRESS & !DACCESS_COMPILE
    UNREFERENCED_PARAMETER(CallsiteIP);
    return false;
#endif // FEATURE_GC_STRESS & !DACCESS_COMPILE
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

// Given the EEType* for an instantiated generic type retrieve the GenericInstanceDesc associated with that
// type. This is legal only for types that are guaranteed to have this metadata at runtime; generic types
// which have variance over one or more of their type parameters and generic interfaces on array).
GenericInstanceDesc * RuntimeInstance::LookupGenericInstance(EEType * pEEType)
{
    // EETypes we will attempt to match against will always be the canonical version. Canonicalize our input
    // EEType as well if required.
    if (pEEType->IsCloned())
        pEEType = pEEType->get_CanonicalEEType();

    if (m_pGenericTypeHashTable == NULL)
    {
        if (!BuildGenericTypeHashTable())
        {
            // We failed the allocation but we don't want to fail the call (because we build this table lazily
            // we're doing the allocation at a point the caller doesn't expect can fail). So fall back to the
            // slow linear scan of all variant GIDs in this case.

            FOREACH_MODULE(pModule)
            {
                Module::GenericInstanceDescEnumerator gidEnumerator(pModule, Module::GenericInstanceDescKind::VariantGenericInstances);
                GenericInstanceDesc * pGid;
                while ((pGid = gidEnumerator.Next()) != NULL)
                {
                    if (pGid->GetEEType() == pEEType)
                        return pGid;
                }
            }
            END_FOREACH_MODULE;

            // It is not legal to call this API unless you know there is a matching GenericInstanceDesc.
            UNREACHABLE();
        }
    }

    ReaderWriterLock::ReadHolder read(&m_GenericHashTableLock);

    const PTR_GenericInstanceDesc * ppGid = m_pGenericTypeHashTable->LookupPtr(pEEType);
    if (ppGid != NULL)
        return *ppGid;

    // It is not legal to call this API unless you know there is a matching GenericInstanceDesc.
    UNREACHABLE();
}

// Given the EEType* for an instantiated generic type retrieve instantiation information (generic type
// definition EEType, arity, type arguments and variance info for each type parameter). Has the same
// limitations on usage as LookupGenericInstance above.
EEType * RuntimeInstance::GetGenericInstantiation(EEType *               pEEType,
                                                  UInt32 *               pArity,
                                                  EEType ***             ppInstantiation,
                                                  GenericVarianceType ** ppVarianceInfo)
{
    GenericInstanceDesc * pInst = LookupGenericInstance(pEEType);

    ASSERT(pInst != NULL && pInst->HasInstantiation());

    *pArity = pInst->GetArity();
    *ppInstantiation = (EEType**)((UInt8*)pInst + pInst->GetParameterTypeOffset(0));

    if (pInst->HasVariance())
        *ppVarianceInfo = (GenericVarianceType*)((UInt8*)pInst + pInst->GetParameterVarianceOffset(0));
    else
        *ppVarianceInfo = NULL;

    return pInst->GetGenericTypeDef().GetValue();
}

bool RuntimeInstance::CreateGenericInstanceDesc(EEType *             pEEType,
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
    if (m_pGenericTypeHashTable == NULL)
    {
        if (!BuildGenericTypeHashTable())
            return false;
    }

    GenericInstanceDesc::OptionalFieldTypes flags = GenericInstanceDesc::GID_Instantiation;
    
    if (pTemplateType->HasGenericVariance())
        flags |= GenericInstanceDesc::GID_Variance;
    if (gcStaticDataSize > 0)
        flags |= GenericInstanceDesc::GID_GcStaticFields | GenericInstanceDesc::GID_GcRoots;
    if (nonGcStaticDataSize > 0)
        flags |= GenericInstanceDesc::GID_NonGcStaticFields;
    if (threadStaticOffset != 0)
        flags |= GenericInstanceDesc::GID_ThreadStaticFields | GenericInstanceDesc::GID_GcRoots;

    // Note: arity is limited to a maximum value of 65535 on the managed layer before CreateGenericInstanceDesc
    // gets called. With this value, cbGidSize will not exceed 600K, so no need to use safe integers
    size_t cbGidSize = GenericInstanceDesc::GetSize(flags, arity);

    NewArrayHolder<UInt8> pGidMemory = new (nothrow) UInt8[cbGidSize];
    if (pGidMemory == NULL)
        return false;

    GenericInstanceDesc * pGid = (GenericInstanceDesc *)(UInt8 *)pGidMemory;
    memset(pGid, 0, cbGidSize);

    pGid->Init(flags);
    pGid->SetEEType(pEEType);
    pGid->SetArity(arity);

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
        pGid->SetNonGcStaticFieldData(pNonGcStaticData + nonGCStaticDataOffset);
    }

    NewArrayHolder<UInt8> pGcStaticData;
    if (gcStaticDataSize > 0)
    {
        // The value of gcStaticDataSize is read from native layout info in the managed layer, where
        // there is also a check that it does not exceed the max value of a signed Int32
        pGcStaticData = new (nothrow) UInt8[gcStaticDataSize];
        if (pGcStaticData == NULL)
            return false;
        memset(pGcStaticData, 0, gcStaticDataSize);
        pGid->SetGcStaticFieldData(pGcStaticData);
        pGid->SetGcStaticFieldDesc(pGcStaticsDesc);
    }

    if (threadStaticOffset != 0)
    {
        // Note: TLS index not used for dynamically created types
        pGid->SetThreadStaticFieldTlsIndex(0);
        pGid->SetThreadStaticFieldStartOffset(threadStaticOffset);

        // Note: pThreadStaticsDesc can possibly be NULL if the type doesn't have any thread-static reference types
        pGid->SetThreadStaticFieldDesc(pThreadStaticsDesc);
    }

    if (pTemplateType->HasGenericVariance())
    {
        ASSERT(pGenericVarianceFlags != NULL);

        for (UInt32 i = 0; i < arity; i++)
        {
            GenericVarianceType variance = (GenericVarianceType)pGenericVarianceFlags[i];
            pGid->SetParameterVariance(i, variance);
        }
    }

    ReaderWriterLock::WriteHolder write(&m_GenericHashTableLock);

    if (!m_pGenericTypeHashTable->Add(pGid))
        return false;

    if (gcStaticDataSize > 0 || pGid->HasThreadStaticFields())
    {
        pGid->SetNextGidWithGcRoots(m_genericInstReportList);
        m_genericInstReportList = pGid;
    }

    pGidMemory.SuppressRelease();
    pNonGcStaticData.SuppressRelease();
    pGcStaticData.SuppressRelease();
    return true;
}

bool RuntimeInstance::SetGenericInstantiation(EEType *               pEEType,
                                              EEType *               pEETypeDef,
                                              UInt32                 arity,
                                              EEType **              pInstantiation)
{
    ASSERT(pEEType->IsGeneric());
    ASSERT(pEEType->IsDynamicType());
    ASSERT(m_pGenericTypeHashTable != NULL)

    GenericInstanceDesc * pGid = LookupGenericInstance(pEEType);
    ASSERT(pGid != NULL);
    
    pGid->SetGenericTypeDef((EETypeRef&)pEETypeDef);

    // Arity should have been set during the GID creation time
    ASSERT(pGid->GetArity() == arity);

    for (UInt32 iArg = 0; iArg < arity; iArg++)
        pGid->SetParameterType(iArg, (EETypeRef&)pInstantiation[iArg]);

    return true;
}

COOP_PINVOKE_HELPER(EEType *, RhGetGenericInstantiation, (EEType *               pEEType,
                                                          UInt32 *               pArity,
                                                          EEType ***             ppInstantiation,
                                                          GenericVarianceType ** ppVarianceInfo))
{
#if CORERT
    *pArity = pEEType->get_GenericArity();
    *ppInstantiation = pEEType->get_GenericArguments();
    if (pEEType->HasGenericVariance())
        *ppVarianceInfo = pEEType->get_GenericVariance();
    else
        *ppVarianceInfo = NULL;
    return pEEType->get_GenericDefinition();
#else
    return GetRuntimeInstance()->GetGenericInstantiation(pEEType,
                                                         pArity,
                                                         ppInstantiation,
                                                         ppVarianceInfo);
#endif
}

COOP_PINVOKE_HELPER(bool, RhSetGenericInstantiation, (EEType *               pEEType,
                                                      EEType *               pEETypeDef,
                                                      UInt32                 arity,
                                                      EEType **              pInstantiation))
{
    return GetRuntimeInstance()->SetGenericInstantiation(pEEType,
                                                         pEETypeDef,
                                                         arity,
                                                         pInstantiation);
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

    return GetRuntimeInstance()->CreateGenericInstanceDesc(pEEType, pTemplateType, arity, nonGcStaticDataSize, nonGCStaticDataOffset, gcStaticDataSize,
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
    // Fortunately, Windows heap guarantees this aligment.
    ASSERT(IS_ALIGNED(pCell, 2 * POINTER_SIZE));
    ASSERT(IS_ALIGNED(pInterface, (InterfaceDispatchCell::IDC_CachePointerMask + 1)));

    pCell[0].m_pStub = (UIntNative)&RhpInitialDynamicInterfaceDispatch;
    pCell[0].m_pCache = ((UIntNative)pInterface) | InterfaceDispatchCell::IDC_CachePointerIsInterfacePointer;
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

COOP_PINVOKE_HELPER(void *, RhGetNonGcStaticFieldData, (EEType * pEEType))
{
    // We shouldn't be attempting to get the gc/non-gc statics data for non-dynamic types...
    // For non-dynamic types, that info should have been hashed in a table and stored in its corresponding blob in the image.
    // The reason we don't want to do the lookup for non-dynamic types is that LookupGenericInstance will do the lookup in 
    // a hashtable that *only* has the GIDs with variance. If we were to store all GIDs in that hashtable, we'd be violating
    // pay-for-play principles
    ASSERT(pEEType->IsDynamicType());

    GenericInstanceDesc * pGid = GetRuntimeInstance()->LookupGenericInstance(pEEType);
    ASSERT(pGid != NULL);

    if (pGid->HasNonGcStaticFields())
    {
        return dac_cast<DPTR(TgtPTR_UInt8)>(pGid + pGid->GetNonGcStaticFieldDataOffset());
    }

    return NULL;
}

COOP_PINVOKE_HELPER(void *, RhGetGcStaticFieldData, (EEType * pEEType))
{
    // We shouldn't be attempting to get the gc/non-gc statics data for non-dynamic types...
    // For non-dynamic types, that info should have been hashed in a table and stored in its corresponding blob in the image.
    // The reason we don't want to do the lookup for non-dynamic types is that LookupGenericInstance will do the lookup in 
    // a hashtable that *only* has the GIDs with variance. If we were to store all GIDs in that hashtable, we'd be violating
    // pay-for-play principles
    ASSERT(pEEType->IsDynamicType());

    GenericInstanceDesc * pGid = GetRuntimeInstance()->LookupGenericInstance(pEEType);
    ASSERT(pGid != NULL);

    if (pGid->HasGcStaticFields())
    {
        return dac_cast<DPTR(TgtPTR_UInt8)>(pGid + pGid->GetGcStaticFieldDataOffset());
    }

    return NULL;
}

COOP_PINVOKE_HELPER(void *, RhAllocateThunksFromTemplate, (PTR_UInt8 moduleBase, UInt32 templateRva, UInt32 templateSize))
{
    void* pThunkMap = NULL;
    if (PalAllocateThunksFromTemplate((HANDLE)moduleBase, templateRva, templateSize, &pThunkMap) == FALSE)
        return NULL;

    return pThunkMap;
}

#endif
