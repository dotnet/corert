// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#pragma once
#include "ModuleHeaders.h"

class DispatchMap;

class ModuleManager
{
    ReadyToRunHeader *          m_pHeader;

    DispatchMap**               m_pDispatchMapTable;

    ModuleManager(ReadyToRunHeader * pHeader);

public:
    static ModuleManager * Create(void * pModuleHeader);
    void * GetModuleSection(ReadyToRunSectionType sectionId, int * length);
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
