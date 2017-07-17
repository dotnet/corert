// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace System.Threading
{
    internal partial class ClrThreadPool
    {
        private class CpuUtilizationReader
        {
            private struct ProcessCpuInformation
            {
                public long idleTime;
                public long kernelTime;
                public long userTime;
                public int numberOfProcessors;
                public Interop.mincore.SystemProcessorPerformanceInformation[] usageBuffer;
            }

            private ProcessCpuInformation _cpuInfo = new ProcessCpuInformation();

            public CpuUtilizationReader()
            {
                _cpuInfo.numberOfProcessors = ThreadPoolGlobals.processorCount;

                _cpuInfo.usageBuffer = new Interop.mincore.SystemProcessorPerformanceInformation[ThreadPoolGlobals.processorCount];
                GetCpuUtilization(); // Call once to initialize the usage buffer
            }

            private unsafe int GetCpuUtilization()
            {
                fixed (Interop.mincore.SystemProcessorPerformanceInformation* buffer = _cpuInfo.usageBuffer)
                {
                    int status = Interop.mincore.QuerySystemInformation(Interop.mincore.SystemInformationClass.SystemProcessorPerformanceInformation,
                        buffer,
                        sizeof(Interop.mincore.SystemProcessorPerformanceInformation) * _cpuInfo.usageBuffer.Length,
                        out uint returnLength);

                    if (status != 0)
                    {
                        Environment.FailFast($"NtQuerySystemInformation call failed with status {status}");
                    }
                }

                long idleTime = 0;
                long kernelTime = 0;
                long userTime = 0;

                for (long procNumber = 0; procNumber < _cpuInfo.usageBuffer.Length; procNumber++)
                {
                    idleTime += _cpuInfo.usageBuffer[procNumber].IdleTime;
                    kernelTime += _cpuInfo.usageBuffer[procNumber].KernelTime;
                    userTime += _cpuInfo.usageBuffer[procNumber].UserTime;
                }

                long cpuTotalTime = (userTime - _cpuInfo.userTime) + (kernelTime - _cpuInfo.kernelTime);
                long cpuBusyTime = cpuTotalTime - (idleTime - _cpuInfo.idleTime);

                _cpuInfo.kernelTime = kernelTime;
                _cpuInfo.userTime = userTime;
                _cpuInfo.idleTime = idleTime;

                if (cpuTotalTime > 0)
                {
                    long reading = cpuBusyTime * 100 / cpuTotalTime;
                    Debug.Assert(0 <= reading && reading <= int.MaxValue);
                    return (int)reading;
                }
                return 0;
            }

            public int CurrentUtilization => GetCpuUtilization();
        }
    }
}
