using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Internal.Runtime.Augments;

namespace System.Threading
{
    internal static partial class ClrThreadPool
    {
        internal static class WorkerThread
        {
            internal static WaitSubsystem.WaitableObject s_semaphore = WaitSubsystem.WaitableObject.NewSemaphore(0, int.MaxValue);

            private const int TimeoutMs = 20 * 1000;
            
            private static IntPtr WorkerThreadStart(IntPtr context)
            {
                RuntimeThread.InitializeThreadPoolThread();
                // TODO: Event: Worker Thread Start event

                while (true)
                {
                    // TODO: Event:  Worker thread wait event
                    while (WaitSubsystem.Wait(s_semaphore, TimeoutMs, false, true))
                    {
                        if(!ThreadPoolWorkQueue.Dispatch())
                        {
                            return IntPtr.Zero;
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
                            return IntPtr.Zero;
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
                    WaitSubsystem.ReleaseSemaphore(s_semaphore, toRelease);
                }

                while(toCreate > 0)
                {
                    if(CreateWorkerThread())
                    {
                        toCreate--;
                    }
                    else
                    {
                        //
                        // Uh-oh, we promised to create a new thread, but the creation failed.  We have to renege on our
                        // promise.  This may possibly result in no work getting done for a while, but the gate thread will
                        // eventually notice that no completions are happening and force the creation of a new thread.
                        // Of course, there's no guarantee *that* will work - but hopefully enough time will have passed
                        // to allow whoever's using all the memory right now to release some.
                        //

                        counts = ThreadCounts.VolatileReadCounts(ref s_counts);
                        while(true)
                        {
                            newCounts = counts;
                            newCounts.numWorking -= toCreate;
                            newCounts.numActive -= toCreate;

                            ThreadCounts oldCounts = ThreadCounts.CompareExchangeCounts(ref s_counts, newCounts, counts);

                            if (oldCounts == counts)
                            {
                                break;
                            }

                            counts = oldCounts;
                        }

                        toCreate = 0;
                    }
                }
            }

            private static bool CreateWorkerThread()
            {
                return Interop.Sys.RuntimeThread_CreateThread(IntPtr.Zero /*use default stack size*/,
                    AddrofIntrinsics.AddrOf<Interop.Sys.ThreadProc>(WorkerThreadStart), IntPtr.Zero);
            }
        }
    }
}
