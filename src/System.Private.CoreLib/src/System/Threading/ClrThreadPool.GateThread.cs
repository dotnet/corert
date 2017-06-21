// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Internal.Runtime.Augments;

namespace System.Threading
{
    internal partial class ClrThreadPool
    {
        private static class GateThread
        {
            private const int GateThreadDelayMs = 500;
            private const int DequeueDelayThresholdMs = GateThreadDelayMs * 2;
            
            private static bool s_disableStarvationDetection = true; // TODO: Config
            private static bool s_debuggerBreakOnWorkStarvation = false; // TODO: Config
            
            private static volatile bool s_requested = false;

            private static bool s_created = false;
            private static LowLevelLock s_createdLock = new LowLevelLock();
            private static CpuUtilizationReader s_cpu = new CpuUtilizationReader();

            // TODO: CoreCLR: Worker Tracking in CoreCLR? (Config name: ThreadPool_EnableWorkerTracking)
            private static void GateThreadStart()
            {
                while (true)
                {
                    RuntimeThread.Sleep(GateThreadDelayMs);

                    if(ThreadPoolInstance._numRequestedWorkers > 0)
                    {
                        WorkerThread.MaybeAddWorkingWorker();
                    }

                    if (!s_requested)
                    {
                        continue;
                    }
                    s_requested = false;

                    ThreadPoolInstance._cpuUtilization = s_cpu.CurrentUtilization;

                    if (!s_disableStarvationDetection)
                    {
                        if (ThreadPoolInstance._numRequestedWorkers > 0 && SufficientDelaySinceLastDequeue())
                        {
                            ThreadCounts counts = ThreadCounts.VolatileReadCounts(ref ThreadPoolInstance._separated.counts);
                            // don't add a thread if we're at max or if we are already in the process of adding threads
                            while (counts.numExistingThreads < ThreadPoolInstance._maxThreads && counts.numExistingThreads >= counts.numThreadsGoal)
                            {
                                if (s_debuggerBreakOnWorkStarvation)
                                {
                                    Debug.WriteLine("The CLR ThreadPool detected work starvation!");
                                    Debugger.Break();
                                }

                                ThreadCounts newCounts = counts;
                                newCounts.numThreadsGoal = (short)(newCounts.numExistingThreads + 1);
                                ThreadCounts oldCounts = ThreadCounts.CompareExchangeCounts(ref ThreadPoolInstance._separated.counts, newCounts, counts);
                                if (oldCounts == counts)
                                {
                                    HillClimbing.ThreadPoolHillClimber.ForceChange(newCounts.numThreadsGoal, HillClimbing.StateOrTransition.Starvation);
                                    WorkerThread.MaybeAddWorkingWorker();
                                    break;
                                }
                                counts = oldCounts;
                            }
                        }
                    }
                }
            }

            // called by logic to spawn new worker threads, return true if it's been too long
            // since the last dequeue operation - takes number of worker threads into account
            // in deciding "too long"
            private static bool SufficientDelaySinceLastDequeue()
            {
                int delay = Environment.TickCount - Volatile.Read(ref ThreadPoolInstance._separated.lastDequeueTime);

                int minimumDelay;

                if(ThreadPoolInstance._cpuUtilization < CpuUtilizationLow)
                {
                    minimumDelay = GateThreadDelayMs;
                }
                else
                {
                    ThreadCounts counts = ThreadCounts.VolatileReadCounts(ref ThreadPoolInstance._separated.counts);
                    int numThreads = counts.numThreadsGoal;
                    minimumDelay = numThreads * DequeueDelayThresholdMs;
                }

                return delay > minimumDelay;
            }

            private static void CreateGateThread()
            {
                RuntimeThread gateThread = RuntimeThread.Create(GateThreadStart);
                gateThread.IsBackground = true;
                gateThread.Start();
                s_created = true;
            }

            // This is called by a worker thread
            internal static void EnsureRunning()
            {
                s_requested = true;
                if (!s_created)
                {
                    try
                    {
                        s_createdLock.Acquire();
                        if (!s_created)
                        {
                            CreateGateThread();
                        }
                    }
                    finally
                    {
                        s_createdLock.Release();
                    }
                }
            }
        }
    }
}