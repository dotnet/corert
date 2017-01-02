// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime;
using Microsoft.Win32.SafeHandles;

namespace System.Threading
{
    public sealed partial class RuntimeThread
    {
        [ThreadStatic]
        private static RuntimeThread t_currentThread;

        /// <summary>
        /// Used by <see cref="WaitHandle"/>'s multi-wait functions
        /// </summary>
        private WaitHandleArray<SafeWaitHandle> _waitedSafeWaitHandles;

        private ThreadState _threadState;

        private RuntimeThread()
        {
            _waitedSafeWaitHandles = new WaitHandleArray<SafeWaitHandle>(elementInitializer: null);
            _threadState = ThreadState.Unstarted;

#if PLATFORM_UNIX
            _waitInfo = new WaitSubsystem.ThreadWaitInfo(this);
#else
            _waitedHandles = new WaitHandleArray<IntPtr>(elementInitializer: null);
#endif
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

        public static RuntimeThread Create(ThreadStart start) { throw null; }
        public static RuntimeThread Create(ThreadStart start, int maxStackSize) { throw null; }
        public static RuntimeThread Create(ParameterizedThreadStart start) { throw null; }
        public static RuntimeThread Create(ParameterizedThreadStart start, int maxStackSize) { throw null; }

        public static RuntimeThread CurrentThread
        {
            get
            {
                RuntimeThread currentThread = t_currentThread;
                if (currentThread != null)
                {
                    return currentThread;
                }

                t_currentThread = currentThread = new RuntimeThread();
                Debug.Assert(currentThread._threadState == ThreadState.Unstarted);
                currentThread._threadState = ThreadState.Running | ThreadState.Background;
                return currentThread;
            }
        }

        public bool IsAlive { get { throw null; } }
        public bool IsBackground { get { throw null; } set { throw null; } }
        public bool IsThreadPoolThread { get { throw null; } }
        public int ManagedThreadId { get { throw null; } }
        public string Name { get { throw null; } set { throw null; } }
        public ThreadPriority Priority { get { throw null; } set { throw null; } }

        public ThreadState ThreadState
        {
            get
            {
                Interlocked.MemoryBarrier(); // read the latest value
                return _threadState;
            }
        }

        internal void SetWaitSleepJoinState()
        {
            Debug.Assert(this == CurrentThread);
            Debug.Assert((_threadState & ThreadState.WaitSleepJoin) == 0);

            _threadState |= ThreadState.WaitSleepJoin;
            // A memory barrier is not necessary to make this change visible to other threads immediately, since a system wait
            // call will soon follow
        }

        internal void ClearWaitSleepJoinState()
        {
            Debug.Assert(this == CurrentThread);
            Debug.Assert((_threadState & ThreadState.WaitSleepJoin) != 0);

            _threadState ^= ThreadState.WaitSleepJoin;
            Interlocked.MemoryBarrier(); // make the change visible to other threads immediately
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

        public void Join() { throw null; }
        public bool Join(int millisecondsTimeout) { throw null; }
        public static void Sleep(int millisecondsTimeout) => SleepCore(VerifyTimeoutMilliseconds(millisecondsTimeout));
        public static void SpinWait(int iterations) => RuntimeImports.RhSpinWait(iterations);
        public static bool Yield() => RuntimeImports.RhYield();

        public void Start() { throw null; }
        public void Start(object parameter) { throw null; }
    }
}
