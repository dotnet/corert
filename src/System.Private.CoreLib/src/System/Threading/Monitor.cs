// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
        #region Object->Lock/Condition mapping

#if !FEATURE_SYNCTABLE
        private static ConditionalWeakTable<object, Lock> s_lockTable = new ConditionalWeakTable<object, Lock>();
        private static ConditionalWeakTable<object, Lock>.CreateValueCallback s_createLock = (o) => new Lock();
#endif

        private static ConditionalWeakTable<object, Condition> s_conditionTable = new ConditionalWeakTable<object, Condition>();
        private static ConditionalWeakTable<object, Condition>.CreateValueCallback s_createCondition = (o) => new Condition(GetLock(o));

        internal static Lock GetLock(Object obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            Debug.Assert(!(obj is Lock),
                "Do not use Monitor.Enter or TryEnter on a Lock instance; use Lock methods directly instead.");

#if FEATURE_SYNCTABLE
            return ObjectHeader.GetLockObject(obj);
#else
            return s_lockTable.GetValue(obj, s_createLock);
#endif
        }

        private static Condition GetCondition(Object obj)
        {
            Debug.Assert(
                !(obj is Condition || obj is Lock),
                "Do not use Monitor.Pulse or Wait on a Lock or Condition instance; use the methods on Condition instead.");
            return s_conditionTable.GetValue(obj, s_createCondition);
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
                throw new ArgumentException(SR.Argument_MustBeFalse, nameof(lockTaken));

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
                throw new ArgumentException(SR.Argument_MustBeFalse, nameof(lockTaken));

            lockTaken = GetLock(obj).TryAcquire(0);
        }

        public static bool TryEnter(Object obj, int millisecondsTimeout)
        {
            if (millisecondsTimeout < -1)
                throw new ArgumentOutOfRangeException(nameof(millisecondsTimeout), SR.ArgumentOutOfRange_NeedNonNegOrNegative1);

            Lock lck = GetLock(obj);
            if (lck.TryAcquire(0))
                return true;
            return TryAcquireContended(lck, obj, millisecondsTimeout);
        }

        public static void TryEnter(Object obj, int millisecondsTimeout, ref bool lockTaken)
        {
            if (lockTaken)
                throw new ArgumentException(SR.Argument_MustBeFalse, nameof(lockTaken));
            if (millisecondsTimeout < -1)
                throw new ArgumentOutOfRangeException(nameof(millisecondsTimeout), SR.ArgumentOutOfRange_NeedNonNegOrNegative1);

            Lock lck = GetLock(obj);
            if (lck.TryAcquire(0))
            {
                lockTaken = true;
                return;
            }
            lockTaken = TryAcquireContended(lck, obj, millisecondsTimeout);
        }

        public static bool TryEnter(Object obj, TimeSpan timeout) =>
            TryEnter(obj, WaitHandle.ToTimeoutMilliseconds(timeout));

        public static void TryEnter(Object obj, TimeSpan timeout, ref bool lockTaken) =>
            TryEnter(obj, WaitHandle.ToTimeoutMilliseconds(timeout), ref lockTaken);

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

        // Remoting is not supported, ignore exitContext
        public static bool Wait(Object obj, int millisecondsTimeout, bool exitContext) =>
            Wait(obj, millisecondsTimeout);

        // Remoting is not supported, ignore exitContext
        public static bool Wait(Object obj, TimeSpan timeout, bool exitContext) =>
            Wait(obj, WaitHandle.ToTimeoutMilliseconds(timeout));

        public static bool Wait(Object obj, int millisecondsTimeout)
        {
            Condition condition = GetCondition(obj);
            DebugBlockingItem blockingItem;

            using (new DebugBlockingScope(obj, DebugBlockingItemType.MonitorEvent, millisecondsTimeout, out blockingItem))
            {
                return condition.Wait(millisecondsTimeout);
            }
        }

        public static bool Wait(Object obj, TimeSpan timeout) => Wait(obj, WaitHandle.ToTimeoutMilliseconds(timeout));

        public static bool Wait(Object obj) => Wait(obj, Timeout.Infinite);

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

        internal static bool TryAcquireContended(Lock lck, Object obj, int millisecondsTimeout)
        {
            DebugBlockingItem blockingItem;

            using (new DebugBlockingScope(obj, DebugBlockingItemType.MonitorCriticalSection, millisecondsTimeout, out blockingItem))
            {
                return lck.TryAcquire(millisecondsTimeout);
            }
        }

        #endregion

        #region Debugger support

        // The debugger binds to the fields below by name. Do not change any names or types without
        // updating the debugger!

        // The head of the list of DebugBlockingItem stack objects used by the debugger to implement
        // ICorDebugThread4::GetBlockingObjects. Usually the list either is empty or contains a single
        // item. However, a wait on an STA thread may reenter via the message pump and cause the thread
        // to be blocked on a second object.
        [ThreadStatic]
        private static IntPtr t_firstBlockingItem;

        // Different ways a thread can be blocked that the debugger will expose.
        // Do not change or add members without updating the debugger code.
        private enum DebugBlockingItemType
        {
            MonitorCriticalSection = 0,
            MonitorEvent = 1
        }

        // Represents an item a thread is blocked on. This structure is allocated on the stack and accessed by the debugger.
        // Fields are volatile to avoid potential compiler optimizations.
        private struct DebugBlockingItem
        {
            // The object the thread is waiting on
            public volatile object _object;

            // Indicates how the thread is blocked on the item
            public volatile DebugBlockingItemType _blockingType;

            // Blocking timeout in milliseconds or Timeout.Infinite for no timeout
            public volatile int _timeout;

            // Next pointer in the linked list of DebugBlockingItem records
            public volatile IntPtr _next;
        }

        private unsafe struct DebugBlockingScope : IDisposable
        {
            public DebugBlockingScope(object obj, DebugBlockingItemType blockingType, int timeout, out DebugBlockingItem blockingItem)
            {
                blockingItem._object = obj;
                blockingItem._blockingType = blockingType;
                blockingItem._timeout = timeout;
                blockingItem._next = t_firstBlockingItem;

                t_firstBlockingItem = (IntPtr)Unsafe.AsPointer(ref blockingItem);
            }

            public void Dispose()
            {
                t_firstBlockingItem = Unsafe.Read<DebugBlockingItem>((void*)t_firstBlockingItem)._next;
            }
        }

        #endregion
    }
}
