// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Runtime.InteropServices;
using Internal.LowLevelLinq;

namespace System.Threading
{
    /// <summary>
    /// A thread-pool run and managed on the CLR.
    /// </summary>
    internal partial class ClrThreadPool
    {
#pragma warning disable IDE1006 // Naming Styles
        public static readonly ClrThreadPool ThreadPoolInstance = new ClrThreadPool();
#pragma warning restore IDE1006 // Naming Styles

        private const short MaxPossibleThreadCount = short.MaxValue;

        private static short GetThreadCountConfig(string configName)
        {
            var value = AppContext.GetData(configName);
            ushort numThreads = 0;
            switch (value)
            {
                case string str:
                    if (str.StartsWith("0x"))
                    {
                        numThreads = Convert.ToUInt16(str, 16);
                    }
                    else if (str.StartsWith("0"))
                    {
                        numThreads = Convert.ToUInt16(str, 8);
                    }
                    else
                    {
                        ushort.TryParse(str, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out numThreads);
                    }
                    break;
                case IConvertible convertible:
                    return convertible.ToInt16(NumberFormatInfo.InvariantInfo);
            }
            return (short)Math.Min(numThreads, MaxPossibleThreadCount);
        }

        private const int CpuUtilizationHigh = 95;
        private const int CpuUtilizationLow = 80;
        private int _cpuUtilization = 85; // TODO: Add calculation for CPU utilization


        private static readonly short s_forcedMinWorkerThreads = AppContextConfigHelper.GetConfig("System.Threading.ThreadPool.MinThreads", (short)0);
        private static readonly short s_forcedMaxWorkerThreads = AppContextConfigHelper.GetConfig("System.Threading.ThreadPool.MaxThreads", (short)0);

        private short _minThreads = (short)ThreadPoolGlobals.processorCount;
        private short _maxThreads = MaxPossibleThreadCount;
        private readonly LowLevelLock _maxMinThreadLock = new LowLevelLock();

        [StructLayout(LayoutKind.Explicit, Size = CacheLineSize * 5)]
        private struct CacheLineSeparated
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

        private CacheLineSeparated _separated;
        private ulong _currentSampleStartTime;
        private int _completionCount = 0;
        private int _threadAdjustmentIntervalMs;

        private LowLevelLock _hillClimbingThreadAdjustmentLock = new LowLevelLock();

        private volatile int _numRequestedWorkers = 0;

        private ClrThreadPool()
        {
            _separated = new CacheLineSeparated
            {
                counts = new ThreadCounts
                {
                    numThreadsGoal = s_forcedMinWorkerThreads > 0 ? s_forcedMinWorkerThreads : _minThreads
                }
            };
        }

