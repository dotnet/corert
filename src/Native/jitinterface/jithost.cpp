// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <stdlib.h>

#include "dllexport.h"

class JitHost
{
public:
    virtual void* allocateMemory(size_t size, bool usePageAllocator = false)
    {
        return malloc(size);
    }

    virtual void freeMemory(void* block, bool usePageAllocator = false)
    {
        free(block);
    }

    virtual int getIntConfigValue(
        const wchar_t* name, 
        int defaultValue
        )
    {
        return defaultValue;
    }

    virtual const wchar_t* getStringConfigValue(
        const wchar_t* name
        )
    {
        return nullptr;
    }

    virtual void freeStringConfigValue(
        const wchar_t* value
        )
    {
    }
};

static JitHost instance;

DLL_EXPORT void* __stdcall GetJitHost()
{
    return &instance;
}
