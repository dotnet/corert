// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.Contracts;

using Internal.Runtime.Augments;

namespace System.Threading
{
    public delegate void TimerCallback(Object state);

    //
    // TimerQueue maintains a list of active timers in this AppDomain.  We use a single native timer to schedule 
    // all managed timers in the process.
    //
    // Perf assumptions:  We assume that timers are created and destroyed frequently, but rarely actually fire.
    // There are roughly two types of timer:
    //
    //  - timeouts for operations.  These are created and destroyed very frequently, but almost never fire, because
    //    the whole point is that the timer only fires if something has gone wrong.
    //
    //  - scheduled background tasks.  These typically do fire, but they usually have quite long durations.
    //    So the impact of spending a few extra cycles to fire these is negligible.
    //
    // Because of this, we want to choose a data structure with very fast insert and delete times, but we can live
    // with linear traversal times when firing timers.
    //
    // The data structure we've chosen is an unordered doubly-linked list of active timers.  This gives O(1) insertion
    // and removal, and O(N) traversal when finding expired timers.
    //
    // Note that all instance methods of this class require that the caller hold a lock on TimerQueue.Instance.
    //
    internal partial class TimerQueue
    {
        #region singleton pattern implementation

        // The one-and-only TimerQueue for the AppDomain.
        static TimerQueue s_queue = new TimerQueue();

        public static TimerQueue Instance
        {
            get { return s_queue; }
        }

        private TimerQueue()
        {
            // empty private constructor to ensure we remain a singleton.
        }

        #endregion

        #region interface to native per-AppDomain timer

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

        int m_currentNativeTimerStartTicks;
        uint m_currentNativeTimerDuration = UInt32.MaxValue;

        private void EnsureAppDomainTimerFiresBy(uint requestedDuration)
        {
            //
            // The CLR VM's timer implementation does not work well for very long-duration timers.
            // See kb 950807.
            // So we'll limit our native timer duration to a "small" value.
            // This may cause us to attempt to fire timers early, but that's ok - 
            // we'll just see that none of our timers has actually reached its due time,
            // and schedule the native timer again.
            //
            const uint maxPossibleDuration = 0x0fffffff;
            uint actualDuration = Math.Min(requestedDuration, maxPossibleDuration);

            if (m_currentNativeTimerDuration != UInt32.MaxValue)
            {
                uint elapsed = (uint)(TickCount - m_currentNativeTimerStartTicks);
                if (elapsed >= m_currentNativeTimerDuration)
                    return; //the timer's about to fire

                uint remainingDuration = m_currentNativeTimerDuration - elapsed;
                if (actualDuration >= remainingDuration)
                    return; //the timer will fire earlier than this request
            }

            SetTimer(actualDuration);
            m_currentNativeTimerDuration = actualDuration;

            m_currentNativeTimerStartTicks = TickCount;
        }

        #endregion

        #region Firing timers

        //
        // The list of timers
        //
        TimerQueueTimer m_timers;
        readonly internal Lock Lock = new Lock();

