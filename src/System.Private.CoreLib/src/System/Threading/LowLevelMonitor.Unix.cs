// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace System.Threading
{
    /// <summary>
    /// Wraps a non-recursive mutex and condition.
    /// 
    /// Used by the wait subsystem on Unix, so this class cannot have any dependencies on the wait subsystem.
    /// </summary>
    internal sealed partial class LowLevelMonitor : IDisposable
    {
        private IntPtr _nativeMonitor;

        public LowLevelMonitor()
        {
            _nativeMonitor = Interop.Sys.LowLevelMonitor_New();
            if (_nativeMonitor == IntPtr.Zero)
            {
                throw new OutOfMemoryException();
            }
        }

        private void DisposeCore()
        {
            if (_nativeMonitor == IntPtr.Zero)
            {
                return;
            }

            Interop.Sys.LowLevelMonitor_Delete(_nativeMonitor);
            _nativeMonitor = IntPtr.Zero;
        }

        private void AcquireCore()
        {
            Interop.Sys.LowLevelMutex_Acquire(_nativeMonitor);
        }

        private void ReleaseCore()
        {
            Interop.Sys.LowLevelMutex_Release(_nativeMonitor);
        }

        private void WaitCore()
        {
            Interop.Sys.LowLevelMonitor_Wait(_nativeMonitor);
        }

        private bool WaitCore(int timeoutMilliseconds)
        {
            Debug.Assert(timeoutMilliseconds >= -1);

            if (timeoutMilliseconds < 0)
            {
                WaitCore();
                return true;
            }

            return Interop.Sys.LowLevelMonitor_TimedWait(_nativeMonitor, timeoutMilliseconds);
        }

        private void Signal_ReleaseCore()
        {
            Interop.Sys.LowLevelMonitor_Signal_Release(_nativeMonitor);
        }
    }
}
