// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Internal.Runtime.Augments;

namespace System.Threading
{
    /// <summary>
    /// A LIFO semaphore.
    /// Waits on this semaphore are uninterruptible.
    /// </summary>
    internal sealed partial class LowLevelLifoSemaphore : IDisposable
    {
        private CacheLineSeparatedCounts _separated;

        private int _maximumSignalCount;
        private int _spinCount;

        private const int SpinSleep0Threshold = 10;

        private static int s_processorCount = Environment.ProcessorCount;

        public LowLevelLifoSemaphore(int initialSignalCount, int maximumSignalCount, int spinCount)
        {
            Debug.Assert(initialSignalCount >= 0);
            Debug.Assert(initialSignalCount <= maximumSignalCount);
            Debug.Assert(maximumSignalCount > 0);

            _separated = new CacheLineSeparatedCounts();
            _separated._counts._signalCount = (uint)initialSignalCount;
            _maximumSignalCount = maximumSignalCount;
            _spinCount = spinCount;

            Create(maximumSignalCount);
        }

        public bool Wait(int timeoutMs)
        {
            // Try to acquire the semaphore or
            // a) register as a spinner if spinCount > 0 and timeoutMs > 0
            // b) register as a waiter if there's already too many spinners or spinCount == 0 and timeoutMs > 0
            // c) bail out if timeoutMs == 0 and return false
            Counts counts = Counts.VolatileRead(ref _separated._counts);
            while (true)
            {
                Debug.Assert(counts._signalCount <= _maximumSignalCount);
                Counts newCounts = counts;

                if (counts._signalCount != 0)
                {
                    newCounts._signalCount--;
                }
                else if (timeoutMs != 0)
                {
                    if (_spinCount > 0 && newCounts._spinnerCount < byte.MaxValue)
                    {
                        newCounts._spinnerCount++;
                    }
                    else
                    {
                        // Maximum number of spinners reached, register as a waiter instead
                        newCounts._waiterCount++;
                        Debug.Assert(newCounts._waiterCount != 0); // overflow check, this many waiters is currently not supported
                    }
                }

                Counts countsBeforeUpdate = Counts.CompareExchange(ref _separated._counts, newCounts, counts);
                if (countsBeforeUpdate == counts)
                {
                    if (counts._signalCount != 0)
                    {
                        return true;
                    }
                    if (newCounts._waiterCount != counts._waiterCount)
                    {
                        return WaitForSignal(timeoutMs);
                    }
                    if (timeoutMs == 0)
                    {
                        return false;
                    }
                    break;
                }

                counts = countsBeforeUpdate;
            }

            int spinIndex = s_processorCount > 1 ? 0 : SpinSleep0Threshold;
            while (spinIndex < _spinCount)
            {
                // TODO: Unify this with LowLevelSpinWaiter
                if (s_processorCount > 1)
                {
                    if (spinIndex < SpinSleep0Threshold || (spinIndex - SpinSleep0Threshold) % 2 != 0)
                    {
                        // Matches algorithm in SpinWait.SpinOnce
                        int n = RuntimeThread.OptimalMaxSpinWaitsPerSpinIteration;
                        if (spinIndex <= 30 && (1 << spinIndex) < n)
                        {
                            n = 1 << spinIndex;
                        }
                        RuntimeThread.SpinWait(n);
                    }
                    else
                    {
                        RuntimeThread.Yield();
                    }

                    spinIndex++;
                }
                else
                {
                    // On uniprocessor system we only Yield because there cannot be other
                    // threads executing in parallel and satisfying the condition. We also
                    // compensate for the unnecessary spins by incrementing spinIndex by 2
                    // and doing the same number of Yields as in the multi-processor code
                    // above.
                    RuntimeThread.Yield();
                    spinIndex += 2;
                }

                counts = Counts.VolatileRead(ref _separated._counts);
                while (counts._signalCount > 0)
                {
                    Counts newCounts = counts;
                    newCounts._signalCount--;
                    newCounts._spinnerCount--;

                    Counts countsBeforeUpdate = Counts.CompareExchange(ref _separated._counts, newCounts, counts);
                    if (countsBeforeUpdate == counts)
                    {
                        return true;
                    }

                    counts = countsBeforeUpdate;
                }
            }

            // Unregister as spinner and acquire the semaphore or register as a waiter
            counts = Counts.VolatileRead(ref _separated._counts);
            while (true)
            {
                Counts newCounts = counts;
                newCounts._spinnerCount--;
                if (counts._signalCount != 0)
                {
                    newCounts._signalCount--;
                }
                else
                {
                    newCounts._waiterCount++;
                    Debug.Assert(newCounts._waiterCount != 0); // overflow check, this many waiters is currently not supported
                }

                Counts countsBeforeUpdate = Counts.CompareExchange(ref _separated._counts, newCounts, counts);
                if (countsBeforeUpdate == counts)
                {
                    return counts._signalCount != 0 || WaitForSignal(timeoutMs);
                }

                counts = countsBeforeUpdate;
            }
        }


