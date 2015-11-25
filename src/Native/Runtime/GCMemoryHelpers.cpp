//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

//
// Unmanaged GC memory helpers
//

#include "common.h"
#include "gcenv.h"
#include "PalRedhawkCommon.h"

#include "GCMemoryHelpers.h"
#include "GCMemoryHelpers.inl"

#ifdef CORERT
void RhpBulkWriteBarrier(void* pMemStart, UInt32 cbMemSize)
{
    InlinedBulkWriteBarrier(pMemStart, cbMemSize);
}
#endif // CORERT
