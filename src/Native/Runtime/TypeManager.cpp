// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"
#include "holder.h"
#include "rhassert.h"
#include "slist.h"
#include "gcrhinterface.h"
#include "shash.h"
#include "RWLock.h"
#include "module.h"
#include "varint.h"
#include "rhbinder.h"
#include "regdisplay.h"
#include "StackFrameIterator.h"
#include "thread.h"
#include "event.h"
#include "threadstore.h"
#include "TypeManager.h"

/* static */
TypeManager * TypeManager::Create(HANDLE osModule, void * pModuleHeader, void** pClasslibFunctions, UInt32 nClasslibFunctions)
{
    ReadyToRunHeader * pReadyToRunHeader = (ReadyToRunHeader *)pModuleHeader;

    // Sanity check the signature magic
    ASSERT(pReadyToRunHeader->Signature == ReadyToRunHeaderConstants::Signature);
    if (pReadyToRunHeader->Signature != ReadyToRunHeaderConstants::Signature)
        return nullptr;

    // Only the current major version is supported currently
    ASSERT(pReadyToRunHeader->MajorVersion == ReadyToRunHeaderConstants::CurrentMajorVersion);
    if (pReadyToRunHeader->MajorVersion != ReadyToRunHeaderConstants::CurrentMajorVersion)
        return nullptr;

    return new (nothrow) TypeManager(osModule, pReadyToRunHeader, pClasslibFunctions, nClasslibFunctions);
}

TypeManager::TypeManager(HANDLE osModule, ReadyToRunHeader * pHeader, void** pClasslibFunctions, UInt32 nClasslibFunctions)
    : m_osModule(osModule), m_pHeader(pHeader), m_pDispatchMapTable(nullptr),
      m_pClasslibFunctions(pClasslibFunctions), m_nClasslibFunctions(nClasslibFunctions)
{
    int length;
    m_pStaticsGCDataSection = (UInt8*)GetModuleSection(ReadyToRunSectionType::GCStaticRegion, &length);
    m_pStaticsGCInfo = (StaticGcDesc*)GetModuleSection(ReadyToRunSectionType::GCStaticDesc, &length);
    m_pThreadStaticsDataSection = (UInt8*)GetModuleSection(ReadyToRunSectionType::ThreadStaticRegion, &length);
    m_pThreadStaticsGCInfo = (StaticGcDesc*)GetModuleSection(ReadyToRunSectionType::ThreadStaticGCDescRegion, &length);
    m_pTlsIndex = (UInt32*)GetModuleSection(ReadyToRunSectionType::ThreadStaticIndex, &length);
    m_pLoopHijackFlag = (UInt32*)GetModuleSection(ReadyToRunSectionType::LoopHijackFlag, &length);
}

void * TypeManager::GetModuleSection(ReadyToRunSectionType sectionId, int * length)
{
    ModuleInfoRow * pModuleInfoRows = (ModuleInfoRow *)(m_pHeader + 1);

    ASSERT(m_pHeader->EntrySize == sizeof(ModuleInfoRow));

    // TODO: Binary search
    for (int i = 0; i < m_pHeader->NumberOfSections; i++)
    {
        ModuleInfoRow * pCurrent = pModuleInfoRows + i;
        if ((int32_t)sectionId == pCurrent->SectionId)
        {
            *length = pCurrent->GetLength();
            return pCurrent->Start;
        }
    }

    *length = 0;
    return nullptr;
}

void * TypeManager::GetClasslibFunction(ClasslibFunctionId functionId)
{
    uint32_t id = (uint32_t)functionId;

    if (id >= m_nClasslibFunctions)
        return nullptr;

    return m_pClasslibFunctions[id];
}

DispatchMap** TypeManager::GetDispatchMapLookupTable()
{
    if (m_pDispatchMapTable == nullptr)
    {
        int length = 0;
        DispatchMap ** pDispatchMapTable = (DispatchMap **)GetModuleSection(ReadyToRunSectionType::InterfaceDispatchTable, &length);
        m_pDispatchMapTable = pDispatchMapTable;
    }

    return m_pDispatchMapTable;
}

bool TypeManager::ModuleInfoRow::HasEndPointer()
{
    return Flags & (int32_t)ModuleInfoFlags::HasEndPointer;
}

