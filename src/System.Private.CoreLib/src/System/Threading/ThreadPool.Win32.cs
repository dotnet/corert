// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace System.Threading
{
    //
    // Win32-specific implementation of ThreadPool
    //
    internal static partial class ThreadPool
    {
        static IntPtr s_work;

        [NativeCallable(CallingConvention = CallingConvention.StdCall)]
        private static void DispatchCallback(IntPtr instance, IntPtr context, IntPtr work)
        {
            Debug.Assert(s_work == work);
            ThreadPoolWorkQueue.Dispatch();
        }

        internal static void QueueDispatch()
        {
            if (s_work == IntPtr.Zero)
            {
                IntPtr nativeCallback = AddrofIntrinsics.AddrOf<Action<IntPtr, IntPtr, IntPtr>>(DispatchCallback);

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
            GCHandle gcHandle = GCHandle.FromIntPtr(context);
            Action callback = (Action)gcHandle.Target;
            gcHandle.Free();

            callback();
        }

        internal unsafe static void QueueLongRunningWork(Action callback)
        {
            var environ = default(Interop.mincore.TP_CALLBACK_ENVIRON);

            environ.Initialize();
            environ.SetLongFunction();

            IntPtr nativeCallback = AddrofIntrinsics.AddrOf<Action<IntPtr, IntPtr>>(LongRunningWorkCallback);

            GCHandle gcHandle = GCHandle.Alloc(callback);
            if (!Interop.mincore.TrySubmitThreadpoolCallback(nativeCallback, GCHandle.ToIntPtr(gcHandle), &environ))
            {
                gcHandle.Free();
                throw new OutOfMemoryException();
            }
        }
    }
}
