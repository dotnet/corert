// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <dlfcn.h>

extern "C" void* CoreLibNative_LoadLibrary(const char* filename)
{
    return dlopen(filename, RTLD_LAZY);
}

extern "C" void* CoreLibNative_GetProcAddress(void* handle, const char* symbol)
{
    // We're not trying to disambiguate between "symbol was not found" and "symbol found, but
    // the value is null". .NET does not define a behavior for DllImports of null entrypoints,
    // so we might as well take the "not found" path on the managed side.
    return dlsym(handle, symbol);
}

extern "C" void CoreLibNative_FreeLibrary(void* handle)
{
    dlclose(handle);
}
