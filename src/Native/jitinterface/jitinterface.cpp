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
    void*                       runtimeLookupArgs;
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

    virtual void STDMETHODCALLTYPE QueryInterface() { NotImplemented(); }
    virtual void STDMETHODCALLTYPE AddRef() { NotImplemented(); }
    virtual void STDMETHODCALLTYPE Release() { NotImplemented(); }

    // JIT only ever uses IEEMemoryManager::ClrVirtualAlloc/IEEMemoryManager::ClrVirtualFree

    virtual void * STDMETHODCALLTYPE ClrVirtualAlloc(void * lpAddress, size_t dwSize, uint32_t flAllocationType, uint32_t flProtect)
    {
        return malloc(dwSize);
    }

    virtual uint32_t STDMETHODCALLTYPE ClrVirtualFree(void * lpAddress, size_t dwSize, uint32_t dwFreeType)
    {
        free(lpAddress);
        return 1;
    }

    virtual void STDMETHODCALLTYPE ClrVirtualQuery() { NotImplemented(); }
    virtual void STDMETHODCALLTYPE ClrVirtualProtect() { NotImplemented(); }
    virtual void STDMETHODCALLTYPE ClrGetProcessHeap() { NotImplemented(); }
    virtual void STDMETHODCALLTYPE ClrHeapCreate() { NotImplemented(); }
    virtual void STDMETHODCALLTYPE ClrHeapDestroy() { NotImplemented(); }
    virtual void STDMETHODCALLTYPE ClrHeapAlloc() { NotImplemented(); }
    virtual void STDMETHODCALLTYPE ClrHeapFree() { NotImplemented(); }
    virtual void STDMETHODCALLTYPE ClrHeapValidate() { NotImplemented(); }
    virtual void STDMETHODCALLTYPE ClrGetProcessExecutableHeap() { NotImplemented(); }
};

static EEMemoryManager eeMemoryManager;

void* JitInterfaceWrapper::getMemoryManager()
{
    return &eeMemoryManager;
}
