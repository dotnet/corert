// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <stdlib.h>
#include <stdio.h>
#include <windows.h>


extern "C" __declspec(dllexport) int __stdcall Square(int intValue)
{
    return intValue * intValue;
}

extern "C" __declspec(dllexport) int __stdcall IsTrue(bool value)
{
    if (value == true)
        return 1;
    return 0;
}

extern "C" __declspec(dllexport) int __stdcall CheckIncremental(int *array, int sz)
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

extern "C" __declspec(dllexport) int __stdcall Inc(int *val)
{
    if (val == NULL)
        return -1;

    *val = *val + 1;
    return 0;
}

extern "C" __declspec(dllexport) int __stdcall VerifyAnsiString(char *val)
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

    return *p == NULL  &&  *q == NULL;
}

extern "C" __declspec(dllexport) int __stdcall VerifyUnicodeString(wchar_t *val)
{
    if (val == NULL)
        return 1;

    wchar_t expected[] = L"Hello World";
    wchar_t *p = expected;
    wchar_t *q = val;

    while (*p  && *q && *p == *q)
    {
        p++;
        q++;
    }

    return *p == NULL  &&  *q == NULL;
}

extern "C" __declspec(dllexport) bool __stdcall SafeHandleTest(HANDLE sh, int shValue)
{
    return (int)sh == shValue;
}

