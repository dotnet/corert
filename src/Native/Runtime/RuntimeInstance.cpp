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
#include "static_check.h"
#include "slist.h"
#include "holder.h"
#include "Crst.h"
#include "rhbinder.h"
#include "RWLock.h"
#include "RuntimeInstance.h"
#include "event.h"
#include "threadstore.h"
#include "gcrhinterface.h"
#include "module.h"
#include "eetype.h"
#include "GenericInstance.h"
#include "varint.h"
#include "regdisplay.h"
#include "StackFrameIterator.h"
#include "thread.h"
#include "DebugEventSource.h"

#include "CommonMacros.inl"
#include "slist.inl"
#include "eetype.inl"
#include "OptionalFields.inl"

#ifdef  FEATURE_GC_STRESS
enum HijackType { htLoop, htCallsite };
bool ShouldHijackForGcStress(UIntNative CallsiteIP, HijackType ht);
#endif // FEATURE_GC_STRESS

#include "shash.h"
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

        // If there is no generic unification, we report generic instantiation statics directly from each module (the
        // runtime and the binary/binaries for the library and the app) since they haven't been unified.
        if (m_genericInstHashtabCount == 0)
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

    m_genericInstHashtabLock.Destroy();
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

    pModule.SuppressRelease();
    // This event must occur after the module is added to the enumeration
    DebugEventSource::SendModuleLoadEvent(pModule);
    return true;
}

bool RuntimeInstance::RegisterSimpleModule(SimpleModuleHeader *pModuleHeader)
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

#ifdef FEATURE_VSD
    VirtualCallStubManager * pVSD;
    if (!CreateVSD(&pVSD))
        return NULL;
#endif

    pThreadStore.SuppressRelease();
    pRuntimeInstance->m_pThreadStore = pThreadStore;
    pRuntimeInstance->m_hPalInstance = hPalInstance;

#ifdef FEATURE_VSD
    pRuntimeInstance->m_pVSDManager = pVSD;
#endif

    pRuntimeInstance->m_genericInstHashtab = NULL;
    pRuntimeInstance->m_genericInstHashtabCount = 0;
    pRuntimeInstance->m_genericInstHashtabEntries = 0;
    pRuntimeInstance->m_genericInstHashtabLock.Init(CrstGenericInstHashtab);
#ifdef _DEBUG
    pRuntimeInstance->m_genericInstHashUpdateInProgress = false;
#endif

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

