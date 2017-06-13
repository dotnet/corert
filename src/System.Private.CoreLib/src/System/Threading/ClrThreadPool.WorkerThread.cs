// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Internal.Runtime.Augments;

namespace System.Threading
{
    internal static partial class ClrThreadPool
    {
        internal static class WorkerThread
        {
            private static LowLevelLifoSemaphore s_semaphore = new LowLevelLifoSemaphore(0, int.MaxValue);

            private const int TimeoutMs = 20 * 1000;
            
            private static void WorkerThreadStart()
            {
                RuntimeThread.InitializeThreadPoolThread();
                // TODO: Event: Worker Thread Start event

                while (true)
                {
                    // TODO: Event:  Worker thread wait event
                    while (s_semaphore.Wait(TimeoutMs))
                    {
                        if(!ThreadPoolWorkQueue.Dispatch())
                        {
                            return;
                        }

                        RuntimeThread.CurrentThread.Priority = ThreadPriority.Normal;
                        CultureInfo.CurrentCulture = CultureInfo.InstalledUICulture;
                        CultureInfo.CurrentUICulture = CultureInfo.InstalledUICulture;
                    }
                    s_threadAdjustmentLock.Acquire();
                    ThreadCounts counts = ThreadCounts.VolatileReadCounts(ref s_counts);
                    while (true)
                    {
                        if (counts.numActive == counts.numWorking)
                        {
                            s_threadAdjustmentLock.Release();
                            break;
                        }

                        ThreadCounts newCounts = counts;
                        newCounts.numActive--;
                        newCounts.maxWorking = (short)Math.Max(s_minThreads, Math.Min(newCounts.numActive, newCounts.maxWorking));
                        ThreadCounts oldCounts = ThreadCounts.CompareExchangeCounts(ref s_counts, newCounts, counts);
                        if (oldCounts == counts)
                        {
                            s_threadAdjustmentLock.Release();
                            HillClimbing.ThreadPoolHillClimber.ForceChange(newCounts.maxWorking, HillClimbing.StateOrTransition.ThreadTimedOut);
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
                    newCounts.numWorking = Math.Max(counts.numWorking, Math.Min((short)(counts.numWorking + 1), counts.maxWorking));
                    newCounts.numActive = Math.Max(counts.numActive, newCounts.numWorking);
                    
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

                short toCreate = (short)(newCounts.numActive - counts.numActive);
                int toRelease = newCounts.numWorking - counts.numWorking;

                if(toRelease > 0)
                {
                    s_semaphore.Release(toRelease);
                }

                for (int i = 0; i < toCreate; i++)
                {
                    CreateWorkerThread();
                }
            }

            private static void CreateWorkerThread()
            {
                RuntimeThread workerThread = RuntimeThread.Create(WorkerThreadStart);
                workerThread.IsThreadPoolThread = true;
                workerThread.Start();
            }
        }
    }
}