int TypeManager::ModuleInfoRow::GetLength()
{
    if (HasEndPointer())
    {
        return (int)((UInt8*)End - (UInt8*)Start);
    }
    else
    {
        return sizeof(void*);
    }
}

void TypeManager::EnumStaticGCRefsBlock(void * pfnCallback, void * pvCallbackData, StaticGcDesc* pStaticGcInfo)
{
    if (pStaticGcInfo == NULL)
        return;

    for (UInt32 idxSeries = 0; idxSeries < pStaticGcInfo->m_numSeries; idxSeries++)
    {
        PTR_StaticGcDescGCSeries pSeries = dac_cast<PTR_StaticGcDescGCSeries>(dac_cast<TADDR>(pStaticGcInfo) +
            offsetof(StaticGcDesc, m_series) + (idxSeries * sizeof(StaticGcDesc::GCSeries)));

        // The m_startOffset field is really 32-bit relocation (IMAGE_REL_BASED_RELPTR32) to the GC static base of the type
        // the GCSeries is describing for. This makes it tolerable to the symbol sorting that the linker conducts.
        PTR_RtuObjectRef    pRefLocation = dac_cast<PTR_RtuObjectRef>(dac_cast<PTR_UInt8>(&pSeries->m_startOffset) + (Int32)pSeries->m_startOffset);
        UInt32              numObjects = pSeries->m_size;

        RedhawkGCInterface::BulkEnumGcObjRef(pRefLocation, numObjects, pfnCallback, pvCallbackData);
    }
}

void TypeManager::EnumThreadStaticGCRefsBlock(void * pfnCallback, void * pvCallbackData, StaticGcDesc* pStaticGcInfo, UInt8* pbThreadStaticData)
{
    if (pStaticGcInfo == NULL)
        return;

    for (UInt32 idxSeries = 0; idxSeries < pStaticGcInfo->m_numSeries; idxSeries++)
    {
        PTR_StaticGcDescGCSeries pSeries = dac_cast<PTR_StaticGcDescGCSeries>(dac_cast<TADDR>(pStaticGcInfo) +
            offsetof(StaticGcDesc, m_series) + (idxSeries * sizeof(StaticGcDesc::GCSeries)));

        // The m_startOffset field is really a 32-bit relocation (IMAGE_REL_SECREL) to the TLS section.
        UInt8* pTlsObject = pbThreadStaticData + pSeries->m_startOffset;
        PTR_RtuObjectRef    pRefLocation = dac_cast<PTR_RtuObjectRef>(pTlsObject);
        UInt32              numObjects = pSeries->m_size;

        RedhawkGCInterface::BulkEnumGcObjRef(pRefLocation, numObjects, pfnCallback, pvCallbackData);
    }
}

void TypeManager::EnumStaticGCRefs(void * pfnCallback, void * pvCallbackData)
{
    // Regular statics.
    EnumStaticGCRefsBlock(pfnCallback, pvCallbackData, m_pStaticsGCInfo);
    
    // Thread local statics.
    if (m_pThreadStaticsGCInfo != NULL)
    {
        FOREACH_THREAD(pThread)
        {
            // To calculate the address of the data for each thread's TLS fields we need two values:
            //  1) The TLS slot index allocated for this module by the OS loader. We keep a pointer to this
            //     value in the module header.
            //  2) The offset into the TLS block at which managed data begins. 
            EnumThreadStaticGCRefsBlock(pfnCallback, pvCallbackData, m_pThreadStaticsGCInfo,
                dac_cast<UInt8*>(pThread->GetThreadLocalStorage(*m_pTlsIndex, 0)));
        }
        END_FOREACH_THREAD
    }
}

HANDLE TypeManager::GetOsModuleHandle()
{
    return m_osModule;
}

bool TypeManagerHandle::IsTypeManager()
{
#if PROJECTN
    if (((uintptr_t)_value & 1) == 0)
        return false;
#endif

    return true;
}

TypeManager* TypeManagerHandle::AsTypeManager()
{
    ASSERT(IsTypeManager());
    return (TypeManager*)(((uint8_t *)_value) - 1);
}

HANDLE TypeManagerHandle::AsOsModule()
{
    ASSERT(!IsTypeManager());
    return (HANDLE)_value;
}

