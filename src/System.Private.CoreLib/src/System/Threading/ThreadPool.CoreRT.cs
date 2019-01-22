// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*=============================================================================
**
**
**
** Purpose: Class for creating and managing a threadpool
**
**
=============================================================================*/

#pragma warning disable 0420

using Internal.Runtime.Augments;
using Internal.Runtime.CompilerServices;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Thread = Internal.Runtime.Augments.RuntimeThread;

namespace System.Threading
{
    internal sealed partial class ThreadPoolWorkQueue
    {
        internal static bool DispatchWithExceptionHandling()
        {
            try
            {
                return Dispatch();
            }
            catch (Exception e)
            {
                // Work items should not allow exceptions to escape.  For example, Task catches and stores any exceptions.
                Environment.FailFast("Unhandled exception in ThreadPool dispatch loop", e);
                return true; // Will never actually be executed because Environment.FailFast doesn't return
            }
        }
    }

    public static partial class ThreadPool
    {
        private static void EnsureInitialized()
        {
            ThreadPoolGlobals.threadPoolInitialized = true;
            ThreadPoolGlobals.enableWorkerTracking = false;
        }

        internal static void ReportThreadStatus(bool isWorking)
        {
        }

        unsafe private static void NativeOverlappedCallback(object obj)
        {
            NativeOverlapped* overlapped = (NativeOverlapped*)(IntPtr)obj;
            _IOCompletionCallback.PerformIOCompletionCallback(0, 0, overlapped);
        }

        [CLSCompliant(false)]
        unsafe public static bool UnsafeQueueNativeOverlapped(NativeOverlapped* overlapped)
        {
            // OS doesn't signal handle, so do it here (CoreCLR does this assignment in ThreadPoolNative::CorPostQueuedCompletionStatus)
            overlapped->InternalLow = (IntPtr)0;
            // Both types of callbacks are executed on the same thread pool
            return UnsafeQueueUserWorkItem(NativeOverlappedCallback, (IntPtr)overlapped);
        }

        [Obsolete("ThreadPool.BindHandle(IntPtr) has been deprecated.  Please use ThreadPool.BindHandle(SafeHandle) instead.", false)]
        public static bool BindHandle(IntPtr osHandle)
        {
            throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported); // Replaced by ThreadPoolBoundHandle.BindHandle
        }

        public static bool BindHandle(SafeHandle osHandle)
        {
            throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported); // Replaced by ThreadPoolBoundHandle.BindHandle
        }
    }
}
