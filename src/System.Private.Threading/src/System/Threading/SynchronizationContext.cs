// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// 

// 
//

/*============================================================
**
** Class:  SynchronizationContext
**
**
** Purpose: Capture synchronization semantics for asynchronous callbacks
**
** 
===========================================================*/

using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
#if FEATURE_CORRUPTING_EXCEPTIONS
using System.Runtime.ExceptionServices;
#endif // FEATURE_CORRUPTING_EXCEPTIONS
using System.Runtime;
using System.Runtime.Versioning;
using System.Reflection;
using System.Security;
using System.Diagnostics.Contracts;
using System.Diagnostics.CodeAnalysis;
using Internal.Runtime.Augments;
using System.Runtime.ExceptionServices;

namespace System.Threading
{
    public class SynchronizationContext
    {
        public SynchronizationContext()
        {
        }

        public virtual void Send(SendOrPostCallback d, Object state)
        {
            d(state);
        }

        public virtual void Post(SendOrPostCallback d, Object state)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(d), state);
        }

        /// <summary>
        ///     Optional override for subclasses, for responding to notification that operation is starting.
        /// </summary>
        public virtual void OperationStarted()
        {
        }

        /// <summary>
        ///     Optional override for subclasses, for responding to notification that operation has completed.
        /// </summary>
        public virtual void OperationCompleted()
        {
        }

        [ThreadStatic]
        private static SynchronizationContext s_current;

        // Set the SynchronizationContext on the current thread
        public static void SetSynchronizationContext(SynchronizationContext syncContext)
        {
            s_current = syncContext;
        }

        // use when you don't want the TLS access to be inlined.
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void SetSynchronizationContextNoInline(SynchronizationContext syncContext)
        {
            SetSynchronizationContext(syncContext);
        }

        // Get the current SynchronizationContext on the current thread
        public static SynchronizationContext Current
        {
            get
            {
                return s_current ?? GetWinRTContext();
            }
        }

        internal static SynchronizationContext CurrentExplicit
        {
            get
            {
                return s_current;
            }
        }

        // use when you don't want the TLS access to be inlined.
        internal static SynchronizationContext CurrentExplicitNoInline
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            get
            {
                return CurrentExplicit;
            }
        }

        //
        // It's important that we always return the same SynchronizationContext object for any particular ICoreDispatcher
        // object, as long as any existing instance is still reachable.  This allows reference equality checks against the 
        // SynchronizationContext to determine if two instances represent the same dispatcher.  Async frameworks rely on this.
        // To accomplish this, we use a ConditionalWeakTable to track which instances of WinRTSynchronizationContext are bound
        // to each ICoreDispatcher instance.
        //
        private static readonly ConditionalWeakTable<Object, WinRTSynchronizationContext> s_winRTContextCache =
            new ConditionalWeakTable<Object, WinRTSynchronizationContext>();

        private static SynchronizationContext GetWinRTContext()
        {
            var dispatcher = WinRTInterop.Callbacks.GetCurrentCoreDispatcher();
            if (dispatcher == null)
                return null;

            return s_winRTContextCache.GetValue(dispatcher, _dispatcher => new WinRTSynchronizationContext(_dispatcher));
        }

        // helper to Clone this SynchronizationContext, 
        public virtual SynchronizationContext CreateCopy()
        {
            // the CLR dummy has an empty clone function - no member data
            return new SynchronizationContext();
        }
    }


    internal sealed class WinRTSynchronizationContext : SynchronizationContext
    {
        private readonly Object m_dispatcher;

        internal WinRTSynchronizationContext(Object dispatcher)
        {
            m_dispatcher = dispatcher;
        }

        private class Invoker
        {
            private readonly SendOrPostCallback m_callback;
            private readonly object m_state;

            public static readonly Action<object> InvokeDelegate = Invoke;

            public Invoker(SendOrPostCallback callback, object state)
            {
                m_callback = callback;
                m_state = state;
            }

            public static void Invoke(object thisObj)
            {
                var invoker = (Invoker)thisObj;
                invoker.InvokeCore();
            }

            private void InvokeCore()
            {
                SynchronizationContext prevSyncCtx = SynchronizationContext.CurrentExplicit;
                try
                {
                    m_callback(m_state);
                }
                catch (Exception ex)
                {
                    //
                    // If we let exceptions propagate to CoreDispatcher, it will swallow them with the idea that someone will
                    // observe them later using the IAsyncInfo returned by CoreDispatcher.RunAsync.  However, we ignore
                    // that IAsyncInfo, because there's nothing Post can do with it (since Post returns void).
                    // So, we report these as unhandled exceptions.
                    //
                    RuntimeAugments.ReportUnhandledException(ex);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(prevSyncCtx);
                }
            }
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            if (d == null)
                throw new ArgumentNullException("d");
            Contract.EndContractBlock();

            var invoker = new Invoker(d, state);
            WinRTInterop.Callbacks.PostToCoreDispatcher(m_dispatcher, Invoker.InvokeDelegate, invoker);
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            throw new NotSupportedException(SR.InvalidOperation_SendNotSupportedOnWindowsRTSynchronizationContext);
        }

        public override SynchronizationContext CreateCopy()
        {
            return new WinRTSynchronizationContext(m_dispatcher);
        }
    }
}
