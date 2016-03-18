// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// 

// 
//

/*============================================================
**
** Class:  ExecutionContext
**
**
** Purpose: Capture execution  context for a thread
**
** 
===========================================================*/
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Security;

namespace System.Threading
{
    public delegate void ContextCallback(Object state);

    internal struct ExecutionContextSwitcher
    {
        internal ExecutionContext m_ec;
        internal SynchronizationContext m_sc;

        internal void Undo()
        {
            SynchronizationContext.SetSynchronizationContext(m_sc);
            ExecutionContext.Restore(m_ec);
        }
    }

    public sealed class ExecutionContext
    {
        public static readonly ExecutionContext Default = new ExecutionContext();

        [ThreadStatic]
        static ExecutionContext t_currentMaybeNull;

        private readonly Dictionary<IAsyncLocal, object> m_localValues;
        private readonly List<IAsyncLocal> m_localChangeNotifications;

        private ExecutionContext()
        {
            m_localValues = new Dictionary<IAsyncLocal, object>();
            m_localChangeNotifications = new List<IAsyncLocal>();
        }

        private ExecutionContext(ExecutionContext other)
        {
            m_localValues = new Dictionary<IAsyncLocal, object>(other.m_localValues);
            m_localChangeNotifications = new List<IAsyncLocal>(other.m_localChangeNotifications);
        }

        public static ExecutionContext Capture()
        {
            return t_currentMaybeNull ?? ExecutionContext.Default;
        }

        public static void Run(ExecutionContext executionContext, ContextCallback callback, Object state)
        {
            ExecutionContextSwitcher ecsw = default(ExecutionContextSwitcher);
            try
            {
                EstablishCopyOnWriteScope(ref ecsw);

                ExecutionContext.Restore(executionContext);
                callback(state);
            }
            catch
            {
                // Note: we have a "catch" rather than a "finally" because we want
                // to stop the first pass of EH here.  That way we can restore the previous
                // context before any of our callers' EH filters run.  That means we need to 
                // end the scope separately in the non-exceptional case below.
                ecsw.Undo();
                throw;
            }
            ecsw.Undo();
        }

        internal static void Restore(ExecutionContext executionContext)
        {
            if (executionContext == null)
                throw new InvalidOperationException(SR.InvalidOperation_NullContext);

            ExecutionContext previous = t_currentMaybeNull ?? Default;
            t_currentMaybeNull = executionContext;

            if (previous != executionContext)
                OnContextChanged(previous, executionContext);
        }

        static internal void EstablishCopyOnWriteScope(ref ExecutionContextSwitcher ecsw)
        {
            ecsw.m_ec = Capture();
            ecsw.m_sc = SynchronizationContext.CurrentExplicit;
        }

        private static void OnContextChanged(ExecutionContext previous, ExecutionContext current)
        {
            previous = previous ?? Default;

            foreach (IAsyncLocal local in previous.m_localChangeNotifications)
            {
                object previousValue;
                object currentValue;
                previous.m_localValues.TryGetValue(local, out previousValue);
                current.m_localValues.TryGetValue(local, out currentValue);

                if (previousValue != currentValue)
                    local.OnValueChanged(previousValue, currentValue, true);
            }

            if (current.m_localChangeNotifications != previous.m_localChangeNotifications)
            {
                try
                {
                    foreach (IAsyncLocal local in current.m_localChangeNotifications)
                    {
                        // If the local has a value in the previous context, we already fired the event for that local
                        // in the code above.
                        object previousValue;
                        if (!previous.m_localValues.TryGetValue(local, out previousValue))
                        {
                            object currentValue;
                            current.m_localValues.TryGetValue(local, out currentValue);

                            if (previousValue != currentValue)
                                local.OnValueChanged(previousValue, currentValue, true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Environment.FailFast(SR.ExecutionContext_ExceptionInAsyncLocalNotification, ex);
                }
            }
        }

        internal static object GetLocalValue(IAsyncLocal local)
        {
            ExecutionContext current = t_currentMaybeNull;
            if (current == null)
                return null;

            object value;
            current.m_localValues.TryGetValue(local, out value);
            return value;
        }

        internal static void SetLocalValue(IAsyncLocal local, object newValue, bool needChangeNotifications)
        {
            ExecutionContext current = t_currentMaybeNull ?? ExecutionContext.Default;

            object previousValue;
            bool hadPreviousValue = current.m_localValues.TryGetValue(local, out previousValue);

            if (previousValue == newValue)
                return;

            current = new ExecutionContext(current);
            current.m_localValues[local] = newValue;

            t_currentMaybeNull = current;

            if (needChangeNotifications)
            {
                if (hadPreviousValue)
                    Contract.Assert(current.m_localChangeNotifications.Contains(local));
                else
                    current.m_localChangeNotifications.Add(local);

                local.OnValueChanged(previousValue, newValue, false);
            }
        }

        [Flags]
        internal enum CaptureOptions
        {
            None = 0x00,
            IgnoreSyncCtx = 0x01,
            OptimizeDefaultCase = 0x02,
        }

        internal static ExecutionContext PreAllocatedDefault
        {
            get
            { return ExecutionContext.Default; }
        }

        internal bool IsPreAllocatedDefault
        {
            get { return this == ExecutionContext.Default; }
        }
    }
}


