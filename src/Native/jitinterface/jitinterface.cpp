//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#include "jitinterface.h"

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

extern "C" void* GetJitInterfaceWrapper(IJitInterface *pCorInfo)
{
    instance._pCorInfo = pCorInfo;
    return &instance;
}
