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
        /// <summary>
        /// The worker thread infastructure for the CLR thread pool.
        /// </summary>
        private static class WorkerThread
        {
            /// <summary>
            /// Semaphore for controlling how many threads are currently working.
            /// </summary>
            private static LowLevelLifoSemaphore s_semaphore = new LowLevelLifoSemaphore(0, MaxPossibleThreadCount);

            private const int TimeoutMs = 20 * 1000;
            
            private static void WorkerThreadStart()
            {
                // TODO: Event: Worker Thread Start event
                RuntimeThread currentThread = RuntimeThread.CurrentThread;
                while (true)
                {
                    // TODO: Event:  Worker thread wait event
                    while (s_semaphore.Wait(TimeoutMs))
                    {
                        Volatile.Write(ref s_separated.lastDequeueTime, Environment.TickCount);
                        if (TakeActiveRequest())
                        {
                            if (ThreadPoolWorkQueue.Dispatch())
                            {
                                // If we ran out of work, we need to update s_separated.counts that we are done working for now
                                // (this is already done for us if we are forced to stop working early in ShouldStopProcessingWorkNow)
                                ThreadCounts currentCounts = ThreadCounts.VolatileReadCounts(ref s_separated.counts);
                                while (true)
                                {
                                    ThreadCounts newCounts = currentCounts;
                                    newCounts.numProcessingWork--;
                                    ThreadCounts oldCounts = ThreadCounts.CompareExchangeCounts(ref s_separated.counts, newCounts, currentCounts);

                                    if (oldCounts == currentCounts)
                                    {
                                        break;
                                    }
                                    currentCounts = oldCounts;
                                }
                                
                                // It's possible that we decided we had no work just before some work came in, 
                                // but reduced the worker count *after* the work came in.  In this case, we might
                                // miss the notification of available work.  So we wake up a thread (maybe this one!)
                                // if there is work to do.
                                if (s_numRequestedWorkers > 0)
                                {
                                    MaybeAddWorkingWorker();
                                }
                            }

                            // Reset thread-local state that we control.
                            if (currentThread.Priority != ThreadPriority.Normal)
                            {
                                currentThread.Priority = ThreadPriority.Normal;
                            }

                            CultureInfo.CurrentCulture = CultureInfo.InstalledUICulture;
                            CultureInfo.CurrentUICulture = CultureInfo.InstalledUICulture;
                        }
                    }

                    // At this point, the thread's wait timed out. We are shutting down this thread.
                    // We are going to decrement the number of exisiting threads to no longer include this one
                    // and then change the max number of threads in the thread pool to reflect that we don't need as many
                    // as we had. Finally, we are going to tell hill climbing that we changed the max number of threads.
                    ThreadCounts counts = ThreadCounts.VolatileReadCounts(ref s_separated.counts);
                    while (true)
                    {
                        if (counts.numExistingThreads == counts.numProcessingWork)
                        {
                            // In this case, enough work came in that this thread should not time out and should go back to work.
                            break;
                        }

                        ThreadCounts newCounts = counts;
                        newCounts.numExistingThreads--;
                        newCounts.numThreadsGoal = Math.Max(s_minThreads, Math.Min(newCounts.numExistingThreads, newCounts.numThreadsGoal));
                        ThreadCounts oldCounts = ThreadCounts.CompareExchangeCounts(ref s_separated.counts, newCounts, counts);
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
                ThreadCounts counts = ThreadCounts.VolatileReadCounts(ref s_separated.counts);
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

                    ThreadCounts oldCounts = ThreadCounts.CompareExchangeCounts(ref s_separated.counts, newCounts, counts);

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

            /// <summary>
            /// Returns if the current thread should stop processing work on the thread pool.
            /// A thread should stop processing work on the thread pool when work remains only when
            /// there are more worker threads in the thread pool than we currently want.
            /// </summary>
            /// <returns>Whether or not this thread should stop processing work even if there is still work in the queue.</returns>
            internal static bool ShouldStopProcessingWorkNow()
            {
                ThreadCounts counts = ThreadCounts.VolatileReadCounts(ref s_separated.counts);
                while (true)
                {
                    if (counts.numExistingThreads <= counts.numThreadsGoal)
                    {
                        return false;
                    }

                    ThreadCounts newCounts = counts;
                    newCounts.numProcessingWork--;

                    ThreadCounts oldCounts = ThreadCounts.CompareExchangeCounts(ref s_separated.counts, newCounts, counts);

                    if (oldCounts == counts)
                    {
                        return true;
                    }
                    counts = oldCounts;
                }
            }

            private static bool TakeActiveRequest()
            {
                int count = s_numRequestedWorkers;
                while (count > 0)
                {
                    int prevCount = Interlocked.CompareExchange(ref s_numRequestedWorkers, count - 1, count);
                    if (prevCount == count)
                    {
                        return true;
                    }
                    count = prevCount;
                }
                return false;
            }

            private static void CreateWorkerThread()
            {
                // TODO: Replace RuntimeThread.Create with a more perfomant thread creation
                // Note: Thread local data is created lazily on CoreRT, so we might get an OOM exception
                // if we run out of memory when starting this thread.
                // If we use RuntimeThread.Create, we get the exception on this thread.
                // If we don't, we will get the exception on our worker thread.
                // Goal: Figure out how to safely manage the OOM possibility of a worker thread
                // without perf issues.
                RuntimeThread workerThread = RuntimeThread.Create(WorkerThreadStart);
                workerThread.IsThreadPoolThread = true;
                workerThread.IsBackground = true;
                workerThread.Start();
            }
        }
    }
}
