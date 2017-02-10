// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Internal.Runtime.Augments;

namespace System.Threading
{
    internal static partial class WaitSubsystem
    {
        /// <summary>
        /// Contains thread-specific information for the wait subsystem. There is one instance per thread that is registered
        /// using <see cref="WaitedListNode"/>s with each <see cref="WaitableObject"/> that the thread is waiting upon.
        /// 
        /// Used by the wait subsystem on Unix, so this class cannot have any dependencies on the wait subsystem.
        /// </summary>
        public sealed class ThreadWaitInfo
        {
            private readonly RuntimeThread _thread;

            /// <summary>
            /// The monitor the thread would wait upon when the wait needs to be interruptible
            /// </summary>
            private readonly LowLevelMonitor _waitMonitor;

            ////////////////////////////////////////////////////////////////
            /// Thread wait state. The following members indicate the waiting state of the thread, and convery information from
            /// a signaler to the waiter. They are synchronized with <see cref="_waitMonitor"/>.

            private WaitSignalState _waitSignalState;

            /// <summary>
            /// Index of the waitable object in <see cref="_waitedObjects"/>, which got signaled and satisfied the wait. -1 if
            /// the wait has not yet been satisfied.
            /// </summary>
            private int _waitedObjectIndexThatSatisfiedWait;

            ////////////////////////////////////////////////////////////////
            /// Information about the current wait, including the type of wait, the <see cref="WaitableObject"/>s involved in
            /// the wait, etc. They are synchronized with <see cref="s_lock"/>.

            private bool _isWaitForAll;

            /// <summary>
            /// Number of <see cref="WaitableObject"/>s the thread is waiting upon
            /// </summary>
            private int _waitedCount;

            /// <summary>
            /// - <see cref="WaitableObject"/>s that are waited upon by the thread. This array is also used for temporarily
            ///   storing <see cref="WaitableObject"/>s corresponding to <see cref="WaitHandle"/>s when the thread is not
            ///   waiting.
            /// - The count of this array is a power of 2, the filled count is <see cref="_waitedCount"/>
            /// - Indexes in all arrays that use <see cref="_waitedCount"/> correspond
            /// </summary>
            private WaitHandleArray<WaitableObject> _waitedObjects;

            /// <summary>
            /// - Nodes used for registering a thread's wait on each <see cref="WaitableObject"/>, in the
            ///   <see cref="WaitableObject.WaitersHead"/> linked list
            /// - The count of this array is a power of 2, the filled count is <see cref="_waitedCount"/>
            /// - Indexes in all arrays that use <see cref="_waitedCount"/> correspond
            /// </summary>
            private WaitHandleArray<WaitedListNode> _waitedListNodes;

            private int _isPendingInterrupt;

            ////////////////////////////////////////////////////////////////

            /// <summary>
            /// Linked list of mutex <see cref="WaitableObject"/>s that are owned by the thread and need to be abandoned before
            /// the thread exits. The linked list has only a head and no tail, which means acquired mutexes are prepended and
            /// mutexes are abandoned in reverse order.
            /// </summary>
            private WaitableObject _lockedMutexesHead;

            public ThreadWaitInfo(RuntimeThread thread)
            {
                Debug.Assert(thread != null);

                _thread = thread;
                _waitMonitor = new LowLevelMonitor();
                _waitedObjectIndexThatSatisfiedWait = -1;
                _waitedObjects = new WaitHandleArray<WaitableObject>(elementInitializer: null);
                _waitedListNodes = new WaitHandleArray<WaitedListNode>(i => new WaitedListNode(this, i));
            }

            public RuntimeThread Thread => _thread;

            /// <summary>
            /// Callers must ensure to clear the array after use. Once <see cref="RegisterWait(int, bool)"/> is called (followed
            /// by a call to <see cref="Wait(int, bool, out int)"/>, the array will be cleared automatically.
            /// </summary>
            public WaitableObject[] GetWaitedObjectArray(int requiredCapacity)
            {
                Debug.Assert(_thread == RuntimeThread.CurrentThread);
                Debug.Assert(_waitedCount == 0);

                _waitedObjects.VerifyElementsAreDefault();
                _waitedObjects.EnsureCapacity(requiredCapacity);
                return _waitedObjects.Items;
            }

