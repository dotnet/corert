// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;

namespace Internal.Runtime.Augments
{
    public sealed partial class RuntimeThread
    {
        private readonly WaitSubsystem.ThreadWaitInfo _waitInfo;

        private void PlatformSpecificInitialize()
        {
            _waitInfo = new WaitSubsystem.ThreadWaitInfo(this);
        }

        private void PlatformSpecificInitializeExistingThread() { }

        internal WaitSubsystem.ThreadWaitInfo WaitInfo => _waitInfo;

        private static bool SetPriority(ThreadPriority priority) { return true; }

        private bool HasFinishedExecution()
        {
            // TODO: Return true if the thread has finished execution
            return false;
        }

        private void StartCore(object parameter)
        {
            // TODO: Start a new thread using _threadStart and parameter
        }

        public ApartmentState GetApartmentState()
        {
            Environment.FailFast(
                "Should not reach here because CoreFX's Thread class should not forward to this function on Unix");
            return ApartmentState.Unknown;
        }

        public bool TrySetApartmentState(ApartmentState state)
        {
            Environment.FailFast(
                "Should not reach here because CoreFX's Thread class should not forward to this function on Unix");
            return false;
        }

        public void DisableComObjectEagerCleanup() { }

        public void Interrupt() => WaitSubsystem.Interrupt(this);
        internal static void UninterruptibleSleep0() => WaitSubsystem.UninterruptibleSleep0();
        private static void SleepCore(int millisecondsTimeout) => WaitSubsystem.Sleep(millisecondsTimeout);

        internal static void SuppressReentrantWaits()
        {
            throw new PlatformNotSupportedException();
        }

        internal static void RestoreReentrantWaits()
        {
            throw new PlatformNotSupportedException();
        }

        internal const bool ReentrantWaitsEnabled = false;
    }
}
