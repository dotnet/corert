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
                public Interop.mincore.SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION[] usageBuffer;
            }

            private ProcessCpuInformation cpuInfo = new ProcessCpuInformation();

            public CpuUtilizationReader()
            {
                cpuInfo.numberOfProcessors = ThreadPoolGlobals.processorCount;

                cpuInfo.usageBuffer = new Interop.mincore.SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION[ThreadPoolGlobals.processorCount];
                GetCpuUtilization(); // Call once to initialize the usage buffer
            }

            private unsafe int GetCpuUtilization()
            {
                fixed (Interop.mincore.SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION* buffer = cpuInfo.usageBuffer)
                {
                    Interop.mincore.QuerySystemInformation(Interop.mincore.SYSTEM_INFORMATION_CLASS.SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION,
                        buffer,
                        sizeof(Interop.mincore.SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION) * cpuInfo.usageBuffer.Length,
                        out uint returnLength);
                }

                long idleTime = 0;
                long kernelTime = 0;
                long userTime = 0;

                for (long procNumber = 0; procNumber < cpuInfo.usageBuffer.Length; procNumber++)
                {
                    idleTime += cpuInfo.usageBuffer[procNumber].IdleTime;
                    kernelTime += cpuInfo.usageBuffer[procNumber].KernelTime;
                    userTime += cpuInfo.usageBuffer[procNumber].UserTime;
                }

                long cpuTotalTime = (userTime - cpuInfo.userTime) + (kernelTime - cpuInfo.kernelTime);
                long cpuBusyTime = cpuTotalTime - (idleTime - cpuInfo.idleTime);

                cpuInfo.kernelTime = kernelTime;
                cpuInfo.userTime = userTime;
                cpuInfo.idleTime = idleTime;

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
