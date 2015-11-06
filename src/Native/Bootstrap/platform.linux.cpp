// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <cstdlib>
#include <cstring>

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
