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
            private const int DequeueDelayThreshold = GateThreadDelayMs * 2;
            
            private static bool s_disableStarvationDetection = true; // TODO: Config
            private static bool s_debuggerBreakOnWorkStarvation; // TODO: Config
            
            private static volatile bool s_requested;

            private static bool s_created;
            private static LowLevelLock s_createdLock;

            // TODO: CoreCLR: Worker Tracking in CoreCLR? (Config name: ThreadPool_EnableWorkerTracking)
            private static void GateThreadStart()
            {
                RuntimeThread.Sleep(GateThreadDelayMs); // delay getting initial CPU reading so we don't accidentally detect starvation from the Thread Pool doing its work.
                CpuUtilizationReader cpu = new CpuUtilizationReader();

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

                    ThreadPoolInstance._cpuUtilization = cpu.CurrentUtilization;

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
                    minimumDelay = numThreads * DequeueDelayThreshold;
                }

                return delay > minimumDelay;
            }

            private static void CreateGateThread()
            {
                if (!s_created)
                {
                    try
                    {
                        s_createdLock.Acquire();
                        if (!s_created)
                        {
                            RuntimeThread.Create(GateThreadStart);
                            s_created = true;
                        }
                    }
                    finally
                    {
                        s_createdLock.Release();
                    }
                }
            }

            // This is called by a worker thread
            internal static void EnsureRunning()
            {
                s_requested = true;
                CreateGateThread();
            }
        }
    }
}