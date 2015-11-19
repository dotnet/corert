// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

//
// Exposes features of the Garbage Collector to managed code.
//
// This is an extremely simple initial version that only exposes a small fraction of the API that the CLR
// version does.
//

namespace System
{
    // !!!!!!!!!!!!!!!!!!!!!!!
    // Make sure you change the def in gc.h if you change this!
    public enum InternalGCCollectionMode
    {
        NonBlocking = 0x00000001,
        Blocking = 0x00000002,
        Optimized = 0x00000004,
    }

    public enum GCLatencyMode
    {
        Batch = 0,
        Interactive = 1,
        LowLatency = 2,
        SustainedLowLatency = 3
    }
}
