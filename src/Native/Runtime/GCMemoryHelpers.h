//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

//
// Unmanaged GC memory helpers
//

void GCSafeZeroMemory(void * dest, size_t len);
void GCSafeCopyMemoryWithWriteBarrier(void * dest, const void *src, size_t len);

EXTERN_C void REDHAWK_CALLCONV RhpBulkWriteBarrier(void* pMemStart, UInt32 cbMemSize);
