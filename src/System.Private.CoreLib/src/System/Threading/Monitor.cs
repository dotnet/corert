// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*=============================================================================
**
**
**
** Purpose: Synchronizes access to a shared resource or region of code in a multi-threaded 
**             program.
**
**
=============================================================================*/

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Threading
{
    public static class Monitor
    {
        #region Object->Lock mappings

        private static ConditionalWeakTable<object, Lock> s_lockTable;
        private static ConditionalWeakTable<object, Condition> s_conditionTable;

        private static Lock GetLockFromTable(Object obj)
        {
            ConditionalWeakTable<object, Lock> table = LazyInitializer.EnsureInitialized(ref s_lockTable, () => new ConditionalWeakTable<object, Lock>());
            return table.GetValue(obj, (o) => new Lock());
        }

        private static Lock GetLock(Object obj)
        {
            if (obj == null)
                throw new ArgumentNullException("obj");

            if (Lock.IsLock(obj))
                return (Lock)obj;

            return GetLockFromTable(obj);
        }

        private static Condition GetConditionFromTable(Object obj)
        {
            ConditionalWeakTable<object, Condition> table = LazyInitializer.EnsureInitialized(ref s_conditionTable, () => new ConditionalWeakTable<object, Condition>());
            return table.GetValue(obj, (o) => new Condition(GetLock(o)));
        }

        private static Condition GetCondition(Object obj)
        {
            Debug.Assert(
                !(obj is Condition || obj is Lock),
                "Do not use Monitor.Pulse or Wait on a Lock or Condition instance; use the methods on Condition instead.");
            return GetConditionFromTable(obj);
        }
        #endregion

        #region Public Enter/Exit methods

        public static void Enter(Object obj)
        {
            Lock lck = GetLock(obj);
            if (lck.TryAcquire(0))
                return;
            TryAcquireContended(lck, obj, Timeout.Infinite);
        }

        public static void Enter(Object obj, ref bool lockTaken)
        {
            if (lockTaken)
                throw new ArgumentException(SR.Argument_MustBeFalse, "lockTaken");

            Lock lck = GetLock(obj);
            if (lck.TryAcquire(0))
            {
                lockTaken = true;
                return;
            }
            TryAcquireContended(lck, obj, Timeout.Infinite);
            lockTaken = true;
        }

        public static bool TryEnter(Object obj)
        {
            return GetLock(obj).TryAcquire(0);
        }

        public static void TryEnter(Object obj, ref bool lockTaken)
        {
            if (lockTaken)
                throw new ArgumentException(SR.Argument_MustBeFalse, "lockTaken");

            lockTaken = GetLock(obj).TryAcquire(0);
        }

        public static bool TryEnter(Object obj, int millisecondsTimeout)
        {
            if (millisecondsTimeout < -1)
                throw new ArgumentOutOfRangeException("millisecondsTimeout", SR.ArgumentOutOfRange_NeedNonNegOrNegative1);

            Lock lck = GetLock(obj);
            if (lck.TryAcquire(0))
                return true;
            return TryAcquireContended(lck, obj, millisecondsTimeout);
        }

        public static void TryEnter(Object obj, int millisecondsTimeout, ref bool lockTaken)
        {
            if (lockTaken)
                throw new ArgumentException(SR.Argument_MustBeFalse, "lockTaken");
            if (millisecondsTimeout < -1)
                throw new ArgumentOutOfRangeException("millisecondsTimeout", SR.ArgumentOutOfRange_NeedNonNegOrNegative1);

            Lock lck = GetLock(obj);
            if (lck.TryAcquire(0))
            {
                lockTaken = true;
                return;
            }
            lockTaken = TryAcquireContended(lck, obj, millisecondsTimeout);
        }

        public static bool TryEnter(Object obj, TimeSpan timeout)
        {
            long tm = (long)timeout.TotalMilliseconds;
            if (tm < -1 || tm > (long)Int32.MaxValue)
                throw new ArgumentOutOfRangeException("timeout", SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            int millisecondsTimeout = (int)tm;

            Lock lck = GetLock(obj);
            if (lck.TryAcquire(0))
                return true;
            return lck.TryAcquire(millisecondsTimeout);
        }

        public static void TryEnter(Object obj, TimeSpan timeout, ref bool lockTaken)
        {
            if (lockTaken)
                throw new ArgumentException(SR.Argument_MustBeFalse, "lockTaken");
            long tm = (long)timeout.TotalMilliseconds;
            if (tm < -1 || tm > (long)Int32.MaxValue)
                throw new ArgumentOutOfRangeException("timeout", SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            int millisecondsTimeout = (int)tm;

            Lock lck = GetLock(obj);
            if (lck.TryAcquire(0))
            {
                lockTaken = true;
                return;
            }

            lockTaken = TryAcquireContended(lck, obj, millisecondsTimeout);
        }

        public static void Exit(Object obj)
        {
            GetLock(obj).Release();
        }

        public static bool IsEntered(Object obj)
        {
            return GetLock(obj).IsAcquired;
        }

        #endregion

        #region Public Wait/Pulse methods

        public static bool Wait(object obj)
        {
            Condition condition = GetCondition(obj);
            int removeCookie = t_blockingObjects.Add(obj, ReasonForBlocking.OnEvent);
            try
            {
                return condition.Wait();
            }
            finally
            {
                t_blockingObjects.Remove(removeCookie);
            }
        }

        public static bool Wait(object obj, int millisecondsTimeout)
        {
            Condition condition = GetCondition(obj);
            int removeCookie = t_blockingObjects.Add(obj, ReasonForBlocking.OnEvent);
            try
            {
                return condition.Wait(millisecondsTimeout);
            }
            finally
            {
                t_blockingObjects.Remove(removeCookie);
            }
        }

        public static bool Wait(object obj, TimeSpan timeout)
        {
            Condition condition = GetCondition(obj);
            int removeCookie = t_blockingObjects.Add(obj, ReasonForBlocking.OnEvent);
            try
            {
                return condition.Wait(timeout);
            }
            finally
            {
                t_blockingObjects.Remove(removeCookie);
            }
        }

        public static void Pulse(object obj)
        {
            GetCondition(obj).SignalOne();
        }

        public static void PulseAll(object obj)
        {
            GetCondition(obj).SignalAll();
        }

        #endregion

        #region Slow path for Entry/TryEnter methods.
        private static bool TryAcquireContended(Lock lck, Object obj, int millisecondsTimeout)
        {
            int removeCookie = t_blockingObjects.Add(obj, ReasonForBlocking.OnCrst);
            try
            {
                return lck.TryAcquire(millisecondsTimeout);
            }
            finally
            {
                t_blockingObjects.Remove(removeCookie);
            }
        }

#pragma warning disable 414 // assigned but never used.
        //
        // Do not remove: These fields are examined by the debugger interface to implement ICorDebugThread4::GetBlockingObjects().
        //
        [ThreadStatic]
        private static CorDbgList t_blockingObjects;


        //
        // A simple LIFO list of blocking objects for the use of the debugger api.
        //
        // Ordinarily, the list is unlikely to grow past one (a wait on a STA may reenter via the message pump and cause the thread to be blocked on a second object.)
        // The list is capped in size - in the unlikely event that we reach the cap, the debugger will receive incomplete information.
        //
        private struct CorDbgList
        {
            public int Add(Object blockingObject, ReasonForBlocking reasonForBlocking)
            {
                if (_blockingObjects == null)
                    _blockingObjects = new BlockingObject[InitialSize];

                int count = _count;
                if (count == _blockingObjects.Length)
                {
                    int newSize = count + GrowBy;
                    if (newSize > MaxSize)
                        return -1;
                    Array.Resize<BlockingObject>(ref _blockingObjects, newSize);
                }

                _blockingObjects[count] = new BlockingObject(blockingObject, reasonForBlocking);
                _count++;
                return count;
            }

            public void Remove(int removeCookie)
            {
                if (removeCookie != -1)
                {
                    _count = removeCookie;
                }
            }

            private BlockingObject[] _blockingObjects;
            private int _count;

            private const int InitialSize = 5;
            private const int GrowBy = 5;
            private const int MaxSize = 100;

            private struct BlockingObject
            {
                public BlockingObject(Object obj, ReasonForBlocking reasonForBlocking)
                {
                    _object = obj;
                    _reasonForBlocking = reasonForBlocking;
                }

                private Object _object;   // This field is examined directly by the debugger: Do not change without talking to debugger folks.
                private ReasonForBlocking _reasonForBlocking; // This field is examined directly by the debugger: Do not change without talking to debugger folks.
            }
        }

        // The CorDbg interfaces know about this enum: Do not change or add members to this enum without talking to the debugger folks.
        private enum ReasonForBlocking
        {
            OnCrst = 0,
            OnEvent = 1,
        }
#pragma warning restore 414

        #endregion
    }
}
