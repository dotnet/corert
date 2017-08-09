// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;

namespace System.Threading
{
    [EventSource(Name = "Microsoft-Windows-DotNETRuntime", Guid = "{e13c0d23-ccbc-4e12-931b-d9cc2eee27e4}")]
    public sealed class ClrThreadPoolEventSource : EventSource
    {
        private const string WorkerThreadMessage = "WorkerThreadCount=%1";
        private const string WorkerThreadAdjustmentSampleMessage = "Throughput=%1";
        private const string WorkerThreadAdjustmentAdjustmentEventMessage = "AverageThroughput=%1;%nNewWorkerThreadCount=%2;%nReason=%3";
        private const string WorkerThreadAdjustmentStatsEventMessage = "Duration=%1;%nThroughput=%2;%nThreadWave=%3;%nThroughputWave=%4;%nThroughputErrorEstimate=%5;%nAverageThroughputErrorEstimate=%6;%nThroughputRatio=%7;%nConfidence=%8;%nNewControlSetting=%9;%nNewThreadWaveMagnitude=%10";

        // We have these constants here because EventTask, EventOpcode, and EventKeywords are public APIs, and we don't want to expose these values to the end user to use in their events.
        private const EventTask WorkerThreadTask = (EventTask)16;
        private const EventTask WorkerThreadAdjustmentTask = (EventTask)18;

        private const EventOpcode WaitOpcode = (EventOpcode)90;
        private const EventOpcode SampleOpcode = (EventOpcode)100;
        private const EventOpcode AdjustmentOpcode = (EventOpcode)101;
        private const EventOpcode StatsOpcode = (EventOpcode)102;
        private const EventKeywords ThreadingKeyword = (EventKeywords)0x10000;

        private ClrThreadPoolEventSource()
        {
        }

        [Event(1, Level = EventLevel.Informational, Message = WorkerThreadMessage, Task = WorkerThreadTask, Opcode = EventOpcode.Start, Version = 0, Keywords = ThreadingKeyword)]
        public void WorkerThreadStart(short numExistingThreads)
        {
            WriteEvent(1, numExistingThreads);
        }

        [Event(2, Level = EventLevel.Informational, Message = WorkerThreadMessage, Task = WorkerThreadTask, Opcode = EventOpcode.Stop, Version = 0, Keywords = ThreadingKeyword)]
        public void WorkerThreadStop(short numExistingThreads)
        {
            WriteEvent(2, numExistingThreads);
        }

        [Event(3, Level = EventLevel.Informational, Message = WorkerThreadMessage, Task = WorkerThreadTask, Opcode = WaitOpcode, Version = 0, Keywords = ThreadingKeyword)]
        public void WorkerThreadWait(short numExistingThreads)
        {
            WriteEvent(3, numExistingThreads);
        }

        [Event(4, Level = EventLevel.Informational, Message = WorkerThreadAdjustmentSampleMessage, Opcode = SampleOpcode, Version = 0, Task = WorkerThreadAdjustmentTask, Keywords = ThreadingKeyword)]
        public unsafe void WorkerThreadAdjustmentSample(double throughput)
        {
            if (IsEnabled())
            {
                EventData* data = stackalloc EventData[1];
                data[0].DataPointer = (IntPtr)(&throughput);
                data[0].Size = sizeof(double);
                WriteEventCore(4, 1, data);
            }
        }

        [Event(5, Level = EventLevel.Informational, Message = WorkerThreadAdjustmentSampleMessage, Opcode = AdjustmentOpcode, Version = 0, Task = WorkerThreadAdjustmentTask, Keywords = ThreadingKeyword)]
        public unsafe void WorkerThreadAdjustmentAdjustment(double averageThroughput, int newWorkerThreadCount, int stateOrTransition)
        {
            if (IsEnabled())
            {
                EventData* data = stackalloc EventData[3];
                data[0].DataPointer = (IntPtr)(&averageThroughput);
                data[0].Size = sizeof(double);
                data[1].DataPointer = (IntPtr)(&newWorkerThreadCount);
                data[1].Size = sizeof(int);
                data[2].DataPointer = (IntPtr)(&stateOrTransition);
                data[2].Size = sizeof(int);
                WriteEventCore(5, 3, data);
            }
        }

        [Event(6, Level = EventLevel.Verbose, Message = WorkerThreadAdjustmentSampleMessage, Opcode = StatsOpcode, Version = 0, Task = WorkerThreadAdjustmentTask, Keywords = ThreadingKeyword)]
        [CLSCompliant(false)]
        public unsafe void WorkerThreadAdjustmentStats(double duration, double throughput, double threadWave, double throughputWave, double throughputErrorEstimate,
            double averageThroughputNoise, double ratio, double confidence, double currentControlSetting, ushort newThreadWaveMagnitude)
        {
            if (IsEnabled())
            {
                EventData* data = stackalloc EventData[10];
                data[0].DataPointer = (IntPtr)(&duration);
                data[0].Size = sizeof(double);
                data[1].DataPointer = (IntPtr)(&throughput);
                data[1].Size = sizeof(double);
                data[2].DataPointer = (IntPtr)(&threadWave);
                data[2].Size = sizeof(double);
                data[3].DataPointer = (IntPtr)(&throughputWave);
                data[3].Size = sizeof(double);
                data[4].DataPointer = (IntPtr)(&throughputErrorEstimate);
                data[4].Size = sizeof(double);
                data[5].DataPointer = (IntPtr)(&averageThroughputNoise);
                data[5].Size = sizeof(double);
                data[6].DataPointer = (IntPtr)(&ratio);
                data[6].Size = sizeof(double);
                data[7].DataPointer = (IntPtr)(&confidence);
                data[7].Size = sizeof(double);
                data[8].DataPointer = (IntPtr)(&currentControlSetting);
                data[8].Size = sizeof(double);
                data[9].DataPointer = (IntPtr)(&newThreadWaveMagnitude);
                data[9].Size = sizeof(ushort);
                WriteEventCore(6, 10, data);
            }
        }

#pragma warning disable IDE1006 // Naming Styles
        public static readonly ClrThreadPoolEventSource Log = new ClrThreadPoolEventSource();
#pragma warning restore IDE1006 // Naming Styles
    }
}
