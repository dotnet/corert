// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Internal.LowLevelLinq;
using Internal.Runtime.Augments;

namespace System.Threading
{
    internal static partial class ClrThreadPool
    {
        internal static class WorkerThread
        {
            /// <summary>
            /// Semaphore for controlling how many threads are currently working.
            /// </summary>
            private static LowLevelLifoSemaphore s_semaphore = new LowLevelLifoSemaphore(0, int.MaxValue);

            private const int TimeoutMs = 20 * 1000;
            
            private static void WorkerThreadStart()
            {
                // TODO: Event: Worker Thread Start event

                while (true)
                {
                    // TODO: Event:  Worker thread wait event
                    while (s_semaphore.Wait(TimeoutMs))
                    {
                        if(ThreadPoolWorkQueue.Dispatch())
                        {
                            // If we ran out of work, we need to update s_counts that we are done working for now
                            // (this is already done for us if we are forced to stop working early in ShouldStopProcessingWorkNow)
                            ThreadCounts currentCounts = ThreadCounts.VolatileReadCounts(ref s_counts);
                            while (true)
                            {
                                ThreadCounts newCounts = currentCounts;
                                newCounts.numProcessingWork--;

                                ThreadCounts oldCounts = ThreadCounts.CompareExchangeCounts(ref s_counts, newCounts, currentCounts);

                                if (oldCounts == currentCounts)
                                {
                                    break;
                                }
                                currentCounts = oldCounts;
                            }
                        }

                        // Reset thread-local state that we control.
                        RuntimeThread.CurrentThread.Priority = ThreadPriority.Normal;
                        CultureInfo.CurrentCulture = CultureInfo.InstalledUICulture;
                        CultureInfo.CurrentUICulture = CultureInfo.InstalledUICulture;

                        // It's possible that we decided we had no work just before some work came in, 
                        // but reduced the worker count *after* the work came in.  In this case, we might
                        // miss the notification of available work.  So we wake up a thread (maybe this one!)
                        // if there is work to do.
                        if (ThreadPool.GetQueuedWorkItems().Any())
                        {
                            MaybeAddWorkingWorker();
                        }
                    }

                    ThreadCounts counts = ThreadCounts.VolatileReadCounts(ref s_counts);
                    while (true)
                    {
                        if (counts.numExistingThreads == counts.numProcessingWork)
                        {
                            break;
                        }

                        ThreadCounts newCounts = counts;
                        newCounts.numExistingThreads--;
                        newCounts.numThreadsGoal = Math.Max(s_minThreads, Math.Min(newCounts.numExistingThreads, newCounts.numThreadsGoal));
                        ThreadCounts oldCounts = ThreadCounts.CompareExchangeCounts(ref s_counts, newCounts, counts);
                        if (oldCounts == counts)
                        {
                            HillClimbing.ThreadPoolHillClimber.ForceChange(newCounts.numThreadsGoal, HillClimbing.StateOrTransition.ThreadTimedOut);
                            // TODO: Event:  Worker Thread stop event
                            return;
                        }
                        counts = oldCounts;
                    } 
                }
            }

            internal static void MaybeAddWorkingWorker()
            {
                ThreadCounts counts = ThreadCounts.VolatileReadCounts(ref s_counts);
                ThreadCounts newCounts;
                while (true)
                {
                    newCounts = counts;
                    newCounts.numProcessingWork = Math.Max(counts.numProcessingWork, Math.Min((short)(counts.numProcessingWork + 1), counts.numThreadsGoal));
                    newCounts.numExistingThreads = Math.Max(counts.numExistingThreads, newCounts.numProcessingWork);
                    
                    if(newCounts == counts)
                    {
                        return;
                    }

                    ThreadCounts oldCounts = ThreadCounts.CompareExchangeCounts(ref s_counts, newCounts, counts);

                    if(oldCounts == counts)
                    {
                        break;
                    }

                    counts = oldCounts;
                }

                int toCreate = newCounts.numExistingThreads - counts.numExistingThreads;
                int toRelease = newCounts.numProcessingWork - counts.numProcessingWork;

                if(toRelease > 0)
                {
                    s_semaphore.Release(toRelease);
                }

                for (int i = 0; i < toCreate; i++)
                {
                    CreateWorkerThread();
                }
            }

            internal static bool ShouldStopProcessingWorkNow()
            {
                ThreadCounts counts = ThreadCounts.VolatileReadCounts(ref s_counts);
                while (true)
                {
                    if (counts.numExistingThreads <= counts.numProcessingWork)
                    {
                        return true;
                    }

                    ThreadCounts newCounts = counts;
                    newCounts.numProcessingWork--;

                    ThreadCounts oldCounts = ThreadCounts.CompareExchangeCounts(ref s_counts, newCounts, counts);

                    if (oldCounts == counts)
                    {
                        return false;
                    }
                    counts = oldCounts;
                }
            }

            private static void CreateWorkerThread()
            {
                RuntimeThread workerThread = RuntimeThread.Create(WorkerThreadStart);
                workerThread.IsThreadPoolThread = true;
                workerThread.IsBackground = true;
                workerThread.Start();
            }
        }
    }
}
