// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Runtime.Augments;

namespace System.Threading
{
    internal static partial class ClrThreadPool
    {
        /// <summary>
        /// A linked list of <see cref="WaitThread"/>s.
        /// </summary>
        private static WaitThreadNode s_waitThreadsHead = new WaitThreadNode
        {
            Thread = new WaitThread()
        };

        /// <summary>
        /// Register a wait handle on a <see cref="WaitThread"/>.
        /// </summary>
        /// <param name="handle">A description of the requested registration.</param>
        internal static void RegisterWaitHandle(RegisteredWaitHandle handle)
        {
            // Register the wait handle on the first wait thread that is not at capacity.
            WaitThreadNode prev;
            WaitThreadNode current = s_waitThreadsHead;
            do
            {
                if(current.Thread.RegisterWaitHandle(handle))
                {
                    return;
                }
                prev = current;
                current = current.Next;
            } while (current != null);

            // If all wait threads are full, create a new one.
            prev.Next = new WaitThreadNode
            {
                Thread = new WaitThread()
            };
            prev.Next.Thread.RegisterWaitHandle(handle);
        }

        private class WaitThreadNode
        {
            public WaitThread Thread { get; set; }
            public WaitThreadNode Next { get; set; }
        }

        /// <summary>
        /// A thread pool wait thread.
        /// </summary>
        public class WaitThread
        {
            /// <summary>
            /// The info for a completed wait on a specific <see cref="RegisteredWaitHandle"/>.
            /// </summary>
            private struct CompletedWaitHandle
            {
                public CompletedWaitHandle(RegisteredWaitHandle completedHandle, bool timedOut)
                {
                    CompletedHandle = completedHandle;
                    TimedOut = timedOut;
                }

                public RegisteredWaitHandle CompletedHandle { get; }
                public bool TimedOut { get; }
            }

            /// <summary>
            /// The wait handles registered on this wait thread.
            /// </summary>
            private readonly RegisteredWaitHandle[] _registeredWaitHandles = new RegisteredWaitHandle[WaitHandle.MaxWaitHandles - 1];
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
            /// The current calculated timeout of this wait thread.
            /// </summary>
            private int _currentTimeout = Timeout.Infinite;
            /// <summary>
            /// A lock for editing any handle registration (i.e. the fields above).
            /// </summary>
            private readonly LowLevelLock _registeredHandlesLock = new LowLevelLock();

            /// <summary>
            /// A list of removals of wait handles that are waiting for the wait thread to process.
            /// </summary>
            private readonly RegisteredWaitHandle[] _pendingRemoves = new RegisteredWaitHandle[WaitHandle.MaxWaitHandles - 1];
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

            /// <summary>
            /// An event that is signaled when it is safe to dispose any handles that were queued to be removed previously.
            /// </summary>
            private readonly AutoResetEvent _safeToDisposeHandleEvent = new AutoResetEvent(false);

            /// <summary>
            /// Whether or not the wait thread has started.
            /// </summary>
            private bool _waitThreadStarted = false;
            /// <summary>
            /// A lock for the <see cref="_waitThreadStarted"/>.
            /// </summary>
            private LowLevelLock _waitThreadStartedLock = new LowLevelLock();

