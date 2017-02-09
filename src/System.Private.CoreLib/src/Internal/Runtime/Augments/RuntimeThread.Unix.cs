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

        internal WaitSubsystem.ThreadWaitInfo WaitInfo => _waitInfo;

        public ApartmentState GetApartmentState()
        {
            Debug.Fail("Should not reach here because CoreFX's Thread class does not forward to this function on Unix");
            return ApartmentState.Unknown;
        }

        public bool TrySetApartmentState(ApartmentState state)
        {
            Debug.Fail("Should not reach here because CoreFX's Thread class does not forward to this function on Unix");
            return state == ApartmentState.Unknown;
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

        internal static bool ReentrantWaitsEnabled => false;
    }
}
