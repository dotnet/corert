// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

internal partial class Interop
{
    internal partial class Kernel32
    {
        internal enum SYSTEM_INFORMATION_CLASS
        {
            SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION = 8
        }

        internal struct SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION
        {
            // These fields are all assigned by the native call.
#pragma warning disable CS0649
            public long IdleTime;
            public long KernelTime;
            public long UserTime;
            public long DpcTime;
            public long InterruptTime;
            public uint InterruptCount;
#pragma warning restore CS0649
        }
        
        [DllImport(Libraries.Kernel32, EntryPoint = "NtQuerySystemInformation")]
        internal static unsafe extern int QuerySystemInformation(SYSTEM_INFORMATION_CLASS SystemInformationClass, void* SystemInformation, int SystemInformationLength, out uint returnLength);
    }
}
