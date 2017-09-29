// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
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
    [EagerStaticClassConstruction] // the spin waiter is used during lazy class construction on Unix
    internal static class FirstLevelSpinWaiter
    {
        // TODO: Tune these values
        private const int SpinCount = 8;
        private const int SpinYieldThreshold = 4;
        private const int SpinSleep0Threshold = 6;

        private static readonly int ProcessorCount = Environment.ProcessorCount;

        private static int s_spinningThreadCount;

        public static bool SpinWaitForCondition<T>(Func<T, bool> condition, T obj)
        {
            Debug.Assert(condition != null);

            Debug.Assert(SpinYieldThreshold > 0);
            Debug.Assert(SpinYieldThreshold < SpinSleep0Threshold);
            Debug.Assert(SpinSleep0Threshold < SpinCount);

            Debug.Assert(ProcessorCount > 0);

            int processorCount = ProcessorCount;
            int spinningThreadCount = Interlocked.Increment(ref s_spinningThreadCount);
            try
            {
                // Limit the maximum spinning thread count to the processor count to prevent unnecessary context switching
                // caused by an excessive number of threads spin waiting, perhaps even slowing down the thread holding the
                // resource being waited upon
                if (spinningThreadCount > processorCount)
                {
                    return false;
                }

                // For uniprocessor systems, start at the yield threshold since the pause instructions used for waiting
                // prior to that threshold would not help other threads make progress
                for (int spinIndex = processorCount > 1 ? 0 : SpinYieldThreshold; spinIndex < SpinCount; ++spinIndex)
                {
                    // The caller should check the condition in a fast path before calling this method, so wait first
                    Wait(spinIndex);

                    if (condition(obj))
                    {
                        return true;
                    }
                }
                return false;
            }
            finally
            {
                Interlocked.Decrement(ref s_spinningThreadCount);
            }
        }

        private static void Wait(int spinIndex)
        {
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
