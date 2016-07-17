// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <cstdlib>
#include <cstring>
#include <stdint.h>

//
// Temporary stubs for Windows PInvoke functions not ported to Unix yet
//

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

extern "C"
{
    uint32_t GetLastError()
    {
        return 1;
    }

    uint32_t WaitForMultipleObjectsEx(uint32_t, void*, uint32_t, uint32_t, uint32_t)
    {
        throw "WaitForMultipleObjectsEx";
    }

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

    void OutputDebugStringW()
    {
        throw "OutputDebugStringW";
    }

    uint32_t GetCurrentThreadId()
    {
        throw "GetCurrentThreadId";
    }

    uint32_t RhCompatibleReentrantWaitAny(uint32_t alertable, uint32_t timeout, uint32_t count, void* pHandles)
    {
        throw "RhCompatibleReentrantWaitAny";
    }

    void EnumDynamicTimeZoneInformation()
    {
        throw "EnumDynamicTimeZoneInformation";
    }

    void GetDynamicTimeZoneInformation()
    {
        throw "GetDynamicTimeZoneInformation";
    }

    void GetDynamicTimeZoneInformationEffectiveYears()
    {
        throw "GetDynamicTimeZoneInformationEffectiveYears";
    }

    void GetTimeZoneInformationForYear()
    {
        throw "GetTimeZoneInformationForYear";
    }
}
