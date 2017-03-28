// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Runtime.Augments;
using System.Runtime.InteropServices;

namespace System.Threading
{
    //
    // Unix-specific implementation of ThreadPool
    //
    internal static partial class ThreadPool
    {
        // TODO: this is a very primitive (temporary) implementation of Thread Pool to allow Tasks to be
        // used on Unix. All of this code must be replaced with proper implementation.

        /// <summary>
        /// Max allowed number of thread in the thread pool. This is just arbitrary number
        /// that is used to prevent unbound creation of threads.
        /// It should by high enough to provide sufficient number of thread pool workers
        /// in case if some threads get blocked while running user code.
        /// </summary>
        private static readonly int MaxThreadCount = 4 * ThreadPoolGlobals.processorCount;

        /// <summary>
        /// Semaphore that is used to release waiting thread pool workers when new work becomes available.
        /// </summary>
        private static SemaphoreSlim s_semaphore = new SemaphoreSlim(0);

        /// <summary>
        /// Number of worker threads created by the thread pool.
        /// </summary>
        private static volatile int s_workerCount = 0;

        /// <summary>
        /// This method is called to request a new thread pool worker to handle pending work.
        /// </summary>
        internal static void QueueDispatch()
        {
            // For simplicity of the state management, we pre-create all thread pool workers on the first
            // request and then use the semaphore to release threads as new requests come in.
            if ((s_workerCount == 0) && Interlocked.Exchange(ref s_workerCount, MaxThreadCount) == 0)
            {
                for (int i = 0; i < MaxThreadCount; i++)
                {
                    if (!Interop.Sys.RuntimeThread_CreateThread(IntPtr.Zero /*use default stack size*/,
                        AddrofIntrinsics.AddrOf<Interop.Sys.ThreadProc>(ThreadPoolDispatchCallback), IntPtr.Zero))
                    {
                        throw new OutOfMemoryException();
                    }
                }
            }

            // Release one thread to handle the new request
            s_semaphore.Release(1);
        }

        internal static void QueueLongRunningWork(Action callback)
        {
            GCHandle gcHandle = GCHandle.Alloc(callback);

            if (!Interop.Sys.RuntimeThread_CreateThread(IntPtr.Zero /*use default stack size*/,
                AddrofIntrinsics.AddrOf<Interop.Sys.ThreadProc>(LongRunningWorkCallback), GCHandle.ToIntPtr(gcHandle)))
            {
                gcHandle.Free();
                throw new OutOfMemoryException();
            }
        }

        /// <summary>
        /// This method is an entry point of a thread pool worker thread.
        /// </summary>
        [NativeCallable]
        private static IntPtr ThreadPoolDispatchCallback(IntPtr context)
        {
            RuntimeThread.InitializeThreadPoolThread();

            do
            {
                // Handle pending requests
                ThreadPoolWorkQueue.Dispatch();

                // Wait for new requests to arrive
                s_semaphore.Wait();

            } while (true);
        }

        [NativeCallable]
        private static IntPtr LongRunningWorkCallback(IntPtr context)
        {
            RuntimeThread.InitializeThreadPoolThread();

            GCHandle gcHandle = GCHandle.FromIntPtr(context);
            Action callback = (Action)gcHandle.Target;
            gcHandle.Free();

            callback();
            return IntPtr.Zero;
        }

    }
}
