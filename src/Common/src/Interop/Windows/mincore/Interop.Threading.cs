// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal static partial class mincore
    {
        [DllImport("api-ms-win-core-synch-l1-1-0.dll", EntryPoint = "CreateEventExW", CharSet = CharSet.Unicode)]
        internal extern static IntPtr CreateEventEx(IntPtr lpEventAttributes, string lpName, uint dwFlags, uint dwDesiredAccess);

        [DllImport("api-ms-win-core-synch-l1-1-0.dll", EntryPoint = "CreateSemaphoreExW", CharSet = CharSet.Unicode)]
        internal static extern IntPtr CreateSemaphoreEx(IntPtr lpSemaphoreAttributes, int lInitialCount, int lMaximumCount, string lpName, uint dwFlags, uint dwDesiredAccess);

        [DllImport("api-ms-win-core-synch-l1-1-0.dll", EntryPoint = "CreateMutexExW", CharSet = CharSet.Unicode)]
        internal extern static IntPtr CreateMutexEx(IntPtr lpMutexAttributes, string lpName, uint dwFlags, uint dwDesiredAccess);

        [DllImport("api-ms-win-core-synch-l1-1-0.dll", EntryPoint = "OpenEventW", CharSet = CharSet.Unicode)]
        private extern static IntPtr OpenEvent(uint dwDesiredAccess, int bInheritHandle, string lpName);

        internal static IntPtr OpenEvent(uint dwDesiredAccess, bool bInheritHandle, string lpName)
        {
            return OpenEvent(dwDesiredAccess, bInheritHandle ? 1 : 0, lpName);
        }

        [DllImport("api-ms-win-core-synch-l1-1-0.dll", EntryPoint = "OpenSemaphoreW", CharSet = CharSet.Unicode)]
        internal extern static IntPtr OpenSemaphore(uint dwDesiredAccess, bool bInheritHandle, string lpName);

        [DllImport("api-ms-win-core-synch-l1-1-0.dll", EntryPoint = "OpenMutexW", CharSet = CharSet.Unicode)]
        internal extern static IntPtr OpenMutex(uint dwDesiredAccess, bool bInheritHandle, string lpName);

        [DllImport("api-ms-win-core-synch-l1-1-0.dll")]
        internal extern static bool ResetEvent(IntPtr hEvent);

        [DllImport("api-ms-win-core-synch-l1-1-0.dll")]
        internal extern static bool SetEvent(IntPtr hEvent);

        [DllImport("api-ms-win-core-synch-l1-1-0.dll")]
        internal extern static bool ReleaseSemaphore(IntPtr hSemaphore, int lReleaseCount, out int lpPreviousCount);

        [DllImport("api-ms-win-core-synch-l1-1-0.dll")]
        internal extern static bool ReleaseMutex(IntPtr hMutex);

        [DllImport("api-ms-win-core-synch-l1-1-0.dll")]
        internal extern static uint WaitForMultipleObjectsEx(uint nCount, IntPtr lpHandles, bool bWaitAll, uint dwMilliseconds, bool bAlertable);

        [DllImport("api-ms-win-core-synch-l1-2-0.dll")]
        internal extern static uint SignalObjectAndWait(IntPtr hObjectToSignal, IntPtr hObjectToWaitOn, uint dwMilliseconds, bool bAlertable);

        [DllImport("api-ms-win-core-synch-l1-2-0.dll")]
        internal extern static void Sleep(uint milliseconds);
    }
}
