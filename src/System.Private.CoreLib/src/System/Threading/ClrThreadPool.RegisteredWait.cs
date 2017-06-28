// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Internal.Runtime.Augments;

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
                TimeoutTime = millisecondsTimeout;
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
            internal int TimeoutTime { get; }

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
            /// The <see cref="ClrThreadPool.WaitThread"/> this <see cref="RegisteredWait"/> was registered on.
            /// </summary>
            internal WaitThread WaitThread { get; set; }

            private int _unregisterCalled;

            internal bool Unregister(WaitHandle waitObject)
            {
                if (Interlocked.CompareExchange(ref _unregisterCalled, 1, 0) == 0)
                {
                    UserUnregisterWaitHandle = waitObject;
                    WaitThread.QueueOrExecuteUnregisterWait(this);
                    return true;
                }
                return false;
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
                try
                {
                    if (!SignaledUserWaitHandle)
                    {
                        _ThreadPoolWaitOrTimerCallback.PerformWaitOrTimerCallback(Callback, timedOut);
                    }
                }
                finally
                {
                    SignalAndCallbackLock.Release();
                }
            }
        }

    }
}
