// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System
{
    public static class HighPerformanceCounter
    {
        public static ulong TickCount => Interop.Sys.GetHighPrecisionCount();

        // Cache the frequency on the managed side to avoid the cost of P/Invoke on every access to Frequency
        private static ulong s_frequency;
        public static ulong Frequency => s_frequency == 0 ? s_frequency = Interop.Sys.GetHighPrecisionCounterFrequency() : s_frequency;
    }
}
