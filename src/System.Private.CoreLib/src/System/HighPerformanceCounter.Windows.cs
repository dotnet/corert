// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System
{
    internal static class HighPerformanceCounter
    {
        public static ulong TickCount
        {
            get
            {
                Interop.Kernel32.QueryPerformanceCounter(out ulong counter);
                return counter;
            }
        }

        // Cache the frequency on the managed side to avoid the cost of P/Invoke on every access to Frequency
        private static ulong s_frequency;

        public static ulong Frequency
        {
            get
            {
                if (s_frequency == 0)
                {
                    Interop.Kernel32.QueryPerformanceFrequency(out s_frequency);
                }
                return s_frequency;
            }
        }
    }
}
