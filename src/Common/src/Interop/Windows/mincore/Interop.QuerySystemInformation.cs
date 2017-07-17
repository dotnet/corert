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

        internal enum SystemInformationClass
        {
            SystemProcessorPerformanceInformation = 8
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct SystemProcessorPerformanceInformation
        {
            public long IdleTime;
            public long KernelTime;
            public long UserTime;
            public long DpcTime;
            public long InterruptTime;
            public uint InterruptCount;
        }
        
        // We have to dynamically load the address of NtQuerySystemInformation because the symbol is not exported from NtDll.dll.
        // In RyuJIT-compiled code, a regular P/Invoke works perfectly since it dynamically loads the symbol anyway.
        // However, with C++ code-gen, a regular P/Invoke compiles to a static call, which fails in linking since the function is not exported.
        // So, we have to manually get the proc address and get the delegate at runtime ourselves to compile and link in the C++ code-gen scenario.

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal unsafe delegate int QuerySystemInformationDelegate(SystemInformationClass SystemInformationClass, void* SystemInformation, int SystemInformationLength, out uint returnLength);

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
