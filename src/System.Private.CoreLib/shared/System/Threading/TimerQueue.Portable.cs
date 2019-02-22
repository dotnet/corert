// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;

namespace System.Threading
{
    //
    // Unix-specific implementation of Timer
    //
    internal partial class TimerQueue : IThreadPoolWorkItem
    {
        private static List<ScheduledTimer> s_scheduledTimers;
        private static List<int> s_timerIDsToFire;

        /// <summary>
        /// This event is used by the timer thread to wait for timer expiration. It is also
        /// used to notify the timer thread that a new timer has been set.
        /// </summary>
        private static readonly AutoResetEvent s_timerEvent = new AutoResetEvent(false);

        private readonly int _id;

        private TimerQueue(int id)
        {
            _id = id;
        }

        private static List<ScheduledTimer> InitializeScheduledTimerManager_Locked()
        {
            Debug.Assert(s_scheduledTimers == null);

            var scheduledTimers = new List<ScheduledTimer>(Instances.Length);
            if (s_timerIDsToFire == null)
            {
                s_timerIDsToFire = new List<int>(Instances.Length);
            }

            Thread timerThread = new Thread(TimerThread);
            timerThread.IsBackground = true;
            timerThread.Start();

            // Do this after creating the thread in case thread creation fails so that it will try again next time
            s_scheduledTimers = scheduledTimers;
            return scheduledTimers;
        }

        private bool SetTimer(uint actualDuration)
        {
            Debug.Assert((int)actualDuration >= 0);
            var timer = new ScheduledTimer(_id, TickCount + (int)actualDuration);
            AutoResetEvent timerEvent = s_timerEvent;
            lock (timerEvent)
            {
                List<ScheduledTimer> timers = s_scheduledTimers;
                if (timers == null)
                {
                    timers = InitializeScheduledTimerManager_Locked();
                }

                timers.Add(timer);
            }

            timerEvent.Set();
            return true;
        }

        /// <summary>
        /// This method is executed on a dedicated a timer thread. Its purpose is
        /// to handle timer requests and notify the TimerQueue when a timer expires.
        /// </summary>
        private static void TimerThread()
        {
            AutoResetEvent timerEvent = s_timerEvent;
            List<int> timerIDsToFire = s_timerIDsToFire;
            List<ScheduledTimer> timers;
            lock (timerEvent)
            {
                timers = s_scheduledTimers;
            }

            int shortestWaitDurationMs = Timeout.Infinite;
            while (true)
            {
                timerEvent.WaitOne(shortestWaitDurationMs);

                int currentTimeMs = TickCount;
                shortestWaitDurationMs = int.MaxValue;
                lock (timerEvent)
                {
                    for (int i = timers.Count - 1; i >= 0; --i)
                    {
                        int waitDurationMs = timers[i].dueTimeMs - currentTimeMs;
                        if (waitDurationMs <= 0)
                        {
                            timerIDsToFire.Add(timers[i].id);

                            int lastIndex = timers.Count - 1;
                            if (i != lastIndex)
                            {
                                timers[i] = timers[lastIndex];
                            }
                            timers.RemoveAt(lastIndex);
                            continue;
                        }

                        if (waitDurationMs < shortestWaitDurationMs)
                        {
                            shortestWaitDurationMs = waitDurationMs;
                        }
                    }
                }

                if (timerIDsToFire.Count > 0)
                {
                    foreach (int timerIDToFire in timerIDsToFire)
                    {
                        ThreadPool.UnsafeQueueUserWorkItemInternal(Instances[timerIDToFire], preferLocal: false);
                    }
                    timerIDsToFire.Clear();
                }

                if (shortestWaitDurationMs == int.MaxValue)
                {
                    shortestWaitDurationMs = Timeout.Infinite;
                }
            }
        }

        private static int TickCount => Environment.TickCount;
        void IThreadPoolWorkItem.Execute() => FireNextTimers();

        private struct ScheduledTimer
        {
            public int id;
            public int dueTimeMs;

            public ScheduledTimer(int id, int dueTimeMs)
            {
                Debug.Assert(id >= 0);
                Debug.Assert(id < Instances.Length);
                Debug.Assert(id == Instances[id]._id);

                this.id = id;
                this.dueTimeMs = dueTimeMs;
            }
        }
    }
}
