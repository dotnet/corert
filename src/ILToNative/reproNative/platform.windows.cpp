// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

