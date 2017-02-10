// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal static partial class mincore
    {
        [DllImport(Libraries.Kernel32, EntryPoint = "CreateEventExW", CharSet = CharSet.Unicode)]
        internal extern static IntPtr CreateEventEx(IntPtr lpEventAttributes, string lpName, uint dwFlags, uint dwDesiredAccess);

        [DllImport(Libraries.Kernel32, EntryPoint = "CreateSemaphoreExW", CharSet = CharSet.Unicode)]
        internal static extern IntPtr CreateSemaphoreEx(IntPtr lpSemaphoreAttributes, int lInitialCount, int lMaximumCount, string lpName, uint dwFlags, uint dwDesiredAccess);

        [DllImport(Libraries.Kernel32, EntryPoint = "CreateMutexExW", CharSet = CharSet.Unicode)]
        internal extern static IntPtr CreateMutexEx(IntPtr lpMutexAttributes, string lpName, uint dwFlags, uint dwDesiredAccess);

        [DllImport(Libraries.Kernel32, EntryPoint = "OpenEventW", CharSet = CharSet.Unicode)]
        internal extern static IntPtr OpenEvent(uint dwDesiredAccess, bool bInheritHandle, string lpName);

        [DllImport(Libraries.Kernel32, EntryPoint = "OpenSemaphoreW", CharSet = CharSet.Unicode)]
        internal extern static IntPtr OpenSemaphore(uint dwDesiredAccess, bool bInheritHandle, string lpName);

        [DllImport(Libraries.Kernel32, EntryPoint = "OpenMutexW", CharSet = CharSet.Unicode)]
        internal extern static IntPtr OpenMutex(uint dwDesiredAccess, bool bInheritHandle, string lpName);

        [DllImport(Libraries.Kernel32)]
        internal extern static bool ResetEvent(IntPtr hEvent);

        [DllImport(Libraries.Kernel32)]
        internal extern static bool SetEvent(IntPtr hEvent);

        [DllImport(Libraries.Kernel32)]
        internal extern static bool ReleaseSemaphore(IntPtr hSemaphore, int lReleaseCount, out int lpPreviousCount);

        [DllImport(Libraries.Kernel32)]
        internal extern static bool ReleaseMutex(IntPtr hMutex);

        [DllImport(Libraries.Kernel32)]
        internal extern static uint WaitForMultipleObjectsEx(uint nCount, IntPtr lpHandles, bool bWaitAll, uint dwMilliseconds, bool bAlertable);

        [DllImport(Libraries.Kernel32)]
        internal extern static uint SignalObjectAndWait(IntPtr hObjectToSignal, IntPtr hObjectToWaitOn, uint dwMilliseconds, bool bAlertable);

        [DllImport(Libraries.Kernel32)]
        internal extern static void Sleep(uint milliseconds);
    }
}
