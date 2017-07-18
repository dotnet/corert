// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace System.Threading
{
    internal partial class ClrThreadPool
    {
        private class CpuUtilizationReader
        {
            private struct ProcessCpuInformation
            {
                public ulong idleTime;
                public ulong kernelTime;
                public ulong userTime;
            }

            private ProcessCpuInformation cpuInfo = new ProcessCpuInformation();

            public CpuUtilizationReader()
            {
                GetCpuUtilization(); // Call once to initialize the usage buffer
            }

            private unsafe int GetCpuUtilization()
            {
                if (!Interop.Kernel32.GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
                {
                    int error = Marshal.GetLastWin32Error();
                    var exception = new OutOfMemoryException();
                    exception.SetErrorCode(error);
                    throw exception;
                }

                ulong cpuTotalTime = (userTime - cpuInfo.userTime) + (kernelTime - cpuInfo.kernelTime);
                ulong cpuBusyTime = cpuTotalTime - (idleTime - cpuInfo.idleTime);

                cpuInfo.kernelTime = kernelTime;
                cpuInfo.userTime = userTime;
                cpuInfo.idleTime = idleTime;

                if (cpuTotalTime > 0)
                {
                    ulong reading = cpuBusyTime * 100 / cpuTotalTime;
                    Debug.Assert(0 <= reading && reading <= int.MaxValue);
                    return (int)reading;
                }
                return 0;
            }

            public int CurrentUtilization => GetCpuUtilization();
        }
    }
}