// For the given generic instantiation remove any type indirections via IAT entries. This is used during
// generic instantiation type unification to remove any arbitrary dependencies on the module which happened to
// publish the instantiation first (an IAT indirection is a module dependency since the IAT cell lives in the
// module image itself). The indirections are removed by inspecting the current value of the IAT entry and
// moving the value up into the parent data structure (adjusting whatever flags are necessary to indicate that
// the datum is no longer accessed via the IAT). We can do this since generic instantiation type unification
// is performed at runtime after all the IAT entries have been bound to their final values.
static bool FlattenGenericInstance(UnifiedGenericInstance * pInst)
{
    GenericInstanceDesc * pGid = pInst->GetGid();
    UInt32 cTypeVars = pGid->GetArity();

    // Flatten the instantiated type itself.
    pGid->GetEEType()->Flatten();

    // Flatten the generic type definition. Not much else to do for this case since the generic type
    // definition isn't a real EEType (it has virtually no use at runtime).
    pGid->GetGenericTypeDef().Flatten();

    // Flatten each of the type arguments.
    for (UInt32 i = 0; i < cTypeVars; i++)
    {
        // Note that if a type reference stored in the GenericInstanceDesc was flattened then we're done with
        // that entry. That's because if the reference was indirected in the first place we can be sure there
        // are no further (arbitrary) references to the module; the entry refered to a different module and
        // did so because there was a non-arbitrary dependency on that module.
        if (!pGid->GetParameterType(i).Flatten())
        {
            EETypeRef typeVarRef = pGid->GetParameterType(i);
            EEType * pTypeVar = typeVarRef.GetValue();

            // The type reference above wasn't indirected to another module. Examine the type to see whether
            // it contains any arbitrary references to the module which provided it.
            switch (pTypeVar->get_Kind())
            {
            case EEType::CanonicalEEType:
                // Nothing to do here; a canonical type means this instantiation has a direct, non-arbitrary
                // dependency on the module anyway. Therefore we don't care if there are any arbitrary
                // dependencies to the same module as well.
                break;

            case EEType::GenericTypeDefEEType:
                // Nothing to do here. GenericTypeDefinitions are local to their defining module
                break;

            case EEType::ClonedEEType:
                // For cloned types we can simply replace the type argument with the corresponding canonical
                // type.
                typeVarRef.pEEType = pTypeVar->get_CanonicalEEType();
                pGid->SetParameterType(i, typeVarRef);
                break;

            case EEType::ParameterizedEEType:
                // Array types are tricky. They're always declared locally and unified at runtime (during cast
                // operations) since there's a high degree of structural equivalence and only ever one type
                // variable. This puts us in the nasty situation where we might have to allocate a whole new
                // array type which is equivalent but doesn't reside in any one module (e.g. we allocate it
                // from the NT heap). We can avoid this in the sub-case where the element type is bound to the
                // providing module (i.e. the module defines the element type) since that would place a
                // (non-arbitrary) dependence on the module for this generic instantiation anyway).
                if (pTypeVar->IsRelatedTypeViaIAT())
                {
                    // The element type of this array wasn't defined by the module directly. Therefore it is
                    // likely that continuing to use this definition of the array type would place an
                    // arbitrary dependence on the module. We need to create a new, module neutral, type in
                    // this case.

                    // Luckily we only have to create a fairly simplistic type. Since this is only used to
                    // establish identity between generic instances (i.e. type checks) we only need the base
                    // EEType, no GC desc, interface map or interface dispatch map.
                    EEType *pArrayType = new (nothrow) EEType();
                    if (pArrayType == NULL)
                        return false;

                    // Initialize the type as an array of the element type we extracted from the original
                    // array type.
                    pArrayType->InitializeAsArrayType(pTypeVar->get_RelatedParameterType(), pTypeVar->get_BaseSize());

                    // Mark the type as runtime allocated so we can identify and deallocate it when we no
                    // longer need it.
                    pArrayType->SetRuntimeAllocated();

                    // Patch the type variable to point to the module-neutral version of the array type.
                    typeVarRef.pEEType = pArrayType;
                    pGid->SetParameterType(i, typeVarRef);
                }
                break;

            default:
                UNREACHABLE();
            }
        }
    }

    return true;
}

// List of primes used to size the generic instantiation hash table bucket array.
static const UInt32 s_rgPrimes[] = { 3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239, 293, 353,
                                     431, 521, 631, 761, 919, 1103, 1327, 1597, 1931, 2333, 2801, 3371, 4049,
                                     4861, 5839, 7013, 8419, 10103 };

// The cInstances parameter is optional and only used for the first call to StartGenericUnification to
// determine the initial number of hash chain buckets.
bool RuntimeInstance::StartGenericUnification(UInt32 cInstances)
{
    ASSERT(!m_fStandaloneExeMode);

    // We take the hash table lock here and release it in EndGenericUnification. This avoids re-taking the
    // lock for every generic instantiation, of which there can be many.
    Crst::Enter(&m_genericInstHashtabLock);

#ifdef _DEBUG
    ASSERT(!m_genericInstHashUpdateInProgress);
    m_genericInstHashUpdateInProgress = true;
#endif

    // We lazily allocate the hash table.
    if (m_genericInstHashtab == NULL)
    {
        // Base the initial hash table bucket array size on the number of generic instantiations in the first
        // module that registers such instantiations (this should be the class library since rtm does use any
        // generic instantiations). This is an arbitrary number but at least in the cases we've seen so far
        // it's the dominant one (System.Private.CoreLib, roslyn). This way we're at least slightly pay-for-play (the old
        // scheme had a fixed constant size and we'd either allocate too many buckets for generics light
        // scenarios such as WCL or too few for heavy scenarios such as System.Private.CoreLib or roslyn).
        //
        // Note that we don't yet support dynamically re-sizing the hash table, mainly because the resulting
        // re-hash has working set implications of its own.
        //
        // Round up the number of hash buckets to some prime number (up to some reasonable ceiling).
        cInstances = max(17, cInstances);
        UInt32 cHashBuckets = 0;

        UInt32 cPrimes = sizeof(s_rgPrimes) / sizeof(s_rgPrimes[0]);
        for (UInt32 i = 0; i < cPrimes; i++)
        {
            if (s_rgPrimes[i] >= cInstances)
            {
                cHashBuckets = s_rgPrimes[i];
                break;
            }
        }
        if (cHashBuckets == 0)
            cHashBuckets = s_rgPrimes[cPrimes - 1];

        m_genericInstHashtabCount = cHashBuckets;

        // Allocate a second set of buckets used during updates to temporarily store the newly added items.
        // This avoids having to search those new entries during subsequent additions within the same update
        // (since a module will never publish two identical generic instantiations these extra equality checks
        // are unnecessary as well as expensive).
        m_genericInstHashtab = new (nothrow) UnifiedGenericInstance*[cHashBuckets * 2];
        m_genericInstHashtabUpdates = m_genericInstHashtab + cHashBuckets;
        if (m_genericInstHashtab == NULL)
        {
            Crst::Leave(&m_genericInstHashtabLock);
            return false;
        }
        memset(m_genericInstHashtab, 0, m_genericInstHashtabCount * sizeof(m_genericInstHashtab[0]) * 2);
    }

    // Initialize the temporary update buckets to point to the head of each live bucket. As we unify
    // instantiations we'll add to the heads of these update buckets so we'll record the new entries without
    // publishing them in the real table (until EndGenericUnification is called).
    for (UInt32 i = 0; i < m_genericInstHashtabCount; i++)
        m_genericInstHashtabUpdates[i] = m_genericInstHashtab[i];

    return true;
}

