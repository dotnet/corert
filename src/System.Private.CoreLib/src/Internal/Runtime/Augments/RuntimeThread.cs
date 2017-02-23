// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace Internal.Runtime.Augments
{
    public sealed partial class RuntimeThread
    {
        // Extra bits used in _threadState
        private const ThreadState ThreadPoolThread = (ThreadState)0x1000;
        private const ThreadState PublicThreadStateMask = (ThreadState)0x1FF;

        [ThreadStatic]
        private static RuntimeThread t_currentThread;

        private int _threadState;
        private ThreadPriority _priority;
        private ManagedThreadId _managedThreadId;
        private string _name;
        private Lock _lock;

        private Delegate _threadStart;
        private object _threadStartArg;
        private int _maxStackSize;

        /// <summary>
        /// Used by <see cref="WaitHandle"/>'s multi-wait functions
        /// </summary>
        private WaitHandleArray<SafeWaitHandle> _waitedSafeWaitHandles;

        private RuntimeThread()
        {
            _waitedSafeWaitHandles = new WaitHandleArray<SafeWaitHandle>(elementInitializer: null);
            _threadState = (int)ThreadState.Unstarted;
            _priority = ThreadPriority.Normal;
            _lock = new Lock();

            PlatformSpecificInitialize();
        }

        private RuntimeThread(Delegate threadStart, int maxStackSize)
            : this()
        {
            _threadStart = threadStart;
            _maxStackSize = maxStackSize;
            _managedThreadId = new ManagedThreadId();
        }

        /// <summary>
        /// Callers must ensure to clear the array after use
        /// </summary>
        internal SafeWaitHandle[] GetWaitedSafeWaitHandleArray(int requiredCapacity)
        {
            Debug.Assert(this == CurrentThread);

            _waitedSafeWaitHandles.VerifyElementsAreDefault();
            _waitedSafeWaitHandles.EnsureCapacity(requiredCapacity);
            return _waitedSafeWaitHandles.Items;
        }

        public static RuntimeThread Create(ThreadStart start) => new RuntimeThread(start, 0);
        public static RuntimeThread Create(ThreadStart start, int maxStackSize) => new RuntimeThread(start, maxStackSize);

        public static RuntimeThread Create(ParameterizedThreadStart start) => new RuntimeThread(start, 0);
        public static RuntimeThread Create(ParameterizedThreadStart start, int maxStackSize) => new RuntimeThread(start, maxStackSize);

        public static RuntimeThread CurrentThread
        {
            get
            {
                RuntimeThread currentThread = t_currentThread;
                return t_currentThread ?? InitializeExistingThread();
            }
        }

        // Slow path executed once per thread
        private static RuntimeThread InitializeExistingThread()
        {
            var currentThread = new RuntimeThread();
            currentThread._managedThreadId = System.Threading.ManagedThreadId.GetCurrentThreadId();
            Debug.Assert(currentThread._threadState == (int)ThreadState.Unstarted);
            // The main thread is foreground, other ones are background
            if (currentThread._managedThreadId.Id == System.Threading.ManagedThreadId.IdMainThread)
            {
                currentThread._threadState = (int)(ThreadState.Running);
            }
            else
            {
                currentThread._threadState = (int)(ThreadState.Running | ThreadState.Background);
            }
            currentThread.PlatformSpecificInitializeExistingThread();
            t_currentThread = currentThread;
            return currentThread;
        }

        public static void InitializeThreadPoolThread()
        {
            if (t_currentThread == null)
            {
                InitializeExistingThread().SetThreadStateBit(ThreadPoolThread);
            }
        }

        public bool IsAlive
        {
            get
            {
                // Refresh ThreadState.Stopped bit if necessary
                ThreadState state = GetThreadState();
                return (state & (ThreadState.Unstarted | ThreadState.Stopped | ThreadState.Aborted)) == 0;
            }
        }

        public bool IsDead
        {
            get
            {
                // Refresh ThreadState.Stopped bit if necessary
                ThreadState state = GetThreadState();
                return (state & (ThreadState.Stopped | ThreadState.Aborted)) != 0;
            }
        }

        public bool IsBackground
        {
            get
            {
                if (IsDead)
                {
                    throw new ThreadStateException(SR.ThreadState_Dead_State);
                }
                return GetThreadStateBit(ThreadState.Background);
            }
            set
            {
                if (IsDead)
                {
                    throw new ThreadStateException(SR.ThreadState_Dead_State);
                }
                // TODO: Keep a counter of running foregroung threads so we can wait on process exit
                if (value)
                {
                    SetThreadStateBit(ThreadState.Background);
                }
                else
                {
                    ClearThreadStateBit(ThreadState.Background);
                }
            }
        }

        public bool IsThreadPoolThread
        {
            get
            {
                if (IsDead)
                {
                    throw new ThreadStateException(SR.ThreadState_Dead_State);
                }
                return GetThreadStateBit(ThreadPoolThread);
            }
        }

        public int ManagedThreadId => _managedThreadId.Id;

        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                using (LockHolder.Hold(_lock))
                {
                    if (_name != null)
                    {
                        throw new InvalidOperationException(SR.InvalidOperation_WriteOnce);
                    }
                    _name = value;
                    // TODO: Inform the debugger and the profiler
                }
            }
        }

        public ThreadPriority Priority
        {
            get
            {
                if (IsDead)
                {
                    throw new ThreadStateException(SR.ThreadState_Dead_Priority);
                }
                return _priority;
            }
            set
            {
                if ((value < ThreadPriority.Lowest) || (ThreadPriority.Highest < value))
                {
                    throw new ArgumentOutOfRangeException(SR.Argument_InvalidFlag);
                }
                if (IsDead)
                {
                    throw new ThreadStateException(SR.ThreadState_Dead_Priority);
                }

                // Possible race condition with starting this thread
                using (LockHolder.Hold(_lock))
                {
                    if (!SetPriority(value))
                    {
                        throw new ThreadStateException(SR.ThreadState_SetPriorityFailed);
                    }
                    _priority = value;
                }
            }
        }

        public ThreadState ThreadState => GetThreadState();

        private ThreadState GetThreadState()
        {
            int state = Volatile.Read(ref _threadState);
            // If the thread is marked as alive, check if it has finished execution
            if ((state & (int)(ThreadState.Unstarted | ThreadState.Stopped | ThreadState.Aborted)) == 0)
            {
                if (HasFinishedExecution())
                {
                    state = Volatile.Read(ref _threadState);
                    if ((state & (int)(ThreadState.Stopped | ThreadState.Aborted)) == 0)
                    {
                        SetThreadStateBit(ThreadState.Stopped);
                        state = _threadState;
                    }
                }
            }
            return (ThreadState)state & PublicThreadStateMask;
        }

        private bool GetThreadStateBit(ThreadState bit)
        {
            Debug.Assert((bit & ThreadState.Stopped) == 0, "ThreadState.Stopped bit may be stale; use GetThreadState instead.");
            return (Volatile.Read(ref _threadState) & (int)bit) != 0;
        }

        private void SetThreadStateBit(ThreadState bit)
        {
            int oldState, newState;
            do
            {
                oldState = _threadState;
                newState = oldState | (int)bit;
            } while (Interlocked.CompareExchange(ref _threadState, newState, oldState) != oldState);
        }

        private void ClearThreadStateBit(ThreadState bit)
        {
            int oldState, newState;
            do
            {
                oldState = _threadState;
                newState = oldState & ~(int)bit;
            } while (Interlocked.CompareExchange(ref _threadState, newState, oldState) != oldState);
        }

        internal void SetWaitSleepJoinState()
        {
            Debug.Assert(this == CurrentThread);
            Debug.Assert(!GetThreadStateBit(ThreadState.WaitSleepJoin));

            SetThreadStateBit(ThreadState.WaitSleepJoin);
        }

        internal void ClearWaitSleepJoinState()
        {
            Debug.Assert(this == CurrentThread);
            Debug.Assert(GetThreadStateBit(ThreadState.WaitSleepJoin));

            ClearThreadStateBit(ThreadState.WaitSleepJoin);
        }

        private static int VerifyTimeoutMilliseconds(int millisecondsTimeout)
        {
            if (millisecondsTimeout < -1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(millisecondsTimeout),
                    millisecondsTimeout,
                    SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            }
            return millisecondsTimeout;
        }

        public void Join() => Join(Timeout.Infinite);

        public bool Join(int millisecondsTimeout)
        {
            VerifyTimeoutMilliseconds(millisecondsTimeout);
            if (GetThreadStateBit(ThreadState.Unstarted))
            {
                throw new ThreadStateException(SR.ThreadState_NotStarted);
            }
            if (millisecondsTimeout == 0)
            {
                return HasFinishedExecution();
            }
            return JoinCore(millisecondsTimeout);
        }

        public static void Sleep(int millisecondsTimeout) => SleepCore(VerifyTimeoutMilliseconds(millisecondsTimeout));
        public static void SpinWait(int iterations) => RuntimeImports.RhSpinWait(iterations);
        public static bool Yield() => RuntimeImports.RhYield();

        public void Start() => StartCore(null);

        public void Start(object parameter)
        {
            if (_threadStart is ThreadStart)
            {
                throw new InvalidOperationException(SR.InvalidOperation_ThreadWrongThreadStart);
            }
            StartCore(parameter);
        }
    }
}
