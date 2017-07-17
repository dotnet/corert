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
        private const int DllSearchPathUseSystem32 = 0x800;

        internal enum SYSTEM_INFORMATION_CLASS
        {
            SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION = 8
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION
        {
            public long IdleTime;
            public long KernelTime;
            public long UserTime;
            public long DpcTime;
            public long InterruptTime;
            public uint InterruptCount;
        }
        
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal unsafe delegate int QuerySystemInformationDelegate(SYSTEM_INFORMATION_CLASS SystemInformationClass, void* SystemInformation, int SystemInformationLength, out uint returnLength);

        private static QuerySystemInformationDelegate s_querySystemInformation;

        internal unsafe static QuerySystemInformationDelegate QuerySystemInformation
        {
            get
            {
                if(s_querySystemInformation == null)
                {
                    IntPtr ntDll = LoadLibraryEx("NtDll.dll", IntPtr.Zero, DllSearchPathUseSystem32);
                    fixed (byte* name = Encoding.ASCII.GetBytes("NtQuerySystemInformation"))
                    {
                        IntPtr entryPoint = GetProcAddress(ntDll, name);
                        s_querySystemInformation = (QuerySystemInformationDelegate)PInvokeMarshal.GetPInvokeDelegateForStub(entryPoint, typeof(QuerySystemInformationDelegate).TypeHandle);
                        if (s_querySystemInformation == null)
                        {
                            Environment.FailFast("NtQuerySystemInformation function not found.", new PlatformNotSupportedException("NtQuerySystemInformation function not found."));
                        }
                    }
                }
                return s_querySystemInformation;
            }
        }
    }
}
