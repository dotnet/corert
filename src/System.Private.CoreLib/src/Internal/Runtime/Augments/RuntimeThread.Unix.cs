// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Win32.SafeHandles;
using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Threading;

namespace Internal.Runtime.Augments
{
    public sealed partial class RuntimeThread
    {
        private static bool s_setThreadExitCallback;

        // Event signaling that the thread has stopped
        private ManualResetEvent _stopped;

        private readonly WaitSubsystem.ThreadWaitInfo _waitInfo;

        internal WaitSubsystem.ThreadWaitInfo WaitInfo => _waitInfo;

        private void PlatformSpecificInitialize()
        {
            // Race condition is OK here since we set to the same value
            if (!s_setThreadExitCallback)
            {
                RuntimeImports.RhSetThreadExitCallback(AddrofIntrinsics.AddrOf<Action>(OnThreadExit));
                s_setThreadExitCallback = true;
            }
        }

        private void PlatformSpecificInitializeExistingThread()
        {
            _stopped = new ManualResetEvent(false);
        }

        /// <summary>
        /// Returns true if the thread is started or being started in StartInternal.
        /// </summary>
        private bool HasStarted()
        {
            return _stopped != null;
        }

        private ThreadPriority GetPriorityLive()
        {
            return ThreadPriority.Normal;
        }

        private bool SetPriorityLive(ThreadPriority priority)
        {
            return true;
        }

        [NativeCallable]
        private static void OnThreadExit()
        {
            // Set the Stopped bit and signal the current thread as stopped
            RuntimeThread currentThread = t_currentThread;
            if (currentThread != null)
            {
                int state = currentThread._threadState;
                if ((state & (int)(ThreadState.Stopped | ThreadState.Aborted)) == 0)
                {
                    currentThread.SetThreadStateBit(ThreadState.Stopped);
                }
                currentThread._stopped.Set();
            }
        }

        private ThreadState GetThreadState() => (ThreadState)_threadState;

        private bool JoinInternal(int millisecondsTimeout)
        {
            // This method assumes the thread has been started
            Debug.Assert(!GetThreadStateBit(ThreadState.Unstarted) || (millisecondsTimeout == 0));
            SafeWaitHandle waitHandle = _stopped.SafeWaitHandle;

            // If an OS thread is terminated and its Thread object is resurrected, waitHandle may be finalized and closed
            if (waitHandle.IsClosed)
            {
                return true;
            }

            // Prevent race condition with the finalizer
            try
            {
                waitHandle.DangerousAddRef();
            }
            catch (ObjectDisposedException)
            {
                return true;
            }

            try
            {
                return _stopped.WaitOne(millisecondsTimeout);
            }
            finally
            {
                waitHandle.DangerousRelease();
            }
        }

        private bool CreateThread(GCHandle thisThreadHandle)
        {
            // Avoid OOM after creating the thread
            var stopped = new ManualResetEvent(false);

            if (!Interop.Sys.RuntimeThread_CreateThread((IntPtr)_maxStackSize,
                AddrofIntrinsics.AddrOf<Interop.Sys.ThreadProc>(StartThread), (IntPtr)thisThreadHandle))
            {
                return false;
            }

            // This marks the thread as being started
            _stopped = stopped;

            // CoreCLR ignores OS errors while setting the priority, so do we
            SetPriorityLive(_priority);

            return true;
        }

        public void Interrupt() => WaitSubsystem.Interrupt(this);
        internal static void UninterruptibleSleep0() => WaitSubsystem.UninterruptibleSleep0();
        private static void SleepInternal(int millisecondsTimeout) => WaitSubsystem.Sleep(millisecondsTimeout);

        internal const bool ReentrantWaitsEnabled = false;

        internal static void SuppressReentrantWaits()
        {
            throw new PlatformNotSupportedException();
        }

        internal static void RestoreReentrantWaits()
        {
            throw new PlatformNotSupportedException();
        }
    }
}