            private WaitedListNode[] GetWaitedListNodeArray(int requiredCapacity)
            {
                Debug.Assert(_thread == RuntimeThread.CurrentThread);
                Debug.Assert(_waitedCount == 0);

                _waitedListNodes.EnsureCapacity(requiredCapacity, i => new WaitedListNode(this, i));
                return _waitedListNodes.Items;
            }

            /// <summary>
            /// The caller is expected to populate <see cref="WaitedObjects"/> and pass in the number of objects filled
            /// </summary>
            public void RegisterWait(int waitedCount, bool isWaitForAll)
            {
                s_lock.VerifyIsLocked();
                Debug.Assert(_thread == RuntimeThread.CurrentThread);

                Debug.Assert(waitedCount > (isWaitForAll ? 1 : 0));
                Debug.Assert(waitedCount <= _waitedObjects.Items.Length);

                Debug.Assert(_waitedCount == 0);

                WaitableObject[] waitedObjects = _waitedObjects.Items;
#if DEBUG
                for (int i = 0; i < waitedCount; ++i)
                {
                    Debug.Assert(waitedObjects[i] != null);
                }
                for (int i = waitedCount; i < waitedObjects.Length; ++i)
                {
                    Debug.Assert(waitedObjects[i] == null);
                }
#endif

                bool success = false;
                WaitedListNode[] waitedListNodes;
                try
                {
                    waitedListNodes = GetWaitedListNodeArray(waitedCount);
                    success = true;
                }
                finally
                {
                    if (!success)
                    {
                        // Once this function is called, the caller is effectively transferring ownership of the waited objects
                        // to this and the wait functions. On exception, clear the array.
                        for (int i = 0; i < waitedCount; ++i)
                        {
                            waitedObjects[i] = null;
                        }
                    }
                }

                _isWaitForAll = isWaitForAll;
                _waitedCount = waitedCount;
                for (int i = 0; i < waitedCount; ++i)
                {
                    waitedListNodes[i].RegisterWait(waitedObjects[i]);
                }
            }

            public void UnregisterWait()
            {
                s_lock.VerifyIsLocked();
                Debug.Assert(_waitedCount > (_isWaitForAll ? 1 : 0));

                for (int i = 0; i < _waitedCount; ++i)
                {
                    _waitedListNodes.Items[i].UnregisterWait(_waitedObjects.Items[i]);
                    _waitedObjects.Items[i] = null;
                }
                _waitedCount = 0;
            }

            private int ProcessSignaledWaitState(WaitHandle[] waitHandlesForAbandon, out Exception exception)
            {
                s_lock.VerifyIsNotLocked();
                _waitMonitor.VerifyIsLocked();
                Debug.Assert(_thread == RuntimeThread.CurrentThread);

                switch (_waitSignalState)
                {
                    case WaitSignalState.Waiting:
                        exception = null;
                        return WaitHandle.WaitTimeout;

                    case WaitSignalState.Waiting_SignaledToSatisfyWait:
                        {
                            Debug.Assert(_waitedObjectIndexThatSatisfiedWait >= 0);
                            int waitedObjectIndexThatSatisfiedWait = _waitedObjectIndexThatSatisfiedWait;
                            _waitedObjectIndexThatSatisfiedWait = -1;
                            exception = null;
                            return waitedObjectIndexThatSatisfiedWait;
                        }

                    case WaitSignalState.Waiting_SignaledToSatisfyWaitWithAbandonedMutex:
                        Debug.Assert(_waitedObjectIndexThatSatisfiedWait >= 0);
                        if (waitHandlesForAbandon == null)
                        {
                            _waitedObjectIndexThatSatisfiedWait = -1;
                            exception = new AbandonedMutexException();
                        }
                        else
                        {
                            int waitedObjectIndexThatSatisfiedWait = _waitedObjectIndexThatSatisfiedWait;
                            _waitedObjectIndexThatSatisfiedWait = -1;
                            exception =
                                new AbandonedMutexException(
                                    waitedObjectIndexThatSatisfiedWait,
                                    waitHandlesForAbandon[waitedObjectIndexThatSatisfiedWait]);
                        }
                        return 0;

                    case WaitSignalState.Waiting_SignaledToAbortWaitDueToMaximumMutexReacquireCount:
                        Debug.Assert(_waitedObjectIndexThatSatisfiedWait < 0);
                        exception = new OverflowException(SR.Overflow_MutexReacquireCount);
                        return 0;

                    default:
                        Debug.Assert(_waitSignalState == WaitSignalState.Waiting_SignaledToInterruptWait);
                        Debug.Assert(_waitedObjectIndexThatSatisfiedWait < 0);
                        exception = new ThreadInterruptedException();
                        return 0;
                }
            }

