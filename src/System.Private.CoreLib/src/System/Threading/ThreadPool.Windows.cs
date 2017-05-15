// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Runtime.Augments;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace System.Threading
{
    //
    // Win32-specific implementation of ThreadPool
    //
    public static partial class ThreadPool
    {
        private static IntPtr s_work;

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

        [NativeCallable(CallingConvention = CallingConvention.StdCall)]
        private static void LongRunningWorkCallback(IntPtr instance, IntPtr context)
        {
            RuntimeThread.InitializeThreadPoolThread();

            GCHandle gcHandle = GCHandle.FromIntPtr(context);
            Action callback = (Action)gcHandle.Target;
            gcHandle.Free();

            callback();
        }

        internal static unsafe void QueueLongRunningWork(Action callback)
        {
            var environ = default(Interop.mincore.TP_CALLBACK_ENVIRON);

            environ.Initialize();
            environ.SetLongFunction();

            IntPtr nativeCallback = AddrofIntrinsics.AddrOf<Interop.mincore.SimpleCallback>(LongRunningWorkCallback);

            GCHandle gcHandle = GCHandle.Alloc(callback);
            if (!Interop.mincore.TrySubmitThreadpoolCallback(nativeCallback, GCHandle.ToIntPtr(gcHandle), &environ))
            {
                gcHandle.Free();
                throw new OutOfMemoryException();
            }
        }
    }
}
