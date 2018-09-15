// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Win32.SafeHandles;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Internal.Runtime.Augments
{
    public sealed partial class RuntimeThread
    {
        // Extra bits used in _threadState
        private const ThreadState ThreadPoolThread = (ThreadState)0x1000;

        // Bits of _threadState that are returned by the ThreadState property
        private const ThreadState PublicThreadStateMask = (ThreadState)0x1FF;

        [ThreadStatic]
        private static RuntimeThread t_currentThread;

        private ExecutionContext _executionContext;
        private SynchronizationContext _synchronizationContext;

        private volatile int _threadState;
        private ThreadPriority _priority;
        private ManagedThreadId _managedThreadId;
        private string _name;
        private Delegate _threadStart;
        private object _threadStartArg;
        private int _maxStackSize;

        // Protects starting the thread and setting its priority
        private Lock _lock;

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

#if PLATFORM_UNIX
            _waitInfo = new WaitSubsystem.ThreadWaitInfo(this);
#endif

            PlatformSpecificInitialize();
        }

        // Constructor for threads created by the Thread class
        private RuntimeThread(Delegate threadStart, int maxStackSize)
            : this()
        {
            _threadStart = threadStart;
            _maxStackSize = maxStackSize;
            _managedThreadId = new ManagedThreadId();
        }

        public static RuntimeThread Create(ThreadStart start) => new RuntimeThread(start, 0);
        public static RuntimeThread Create(ThreadStart start, int maxStackSize) => new RuntimeThread(start, maxStackSize);

        public static RuntimeThread Create(ParameterizedThreadStart start) => new RuntimeThread(start, 0);
        public static RuntimeThread Create(ParameterizedThreadStart start, int maxStackSize) => new RuntimeThread(start, maxStackSize);

        public static RuntimeThread CurrentThread
        {
            get
            {
                return t_currentThread ?? InitializeExistingThread(false);
            }
        }

        internal static ulong CurrentOSThreadId
        {
            get
            {
                return RuntimeImports.RhCurrentOSThreadId();
            }
        }

        // Slow path executed once per thread
        private static RuntimeThread InitializeExistingThread(bool threadPoolThread)
        {
            Debug.Assert(t_currentThread == null);

            var currentThread = new RuntimeThread();
            currentThread._managedThreadId = System.Threading.ManagedThreadId.GetCurrentThreadId();
            Debug.Assert(currentThread._threadState == (int)ThreadState.Unstarted);

            ThreadState state = threadPoolThread ? ThreadPoolThread : 0;

            // The main thread is foreground, other ones are background
            if (currentThread._managedThreadId.Id != System.Threading.ManagedThreadId.IdMainThread)
            {
                state |= ThreadState.Background;
            }

            currentThread._threadState = (int)(state | ThreadState.Running);
            currentThread.PlatformSpecificInitializeExistingThread();
            currentThread._priority = currentThread.GetPriorityLive();
            t_currentThread = currentThread;

            if (threadPoolThread)
            {
                InitializeCom();
            }

            return currentThread;
        }

        // Use ThreadPoolCallbackWrapper instead of calling this function directly
        internal static RuntimeThread InitializeThreadPoolThread()
        {
            return t_currentThread ?? InitializeExistingThread(true);
        }

        /// <summary>
        /// Resets properties of the current thread pool thread that may have been changed by a user callback.
        /// </summary>
        internal void ResetThreadPoolThread()
        {
            Debug.Assert(this == RuntimeThread.CurrentThread);

            CultureInfo.ResetThreadCulture();

            if (_name != null)
            {
                _name = null;
            }

            if (!GetThreadStateBit(ThreadState.Background))
            {
                SetThreadStateBit(ThreadState.Background);
            }

            ThreadPriority newPriority = ThreadPriority.Normal;
            if ((_priority != newPriority) && SetPriorityLive(newPriority))
            {
                _priority = newPriority;
            }
        }

        /// <summary>
        /// Returns true if the underlying OS thread has been created and started execution of managed code.
        /// </summary>
        private bool HasStarted()
        {
            return !GetThreadStateBit(ThreadState.Unstarted);
        }

        internal ExecutionContext ExecutionContext
        {
            get { return _executionContext; }
            set { _executionContext = value; }
        }

        internal SynchronizationContext SynchronizationContext
        {
            get { return _synchronizationContext; }
            set { _synchronizationContext = value; }
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

        private bool IsDead()
        {
            // Refresh ThreadState.Stopped bit if necessary
            ThreadState state = GetThreadState();
            return (state & (ThreadState.Stopped | ThreadState.Aborted)) != 0;
        }

        public bool IsBackground
        {
            get
            {
                if (IsDead())
                {
                    throw new ThreadStateException(SR.ThreadState_Dead_State);
                }
                return GetThreadStateBit(ThreadState.Background);
            }
            set
            {
                if (IsDead())
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
                if (IsDead())
                {
                    throw new ThreadStateException(SR.ThreadState_Dead_State);
                }
                return GetThreadStateBit(ThreadPoolThread);
            }
            internal set
            {
                if (IsDead())
                {
                    throw new ThreadStateException(SR.ThreadState_Dead_State);
                }
                if (value)
                {
                    SetThreadStateBit(ThreadPoolThread);
                }
                else
                {
                    ClearThreadStateBit(ThreadPoolThread);
                }
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
                if (Interlocked.CompareExchange(ref _name, value, null) != null)
                {
                    throw new InvalidOperationException(SR.InvalidOperation_WriteOnce);
                }
                // TODO: Inform the debugger and the profiler
            }
        }

        public ThreadPriority Priority
        {
            get
            {
                if (IsDead())
                {
                    throw new ThreadStateException(SR.ThreadState_Dead_Priority);
                }
                if (!HasStarted())
                {
                    // The thread has not been started yet; return the value assigned to the Priority property.
                    // Race condition with setting the priority or starting the thread is OK, we may return an old value.
                    return _priority;
                }
                // The priority might have been changed by external means. Obtain the actual value from the OS
                // rather than using the value saved in _priority.
                return GetPriorityLive();
            }
            set
            {
                if ((value < ThreadPriority.Lowest) || (ThreadPriority.Highest < value))
                {
                    throw new ArgumentOutOfRangeException(SR.Argument_InvalidFlag);
                }
                if (IsDead())
                {
                    throw new ThreadStateException(SR.ThreadState_Dead_Priority);
                }

                // Prevent race condition with starting this thread
                using (LockHolder.Hold(_lock))
                {
                    if (HasStarted() && !SetPriorityLive(value))
                    {
                        throw new ThreadStateException(SR.ThreadState_SetPriorityFailed);
                    }
                    _priority = value;
                }
            }
        }

        public ThreadState ThreadState => (GetThreadState() & PublicThreadStateMask);

        private bool GetThreadStateBit(ThreadState bit)
        {
            Debug.Assert((bit & ThreadState.Stopped) == 0, "ThreadState.Stopped bit may be stale; use GetThreadState instead.");
            return (_threadState & (int)bit) != 0;
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
            return JoinInternal(millisecondsTimeout);
        }

        [MethodImpl(MethodImplOptions.NoInlining)] // Slow path method. Make sure that the caller frame does not pay for PInvoke overhead.
        public static void Sleep(int millisecondsTimeout) => SleepInternal(VerifyTimeoutMilliseconds(millisecondsTimeout));

        /// <summary>
        /// Max value to be passed into <see cref="SpinWait(int)"/> for optimal delaying. Currently, the value comes from
        /// defaults in CoreCLR's Thread::InitializeYieldProcessorNormalized(). This value is supposed to be normalized to be
        /// appropriate for the processor.
        /// TODO: See issue https://github.com/dotnet/corert/issues/4430
        /// </summary>
        internal static readonly int OptimalMaxSpinWaitsPerSpinIteration = 64;

        public static void SpinWait(int iterations) => RuntimeImports.RhSpinWait(iterations);

        [MethodImpl(MethodImplOptions.NoInlining)] // Slow path method. Make sure that the caller frame does not pay for PInvoke overhead.
        public static bool Yield() => RuntimeImports.RhYield();

        public void Start() => StartInternal(null);

        public void Start(object parameter)
        {
            if (_threadStart is ThreadStart)
            {
                throw new InvalidOperationException(SR.InvalidOperation_ThreadWrongThreadStart);
            }
            StartInternal(parameter);
        }

        private void StartInternal(object parameter)
        {
            using (LockHolder.Hold(_lock))
            {
                if (!GetThreadStateBit(ThreadState.Unstarted))
                {
                    throw new ThreadStateException(SR.ThreadState_AlreadyStarted);
                }

                bool waitingForThreadStart = false;
                GCHandle threadHandle = GCHandle.Alloc(this);
                _threadStartArg = parameter;

                try
                {
                    if (!CreateThread(threadHandle))
                    {
                        throw new OutOfMemoryException();
                    }

                    // Skip cleanup if any asynchronous exception happens while waiting for the thread start
                    waitingForThreadStart = true;

                    // Wait until the new thread either dies or reports itself as started
                    while (GetThreadStateBit(ThreadState.Unstarted) && !JoinInternal(0))
                    {
                        Yield();
                    }

                    waitingForThreadStart = false;
                }
                finally
                {
                    Debug.Assert(!waitingForThreadStart, "Leaked threadHandle");
                    if (!waitingForThreadStart)
                    {
                        threadHandle.Free();
                        _threadStartArg = null;
                    }
                }

                if (GetThreadStateBit(ThreadState.Unstarted))
                {
                    // Lack of memory is the only expected reason for thread creation failure
                    throw new ThreadStartException(new OutOfMemoryException());
                }
            }
        }

        private static void StartThread(IntPtr parameter)
        {
            GCHandle threadHandle = (GCHandle)parameter;
            RuntimeThread thread = (RuntimeThread)threadHandle.Target;
            Delegate threadStart = thread._threadStart;
            // Get the value before clearing the ThreadState.Unstarted bit
            object threadStartArg = thread._threadStartArg;

            try
            {
                t_currentThread = thread;
                System.Threading.ManagedThreadId.SetForCurrentThread(thread._managedThreadId);
                thread.InitializeComOnNewThread();
            }
            catch (OutOfMemoryException)
            {
#if PLATFORM_UNIX
                // This should go away once OnThreadExit stops using t_currentThread to signal
                // shutdown of the thread on Unix.
                thread._stopped.Set();
#endif
                // Terminate the current thread. The creator thread will throw a ThreadStartException.
                return;
            }

            // Report success to the creator thread, which will free threadHandle and _threadStartArg
            thread.ClearThreadStateBit(ThreadState.Unstarted);

            try
            {
                // The Thread cannot be started more than once, so we may clean up the delegate
                thread._threadStart = null;

                ParameterizedThreadStart paramThreadStart = threadStart as ParameterizedThreadStart;
                if (paramThreadStart != null)
                {
                    paramThreadStart(threadStartArg);
                }
                else
                {
                    ((ThreadStart)threadStart)();
                }
            }
            finally
            {
                thread.SetThreadStateBit(ThreadState.Stopped);
            }
        }

        // The upper bits of t_currentProcessorIdCache are the currentProcessorId. The lower bits of
        // the t_currentProcessorIdCache are counting down to get it periodically refreshed.
        // TODO: Consider flushing the currentProcessorIdCache on Wait operations or similar 
        // actions that are likely to result in changing the executing core
        [ThreadStatic]
        private static int t_currentProcessorIdCache;

        private const int ProcessorIdCacheShift = 16;
        private const int ProcessorIdCacheCountDownMask = (1 << ProcessorIdCacheShift) - 1;
        private const int ProcessorIdRefreshRate = 5000;

        private static int RefreshCurrentProcessorId()
        {
            int currentProcessorId = ComputeCurrentProcessorId();

            // Add offset to make it clear that it is not guaranteed to be 0-based processor number
            currentProcessorId += 100;

            Debug.Assert(ProcessorIdRefreshRate <= ProcessorIdCacheCountDownMask);

            // Mask with int.MaxValue to ensure the execution Id is not negative
            t_currentProcessorIdCache = ((currentProcessorId << ProcessorIdCacheShift) & int.MaxValue) + ProcessorIdRefreshRate;

            return currentProcessorId;
        }

        // Cached processor id used as a hint for which per-core stack to access. It is periodically
        // refreshed to trail the actual thread core affinity.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetCurrentProcessorId()
        {
            int currentProcessorIdCache = t_currentProcessorIdCache--;
            if ((currentProcessorIdCache & ProcessorIdCacheCountDownMask) == 0)
                return RefreshCurrentProcessorId();
            return (currentProcessorIdCache >> ProcessorIdCacheShift);
        }
    }
}
