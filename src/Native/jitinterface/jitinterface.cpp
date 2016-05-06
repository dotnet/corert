// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <stdarg.h>
#include <stdlib.h>
#include <stdint.h>

#include "dllexport.h"
#include "jitinterface.h"

static void NotImplemented()
{
    abort();
}

enum CORINFO_RUNTIME_LOOKUP_KIND { };
struct CORINFO_LOOKUP_KIND
{
    bool                        needsRuntimeLookup;
    CORINFO_RUNTIME_LOOKUP_KIND runtimeLookupKind;
    unsigned short              runtimeLookupFlags;
};

int JitInterfaceWrapper::FilterException(void* pExceptionPointers)
{
    NotImplemented();
    return 1; // EXCEPTION_EXECUTE_HANDLER
}

void JitInterfaceWrapper::HandleException(void* pExceptionPointers)
{
    NotImplemented();
}

bool JitInterfaceWrapper::runWithErrorTrap(void* function, void* parameter)
{
    typedef void(*pfn)(void*);
    try
    {
        (*(pfn)function)(parameter);
    }
    catch (CorInfoException *)
    {
        return false;
    }
    return true;
}

CORINFO_LOOKUP_KIND JitInterfaceWrapper::getLocationOfThisType(void* context)
{
    CorInfoException* pException = nullptr;
    CORINFO_LOOKUP_KIND _ret;
    _callbacks->getLocationOfThisType(_thisHandle, &pException, &_ret, context);
    if (pException != nullptr)
    {
        throw pException;
    }
    return _ret;
}

class EEMemoryManager
{
public:
    EEMemoryManager()
    {
    }

    virtual void __stdcall QueryInterface() { NotImplemented(); }
    virtual void __stdcall AddRef() { NotImplemented(); }
    virtual void __stdcall Release() { NotImplemented(); }

    // JIT only ever uses IEEMemoryManager::ClrVirtualAlloc/IEEMemoryManager::ClrVirtualFree

    virtual void * __stdcall ClrVirtualAlloc(void * lpAddress, size_t dwSize, uint32_t flAllocationType, uint32_t flProtect)
    {
        return malloc(dwSize);
    }

    virtual uint32_t __stdcall ClrVirtualFree(void * lpAddress, size_t dwSize, uint32_t dwFreeType)
    {
        free(lpAddress);
        return 1;
    }

    virtual void __stdcall ClrVirtualQuery() { NotImplemented(); }
    virtual void __stdcall ClrVirtualProtect() { NotImplemented(); }
    virtual void __stdcall ClrGetProcessHeap() { NotImplemented(); }
    virtual void __stdcall ClrHeapCreate() { NotImplemented(); }
    virtual void __stdcall ClrHeapDestroy() { NotImplemented(); }
    virtual void __stdcall ClrHeapAlloc() { NotImplemented(); }
    virtual void __stdcall ClrHeapFree() { NotImplemented(); }
    virtual void __stdcall ClrHeapValidate() { NotImplemented(); }
    virtual void __stdcall ClrGetProcessExecutableHeap() { NotImplemented(); }
};

static EEMemoryManager eeMemoryManager;

void* JitInterfaceWrapper::getMemoryManager()
{
    return &eeMemoryManager;
}
