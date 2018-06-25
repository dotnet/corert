// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Runtime.Augments;
using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace System.Threading
{
    //
    // Unix-specific implementation of Timer
    //
    internal partial class TimerQueue
    {
        /// <summary>
        /// This event is used by the timer thread to wait for timer expiration. It is also
        /// used to notify the timer thread that a new timer has been set.
        /// </summary>
        private static AutoResetEvent s_timerEvent;

        /// <summary>
        /// This field stores the value of next timer that the timer thread should install.
        /// </summary>
        private static volatile int s_nextTimerDuration;

        private void SetTimer(uint actualDuration)
        {
            // This function is called with the TimerQueue lock acquired
            Debug.Assert(Lock.IsAcquired);

            // Note: AutoResetEvent.WaitOne takes an Int32 value as a timeout.
            // The TimerQueue code ensures that timer duration is not greater than max Int32 value
            Debug.Assert(actualDuration <= (uint)int.MaxValue);
            s_nextTimerDuration = (int)actualDuration;

            // If this is the first time the timer is set then we need to create a thread that
            // will manage and respond to timer requests. Otherwise, simply signal the timer thread
            // to notify it that the timer duration has changed.
            if (s_timerEvent == null)
            {
                s_timerEvent = new AutoResetEvent(false);
                RuntimeThread thread = RuntimeThread.Create(TimerThread, 0);
                thread.IsBackground = true; // Keep this thread from blocking process shutdown
                thread.Start();
            }
            else
            {
                s_timerEvent.Set();
            }
        }


        /// <summary>
        /// This method is executed on a dedicated a timer thread. Its purpose is
        /// to handle timer request and notify the TimerQueue when a timer expires.
        /// </summary>
        private static void TimerThread()
        {
            int currentTimerInterval;

            // Get wait time for the next timer
            currentTimerInterval = Interlocked.Exchange(ref s_nextTimerDuration, Timeout.Infinite);

            for (;;)
            {
                // Wait for the current timer to expire.
                // We will be woken up because either 1) the wait times out, which will indicate that
                // the current timer has expired and/or 2) the TimerQueue installs a new (earlier) timer.
                int startWait = TickCount;
                bool timerHasExpired = !s_timerEvent.WaitOne(currentTimerInterval);
                uint elapsedTime = (uint)(TickCount - startWait);

                // The timer event can be set after this thread reads the new timer interval but before it enters
                // the wait state. This can cause a spurious wake up. In addition, expiration of current timer can
                // happen almost at the same time as this thread is signaled to install a new timer. To handle
                // these cases, we need to update the current interval based on the elapsed time.
                if (currentTimerInterval != Timeout.Infinite)
                {
                    if (elapsedTime >= currentTimerInterval)
                    {
                        timerHasExpired = true;
                    }
                    else
                    {
                        currentTimerInterval -= (int)elapsedTime;
                    }
                }

                // Check whether TimerQueue needs to process expired timers.
                if (timerHasExpired)
                {
                    Instance.FireNextTimers();

                    // When FireNextTimers() installs a new timer, it also sets the timer event.
                    // Reset the event so the timer thread is not woken up right away unnecessary.
                    s_timerEvent.Reset();
                    currentTimerInterval = Timeout.Infinite;
                }

                int nextTimerInterval = Interlocked.Exchange(ref s_nextTimerDuration, Timeout.Infinite);
                if (nextTimerInterval != Timeout.Infinite)
                {
                    currentTimerInterval = nextTimerInterval;
                }
            }
        }

        private static int TickCount
        {
            get
            {
                return Environment.TickCount;
            }
        }
    }

    internal sealed partial class TimerQueueTimer
    {
        private void SignalNoCallbacksRunning()
        {
            SafeWaitHandle waitHandle = _notifyWhenNoCallbacksRunning.SafeWaitHandle;

            waitHandle.DangerousAddRef();
            try
            {
                WaitSubsystem.SetEvent(waitHandle.DangerousGetHandle());
            }
            finally
            {
                waitHandle.DangerousRelease();
            }
        }
    }
}
