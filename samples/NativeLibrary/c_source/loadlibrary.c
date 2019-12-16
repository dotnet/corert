// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#ifdef _WIN32

#include "windows.h"
#define symLoad GetProcAddress GetProcAddress
#define libClose FreeLibrary
#else
#include "dlfcn.h"
#define symLoad dlsym
#define libClose dlclose
#endif


int callSumFunc(char *path, char *funcName, int a, int b)
{
    // Call sum function defined in C# shared library
    #ifdef _WIN32
        HINSTANCE handle = LoadLibrary(path);
    #else
        void *handle = dlopen(path, RTLD_LAZY);
    #endif

    typedef int(*myFunc)();
    myFunc MyImport = symLoad(handle, funcName);

    int result = MyImport(a, b);
    libClose(handle);
    return result;
}

char *callSumStringFunc(char *path, char *funcName, char *a, char *b)
{
    // Library loading
    #ifdef _WIN32
        HINSTANCE handle = LoadLibrary(path);
    #else
        void *handle = dlopen(path, RTLD_LAZY);
    #endif

    // Declare a typedef
    typedef char *(*myFunc)();

    // Import Symbol named funcName
    myFunc MyImport = symLoad(handle, funcName);

    // The C# function will return a pointer
    char *result = MyImport(a, b);
    libClose(handle);
    return result;
}
