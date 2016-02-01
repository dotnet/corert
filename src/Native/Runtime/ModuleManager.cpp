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
#include "ModuleManager.h"

/* static */
ModuleManager * ModuleManager::Create(void * pHeaderStart, void * pHeaderEnd)
{
    ASSERT(pHeaderStart < pHeaderEnd);
    if (pHeaderStart >= pHeaderEnd)
        return nullptr;

    NewHolder<ModuleManager> pNewModule = new (nothrow) ModuleManager(pHeaderStart, pHeaderEnd);
    if (nullptr == pNewModule)
        return nullptr;

    pNewModule.SuppressRelease();
    return pNewModule;
}

void * ModuleManager::GetModuleSection(ModuleHeaderSection sectionId, int * length)
{
    void * pSectionStart = nullptr;
    ModuleInfoRow * pCurrent = (ModuleInfoRow *)m_pHeaderStart;

    // TODO: Binary search
    for (; pCurrent < m_pHeaderEnd; pCurrent ++)
    {
        if ((int32_t)sectionId == pCurrent->SectionId)
        {
            pSectionStart = pCurrent->Start;
            *length = pCurrent->GetLength();;
        }
         
    }

    return pSectionStart;
}

DispatchMap** ModuleManager::GetDispatchMapLookupTable()
{
    if (m_pDispatchMapTable == nullptr)
    {
        int length = 0;
        DispatchMap ** pNewDispatchMapTable = (DispatchMap **)GetModuleSection(ModuleHeaderSection::InterfaceDispatchTable, &length);
        PalInterlockedCompareExchangePointer((void **)&m_pDispatchMapTable, pNewDispatchMapTable, nullptr);
    }

    return m_pDispatchMapTable;
}

bool ModuleManager::ModuleInfoRow::HasEndPointer()
{
    return Flags & (int32_t)ModuleInfoFlags::HasEndPointer;
}

int ModuleManager::ModuleInfoRow::GetLength()
{
    if (HasEndPointer())
    {
        return (int)((PTR_UInt8)End - (PTR_UInt8)Start);
    }
    else
    {
        return sizeof(void*);
    }
}