            public int Wait(int timeoutMilliseconds, WaitHandle[] waitHandlesForAbandon, bool isSleep)
            {
                if (!isSleep)
                {
                    s_lock.VerifyIsLocked();
                }
                Debug.Assert(_thread == RuntimeThread.CurrentThread);

                Debug.Assert(timeoutMilliseconds >= -1);
                Debug.Assert(timeoutMilliseconds != 0); // caller should have taken care of it

                _thread.SetWaitSleepJoinState();

                /// <see cref="_waitMonitor"/> must be acquired before <see cref="s_lock"/> is released, to ensure that there is
                /// no gap during which a waited object may be signaled to satisfy the wait but the thread may not yet be in a
                /// wait state to accept the signal
                _waitMonitor.Acquire();
                if (!isSleep)
                {
                    s_lock.Release();
                }

                Debug.Assert(_waitedObjectIndexThatSatisfiedWait < 0);
                Debug.Assert(_waitSignalState == WaitSignalState.NotWaiting);

                /// A signaled state may be set only when the thread is in the
                /// <see cref="WaitSignalState.Waiting"/> state
                _waitSignalState = WaitSignalState.Waiting;

                int waitResult;
                Exception exception;
                try
                {
                    if (timeoutMilliseconds < 0)
                    {
                        do
                        {
                            _waitMonitor.Wait();
                        } while (_waitSignalState == WaitSignalState.Waiting);

                        waitResult = ProcessSignaledWaitState(waitHandlesForAbandon, out exception);
                        Debug.Assert(exception != null || waitResult != WaitHandle.WaitTimeout);
                    }
                    else
                    {
                        int elapsedMilliseconds = 0;
                        int startTimeMilliseconds = Environment.TickCount;
                        while (true)
                        {
                            bool monitorWaitResult = _waitMonitor.Wait(timeoutMilliseconds - elapsedMilliseconds);

                            // It's possible for the wait to have timed out, but before the monitor could reacquire the lock, a
                            // signaler could have acquired it and signaled to satisfy the wait or interrupt the thread. Accept the
                            // signal and ignore the wait timeout.
                            waitResult = ProcessSignaledWaitState(waitHandlesForAbandon, out exception);
                            if (exception != null || waitResult != WaitHandle.WaitTimeout)
                            {
                                break;
                            }

                            if (monitorWaitResult)
                            {
                                elapsedMilliseconds = Environment.TickCount - startTimeMilliseconds;
                                if (elapsedMilliseconds < timeoutMilliseconds)
                                {
                                    continue;
                                }
                            }

                            // Timeout
                            Debug.Assert(_waitedObjectIndexThatSatisfiedWait < 0);
                            break;
                        }
                    }
                }
                finally
                {
                    _waitSignalState = WaitSignalState.NotWaiting;
                    _waitMonitor.Release();

                    _thread.ClearWaitSleepJoinState();
                }

                if (exception != null)
                {
                    throw exception;
                }
                if (waitResult != WaitHandle.WaitTimeout)
                {
                    return waitResult;
                }

                /// Timeout. It's ok to read <see cref="_waitedCount"/> without acquiring <see cref="s_lock"/> here, because it
                /// is initially set by this thread, and another thread cannot unregister this thread's wait without first
                /// signaling this thread, in which case this thread wouldn't be timing out.
                Debug.Assert(isSleep == (_waitedCount == 0));
                if (!isSleep)
                {
                    s_lock.Acquire();
                    try
                    {
                        UnregisterWait();
                    }
                    finally
                    {
                        s_lock.Release();
                    }
                }
                return waitResult;
            }

            public static void UninterruptibleSleep0()
            {
                // On Unix, a thread waits on a condition variable. The timeout time will have already elapsed at the time
                // of the call. The documentation does not state whether the thread yields or does nothing before returning
                // an error, and in some cases, suggests that doing nothing is acceptable. The behavior could also be
                // different between distributions. Yield directly here.
                RuntimeThread.Yield();
            }

