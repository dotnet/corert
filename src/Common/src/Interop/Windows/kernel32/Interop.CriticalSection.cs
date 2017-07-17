// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

internal partial class Interop
{
    internal partial class Kernel32
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct CRITICAL_SECTION
        {
            private IntPtr DebugInfo;
            private int LockCount;
            private int RecursionCount;
            private IntPtr OwningThread;
            private IntPtr LockSemaphore;
            private uint SpinCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct CONDITION_VARIABLE
        {
            private IntPtr Ptr;
        }

        [DllImport(Libraries.Kernel32)]
        internal static extern void InitializeCriticalSection(out CRITICAL_SECTION lpCriticalSection);

        [DllImport(Libraries.Kernel32)]
        internal static extern void EnterCriticalSection(ref CRITICAL_SECTION lpCriticalSection);

        [DllImport(Libraries.Kernel32)]
        internal static extern void LeaveCriticalSection(ref CRITICAL_SECTION lpCriticalSection);

        [DllImport(Libraries.Kernel32)]
        internal static extern void DeleteCriticalSection(ref CRITICAL_SECTION lpCriticalSection);
        
        [DllImport(Libraries.Kernel32)]
        internal static extern void InitializeConditionVariable(out CONDITION_VARIABLE ConditionVariable);

        [DllImport(Libraries.Kernel32, SetLastError = true)]
        internal static extern void WakeConditionVariable(ref CONDITION_VARIABLE ConditionVariable);

        [DllImport(Libraries.Kernel32, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SleepConditionVariableCS(ref CONDITION_VARIABLE ConditionVariable, ref CRITICAL_SECTION CriticalSection, int dwMilliseconds);
    }
}
