// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

internal partial class Interop
{
    internal partial class mincore
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
        
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal unsafe delegate void QuerySystemInformationDelegate(SYSTEM_INFORMATION_CLASS SystemInformationClass, void* SystemInformation, int SystemInformationLength, out uint returnLength);

        private static QuerySystemInformationDelegate s_querySystemInformation;

        internal unsafe static QuerySystemInformationDelegate QuerySystemInformation
        {
            get
            {
                if(s_querySystemInformation == null)
                {
                    IntPtr ntDll = LoadLibraryEx("NtDll.dll", IntPtr.Zero, 0);
                    fixed (byte* name = Encoding.ASCII.GetBytes("NtQuerySystemInformation"))
                    {
                        IntPtr entryPoint = GetProcAddress(ntDll, name);
                        s_querySystemInformation = (QuerySystemInformationDelegate)PInvokeMarshal.GetPInvokeDelegateForStub(entryPoint, typeof(QuerySystemInformationDelegate).TypeHandle);
                    }
                }
                return s_querySystemInformation;
            }
        }
    }
}
