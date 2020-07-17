// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <stdint.h>
#include <errno.h>

extern "C" int32_t CoreLibNative_GetErrNo()
{
    return errno;
}

extern "C" void CoreLibNative_ClearErrNo()
{
    errno = 0;
}
