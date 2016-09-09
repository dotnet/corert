// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Threading
{
    //
    // Unix-specific implementation of ThreadPool
    //
    internal static partial class ThreadPool
    {
        internal static void QueueDispatch()
        {
            // UNIXTODO: Threadpool
            throw new NotImplementedException();
        }

        internal static void QueueLongRunningWork(Action callback)
        {
            // UNIXTODO: Threadpool
            throw new NotImplementedException();
        }
    }
}