        public bool SetMinThreads(int minThreads)
        {
            _maxMinThreadLock.Acquire();
            try
            {
                if (minThreads < 0 || minThreads > _maxThreads)
                {
                    return false;
                }
                else
                {
                    short threads = (short)Math.Min(minThreads, MaxPossibleThreadCount);
                    if (s_forcedMinWorkerThreads == 0)
                    {
                        _minThreads = threads;

                        ThreadCounts counts = ThreadCounts.VolatileReadCounts(ref _separated.counts);
                        while (counts.numThreadsGoal < minThreads)
                        {
                            ThreadCounts newCounts = counts;
                            newCounts.numThreadsGoal = _minThreads;

                            ThreadCounts oldCounts = ThreadCounts.CompareExchangeCounts(ref _separated.counts, newCounts, counts);
                            if (oldCounts == counts)
                            {
                                counts = newCounts;

                                if (newCounts.numThreadsGoal > oldCounts.numThreadsGoal && _numRequestedWorkers > 0)
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
                _maxMinThreadLock.Release();
            }
        }

        public int GetMinThreads() => _minThreads;

        public bool SetMaxThreads(int maxThreads)
        {
            _maxMinThreadLock.Acquire();
            try
            {
                if (maxThreads < _minThreads || maxThreads == 0)
                {
                    return false;
                }
                else
                {
                    short threads = (short)Math.Min(maxThreads, MaxPossibleThreadCount);
                    if (s_forcedMaxWorkerThreads == 0)
                    {
                        _maxThreads = threads;

                        ThreadCounts counts = ThreadCounts.VolatileReadCounts(ref _separated.counts);
                        while (counts.numThreadsGoal > maxThreads)
                        {
                            ThreadCounts newCounts = counts;
                            newCounts.numThreadsGoal = _maxThreads;

                            ThreadCounts oldCounts = ThreadCounts.CompareExchangeCounts(ref _separated.counts, newCounts, counts);
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
                _maxMinThreadLock.Release();
            }
        }

        public int GetMaxThreads() => _maxThreads;

        public int GetAvailableThreads()
        {
            ThreadCounts counts = ThreadCounts.VolatileReadCounts(ref _separated.counts);
            int count = _maxThreads - counts.numExistingThreads;
            if (count < 0)
            {
                return 0;
            }
            return count;
        }

        internal bool NotifyWorkItemComplete()
        {
            // TODO: Check perf. Might need to make this thread-local.
            Interlocked.Increment(ref _completionCount);
            Volatile.Write(ref _separated.lastDequeueTime, Environment.TickCount);
            
            if (ShouldAdjustMaxWorkersActive())
            {
                bool acquiredLock = _hillClimbingThreadAdjustmentLock.TryAcquire();
                try
                {
                    if (acquiredLock)
                    {
                        AdjustMaxWorkersActive();
                    }
                }
                finally
                {
                    if (acquiredLock)
                    {
                        _hillClimbingThreadAdjustmentLock.Release();
                    }
                } 
            }

            return !WorkerThread.ShouldStopProcessingWorkNow();
        }

        //
        // This method must only be called if ShouldAdjustMaxWorkersActive has returned true, *and*
        // _hillClimbingThreadAdjustmentLock is held.
        //
        private void AdjustMaxWorkersActive()
        {
            int currentTicks = Environment.TickCount;
            int totalNumCompletions = Volatile.Read(ref _completionCount);
            int numCompletions = totalNumCompletions - _separated.priorCompletionCount;
            ulong startTime = _currentSampleStartTime;
            ulong endTime = HighPerformanceCounter.TickCount;
            ulong freq = HighPerformanceCounter.Frequency;

            double elapsedSeconds = (double)(endTime - startTime) / freq;

            if(elapsedSeconds * 1000 >= _threadAdjustmentIntervalMs / 2)
            {
                ThreadCounts currentCounts = ThreadCounts.VolatileReadCounts(ref _separated.counts);
                int newMax;
                (newMax, _threadAdjustmentIntervalMs) = HillClimbing.ThreadPoolHillClimber.Update(currentCounts.numThreadsGoal, elapsedSeconds, numCompletions);

                while(newMax != currentCounts.numThreadsGoal)
                {
                    ThreadCounts newCounts = currentCounts;
                    newCounts.numThreadsGoal = (short)newMax;

                    ThreadCounts oldCounts = ThreadCounts.CompareExchangeCounts(ref _separated.counts, newCounts, currentCounts);
                    if (oldCounts == currentCounts)
                    {
                        //
                        // If we're increasing the max, inject a thread.  If that thread finds work, it will inject
                        // another thread, etc., until nobody finds work or we reach the new maximum.
                        //
                        // If we're reducing the max, whichever threads notice this first will sleep and timeout themselves.
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
                _separated.priorCompletionCount = totalNumCompletions;
                _separated.nextCompletedWorkRequestsTime = currentTicks + _threadAdjustmentIntervalMs;
                Volatile.Write(ref _separated.priorCompletedWorkRequestsTime, currentTicks);
                _currentSampleStartTime = endTime;
            }
        }

        private bool ShouldAdjustMaxWorkersActive()
        {
            // We need to subtract by prior time because Environment.TickCount can wrap around, making a comparison of absolute times unreliable.
            int priorTime = Volatile.Read(ref _separated.priorCompletedWorkRequestsTime);
            int requiredInterval = _separated.nextCompletedWorkRequestsTime - priorTime;
            int elapsedInterval = Environment.TickCount - priorTime;
            if(elapsedInterval >= requiredInterval)
            {
                ThreadCounts counts = ThreadCounts.VolatileReadCounts(ref _separated.counts);
                return counts.numExistingThreads >= counts.numThreadsGoal;
            }
            return false;
        }

        internal void RequestWorker()
        {
            Interlocked.Increment(ref _numRequestedWorkers);
            WorkerThread.MaybeAddWorkingWorker();
            GateThread.EnsureRunning();
        }
    }
}
