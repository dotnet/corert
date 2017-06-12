// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Runtime.Augments;

namespace System.Threading
{
    internal static partial class ClrThreadPool
    {
        private static WaitThreadNode s_waitThreadsHead = new WaitThreadNode
        {
            Thread = new WaitThread()
        };

        internal static void RegisterWaitHandle(RegisteredWaitHandle handle)
        {
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

        public class WaitThread
        {
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

            private readonly RegisteredWaitHandle[] _registeredWaitHandles = new RegisteredWaitHandle[WaitHandle.MaxWaitHandles - 1];
            private readonly WaitHandle[] _waitHandles = new WaitHandle[WaitHandle.MaxWaitHandles];
            private int _numUserWaits = 0;
            private int _currentTimeout = Timeout.Infinite;
            private readonly LowLevelLock _registeredHandlesLock = new LowLevelLock();

            private readonly RegisteredWaitHandle[] _pendingRemoves = new RegisteredWaitHandle[WaitHandle.MaxWaitHandles - 1];
            private int _numPendingRemoves = 0;
            private readonly LowLevelLock _removesLock = new LowLevelLock();

            private readonly AutoResetEvent _changeHandlesEvent = new AutoResetEvent(false);

            private readonly AutoResetEvent _safeToDisposeHandleEvent = new AutoResetEvent(false);

            private bool _waitThreadStarted = false;
            private LowLevelLock _waitThreadStartedLock = new LowLevelLock();

            private void WaitThreadStart()
            {
                while (true)
                {
                    _safeToDisposeHandleEvent.Reset();
                    ProcessRemovals();
                    int signaledHandleIndex = WaitHandle.WaitAny(_waitHandles, _numUserWaits + 1, _currentTimeout);
                    WaitHandle signaledHandle = _waitHandles[signaledHandleIndex];
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
                                ExecuteWaitCompletion(registeredHandle, false);
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
                                ExecuteWaitCompletion(registeredHandle, true);
                            }
                        }
                    }
                }
            }

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

            private void ExecuteWaitCompletion(RegisteredWaitHandle registeredHandle, bool timedOut)
            {
                ThreadPool.QueueUserWorkItem(CompleteWait, new CompletedWaitHandle(registeredHandle, timedOut));
            }

            private void CompleteWait(object state)
            {
                CompletedWaitHandle handle = (CompletedWaitHandle)state;
                handle.CompletedHandle.CanUnregister.Reset();
                handle.CompletedHandle.PerformCallback(handle.TimedOut);
                handle.CompletedHandle.CanUnregister.Set();
                if (!handle.CompletedHandle.Repeating)
                {
                    QueueUnregisterWait(handle.CompletedHandle);
                }
            }

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

            private void AddWaitHandleForNextWait(RegisteredWaitHandle handle)
            {
                _registeredHandlesLock.Acquire();

                _registeredWaitHandles[_numUserWaits] = handle;
                _waitHandles[_numUserWaits + 1] = handle.Handle;
                _numUserWaits++;
                if (_currentTimeout == Timeout.Infinite)
                {
                    _currentTimeout = handle.Timeout;
                }
                else
                {
                    _currentTimeout = Math.Min(_currentTimeout, handle.Timeout);
                }

                _registeredHandlesLock.Release();
            }

            private void StartWaitThreadIfNotStarted()
            {
                _waitThreadStartedLock.Acquire();
                if (!_waitThreadStarted)
                {
                    _waitHandles[0] = _changeHandlesEvent;
                    RuntimeThread waitThread = RuntimeThread.Create(WaitThreadStart);
                    waitThread.IsBackground = true;
                    waitThread.Start();
                }
                _waitThreadStartedLock.Release();
            }

            public void QueueUnregisterWait(RegisteredWaitHandle handle)
            {
                if (handle.Handle?.SafeWaitHandle.DangerousGetHandle() == (IntPtr)(-1))
                {
                    UnregisterWait(handle);
                }
                else
                {
                    ThreadPool.QueueUserWorkItem(UnregisterWait, handle);
                }
                _safeToDisposeHandleEvent.WaitOne();
            }

            private void UnregisterWait(object state)
            {
                RegisteredWaitHandle handle = (RegisteredWaitHandle)state;
                
                // TODO: Optimization: Try to unregister wait directly if it isn't being waited on.
                _removesLock.Acquire();
                _pendingRemoves[_numPendingRemoves++] = handle;
                _removesLock.Release();
                _changeHandlesEvent.Set();
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
