// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal static partial class mincore
    {
        [DllImport("api-ms-win-core-threadpool-l1-2-0.dll")]
        internal extern static IntPtr CreateThreadpoolTimer(IntPtr pfnti, IntPtr pv, IntPtr pcbe);

        [DllImport("api-ms-win-core-threadpool-l1-2-0.dll")]
        internal extern static unsafe IntPtr SetThreadpoolTimer(IntPtr pti, long* pftDueTime, uint msPeriod, uint msWindowLength);

        internal delegate void TimerCallback(IntPtr Instance, IntPtr Context, IntPtr Timer);
    }
}
