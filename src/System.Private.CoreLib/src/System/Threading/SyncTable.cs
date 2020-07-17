// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Threading
{
    /// <summary>
    /// Stores the hash code and the Monitor synchronization object for all managed objects used
    /// with the Monitor.Enter/TryEnter/Exit methods.
    /// </summary>
    /// <remarks>
    /// This implementation is faster than ConditionalWeakTable for two reasons:
    /// 1) We store the synchronization entry index in the object header, which avoids a hash table
    ///    lookup.
    /// 2) We store a strong reference to the synchronization object, which allows retrieving it
    ///    much faster than going through a DependentHandle.
    ///
    /// SyncTable assigns a unique table entry to each object it is asked for.  The assigned entry
    /// index is stored in the object header and preserved during table expansion (we never shrink
    /// the table).  Each table entry contains a long weak GC handle representing the owner object
    /// of that entry and may be in one of the three states:
    /// 1) Free (IsAllocated == false).  These entries have been either never used or used and
    ///    freed/recycled after their owners died.  We keep a linked list of recycled entries and
    ///    use it to dispense entries to new objects.
    /// 2) Live (Target != null).  These entries store the hash code and the Monitor synchronization
    ///    object assigned to Target.
    /// 3) Dead (Target == null).  These entries lost their owners and are ready to be freed/recycled.
    ///
    /// Here is the state diagram for an entry:
    ///    Free --{AssignEntry}--> Live --{GC}--> Dead --{(Recycle|Free)DeadEntries} --> Free
    ///
    /// Dead entries are freed/recycled when there are no free entries available (in this case they
    /// are recycled and added to the free list) or periodically from the finalizer thread (in this
    /// case they are freed without adding to the free list).  That small difference in behavior
    /// allows using a more fine-grained locking when freeing is done on the finalizer thread.
    ///
    /// Thread safety is ensured by two locks: s_freeEntriesLock and s_usedEntriesLock.  The former
    /// protects everything related to free entries: s_freeEntryList, s_unusedEntryIndex, and the
    /// content of free entries in the s_entries array.  The latter protects the content of used
    /// entries in the s_entries array.  Growing the table and updating the s_entries reference is
    /// protected by both locks.  Having two locks is not required for correctness, they may be
    /// merged into a single coarser lock.
    ///
    /// The public methods operates on live entries only and acquire the following locks:
    /// * GetLockObject : Lock-free.  We always allocate a Monitor synchronization object before
    ///                   the entry goes live.  The returned object may be used as normal; no
    ///                   additional synchronization required.
    /// * GetHashCode   : Lock-free.  A stale zero value may be returned.
    /// * SetHashCode   : Acquires s_usedEntriesLock.
    /// * AssignEntry   : Acquires s_freeEntriesLock if at least one free entry is available;
    ///                   otherwise also acquires s_usedEntriesLock to recycle dead entries
    ///                   and/or grow the table.
    ///
    /// The important part here is that all read operations are lock-free and fast, and write
    /// operations are expected to be much less frequent than read ones.
    ///
    /// One possible future optimization is recycling Monitor synchronization objects from dead
    /// entries.
    /// </remarks>
    [EagerStaticClassConstruction]
    internal static class SyncTable
    {
        /// <summary>
        /// The initial size of the table.  Must be positive and not greater than
        /// ObjectHeader.MASK_HASHCODE_INDEX + 1.
        /// </summary>
        /// <remarks>
        /// CLR uses 250 as the initial table size.  In contrast to CoreRT, CLR creates sync
        /// entries less frequently since uncontended Monitor synchronization employs a thin lock
        /// stored in the object header.
        /// </remarks>
#if DEBUG
        // Exercise table expansion more frequently in debug builds
        private const int InitialSize = 1;
#else
        private const int InitialSize = 1 << 7;
#endif

        /// <summary>
        /// The table size threshold for doubling in size.  Must be positive.
        /// </summary>
        private const int DoublingSizeThreshold = 1 << 20;

        /// <summary>
        /// Protects everything related to free entries: s_freeEntryList, s_unusedEntryIndex, and the
        /// content of free entries in the s_entries array.  Also protects growing the table.
        /// </summary>
        internal static Lock s_freeEntriesLock = new Lock();

        /// <summary>
        /// The dynamically growing array of sync entries.
        /// </summary>
        private static Entry[] s_entries = new Entry[InitialSize];

        /// <summary>
        /// The head of the list of freed entries linked using the Next property.
        /// </summary>
        private static int s_freeEntryList = 0;

        /// <summary>
        /// The index of the lowest never used entry.  We skip the 0th entry and start with 1.
        /// If all entries have been used, s_unusedEntryIndex == s_entries.Length.  This counter
        /// never decreases.
        /// </summary>
        private static int s_unusedEntryIndex = 1;

        /// <summary>
        /// Protects the content of used entries in the s_entries array.  Also protects growing
        /// the table.
        /// </summary>
        private static Lock s_usedEntriesLock = new Lock();

        /// <summary>
        /// Creates the initial array of entries and the dead entries collector.
        /// </summary>
        static SyncTable()
        {
            // Create only one collector instance and do not store any references to it, so it may
            // be finalized.  Use GC.KeepAlive to ensure the allocation will not be optimized out.
            GC.KeepAlive(new DeadEntryCollector());
        }

        /// <summary>
        /// Assigns a sync table entry to the object in a thread-safe way.
        /// </summary>
        public static unsafe int AssignEntry(object obj, int* pHeader)
        {
            // Allocate the synchronization object outside the lock
            Lock lck = new Lock();

            using (LockHolder.Hold(s_freeEntriesLock))
            {
                // After acquiring the lock check whether another thread already assigned the sync entry
                int hashOrIndex;
                if (ObjectHeader.GetSyncEntryIndex(*pHeader, out hashOrIndex))
                {
                    return hashOrIndex;
                }

                // Allocate a new sync entry.  First, make sure all data is ready.  This call may OOM.
                GCHandle owner = GCHandle.Alloc(obj, GCHandleType.WeakTrackResurrection);

                try
                {
                    // Now find a free entry in the table
                    int syncIndex;

                    if (s_freeEntryList != 0)
                    {
                        // Grab a free entry from the list
                        syncIndex = s_freeEntryList;
                        s_freeEntryList = s_entries[syncIndex].Next;
                        s_entries[syncIndex].Next = 0;
                    }
                    else if (s_unusedEntryIndex < s_entries.Length)
                    {
                        // Grab the next unused entry
                        syncIndex = s_unusedEntryIndex++;
                    }
                    else
                    {
                        // No free entries, use the slow path.  This call may OOM.
                        syncIndex = EnsureFreeEntry();
                    }

                    // Found a free entry to assign
                    Debug.Assert(!s_entries[syncIndex].Owner.IsAllocated);
                    Debug.Assert(s_entries[syncIndex].Lock == null);
                    Debug.Assert(s_entries[syncIndex].HashCode == 0);

                    // Set up the new entry.  We should not fail after this point.
                    s_entries[syncIndex].Lock = lck;
                    // The hash code will be set by the SetSyncEntryIndex call below
                    s_entries[syncIndex].Owner = owner;
                    owner = default(GCHandle);

                    // Finally, store the entry index in the object header
                    ObjectHeader.SetSyncEntryIndex(pHeader, syncIndex);
                    return syncIndex;
                }
                finally
                {
                    if (owner.IsAllocated)
                    {
                        owner.Free();
                    }
                }
            }
        }

        /// <summary>
        /// Creates a free entry by either freeing dead entries or growing the sync table.
        /// This method either returns an index of a free entry or throws an OOM exception
        /// keeping the state valid.
        /// </summary>
        private static int EnsureFreeEntry()
        {
            Debug.Assert(s_freeEntriesLock.IsAcquired);
            Debug.Assert((s_freeEntryList == 0) && (s_unusedEntryIndex == s_entries.Length));

            int syncIndex;

            // Scan for dead and freed entries and put them into s_freeEntryList
            int recycledEntries = RecycleDeadEntries();
            if (s_freeEntryList != 0)
            {
                // If the table is almost full (less than 1/8 of free entries), try growing it
                // to avoid frequent RecycleDeadEntries scans, which may degrade performance.
                if (recycledEntries < (s_entries.Length >> 3))
                {
                    try
                    {
                        Grow();
                    }
                    catch (OutOfMemoryException)
                    {
                        // Since we still have free entries, ignore memory shortage
                    }
                }
                syncIndex = s_freeEntryList;
                s_freeEntryList = s_entries[syncIndex].Next;
                s_entries[syncIndex].Next = 0;
            }
            else
            {
                // No entries were recycled; must grow the table.
                // This call may throw OOM; keep the state valid.
                Grow();
                Debug.Assert(s_unusedEntryIndex < s_entries.Length);
                syncIndex = s_unusedEntryIndex++;
            }
            return syncIndex;
        }

        /// <summary>
        /// Scans the table and recycles all dead and freed entries adding them to the free entry
        /// list.  Returns the number of recycled entries.
        /// </summary>
        private static int RecycleDeadEntries()
        {
            Debug.Assert(s_freeEntriesLock.IsAcquired);

            using (LockHolder.Hold(s_usedEntriesLock))
            {
                int recycledEntries = 0;
                for (int idx = s_unusedEntryIndex; --idx > 0;)
                {
                    bool freed = !s_entries[idx].Owner.IsAllocated;
                    if (freed || (s_entries[idx].Owner.Target == null))
                    {
                        s_entries[idx].Lock = null;
                        s_entries[idx].Next = s_freeEntryList;
                        if (!freed)
                        {
                            s_entries[idx].Owner.Free();
                        }
                        s_freeEntryList = idx;
                        recycledEntries++;
                    }
                }
                return recycledEntries;
            }
        }

        /// <summary>
        /// Scans the table and frees all dead entries without adding them to the free entry list.
        /// Runs on the finalizer thread.
        /// </summary>
        private static void FreeDeadEntries()
        {
            // Be cautious as this method may run in parallel with grabbing a free entry in the
            // AssignEntry method.  The potential race is checking IsAllocated && (Target == null)
            // while a new non-zero (allocated) GCHandle is being assigned to the Owner field
            // containing a zero (non-allocated) GCHandle.  That must be safe as a GCHandle is
            // just an IntPtr, which is assigned atomically, and Target has load dependency on it.
            using (LockHolder.Hold(s_usedEntriesLock))
            {
                // We do not care if the s_unusedEntryIndex value is stale here; it suffices that
                // the s_entries reference is locked and s_unusedEntryIndex points within that array.
                Debug.Assert(s_unusedEntryIndex <= s_entries.Length);

                for (int idx = s_unusedEntryIndex; --idx > 0;)
                {
                    bool allocated = s_entries[idx].Owner.IsAllocated;
                    if (allocated && (s_entries[idx].Owner.Target == null))
                    {
                        s_entries[idx].Lock = null;
                        s_entries[idx].Next = 0;
                        s_entries[idx].Owner.Free();
                    }
                }
            }
        }

        /// <summary>
        /// Grows the sync table.  If memory is not available, it throws an OOM exception keeping
        /// the state valid.
        /// </summary>
        private static void Grow()
        {
            Debug.Assert(s_freeEntriesLock.IsAcquired);

            int oldSize = s_entries.Length;
            int newSize = CalculateNewSize(oldSize);
            Entry[] newEntries = new Entry[newSize];

            using (LockHolder.Hold(s_usedEntriesLock))
            {
                // Copy the shallow content of the table
                Array.Copy(s_entries, newEntries, oldSize);

                // Publish the new table.  Lock-free reader threads must not see the new value of
                // s_entries until all the content is copied to the new table.
                Volatile.Write(ref s_entries, newEntries);
            }
        }

        /// <summary>
        /// Calculates the new size of the sync table if it needs to grow.  Throws an OOM exception
        /// in case of size overflow.
        /// </summary>
        private static int CalculateNewSize(int oldSize)
        {
            Debug.Assert(oldSize > 0);
            Debug.Assert(ObjectHeader.MASK_HASHCODE_INDEX < int.MaxValue);
            int newSize;

            if (oldSize <= DoublingSizeThreshold)
            {
                // Double in size; overflow is checked below
                newSize = unchecked(oldSize * 2);
            }
            else
            {
                // For bigger tables use a smaller factor 1.5
                Debug.Assert(oldSize > 1);
                newSize = unchecked(oldSize + (oldSize >> 1));
            }

            // All indices must fit in the mask, limit the size accordingly
            newSize = Math.Min(newSize, ObjectHeader.MASK_HASHCODE_INDEX + 1);

            // Make sure the new size has not overflowed and is actually bigger
            if (newSize <= oldSize)
            {
                throw new OutOfMemoryException();
            }

            return newSize;
        }

        /// <summary>
        /// Returns the stored hash code.  The zero value indicates the hash code has not yet been
        /// assigned or visible to this thread.
        /// </summary>
        public static int GetHashCode(int syncIndex)
        {
            // This thread may be looking at an old version of s_entries.  If the old version had
            // no hash code stored, GetHashCode returns zero and the subsequent SetHashCode call
            // will resolve the potential race.
            return s_entries[syncIndex].HashCode;
        }

        /// <summary>
        /// Sets the hash code in a thread-safe way.
        /// </summary>
        public static int SetHashCode(int syncIndex, int hashCode)
        {
            Debug.Assert((0 < syncIndex) && (syncIndex < s_unusedEntryIndex));

            // Acquire the lock to ensure we are updating the latest version of s_entries.  This
            // lock may be avoided if we store the hash code and Monitor synchronization data in
            // the same object accessed by a reference.
            using (LockHolder.Hold(s_usedEntriesLock))
            {
                int currentHash = s_entries[syncIndex].HashCode;
                if (currentHash != 0)
                {
                    return currentHash;
                }
                s_entries[syncIndex].HashCode = hashCode;
                return hashCode;
            }
        }

        /// <summary>
        /// Sets the hash code assuming the caller holds s_freeEntriesLock.  Use for not yet
        /// published entries only.
        /// </summary>
        public static void MoveHashCodeToNewEntry(int syncIndex, int hashCode)
        {
            Debug.Assert(s_freeEntriesLock.IsAcquired);
            Debug.Assert((0 < syncIndex) && (syncIndex < s_unusedEntryIndex));
            s_entries[syncIndex].HashCode = hashCode;
        }

        /// <summary>
        /// Returns the Monitor synchronization object.  The return value is never null.
        /// </summary>
        public static Lock GetLockObject(int syncIndex)
        {
            // Note that we do not take a lock here.  When we replace s_entries, we preserve all
            // indices and Lock references.
            return s_entries[syncIndex].Lock;
        }

        /// <summary>
        /// Periodically scans the SyncTable and frees dead entries.  It runs on the finalizer
        /// thread roughly every full (i.e. generation 2) garbage collection.
        /// </summary>
        private sealed class DeadEntryCollector
        {
            ~DeadEntryCollector()
            {
                if (!Environment.HasShutdownStarted)
                {
                    SyncTable.FreeDeadEntries();
                    // Resurrect itself by re-registering for finalization
                    GC.ReRegisterForFinalize(this);
                }
            }
        }

        /// <summary>
        /// Stores the Monitor synchronization object and the hash code for an arbitrary object.
        /// </summary>
        private struct Entry
        {
            /// <summary>
            /// The Monitor synchronization object.
            /// </summary>
            public Lock Lock;

            /// <summary>
            /// Contains either the hash code or the index of the next freed entry.
            /// </summary>
            private int _hashOrNext;

            /// <summary>
            /// The long weak GC handle representing the owner object of this sync entry.
            /// </summary>
            public GCHandle Owner;

            /// <summary>
            /// For entries in use, this property gets or sets the hash code of the owner object.
            /// The zero value indicates the hash code has not yet been assigned.
            /// </summary>
            public int HashCode
            {
                get { return _hashOrNext; }
                set { _hashOrNext = value; }
            }

            /// <summary>
            /// For freed entries, this property gets or sets the index of the next freed entry.
            /// The zero value indicates the end of the list.
            /// </summary>
            public int Next
            {
                get { return _hashOrNext; }
                set { _hashOrNext = value; }
            }
        }
    }
}
