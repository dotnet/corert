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
    internal sealed class LowLevelMonitor : IDisposable
    {
        private const int ErrorTimeout = 0x000005B4;

        private Interop.Kernel32.CRITICAL_SECTION _criticalSection;
        private Interop.Kernel32.CONDITION_VARIABLE _conditionVariable;

#if DEBUG
        private RuntimeThread _ownerThread;
#endif

        public LowLevelMonitor()
        {
            Interop.Kernel32.InitializeCriticalSection(out _criticalSection);
            Interop.Kernel32.InitializeConditionVariable(out _conditionVariable);

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

            Interop.Kernel32.DeleteCriticalSection(ref _criticalSection);
            GC.SuppressFinalize(this);
        }

#if DEBUG
        public bool IsLocked => _ownerThread == RuntimeThread.CurrentThread;
#endif

        public void VerifyIsLocked()
        {
#if DEBUG
            Debug.Assert(IsLocked);
#endif
        }

        public void VerifyIsNotLocked()
        {
#if DEBUG
            Debug.Assert(!IsLocked);
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
            Interop.Kernel32.EnterCriticalSection(ref _criticalSection);
            SetOwnerThreadToCurrent();
        }

        public void Release()
        {
            ResetOwnerThread();
            Interop.Kernel32.LeaveCriticalSection(ref _criticalSection);
        }

        public void Wait()
        {
            Wait(-1);
        }

        public bool Wait(int timeoutMilliseconds)
        {
            Debug.Assert(timeoutMilliseconds >= -1);

            ResetOwnerThread();
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
            SetOwnerThreadToCurrent();
            return waitResult;
        }

        public void Signal_Release()
        {
            ResetOwnerThread();
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
