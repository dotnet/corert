// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Runtime.Augments;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace System.Threading
{
    //
    // Unix-specific implementation of ThreadPool
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
            return ClrThreadPool.SetMaxThreads(workerThreads);
        }

        public static void GetMaxThreads(out int workerThreads, out int completionPortThreads)
        {
            completionPortThreads = workerThreads = ClrThreadPool.GetMaxThreads();
        }

        public static bool SetMinThreads(int workerThreads, int completionPortThreads)
        {
            return ClrThreadPool.SetMinThreads(workerThreads);
        }

        public static void GetMinThreads(out int workerThreads, out int completionPortThreads)
        {
            completionPortThreads = workerThreads = ClrThreadPool.GetMinThreads();
        }

        public static void GetAvailableThreads(out int workerThreads, out int completionPortThreads)
        {
            completionPortThreads = workerThreads = ClrThreadPool.GetAvailableThreads();
        }

        /// <summary>
        /// This method is called to request a new thread pool worker to handle pending work.
        /// </summary>
        internal static void QueueDispatch()
        {
            ClrThreadPool.WorkerThread.MaybeAddWorkingWorker();
            // TODO: Ensure gate thread is running here.
        }

        internal static bool NotifyWorkItemComplete()
        {
            return ClrThreadPool.NotifyWorkItemComplete();
        }

        internal static void NotifyWorkItemProgress()
        {
            ClrThreadPool.NotifyWorkItemComplete();
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