            /// <summary>
            /// The main routine for the wait thread.
            /// </summary>
            private void WaitThreadStart()
            {
                while (true)
                {
                    // We are going to be modifying the removals array and waiting on the remaining wait handles, so client code should wait
                    // until we are not directly using the wait handles to allow user code to dispose of the WaitHandle.
                    _safeToDisposeHandleEvent.Reset();
                    ProcessRemovals();
                    int signaledHandleIndex = WaitHandle.WaitAny(_waitHandles, _numUserWaits + 1, _currentTimeout);
                    WaitHandle signaledHandle = signaledHandleIndex != WaitHandle.WaitTimeout ? _waitHandles[signaledHandleIndex] : null;
                    ProcessRemovals();
                    _safeToDisposeHandleEvent.Set();

                    // Indices may have changed when processing removals and the signalled handle may have already been unregistered
                    // so we do a linear search over the active user waits to see if the signaled handle is still registered
                    if (signaledHandleIndex != WaitHandle.WaitTimeout)
                    {
                        for (int i = 0; i < _numUserWaits; i++)
                        {
                            RegisteredWaitHandle registeredHandle = _registeredWaitHandles[i];
                            if (registeredHandle.Handle == signaledHandle)
                            {
                                QueueWaitCompletion(registeredHandle, false);
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < _numUserWaits; i++)
                        {
                            RegisteredWaitHandle registeredHandle = _registeredWaitHandles[i];
                            if (registeredHandle.Timeout == _currentTimeout)
                            {
                                QueueWaitCompletion(registeredHandle, true);
                            }
                        }
                    }
                }
            }

            /// <summary>
            /// Go through the <see cref="_pendingRemoves"/> array and remove those registered wait handles from the <see cref="_registeredWaitHandles"/>
            /// and <see cref="_waitHandles"/> arrays. Then move elements around in those arrays to fill holes.
            /// </summary>
            private void ProcessRemovals()
            {
                _removesLock.Acquire();
                _registeredHandlesLock.Acquire();
                if(_numPendingRemoves == 0)
                {
                    _registeredHandlesLock.Release();
                    _removesLock.Release();
                    return;
                }

                // This is O(N^2), but max(N) = 63 and N will usually be very low
                for (int i = 0; i < _numPendingRemoves; i++)
                {
                    for (int j = 0; j < _numUserWaits; j++)
                    {
                        if (_pendingRemoves[i] == _registeredWaitHandles[j])
                        {
                            _registeredWaitHandles[j] = null;
                            _waitHandles[j + 1] = null;
                            break;
                        }
                    }
                    _pendingRemoves[i] = null;
                }
                _numPendingRemoves = 0;

                // Fill in nulls
                // This is O(1), Goes through each of the 63 possible handles once.
                for (int i = 0; i < _numUserWaits; i++)
                {
                    if (_registeredWaitHandles[i] == null)
                    {
                        for (int j = _numUserWaits - 1; j > i; j--)
                        {
                            if(_registeredWaitHandles[j] != null)
                            {
                                _registeredWaitHandles[i] = _registeredWaitHandles[j];
                                _registeredWaitHandles[j] = null;
                                _waitHandles[i + 1] = _waitHandles[j + 1];
                                _waitHandles[j + 1] = null;
                                _numUserWaits = j;
                                break;
                            }
                        }
                        if (_registeredWaitHandles[i] == null)
                        {
                            _numUserWaits = i - 1;
                            break;
                        }
                    }
                }

                // Recalculate Timeout
                int timeout = Timeout.Infinite;
                for (int i = 0; i < _numUserWaits; i++)
                {
                    if (timeout == Timeout.Infinite)
                    {
                        timeout = _registeredWaitHandles[i].Timeout;
                    }
                    else
                    {
                        timeout = Math.Min(_registeredWaitHandles[i].Timeout, timeout);
                    }
                }
                _currentTimeout = timeout;
                _registeredHandlesLock.Release();
                _removesLock.Release();
            }
            
            /// <summary>
            /// Queue a call to <see cref="CompleteWait(object)"/> on the ThreadPool.
            /// </summary>
            /// <param name="registeredHandle">The handle that completed.</param>
            /// <param name="timedOut">Whether or not the wait timed out.</param>
            private void QueueWaitCompletion(RegisteredWaitHandle registeredHandle, bool timedOut)
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
            public bool RegisterWaitHandle(RegisteredWaitHandle handle)
            {
                StartWaitThreadIfNotStarted();

                if (_numUserWaits == WaitHandle.MaxWaitHandles - 1)
                {
                    return false;
                }

                AddWaitHandleForNextWait(handle);
                handle.WaitThread = this;
                _changeHandlesEvent.Set();
                return true;
            }

            /// <summary>
            /// Adds a wait handle for the next call to <see cref="WaitHandle.WaitAny(WaitHandle[], int, int)"/>.
            /// </summary>
            /// <param name="handle"></param>
            private void AddWaitHandleForNextWait(RegisteredWaitHandle handle)
            {
                _registeredHandlesLock.Acquire();

                _registeredWaitHandles[_numUserWaits] = handle;
                _waitHandles[_numUserWaits + 1] = handle.Handle;
                if (_currentTimeout == Timeout.Infinite)
                {
                    _currentTimeout = handle.Timeout;
                }
                else
                {
                    _currentTimeout = Math.Min(_currentTimeout, handle.Timeout);
                }
                _numUserWaits++;
                _registeredHandlesLock.Release();
            }

            /// <summary>
            /// Starts the wait thread if it has not been started yet.
            /// </summary>
            private void StartWaitThreadIfNotStarted()
            {
                if(_waitThreadStarted)
                {
                    return;
                }

                _waitThreadStartedLock.Acquire();
                if (!_waitThreadStarted)
                {
                    _waitHandles[0] = _changeHandlesEvent;
                    RuntimeThread waitThread = RuntimeThread.Create(WaitThreadStart);
                    waitThread.IsBackground = true;
                    waitThread.Start();
                    _waitThreadStarted = true;
                }
                _waitThreadStartedLock.Release();
            }

            /// <summary>
            /// Queues on the thread pool or executes directly a call to <see cref="UnregisterWait(object)"/>.
            /// </summary>
            /// <param name="handle">The handle to unregister.</param>
            /// <remarks>
            /// As per CoreCLR's behavior, if the user passes in a <see cref="WaitHandle"/> that is equal to <c>-1</c>
            /// into <see cref="RegisteredWaitHandle.Unregister(WaitHandle)"/>, then the unregistration of the wait handle is blocking.
            /// Otherwise, the unregistration of the wait handle is queued on the thread pool.
            /// </remarks>
            public void QueueOrExecuteUnregisterWait(RegisteredWaitHandle handle)
            {
                if (handle.UserUnregisterWaitHandle?.SafeWaitHandle.DangerousGetHandle() == (IntPtr)(-1))
                {
                    UnregisterWait(handle);
                }
                else
                {
                    ThreadPool.QueueUserWorkItem(UnregisterWait, handle);
                }
                // Wait until it is safe for the user to dispose the wait handle they just unregistered before returning.
                _safeToDisposeHandleEvent.WaitOne();
            }

            /// <summary>
            /// Unregister a wait handle.
            /// </summary>
            /// <param name="state">The wait handle to unregister.</param>
            private void UnregisterWait(object state)
            {
                RegisteredWaitHandle handle = (RegisteredWaitHandle)state;
                
                // TODO: Optimization: Try to unregister wait directly if it isn't being waited on.
                _removesLock.Acquire();
                if(Array.IndexOf(_pendingRemoves, handle) == -1) // If this handle is not already pending removal
                {
                    _pendingRemoves[_numPendingRemoves++] = handle;
                }
                _removesLock.Release();

                _changeHandlesEvent.Set(); // Tell the wait thread that there are changes pending.
                if (handle.UserUnregisterWaitHandle != null)
                {
                    WaitHandle.WaitAll(new WaitHandle[] { _safeToDisposeHandleEvent, handle.CanUnregister });

                    if (handle.UserUnregisterWaitHandle.SafeWaitHandle.DangerousGetHandle() != (IntPtr)(-1))
                    {
                        handle.SignalUserWaitHandle();
                    }
                }
            }
        }
    }
}