UnifiedGenericInstance *RuntimeInstance::UnifyGenericInstance(GenericInstanceDesc * pLocalGid, UInt32 uiLocalTlsIndex)
{
    EEType * pLocalEEType = pLocalGid->GetEEType();
    UnifiedGenericInstance * pCanonicalInst = NULL;
    GenericInstanceDesc * pCanonicalGid = NULL;

    UInt32 hashCode = pLocalGid->GetHashCode() % m_genericInstHashtabCount;
    ASSERT(hashCode < m_genericInstHashtabCount);
    for (pCanonicalInst = m_genericInstHashtab[hashCode];
         pCanonicalInst != NULL;
         pCanonicalInst = pCanonicalInst->m_pNext)
    {
        if (pCanonicalInst->Equals(pLocalGid))
        {
            pCanonicalGid = pCanonicalInst->GetGid();

            // Increment count of modules which depend on this type.
            pCanonicalInst->m_cRefs++;

            goto Done;
        }
    }

    {
        // No module has previously registered this generic instantiation. We need to allocate and create a new
        // unified canonical representation for this type.

        // Allocate enough memory for the UnifiedGenericInstance, canonical GenericInstanceDesc, canonical generic
        // instantiation EEType and static fields (GC and non-GC). Note that we don't have to allocate space for a
        // GC descriptor, vtable or interface dispatch map for the EEType since this type will never appear in an
        // object header on the GC heap (we always use module-local EETypes for this so that virtual dispatch is
        // bound back to the local module).
        UInt32 cbGid = pLocalGid->GetSize();
        UInt32 cbPaddedGid = (UInt32)(ALIGN_UP(cbGid, sizeof(void*)));
        UInt32 cbEEType = EEType::GetSizeofEEType(0,                                      // # of virtuals (no vtable)
                                                  pLocalEEType->GetNumInterfaces(),
                                                  false,                                  // HasFinalizer (we don't care)
                                                  false,                                  // RequiresOptionalFields (we don't care)
                                                  false,                                  // IsNullable (we don't care)
                                                  false);                                 // fHasSealedVirtuals (we don't care)
        UInt32 cbNonGcStaticFields = pLocalGid->GetSizeOfNonGcStaticFieldData();
        UInt32 cbGcStaticFields = pLocalGid->GetSizeOfGcStaticFieldData();
        PTR_StaticGcDesc pLocalGcStaticDesc = cbGcStaticFields ? pLocalGid->GetGcStaticFieldDesc() : NULL;
        UInt32 cbGcDesc = pLocalGcStaticDesc ? pLocalGcStaticDesc->GetSize() : 0;

        // for performance and correctness reasons (at least on ARM), we wish to align the static areas on a
        // multiple of STATIC_FIELD_ALIGNMENT
        const UInt32 STATIC_FIELD_ALIGNMENT = 8;
        UInt32 cbMemory = (UInt32)ALIGN_UP(sizeof(UnifiedGenericInstance) + cbPaddedGid + cbEEType, STATIC_FIELD_ALIGNMENT) +
                          (UInt32)ALIGN_UP(cbNonGcStaticFields, STATIC_FIELD_ALIGNMENT) +
                          cbGcStaticFields +
                          cbGcDesc;
        // Note: Generic instance unification is not a product feature that we ship in ProjectN, so there is no need to
        // use safe integers when computing the value of cbMemory.
        UInt8 * pMemory = new (nothrow) UInt8[cbMemory];
        if (pMemory == NULL)
            return NULL;

        // Determine the start of the various individual data structures in the monolithic chunk of memory we
        // allocated.

        pCanonicalInst = (UnifiedGenericInstance*)pMemory;
        pMemory += sizeof(UnifiedGenericInstance);

        pCanonicalGid = (GenericInstanceDesc*)pMemory;
        pMemory += cbPaddedGid;

        EEType * pCanonicalType = (EEType*)pMemory;
        pMemory = ALIGN_UP(pMemory + cbEEType, STATIC_FIELD_ALIGNMENT);

        UInt8 * pStaticData = pMemory;
        pMemory += ALIGN_UP(cbNonGcStaticFields, STATIC_FIELD_ALIGNMENT);

        UInt8 * pGcStaticData = pMemory;
        pMemory += cbGcStaticFields;

        StaticGcDesc * pStaticGcDesc = (StaticGcDesc*)pMemory;
        pMemory += cbGcDesc;

        // Copy local GenericInstanceDesc.
        memcpy(pCanonicalGid, pLocalGid, cbGid);

        // Copy local definition of the generic instantiation EEType (no vtable).
        memcpy(pCanonicalType, pLocalEEType, sizeof(EEType));

        // Set the type as runtime allocated (just for debugging purposes at the moment).
        pCanonicalType->SetRuntimeAllocated();

        // Copy the interface map directly after the EEType (if there are any interfaces).
        if (pLocalEEType->HasInterfaces())
            memcpy(pCanonicalType + 1,
                   pLocalEEType->GetInterfaceMap().GetRawPtr(),
                   pLocalEEType->GetNumInterfaces() * sizeof(EEInterfaceInfo));

        // Copy initial static data from the module.
        if (cbNonGcStaticFields)
            memcpy(pStaticData, pLocalGid->GetNonGcStaticFieldData(), cbNonGcStaticFields);
        if (cbGcStaticFields)
            memcpy(pGcStaticData, pLocalGid->GetGcStaticFieldData(), cbGcStaticFields);

        // If we have any GC static data then we need to copy over GC descriptors for it.
        if (cbGcDesc)
            memcpy(pStaticGcDesc, pLocalGcStaticDesc, cbGcDesc);

        // Because we don't store the vtable with our canonical EEType it throws the calculation of the interface
        // map (which is still required for cast operations) off. We need to clear the count of virtual methods in
        // the EEType to correct this (this field should not be required for the canonical type).
        pCanonicalType->SetNumVtableSlots(0);

        // Initialize the UnifiedGenericInstance.
        pCanonicalInst->m_pNext = m_genericInstHashtabUpdates[hashCode];
        pCanonicalInst->m_cRefs = 1;

        // Update canonical GenericInstanceDesc with any values that are no longer local to the module.
        pCanonicalGid->SetEEType(pCanonicalType);
        if (cbNonGcStaticFields)
            pCanonicalGid->SetNonGcStaticFieldData(pStaticData);
        if (cbGcStaticFields)
            pCanonicalGid->SetGcStaticFieldData(pGcStaticData);
        if (cbGcDesc)
            pCanonicalGid->SetGcStaticFieldDesc(pStaticGcDesc);

        // Any generic types with thread static fields need to know the TLS index assigned to the module by the OS
        // loader for the module that ends up "owning" the unified instance. Note that this breaks the module
        // unload scenario since when the arbitrarily chosen owning module is unloaded it's TLS index will be
        // released. Since the OS doesn't provide access to the TLS allocation mechanism used by .tls support
        // (it's a different system than that used by TlsAlloc) our only alternative here would be to allocate TLS
        // slots manually and managed the storage ourselves, which is both complicated and would result in lower
        // performance at the thread static access site (since at a minimum regular TlsAlloc'd TLS indices need to
        // be range checked to determine how they are used with a TEB).
        if (pCanonicalGid->HasThreadStaticFields())
            pCanonicalGid->SetThreadStaticFieldTlsIndex(uiLocalTlsIndex);

        // Attempt to remove any arbitrary dependencies on the module that provided the instantiation. Here
        // arbitrary refers to references to the module that exist purely because the module used an IAT
        // indirection to point to non-local types. We can remove most of these in-place by performing the IAT
        // lookup now and copying the direct pointer up one level of the data structures (see
        // FlattenGenericInstance above for more details). Unfortunately one edge case in particular might require
        // us to allocate memory (some generic instantiations over array types) so the call below can fail. So
        // don't modify any global state (the unification hash table) until this call has succeeded.
        if (!FlattenGenericInstance(pCanonicalInst))
        {
            delete [] pMemory;
            return NULL;
        }

        // If this generic instantiation has GC fields to report add it to the list we traverse during garbage
        // collections.
        if (cbGcStaticFields || pLocalGid->HasThreadStaticFields())
        {
            pCanonicalGid->SetNextGidWithGcRoots(m_genericInstReportList);
            m_genericInstReportList = pCanonicalGid;
        }

        // We've built the new unified generic instantiation, publish it in the hash table. But don't put it on
        // the real bucket chain yet otherwise further additions as part of this same update will needlessly
        // search it. Instead add it to the head of the update bucket. All updated chains will be published back
        // to the real buckets at the end of the update.
        m_genericInstHashtabEntries += 1;
        m_genericInstHashtabUpdates[hashCode] = pCanonicalInst;
    }
  Done:

    // Get here whether we found an existing match for the type or had to create a new entry. All that's left
    // to do is perform any updates to the module local data structures necessary to reflect the unification.

    // Update the module local EEType to be a cloned type refering back to the unified EEType.
    EEType ** ppCanonicalType = (EEType**)((UInt8*)pCanonicalGid + pCanonicalGid->GetEETypeOffset());
    pLocalEEType->MakeClonedType(ppCanonicalType);

    // Update the module local GenericInstanceDesc fields that that module local code still refers to but
    // which need to be redirected to their unified versions.

    if (pLocalGid->HasNonGcStaticFields())
        pLocalGid->SetNonGcStaticFieldData(pCanonicalGid->GetNonGcStaticFieldData());
    if (pLocalGid->HasGcStaticFields())
        pLocalGid->SetGcStaticFieldData(pCanonicalGid->GetGcStaticFieldData());
    if (pLocalGid->HasThreadStaticFields())
    {
        pLocalGid->SetThreadStaticFieldTlsIndex(pCanonicalGid->GetThreadStaticFieldTlsIndex());
        pLocalGid->SetThreadStaticFieldStartOffset(pCanonicalGid->GetThreadStaticFieldStartOffset());
    }

    return pCanonicalInst;
}

