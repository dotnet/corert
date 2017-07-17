// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Internal.Runtime.Augments;

namespace System.Threading
{
    /// <summary>
    /// Wraps a critical section and condition variable.
    /// </summary>
    internal sealed partial class LowLevelMonitor : IDisposable
    {
        private const int ErrorTimeout = 0x000005B4;

        private Interop.Kernel32.CRITICAL_SECTION _criticalSection;
        private Interop.Kernel32.CONDITION_VARIABLE _conditionVariable;

        public LowLevelMonitor()
        {
            Interop.Kernel32.InitializeCriticalSection(out _criticalSection);
            Interop.Kernel32.InitializeConditionVariable(out _conditionVariable);
        }

        private void DisposeCore()
        {
            Interop.Kernel32.DeleteCriticalSection(ref _criticalSection);
        }

        private void AcquireCore()
        {
            Interop.Kernel32.EnterCriticalSection(ref _criticalSection);
        }

        private void ReleaseCore()
        {
            Interop.Kernel32.LeaveCriticalSection(ref _criticalSection);
        }

        private void WaitCore()
        {
            WaitCore(-1);
        }

        private bool WaitCore(int timeoutMilliseconds)
        {
            bool waitResult = Interop.Kernel32.SleepConditionVariableCS(ref _conditionVariable, ref _criticalSection, timeoutMilliseconds);
            if (!waitResult)
            {
                int lastError = Marshal.GetLastWin32Error();
                if (lastError != ErrorTimeout)
                {
                    var exception = new OutOfMemoryException();
                    exception.SetErrorCode(lastError);
                    throw exception;
                }
            }
            return waitResult;
        }

        private void Signal_ReleaseCore()
        {
            Interop.Kernel32.WakeConditionVariable(ref _conditionVariable);
            Interop.Kernel32.LeaveCriticalSection(ref _criticalSection);
        }

        /// The following methods typical in a monitor are omitted since they are currently not necessary for the way in which
        /// this class is used:
        ///   - TryAcquire
        ///   - Signal (use <see cref="Signal_Release"/> instead)
        ///   - SignalAll
    }
}
