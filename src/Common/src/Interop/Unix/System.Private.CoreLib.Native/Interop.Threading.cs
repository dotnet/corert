// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal unsafe partial class Sys
    {
        [DllImport(Libraries.CoreLibNative, EntryPoint = "CoreLibNative_LowLevelMutex_Acquire")]
        internal static extern void LowLevelMutex_Acquire(IntPtr mutex);

        [DllImport(Libraries.CoreLibNative, EntryPoint = "CoreLibNative_LowLevelMutex_Release")]
        internal static extern void LowLevelMutex_Release(IntPtr mutex);

        [DllImport(Libraries.CoreLibNative, EntryPoint = "CoreLibNative_LowLevelMonitor_New")]
        internal static extern IntPtr LowLevelMonitor_New();

        [DllImport(Libraries.CoreLibNative, EntryPoint = "CoreLibNative_LowLevelMonitor_Delete")]
        internal static extern void LowLevelMonitor_Delete(IntPtr monitor);

        [DllImport(Libraries.CoreLibNative, EntryPoint = "CoreLibNative_LowLevelMonitor_Wait")]
        internal static extern void LowLevelMonitor_Wait(IntPtr monitor);

        [DllImport(Libraries.CoreLibNative, EntryPoint = "CoreLibNative_LowLevelMonitor_TimedWait")]
        internal static extern bool LowLevelMonitor_TimedWait(IntPtr monitor, int timeoutMilliseconds);

        [DllImport(Libraries.CoreLibNative, EntryPoint = "CoreLibNative_LowLevelMonitor_Signal_Release")]
        internal static extern void LowLevelMonitor_Signal_Release(IntPtr monitor);

        internal delegate IntPtr ThreadProc(IntPtr parameter);

        [DllImport(Libraries.CoreLibNative, EntryPoint = "CoreLibNative_RuntimeThread_CreateThread")]
        internal static extern bool RuntimeThread_CreateThread(IntPtr stackSize, IntPtr startAddress, IntPtr parameter);
    }
}
