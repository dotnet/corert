// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// Unmanaged GC memory helpers
//

void GCSafeZeroMemory(void * dest, size_t len);
void GCSafeCopyMemoryWithWriteBarrier(void * dest, const void *src, size_t len);

EXTERN_C void REDHAWK_CALLCONV RhpBulkWriteBarrier(void* pMemStart, UInt32 cbMemSize);
