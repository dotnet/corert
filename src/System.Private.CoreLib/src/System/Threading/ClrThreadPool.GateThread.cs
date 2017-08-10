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
            private const int GateThreadRunningMask = 0x4;
            
            private static int numRunsRemaining;

            private static AutoResetEvent runGateThreadEvent = new AutoResetEvent(false);
          
            private static LowLevelLock s_createdLock = new LowLevelLock();

            private static readonly CpuUtilizationReader s_cpu = new CpuUtilizationReader();

            // TODO: CoreCLR: Worker Tracking in CoreCLR? (Config name: ThreadPool_EnableWorkerTracking)
            private static void GateThreadStart()
            {
                var initialCpuRead = s_cpu.CurrentUtilization; // The first reading is over a time range other than what we are focusing on, so we do not use the read.

                AppContext.TryGetSwitch("System.Threading.ThreadPool.DisableStarvationDetection", out bool disableStarvationDetection);
                AppContext.TryGetSwitch("System.Threading.ThreadPool.DebugBreakOnWorkerStarvation", out bool debuggerBreakOnWorkStarvation);

                while (true)
                {
                    runGateThreadEvent.WaitOne();
                    do
                    {
                        RuntimeThread.Sleep(GateThreadDelayMs);

                        ThreadPoolInstance._cpuUtilization = s_cpu.CurrentUtilization;

                        if (ThreadPoolInstance._numRequestedWorkers == 0)
                        {
                            continue;
                        }


                        if (!disableStarvationDetection)
                        {
                            if (ThreadPoolInstance._numRequestedWorkers > 0 && SufficientDelaySinceLastDequeue())
                            {
                                try
                                {
                                    ThreadPoolInstance._hillClimbingThreadAdjustmentLock.Acquire();
                                    ThreadCounts counts = ThreadCounts.VolatileReadCounts(ref ThreadPoolInstance._separated.counts);
                                    // don't add a thread if we're at max or if we are already in the process of adding threads
                                    while (counts.numExistingThreads < ThreadPoolInstance._maxThreads && counts.numExistingThreads >= counts.numThreadsGoal)
                                    {
                                        if (debuggerBreakOnWorkStarvation)
                                        {
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
                                finally
                                {
                                    ThreadPoolInstance._hillClimbingThreadAdjustmentLock.Release();
                                }
                            }
                        }
                    } while (Interlocked.Decrement(ref numRunsRemaining) > GetRunningMaskForNumRuns(0));
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

            // This is called by a worker thread
            internal static void EnsureRunning()
            {
                int numRunsMask = Interlocked.Exchange(ref numRunsRemaining, GetRunningMaskForNumRuns(2));
                if ((numRunsMask & GateThreadRunningMask) == 0)
                {
                    try
                    {
                        CreateGateThread();
                        runGateThreadEvent.Set();
                    }
                    catch (Exception)
                    {
                        Interlocked.Exchange(ref numRunsRemaining, 0);
                        throw;
                    }
                }
                else if (numRunsMask == GetRunningMaskForNumRuns(0))
                {
                    runGateThreadEvent.Set();
                }
            }

            private static int GetRunningMaskForNumRuns(int numRuns)
            {
                Debug.Assert(numRuns >= 0);
                Debug.Assert(numRuns < GateThreadRunningMask);
                return GateThreadRunningMask | numRuns;
            }

            private static void CreateGateThread()
            {
                RuntimeThread gateThread = RuntimeThread.Create(GateThreadStart);
                gateThread.IsBackground = true;
                gateThread.Start();
            }
        }
    }
}
