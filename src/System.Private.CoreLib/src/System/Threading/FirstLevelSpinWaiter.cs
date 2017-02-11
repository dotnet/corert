// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Internal.Runtime.Augments;

namespace System.Threading
{
    /// <summary>
    /// A lightweight spin-waiter intended to be used as the first-level wait for a condition before the user forces the thread
    /// into a wait state, and where the condition to be checked in each iteration is relatively cheap, like just an interlocked
    /// operation.
    /// 
    /// Used by the wait subsystem on Unix, so this class cannot have any dependencies on the wait subsystem.
    /// </summary>
    internal struct FirstLevelSpinWaiter
    {
        // TODO: Tune these values
        private const int SpinCount = 8;
        private const int SpinYieldThreshold = 4;
        private const int SpinSleep0Threshold = 6;

        private static int s_processorCount;

        private int _spinningThreadCount;

        public void Initialize()
        {
            if (s_processorCount == 0)
            {
                s_processorCount = Environment.ProcessorCount;
            }
        }

        public bool SpinWaitForCondition(Func<bool> condition)
        {
            Debug.Assert(condition != null);
            Debug.Assert(s_processorCount > 0);

            int processorCount = s_processorCount;
            int spinningThreadCount = Interlocked.Increment(ref _spinningThreadCount);
            try
            {
                // Limit the maximum spinning thread count to the processor count to prevent unnecessary context switching
                // caused by an excessive number of threads spin waiting, perhaps even slowing down the thread holding the
                // resource being waited upon
                if (spinningThreadCount <= processorCount)
                {
                    // For uniprocessor systems, start at the yield threshold since the pause instructions used for waiting
                    // prior to that threshold would not help other threads make progress
                    for (int spinIndex = processorCount > 1 ? 0 : SpinYieldThreshold; spinIndex < SpinCount; ++spinIndex)
                    {
                        // The caller should check the condition in a fast path before calling this method, so wait first
                        Wait(spinIndex);

                        if (condition())
                        {
                            return true;
                        }
                    }
                }
            }
            finally
            {
                Interlocked.Decrement(ref _spinningThreadCount);
            }

            return false;
        }

        private static void Wait(int spinIndex)
        {
            Debug.Assert(SpinYieldThreshold < SpinSleep0Threshold);

            if (spinIndex < SpinYieldThreshold)
            {
                RuntimeThread.SpinWait(1 << spinIndex);
                return;
            }

            if (spinIndex < SpinSleep0Threshold && RuntimeThread.Yield())
            {
                return;
            }

            /// <see cref="RuntimeThread.Sleep(int)"/> is interruptible. The current operation may not allow thread interrupt
            /// (for instance, <see cref="LowLevelLock.Acquire"/> as part of <see cref="EventWaitHandle.Set"/>). Use the
            /// uninterruptible version of Sleep(0).
            RuntimeThread.UninterruptibleSleep0();

            // Don't want to Sleep(1) in this spin wait:
            //   - Don't want to spin for that long, since a proper wait will follow when the spin wait fails
            //   - Sleep(1) would put the thread into a wait state, and a proper wait will follow when the spin wait fails
            //     anyway (the intended use for this class), so it's preferable to put the thread into the proper wait state
        }
    }
}
