// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.LowLevelLinq;

namespace System.Threading
{
    internal static partial class ClrThreadPool
    {
        private const int CpuUtilizationHigh = 95;
        private const int CpuUtilizationLow = 80;
        private static int s_cpuUtilization = 85; // TODO: Add calculation for CPU utilization

        private static int s_forcedMinWorkerThreads = 0; // TODO: Config
        private static int s_forcedMaxWorkerThreads = 0; // TODO: Config

        private const short MaxPossibleThreadCount = short.MaxValue;
        private static short s_minThreads; // TODO: Initialize
        private static short s_maxThreads; // TODO: Initialize
        private static readonly LowLevelLock s_maxMinThreadLock = new LowLevelLock();
        
        private static ThreadCounts s_counts = new ThreadCounts();
        private static int s_lastDequeueTime;
        private static int s_priorCompletedWorkRequestsTime;
        private static int s_nextCompletedWorkRequestsTime;
        private static long s_currentSampleStartTime;
        private static int s_priorCompletionCount = 0;
        private static int s_completionCount = 0;
        private static int s_threadAdjustmentInterval;

        public static bool SetMinThreads(int minThreads)
        {
            s_maxMinThreadLock.Acquire();
            try
            {
                if (minThreads < 0 || minThreads > s_maxThreads)
                {
                    return false;
                }
                else
                {
                    short threads = (short)Math.Min(minThreads, MaxPossibleThreadCount);
                    if (s_forcedMinWorkerThreads == 0)
                    {
                        s_minThreads = threads;

                        ThreadCounts counts = ThreadCounts.VolatileReadCounts(ref s_counts);
                        while (counts.numThreadsGoal < s_minThreads)
                        {
                            ThreadCounts newCounts = counts;
                            newCounts.numThreadsGoal = s_minThreads;

                            ThreadCounts oldCounts = ThreadCounts.CompareExchangeCounts(ref s_counts, newCounts, counts);
                            if (oldCounts == counts)
                            {
                                counts = newCounts;

                                if (newCounts.numThreadsGoal > oldCounts.numThreadsGoal && ThreadPool.GetQueuedWorkItems().Any())
                                {
                                    WorkerThread.MaybeAddWorkingWorker();
                                }
                            }
                            else
                            {
                                counts = oldCounts;
                            }
                        }
                    }
                    return true;
                }
            }
            finally
            {
                s_maxMinThreadLock.Release();
            }
        }

        public static int GetMinThreads() => s_minThreads;

        public static bool SetMaxThreads(int maxThreads)
        {
            s_maxMinThreadLock.Acquire();
            try
            {
                if (maxThreads < s_minThreads || maxThreads == 0)
                {
                    return false;
                }
                else
                {
                    short threads = (short)Math.Min(maxThreads, MaxPossibleThreadCount);
                    if (s_forcedMaxWorkerThreads == 0)
                    {
                        s_maxThreads = threads;

                        ThreadCounts counts = ThreadCounts.VolatileReadCounts(ref s_counts);
                        while (counts.numThreadsGoal > s_maxThreads)
                        {
                            ThreadCounts newCounts = counts;
                            newCounts.numThreadsGoal = s_maxThreads;

                            ThreadCounts oldCounts = ThreadCounts.CompareExchangeCounts(ref s_counts, newCounts, counts);
                            if (oldCounts == counts)
                            {
                                counts = newCounts;
                            }
                            else
                            {
                                counts = oldCounts;
                            }
                        }
                    }
                    return true;
                }
            }
            finally
            {
                s_maxMinThreadLock.Release();
            }
        }

        public static int GetMaxThreads() => s_maxThreads;

        internal static bool NotifyWorkItemComplete()
        {
            Interlocked.Increment(ref s_completionCount);
            Volatile.Write(ref s_lastDequeueTime, Environment.TickCount);

            bool shouldAdjustWorkers = ShouldAdjustMaxWorkersActive();
            if(shouldAdjustWorkers)
            {
                AdjustMaxWorkersActive();
            }
            return WorkerThread.ShouldStopProcessingWorkNow();
        }

        //
        // This method must only be called if ShouldAdjustMaxWorkersActive has returned true, *and*
        // s_threadAdjustmentLock is held.
        //
        private static void AdjustMaxWorkersActive()
        {
            int currentTicks = Environment.TickCount;
            int totalNumCompletions = Volatile.Read(ref s_completionCount);
            int numCompletions = totalNumCompletions - Volatile.Read(ref s_priorCompletionCount);
            long startTime = s_currentSampleStartTime;
            long endTime = 0; // TODO: PAL High Performance Counter
            long freq = 0;

            double elapsedSeconds = (double)(endTime - startTime) / freq;

            if(elapsedSeconds * 1000 >= s_threadAdjustmentInterval / 2)
            {
                ThreadCounts currentCounts = ThreadCounts.VolatileReadCounts(ref s_counts);
                int newMax;
                (newMax, s_threadAdjustmentInterval) = HillClimbing.ThreadPoolHillClimber.Update(currentCounts.numThreadsGoal, elapsedSeconds, numCompletions);

                while(newMax != currentCounts.numThreadsGoal)
                {
                    ThreadCounts newCounts = currentCounts;
                    newCounts.numThreadsGoal = (short)newMax;

                    ThreadCounts oldCounts = ThreadCounts.CompareExchangeCounts(ref s_counts, newCounts, currentCounts);
                    if (oldCounts == currentCounts)
                    {
                        if(newMax > oldCounts.numThreadsGoal)
                        {
                            WorkerThread.MaybeAddWorkingWorker();
                        }
                        break;
                    }
                    else
                    {
                        if(oldCounts.numThreadsGoal > currentCounts.numThreadsGoal && oldCounts.numThreadsGoal >= newMax)
                        {
                            // someone (probably the gate thread) increased the thread count more than
                            // we are about to do.  Don't interfere.
                            break;
                        }

                        currentCounts = oldCounts;
                    }
                }
                s_priorCompletionCount = totalNumCompletions;
                Volatile.Write(ref s_nextCompletedWorkRequestsTime, currentTicks + s_threadAdjustmentInterval);
                s_priorCompletedWorkRequestsTime = currentTicks;
                s_currentSampleStartTime = endTime;
            }
        }

        private static bool ShouldAdjustMaxWorkersActive()
        {
            // We need to subtract by prior time because Environment.TickCount can wrap around, making a comparison of absolute times unreliable.
            int priorTime = s_priorCompletedWorkRequestsTime;
            int requiredInterval = Volatile.Read(ref s_nextCompletedWorkRequestsTime) - priorTime;
            int elapsedInterval = Environment.TickCount - priorTime;
            if(elapsedInterval >= requiredInterval)
            {
                ThreadCounts counts = ThreadCounts.VolatileReadCounts(ref s_counts);
                return counts.numExistingThreads >= counts.numThreadsGoal;
            }
            return false;
        }
    }
}
