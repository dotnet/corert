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
    }
}
