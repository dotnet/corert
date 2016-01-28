// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "common.h"
#include "platform.h"
#include "windows.h"

int UTF8ToWideCharLen(char* bytes, int len)
{
    assert(len > 0);
    return MultiByteToWideChar(CP_UTF8, 0, bytes, len, NULL, 0);
}

int UTF8ToWideChar(char* bytes, int len, uint16_t* buffer, int bufLen)
{
    assert(len > 0 && bufLen > 0);
    return MultiByteToWideChar(CP_UTF8, 0, bytes, len, (LPWSTR) buffer, bufLen);
}

