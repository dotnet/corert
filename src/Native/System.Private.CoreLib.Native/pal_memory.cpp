//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
#include <stdlib.h>
extern "C" void *MemAlloc(size_t size)
{
	return malloc(size);
}
extern "C" void *MemReAlloc(void *ptr, size_t size)
{
	return realloc(ptr, size);
}
extern "C" void MemFree(void *ptr)
{
	free(ptr);
}





