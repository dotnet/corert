// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Threading
{
    /// <summary>
    /// A LIFO semaphore.
    /// Waits on this semaphore are uninterruptible.
    /// </summary>
    internal sealed class LowLevelLifoSemaphore : IDisposable
    {
        private WaitSubsystem.WaitableObject _semaphore;

        public LowLevelLifoSemaphore(int initialSignalCount, int maximumSignalCount)
        {
            _semaphore = WaitSubsystem.WaitableObject.NewSemaphore(initialSignalCount, maximumSignalCount);
        }

        public bool Wait(int timeoutMs)
        {
            return WaitSubsystem.Wait(_semaphore, timeoutMs, false, true);
        }

        public int Release(int count)
        {
            return WaitSubsystem.ReleaseSemaphore(_semaphore, count);
        }

        public void Dispose()
        {
        }
    }
}
