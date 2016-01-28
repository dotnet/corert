// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

extern "C" {

void LCMapStringEx(void*, uint32_t, void*, int32_t, intptr_t, int32_t, intptr_t, intptr_t, intptr_t)
{
    throw 42;
}

int32_t WideCharToMultiByte(uint32_t CodePage, uint32_t dwFlags, uint16_t* lpWideCharStr, int32_t cchWideChar, intptr_t lpMultiByteStr, int32_t cbMultiByte, intptr_t lpDefaultChar, intptr_t lpUsedDefaultChar)
{
    throw 42;
}

int32_t MultiByteToWideChar(uint32_t CodePage, uint32_t dwFlags, const uint8_t * lpMultiByteStr, int32_t cbMultiByte, uint16_t* lpWideCharStr, int32_t cchWideChar)
{
    throw 42;
}

void CoTaskMemFree(void* m)
{
    free(m);
}

intptr_t CoTaskMemAlloc(intptr_t size)
{
    return (intptr_t)malloc(size);
}

int32_t CompareStringEx(int16_t*, uint32_t, int16_t*, int32_t, int16_t*, int32_t, void*, void*, intptr_t)
{
	throw 42;
}

int32_t CompareStringOrdinal(int16_t*, int32_t, int16_t*, int32_t, int32_t)
{
	throw 42;
}

int32_t FindNLSStringEx(int16_t*, uint32_t, int16_t*, int32_t, int16_t*, int32_t, int32_t*, void*, void*, intptr_t)
{
	throw 42;
}

int32_t GetLocaleInfoEx(intptr_t, uint32_t, intptr_t, int32_t)
{
	throw 42;
}

int32_t ResolveLocaleName(intptr_t, intptr_t, int32_t)
{
	throw 42;
}

void CoCreateGuid() { throw 42; }
void CoGetApartmentType() { throw 42; }
void CreateEventExW() { throw 42; }
void GetNativeSystemInfo() { throw 42; }

}