            public void Sleep(int timeoutMilliseconds)
            {
                s_lock.VerifyIsNotLocked();
                Debug.Assert(_thread == RuntimeThread.CurrentThread);

                Debug.Assert(timeoutMilliseconds >= -1);

                if (timeoutMilliseconds == 0)
                {
                    UninterruptibleSleep0();
                    return;
                }

                int waitResult = Wait(timeoutMilliseconds, waitHandlesForAbandon: null, isSleep: true);
                Debug.Assert(waitResult == WaitHandle.WaitTimeout);
            }

            public bool TrySignalToSatisfyWait(WaitedListNode registeredListNode, bool isAbandonedMutex)
            {
                s_lock.VerifyIsLocked();
                Debug.Assert(_thread != RuntimeThread.CurrentThread);

                Debug.Assert(registeredListNode != null);
                Debug.Assert(registeredListNode.WaitInfo == this);
                Debug.Assert(registeredListNode.WaitedObjectIndex >= 0);
                Debug.Assert(registeredListNode.WaitedObjectIndex < _waitedCount);

                Debug.Assert(_waitedCount > (_isWaitForAll ? 1 : 0));

                int signaledWaitedObjectIndex = registeredListNode.WaitedObjectIndex;
                bool isWaitForAll = _isWaitForAll;
                int waitedCount = 0;
                WaitableObject[] waitedObjects = null;
                bool wouldAnyMutexReacquireCountOverflow = false;
                if (isWaitForAll)
                {
                    // Determine if all waits would be satisfied
                    waitedCount = _waitedCount;
                    waitedObjects = _waitedObjects.Items;
                    if (!WaitableObject.WouldWaitForAllBeSatisfiedOrAborted(
                            _thread,
                            waitedObjects,
                            waitedCount,
                            signaledWaitedObjectIndex,
                            ref wouldAnyMutexReacquireCountOverflow,
                            ref isAbandonedMutex))
                    {
                        return false;
                    }
                }

                // The wait would be satisfied. Before making changes to satisfy the wait, acquire the monitor and verify that
                // the thread can accept a signal.
                _waitMonitor.Acquire();

                if (_waitSignalState != WaitSignalState.Waiting)
                {
                    _waitMonitor.Release();
                    return false;
                }

                if (isWaitForAll && !wouldAnyMutexReacquireCountOverflow)
                {
                    // All waits would be satisfied, accept the signals
                    WaitableObject.SatisfyWaitForAll(this, waitedObjects, waitedCount, signaledWaitedObjectIndex);
                }

                UnregisterWait();

                Debug.Assert(_waitedObjectIndexThatSatisfiedWait < 0);
                if (wouldAnyMutexReacquireCountOverflow)
                {
                    _waitSignalState = WaitSignalState.Waiting_SignaledToAbortWaitDueToMaximumMutexReacquireCount;
                }
                else
                {
                    _waitedObjectIndexThatSatisfiedWait = signaledWaitedObjectIndex;
                    _waitSignalState =
                        isAbandonedMutex
                            ? WaitSignalState.Waiting_SignaledToSatisfyWaitWithAbandonedMutex
                            : WaitSignalState.Waiting_SignaledToSatisfyWait;
                }

                _waitMonitor.Signal_Release();
                return !wouldAnyMutexReacquireCountOverflow;
            }

            public void TrySignalToInterruptWaitOrRecordPendingInterrupt()
            {
                s_lock.VerifyIsLocked();

                _waitMonitor.Acquire();

                if (_waitSignalState != WaitSignalState.Waiting)
                {
                    _waitMonitor.Release();
                    RecordPendingInterrupt();
                    return;
                }

                if (_waitedCount != 0)
                {
                    UnregisterWait();
                }

                Debug.Assert(_waitedObjectIndexThatSatisfiedWait < 0);
                _waitSignalState = WaitSignalState.Waiting_SignaledToInterruptWait;

                _waitMonitor.Signal_Release();
            }

            private void RecordPendingInterrupt() => Interlocked.Exchange(ref _isPendingInterrupt, 1);
            public bool CheckAndResetPendingInterrupt => Interlocked.CompareExchange(ref _isPendingInterrupt, 0, 1) != 0;