        //
        // Fire any timers that have expired, and update the native timer to schedule the rest of them.
        //
        private void FireNextTimers()
        {
            //
            // we fire the first timer on this thread; any other timers that might have fired are queued
            // to the ThreadPool.
            //
            TimerQueueTimer timerToFireOnThisThread = null;

            using (LockHolder.Hold(Lock))
            {
                //
                // since we got here, that means our previous timer has fired.
                //
                ReleaseTimer();
                m_currentNativeTimerDuration = UInt32.MaxValue;

                bool haveTimerToSchedule = false;
                uint nextAppDomainTimerDuration = uint.MaxValue;

                int nowTicks = TickCount;

                //
                // Sweep through all timers.  The ones that have reached their due time
                // will fire.  We will calculate the next native timer due time from the
                // other timers.
                //
                TimerQueueTimer timer = m_timers;
                while (timer != null)
                {
                    Debug.Assert(timer.m_dueTime != Timer.UnsignedInfiniteTimeout);

                    uint elapsed = (uint)(nowTicks - timer.m_startTicks);
                    if (elapsed >= timer.m_dueTime)
                    {
                        //
                        // Remember the next timer in case we delete this one
                        //
                        TimerQueueTimer nextTimer = timer.m_next;

                        if (timer.m_period != Timer.UnsignedInfiniteTimeout)
                        {
                            timer.m_startTicks = nowTicks;
                            timer.m_dueTime = timer.m_period;

                            //
                            // This is a repeating timer; schedule it to run again.
                            //
                            if (timer.m_dueTime < nextAppDomainTimerDuration)
                            {
                                haveTimerToSchedule = true;
                                nextAppDomainTimerDuration = timer.m_dueTime;
                            }
                        }
                        else
                        {
                            //
                            // Not repeating; remove it from the queue
                            //
                            DeleteTimer(timer);
                        }

                        //
                        // If this is the first timer, we'll fire it on this thread.  Otherwise, queue it
                        // to the ThreadPool.
                        //
                        if (timerToFireOnThisThread == null)
                            timerToFireOnThisThread = timer;
                        else
                            QueueTimerCompletion(timer);

                        timer = nextTimer;
                    }
                    else
                    {
                        //
                        // This timer hasn't fired yet.  Just update the next time the native timer fires.
                        //
                        uint remaining = timer.m_dueTime - elapsed;
                        if (remaining < nextAppDomainTimerDuration)
                        {
                            haveTimerToSchedule = true;
                            nextAppDomainTimerDuration = remaining;
                        }
                        timer = timer.m_next;
                    }
                }

                if (haveTimerToSchedule)
                    EnsureAppDomainTimerFiresBy(nextAppDomainTimerDuration);
            }

            //
            // Fire the user timer outside of the lock!
            //
            if (timerToFireOnThisThread != null)
                timerToFireOnThisThread.Fire();
        }

        private static void QueueTimerCompletion(TimerQueueTimer timer)
        {
            WaitCallback callback = s_fireQueuedTimerCompletion;
            if (callback == null)
                s_fireQueuedTimerCompletion = callback = new WaitCallback(FireQueuedTimerCompletion);

            // Can use "unsafe" variant because we take care of capturing and restoring
            // the ExecutionContext.
            ThreadPool.UnsafeQueueUserWorkItem(callback, timer);
        }

        private static WaitCallback s_fireQueuedTimerCompletion;

        private static void FireQueuedTimerCompletion(object state)
        {
            ((TimerQueueTimer)state).Fire();
        }

        #endregion

        #region Queue implementation

        public bool UpdateTimer(TimerQueueTimer timer, uint dueTime, uint period)
        {
            if (timer.m_dueTime == Timer.UnsignedInfiniteTimeout)
            {
                // the timer is not in the list; add it (as the head of the list).
                timer.m_next = m_timers;
                timer.m_prev = null;
                if (timer.m_next != null)
                    timer.m_next.m_prev = timer;
                m_timers = timer;
            }
            timer.m_dueTime = dueTime;
            timer.m_period = (period == 0) ? Timer.UnsignedInfiniteTimeout : period;
            timer.m_startTicks = TickCount;
            EnsureAppDomainTimerFiresBy(dueTime);
            return true;
        }

        public void DeleteTimer(TimerQueueTimer timer)
        {
            if (timer.m_dueTime != Timer.UnsignedInfiniteTimeout)
            {
                if (timer.m_next != null)
                    timer.m_next.m_prev = timer.m_prev;
                if (timer.m_prev != null)
                    timer.m_prev.m_next = timer.m_next;
                if (m_timers == timer)
                    m_timers = timer.m_next;

                timer.m_dueTime = Timer.UnsignedInfiniteTimeout;
                timer.m_period = Timer.UnsignedInfiniteTimeout;
                timer.m_startTicks = 0;
                timer.m_prev = null;
                timer.m_next = null;
            }
        }
        #endregion
    }

