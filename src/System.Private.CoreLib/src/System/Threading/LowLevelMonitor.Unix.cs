// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Internal.Runtime.Augments;

namespace System.Threading
{
    /// <summary>
    /// Wraps a non-recursive mutex and condition.
    /// 
    /// Used by the wait subsystem on Unix, so this class cannot have any dependencies on the wait subsystem.
    /// </summary>
    internal sealed class LowLevelMonitor : IDisposable
    {
        private IntPtr _nativeMonitor;

#if DEBUG
        private RuntimeThread _ownerThread;
#endif

        public LowLevelMonitor()
        {
            _nativeMonitor = Interop.Sys.LowLevelMonitor_New();
            if (_nativeMonitor == IntPtr.Zero)
            {
                throw new OutOfMemoryException();
            }

#if DEBUG
            _ownerThread = null;
#endif
        }

        ~LowLevelMonitor()
        {
            Dispose();
        }

        public void Dispose()
        {
            VerifyIsNotLockedByAnyThread();

            if (_nativeMonitor == IntPtr.Zero)
            {
                return;
            }

            Interop.Sys.LowLevelMonitor_Delete(_nativeMonitor);
            _nativeMonitor = IntPtr.Zero;
            GC.SuppressFinalize(this);
        }

        public void VerifyIsLocked()
        {
#if DEBUG
            Debug.Assert(_ownerThread == RuntimeThread.CurrentThread);
#endif
        }

        public void VerifyIsNotLocked()
        {
#if DEBUG
            Debug.Assert(_ownerThread != RuntimeThread.CurrentThread);
#endif
        }

        private void VerifyIsNotLockedByAnyThread()
        {
#if DEBUG
            Debug.Assert(_ownerThread == null);
#endif
        }

        private void ResetOwnerThread()
        {
#if DEBUG
            VerifyIsLocked();
            _ownerThread = null;
#endif
        }

        private void SetOwnerThreadToCurrent()
        {
#if DEBUG
            VerifyIsNotLockedByAnyThread();
            _ownerThread = RuntimeThread.CurrentThread;
#endif
        }

        public void Acquire()
        {
            VerifyIsNotLocked();
            Interop.Sys.LowLevelMutex_Acquire(_nativeMonitor);
            SetOwnerThreadToCurrent();
        }

        public void Release()
        {
            ResetOwnerThread();
            Interop.Sys.LowLevelMutex_Release(_nativeMonitor);
        }

        public void Wait()
        {
            ResetOwnerThread();
            Interop.Sys.LowLevelMonitor_Wait(_nativeMonitor);
            SetOwnerThreadToCurrent();
        }

        public bool Wait(int timeoutMilliseconds)
        {
            Debug.Assert(timeoutMilliseconds >= -1);

            if (timeoutMilliseconds < 0)
            {
                Wait();
                return true;
            }

            ResetOwnerThread();
            bool waitResult = Interop.Sys.LowLevelMonitor_TimedWait(_nativeMonitor, timeoutMilliseconds);
            SetOwnerThreadToCurrent();
            return waitResult;
        }

        public void Signal_Release()
        {
            ResetOwnerThread();
            Interop.Sys.LowLevelMonitor_Signal_Release(_nativeMonitor);
        }

        /// The following methods typical in a monitor are omitted since they are currently not necessary for the way in which
        /// this class is used:
        ///   - TryAcquire
        ///   - Signal (use <see cref="Signal_Release"/> instead)
        ///   - SignalAll
    }
}
