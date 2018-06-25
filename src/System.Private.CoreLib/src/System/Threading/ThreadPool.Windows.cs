// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace System.Threading
{
    //
    // Windows-specific implementation of ThreadPool
    //
    public sealed class RegisteredWaitHandle : MarshalByRefObject
    {
        private readonly Lock _lock;
        private SafeWaitHandle _waitHandle;
        private readonly _ThreadPoolWaitOrTimerCallback _callbackHelper;
        private readonly uint _millisecondsTimeout;
        private bool _repeating;
        private bool _unregistering;

        // Handle to this object to keep it alive
        private GCHandle _gcHandle;

        // Pointer to the TP_WAIT structure
        private IntPtr _tpWait;

        internal RegisteredWaitHandle(SafeWaitHandle waitHandle, _ThreadPoolWaitOrTimerCallback callbackHelper,
            uint millisecondsTimeout, bool repeating)
        {
            _lock = new Lock();

            // Protect the handle from closing while we are waiting on it (VSWhidbey 285642)
            waitHandle.DangerousAddRef();
            _waitHandle = waitHandle;

            _callbackHelper = callbackHelper;
            _millisecondsTimeout = millisecondsTimeout;
            _repeating = repeating;

            // Allocate _gcHandle and _tpWait as the last step and make sure they are never leaked
            _gcHandle = GCHandle.Alloc(this);

            _tpWait = Interop.mincore.CreateThreadpoolWait(
                AddrofIntrinsics.AddrOf<Interop.mincore.WaitCallback>(RegisteredWaitCallback), (IntPtr)_gcHandle, IntPtr.Zero);

            if (_tpWait == IntPtr.Zero)
            {
                _gcHandle.Free();
                throw new OutOfMemoryException();
            }
        }

        [NativeCallable(CallingConvention = CallingConvention.StdCall)]
        internal static void RegisteredWaitCallback(IntPtr instance, IntPtr context, IntPtr wait, uint waitResult)
        {
            var wrapper = ThreadPoolCallbackWrapper.Enter();
            GCHandle handle = (GCHandle)context;
            RegisteredWaitHandle registeredWaitHandle = (RegisteredWaitHandle)handle.Target;
            Debug.Assert((handle == registeredWaitHandle._gcHandle) && (wait == registeredWaitHandle._tpWait));

            bool timedOut = (waitResult == (uint)Interop.Constants.WaitTimeout);
            registeredWaitHandle.PerformCallback(timedOut);
            wrapper.Exit();
        }

        private void PerformCallback(bool timedOut)
        {
            bool lockAcquired;
            var spinner = new SpinWait();

            // Prevent the race condition with Unregister and the previous PerformCallback call, which may still be
            // holding the _lock.
            while (!(lockAcquired = _lock.TryAcquire(0)) && !Volatile.Read(ref _unregistering))
            {
                spinner.SpinOnce();
            }

            // If another thread is running Unregister, no need to restart the timer or clean up
            if (lockAcquired)
            {
                try
                {
                    if (!_unregistering)
                    {
                        if (_repeating)
                        {
                            // Allow this wait to fire again. Restart the timer before executing the callback.
                            RestartWait();
                        }
                        else
                        {
                            // This wait will not be fired again. Free the GC handle to allow the GC to collect this object.
                            Debug.Assert(_gcHandle.IsAllocated);
                            _gcHandle.Free();
                        }
                    }
                }
                finally
                {
                    _lock.Release();
                }
            }

            _ThreadPoolWaitOrTimerCallback.PerformWaitOrTimerCallback(_callbackHelper, timedOut);
        }

        internal unsafe void RestartWait()
        {
            long timeout;
            long* pTimeout = null;  // Null indicates infinite timeout

            if (_millisecondsTimeout != Timeout.UnsignedInfinite)
            {
                timeout = -10000L * _millisecondsTimeout;
                pTimeout = &timeout;
            }

            // We can use DangerousGetHandle because of DangerousAddRef in the constructor
            Interop.mincore.SetThreadpoolWait(_tpWait, _waitHandle.DangerousGetHandle(), (IntPtr)pTimeout);
        }

        public bool Unregister(WaitHandle waitObject)
        {
            // Hold the lock during the synchronous part of Unregister (as in CoreCLR)
            using (LockHolder.Hold(_lock))
            {
                if (!_unregistering)
                {
                    // Ensure callbacks will not call SetThreadpoolWait anymore
                    _unregistering = true;

                    // Cease queueing more callbacks
                    Interop.mincore.SetThreadpoolWait(_tpWait, IntPtr.Zero, IntPtr.Zero);

                    // Should we wait for callbacks synchronously? Note that we treat the zero handle as the asynchronous case.
                    SafeWaitHandle safeWaitHandle = waitObject?.SafeWaitHandle;
                    bool blocking = ((safeWaitHandle != null) && (safeWaitHandle.DangerousGetHandle() == Interop.InvalidHandleValue));

                    if (blocking)
                    {
                        FinishUnregistering();
                    }
                    else
                    {
                        // Wait for callbacks and dispose resources asynchronously
                        ThreadPool.QueueUserWorkItem(FinishUnregisteringAsync, safeWaitHandle);
                    }

                    return true;
                }
            }
            return false;
        }

        private void FinishUnregistering()
        {
            Debug.Assert(_unregistering);

            // Wait for outstanding wait callbacks to complete
            Interop.mincore.WaitForThreadpoolWaitCallbacks(_tpWait, false);

            // Now it is safe to dispose resources
            Interop.mincore.CloseThreadpoolWait(_tpWait);
            _tpWait = IntPtr.Zero;

            if (_gcHandle.IsAllocated)
            {
                _gcHandle.Free();
            }

            Debug.Assert(_waitHandle != null);
            _waitHandle.DangerousRelease();
            _waitHandle = null;

            GC.SuppressFinalize(this);
        }

        private void FinishUnregisteringAsync(object waitObject)
        {
            FinishUnregistering();

            // Signal the provided wait object
            SafeWaitHandle safeWaitHandle = (SafeWaitHandle)waitObject;

            if ((safeWaitHandle != null) && !safeWaitHandle.IsInvalid)
            {
                Interop.Kernel32.SetEvent(safeWaitHandle);
            }
        }

        ~RegisteredWaitHandle()
        {
            // If _gcHandle is allocated, it points to this object, so this object must not be collected by the GC
            Debug.Assert(!_gcHandle.IsAllocated);

            // If this object gets resurrected and another thread calls Unregister, that creates a race condition.
            // Do not block the finalizer thread. If another thread is running Unregister, it will clean up for us.
            // The _lock may be null in case of OOM in the constructor.
            if ((_lock != null) && _lock.TryAcquire(0))
            {
                try
                {
                    if (!_unregistering)
                    {
                        _unregistering = true;

                        if (_tpWait != IntPtr.Zero)
                        {
                            // There must be no in-flight callbacks; just dispose resources
                            Interop.mincore.CloseThreadpoolWait(_tpWait);
                            _tpWait = IntPtr.Zero;
                        }

                        if (_waitHandle != null)
                        {
                            _waitHandle.DangerousRelease();
                            _waitHandle = null;
                        }
                    }
                }
                finally
                {
                    _lock.Release();
                }
            }
        }
    }

    public static partial class ThreadPool
    {
        // Time in ms for which ThreadPoolWorkQueue.Dispatch keeps executing work items before returning to the OS
        private const uint DispatchQuantum = 30;

        /// <summary>
        /// The maximum number of threads in the default thread pool on Windows 10 as computed by
        /// TppComputeDefaultMaxThreads(TppMaxGlobalPool).
        /// </summary>
        /// <remarks>
        /// Note that Windows 8 and 8.1 used a different value: Math.Max(4 * ThreadPoolGlobals.processorCount, 512).
        /// </remarks>
        private static readonly int MaxThreadCount = Math.Max(8 * ThreadPoolGlobals.processorCount, 768);

        private static IntPtr s_work;

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
            workerThreads = 0;
            completionPortThreads = 0;
        }

        public static void GetAvailableThreads(out int workerThreads, out int completionPortThreads)
        {
            // Make sure we return a non-negative value if thread pool defaults are changed
            int availableThreads = Math.Max(MaxThreadCount - ThreadPoolGlobals.workQueue.numWorkingThreads, 0);

            workerThreads = availableThreads;
            completionPortThreads = availableThreads;
        }

        internal static bool KeepDispatching(int startTickCount)
        {
            // Note: this function may incorrectly return false due to TickCount overflow
            // if work item execution took around a multiple of 2^32 milliseconds (~49.7 days),
            // which is improbable.
            return ((uint)(Environment.TickCount - startTickCount) < DispatchQuantum);
        }

        internal static void NotifyWorkItemProgress()
        {
        }

        internal static bool NotifyWorkItemComplete()
        {
            return true;
        }

        [NativeCallable(CallingConvention = CallingConvention.StdCall)]
        private static void DispatchCallback(IntPtr instance, IntPtr context, IntPtr work)
        {
            var wrapper = ThreadPoolCallbackWrapper.Enter();
            Debug.Assert(s_work == work);
            ThreadPoolWorkQueue.Dispatch();
            // We reset the thread after executing each callback
            wrapper.Exit(resetThread: false);
        }

        internal static void RequestWorkerThread()
        {
            if (s_work == IntPtr.Zero)
            {
                IntPtr nativeCallback = AddrofIntrinsics.AddrOf<Interop.mincore.WorkCallback>(DispatchCallback);

                IntPtr work = Interop.mincore.CreateThreadpoolWork(nativeCallback, IntPtr.Zero, IntPtr.Zero);
                if (work == IntPtr.Zero)
                    throw new OutOfMemoryException();

                if (Interlocked.CompareExchange(ref s_work, work, IntPtr.Zero) != IntPtr.Zero)
                    Interop.mincore.CloseThreadpoolWork(work);
            }

            Interop.mincore.SubmitThreadpoolWork(s_work);
        }

        private static RegisteredWaitHandle RegisterWaitForSingleObject(
             WaitHandle waitObject,
             WaitOrTimerCallback callBack,
             object state,
             uint millisecondsTimeOutInterval,
             bool executeOnlyOnce,
             bool flowExecutionContext)
        {
            if (waitObject == null)
                throw new ArgumentNullException(nameof(waitObject));

            if (callBack == null)
                throw new ArgumentNullException(nameof(callBack));

            var callbackHelper = new _ThreadPoolWaitOrTimerCallback(callBack, state, flowExecutionContext);
            var registeredWaitHandle = new RegisteredWaitHandle(waitObject.SafeWaitHandle, callbackHelper, millisecondsTimeOutInterval, !executeOnlyOnce);

            registeredWaitHandle.RestartWait();
            return registeredWaitHandle;
        }
    }
}
