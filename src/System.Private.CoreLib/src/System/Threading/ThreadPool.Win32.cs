// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Runtime.Augments;

namespace System.Threading
{
    //
    // Win32-specific implementation of ThreadPool
    //
    internal static partial class ThreadPool
    {
        internal static void QueueDispatch()
        {
            throw new NotImplementedException();
        }

        internal static void QueueLongRunningWork(Action callback)
        {
            throw new NotImplementedException();
        }
    }
}
