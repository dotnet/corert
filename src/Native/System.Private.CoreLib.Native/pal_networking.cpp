// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>
#include <fcntl.h>
#include <unistd.h>
#include <assert.h>
#include <errno.h>
#include "pal_utilities.h"

extern "C" int32_t CoreLibNative_GetHostName(uint8_t* name, int32_t nameLength)
{
    assert(name != nullptr);
    assert(nameLength > 0);

    size_t unsignedSize = UnsignedCast(nameLength);
    return gethostname(reinterpret_cast<char*>(name), unsignedSize);
}


