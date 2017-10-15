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
    [Flags]
    internal enum SynchronizationContextProperties
    {
        None = 0,
        RequireWaitNotification = 0x1
    };

    public partial class SynchronizationContext
    {
        private SynchronizationContextProperties _props = SynchronizationContextProperties.None;

        public SynchronizationContext()
        {
        }

#if PLATFORM_WINDOWS
        // protected so that only the derived sync context class can enable these flags
        protected void SetWaitNotificationRequired()
        {
            _props |= SynchronizationContextProperties.RequireWaitNotification;
        }
#endif

        public bool IsWaitNotificationRequired()
        {
            return ((_props & SynchronizationContextProperties.RequireWaitNotification) != 0);
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

#if PLATFORM_WINDOWS
        // Method called when the CLR does a wait operation
        [CLSCompliant(false)]
        public virtual int Wait(IntPtr[] waitHandles, bool waitAll, int millisecondsTimeout)
        {
            return WaitHelper(waitHandles, waitAll, millisecondsTimeout);
        }

        // Method that can be called by Wait overrides
        [CLSCompliant(false)]
        protected static int WaitHelper(IntPtr[] waitHandles, bool waitAll, int millisecondsTimeout)
        {
            if (waitHandles == null)
            {
                throw new ArgumentNullException(nameof(waitHandles));
            }

            return WaitHandle.WaitForMultipleObjectsIgnoringSyncContext(waitHandles, waitHandles.Length, waitAll, millisecondsTimeout);
        }
#endif

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