            public WaitableObject LockedMutexesHead
            {
                get
                {
                    s_lock.VerifyIsLocked();
                    return _lockedMutexesHead;
                }
                set
                {
                    s_lock.VerifyIsLocked();
                    _lockedMutexesHead = value;
                }
            }

            public void OnThreadExiting()
            {
                // Abandon locked mutexes. Acquired mutexes are prepended to the linked list, so the mutexes are abandoned in
                // last-acquired-first-abandoned order.
                s_lock.Acquire();
                try
                {
                    while (true)
                    {
                        WaitableObject waitableObject = LockedMutexesHead;
                        if (waitableObject == null)
                        {
                            break;
                        }

                        waitableObject.AbandonMutex();
                        Debug.Assert(LockedMutexesHead != waitableObject);
                    }
                }
                finally
                {
                    s_lock.Release();
                }
            }

            public sealed class WaitedListNode
            {
                /// <summary>
                /// For <see cref="WaitedListNode"/>s registered with <see cref="WaitableObject"/>s, this provides information
                /// about the thread that is waiting and the <see cref="WaitableObject"/>s it is waiting upon
                /// </summary>
                private readonly ThreadWaitInfo _waitInfo;

                /// <summary>
                /// Index of the waited object corresponding to this node
                /// </summary>
                private readonly int _waitedObjectIndex;

                /// <summary>
                /// Link in the <see cref="WaitableObject.WaitersHead"/> linked list
                /// </summary>
                private WaitedListNode _previous, _next;

                public WaitedListNode(ThreadWaitInfo waitInfo, int waitedObjectIndex)
                {
                    Debug.Assert(waitInfo != null);
                    Debug.Assert(waitedObjectIndex >= 0);
                    Debug.Assert(waitedObjectIndex < WaitHandle.MaxWaitHandles);

                    _waitInfo = waitInfo;
                    _waitedObjectIndex = waitedObjectIndex;
                }

                public ThreadWaitInfo WaitInfo
                {
                    get
                    {
                        s_lock.VerifyIsLocked();
                        return _waitInfo;
                    }
                }

                public int WaitedObjectIndex
                {
                    get
                    {
                        s_lock.VerifyIsLocked();
                        return _waitedObjectIndex;
                    }
                }

                public WaitedListNode Previous
                {
                    get
                    {
                        s_lock.VerifyIsLocked();
                        return _previous;
                    }
                }

                public WaitedListNode Next
                {
                    get
                    {
                        s_lock.VerifyIsLocked();
                        return _next;
                    }
                }

                public void RegisterWait(WaitableObject waitableObject)
                {
                    s_lock.VerifyIsLocked();
                    Debug.Assert(_waitInfo.Thread == RuntimeThread.CurrentThread);

                    Debug.Assert(waitableObject != null);

                    Debug.Assert(_previous == null);
                    Debug.Assert(_next == null);

                    WaitedListNode tail = waitableObject.WaitersTail;
                    if (tail != null)
                    {
                        _previous = tail;
                        tail._next = this;
                    }
                    else
                    {
                        waitableObject.WaitersHead = this;
                    }
                    waitableObject.WaitersTail = this;
                }

                public void UnregisterWait(WaitableObject waitableObject)
                {
                    s_lock.VerifyIsLocked();
                    Debug.Assert(waitableObject != null);

                    WaitedListNode previous = _previous;
                    WaitedListNode next = _next;

                    if (previous != null)
                    {
                        previous._next = next;
                        _previous = null;
                    }
                    else
                    {
                        Debug.Assert(waitableObject.WaitersHead == this);
                        waitableObject.WaitersHead = next;
                    }

                    if (next != null)
                    {
                        next._previous = previous;
                        _next = null;
                    }
                    else
                    {
                        Debug.Assert(waitableObject.WaitersTail == this);
                        waitableObject.WaitersTail = previous;
                    }
                }
            }

            private enum WaitSignalState : byte
            {
                NotWaiting,
                Waiting,
                Waiting_SignaledToSatisfyWait,
                Waiting_SignaledToSatisfyWaitWithAbandonedMutex,
                Waiting_SignaledToAbortWaitDueToMaximumMutexReacquireCount,
                Waiting_SignaledToInterruptWait
            }
        }
    }
}
