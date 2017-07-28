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
                TimeoutTimeMs = millisecondsTimeout;
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
            /// The time this handle times out at in ticks.
            /// </summary>
            internal int TimeoutTimeMs { get; }

            /// <summary>
            /// Whether or not the wait is a repeating wait.
            /// </summary>
            internal bool Repeating { get; }

            private RecursiveEvent CanUnregister { get; } = new RecursiveEvent();

            /// <summary>
            /// The <see cref="WaitHandle"/> the user passed in via <see cref="Unregister(WaitHandle)"/>.
            /// </summary>
            private SafeWaitHandle UserUnregisterWaitHandle { get; set; } = new SafeWaitHandle((IntPtr)(-1), false); // Initialize with an invalid handle like CoreCLR

            /// <summary>
            /// Whether or not <see cref="UserUnregisterWaitHandle"/> has been signaled yet.
            /// </summary>
            private volatile int _unregisterSignaled;

            public bool IsUnregistered => _unregisterSignaled != 0;

            public bool IsBlocking { get; set; } = true;

            /// <summary>
            /// The <see cref="ClrThreadPool.WaitThread"/> this <see cref="RegisteredWait"/> was registered on.
            /// </summary>
            internal WaitThread WaitThread { get; set; }

            private int _unregisterCalled;

            private bool _automaticallyUnregistered;

            internal bool Unregister(WaitHandle waitObject)
            {
                // In this case we will never signal the user-provided handle since we've already unregistered
                // so we can just return.
                if (_unregisterSignaled == 1 && _automaticallyUnregistered) 
                {
                    return true;
                }

                _automaticallyUnregistered = false;

                if (Interlocked.Exchange(ref _unregisterCalled, 1) == 0)
                {
                    UserUnregisterWaitHandle = waitObject?.SafeWaitHandle;
                    if (!(UserUnregisterWaitHandle?.IsInvalid ?? true))
                    {
                        UserUnregisterWaitHandle.DangerousAddRef();
                        IsBlocking = UserUnregisterWaitHandle.DangerousGetHandle() == new IntPtr(-1);
                    }
                    WaitThread.QueueOrExecuteUnregisterWait(this);
                    return true;
                }
                return false;
            }

            /// <summary>
            /// Signal <see cref="UserUnregisterWaitHandle"/> if it has not been signaled yet and is a valid handle.
            /// </summary>
            internal void SignalUserWaitHandle(bool automaticallyUnregistered = false)
            {
                if (Interlocked.Exchange(ref _unregisterSignaled, 1) == 0)
                {
                    _automaticallyUnregistered = automaticallyUnregistered;
                    SafeWaitHandle handle = UserUnregisterWaitHandle;
                    if (!(handle?.IsInvalid ?? true))
                    {
                        try
                        {
                            CanUnregister.Wait();
                            EventWaitHandle.Set(handle);
                        }
                        finally
                        {
                            handle.DangerousRelease();
                        }
                    }
                }
            }

            /// <summary>
            /// Perform the registered callback if the <see cref="UserUnregisterWaitHandle"/> has not been signaled.
            /// </summary>
            /// <param name="timedOut">Whether or not the wait timed out.</param>
            internal void PerformCallback(bool timedOut)
            {
                CanUnregister.Reset(); // TODO: Refcount like setup for tracking calls into PerformCallback
                if (_unregisterSignaled == 0)
                {
                    _ThreadPoolWaitOrTimerCallback.PerformWaitOrTimerCallback(Callback, timedOut);
                }
                CanUnregister.Set();
            }


            private class RecursiveEvent
            {
                private ManualResetEvent _unregisterEvent = new ManualResetEvent(true);

                private volatile int _callbackCount;
                private LowLevelLock _callbackLock = new LowLevelLock();

                public void Set()
                {
                    _callbackLock.Acquire();
                    try
                    {
                        if (_callbackCount++ == 0)
                        {
                            _unregisterEvent.Reset();
                        }
                    }
                    finally
                    {
                        _callbackLock.Release();
                    }
                }

                public void Reset()
                {
                    _callbackLock.Acquire();
                    try
                    {
                        if (--_callbackCount == 0)
                        {
                            _unregisterEvent.Set();
                        }
                    }
                    finally
                    {
                        _callbackLock.Release();
                    }
                }

                public void Wait()
                {
                    _unregisterEvent.WaitOne(interruptible: false);
                }
            }
        }

    }
}
