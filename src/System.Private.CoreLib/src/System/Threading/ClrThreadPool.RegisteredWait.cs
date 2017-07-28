// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Internal.Runtime.Augments;
using Microsoft.Win32.SafeHandles;

namespace System.Threading
{
    internal partial class ClrThreadPool
    {
        /// <summary>
        /// An object representing the registration of a <see cref="WaitHandle"/> via <see cref="ThreadPool.RegisterWaitForSingleObject"/>.
        /// </summary>
        public sealed class RegisteredWait
        {
            internal RegisteredWait(WaitHandle waitHandle, _ThreadPoolWaitOrTimerCallback callbackHelper,
                int millisecondsTimeout, bool repeating)
            {
                Handle = waitHandle;
                Callback = callbackHelper;
                TimeoutDurationMs = millisecondsTimeout;
                Repeating = repeating;
                RestartTimeout(Environment.TickCount);
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

            public bool InfiniteTimeout => TimeoutDurationMs == -1;

            public void RestartTimeout(int currentTimeMs)
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

            /// <summary>
            /// Whether or not <see cref="UserUnregisterWaitHandle"/> has been signaled yet.
            /// </summary>
            private volatile int _unregisterSignaled;

            public bool IsBlocking { get; set; } = true;

            /// <summary>
            /// The <see cref="ClrThreadPool.WaitThread"/> this <see cref="RegisteredWait"/> was registered on.
            /// </summary>
            internal WaitThread WaitThread { get; set; }

            private int _unregisterCalled;

            private AutoResetEvent _unregisteredEvent = new AutoResetEvent(false);

            internal bool Unregister(WaitHandle waitObject)
            {
                if (Interlocked.Exchange(ref _unregisterCalled, 1) == 0)
                {
                    UserUnregisterWaitHandle = waitObject?.SafeWaitHandle;
                    if (!(UserUnregisterWaitHandle?.IsInvalid ?? true))
                    {
                        UserUnregisterWaitHandle.DangerousAddRef();
                        UserUnregisterWaitHandleValue = UserUnregisterWaitHandle.DangerousGetHandle();
                        IsBlocking = UserUnregisterWaitHandleValue == new IntPtr(-1);
                    }
                    else
                    {
                        IsBlocking = false;
                    }

                    if (_unregisterSignaled == 0)
                    {
                        WaitThread.QueueOrExecuteUnregisterWait(this);
                    }
                    return true;
                }
                return false;
            }

            /// <summary>
            /// Signal <see cref="UserUnregisterWaitHandle"/> if it has not been signaled yet and is a valid handle.
            /// </summary>
            private void SignalUserWaitHandle()
            {
                if (Interlocked.Exchange(ref _unregisterSignaled, 1) == 0)
                {
                    var handle = UserUnregisterWaitHandle;
                    if (UserUnregisterWaitHandleValue != new IntPtr(-1))
                    {
                        try
                        {
                            EventWaitHandle.Set(handle);
                        }
                        finally
                        {
                            handle.DangerousRelease();
                        }
                    }
                    _unregisteredEvent.Set();
                }
            }

            /// <summary>
            /// Perform the registered callback if the <see cref="UserUnregisterWaitHandle"/> has not been signaled.
            /// </summary>
            /// <param name="timedOut">Whether or not the wait timed out.</param>
            internal void PerformCallback(bool timedOut)
            {
                if (_unregisterSignaled == 0)
                {
                    _ThreadPoolWaitOrTimerCallback.PerformWaitOrTimerCallback(Callback, timedOut);
                }
                CompleteCallbackRequest();
            }

            private volatile int _numRequestedCallbacks;
            private LowLevelLock _callbackLock = new LowLevelLock();
            private bool _signalAfterCallbacksComplete;

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

            internal void TrySignalUserWaitHandle()
            {
                _callbackLock.Acquire();
                try
                {

                    if (UserUnregisterWaitHandle != null && UserUnregisterWaitHandleValue != new IntPtr(-1))
                    {
                        if (_numRequestedCallbacks == 0)
                        {
                            SignalUserWaitHandle();
                        }
                        else
                        {
                            _signalAfterCallbacksComplete = true;
                        }
                    }
                }
                finally
                {
                    _callbackLock.Release();
                }
            }

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

            private int callCount = 0;
            internal void BlockOnUnregistration()
            {
                ++callCount;
                Debug.Assert(callCount == 1);
                _unregisteredEvent.WaitOne();
            }
        }
    }
}
