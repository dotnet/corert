// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace System
{
    internal static class HighPerformanceCounter
    {
        public static ulong TickCount
        {
            get
            {
                long tickCount;
                bool success = Interop.Sys.GetTimestamp(out tickCount);
                Debug.Assert(success);
                return (ulong)tickCount;
            }
        }

        public static ulong Frequency { get; } = GetFrequency();

        private static ulong GetFrequency()
        {
            // Cache the frequency on the managed side to avoid the cost of P/Invoke on every access to Frequency
            long frequency;
            bool success = Interop.Sys.GetTimestampResolution(out frequency);
            Debug.Assert(success);
            return (ulong)frequency;
        }
    }
}
