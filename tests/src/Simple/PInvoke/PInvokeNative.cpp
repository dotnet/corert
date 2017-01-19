// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <stdlib.h>
#include <stdio.h>
#ifdef Windows_NT
#include <windows.h>
#define DLL_EXPORT extern "C" __declspec(dllexport)
#else
#include<errno.h>
#define HANDLE size_t
#define DLL_EXPORT extern "C" __attribute((visibility("default")))
#endif

#if !defined(__stdcall)
#define __stdcall
#endif

DLL_EXPORT int __stdcall Square(int intValue)
{
    return intValue * intValue;
}

DLL_EXPORT int __stdcall IsTrue(bool value)
{
    if (value == true)
        return 1;
    return 0;
}

DLL_EXPORT int __stdcall CheckIncremental(int *array, int sz)
{
    if (array == NULL)
        return 1;

    for (int i = 0; i < sz; i++)
    {
        if (array[i] != i)
            return 1;
    }
    return 0;
}

DLL_EXPORT int __stdcall Inc(int *val)
{
    if (val == NULL)
        return -1;

    *val = *val + 1;
    return 0;
}

DLL_EXPORT int __stdcall VerifyAnsiString(char *val)
{
    if (val == NULL)
        return 1;

    char expected[] = "Hello World";
    char *p = expected;
    char *q = val;

    while (*p  && *q && *p == *q)
    {
        p++;
        q++;
    }

    return *p == 0  &&  *q == 0;
}

DLL_EXPORT int __stdcall VerifyUnicodeString(unsigned short *val)
{
    if (val == NULL)
        return 1;

    unsigned short expected[] = {'H', 'e', 'l', 'l', 'o', ' ', 'W', 'o', 'r', 'l', 'd', 0};
    unsigned short *p = expected;
    unsigned short *q = val;

    while (*p  && *q && *p == *q)
    {
        p++;
        q++;
    }

    return *p == 0  &&  *q == 0;
}

DLL_EXPORT bool __stdcall LastErrorTest()
{
    int lasterror;
#ifdef Windows_NT
    lasterror = GetLastError();
    SetLastError(12345);
#else
    lasterror = errno;
    errno = 12345;
#endif
    return lasterror == 0;
}

DLL_EXPORT void* __stdcall AllocateMemory(int bytes)
{
    void *mem = malloc(bytes);
    return mem;
}

DLL_EXPORT bool __stdcall ReleaseMemory(void *mem)
{
   free(mem);
   return true;
}

DLL_EXPORT bool __stdcall SafeHandleTest(HANDLE sh, long shValue)
{
    return (long)((size_t)(sh)) == shValue;
}

DLL_EXPORT long __stdcall SafeHandleOutTest(HANDLE **sh)
{
    if (sh == NULL) 
        return -1;

    *sh = (HANDLE *)malloc(100);
    return (long)((size_t)(*sh));
}
