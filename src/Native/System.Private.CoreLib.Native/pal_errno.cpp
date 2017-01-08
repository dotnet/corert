// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <stdint.h>
#include <errno.h>

extern "C" int32_t CoreLibNative_GetLastErrNo()
{
    return errno;
}

extern "C" void CoreLibNative_SetLastErrNo(int32_t error)
{
    errno = error;
}
