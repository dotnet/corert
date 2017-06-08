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
        
        private static int s_minThreads; // TODO: Initialize
        private static int s_maxThreads; // TODO: Initialize
        private static readonly LowLevelLock s_maxMinThreadLock = new LowLevelLock();


        private static readonly LowLevelLock s_threadAdjustmentLock = new LowLevelLock();
        private static ThreadCounts s_counts = new ThreadCounts();
        private static int s_lastDequeueTime;
        private static int s_priorCompletedWorkRequestsTime;
        private static int s_nextCompletedWorkRequestsTime;
        private static long s_currentSampleStartTime;
        private static int s_priorCompletionCount = 0;
        private static int s_completionCount = 0;
        private static int s_threadAdjustmentInterval;

        public static bool SetMinThreads(int threads)
        {
            s_maxMinThreadLock.Acquire();
            bool success;
            if (threads < 0 || threads > s_maxThreads)
            {
                success = false;
            }
            else
            {
                s_minThreads = threads;

                ThreadCounts counts = ThreadCounts.VolatileReadCounts(ref s_counts);
                while(counts.maxWorking < s_minThreads)
                {
                    ThreadCounts newCounts = counts;
                    newCounts.maxWorking = (short)s_minThreads;

                    ThreadCounts oldCounts = ThreadCounts.CompareExchangeCounts(ref s_counts, newCounts, counts);
                    if (oldCounts == counts)
                    {
                        counts = newCounts;

                        if(newCounts.maxWorking > oldCounts.maxWorking && ThreadPool.GetQueuedWorkItems().Any())
                        {
                            WorkerThread.MaybeAddWorkingWorker();
                        }
                    }
                    else
                    {
                        counts = oldCounts;
                    }
                }
                success = true;
            }
            s_maxMinThreadLock.Release();
            return success;
        }

        public static int GetMinThreads() => s_minThreads;

        public static bool SetMaxThreads(int threads)
        {
            s_maxMinThreadLock.Acquire();
            bool success;
            if (threads < s_minThreads || threads == 0)
            {
                success = false;
            }
            else
            {
                s_maxThreads = threads;

                ThreadCounts counts = ThreadCounts.VolatileReadCounts(ref s_counts);
                while (counts.maxWorking > s_maxThreads)
                {
                    ThreadCounts newCounts = counts;
                    newCounts.maxWorking = (short)s_maxThreads;

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
                success = true;
            }
            s_maxMinThreadLock.Release();
            return success;
        }

        public static int GetMaxThreads() => s_maxThreads;

        internal static bool NotifyWorkItemComplete()
        {
            Interlocked.Increment(ref s_completionCount);
            Volatile.Write(ref s_lastDequeueTime, Environment.TickCount);

            bool shouldAdjustWorkers = ShouldAdjustMaxWorkersActive();
            if(shouldAdjustWorkers && s_threadAdjustmentLock.TryAcquire())
            {
                AdjustMaxWorkersActive();
                s_threadAdjustmentLock.Release();
            }
            return ShouldWorkerKeepRunning();
        }

        private static bool ShouldWorkerKeepRunning()
        {
            ThreadCounts counts = ThreadCounts.VolatileReadCounts(ref s_counts);
            while(true)
            {
                if(counts.numActive <= counts.numWorking)
                {
                    return true;
                }

                ThreadCounts newCounts = counts;
                newCounts.numActive--;
                newCounts.numWorking--;

                ThreadCounts oldCounts = ThreadCounts.CompareExchangeCounts(ref s_counts, newCounts, counts);

                if(oldCounts == counts)
                {
                    return false;
                }
                counts = oldCounts;
            }
        }

        private static void AdjustMaxWorkersActive()
        {
            int currentTicks = Environment.TickCount;
            int totalNumCompletions = Volatile.Read(ref s_completionCount);
            int numCompletions = totalNumCompletions - Volatile.Read(ref s_priorCompletionCount);
            long startTime = s_currentSampleStartTime;
            long endTime = 0; // TODO: PAL High Performance Counter
            long freq = 0;

            double elapsed = (double)(endTime - startTime) / freq;

            if(elapsed * 1000 >= s_threadAdjustmentInterval / 2)
            {
                ThreadCounts currentCounts = ThreadCounts.VolatileReadCounts(ref s_counts);
                int newMax;
                (newMax, s_threadAdjustmentInterval) = HillClimbing.ThreadPoolHillClimber.Update(currentCounts.maxWorking, elapsed, numCompletions);

                while(newMax != currentCounts.maxWorking)
                {
                    ThreadCounts newCounts = currentCounts;
                    newCounts.maxWorking = (short)newMax;

                    ThreadCounts oldCounts = ThreadCounts.CompareExchangeCounts(ref s_counts, newCounts, currentCounts);
                    if (oldCounts == currentCounts)
                    {
                        if(newMax > oldCounts.maxWorking)
                        {
                            WorkerThread.MaybeAddWorkingWorker();
                        }
                        break;
                    }
                    else
                    {
                        if(oldCounts.maxWorking > currentCounts.maxWorking && oldCounts.maxWorking >= newMax)
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
                return counts.numActive >= counts.maxWorking;
            }
            return false;
        }
    }
}
