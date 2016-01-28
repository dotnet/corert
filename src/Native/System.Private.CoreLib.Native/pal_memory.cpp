// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#include <stdlib.h>

extern "C" void * CoreLibNative_MemAlloc(size_t size)
{
    return malloc(size);
}

extern "C" void * CoreLibNative_MemReAlloc(void *ptr, size_t size)
{
    return realloc(ptr, size);
}

extern "C" void CoreLibNative_MemFree(void *ptr)
{
    free(ptr);
}
