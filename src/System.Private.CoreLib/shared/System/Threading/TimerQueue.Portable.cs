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
        private static readonly Lock s_lock = new Lock();
        private static List<AppDomainTimer> s_appDomainTimers;

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

        private static List<AppDomainTimer> InitializeAppDomainTimerManager_Locked()
        {
            Debug.Assert(s_lock.IsAcquired);
            Debug.Assert(s_appDomainTimers == null);

            var appDomainTimers = new List<AppDomainTimer>(Instances.Length);

            Thread timerThread = new Thread(TimerThread);
            timerThread.IsBackground = true;
            timerThread.Start();

            // Do this after creating the thread in case thread creation fails so that it will try again next time
            s_appDomainTimers = appDomainTimers;
            return appDomainTimers;
        }

        private bool SetTimer(uint actualDuration)
        {
            Debug.Assert((int)actualDuration >= 0);
            var timer = new AppDomainTimer(_id, TickCount + (int)actualDuration);
            using (LockHolder.Hold(s_lock))
            {
                List<AppDomainTimer> timers = s_appDomainTimers;
                if (timers == null)
                {
                    timers = InitializeAppDomainTimerManager_Locked();
                }

                timers.Add(timer);
            }

            s_timerEvent.Set();
            return true;
        }

        /// <summary>
        /// This method is executed on a dedicated a timer thread. Its purpose is
        /// to handle timer requests and notify the TimerQueue when a timer expires.
        /// </summary>
        private static void TimerThread()
        {
            AutoResetEvent timerEvent = s_timerEvent;
            Lock lok = s_lock;
            List<AppDomainTimer> timers;
            using (LockHolder.Hold(lok))
            {
                timers = s_appDomainTimers;
            }

            int shortestWaitDurationMs = Timeout.Infinite;
            while (true)
            {
                timerEvent.WaitOne(shortestWaitDurationMs);

                int currentTimeMs = TickCount;
                shortestWaitDurationMs = int.MaxValue;
                using (LockHolder.Hold(lok))
                {
                    for (int i = timers.Count - 1; i >= 0; --i)
                    {
                        int waitDurationMs = timers[i].dueTimeMs - currentTimeMs;
                        if (waitDurationMs <= 0)
                        {
                            ThreadPool.UnsafeQueueUserWorkItemInternal(Instances[timers[i].id], preferLocal: false);

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

                if (shortestWaitDurationMs == int.MaxValue)
                {
                    shortestWaitDurationMs = Timeout.Infinite;
                }
            }
        }

        private static int TickCount => Environment.TickCount;
        void IThreadPoolWorkItem.Execute() => FireNextTimers();

        private struct AppDomainTimer
        {
            public int id;
            public int dueTimeMs;

            public AppDomainTimer(int id, int dueTimeMs)
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
