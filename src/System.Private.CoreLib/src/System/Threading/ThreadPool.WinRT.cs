// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Runtime.Augments;

namespace System.Threading
{
    //
    // WinRT-specific implementation of ThreadPool
    //
    internal static partial class ThreadPool
    {
        private static volatile bool s_dispatchCallbackSet;

        internal static void QueueDispatch()
        {
            if (!s_dispatchCallbackSet)
            {
                WinRTInterop.Callbacks.SetThreadpoolDispatchCallback(ThreadPoolWorkQueue.Dispatch);
                s_dispatchCallbackSet = true;
            }
            WinRTInterop.Callbacks.SubmitThreadpoolDispatchCallback();
        }

        internal static void QueueLongRunningWork(Action callback)
        {
            WinRTInterop.Callbacks.SubmitLongRunningThreadpoolWork(callback);
        }
    }
}
