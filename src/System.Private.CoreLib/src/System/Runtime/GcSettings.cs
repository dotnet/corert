// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Security;

namespace System.Runtime
{
    public enum GCLargeObjectHeapCompactionMode
    {
        Default = 1,
        CompactOnce = 2
    }

    // This is the same format as in clr\src\vm\gcpriv.h
    // make sure you change that one if you change this one!
    public enum GCLatencyMode
    {
        Batch = 0,
        Interactive = 1,
        LowLatency = 2,
        SustainedLowLatency = 3
    }

    public static class GCSettings
    {
        public static GCLatencyMode LatencyMode
        {
            get
            {
                return RuntimeImports.RhGetGcLatencyMode();
            }
            [SecurityCritical] // required to match contract
            set
            {
                if ((value < GCLatencyMode.Batch) || (value > GCLatencyMode.SustainedLowLatency))
                {
                    throw new ArgumentOutOfRangeException(SR.ArgumentOutOfRange_Enum);
                }

                RuntimeImports.RhSetGcLatencyMode(value);
            }
        }

        public static GCLargeObjectHeapCompactionMode LargeObjectHeapCompactionMode
        {
            get
            {
                return (GCLargeObjectHeapCompactionMode)(RuntimeImports.RhGetLohCompactionMode());
            }

            // We don't want to allow this API when hosted.
            [SecurityCritical] // required to match contract
            set
            {
                if ((value < GCLargeObjectHeapCompactionMode.Default) ||
                    (value > GCLargeObjectHeapCompactionMode.CompactOnce))
                {
                    throw new ArgumentOutOfRangeException(SR.ArgumentOutOfRange_Enum);
                }
                Contract.EndContractBlock();

                RuntimeImports.RhSetLohCompactionMode((int)value);
            }
        }

        public static bool IsServerGC
        {
            get
            {
                return RuntimeImports.RhIsServerGc();
            }
        }
    }
}
