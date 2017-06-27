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

    /// <summary>
    /// An object representing the registration of a <see cref="WaitHandle"/> via <see cref="ThreadPool.RegisterWaitForSingleObject"/>.
    /// </summary>
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

        /// <summary>
        /// The callback to execute when the wait on <see cref="Handle"/> either times out or completes.
        /// </summary>
        internal _ThreadPoolWaitOrTimerCallback Callback { get; }

        /// <summary>
        /// The <see cref="WaitHandle"/> that was registered.
        /// </summary>
        internal WaitHandle Handle { get; }

        /// <summary>
        /// The timeout the handle was registered with.
        /// </summary>
        internal int Timeout { get; }

        /// <summary>
        /// Whether or not the wait is a repeating wait.
        /// </summary>
        internal bool Repeating { get; }

        /// <summary>
        /// The <see cref="WaitHandle"/> the user passed in via <see cref="Unregister(WaitHandle)"/>.
        /// </summary>
        internal WaitHandle UserUnregisterWaitHandle { get; private set; }
        /// <summary>
        /// Whether or not <see cref="UserUnregisterWaitHandle"/> has been signaled yet.
        /// </summary>
        private bool SignaledUserWaitHandle { get; set; } = false;
        /// <summary>
        /// A lock around accesses to <see cref="SignaledUserWaitHandle"/>.
        /// </summary>
        private LowLevelLock SignalAndCallbackLock { get; } = new LowLevelLock();

        /// <summary>
        /// A <see cref="ManualResetEvent"/> that allows a <see cref="ClrThreadPool.WaitThread"/> to control when exactly this handle is unregistered.
        /// </summary>
        internal ManualResetEvent CanUnregister { get; } = new ManualResetEvent(true);

        /// <summary>
        /// The <see cref="ClrThreadPool.WaitThread"/> this <see cref="RegisteredWaitHandle"/> was registered on.
        /// </summary>
        internal ClrThreadPool.WaitThread WaitThread { get; set; }

        public bool Unregister(WaitHandle waitObject)
        {
            UserUnregisterWaitHandle = waitObject;
            WaitThread.QueueOrExecuteUnregisterWait(this);
            return true;
        }

        /// <summary>
        /// Signal <see cref="UserUnregisterWaitHandle"/> if it has not been signaled yet and is a valid handle.
        /// </summary>
        internal void SignalUserWaitHandle()
        {
            SignalAndCallbackLock.Acquire();
            try
            {
                if (!SignaledUserWaitHandle && UserUnregisterWaitHandle != null && UserUnregisterWaitHandle.SafeWaitHandle.DangerousGetHandle() != (IntPtr)(-1))
                {
                    SignaledUserWaitHandle = true;
                    WaitHandle.Set(UserUnregisterWaitHandle.SafeWaitHandle);
                }
            }
            finally
            {
                SignalAndCallbackLock.Release();
            }
        }

        /// <summary>
        /// Perform the registered callback if the <see cref="UserUnregisterWaitHandle"/> has not been signaled.
        /// </summary>
        /// <param name="timedOut">Whether or not the wait timed out.</param>
        internal void PerformCallback(bool timedOut)
        {
            SignalAndCallbackLock.Acquire();
            if(!SignaledUserWaitHandle)
            {
                _ThreadPoolWaitOrTimerCallback.PerformWaitOrTimerCallback(Callback, timedOut);
            }
            SignalAndCallbackLock.Release();
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
        
        internal static void NotifyWorkItemProgress()
        {
        }

        internal static bool NotifyWorkItemComplete()
        {
            return true;
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
            ClrThreadPool.RegisterWaitHandle(registeredHandle);
            return registeredHandle;
        }
    }
}
