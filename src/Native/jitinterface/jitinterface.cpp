// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "jitinterface.h"
#include "dllexport.h"

enum CORINFO_RUNTIME_LOOKUP_KIND { };
struct CORINFO_LOOKUP_KIND
{
    bool                        needsRuntimeLookup;
    CORINFO_RUNTIME_LOOKUP_KIND runtimeLookupKind;
};

int JitInterfaceWrapper::FilterException(void* pExceptionPointers)
{
    return 1; // EXCEPTION_EXECUTE_HANDLER
}

void JitInterfaceWrapper::HandleException(void* pExceptionPointers)
{
}

CORINFO_LOOKUP_KIND JitInterfaceWrapper::getLocationOfThisType(void* context)
{
    CorInfoException* pException = nullptr;
    CORINFO_LOOKUP_KIND _ret;
    _pCorInfo->getLocationOfThisType(&pException, &_ret, context);
    if (pException != nullptr)
    {
        throw pException;
    }
    return _ret;
}

static JitInterfaceWrapper instance;

DLL_EXPORT void* GetJitInterfaceWrapper(IJitInterface *pCorInfo)
{
    instance._pCorInfo = pCorInfo;
    return &instance;
}
