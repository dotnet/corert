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

namespace System.Threading
{
    internal class ManagedThreadId
    {
        [ThreadStatic]
        private static ManagedThreadId t_currentThreadId;
        [ThreadStatic]
        private static int t_currentManagedThreadId;

        // We have to avoid the static constructors on the ManagedThreadId class, otherwise we can run into stack overflow as first time Current property get called, 
        // the runtime will ensure running the static constructor and this process will call the Current property again (when taking any lock) 
        //      System::Environment.get_CurrentManagedThreadId
        //      System::Threading::Lock.Acquire
        //      System::Runtime::CompilerServices::ClassConstructorRunner::Cctor.GetCctor
        //      System::Runtime::CompilerServices::ClassConstructorRunner.EnsureClassConstructorRun
        //      System::Threading::ManagedThreadId.get_Current
        //      System::Environment.get_CurrentManagedThreadId

        private static ManagedThreadId s_recycledIdsList;
        private static int s_maxUsedThreadId;

        private int _managedThreadId;
        private ManagedThreadId _next; // Linked list of recycled ids

        internal const int ManagedThreadIdNone = 0;

        public static int Current
        {
            get
            {
                int currentManagedThreadId = t_currentManagedThreadId;
                if (currentManagedThreadId == ManagedThreadIdNone)
                    return MakeForCurrentThread();
                else
                    return currentManagedThreadId;
            }
        }

        private static int MakeForCurrentThread()
        {
            ManagedThreadId newManagedThreadId = null;

            // Try to pop a recycled id from the list
            for (;;)
            {
                var recycledId = Volatile.Read(ref s_recycledIdsList);
                if (recycledId == null)
                    break;

                if (Interlocked.CompareExchange(ref s_recycledIdsList, recycledId._next, recycledId) == recycledId)
                {
                    GC.ReRegisterForFinalize(recycledId);
                    newManagedThreadId = recycledId;
                    break;
                }
            }

            if (newManagedThreadId == null)
            {
                newManagedThreadId = new ManagedThreadId(Interlocked.Increment(ref s_maxUsedThreadId));
            }

            t_currentThreadId = newManagedThreadId;
            t_currentManagedThreadId = newManagedThreadId._managedThreadId;
            return t_currentManagedThreadId;
        }

        private ManagedThreadId(int managedThreadId)
        {
            _managedThreadId = managedThreadId;
        }

        ~ManagedThreadId()
        {
            if (_managedThreadId == ManagedThreadIdNone) // already finalized
            {
                return;
            }

            // Push the recycled id into the list of recycled ids
            for (;;)
            {
                var previousRecycledId = Volatile.Read(ref s_recycledIdsList);

                _next = previousRecycledId;

                if (Interlocked.CompareExchange(ref s_recycledIdsList, this, previousRecycledId) == previousRecycledId)
                {
                    break;
                }
            }
        }
    }
}
