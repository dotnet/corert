// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable 0420 //passing volatile fields by ref


using System;
using System.Diagnostics.Contracts;

namespace System.Threading
{
    public sealed class Condition
    {
        internal class Waiter
        {
            public Waiter next;
            public Waiter prev;
            public AutoResetEvent ev = new AutoResetEvent(false);
            public bool signalled;
        }

        [ThreadStatic]
        private static Waiter t_waiterForCurrentThread;

        private static Waiter GetWaiterForCurrentThread()
        {
            Waiter waiter = t_waiterForCurrentThread;
            if (waiter == null)
                waiter = t_waiterForCurrentThread = new Waiter();
            waiter.signalled = false;
            return waiter;
        }

        private readonly Lock _lock;
        private Waiter _waitersHead;
        private Waiter _waitersTail;

        private unsafe void AssertIsInList(Waiter waiter)
        {
            Contract.Assert(_waitersHead != null && _waitersTail != null);
            Contract.Assert((_waitersHead == waiter) == (waiter.prev == null));
            Contract.Assert((_waitersTail == waiter) == (waiter.next == null));

            for (Waiter current = _waitersHead; current != null; current = current.next)
                if (current == waiter)
                    return;
            Contract.Assert(false, "Waiter is not in the waiter list");
        }

        private unsafe void AssertIsNotInList(Waiter waiter)
        {
            Contract.Assert(waiter.next == null && waiter.prev == null);
            Contract.Assert((_waitersHead == null) == (_waitersTail == null));

            for (Waiter current = _waitersHead; current != null; current = current.next)
                if (current == waiter)
                    Contract.Assert(false, "Waiter is in the waiter list, but should not be");
        }

        private unsafe void AddWaiter(Waiter waiter)
        {
            Contract.Assert(_lock.IsAcquired);
            AssertIsNotInList(waiter);

            waiter.prev = _waitersTail;
            if (waiter.prev != null)
                waiter.prev.next = waiter;

            _waitersTail = waiter;

            if (_waitersHead == null)
                _waitersHead = waiter;
        }

        private unsafe void RemoveWaiter(Waiter waiter)
        {
            Contract.Assert(_lock.IsAcquired);
            AssertIsInList(waiter);

            if (waiter.next != null)
                waiter.next.prev = waiter.prev;
            else
                _waitersTail = waiter.prev;

            if (waiter.prev != null)
                waiter.prev.next = waiter.next;
            else
                _waitersHead = waiter.next;

            waiter.next = null;
            waiter.prev = null;
        }

        public Condition(Lock @lock)
        {
            if (@lock == null)
                throw new ArgumentNullException("lock");
            _lock = @lock;
        }

        public bool Wait()
        {
            return Wait(Timeout.Infinite);
        }

        public bool Wait(TimeSpan timeout)
        {
            long tm = (long)timeout.TotalMilliseconds;
            if (tm < -1 || tm > (long)Int32.MaxValue)
                throw new ArgumentOutOfRangeException("timeout", SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            return Wait((int)tm);
        }

        public unsafe bool Wait(int millisecondsTimeout)
        {
            if (millisecondsTimeout < -1)
                throw new ArgumentOutOfRangeException("millisecondsTimeout", SR.ArgumentOutOfRange_NeedNonNegOrNegative1);

            if (!_lock.IsAcquired)
                throw new SynchronizationLockException();

            Waiter waiter = GetWaiterForCurrentThread();
            AddWaiter(waiter);

            uint recursionCount = _lock.ReleaseAll();
            bool success = false;
            try
            {
                success = waiter.ev.WaitOne(millisecondsTimeout);
            }
            finally
            {
                _lock.Reacquire(recursionCount);
                Contract.Assert(_lock.IsAcquired);

                if (!waiter.signalled)
                {
                    RemoveWaiter(waiter);
                }
                else if (!success)
                {
                    //
                    // The wait timed out, but we were signalled before we could reacquire the lock.
                    // Since WaitOne timed out, it didn't trigger the auto-reset of the AutoResetEvent.
                    // So, we need to manually reset the event.
                    //
                    waiter.ev.Reset();
                }

                AssertIsNotInList(waiter);
            }

            return waiter.signalled;
        }

        public unsafe void SignalAll()
        {
            if (!_lock.IsAcquired)
                throw new SynchronizationLockException();

            while (_waitersHead != null)
                SignalOne();
        }

        public unsafe void SignalOne()
        {
            if (!_lock.IsAcquired)
                throw new SynchronizationLockException();

            Waiter waiter = _waitersHead;
            if (waiter != null)
            {
                RemoveWaiter(waiter);
                waiter.signalled = true;
                waiter.ev.Set();
            }
        }
    }
}
