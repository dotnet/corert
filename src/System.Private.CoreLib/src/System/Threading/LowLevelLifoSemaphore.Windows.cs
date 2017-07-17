// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace System.Threading
{
    /// <summary>
    /// A LIFO semaphore implemented using Win32 IO Completion Ports.
    /// </summary>
    /// <remarks>
    /// IO Completion ports release waiting threads in LIFO order, so we can use them to create a LIFO semaphore.
    /// See https://msdn.microsoft.com/en-us/library/windows/desktop/aa365198(v=vs.85).aspx under How I/O Completion Ports Work.
    /// From the docs "Threads that block their execution on an I/O completion port are released in last-in-first-out (LIFO) order."
    /// </remarks>
    internal sealed class LowLevelLifoSemaphore : IDisposable
    {
        private IntPtr _completionPort;

        public LowLevelLifoSemaphore(int initialSignalCount, int maximumSignalCount)
        {
            Debug.Assert(initialSignalCount >= 0, "Windows LowLevelLifoSemaphore does not support a negative signal count"); // TODO: Track actual signal count to enable this
            _completionPort = Interop.Kernel32.CreateIoCompletionPort(new IntPtr(-1), IntPtr.Zero, UIntPtr.Zero, 1);
            if (_completionPort == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                Environment.FailFast($"Failed to create I/O Completion Port. Error ${error}");
            }
            Release(initialSignalCount);
        }

        public bool Wait(int timeoutMs)
        {
            bool success = Interop.Kernel32.GetQueuedCompletionStatus(_completionPort, out var numberOfBytes, out var completionKey, out var pointerToOverlapped, timeoutMs);
            if (!success)
            {
                var error = Marshal.GetLastWin32Error();
                Debug.Assert(error != Interop.Kernel32.ERROR_ABANDONDED_WAIT_0, "LowLevelLifoSemaphore waited on after dispose.");
                if (error != 0)
                {
                    var exception = new OutOfMemoryException();
                    exception.SetErrorCode(error);
                    throw exception;
                }
            }
            return success;
        }

        public int Release(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if(!Interop.Kernel32.PostQueuedCompletionStatus(_completionPort, 1, UIntPtr.Zero, IntPtr.Zero))
                {
                    var lastError = Marshal.GetLastWin32Error();
                    var exception = new OutOfMemoryException();
                    exception.SetErrorCode(lastError);
                    throw exception;
                }
            }
            return 0; // TODO: Track actual signal count to calculate this
        }

        public void Dispose()
        {
            Interop.Kernel32.CloseHandle(_completionPort);
        }
    }
}
