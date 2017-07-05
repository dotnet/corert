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
                public long affinityMask;
                public int numberOfProcessors;
                public Interop.Kernel32.SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION[] usageBuffer;
            }

            private ProcessCpuInformation cpuInfo = new ProcessCpuInformation();

            public CpuUtilizationReader()
            {
                cpuInfo.numberOfProcessors = ThreadPoolGlobals.processorCount;
                cpuInfo.affinityMask = GetCurrentProcessAffinityMask();

                if (cpuInfo.affinityMask == 0)
                {
                    long mask = 0;
                    long maskPos = 1;
                    for (int i = 0; i < ThreadPoolGlobals.processorCount; i++)
                    {
                        mask |= maskPos;
                        maskPos <<= 1;
                    }
                    cpuInfo.affinityMask = mask;
                }

                cpuInfo.usageBuffer = new Interop.Kernel32.SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION[ThreadPoolGlobals.processorCount];
                GetCpuUtilization(); // Call once to initialize the usage buffer
            }

            private long GetCurrentProcessAffinityMask()
            {
                if (!Interop.Kernel32.GetProcessAffinityMask(Interop.mincore.GetCurrentProcess(), out UIntPtr processMask, out UIntPtr systemAffinityMask))
                {
                    return 1;
                }
                return (long)processMask & (long)systemAffinityMask;
            }

            private unsafe int GetCpuUtilization()
            {
                fixed (Interop.Kernel32.SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION* buffer = cpuInfo.usageBuffer)
                {
                    Interop.Kernel32.QuerySystemInformation(Interop.Kernel32.SYSTEM_INFORMATION_CLASS.SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION,
                        buffer,
                        sizeof(Interop.Kernel32.SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION) * cpuInfo.usageBuffer.Length,
                        out uint returnLength);
                }

                long idleTime = 0;
                long kernelTime = 0;
                long userTime = 0;

                for (long procNumber = 0, mask = cpuInfo.affinityMask; procNumber < cpuInfo.usageBuffer.Length && mask != 0; procNumber++, mask >>= 1)
                {
                    if ((mask & 1) != 0)
                    {
                        idleTime += cpuInfo.usageBuffer[procNumber].IdleTime;
                        kernelTime += cpuInfo.usageBuffer[procNumber].KernelTime;
                        userTime += cpuInfo.usageBuffer[procNumber].UserTime;
                    }
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