// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Runtime.Augments;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace System.Threading
{
    //
    // Windows-specific implementation of ThreadPool
    //
    public static partial class ThreadPool
    {
        /// <summary>
        /// The maximum number of threads in the default thread pool on Windows 10 as computed by
        /// TppComputeDefaultMaxThreads(TppMaxGlobalPool).
        /// </summary>
        /// <remarks>
        /// Note that Windows 8 and 8.1 used a different value: Math.Max(4 * ThreadPoolGlobals.processorCount, 512).
        /// </remarks>
        private static readonly int MaxThreadCount = Math.Max(8 * ThreadPoolGlobals.processorCount, 768);

        private static IntPtr s_work;

        public static bool SetMaxThreads(int workerThreads, int completionPortThreads)
        {
            // Not supported at present
            return false;
        }

        public static void GetMaxThreads(out int workerThreads, out int completionPortThreads)
        {
            // Note that worker threads and completion port threads share the same thread pool.
            // The total number of threads cannot exceed MaxThreadCount.
            workerThreads = MaxThreadCount;
            completionPortThreads = MaxThreadCount;
        }

        public static bool SetMinThreads(int workerThreads, int completionPortThreads)
        {
            // Not supported at present
            return false;
        }

        public static void GetMinThreads(out int workerThreads, out int completionPortThreads)
        {
            workerThreads = 0;
            completionPortThreads = 0;
        }

        public static void GetAvailableThreads(out int workerThreads, out int completionPortThreads)
        {
            // Make sure we return a non-negative value if thread pool defaults are changed
            int availableThreads = Math.Max(MaxThreadCount - ThreadPoolGlobals.workQueue.numWorkingThreads, 0);

            workerThreads = availableThreads;
            completionPortThreads = availableThreads;
        }

        [NativeCallable(CallingConvention = CallingConvention.StdCall)]
        private static void DispatchCallback(IntPtr instance, IntPtr context, IntPtr work)
        {
            RuntimeThread.InitializeThreadPoolThread();
            Debug.Assert(s_work == work);
            ThreadPoolWorkQueue.Dispatch();
        }

        internal static void QueueDispatch()
        {
            if (s_work == IntPtr.Zero)
            {
                IntPtr nativeCallback = AddrofIntrinsics.AddrOf<Interop.mincore.WorkCallback>(DispatchCallback);

                IntPtr work = Interop.mincore.CreateThreadpoolWork(nativeCallback, IntPtr.Zero, IntPtr.Zero);
                if (work == IntPtr.Zero)
                    throw new OutOfMemoryException();

                if (Interlocked.CompareExchange(ref s_work, work, IntPtr.Zero) != IntPtr.Zero)
                    Interop.mincore.CloseThreadpoolWork(work);
            }

            Interop.mincore.SubmitThreadpoolWork(s_work);
        }
    }
}
