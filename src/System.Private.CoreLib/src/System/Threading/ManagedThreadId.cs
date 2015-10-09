// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// =+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
//
// Thread tracks managed thread IDs, recycling them when threads die to keep the set of
// live IDs compact.
//
// =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-

using System;
using System.Diagnostics.Contracts;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security;

#pragma warning disable 0420


namespace System.Threading
{
    internal class ManagedThreadId
    {
        [ThreadStatic]
        private static int t_currentManagedThreadId;

        public static int Current
        {
            get
            {
                int currentManagedThreadId = t_currentManagedThreadId;
                if (currentManagedThreadId == Environment.ManagedThreadIdNone)
                    return MakeForCurrentThread();
                else
                    return currentManagedThreadId;
            }
        }

        private static volatile ManagedThreadId s_list;
        private static int s_nextThreadId;

        private readonly int _managedThreadId; // The ID represented by this object.
        private volatile int _lock;            // 1 if a thread is trying to recycle this ID; 0 otherwise.
        private IntPtr _nativeThreadHandle;    // A handle to the thread that currently owns this ID; the thread may be dead.
        private ManagedThreadId _next;

        public ManagedThreadId(int id)
        {
            _managedThreadId = id;
        }


        private static int MakeForCurrentThread()
        {
            //
            // Get the current thread handle.  We need to use DuplicateHandle, because GetCurrentThread returns a pseudo-handle
            // that cannot be used outside of this thread.
            //
            IntPtr thisNativeThreadHandle;
            Interop.mincore.DuplicateHandle(
                Interop.mincore.GetCurrentProcess(),
                Interop.mincore.GetCurrentThread(),
                Interop.mincore.GetCurrentProcess(),
                out thisNativeThreadHandle,
                0,
                false,
                (uint)Interop.Constants.DuplicateSameAccess);

            //
            // First, search for a dead thread, so we can reuse its thread ID
            //
            for (ManagedThreadId current = s_list; current != null; current = current._next)
            {
                //
                // Try to take the lock on this ID.  If another thread already has it, just move on to the next ID.
                //
                if (Interlocked.Exchange(ref current._lock, 1) != 0)
                    continue;

                try
                {
                    //
                    // Does the ID currently belong to a dead thread?
                    //
                    if (LowLevelThread.WaitForSingleObject(current._nativeThreadHandle, 0) == (uint)Interop.Constants.WaitObject0)
                    {
                        //
                        // The thread is dead.  We can claim this ID by swapping in our own thread handle.
                        //
                        Interop.mincore.CloseHandle(current._nativeThreadHandle);
                        current._nativeThreadHandle = thisNativeThreadHandle;

                        t_currentManagedThreadId = current._managedThreadId;
                        return current._managedThreadId;
                    }
                }
                finally
                {
                    //
                    // Release the lock.
                    //
                    current._lock = 0;
                }
            }

            //
            // We couldn't find a dead thread, so we can't reuse a thread ID.  Create a new one.
            //
            ManagedThreadId newManagedThreadId = new ManagedThreadId(Interlocked.Increment(ref s_nextThreadId));
            newManagedThreadId._nativeThreadHandle = thisNativeThreadHandle;
            while (true)
            {
                ManagedThreadId oldList = s_list;
                newManagedThreadId._next = oldList;
                if (Interlocked.CompareExchange(ref s_list, newManagedThreadId, oldList) == oldList)
                {
                    t_currentManagedThreadId = newManagedThreadId._managedThreadId;
                    return newManagedThreadId._managedThreadId;
                }
            }
        }
    }
}
