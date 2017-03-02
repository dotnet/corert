// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Win32.SafeHandles;
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
        internal extern static uint WaitForSingleObject(SafeWaitHandle hHandle, uint dwMilliseconds);

        [DllImport(Libraries.Kernel32)]
        internal extern static uint SignalObjectAndWait(IntPtr hObjectToSignal, IntPtr hObjectToWaitOn, uint dwMilliseconds, bool bAlertable);

        [DllImport(Libraries.Kernel32)]
        internal extern static void Sleep(uint milliseconds);

        [DllImport(Libraries.Kernel32)]
        internal extern static unsafe SafeWaitHandle CreateThread(
            IntPtr lpThreadAttributes,
            IntPtr dwStackSize,
            IntPtr lpStartAddress,
            IntPtr lpParameter,
            uint dwCreationFlags,
            out uint lpThreadId);

        internal delegate uint ThreadProc(IntPtr lpParameter);

        [DllImport(Libraries.Kernel32)]
        internal extern static uint ResumeThread(SafeWaitHandle hThread);

        [DllImport(Libraries.Kernel32)]
        internal extern static IntPtr GetCurrentProcess();

        [DllImport(Libraries.Kernel32)]
        internal extern static IntPtr GetCurrentThread();

        [DllImport(Libraries.Kernel32, SetLastError = true)]
        internal extern static bool DuplicateHandle(
            IntPtr hSourceProcessHandle,
            IntPtr hSourceHandle,
            IntPtr hTargetProcessHandle,
            out SafeWaitHandle lpTargetHandle,
            uint dwDesiredAccess,
            bool bInheritHandle,
            uint dwOptions);

        internal enum ThreadPriority : int
        {
            Idle = -15,
            Lowest = -2,
            BelowNormal = -1,
            Normal = 0,
            AboveNormal = 1,
            Highest = 2,
            TimeCritical = 15,

            ErrorReturn = 0x7FFFFFFF
        }

        [DllImport(Libraries.Kernel32)]
        internal extern static ThreadPriority GetThreadPriority(SafeWaitHandle hThread);

        [DllImport(Libraries.Kernel32)]
        internal extern static bool SetThreadPriority(SafeWaitHandle hThread, int nPriority);
    }
}
