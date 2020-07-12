// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal static partial class Kernel32
    {
        internal const int WAIT_FAILED = unchecked((int)0xFFFFFFFF);

        [DllImport(Libraries.Kernel32)]
        internal extern static uint WaitForMultipleObjectsEx(uint nCount, IntPtr lpHandles, bool bWaitAll, uint dwMilliseconds, bool bAlertable);

        [DllImport(Libraries.Kernel32)]
        internal extern static uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport(Libraries.Kernel32)]
        internal extern static uint SignalObjectAndWait(IntPtr hObjectToSignal, IntPtr hObjectToWaitOn, uint dwMilliseconds, bool bAlertable);

        [DllImport(Libraries.Kernel32)]
        internal extern static void Sleep(uint milliseconds);

        internal const uint CREATE_SUSPENDED = 0x00000004;
        internal const uint STACK_SIZE_PARAM_IS_A_RESERVATION = 0x00010000;

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
        internal extern static IntPtr GetCurrentThread();

        internal const int DUPLICATE_SAME_ACCESS = 2;

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
