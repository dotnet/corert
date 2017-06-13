// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Threading
{
    internal static partial class ClrThreadPool
    {
        private class LowLevelLifoSemaphore
        {
            private WaitSubsystem.WaitableObject _semaphore;

            public LowLevelLifoSemaphore(int initialCount, int maxSignalCount)
            {
                _semaphore = WaitSubsystem.WaitableObject.NewSemaphore(initialCount, maxSignalCount);
            }

            public bool Wait(int timeoutMs)
            {
                return WaitSubsystem.Wait(_semaphore, timeoutMs, false, true);
            }

            public int Release(int count)
            {
                return WaitSubsystem.ReleaseSemaphore(_semaphore, count);
            }
        }
    }
}