    //
    // A timer in our TimerQueue.
    //
    sealed class TimerQueueTimer
    {
        //
        // All fields of this class are protected by a lock on TimerQueue.Instance.
        //
        // The first four fields are maintained by TimerQueue itself.
        //
        internal TimerQueueTimer m_next;
        internal TimerQueueTimer m_prev;

        //
        // The time, according to TimerQueue.TickCount, when this timer's current interval started.
        //
        internal int m_startTicks;

        //
        // Timer.UnsignedInfiniteTimeout if we are not going to fire.  Otherwise, the offset from m_startTime when we will fire.
        //
        internal uint m_dueTime;

        //
        // Timer.UnsignedInfiniteTimeout if we are a single-shot timer.  Otherwise, the repeat interval.
        //
        internal uint m_period;

        //
        // Info about the user's callback
        //
        readonly TimerCallback m_timerCallback;
        readonly Object m_state;
        readonly ExecutionContext m_executionContext;


        //
        // When Timer.Dispose(WaitHandle) is used, we need to signal the wait handle only
        // after all pending callbacks are complete.  We set m_canceled to prevent any callbacks that
        // are already queued from running.  We track the number of callbacks currently executing in 
        // m_callbacksRunning.  We set m_notifyWhenNoCallbacksRunning only when m_callbacksRunning
        // reaches zero.
        //
        //int m_callbacksRunning;
        volatile bool m_canceled;
        //volatile WaitHandle m_notifyWhenNoCallbacksRunning;


        internal TimerQueueTimer(TimerCallback timerCallback, object state, uint dueTime, uint period)
        {
            m_timerCallback = timerCallback;
            m_state = state;
            m_dueTime = Timer.UnsignedInfiniteTimeout;
            m_period = Timer.UnsignedInfiniteTimeout;

            m_executionContext = ExecutionContext.Capture();

            //
            // After the following statement, the timer may fire.  No more manipulation of timer state outside of
            // the lock is permitted beyond this point!
            //
            if (dueTime != Timer.UnsignedInfiniteTimeout)
                Change(dueTime, period);
        }


        internal bool Change(uint dueTime, uint period)
        {
            bool success;

            using (LockHolder.Hold(TimerQueue.Instance.Lock))
            {
                if (m_canceled)
                    throw new ObjectDisposedException(null, SR.ObjectDisposed_Generic);

                m_period = period;

                if (dueTime == Timer.UnsignedInfiniteTimeout)
                {
                    TimerQueue.Instance.DeleteTimer(this);
                    success = true;
                }
                else
                {
                    success = TimerQueue.Instance.UpdateTimer(this, dueTime, period);
                }
            }

            return success;
        }


        public void Close()
        {
            using (LockHolder.Hold(TimerQueue.Instance.Lock))
            {
                if (!m_canceled)
                {
                    m_canceled = true;
                    TimerQueue.Instance.DeleteTimer(this);
                }
            }
        }

        internal void Fire()
        {
            if (m_canceled)
                return;

            CallCallback();
        }

        internal void CallCallback()
        {
            ContextCallback callback = s_callCallbackInContext;
            if (callback == null)
                s_callCallbackInContext = callback = new ContextCallback(CallCallbackInContext);

            ExecutionContext.Run(m_executionContext, callback, this);
        }

        private static ContextCallback s_callCallbackInContext;

        private static void CallCallbackInContext(object state)
        {
            TimerQueueTimer t = (TimerQueueTimer)state;
            t.m_timerCallback(t.m_state);
        }
    }

    //
    // TimerHolder serves as an intermediary between Timer and TimerQueueTimer, releasing the TimerQueueTimer 
    // if the Timer is collected.
    // This is necessary because Timer itself cannot use its finalizer for this purpose.  If it did,
    // then users could control timer lifetimes using GC.SuppressFinalize/ReRegisterForFinalize.
    // You might ask, wouldn't that be a good thing?  Maybe (though it would be even better to offer this
    // via first-class APIs), but Timer has never offered this, and adding it now would be a breaking
    // change, because any code that happened to be suppressing finalization of Timer objects would now
    // unwittingly be changing the lifetime of those timers.
    //
    sealed class TimerHolder
    {
        internal TimerQueueTimer m_timer;

