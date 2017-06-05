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
        internal RegisteredWaitHandle(WaitHandle waitHandle, _ThreadPoolWaitOrTimerCallback callbackHelper,
            uint millisecondsTimeout, bool repeating)
        {
            Handle = waitHandle;
            Callback = callbackHelper;
            Timeout = (int)millisecondsTimeout;
            Repeating = repeating;
        }

        internal _ThreadPoolWaitOrTimerCallback Callback { get; }
        internal WaitHandle Handle { get; }
        internal int Timeout { get; }
        internal bool Repeating { get; }
        internal WaitHandle UserUnregisterWaitHandle { get; private set; }
        private bool SignaledUserWaitHandle { get; set; } = false;
        private LowLevelLock SignalUserWaitHandleLock { get; } = new LowLevelLock();

        internal ManualResetEventSlim CanUnregister { get; } = new ManualResetEventSlim(true);

        public bool Unregister(WaitHandle waitObject)
        {
            UserUnregisterWaitHandle = waitObject;
            WaitThread.QueueUnregisterWait(this);
            return true;
        }

        internal void SignalUserWaitHandle()
        {
            SignalUserWaitHandleLock.Acquire();
            if(!SignaledUserWaitHandle && UserUnregisterWaitHandle != null && UserUnregisterWaitHandle.SafeWaitHandle.DangerousGetHandle() != (IntPtr)(-1))
            {
                SignaledUserWaitHandle = true;
                WaitSubsystem.SetEvent(UserUnregisterWaitHandle.SafeWaitHandle.DangerousGetHandle());
            }
            SignalUserWaitHandleLock.Release();
        }
    }

    public static partial class ThreadPool
    {
        // TODO: this is a very primitive (temporary) implementation of Thread Pool to allow Tasks to be
        // used on Unix. All of this code must be replaced with proper implementation.

        /// <summary>
        /// Max allowed number of threads in the thread pool. This is just arbitrary number
        /// that is used to prevent unbounded creation of threads.
        /// It should by high enough to provide sufficient number of thread pool workers
        /// in case if some threads get blocked while running user code.
        /// </summary>
        private static readonly int MaxThreadCount = 4 * ThreadPoolGlobals.processorCount;

        /// <summary>
        /// Semaphore that is used to release waiting thread pool workers when new work becomes available.
        /// </summary>
        private static SemaphoreSlim s_semaphore = new SemaphoreSlim(0);

        /// <summary>
        /// Number of worker threads created by the thread pool.
        /// </summary>
        private static volatile int s_workerCount = 0;

        public static bool SetMaxThreads(int workerThreads, int completionPortThreads)
        {
            // Not supported at present
            return false;
        }

        public static void GetMaxThreads(out int workerThreads, out int completionPortThreads)
        {
            // Note that worker threads and completion port threads share the same thread pool.
            // The total number of threads cannot exceed MaxThreadCount.
            workerThreads = MaxThreadCount;
            completionPortThreads = MaxThreadCount;
        }

        public static bool SetMinThreads(int workerThreads, int completionPortThreads)
        {
            // Not supported at present
            return false;
        }

        public static void GetMinThreads(out int workerThreads, out int completionPortThreads)
        {
            // All threads are pre-created at present
            workerThreads = MaxThreadCount;
            completionPortThreads = MaxThreadCount;
        }

        public static void GetAvailableThreads(out int workerThreads, out int completionPortThreads)
        {
            // Make sure we return a non-negative value if thread pool defaults are changed
            int availableThreads = Math.Max(MaxThreadCount - ThreadPoolGlobals.workQueue.numWorkingThreads, 0);

            workerThreads = availableThreads;
            completionPortThreads = availableThreads;
        }

        /// <summary>
        /// This method is called to request a new thread pool worker to handle pending work.
        /// </summary>
        internal static void QueueDispatch()
        {
            // For simplicity of the state management, we pre-create all thread pool workers on the first
            // request and then use the semaphore to release threads as new requests come in.
            if ((s_workerCount == 0) && Interlocked.Exchange(ref s_workerCount, MaxThreadCount) == 0)
            {
                for (int i = 0; i < MaxThreadCount; i++)
                {
                    if (!Interop.Sys.RuntimeThread_CreateThread(IntPtr.Zero /*use default stack size*/,
                        AddrofIntrinsics.AddrOf<Interop.Sys.ThreadProc>(ThreadPoolDispatchCallback), IntPtr.Zero))
                    {
                        throw new OutOfMemoryException();
                    }
                }
            }

            // Release one thread to handle the new request
            s_semaphore.Release(1);
        }

        /// <summary>
        /// This method is an entry point of a thread pool worker thread.
        /// </summary>
        [NativeCallable]
        private static IntPtr ThreadPoolDispatchCallback(IntPtr context)
        {
            RuntimeThread.InitializeThreadPoolThread();

            do
            {
                // Handle pending requests
                ThreadPoolWorkQueue.Dispatch();

                // Wait for new requests to arrive
                s_semaphore.Wait();

            } while (true);
        }

        private static RegisteredWaitHandle RegisterWaitForSingleObject(
             WaitHandle waitObject,
             WaitOrTimerCallback callBack,
             Object state,
             uint millisecondsTimeOutInterval,
             bool executeOnlyOnce,
             bool flowExecutionContext)
        {
            RegisteredWaitHandle registeredHandle = new RegisteredWaitHandle(
                waitObject,
                new _ThreadPoolWaitOrTimerCallback(callBack, state, flowExecutionContext),
                millisecondsTimeOutInterval,
                !executeOnlyOnce);
            WaitThread.RegisterWaitHandle(registeredHandle);
            return registeredHandle;
        }
    }
}
