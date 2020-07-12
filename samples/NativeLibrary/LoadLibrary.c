// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//On unix make sure to compile using -ldl flag.

//Set this value accordingly to your workspace settings
#define PathToLibrary "./bin/Debug/netstandard2.0/linux-x64/native/NativeLibrary.so"


#ifdef _WIN32
#include "windows.h"
#define symLoad GetProcAddress GetProcAddress
#else
#include "dlfcn.h"
#define symLoad dlsym
#endif
#include <unistd.h>
#include <stdlib.h>
#include <stdio.h>

int callSumFunc(char *path, char *funcName, int a, int b);
char *callSumStringFunc(char *path, char *funcName, char *a, char *b);

int main()
{
    // Check if the library file exists
    if (access(PathToLibrary, F_OK) == -1)
    {
        puts("Couldn't find library at the specified path");
        return 0;
    }

    // Sum two integers
    int sum = callSumFunc(PathToLibrary, "add", 2, 8);
    printf("The sum is %d \n", sum);

    // Concatenate two strings
    char *sumstring = callSumStringFunc(PathToLibrary, "sumstring", "ok", "ko");
    printf("The concatenated string is %s \n", sumstring);

    // Free string
    free(sumstring);
}

int callSumFunc(char *path, char *funcName, int firstInt, int secondInt)
{
    // Call sum function defined in C# shared library
    #ifdef _WIN32
        HINSTANCE handle = LoadLibrary(path);
    #else
        void *handle = dlopen(path, RTLD_LAZY);
    #endif

    typedef int(*myFunc)();
    myFunc MyImport = symLoad(handle, funcName);

    int result = MyImport(firstInt, secondInt);

    // CoreRT libraries do not support unloading
    // See https://github.com/dotnet/corert/issues/7887
    return result;
}

char *callSumStringFunc(char *path, char *funcName, char *firstString, char *secondString)
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
    char *result = MyImport(firstString, secondString);

    // CoreRT libraries do not support unloading
    // See https://github.com/dotnet/corert/issues/7887
    return result;
}
