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
#include "TypeManager.h"

/* static */
TypeManager * TypeManager::Create(void * pModuleHeader)
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

    return new (nothrow) TypeManager(pReadyToRunHeader);
}

TypeManager::TypeManager(ReadyToRunHeader * pHeader)
    : m_pHeader(pHeader), m_pDispatchMapTable(nullptr)
{
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
        return (int)((PTR_UInt8)End - (PTR_UInt8)Start);
    }
    else
    {
        return sizeof(void*);
    }
}
