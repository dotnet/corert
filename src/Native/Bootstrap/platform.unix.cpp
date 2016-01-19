// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <cstdlib>
#include <cstring>
#include <stdint.h>

int UTF8ToWideCharLen(char* bytes, int len)
{
    return len;
}

int UTF8ToWideChar(char* bytes, int len, unsigned short* buffer, int bufLen)
{
    for (int i = 0; i < len; i++)
        buffer[i] = bytes[i];
    return len;
}

// UNIXTODO: Port System.Private.Interop to Unix https://github.com/dotnet/corert/issues/669
extern "C"
{
    int32_t WideCharToMultiByte(uint32_t CodePage, uint32_t dwFlags, uint16_t* lpWideCharStr, int32_t cchWideChar, intptr_t lpMultiByteStr, int32_t cbMultiByte, intptr_t lpDefaultChar, intptr_t lpUsedDefaultChar)
    {
        throw "WideCharToMultiByte";
    }

    int32_t MultiByteToWideChar(uint32_t CodePage, uint32_t dwFlags, const uint8_t * lpMultiByteStr, int32_t cbMultiByte, uint16_t* lpWideCharStr, int32_t cchWideChar)
    {
        throw "MultiByteToWideChar";
    }

    void CoTaskMemFree(void* m)
    {
        free(m);
    }

    intptr_t CoTaskMemAlloc(intptr_t size)
    {
        return (intptr_t)malloc(size);
    }
}

// UNIXTODO: Unix port of _ecvt_s and _copysign https://github.com/dotnet/corert/issues/670
extern "C"
{
    void _ecvt_s()
    {
        throw "ecvt_s";
    }

    void _copysign()
    {
        throw "_copysign";
    }
}

extern "C"
{
    void CoCreateGuid()
    {
        throw "CoCreateGuid";
    }

    void CoGetApartmentType()
    {
        throw "CoGetApartmentType";
    }

    void CreateEventExW()
    {
        throw "CreateEventExW";
    }

    void GetNativeSystemInfo()
    {
        throw "GetNativeSystemInfo";
    }
}
