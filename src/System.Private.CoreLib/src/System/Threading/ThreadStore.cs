// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime;

namespace System.Threading
{
    internal static class ThreadStore
    {
        private static readonly ManualResetEvent _allDone = new ManualResetEvent(false);

        // main thread started by runtime and we have no chance (?) to increment it elsewhere
        private static int _foregroundRunningCount = 1;

        internal static void IncrementRunningForeground() 
        {
            Interlocked.Increment(ref _foregroundRunningCount);
        }

        internal static void DecrementRunningForeground() 
        {
            if (Interlocked.Decrement(ref _foregroundRunningCount) == 0) 
            {
                _allDone.Set();
            }
        }

        internal static void IncrementRunningForeground(Thread thread) 
        {
            if (!thread.IsBackground) 
            {
                IncrementRunningForeground();
            }
        }

        internal static void DecrementRunningForeground(Thread thread) 
        {
            if (!thread.IsBackground) 
            {
                DecrementRunningForeground();
            }
        }

        internal static void WaitForForegroundThreads() 
        {
            _allDone.WaitOne();
        }


        [RuntimeExport("RhpManagedShutdown")]
        public static void RhpManagedShutdown()
        {
            Thread.CurrentThread.IsBackground = true;
            ThreadStore.WaitForForegroundThreads();
        }
    }
}