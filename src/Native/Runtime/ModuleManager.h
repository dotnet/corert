// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#pragma once
#include "ModuleHeaders.h"

class DispatchMap;

class ModuleManager
{
    void *                      m_pHeaderStart;
    void *                      m_pHeaderEnd;

    DispatchMap**               m_pDispatchMapTable;

    ModuleManager(void * pHeaderStart, void * pHeaderEnd) : m_pHeaderStart(pHeaderStart), m_pHeaderEnd(pHeaderEnd), m_pDispatchMapTable(nullptr) {}

public:
    static ModuleManager * Create(void * pHeaderStart, void * pHeaderEnd);
    void * GetModuleSection(ModuleHeaderSection sectionId, int * length);
    DispatchMap ** GetDispatchMapLookupTable();

private:
    
    struct ModuleInfoRow
    {
        int32_t SectionId;
        int32_t Flags;
        void * Start;
        void * End;

        bool HasEndPointer();
        int GetLength();
    };
};
