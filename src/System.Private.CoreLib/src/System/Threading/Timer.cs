// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

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
        private static TimerQueue s_queue = new TimerQueue();

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

        private int _currentNativeTimerStartTicks;
        private uint _currentNativeTimerDuration = UInt32.MaxValue;

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

            if (_currentNativeTimerDuration != UInt32.MaxValue)
            {
                uint elapsed = (uint)(TickCount - _currentNativeTimerStartTicks);
                if (elapsed >= _currentNativeTimerDuration)
                    return; //the timer's about to fire

                uint remainingDuration = _currentNativeTimerDuration - elapsed;
                if (actualDuration >= remainingDuration)
                    return; //the timer will fire earlier than this request
            }

            SetTimer(actualDuration);
            _currentNativeTimerDuration = actualDuration;

            _currentNativeTimerStartTicks = TickCount;
        }

        #endregion

        #region Firing timers

        //
        // The list of timers
        //
        private TimerQueueTimer _timers;
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
                _currentNativeTimerDuration = UInt32.MaxValue;

                bool haveTimerToSchedule = false;
                uint nextAppDomainTimerDuration = uint.MaxValue;

                int nowTicks = TickCount;

                //
                // Sweep through all timers.  The ones that have reached their due time
                // will fire.  We will calculate the next native timer due time from the
                // other timers.
                //
                TimerQueueTimer timer = _timers;
                while (timer != null)
                {
                    Debug.Assert(timer.m_dueTime != Timeout.UnsignedInfinite);

                    uint elapsed = (uint)(nowTicks - timer.m_startTicks);
                    if (elapsed >= timer.m_dueTime)
                    {
                        //
                        // Remember the next timer in case we delete this one
                        //
                        TimerQueueTimer nextTimer = timer.m_next;

                        if (timer.m_period != Timeout.UnsignedInfinite)
                        {
                            timer.m_startTicks = nowTicks;
                            uint elapsedForNextDueTime = elapsed - timer.m_dueTime;
                            if (elapsedForNextDueTime < timer.m_period)
                            {
                                // Discount the extra time that has elapsed since the previous firing
                                // to prevent the timer ticks from drifting
                                timer.m_dueTime = timer.m_period - elapsedForNextDueTime;
                            }
                            else
                            {
                                // Enough time has elapsed to fire the timer yet again. The timer is not able to keep up
                                // with the short period, have it fire 1 ms from now to avoid spnning without delay.
                                timer.m_dueTime = 1;
                            }

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
            if (timer.m_dueTime == Timeout.UnsignedInfinite)
            {
                // the timer is not in the list; add it (as the head of the list).
                timer.m_next = _timers;
                timer.m_prev = null;
                if (timer.m_next != null)
                    timer.m_next.m_prev = timer;
                _timers = timer;
            }
            timer.m_dueTime = dueTime;
            timer.m_period = (period == 0) ? Timeout.UnsignedInfinite : period;
            timer.m_startTicks = TickCount;
            EnsureAppDomainTimerFiresBy(dueTime);
            return true;
        }

        public void DeleteTimer(TimerQueueTimer timer)
        {
            if (timer.m_dueTime != Timeout.UnsignedInfinite)
            {
                if (timer.m_next != null)
                    timer.m_next.m_prev = timer.m_prev;
                if (timer.m_prev != null)
                    timer.m_prev.m_next = timer.m_next;
                if (_timers == timer)
                    _timers = timer.m_next;

                timer.m_dueTime = Timeout.UnsignedInfinite;
                timer.m_period = Timeout.UnsignedInfinite;
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
    internal sealed partial class TimerQueueTimer
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
        // Timeout.UnsignedInfinite if we are not going to fire.  Otherwise, the offset from m_startTime when we will fire.
        //
        internal uint m_dueTime;

        //
        // Timeout.UnsignedInfinite if we are a single-shot timer.  Otherwise, the repeat interval.
        //
        internal uint m_period;

        //
        // Info about the user's callback
        //
        private readonly TimerCallback _timerCallback;
        private readonly Object _state;
        private readonly ExecutionContext _executionContext;


        //
        // When Timer.Dispose(WaitHandle) is used, we need to signal the wait handle only
        // after all pending callbacks are complete.  We set _canceled to prevent any callbacks that
        // are already queued from running.  We track the number of callbacks currently executing in 
        // _callbacksRunning.  We set _notifyWhenNoCallbacksRunning only when _callbacksRunning
        // reaches zero.
        //
        private int _callbacksRunning;
        private volatile bool _canceled;
        private volatile WaitHandle _notifyWhenNoCallbacksRunning;


        internal TimerQueueTimer(TimerCallback timerCallback, object state, uint dueTime, uint period)
        {
            _timerCallback = timerCallback;
            _state = state;
            m_dueTime = Timeout.UnsignedInfinite;
            m_period = Timeout.UnsignedInfinite;
            _executionContext = ExecutionContext.Capture();

            //
            // After the following statement, the timer may fire.  No more manipulation of timer state outside of
            // the lock is permitted beyond this point!
            //
            if (dueTime != Timeout.UnsignedInfinite)
                Change(dueTime, period);
        }


        internal bool Change(uint dueTime, uint period)
        {
            bool success;

            using (LockHolder.Hold(TimerQueue.Instance.Lock))
            {
                if (_canceled)
                    throw new ObjectDisposedException(null, SR.ObjectDisposed_Generic);

                m_period = period;

                if (dueTime == Timeout.UnsignedInfinite)
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
                if (!_canceled)
                {
                    _canceled = true;
                    TimerQueue.Instance.DeleteTimer(this);
                }
            }
        }


        public bool Close(WaitHandle toSignal)
        {
            bool success;
            bool shouldSignal = false;

            using (LockHolder.Hold(TimerQueue.Instance.Lock))
            {
                if (_canceled)
                {
                    success = false;
                }
                else
                {
                    _canceled = true;
                    _notifyWhenNoCallbacksRunning = toSignal;
                    TimerQueue.Instance.DeleteTimer(this);

                    if (_callbacksRunning == 0)
                        shouldSignal = true;

                    success = true;
                }
            }

            if (shouldSignal)
                SignalNoCallbacksRunning();

            return success;
        }


        internal void Fire()
        {
            bool canceled = false;

            lock (TimerQueue.Instance)
            {
                canceled = _canceled;
                if (!canceled)
                    _callbacksRunning++;
            }

            if (canceled)
                return;

            CallCallback();

            bool shouldSignal = false;
            using (LockHolder.Hold(TimerQueue.Instance.Lock))
            {
                _callbacksRunning--;
                if (_canceled && _callbacksRunning == 0 && _notifyWhenNoCallbacksRunning != null)
                    shouldSignal = true;
            }

            if (shouldSignal)
                SignalNoCallbacksRunning();
        }

        internal void CallCallback()
        {
            ContextCallback callback = s_callCallbackInContext;
            if (callback == null)
                s_callCallbackInContext = callback = new ContextCallback(CallCallbackInContext);

            // call directly if EC flow is suppressed
            if (_executionContext == null)
            {
                _timerCallback(_state);
            }
            else
            {
                ExecutionContext.Run(_executionContext, callback, this);
            }
        }

        private static ContextCallback s_callCallbackInContext;

        private static void CallCallbackInContext(object state)
        {
            TimerQueueTimer t = (TimerQueueTimer)state;
            t._timerCallback(t._state);
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
    internal sealed class TimerHolder
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

        public bool Close(WaitHandle notifyObject)
        {
            bool result = m_timer.Close(notifyObject);
            GC.SuppressFinalize(this);
            return result;
        }
    }


    public sealed class Timer : MarshalByRefObject, IDisposable
    {
        private const UInt32 MAX_SUPPORTED_TIMEOUT = (uint)0xfffffffe;

        private TimerHolder _timer;

        public Timer(TimerCallback callback,
                     Object state,
                     int dueTime,
                     int period)
        {
            if (dueTime < -1)
                throw new ArgumentOutOfRangeException(nameof(dueTime), SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            if (period < -1)
                throw new ArgumentOutOfRangeException(nameof(period), SR.ArgumentOutOfRange_NeedNonNegOrNegative1);

            TimerSetup(callback, state, (UInt32)dueTime, (UInt32)period);
        }

        public Timer(TimerCallback callback,
                     Object state,
                     TimeSpan dueTime,
                     TimeSpan period)
        {
            long dueTm = (long)dueTime.TotalMilliseconds;
            if (dueTm < -1)
                throw new ArgumentOutOfRangeException(nameof(dueTm), SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            if (dueTm > MAX_SUPPORTED_TIMEOUT)
                throw new ArgumentOutOfRangeException(nameof(dueTm), SR.ArgumentOutOfRange_TimeoutTooLarge);

            long periodTm = (long)period.TotalMilliseconds;
            if (periodTm < -1)
                throw new ArgumentOutOfRangeException(nameof(periodTm), SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            if (periodTm > MAX_SUPPORTED_TIMEOUT)
                throw new ArgumentOutOfRangeException(nameof(periodTm), SR.ArgumentOutOfRange_PeriodTooLarge);

            TimerSetup(callback, state, (UInt32)dueTm, (UInt32)periodTm);
        }

        [CLSCompliant(false)]
        public Timer(TimerCallback callback,
                     Object state,
                     UInt32 dueTime,
                     UInt32 period)
        {
            TimerSetup(callback, state, dueTime, period);
        }

        public Timer(TimerCallback callback,
                     Object state,
                     long dueTime,
                     long period)
        {
            if (dueTime < -1)
                throw new ArgumentOutOfRangeException(nameof(dueTime), SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            if (period < -1)
                throw new ArgumentOutOfRangeException(nameof(period), SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            if (dueTime > MAX_SUPPORTED_TIMEOUT)
                throw new ArgumentOutOfRangeException(nameof(dueTime), SR.ArgumentOutOfRange_TimeoutTooLarge);
            if (period > MAX_SUPPORTED_TIMEOUT)
                throw new ArgumentOutOfRangeException(nameof(period), SR.ArgumentOutOfRange_PeriodTooLarge);
            TimerSetup(callback, state, (UInt32)dueTime, (UInt32)period);
        }

        public Timer(TimerCallback callback)
        {
            int dueTime = -1;    // we want timer to be registered, but not activated.  Requires caller to call
            int period = -1;    // Change after a timer instance is created.  This is to avoid the potential
                                // for a timer to be fired before the returned value is assigned to the variable,
                                // potentially causing the callback to reference a bogus value (if passing the timer to the callback). 

            TimerSetup(callback, this, (UInt32)dueTime, (UInt32)period);
        }

        private void TimerSetup(TimerCallback callback,
                                Object state,
                                UInt32 dueTime,
                                UInt32 period)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(TimerCallback));

            _timer = new TimerHolder(new TimerQueueTimer(callback, state, dueTime, period));
        }

        public bool Change(int dueTime, int period)
        {
            if (dueTime < -1)
                throw new ArgumentOutOfRangeException(nameof(dueTime), SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            if (period < -1)
                throw new ArgumentOutOfRangeException(nameof(period), SR.ArgumentOutOfRange_NeedNonNegOrNegative1);

            return _timer.m_timer.Change((UInt32)dueTime, (UInt32)period);
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            return Change((long)dueTime.TotalMilliseconds, (long)period.TotalMilliseconds);
        }

        [CLSCompliant(false)]
        public bool Change(UInt32 dueTime, UInt32 period)
        {
            return _timer.m_timer.Change(dueTime, period);
        }

        public bool Change(long dueTime, long period)
        {
            if (dueTime < -1)
                throw new ArgumentOutOfRangeException(nameof(dueTime), SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            if (period < -1)
                throw new ArgumentOutOfRangeException(nameof(period), SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            if (dueTime > MAX_SUPPORTED_TIMEOUT)
                throw new ArgumentOutOfRangeException(nameof(dueTime), SR.ArgumentOutOfRange_TimeoutTooLarge);
            if (period > MAX_SUPPORTED_TIMEOUT)
                throw new ArgumentOutOfRangeException(nameof(period), SR.ArgumentOutOfRange_PeriodTooLarge);

            return _timer.m_timer.Change((UInt32)dueTime, (UInt32)period);
        }

        public bool Dispose(WaitHandle notifyObject)
        {
            if (notifyObject == null)
                throw new ArgumentNullException(nameof(notifyObject));

            return _timer.Close(notifyObject);
        }

        public void Dispose()
        {
            _timer.Close();
        }

        internal void KeepRootedWhileScheduled()
        {
            GC.SuppressFinalize(_timer);
        }
    }
}
