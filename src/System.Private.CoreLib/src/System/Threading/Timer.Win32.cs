// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace System.Threading
{
    //
    // Win32-specific implementation of Timer
    //
    internal partial class TimerQueue
    {
        private IntPtr _nativeTimer;

        private unsafe void SetTimer(uint actualDuration)
        {
            if (_nativeTimer == IntPtr.Zero)
            {
                IntPtr nativeCallback = AddrofIntrinsics.AddrOf<Action<IntPtr, IntPtr, IntPtr>>(TimerCallback);

                _nativeTimer = Interop.mincore.CreateThreadpoolTimer(nativeCallback, IntPtr.Zero, IntPtr.Zero);
                if (_nativeTimer == IntPtr.Zero)
                    throw new OutOfMemoryException();
            }

            // Negative time indicates the amount of time to wait relative to the current time, in 100 nanosecond units
            long dueTime = -10000 * (long)actualDuration;
            Interop.mincore.SetThreadpoolTimer(_nativeTimer, &dueTime, 0, 0);
        }

        private void ReleaseTimer()
        {
        }

        [NativeCallable(CallingConvention = CallingConvention.StdCall)]
        private static void TimerCallback(IntPtr instance, IntPtr context, IntPtr timer)
        {
            Instance.FireNextTimers();
        }

        //
        // We need to keep our notion of time synchronized with the calls to SleepEx that drive
        // the underlying native timer.  In Win8, SleepEx does not count the time the machine spends
        // sleeping/hibernating.  Environment.TickCount (GetTickCount) *does* count that time,
        // so we will get out of sync with SleepEx if we use that method.
        //
        // So, on Win8, we use QueryUnbiasedInterruptTime instead; this does not count time spent
        // in sleep/hibernate mode.
        //
        private static int TickCount
        {
            get
            {
                ulong time100ns;

                bool result = Interop.mincore.QueryUnbiasedInterruptTime(out time100ns);
                Debug.Assert(result);

                // convert to 100ns to milliseconds, and truncate to 32 bits.
                return (int)(uint)(time100ns / 10000);
            }
        }        
    }
}
