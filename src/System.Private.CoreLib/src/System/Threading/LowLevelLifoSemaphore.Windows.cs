// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace System.Threading
{
    /// <summary>
    /// A LIFO semaphore implemented using Win32 IO Completion Ports.
    /// </summary>
    internal sealed class LowLevelLifoSemaphore : IDisposable
    {
        private IntPtr _completionPort;

        public LowLevelLifoSemaphore(int initialSignalCount, int maximumSignalCount)
        {
            Debug.Assert(initialSignalCount >= 0, "Windows LowLevelLifoSemaphore does not support a negative signal count"); // TODO: Track actual signal count to enable this
            _completionPort = Interop.Kernel32.CreateIoCompletionPort(new IntPtr(-1), IntPtr.Zero, UIntPtr.Zero, 1);
            Release(initialSignalCount);
        }

        public bool Wait(int timeoutMs)
        {
            return Interop.Kernel32.GetQueuedCompletionStatus(_completionPort, out var numberOfBytes, out var completionKey, out var pointerToOverlapped, timeoutMs);
        }

        public int Release(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Interop.Kernel32.PostQueuedCompletionStatus(_completionPort, 1, UIntPtr.Zero, IntPtr.Zero);
            }
            return 0; // TODO: Track actual signal count to calculate this
        }

        public void Dispose()
        {
            Interop.Kernel32.CloseHandle(_completionPort);
        }
    }
}
