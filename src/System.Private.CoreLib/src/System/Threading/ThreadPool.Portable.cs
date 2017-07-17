// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace System.Threading
{
    //
    // Portable implementation of ThreadPool
    //
    public sealed class RegisteredWaitHandle : MarshalByRefObject
    {
        public bool Unregister(WaitHandle waitObject)
        {
            // UNIXTODO: ThreadPool
            throw new NotImplementedException();
        }
    }

    public static partial class ThreadPool
    {
        public static bool SetMaxThreads(int workerThreads, int completionPortThreads)
        {
            if (workerThreads < 0 || completionPortThreads < 0)
            {
                return false;
            }
            return ClrThreadPool.ThreadPoolInstance.SetMaxThreads(workerThreads);
        }

        public static void GetMaxThreads(out int workerThreads, out int completionPortThreads)
        {
            // Note that worker threads and completion port threads share the same thread pool.
            // The total number of threads cannot exceed MaxThreadCount.
            workerThreads = ClrThreadPool.ThreadPoolInstance.GetMaxThreads();
            completionPortThreads = 1;
        }

        public static bool SetMinThreads(int workerThreads, int completionPortThreads)
        {
            if (workerThreads < 0 || completionPortThreads < 0)
            {
                return false;
            }
            return ClrThreadPool.ThreadPoolInstance.SetMinThreads(workerThreads);
        }

        public static void GetMinThreads(out int workerThreads, out int completionPortThreads)
        {
            // All threads are pre-created at present
            workerThreads = ClrThreadPool.ThreadPoolInstance.GetMinThreads();
            completionPortThreads = 0;
        }

        public static void GetAvailableThreads(out int workerThreads, out int completionPortThreads)
        {
            workerThreads = ClrThreadPool.ThreadPoolInstance.GetAvailableThreads();
            completionPortThreads = 0;
        }

        /// <summary>
        /// This method is called to request a new thread pool worker to handle pending work.
        /// </summary>
        internal static void QueueDispatch()
        {
            ClrThreadPool.ThreadPoolInstance.RequestWorker();
        }
        
        internal static void NotifyWorkItemProgress()
        {
            ClrThreadPool.ThreadPoolInstance.NotifyWorkItemComplete();
        }

        internal static bool NotifyWorkItemComplete()
        {
            return ClrThreadPool.ThreadPoolInstance.NotifyWorkItemComplete();
        }

        private static RegisteredWaitHandle RegisterWaitForSingleObject(
             WaitHandle waitObject,
             WaitOrTimerCallback callBack,
             Object state,
             uint millisecondsTimeOutInterval,
             bool executeOnlyOnce,
             bool flowExecutionContext)
        {
            //
            // This is just a quick-and-dirty implementation to make TaskFactory.FromAsync
            // work for the few apps that are using it.  A proper implementation would coalesce
            // multiple waits onto a single thread, so that fewer machine resources would be
            // consumed.
            //

            Debug.Assert(executeOnlyOnce);

            QueueUserWorkItem(_ =>
            {
                bool timedOut = waitObject.WaitOne((int)millisecondsTimeOutInterval);
                callBack(state, timedOut);
            });

            return new RegisteredWaitHandle();
        }
    }
}
