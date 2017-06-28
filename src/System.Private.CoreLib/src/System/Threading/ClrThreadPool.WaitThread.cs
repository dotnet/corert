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
        /// A linked list of <see cref="WaitThread"/>s.
        /// </summary>
        private WaitThreadNode _waitThreadsHead;
        private WaitThreadNode _waitThreadsTail;

        private LowLevelLock _waitThreadListLock = new LowLevelLock();

        /// <summary>
        /// Register a wait handle on a <see cref="WaitThread"/>.
        /// </summary>
        /// <param name="handle">A description of the requested registration.</param>
        internal void RegisterWaitHandle(RegisteredWait handle)
        {
            bool needToStartWaitThread = RegisterWaitHandleOnWaitThread(handle);

            if (needToStartWaitThread)
            {
                _waitThreadsTail.Thread.Start(); // If this fails, propogate the exception up like CoreCLR
            }
        }

        private bool RegisterWaitHandleOnWaitThread(RegisteredWait handle)
        {
            _waitThreadListLock.Acquire();
            try
            {
                bool needToStartWaitThread = false;
                if (_waitThreadsHead == null) // Lazily create the first wait thread.
                {
                    _waitThreadsTail = _waitThreadsHead = new WaitThreadNode
                    {
                        Thread = new WaitThread()
                    };
                    needToStartWaitThread = true;
                }

                // Register the wait handle on the first wait thread that is not at capacity.
                WaitThreadNode prev;
                WaitThreadNode current = _waitThreadsHead;
                do
                {
                    if (current.Thread.RegisterWaitHandle(handle))
                    {
                        return needToStartWaitThread;
                    }
                    prev = current;
                    current = current.Next;
                } while (current != null);

                // If all wait threads are full, create a new one.
                prev.Next = _waitThreadsTail = new WaitThreadNode
                {
                    Thread = new WaitThread()
                };
                prev.Next.Thread.RegisterWaitHandle(handle);
                return true;
            }
            finally
            {
                _waitThreadListLock.Release();
            }
        }

        private bool TryRemoveWaitThread(WaitThread thread)
        {
            _waitThreadListLock.Acquire();
            try
            {
                if (thread.AnyUserWaits)
                {
                    return false;
                }
                RemoveWaitThread(thread);
            }
            finally
            {
                _waitThreadListLock.Release();
            }
            return true;
        }

        private void RemoveWaitThread(WaitThread thread)
        {
            if (_waitThreadsHead.Thread == thread)
            {
                _waitThreadsHead = _waitThreadsHead.Next;
                return;
            }

            WaitThreadNode prev;
            WaitThreadNode current = _waitThreadsHead;

            do
            {
                prev = current;
                current = current.Next;
            } while (current != null && current.Thread != thread);

            Debug.Assert(current != null, "The wait thread to remove was not found in the list of thread pool wait threads.");

            if (current != null)
            {
                prev.Next = current.Next;
            }
        }

        private class WaitThreadNode
        {
            public WaitThread Thread { get; set; }
            public WaitThreadNode Next { get; set; }
        }

        /// <summary>
        /// A thread pool wait thread.
        /// </summary>
        internal class WaitThread
        {
            /// <summary>
            /// The info for a completed wait on a specific <see cref="RegisteredWait"/>.
            /// </summary>
            private struct CompletedWaitHandle
            {
                public CompletedWaitHandle(RegisteredWait completedHandle, bool timedOut)
                {
                    CompletedHandle = completedHandle;
                    TimedOut = timedOut;
                }

                public RegisteredWait CompletedHandle { get; }
                public bool TimedOut { get; }
            }

            /// <summary>
            /// The wait handles registered on this wait thread.
            /// </summary>
            private readonly RegisteredWait[] _registeredWaits = new RegisteredWait[WaitHandle.MaxWaitHandles - 1];
            /// <summary>
            /// The raw wait handles to wait on.
            /// </summary>
            /// <remarks>
            /// The zeroth element of this array is always <see cref="_changeHandlesEvent"/>.
            /// </remarks>
            private readonly WaitHandle[] _waitHandles = new WaitHandle[WaitHandle.MaxWaitHandles];
            /// <summary>
            /// The number of user-registered waits on this wait thread.
            /// </summary>
            private int _numUserWaits = 0;
            /// <summary>
            /// A lock for editing any handle registration (i.e. the fields above).
            /// </summary>
            private readonly LowLevelLock _registeredHandlesLock = new LowLevelLock();

            /// <summary>
            /// A list of removals of wait handles that are waiting for the wait thread to process.
            /// </summary>
            private readonly RegisteredWait[] _pendingRemoves = new RegisteredWait[WaitHandle.MaxWaitHandles - 1];
            /// <summary>
            /// The number of pending removals.
            /// </summary>
            private int _numPendingRemoves = 0;
            /// <summary>
            /// A lock for modifying the pending removals.
            /// </summary>
            private readonly LowLevelLock _removesLock = new LowLevelLock();

            /// <summary>
            /// An event to notify the wait thread that there are pending adds or removals of wait handles so it needs to wake up.
            /// </summary>
            private readonly AutoResetEvent _changeHandlesEvent = new AutoResetEvent(false);

            internal bool AnyUserWaits => _numUserWaits != 0;

            /// <summary>
            /// The main routine for the wait thread.
            /// </summary>
            private void WaitThreadStart()
            {
                while (true)
                {
                    ProcessRemovals();
                    int numUserWaits = _numUserWaits;
                    int preWaitTicks = Environment.TickCount;

                    // Recalculate Timeout
                    int timeout = ThreadPoolThreadTimeoutMs;
                    for (int i = 0; i < numUserWaits; i++)
                    {
                        int handleRemaining = _registeredWaits[i].TimeoutTime - preWaitTicks;

                        timeout = Math.Min(handleRemaining > 0 ? handleRemaining : 0, timeout);
                    }

                    int signaledHandleIndex = WaitHandle.WaitAny(_waitHandles, numUserWaits + 1, timeout);
                    RegisteredWait signaledHandle = signaledHandleIndex != WaitHandle.WaitTimeout ? _registeredWaits[signaledHandleIndex] : null;

                    if (signaledHandle != null)
                    {
                        QueueWaitCompletion(signaledHandle, false);
                    }
                    else
                    {
                        if(!AnyUserWaits)
                        {
                            if (ThreadPoolInstance.TryRemoveWaitThread(this))
                            {
                                return;
                            }
                        }

                        int elapsedTicks = Environment.TickCount - preWaitTicks; // Calculate using relative time to ensure we don't have issues with overflow wraparound
                        for (int i = 0; i < numUserWaits; i++)
                        {
                            RegisteredWait registeredHandle = _registeredWaits[i];
                            int timeoutRemainingTicks = registeredHandle.TimeoutTime - preWaitTicks;
                            if (elapsedTicks > timeoutRemainingTicks)
                            {
                                QueueWaitCompletion(registeredHandle, true);
                            }
                        }
                    }
                }
            }

            /// <summary>
            /// Go through the <see cref="_pendingRemoves"/> array and remove those registered wait handles from the <see cref="_registeredWaits"/>
            /// and <see cref="_waitHandles"/> arrays. Then move elements around in those arrays to fill holes.
            /// </summary>
            private void ProcessRemovals()
            {
                _removesLock.Acquire();
                _registeredHandlesLock.Acquire();
                try
                {
                    if (_numPendingRemoves == 0)
                    {
                        return;
                    }

                    // This is O(N^2), but max(N) = 63 and N will usually be very low
                    for (int i = 0; i < _numPendingRemoves; i++)
                    {
                        for (int j = 0; j < _numUserWaits; j++)
                        {
                            if (_pendingRemoves[i] == _registeredWaits[j])
                            {
                                _registeredWaits[j] = _registeredWaits[_numUserWaits - 1];
                                _waitHandles[j + 1] = _waitHandles[_numUserWaits];
                                _registeredWaits[_numUserWaits - 1] = null;
                                _waitHandles[_numUserWaits] = null;
                                for (int k = _numUserWaits - 1; k >= 0; k--)
                                {
                                    if (_registeredWaits[k - 1] != null)
                                    {
                                        _numUserWaits = k;
                                        break;
                                    }
                                }
                                break;
                            }
                        }
                        _pendingRemoves[i] = null;
                    }
                    _numPendingRemoves = 0;
                }
                finally
                {
                    _registeredHandlesLock.Release();
                    _removesLock.Release();
                }
            }
            
            /// <summary>
            /// Queue a call to <see cref="CompleteWait(object)"/> on the ThreadPool.
            /// </summary>
            /// <param name="registeredHandle">The handle that completed.</param>
            /// <param name="timedOut">Whether or not the wait timed out.</param>
            private void QueueWaitCompletion(RegisteredWait registeredHandle, bool timedOut)
            {
                ThreadPool.QueueUserWorkItem(CompleteWait, new CompletedWaitHandle(registeredHandle, timedOut));
            }

            /// <summary>
            /// Process the completion of a user-registered wait.
            /// </summary>
            /// <param name="state">A <see cref="CompletedWaitHandle"/> object representing the wait completion.</param>
            private void CompleteWait(object state)
            {
                CompletedWaitHandle handle = (CompletedWaitHandle)state;
                handle.CompletedHandle.CanUnregister.Reset();
                handle.CompletedHandle.PerformCallback(handle.TimedOut);
                handle.CompletedHandle.CanUnregister.Set();
                if (!handle.CompletedHandle.Repeating)
                {
                    QueueOrExecuteUnregisterWait(handle.CompletedHandle);
                }
            }

            /// <summary>
            /// Register a wait handle on this <see cref="WaitThread"/>.
            /// </summary>
            /// <param name="handle">The handle to register.</param>
            /// <returns>If the handle was successfully registered on this wait thread.</returns>
            public bool RegisterWaitHandle(RegisteredWait handle)
            {
                _registeredHandlesLock.Acquire();
                try
                {
                    if (_numUserWaits == WaitHandle.MaxWaitHandles - 1)
                    {
                        return false;
                    }

                    _registeredWaits[_numUserWaits] = handle;
                    _waitHandles[_numUserWaits + 1] = handle.Handle;
                    _numUserWaits++;

                    handle.WaitThread = this;
                }
                finally
                {
                    _registeredHandlesLock.Release();
                }

                _changeHandlesEvent.Set();
                return true;
            }

            /// <summary>
            /// Starts the wait thread.
            /// </summary>
            public void Start()
            {
                _waitHandles[0] = _changeHandlesEvent;
                RuntimeThread waitThread = RuntimeThread.Create(WaitThreadStart);
                waitThread.IsBackground = true;
                waitThread.Start();
            }

            /// <summary>
            /// Queues on the thread pool or executes directly a call to <see cref="UnregisterWait(object)"/>.
            /// </summary>
            /// <param name="handle">The handle to unregister.</param>
            /// <remarks>
            /// As per CoreCLR's behavior, if the user passes in a <see cref="WaitHandle"/> that is equal to <c>-1</c>
            /// into <see cref="RegisteredWait.Unregister(WaitHandle)"/>, then the unregistration of the wait handle is blocking.
            /// Otherwise, the unregistration of the wait handle is queued on the thread pool.
            /// </remarks>
            public void QueueOrExecuteUnregisterWait(RegisteredWait handle)
            {
                if (handle.UserUnregisterWaitHandle?.SafeWaitHandle.DangerousGetHandle() == (IntPtr)(-1))
                {
                    UnregisterWait(handle);
                }
                else
                {
                    ThreadPool.QueueUserWorkItem(UnregisterWait, handle);
                }
            }

            /// <summary>
            /// Unregister a wait handle.
            /// </summary>
            /// <param name="state">The wait handle to unregister.</param>
            private void UnregisterWait(object state)
            {
                RegisteredWait handle = (RegisteredWait)state;

                // TODO: Optimization: Try to unregister wait directly if it isn't being waited on.
                _removesLock.Acquire();
                try
                {
                    if (Array.IndexOf(_pendingRemoves, handle) == -1) // If this handle is not already pending removal
                    {
                        _pendingRemoves[_numPendingRemoves++] = handle;
                        _changeHandlesEvent.Set(); // Tell the wait thread that there are changes pending.
                    }
                }
                finally
                {
                    _removesLock.Release();
                }

                if (handle.UserUnregisterWaitHandle != null)
                {
                    handle.CanUnregister.WaitOne();

                    if (handle.UserUnregisterWaitHandle.SafeWaitHandle.DangerousGetHandle() != (IntPtr)(-1))
                    {
                        handle.SignalUserWaitHandle();
                    }
                }
            }
        }
    }
}
