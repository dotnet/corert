// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace System.Threading
{
    public sealed partial class RuntimeThread
    {
        /// <summary>
        /// Used by <see cref="WaitHandle"/>'s multi-wait functions
        /// </summary>
        private WaitHandleArray<IntPtr> _waitedHandles;

        internal IntPtr[] GetWaitedHandleArray(int requiredCapacity)
        {
            Debug.Assert(this == CurrentThread);

            _waitedHandles.EnsureCapacity(requiredCapacity);
            return _waitedHandles.Items;
        }

        public ApartmentState GetApartmentState() { throw null; }
        public bool TrySetApartmentState(ApartmentState state) { throw null; }
        public void DisableComObjectEagerCleanup() { throw null; }
        public void Interrupt() { throw null; }

        internal static void UninterruptibleSleep0()
        {
            Interop.mincore.Sleep(0);
        }

        private static void SleepCore(int millisecondsTimeout)
        {
            Debug.Assert(millisecondsTimeout >= -1);
            Interop.mincore.Sleep((uint)millisecondsTimeout);
        }

        internal static void SuppressReentrantWaits() => LowLevelThread.SuppressReentrantWaits();
        internal static void RestoreReentrantWaits() => LowLevelThread.RestoreReentrantWaits();
        internal static bool ReentrantWaitsEnabled => LowLevelThread.ReentrantWaitsEnabled;
    }
}
