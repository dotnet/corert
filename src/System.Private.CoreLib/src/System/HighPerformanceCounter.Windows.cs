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

        public static ulong Frequency { get; } = GetFrequency();

        private static ulong GetFrequency()
        {
            Interop.Kernel32.QueryPerformanceFrequency(out ulong frequency);
            return frequency;
        }
    }
}
