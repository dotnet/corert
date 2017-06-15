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
            enum Status
            {
                NotRunning = 0,
                Requested = 1,
                WaitingForRequest = 2
            }

            private const int GateThreadDelayMs = 500;
            private const int DequeueDelayThreshold = GateThreadDelayMs * 2;

            private static bool s_enableWorkerTracking; // TODO: Config
            private static bool s_disableStarvationDetection = true; // TODO: Config
            private static bool s_debuggerBreakOnWorkStarvation; // TODO: Config

            private static int s_status; // This needs to be an int instead of Status so we can use Interlocked.CompareExchange with it

            private static void GateThreadStart()
            {
                // TODO: Do we need to do this? (comment pulled from CoreCLR. Comment older than CoreCLR)
                RuntimeThread.Sleep(GateThreadDelayMs);
                Interop.Sys.ProcessCpuInformation cpuInfo = new Interop.Sys.ProcessCpuInformation();
                Interop.Sys.GetCpuUtilization(ref cpuInfo); // ignore return value the first time. The first time populates the cpuInfo structure to calculate in future calls.

                do
                {
                    RuntimeThread.Sleep(GateThreadDelayMs);
                    if (s_enableWorkerTracking)
                    {
                        // TODO: Event: Working Thread Count event
                    }

                    s_cpuUtilization = Interop.Sys.GetCpuUtilization(ref cpuInfo); // updates cpuInfo as side effect

                    if (!s_disableStarvationDetection)
                    {
                        if (/* s_numRequestedWorkers > 0 && */ SufficientDelaySinceLastDequeue())
                        {
                            ThreadCounts counts = ThreadCounts.VolatileReadCounts(ref s_separated.counts);
                            // don't add a thread if we're at max or if we are already in the process of adding threads
                            while (counts.numExistingThreads < s_maxThreads && counts.numExistingThreads >= counts.numThreadsGoal)
                            {
                                if (s_debuggerBreakOnWorkStarvation)
                                {
                                    Debug.WriteLine("The CLR ThreadPool detected work starvation!");
                                    Debugger.Break();
                                }

                                ThreadCounts newCounts = counts;
                                newCounts.numThreadsGoal = (short)(newCounts.numExistingThreads + 1);
                                ThreadCounts oldCounts = ThreadCounts.CompareExchangeCounts(ref s_separated.counts, newCounts, counts);
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
                } while (ShouldGateThreadKeepRunning());
            }

            // called by logic to spawn new worker threads, return true if it's been too long
            // since the last dequeue operation - takes number of worker threads into account
            // in deciding "too long"
            private static bool SufficientDelaySinceLastDequeue()
            {
                int delay = Environment.TickCount - Volatile.Read(ref s_separated.lastDequeueTime);

                int minimumDelay;

                if(s_cpuUtilization < CpuUtilizationLow)
                {
                    minimumDelay = GateThreadDelayMs;
                }
                else
                {
                    ThreadCounts counts = ThreadCounts.VolatileReadCounts(ref s_separated.counts);
                    int numThreads = counts.numThreadsGoal;
                    minimumDelay = numThreads * DequeueDelayThreshold;
                }

                return delay > minimumDelay;
            }

            private static bool ShouldGateThreadKeepRunning()
            {
                Debug.Assert(s_status == (int)Status.WaitingForRequest || s_status == (int)Status.Requested);

                // Switch to WaitingForRequest and see if we had a request since the last check.
                Status previousStatus = (Status)Interlocked.Exchange(ref s_status, (int)Status.WaitingForRequest);
                if(previousStatus == Status.WaitingForRequest)
                {
                    //
                    // No recent requests for the gate thread.  Check to see if we're still needed.
                    //

                    //
                    // Are there any work requests in any worker queue?  If so, we need a gate thread.
                    // This imples that whenever a work queue goes from empty to non-empty, we need to call EnsureGateThreadRunning().
                    //
                    bool needGateThreadForWorkerThreads = false;
                    /* = s_numWorkerRequests > 0 */

                    bool needGateThreadForWorkerTracking = s_enableWorkerTracking;

                    if(!(needGateThreadForWorkerThreads || needGateThreadForWorkerTracking))
                    {
                        previousStatus = (Status)Interlocked.CompareExchange(ref s_status, (int)Status.NotRunning, (int)Status.WaitingForRequest);
                        if (previousStatus == Status.WaitingForRequest)
                        {
                            return false;
                        }
                    }
                }

                Debug.Assert(s_status == (int)Status.WaitingForRequest || s_status == (int)Status.Requested);

                return true;
            }

            internal static void CreateGateThread()
            {
                RuntimeThread.Create(GateThreadStart);
            }

            // This is called by a worker thread
            private static void EnsureRunning()
            {
                while (true)
                {
                    switch ((Status)s_status)
                    {
                        case Status.Requested:
                            //
                            // No action needed; the gate thread is running, and someone else has already registered a request
                            // for it to stay.
                            //
                            return;
                        case Status.WaitingForRequest:
                            //
                            // Prevent the gate thread from exiting, if it hasn't already done so.  If it has, we'll create it on the next iteration of
                            // this loop.
                            //
                            Interlocked.CompareExchange(ref s_status, (int)Status.Requested, (int)Status.WaitingForRequest);
                            break;
                        case Status.NotRunning:
                            //
                            // We need to create a new gate thread
                            //
                            if ((Status)Interlocked.CompareExchange(ref s_status, (int)Status.Requested, (int)Status.NotRunning) == Status.NotRunning)
                            {
                                CreateGateThread();
                                return;
                            }
                            break;
                        default:
                            Debug.Assert(false, "Invalid value of ClrThreadPool.GateThread.Status");
                            break;
                    }
                }
            }
        }
    }
}