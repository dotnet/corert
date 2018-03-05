// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#ifdef _WIN32
#include "windows.h"
#else
#include "dlfcn.h"
#endif
#include "stdio.h"
#include "string.h"

#ifndef _WIN32
#define __stdcall
#endif

// typedef for shared lib exported methods
typedef int(__stdcall *f_ReturnsPrimitiveInt)();
typedef bool(__stdcall *f_ReturnsPrimitiveBool)();
typedef char(__stdcall *f_ReturnsPrimitiveChar)();
typedef void(__stdcall *f_EnsureManagedClassLoaders)();

#ifdef _WIN32
int main()
#else
int main(int argc, char* argv[])
#endif
{
#ifdef _WIN32
    HINSTANCE handle = LoadLibrary("SharedLibrary.dll");
#elif __APPLE__
    void *handle = dlopen(strcat(argv[0], ".dylib"), RTLD_LAZY);
#else
    void *handle = dlopen(strcat(argv[0], ".so"), RTLD_LAZY);
#endif

    if (!handle)
        return 1;

#ifdef _WIN32
    f_ReturnsPrimitiveInt returnsPrimitiveInt = (f_ReturnsPrimitiveInt)GetProcAddress(handle, "ReturnsPrimitiveInt");
    f_ReturnsPrimitiveBool returnsPrimitiveBool = (f_ReturnsPrimitiveBool)GetProcAddress(handle, "ReturnsPrimitiveBool");
    f_ReturnsPrimitiveChar returnsPrimitiveChar = (f_ReturnsPrimitiveChar)GetProcAddress(handle, "ReturnsPrimitiveChar");
    f_EnsureManagedClassLoaders ensureManagedClassLoaders = (f_EnsureManagedClassLoaders)GetProcAddress(handle, "EnsureManagedClassLoaders");
#else
    f_ReturnsPrimitiveInt returnsPrimitiveInt = (f_ReturnsPrimitiveInt)dlsym(handle, "ReturnsPrimitiveInt");
    f_ReturnsPrimitiveBool returnsPrimitiveBool = (f_ReturnsPrimitiveBool)dlsym(handle, "ReturnsPrimitiveBool");
    f_ReturnsPrimitiveChar returnsPrimitiveChar = (f_ReturnsPrimitiveChar)dlsym(handle, "ReturnsPrimitiveChar");
    f_EnsureManagedClassLoaders ensureManagedClassLoaders = (f_EnsureManagedClassLoaders)dlsym(handle, "EnsureManagedClassLoaders");
#endif

    if (returnsPrimitiveInt() != 10)
        return 1;

    if (!returnsPrimitiveBool())
        return 1;

    if (returnsPrimitiveChar() != 'a')
        return 1;

    // As long as no unmanaged exception is thrown
    // managed class loaders were initialized successfully
    ensureManagedClassLoaders();

    return 100;
}
