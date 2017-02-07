// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#pragma once
#include "ModuleHeaders.h"

struct StaticGcDesc;
class DispatchMap;
typedef unsigned char       UInt8;

class TypeManager
{
    ReadyToRunHeader *          m_pHeader;
    DispatchMap**               m_pDispatchMapTable;
    StaticGcDesc*               m_pStaticsGCInfo;
    StaticGcDesc*               m_pThreadStaticsGCInfo;
    UInt8*                      m_pStaticsGCDataSection;

    TypeManager(ReadyToRunHeader * pHeader);

public:
    static TypeManager * Create(void * pModuleHeader);
    void * GetModuleSection(ReadyToRunSectionType sectionId, int * length);
    DispatchMap ** GetDispatchMapLookupTable();
    void EnumStaticGCRefs(void * pfnCallback, void * pvCallbackData);

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

    void EnumStaticGCRefsBlock(void * pfnCallback, void * pvCallbackData, StaticGcDesc* pStaticGcInfo);
};