        public void Release(int releaseCount)
        {
            Debug.Assert(releaseCount > 0);
            Debug.Assert(releaseCount <= _maximumSignalCount);

            int countOfWaitersToWake;
            Counts counts = Counts.VolatileRead(ref _separated._counts);
            while (true)
            {
                Counts newCounts = counts;

                // Increase the signal count. The addition doesn't overflow because of the limit on the max signal count in constructor.
                newCounts._signalCount += (uint)releaseCount;
                Debug.Assert(newCounts._signalCount > counts._signalCount);
                Debug.Assert(newCounts._signalCount <= _maximumSignalCount);

                // Determine how many waiters to wake, taking into account how many spinners and waiters there are and how many waiters
                // have previously been signaled to wake but have not yet woken
                countOfWaitersToWake =
                    (int)Math.Min(newCounts._signalCount, (uint)newCounts._waiterCount + newCounts._spinnerCount) -
                    newCounts._spinnerCount -
                    newCounts._countOfWaitersSignaledToWake;
                if (countOfWaitersToWake > 0)
                {
                    // Ideally, limiting to a maximum of releaseCount would not be necessary and could be an assert instead, but since
                    // WaitForSignal() does not have enough information to tell whether a woken thread was signaled, and due to the cap
                    // below, it's possible for countOfWaitersSignaledToWake to be less than the number of threads that have actually
                    // been signaled to wake.
                    if (countOfWaitersToWake > releaseCount)
                    {
                        countOfWaitersToWake = releaseCount;
                    }

                    // Cap countOfWaitersSignaledToWake to its max value. It's ok to ignore some woken threads in this count, it just
                    // means some more threads will be woken next time. Typically, it won't reach the max anyway.
                    newCounts._countOfWaitersSignaledToWake += (byte)Math.Min(countOfWaitersToWake, byte.MaxValue);
                    if (newCounts._countOfWaitersSignaledToWake <= counts._countOfWaitersSignaledToWake)
                    {
                        newCounts._countOfWaitersSignaledToWake = byte.MaxValue;
                    }
                }

                Counts countsBeforeUpdate = Counts.CompareExchange(ref _separated._counts, newCounts, counts);
                if (countsBeforeUpdate == counts)
                {
                    Debug.Assert(releaseCount <= _maximumSignalCount - counts._signalCount);
                    if (countOfWaitersToWake > 0)
                        ReleaseCore(countOfWaitersToWake);
                    return;
                }

                counts = countsBeforeUpdate;
            }
        }

        private bool WaitForSignal(int timeoutMs)
        {
            Debug.Assert(timeoutMs > 0);

            while (true)
            {
                if (!WaitCore(timeoutMs))
                {
                    // Unregister the waiter. The wait subsystem used above guarantees that a thread that wakes due to a timeout does
                    // not observe a signal to the object being waited upon.
                    Counts toSubtract = new Counts();
                    toSubtract._waiterCount++;
                    Counts newCounts = Counts.Subtract(ref _separated._counts, toSubtract);
                    Debug.Assert(newCounts._waiterCount >= 0);
                    return false;
                }

                // Unregister the waiter if this thread will not be waiting anymore, and try to acquire the semaphore
                Counts counts = Counts.VolatileRead(ref _separated._counts);
                while (true)
                {
                    Debug.Assert(counts._waiterCount != 0);
                    Counts newCounts = counts;
                    if (counts._signalCount != 0)
                    {
                        --newCounts._signalCount;
                        --newCounts._waiterCount;
                    }

                    // This waiter has woken up and this needs to be reflected in the count of waiters signaled to wake
                    if (counts._countOfWaitersSignaledToWake != 0)
                    {
                        --newCounts._countOfWaitersSignaledToWake;
                    }

                    Counts countsBeforeUpdate = Counts.CompareExchange(ref _separated._counts, newCounts, counts);
                    if (countsBeforeUpdate == counts)
                    {
                        if (counts._signalCount != 0)
                        {
                            return true;
                        }
                        break;
                    }

                    counts = countsBeforeUpdate;
                }
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct Counts
        {
            [FieldOffset(0)]
            public uint _signalCount;
            [FieldOffset(4)]
            public ushort _waiterCount;
            [FieldOffset(6)]
            public byte _spinnerCount;
            [FieldOffset(8)]
            public byte _countOfWaitersSignaledToWake;

            [FieldOffset(0)]
            private long _asLong;

            public static Counts VolatileRead(ref Counts counts)
            {
                return new Counts { _asLong = Volatile.Read(ref counts._asLong) };
            }

            public static Counts CompareExchange(ref Counts location, Counts newCounts, Counts oldCounts)
            {
                return new Counts { _asLong = Interlocked.CompareExchange(ref location._asLong, newCounts._asLong, oldCounts._asLong) };
            }

            public static Counts Subtract(ref Counts location, Counts subtractCounts)
            {
                return new Counts { _asLong = Interlocked.Add(ref location._asLong, -subtractCounts._asLong) };
            }

            public static bool operator ==(Counts lhs, Counts rhs) => lhs._asLong == rhs._asLong;

            public static bool operator !=(Counts lhs, Counts rhs) => lhs._asLong != rhs._asLong;

            public override bool Equals(object obj)
            {
                return obj is Counts counts && this._asLong == counts._asLong;
            }

            public override int GetHashCode()
            {
                return (int)(_asLong >> 8);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CacheLineSeparatedCounts
        {
            private Internal.PaddingFor32 _pad1;
            public Counts _counts;
            private Internal.PaddingFor32 _pad2;
        }
    }
}
