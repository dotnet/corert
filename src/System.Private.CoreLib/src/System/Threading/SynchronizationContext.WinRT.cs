// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

using Internal.Runtime.Augments;

namespace System.Threading
{
    //
    // WinRT-specific implementation of SynchronizationContext
    //
    public partial class SynchronizationContext
    {
        // Get the current SynchronizationContext on the current thread
        public static SynchronizationContext Current
        {
            get
            {
                return s_current ?? GetWinRTContext();
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
