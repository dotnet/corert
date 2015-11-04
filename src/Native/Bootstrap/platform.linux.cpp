// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <cstdlib>
#include <cstring>

int UTF8ToWideCharLen(char* bytes, int len)
{
    return strlen(bytes);
}

int UTF8ToWideChar(char* bytes, int len, unsigned short* buffer, int bufLen)
{
    return mbtowc((wchar_t*)buffer, bytes, bufLen);
}