void RuntimeInstance::EndGenericUnification()
{
#ifdef _DEBUG
    ASSERT(m_genericInstHashUpdateInProgress);
    m_genericInstHashUpdateInProgress = false;
#endif

    // The update buckets now hold the complete hash chain (since we initialized them to point to the head of
    // the old chains and we always add at the head). Publish these chain heads back into the real hash table
    // buckets to make all updates visible.
    for (UInt32 i = 0; i < m_genericInstHashtabCount; i++)
        m_genericInstHashtab[i] = m_genericInstHashtabUpdates[i];

    Crst::Leave(&m_genericInstHashtabLock);
}

// Release one module's interest in the given generic instantiation. If no modules require the instantiation
// any more release any resources associated with it.
void RuntimeInstance::ReleaseGenericInstance(GenericInstanceDesc * pInst)
{
    CrstHolder hashLock(&m_genericInstHashtabLock);

    // Find the hash chain containing the target instantiation.
    UInt32 hashCode = pInst->GetHashCode() % m_genericInstHashtabCount;
    UnifiedGenericInstance *pGlobalInst = m_genericInstHashtab[hashCode];
    UnifiedGenericInstance *pPrevInst = NULL;

    // Iterate down the hash chain looking for the target instantiation.
    while (pGlobalInst)
    {
        if (pGlobalInst->Equals(pInst))
        {
            // Found it, decrement the count of modules interested in this instantiation. If there are still
            // modules with a reference we can return right now.
            if (--pGlobalInst->m_cRefs > 0)
                return;

            // Unlink the GenericInstanceDesc from the hash chain.
            if (pPrevInst)
                pPrevInst->m_pNext = pGlobalInst->m_pNext;
            else
                m_genericInstHashtab[hashCode] = pGlobalInst->m_pNext;

            GenericInstanceDesc * pGlobalGid = pGlobalInst->GetGid();

            // If the instantiation has GC reference static fields then this descriptor has also been linked
            // on the global list of instantiations we report to the GC. This list is protected from being
            // updated by m_genericInstHashtabLock which we already hold.
            if (pGlobalGid->HasGcStaticFields())
            {
                GenericInstanceDesc *pPrevReportGid = NULL;
                GenericInstanceDesc *pCurrReportGid = m_genericInstReportList;
                while (pCurrReportGid != NULL)
                {
                    if (pCurrReportGid == pGlobalGid)
                    {
                        if (pPrevReportGid == NULL)
                            m_genericInstReportList = pCurrReportGid->GetNextGidWithGcRoots();
                        else
                            pPrevReportGid->SetNextGidWithGcRoots(pCurrReportGid->GetNextGidWithGcRoots());
                        break;
                    }

                    pPrevReportGid = pCurrReportGid;
                    pCurrReportGid = pCurrReportGid->GetNextGidWithGcRoots();
                }
                ASSERT(pCurrReportGid == pGlobalGid);
            }

            // Nobody references the GenericInstanceDesc (and it's associated data such as the EEType and
            // static data), so we can deallocate it. Most data was allocated in one monolithic block and can
            // be deallocated with a single call, but we might have also allocated some module-neutral array
            // types in the instantiation type arguments.
            for (UInt32 i = 0; i < pGlobalGid->GetArity(); i++)
            {
                EEType * pTypeVar = pGlobalGid->GetParameterType(i).GetValue();
                if (pTypeVar->IsRuntimeAllocated())
                    delete pTypeVar;
            }
            delete [] (UInt8*)pGlobalInst;

            return;
        }

        pPrevInst = pGlobalInst;
        pGlobalInst = pGlobalInst->m_pNext;
    }

    // We couldn't find the instantiation in the hash table. This should never happen.
    UNREACHABLE();
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

    if (m_genericInstHashtabCount == 0)
    {
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
    }
    else
    {
        // @TODO: In the multi-module case we can't ask the modules to do the lookups due to generic type
        // unification. If we want to gain the perf benefit here we'll need to build a similar hash table over
        // the unified GIDs (it will be a little more complex and less compact than the standalone version
        // since this hash will be dynamically sized). For now we perform a linear search through all the
        // unified GIDs.
        CrstHolder hashLock(&m_genericInstHashtabLock);

        UnifiedGenericInstance * pInst = NULL;

        for (UInt32 i = 0; i < m_genericInstHashtabCount; i++)
        {
            pInst = m_genericInstHashtab[i];
            for (; pInst; pInst = pInst->m_pNext)
            {
                if (pInst->GetGid()->GetEEType() == pEEType)
                    return pInst->GetGid();
            }
        }
    }

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
    return GetRuntimeInstance()->GetGenericInstantiation(pEEType,
                                                         pArity,
                                                         ppInstantiation,
                                                         ppVarianceInfo);
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

/// FUNCTION IS OBSOLETE AND NOT EXPECTED TO BE USED IN NEW CODE
COOP_PINVOKE_HELPER(PTR_VOID *, RhGetDictionary, (EEType * pEEType))
{
    // Dictionary slot is the first vtable slot

    EEType * pBaseType = pEEType->get_BaseType();
    UInt16 dictionarySlot = (pBaseType != NULL) ? pBaseType->GetNumVtableSlots() : 0;

    return (PTR_VOID *)(*(PTR_VOID *)(pEEType->get_SlotPtr(dictionarySlot)));
}

/// FUNCTION IS OBSOLETE AND NOT EXPECTED TO BE USED IN NEW CODE
COOP_PINVOKE_HELPER(void, RhSetDictionary, (EEType *     pEEType,
                                            EEType *     pEETypeOfDictionary,
                                            PTR_VOID *   pDictionary))
{
    ASSERT(pEEType->IsDynamicType());

    // Update the base type's vtable slot in pEEType's vtable to point to the 
    // new dictionary
    EEType * pBaseType = pEETypeOfDictionary->get_BaseType();
    UInt16 dictionarySlot = (pBaseType != NULL) ? pBaseType->GetNumVtableSlots() : 0;
    *(PTR_VOID *)(pEEType->get_SlotPtr(dictionarySlot)) = pDictionary;
}

/// FUNCTION IS OBSOLETE AND NOT EXPECTED TO BE USED IN NEW CODE
COOP_PINVOKE_HELPER(EEType *, RhCloneType, (EEType *        pTemplate,
                                            UInt32          arity,
                                            UInt32          nonGcStaticDataSize,
                                            UInt32          nonGCStaticDataOffset,
                                            UInt32          gcStaticDataSize,
                                            UInt32          threadStaticsOffset,
                                            StaticGcDesc *  pGcStaticsDesc,
                                            StaticGcDesc *  pThreadStaticsDesc,
                                            UInt32          hashcode))
{
    // In some situations involving arrays we can find as a template a dynamically generated type.
    // In that case, the correct template would be the template used to create the dynamic type in the first
    // place.
    if (pTemplate->IsDynamicType())
    {
        pTemplate = pTemplate->get_DynamicTemplateType();
    }

    OptionalFieldsRuntimeBuilder optionalFields;

    optionalFields.Decode(pTemplate->get_OptionalFields());

    optionalFields.m_rgFields[OFT_RareFlags].m_fPresent = true;
    optionalFields.m_rgFields[OFT_RareFlags].m_uiValue |= EEType::IsDynamicTypeFlag;

    // Remove the NullableTypeViaIATFlag flag
    optionalFields.m_rgFields[OFT_RareFlags].m_uiValue &= ~EEType::NullableTypeViaIATFlag;

    optionalFields.m_rgFields[OFT_DispatchMap].m_fPresent = false; // Dispatch map is fetched from template

    // Compute size of optional fields encoding
    UInt32 cbOptionalFieldsSize = optionalFields.EncodingSize();

    UInt32 cbEEType = EEType::GetSizeofEEType(pTemplate->GetNumVtableSlots(),
                                              pTemplate->GetNumInterfaces(),
                                              pTemplate->HasFinalizer(),
                                              true /* optional fields are always present */,
                                              pTemplate->IsNullable(),
                                              false /* sealed virtual slots come from template */);

    UInt32 cbGCDesc = RedhawkGCInterface::GetGCDescSize(pTemplate);

    UInt32 cbGCDescAligned = (UInt32)ALIGN_UP(cbGCDesc, sizeof(void *));

    // Safe arithmetics note:
    //  - cbGCDescAligned should typically not exceed 16 MB, which is certainly big enough for anything that actually works
    //  - cbEEType should never exceed 1 GB (number based on type with 65535 interface, 65535 virtual method slots, and 64-bit pointer sizes)
    //  - cbOptionalFieldsSize is quite small (6 flags to encode, at most 5 bytes per flag).
    // Adding up these numbers should never exceed the max value of a signed Int32, so there is no need to use safe integers here.
    // A simple check is enough to catch out of range values.
    // Note: this function will be removed soon after RTM and moved to the managed size, where checked integers can be used
    if (cbOptionalFieldsSize >= 200 || cbEEType >= 2000000000 || cbGCDescAligned >= 0x1000000)
    {
        ASSERT_UNCONDITIONALLY("Invalid sizes for dynamic type detected.");
        RhFailFast();
    }

    NewArrayHolder<UInt8> pEETypeMemory = new (nothrow) UInt8[cbGCDescAligned + cbEEType + sizeof(EEType *)+cbOptionalFieldsSize];
    if (pEETypeMemory == NULL)
        return NULL;

    EEType * pEEType = (EEType *)((UInt8*)pEETypeMemory + cbGCDescAligned);

    UInt32 cbTemplate = EEType::GetSizeofEEType(pTemplate->GetNumVtableSlots(),
                                                pTemplate->GetNumInterfaces(),
                                                pTemplate->HasFinalizer(),
                                                false /* optional fields will be updated later - no need to copy it from template */,
                                                false /* nullable type will be updated later - no need to copy it from template */,
                                                false /* sealed virtual slots are not present on dynamic types */);

    memcpy(((UInt8 *)pEEType) - cbGCDesc, ((UInt8 *)pTemplate) - cbGCDesc, cbGCDesc + cbTemplate);

    OptionalFields * pOptionalFields = (OptionalFields *)((UInt8 *)pEEType + cbEEType + sizeof(EEType *));

    // Encode the optional fields for real
    UInt32 cbActualOptionalFieldsSize;
    cbActualOptionalFieldsSize = optionalFields.Encode(pOptionalFields);
    ASSERT(cbActualOptionalFieldsSize == cbOptionalFieldsSize);

    pEEType->set_OptionalFields(pOptionalFields);

    pEEType->set_DynamicTemplateType(pTemplate);

    pEEType->SetHashCode(hashcode);

    if (pEEType->IsGeneric())
    {
        NewArrayHolder<UInt32> pGenericVarianceFlags = NULL;

        if (pTemplate->HasGenericVariance())
        {
            GenericInstanceDesc * pTemplateGid = GetRuntimeInstance()->LookupGenericInstance(pTemplate);
            ASSERT(pTemplateGid != NULL && pTemplateGid->HasInstantiation() && pTemplateGid->HasVariance());
            
            pGenericVarianceFlags = new (nothrow) UInt32[arity];
            if (pGenericVarianceFlags == NULL) return NULL;

            for (UInt32 i = 0; i < arity; i++)
                ((UInt32*)pGenericVarianceFlags)[i] = (UInt32)pTemplateGid->GetParameterVariance(i);
        }

        if (!GetRuntimeInstance()->CreateGenericInstanceDesc(pEEType, pTemplate, arity, nonGcStaticDataSize, nonGCStaticDataOffset, gcStaticDataSize, threadStaticsOffset, pGcStaticsDesc, pThreadStaticsDesc, (UInt32*)pGenericVarianceFlags))
            return NULL;
    }

    pEETypeMemory.SuppressRelease();

    return pEEType;
}

/// FUNCTION IS OBSOLETE AND NOT EXPECTED TO BE USED IN NEW CODE
COOP_PINVOKE_HELPER(PTR_VOID, RhAllocateMemory, (UInt32 size))
{
    // Generic memory allocation function, for use by managed code
    // Note: all callers to RhAllocateMemory on the managed side use checked integer arithmetics to catch overflows,
    // so there is no need to use safe integers here.
    PTR_VOID pMemory = new (nothrow) UInt8[size];
    if (pMemory == NULL)
        return NULL;

#ifdef _DEBUG
    memset(pMemory, 0, size * sizeof(UInt8));
#endif

    return pMemory;
}

/// FUNCTION IS OBSOLETE AND NOT EXPECTED TO BE USED IN NEW CODE
COOP_PINVOKE_HELPER(void, RhSetRelatedParameterType, (EEType * pEEType,
                                                  EEType * pRelatedParamterType))
{
    pEEType->set_RelatedParameterType(pRelatedParamterType);
}

/// FUNCTION IS OBSOLETE AND NOT EXPECTED TO BE USED IN NEW CODE
COOP_PINVOKE_HELPER(void, RhSetNullableType, (EEType * pEEType, EEType * pTheT))
{
    pEEType->SetNullableType(pTheT);
}

/// FUNCTION IS OBSOLETE AND NOT EXPECTED TO BE USED IN NEW CODE
COOP_PINVOKE_HELPER(bool , RhCreateGenericInstanceDescForType, (EEType *        pEEType,
                                                                UInt32          arity,
                                                                UInt32          nonGcStaticDataSize,
                                                                UInt32          nonGCStaticDataOffset,
                                                                UInt32          gcStaticDataSize,
                                                                UInt32          threadStaticsOffset,
                                                                StaticGcDesc *  pGcStaticsDesc,
                                                                StaticGcDesc *  pThreadStaticsDesc))
{
    ASSERT(pEEType->IsDynamicType());

    EEType * pTemplateType = pEEType->get_DynamicTemplateType();

    NewArrayHolder<UInt32> pGenericVarianceFlags = NULL;

    if (pTemplateType->HasGenericVariance())
    {
        GenericInstanceDesc * pTemplateGid = GetRuntimeInstance()->LookupGenericInstance(pTemplateType);
        ASSERT(pTemplateGid != NULL && pTemplateGid->HasInstantiation() && pTemplateGid->HasVariance());

        pGenericVarianceFlags = new (nothrow) UInt32[arity];
        if (pGenericVarianceFlags == NULL) return false;

        for (UInt32 i = 0; i < arity; i++)
            ((UInt32*)pGenericVarianceFlags)[i] = (UInt32)pTemplateGid->GetParameterVariance(i);
    }

    return GetRuntimeInstance()->CreateGenericInstanceDesc(pEEType, pTemplateType, arity, nonGcStaticDataSize, nonGCStaticDataOffset, gcStaticDataSize,
        threadStaticsOffset, pGcStaticsDesc, pThreadStaticsDesc, pGenericVarianceFlags);
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
