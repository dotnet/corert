// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#pragma once
#include "ModuleHeaders.h"
#include "ICodeManager.h"

struct StaticGcDesc;
class DispatchMap;
typedef unsigned char       UInt8;

class TypeManager
{
    // NOTE: Part of this layout is a contract with the managed side in TypeManagerHandle.cs
    HANDLE                      m_osModule;
    ReadyToRunHeader *          m_pHeader;
    DispatchMap**               m_pDispatchMapTable;
    StaticGcDesc*               m_pStaticsGCInfo;
    StaticGcDesc*               m_pThreadStaticsGCInfo;
    UInt8*                      m_pStaticsGCDataSection;
    UInt8*                      m_pThreadStaticsDataSection;
    UInt32*                     m_pTlsIndex;  // Pointer to TLS index if this module uses thread statics 
    void**                      m_pClasslibFunctions;
    UInt32                      m_nClasslibFunctions;
    UInt32*                     m_pLoopHijackFlag; 

    TypeManager(HANDLE osModule, ReadyToRunHeader * pHeader, void** pClasslibFunctions, UInt32 nClasslibFunctions);

public:
    static TypeManager * Create(HANDLE osModule, void * pModuleHeader, void** pClasslibFunctions, UInt32 nClasslibFunctions);
    void * GetModuleSection(ReadyToRunSectionType sectionId, int * length);
    void EnumStaticGCRefs(void * pfnCallback, void * pvCallbackData);
    HANDLE GetOsModuleHandle();
    void* GetClasslibFunction(ClasslibFunctionId functionId);
    UInt32* GetPointerToTlsIndex() { return m_pTlsIndex; }
    void SetLoopHijackFlag(UInt32 flag) { if (m_pLoopHijackFlag != nullptr) *m_pLoopHijackFlag = flag; }

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
    void EnumThreadStaticGCRefsBlock(void * pfnCallback, void * pvCallbackData, StaticGcDesc* pStaticGcInfo, UInt8* pbThreadStaticData);
};

// TypeManagerHandle represents an AOT module in MRT based runtimes.
// These handles are a pointer to a TypeManager.
struct TypeManagerHandle
{
    static TypeManagerHandle Null()
    {
        TypeManagerHandle handle;
        handle._value = nullptr;
        return handle;
    }

    static TypeManagerHandle Create(TypeManager * value)
    {
        TypeManagerHandle handle;
        handle._value = value;
        return handle;
    }

    void *_value;

    TypeManager* AsTypeManager();
};

