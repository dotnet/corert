// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Internal.LowLevelLinq;

namespace System.Threading
{
    internal static partial class ClrThreadPool
    {
        private const int CpuUtilizationHigh = 95;
        private const int CpuUtilizationLow = 80;
        private static int s_cpuUtilization = 85; // TODO: Add calculation for CPU utilization

        private static short s_forcedMinWorkerThreads = 0; // TODO: Config
        private static short s_forcedMaxWorkerThreads = 0; // TODO: Config

        private const short MaxPossibleThreadCount = short.MaxValue;
        private static short s_minThreads = (short)ThreadPoolGlobals.processorCount;
        private static short s_maxThreads = short.MaxValue;
        private static readonly LowLevelLock s_maxMinThreadLock = new LowLevelLock();

        [StructLayout(LayoutKind.Explicit, Size = CacheLineSize * 5)]
        private struct CacheLineAligned
        {
            private const int CacheLineSize = 64;
            [FieldOffset(CacheLineSize * 1)]
            public ThreadCounts counts;
            [FieldOffset(CacheLineSize * 2)]
            public int lastDequeueTime;
            [FieldOffset(CacheLineSize * 3)]
            public int priorCompletionCount;
            [FieldOffset(CacheLineSize * 3 + sizeof(int))]
            public int priorCompletedWorkRequestsTime;
            [FieldOffset(CacheLineSize * 3 + sizeof(int) * 2)]
            public int nextCompletedWorkRequestsTime;
        }

        private static CacheLineAligned s_aligned = new CacheLineAligned();
        private static long s_currentSampleStartTime;
        private static int s_completionCount = 0;
        private static int s_threadAdjustmentInterval;

        static ClrThreadPool() {
            s_aligned.counts.numThreadsGoal = s_forcedMinWorkerThreads > 0 ? s_forcedMinWorkerThreads : s_minThreads;
        }

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

                        ThreadCounts counts = ThreadCounts.VolatileReadCounts(ref s_aligned.counts);
                        while (counts.numThreadsGoal < s_minThreads)
                        {
                            ThreadCounts newCounts = counts;
                            newCounts.numThreadsGoal = s_minThreads;

                            ThreadCounts oldCounts = ThreadCounts.CompareExchangeCounts(ref s_aligned.counts, newCounts, counts);
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

                        ThreadCounts counts = ThreadCounts.VolatileReadCounts(ref s_aligned.counts);
                        while (counts.numThreadsGoal > s_maxThreads)
                        {
                            ThreadCounts newCounts = counts;
                            newCounts.numThreadsGoal = s_maxThreads;

                            ThreadCounts oldCounts = ThreadCounts.CompareExchangeCounts(ref s_aligned.counts, newCounts, counts);
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

        public static int GetAvailableThreads()
        {
            ThreadCounts counts = ThreadCounts.VolatileReadCounts(ref s_aligned.counts);
            return counts.numExistingThreads - counts.numProcessingWork;
        }

        internal static bool NotifyWorkItemComplete()
        {
            // TODO: Check perf. Might need to make this thread-local.
            Interlocked.Increment(ref s_completionCount);
            Volatile.Write(ref s_aligned.lastDequeueTime, Environment.TickCount);

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
            int numCompletions = totalNumCompletions - Volatile.Read(ref s_aligned.priorCompletionCount);
            long startTime = s_currentSampleStartTime;
            long endTime = 0; // TODO: PAL High Performance Counter
            long freq = 0;

            double elapsedSeconds = (double)(endTime - startTime) / freq;

            if(elapsedSeconds * 1000 >= s_threadAdjustmentInterval / 2)
            {
                ThreadCounts currentCounts = ThreadCounts.VolatileReadCounts(ref s_aligned.counts);
                int newMax;
                (newMax, s_threadAdjustmentInterval) = HillClimbing.ThreadPoolHillClimber.Update(currentCounts.numThreadsGoal, elapsedSeconds, numCompletions);

                while(newMax != currentCounts.numThreadsGoal)
                {
                    ThreadCounts newCounts = currentCounts;
                    newCounts.numThreadsGoal = (short)newMax;

                    ThreadCounts oldCounts = ThreadCounts.CompareExchangeCounts(ref s_aligned.counts, newCounts, currentCounts);
                    if (oldCounts == currentCounts)
                    {
                        //
                        // If we're increasing the max, inject a thread.  If that thread finds work, it will inject
                        // another thread, etc., until nobody finds work or we reach the new maximum.
                        //
                        // If we're reducing the max, whichever threads notice this first will retire themselves.
                        //
                        if (newMax > oldCounts.numThreadsGoal)
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
                s_aligned.priorCompletionCount = totalNumCompletions;
                Volatile.Write(ref s_aligned.nextCompletedWorkRequestsTime, currentTicks + s_threadAdjustmentInterval);
                Volatile.Write(ref s_aligned.priorCompletedWorkRequestsTime, currentTicks);
                s_currentSampleStartTime = endTime;
            }
        }

        private static bool ShouldAdjustMaxWorkersActive()
        {
            // We need to subtract by prior time because Environment.TickCount can wrap around, making a comparison of absolute times unreliable.
            int priorTime = Volatile.Read(ref s_aligned.priorCompletedWorkRequestsTime);
            int requiredInterval = Volatile.Read(ref s_aligned.nextCompletedWorkRequestsTime) - priorTime;
            int elapsedInterval = Environment.TickCount - priorTime;
            if(elapsedInterval >= requiredInterval)
            {
                ThreadCounts counts = ThreadCounts.VolatileReadCounts(ref s_aligned.counts);
                return counts.numExistingThreads >= counts.numThreadsGoal;
            }
            return false;
        }
    }
}
