// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*============================================================
**
**
**
** Purpose: Capture synchronization semantics for asynchronous callbacks
**
** 
===========================================================*/

using Internal.Runtime.Augments;

namespace System.Threading
{
    public partial class SynchronizationContext
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

        // Set the SynchronizationContext on the current thread
        public static void SetSynchronizationContext(SynchronizationContext syncContext)
        {
            RuntimeThread.CurrentThread.SynchronizationContext = syncContext;
        }

        internal static SynchronizationContext CurrentExplicit
        {
            get
            {
                return RuntimeThread.CurrentThread.SynchronizationContext;
            }
        }

        // helper to Clone this SynchronizationContext, 
        public virtual SynchronizationContext CreateCopy()
        {
            // the CLR dummy has an empty clone function - no member data
            return new SynchronizationContext();
        }
    }
}
