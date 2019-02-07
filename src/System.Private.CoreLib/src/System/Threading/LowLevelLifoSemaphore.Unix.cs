// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace System.Threading
{
    /// <summary>
    /// A LIFO semaphore.
    /// Waits on this semaphore are uninterruptible.
    /// </summary>
    internal sealed partial class LowLevelLifoSemaphore : IDisposable
    {
        private WaiterListEntry _waiterStackHead;
        private LowLevelLock _waiterStackLock;
        [ThreadStatic]
        private static WaiterListEntry t_waitEntry;

        private void Create(int maximumSignalCount)
        {
            _waiterStackHead = null;
            _waiterStackLock = new LowLevelLock();
        }

        public void Dispose()
        {
        }

        private bool WaitCore(int timeoutMs)
        {
            WaiterListEntry waitEntry = t_waitEntry ?? (t_waitEntry = new WaiterListEntry());
            waitEntry._monitor.Acquire();
            try
            {
                _waiterStackLock.Acquire();
                waitEntry._next = _waiterStackHead;
                _waiterStackHead = waitEntry;
                _waiterStackLock.Release();
                return waitEntry._monitor.Wait(timeoutMs);
            }
            finally
            {
                waitEntry._monitor.Release();
            }
        }

        private void ReleaseCore(int count)
        {
            while (count-- > 0)
            {
                _waiterStackLock.Acquire();
                WaiterListEntry waitEntry = _waiterStackHead;
                _waiterStackHead = waitEntry?._next;
                _waiterStackLock.Release();
                if (waitEntry != null)
                {
                    waitEntry._monitor.Acquire();
                    waitEntry._monitor.Signal_Release();
                }
            }
        }

        class WaiterListEntry
        {
            public LowLevelMonitor _monitor;
            public WaiterListEntry _next;

            public WaiterListEntry()
            {
                this._monitor = new LowLevelMonitor();
            }
        }
    }
}