        public TimerHolder(TimerQueueTimer timer)
        {
            m_timer = timer;
        }

        ~TimerHolder()
        {
            m_timer.Close();
        }

        public void Close()
        {
            m_timer.Close();
            GC.SuppressFinalize(this);
        }
    }


    public sealed class Timer : IDisposable
    {
        private const UInt32 MAX_SUPPORTED_TIMEOUT = (uint)0xfffffffe;
        internal const uint UnsignedInfiniteTimeout = unchecked((uint)-1);

        private TimerHolder m_timer;

        public Timer(TimerCallback callback,
                     Object state,
                     int dueTime,
                     int period)
        {
            if (dueTime < -1)
                throw new ArgumentOutOfRangeException("dueTime", SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            if (period < -1)
                throw new ArgumentOutOfRangeException("period", SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            Contract.EndContractBlock();

            TimerSetup(callback, state, (UInt32)dueTime, (UInt32)period);
        }

        public Timer(TimerCallback callback,
                     Object state,
                     TimeSpan dueTime,
                     TimeSpan period)
        {
            long dueTm = (long)dueTime.TotalMilliseconds;
            if (dueTm < -1)
                throw new ArgumentOutOfRangeException("dueTm", SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            if (dueTm > MAX_SUPPORTED_TIMEOUT)
                throw new ArgumentOutOfRangeException("dueTm", SR.ArgumentOutOfRange_TimeoutTooLarge);

            long periodTm = (long)period.TotalMilliseconds;
            if (periodTm < -1)
                throw new ArgumentOutOfRangeException("periodTm", SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            if (periodTm > MAX_SUPPORTED_TIMEOUT)
                throw new ArgumentOutOfRangeException("periodTm", SR.ArgumentOutOfRange_PeriodTooLarge);

            TimerSetup(callback, state, (UInt32)dueTm, (UInt32)periodTm);
        }


        private void TimerSetup(TimerCallback callback,
                                Object state,
                                UInt32 dueTime,
                                UInt32 period)
        {
            if (callback == null)
                throw new ArgumentNullException("TimerCallback");
            Contract.EndContractBlock();

            m_timer = new TimerHolder(new TimerQueueTimer(callback, state, dueTime, period));
        }

        public bool Change(int dueTime, int period)
        {
            if (dueTime < -1)
                throw new ArgumentOutOfRangeException("dueTime", SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            if (period < -1)
                throw new ArgumentOutOfRangeException("period", SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            Contract.EndContractBlock();

            return m_timer.m_timer.Change((UInt32)dueTime, (UInt32)period);
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            return Change((long)dueTime.TotalMilliseconds, (long)period.TotalMilliseconds);
        }

        private bool Change(long dueTime, long period)
        {
            if (dueTime < -1)
                throw new ArgumentOutOfRangeException("dueTime", SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            if (period < -1)
                throw new ArgumentOutOfRangeException("period", SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            if (dueTime > MAX_SUPPORTED_TIMEOUT)
                throw new ArgumentOutOfRangeException("dueTime", SR.ArgumentOutOfRange_TimeoutTooLarge);
            if (period > MAX_SUPPORTED_TIMEOUT)
                throw new ArgumentOutOfRangeException("period", SR.ArgumentOutOfRange_PeriodTooLarge);
            Contract.EndContractBlock();

            return m_timer.m_timer.Change((UInt32)dueTime, (UInt32)period);
        }

        public void Dispose()
        {
            m_timer.Close();
        }

        internal void KeepRootedWhileScheduled()
        {
            GC.SuppressFinalize(m_timer);
        }
    }
}
