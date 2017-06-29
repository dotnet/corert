// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
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
            private SafeWaitHandle UserUnregisterWaitHandle { get; set; } = new SafeWaitHandle((IntPtr)(-1), false); // Initialize with an invalid handle like CoreCLR

            /// <summary>
            /// Whether or not <see cref="UserUnregisterWaitHandle"/> has been signaled yet.
            /// </summary>
            private volatile int _unregisterSignaled;

            public bool IsUnregistered => _unregisterSignaled != 0;

            public bool IsBlocking => UserUnregisterWaitHandle?.IsInvalid ?? false;

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
                if (Interlocked.Exchange(ref _unregisterCalled, 1) == 0)
                {
                    UserUnregisterWaitHandle = waitObject?.SafeWaitHandle;
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
                if (Interlocked.Exchange(ref _unregisterSignaled, 1) == 0)
                {
                    CanUnregister.WaitOne();

                    if (UserUnregisterWaitHandle != null)
                    {
                        if (!UserUnregisterWaitHandle.IsInvalid)
                        {
                            WaitHandle.Set(UserUnregisterWaitHandle);
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
                if (_unregisterSignaled == 0)
                {
                    _ThreadPoolWaitOrTimerCallback.PerformWaitOrTimerCallback(Callback, timedOut);
                }
            }
        }

    }
}
