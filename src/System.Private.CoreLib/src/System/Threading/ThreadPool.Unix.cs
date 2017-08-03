// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

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
            int millisecondsTimeout, bool repeating)
        {
            Handle = waitHandle;
            Callback = callbackHelper;
            TimeoutDurationMs = millisecondsTimeout;
            Repeating = repeating;
            RestartTimeout(Environment.TickCount);
        }

        ~RegisteredWaitHandle()
        {
            if(WaitThread != null)
            {
                Unregister(null);
            }
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
        /// The time this handle times out at in ms.
        /// </summary>
        internal int TimeoutTimeMs { get; private set; }

        private int TimeoutDurationMs { get; }

        internal bool IsInfiniteTimeout => TimeoutDurationMs == -1;

        internal void RestartTimeout(int currentTimeMs)
        {
            TimeoutTimeMs = currentTimeMs + TimeoutDurationMs;
        }

        /// <summary>
        /// Whether or not the wait is a repeating wait.
        /// </summary>
        internal bool Repeating { get; }

        /// <summary>
        /// The <see cref="WaitHandle"/> the user passed in via <see cref="Unregister(WaitHandle)"/>.
        /// </summary>
        private SafeWaitHandle UserUnregisterWaitHandle { get; set; } = new SafeWaitHandle((IntPtr)(-1), false); // Initialize with an invalid handle like CoreCLR

        private IntPtr UserUnregisterWaitHandleValue { get; set; } = new IntPtr(-1);

        internal bool IsBlocking => UserUnregisterWaitHandleValue == (IntPtr)(-1);

        /// <summary>
        /// The <see cref="ClrThreadPool.WaitThread"/> this <see cref="RegisteredWaitHandle"/> was registered on.
        /// </summary>
        internal ClrThreadPool.WaitThread WaitThread { get; set; }

        /// <summary>
        /// The number of callbacks that are currently queued on the Thread Pool or executing.
        /// </summary>
        private int _numRequestedCallbacks;

        private LowLevelLock _callbackLock = new LowLevelLock();

        /// <summary>
        /// Notes if we need to signal the user's unregister event after all callbacks complete.
        /// </summary>
        private bool _signalAfterCallbacksComplete;

        private int _unregisterCalled;

        private bool _unregistered;

        private AutoResetEvent _callbacksComplete;

        private AutoResetEvent _removed;

        /// <summary>
        /// Unregisters this wait handle registration from the wait threads.
        /// </summary>
        /// <param name="waitObject">The event to signal when the handle is unregistered.</param>
        /// <returns>If the handle was successfully marked to be removed and the provided wait handle was set as the user provided event.</returns>
        /// <remarks>
        /// This method will only return true on the first call.
        /// Passing in a wait handle with a value of -1 will result in a blocking wait, where Unregister will not return until the full unregistration is completed.
        /// </remarks>
        public bool Unregister(WaitHandle waitObject)
        {
            if (Interlocked.Exchange(ref _unregisterCalled, 1) == 0)
            {
                GC.SuppressFinalize(this);

                _callbackLock.Acquire();
                try
                {
                    UserUnregisterWaitHandle = waitObject?.SafeWaitHandle;
                    UserUnregisterWaitHandle?.DangerousAddRef();
                    UserUnregisterWaitHandleValue = UserUnregisterWaitHandle?.DangerousGetHandle() ?? IntPtr.Zero;
                    if (_unregistered)
                    {
                        SignalUserWaitHandle();
                        return true;
                    }

                    _removed = new AutoResetEvent(false);

                    if (IsBlocking)
                    {
                        _callbacksComplete = new AutoResetEvent(false);
                    }
                }
                finally
                {
                    _callbackLock.Release();
                }

                WaitThread.UnregisterWait(this);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Signal <see cref="UserUnregisterWaitHandle"/> if it has not been signaled yet and is a valid handle.
        /// </summary>
        private void SignalUserWaitHandle()
        {
            SafeWaitHandle handle = UserUnregisterWaitHandle;
            IntPtr handleValue = UserUnregisterWaitHandleValue;
            try
            {
                if (handleValue != IntPtr.Zero && handleValue != (IntPtr)(-1))
                {
                    EventWaitHandle.Set(handleValue);
                }
            }
            finally
            {
                handle?.DangerousRelease();
                _callbacksComplete?.Set();
                _unregistered = true;
            }
        }

        /// <summary>
        /// Perform the registered callback if the <see cref="UserUnregisterWaitHandle"/> has not been signaled.
        /// </summary>
        /// <param name="timedOut">Whether or not the wait timed out.</param>
        internal void PerformCallback(bool timedOut)
        {
#if DEBUG
            _callbackLock.Acquire();
            try
            {
                Debug.Assert(_numRequestedCallbacks != 0);
            }
            finally
            {
                _callbackLock.Release();
            }
#endif
            _ThreadPoolWaitOrTimerCallback.PerformWaitOrTimerCallback(Callback, timedOut);
            CompleteCallbackRequest();
        }

        /// <summary>
        /// Tell this handle that there is a callback queued on the thread pool for this handle.
        /// </summary>
        internal void RequestCallback()
        {
            _callbackLock.Acquire();
            try
            {
                _numRequestedCallbacks++;
            }
            finally
            {
                _callbackLock.Release();
            }
        }

        /// <summary>
        /// Called when the wait thread removes this handle registration. This will signal the user's event if there are no callbacks pending,
        /// or note that the user's event must be signaled when the callbacks complete.
        /// </summary>
        internal void OnRemoveWait()
        {
            _callbackLock.Acquire();
            try
            {
                _removed?.Set();
                if (_numRequestedCallbacks == 0)
                {
                    SignalUserWaitHandle();
                }
                else
                {
                    _signalAfterCallbacksComplete = true;
                }
            }
            finally
            {
                _callbackLock.Release();
            }
        }

        /// <summary>
        /// Reduces the number of callbacks requested. If there are no more callbacks and the user's handle is queued to be signaled, signal it.
        /// </summary>
        private void CompleteCallbackRequest()
        {
            _callbackLock.Acquire();
            try
            {
                --_numRequestedCallbacks;
                if (_numRequestedCallbacks == 0 && _signalAfterCallbacksComplete)
                {
                    SignalUserWaitHandle();
                }
            }
            finally
            {
                _callbackLock.Release();
            }
        }

        internal bool ShouldWaitForCallbacks
        {
            get
            {
                _callbackLock.Acquire();
                try
                {
                    return _signalAfterCallbacksComplete;
                }
                finally
                {
                    _callbackLock.Release();
                }
            }
        }

        /// <summary>
        /// Wait for all queued callbacks and the full unregistration to complete.
        /// </summary>
        internal void WaitForCallbacks()
        {
            Debug.Assert(IsBlocking);
            _callbacksComplete.WaitOne();
        }

        internal void WaitForRemoval()
        {
            Debug.Assert(_unregisterCalled == 1); // Should only be called when the wait is unregistered by the user.
            _removed.WaitOne();
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

        internal static bool KeepDispatching(int startTickCount)
        {
            return true;
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
            var wrapper = ThreadPoolCallbackWrapper.Enter();

            do
            {
                // Handle pending requests
                ThreadPoolWorkQueue.Dispatch();

                // Wait for new requests to arrive
                s_semaphore.Wait();

            } while (true);

            //wrapper.Exit(resetThread: false);
        }

        private static RegisteredWaitHandle RegisterWaitForSingleObject(
             WaitHandle waitObject,
             WaitOrTimerCallback callBack,
             Object state,
             uint millisecondsTimeOutInterval,
             bool executeOnlyOnce,
             bool flowExecutionContext)
        {
            if (waitObject == null)
                throw new ArgumentNullException(nameof(waitObject));

            if (callBack == null)
                throw new ArgumentNullException(nameof(callBack));

            RegisteredWaitHandle registeredHandle = new RegisteredWaitHandle(
                waitObject,
                new _ThreadPoolWaitOrTimerCallback(callBack, state, flowExecutionContext),
                (int)millisecondsTimeOutInterval,
                !executeOnlyOnce);
            ClrThreadPool.ThreadPoolInstance.RegisterWaitHandle(registeredHandle);
            return registeredHandle;
        }
    }
}
