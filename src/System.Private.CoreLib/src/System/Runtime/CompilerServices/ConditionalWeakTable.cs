// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;
using System.Threading;

namespace System.Runtime.CompilerServices
{
    #region ConditionalWeakTable
    [ComVisible(false)]
    public sealed class ConditionalWeakTable<TKey, TValue>
        where TKey : class
        where TValue : class
    {
        #region Constructors
        public ConditionalWeakTable()
        {
            _container = new Container();
            _lock = new Lock();
        }
        #endregion

        #region Public Members
        //--------------------------------------------------------------------------------------------
        // key:   key of the value to find. Cannot be null.
        // value: if the key is found, contains the value associated with the key upon method return.
        //        if the key is not found, contains default(TValue).
        //
        // Method returns "true" if key was found, "false" otherwise.
        //
        // Note: The key may get garbaged collected during the TryGetValue operation. If so, TryGetValue
        // may at its discretion, return "false" and set "value" to the default (as if the key was not present.)
        //--------------------------------------------------------------------------------------------
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }

            return _container.TryGetValueWorker(key, out value);
        }

        //--------------------------------------------------------------------------------------------
        // key: key to add. May not be null.
        // value: value to associate with key.
        //
        // If the key is already entered into the dictionary, this method throws an exception.
        //
        // Note: The key may get garbage collected during the Add() operation. If so, Add()
        // has the right to consider any prior entries successfully removed and add a new entry without
        // throwing an exception.
        //--------------------------------------------------------------------------------------------
        public void Add(TKey key, TValue value)
        {
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }

            using (LockHolder.Hold(_lock))
            {
                object otherValue;
                int entryIndex = _container.FindEntry(key, out otherValue);
                if (entryIndex != -1)
                {
                    throw new ArgumentException(SR.Format(SR.Argument_AddingDuplicate, key));
                }

                CreateEntry(key, value);
            }
        }

        //--------------------------------------------------------------------------------------------
        // key: key to remove. May not be null.
        //
        // Returns true if the key is found and removed. Returns false if the key was not in the dictionary.
        //
        // Note: The key may get garbage collected during the Remove() operation. If so,
        // Remove() will not fail or throw, however, the return value can be either true or false
        // depending on who wins the race.
        //--------------------------------------------------------------------------------------------
        public bool Remove(TKey key)
        {
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }

            using (LockHolder.Hold(_lock))
            {
                return _container.Remove(key);
            }
        }


        //--------------------------------------------------------------------------------------------
        // key:                 key of the value to find. Cannot be null.
        // createValueCallback: callback that creates value for key. Cannot be null.
        //
        // Atomically tests if key exists in table. If so, returns corresponding value. If not,
        // invokes createValueCallback() passing it the key. The returned value is bound to the key in the table
        // and returned as the result of GetValue().
        //
        // If multiple threads race to initialize the same key, the table may invoke createValueCallback
        // multiple times with the same key. Exactly one of these calls will "win the race" and the returned
        // value of that call will be the one added to the table and returned by all the racing GetValue() calls.
        // 
        // This rule permits the table to invoke createValueCallback outside the internal table lock
        // to prevent deadlocks.
        //--------------------------------------------------------------------------------------------
        public TValue GetValue(TKey key, CreateValueCallback createValueCallback)
        {
            // Our call to TryGetValue() validates key so no need for us to.
            //
            //  if (key == null)
            //  {
            //      ThrowHelper.ThrowArgumentNullException(ExceptionArgument.key);
            //  }

            if (createValueCallback == null)
            {
                throw new ArgumentNullException("createValueCallback");
            }

            TValue existingValue;
            if (TryGetValue(key, out existingValue))
            {
                return existingValue;
            }

            return GetValueLocked(key, createValueCallback);
        }

        private TValue GetValueLocked(TKey key, CreateValueCallback createValueCallback)
        {
            // If we got here, the key was not in the table. Invoke the callback (outside the lock)
            // to generate the new value for the key. 
            TValue newValue = createValueCallback(key);

            using (LockHolder.Hold(_lock))
            {
                // Now that we've taken the lock, must recheck in case we lost a race to add the key.
                TValue existingValue;
                if (_container.TryGetValueWorker(key, out existingValue))
                {
                    return existingValue;
                }
                else
                {
                    // Verified in-lock that we won the race to add the key. Add it now.
                    CreateEntry(key, newValue);
                    return newValue;
                }
            }
        }

        //--------------------------------------------------------------------------------------------
        // key:                 key of the value to find. Cannot be null.
        //
        // Helper method to call GetValue without passing a creation delegate.  Uses Activator.CreateInstance
        // to create new instances as needed.  If TValue does not have a default constructor, this will
        // throw.
        //--------------------------------------------------------------------------------------------

        public TValue GetOrCreateValue(TKey key)
        {
            return GetValue(key, k => Activator.CreateInstance<TValue>());
        }

        public delegate TValue CreateValueCallback(TKey key);

        #endregion

        #region internal members

        //--------------------------------------------------------------------------------------------
        // Find a key that equals (value equality) with the given key - don't use in perf critical path
        // Note that it calls out to Object.Equals which may calls the override version of Equals
        // and that may take locks and leads to deadlock
        // Currently it is only used by WinRT event code and you should only use this function
        // if you know for sure that either you won't run into dead locks or you need to live with the
        // possiblity
        //--------------------------------------------------------------------------------------------
        internal TKey FindEquivalentKeyUnsafe(TKey key, out TValue value)
        {
            using (LockHolder.Hold(_lock))
            {
                return _container.FindEquivalentKeyUnsafe(key, out value);
            }
        }

        //--------------------------------------------------------------------------------------------
        // Returns a collection of keys - don't use in perf critical path
        //--------------------------------------------------------------------------------------------
        internal ICollection<TKey> Keys
        {
            get
            {
                using (LockHolder.Hold(_lock))
                {
                    return _container.Keys;
                }
            }
        }

        //--------------------------------------------------------------------------------------------
        // Returns a collection of values - don't use in perf critical path
        //--------------------------------------------------------------------------------------------
        internal ICollection<TValue> Values
        {
            get
            {
                using (LockHolder.Hold(_lock))
                {
                    return _container.Values;
                }
            }
        }

        //--------------------------------------------------------------------------------------------
        // Clear all the key/value pairs
        //--------------------------------------------------------------------------------------------
        internal void Clear()
        {
            using (LockHolder.Hold(_lock))
            {
                _container = new Container();
            }
        }

        #endregion

        #region Private Members

        //----------------------------------------------------------------------------------------
        // Worker for adding a new key/value pair.
        // Will resize the container if it is full
        //
        // Preconditions:
        //     Must hold _lock.
        //     Key already validated as non-null and not already in table.
        //----------------------------------------------------------------------------------------
        private void CreateEntry(TKey key, TValue value)
        {
            Contract.Assert(_lock.IsAcquired);

            Container c = _container;
            if (!c.HasCapacity)
            {
                c = _container = c.Resize();
            }
            c.CreateEntryNoResize(key, value);
        }

        private static bool IsPowerOfTwo(int value)
        {
            return (value > 0) && ((value & (value - 1)) == 0);
        }

        #endregion

        #region Private Data Members
        //--------------------------------------------------------------------------------------------
        // Entry can be in one of four states:
        //
        //    - Unused (stored with an index _firstFreeEntry and above)
        //         depHnd.IsAllocated == false
        //         hashCode == <dontcare>
        //         next == <dontcare>)
        //
        //    - Used with live key (linked into a bucket list where _buckets[hashCode & (_buckets.Length - 1)] points to first entry)
        //         depHnd.IsAllocated == true, depHnd.GetPrimary() != null
        //         hashCode == RuntimeHelpers.GetHashCode(depHnd.GetPrimary()) & Int32.MaxValue
        //         next links to next Entry in bucket. 
        //                          
        //    - Used with dead key (linked into a bucket list where _buckets[hashCode & (_buckets.Length - 1)] points to first entry)
        //         depHnd.IsAllocated == true, depHnd.GetPrimary() == null
        //         hashCode == <notcare> 
        //         next links to next Entry in bucket. 
        //
        //    - Has been removed from the table (by a call to Remove)
        //         depHnd.IsAllocated == true, depHnd.GetPrimary() == <notcare>
        //         hashCode == -1 
        //         next links to next Entry in bucket. 
        //
        // The only difference between "used with live key" and "used with dead key" is that
        // depHnd.GetPrimary() returns null. The transition from "used with live key" to "used with dead key"
        // happens asynchronously as a result of normal garbage collection. The dictionary itself
        // receives no notification when this happens.
        //
        // When the dictionary grows the _entries table, it scours it for expired keys and does not
        // add those to the new container.
        //--------------------------------------------------------------------------------------------
        private struct Entry
        {
            public DependentHandle depHnd;      // Holds key and value using a weak reference for the key and a strong reference
                                                // for the value that is traversed only if the key is reachable without going through the value.
            public int hashCode;    // Cached copy of key's hashcode
            public int next;        // Index of next entry, -1 if last
        }

        //
        // Container holds the actual data for the table.  A given instance of Container always has the same capacity.  When we need
        // more capacity, we create a new Container, copy the old one into the new one, and discard the old one.  This helps enable lock-free
        // reads from the table, as readers never need to deal with motion of entries due to rehashing.
        //
        private class Container
        {
            internal Container()
            {
                Contract.Assert(IsPowerOfTwo(InitialCapacity));
                int size = InitialCapacity;
                _buckets = new int[size];
                for (int i = 0; i < _buckets.Length; i++)
                {
                    _buckets[i] = -1;
                }
                _entries = new Entry[size];
            }
            
            private Container(int[] buckets, Entry[] entries, int firstFreeEntry)
            {
                _buckets = buckets;
                _entries = entries;
                _firstFreeEntry = firstFreeEntry;
            }

            internal bool HasCapacity
            {
                get
                {
                    return _firstFreeEntry < _entries.Length;
                }
            }

            //----------------------------------------------------------------------------------------
            // Worker for adding a new key/value pair.
            // Preconditions:
            //     Container must NOT be full
            //----------------------------------------------------------------------------------------
            internal void CreateEntryNoResize(TKey key, TValue value)
            {
                Contract.Assert(HasCapacity);

                VerifyIntegrity();
                _invalid = true;

                int hashCode = RuntimeHelpers.GetHashCode(key) & Int32.MaxValue;
                int newEntry = _firstFreeEntry++;

                _entries[newEntry].hashCode = hashCode;
                _entries[newEntry].depHnd = new DependentHandle(key, value);
                int bucket = hashCode & (_buckets.Length - 1);
                _entries[newEntry].next = _buckets[bucket];

                // This write must be volatile, as we may be racing with concurrent readers.  If they see
                // the new entry, they must also see all of the writes earlier in this method.
                Volatile.Write(ref _buckets[bucket], newEntry);

                _invalid = false;
            }

            //----------------------------------------------------------------------------------------
            // Worker for finding a key/value pair
            //
            // Preconditions:
            //     Must hold _lock.
            //     Key already validated as non-null
            //----------------------------------------------------------------------------------------
            internal bool TryGetValueWorker(TKey key, out TValue value)
            {
                object secondary;
                int entryIndex = FindEntry(key, out secondary);
                value = RuntimeHelpers.UncheckedCast<TValue>(secondary);
                return (entryIndex != -1);
            }

            //----------------------------------------------------------------------------------------
            // Returns -1 if not found (if key expires during FindEntry, this can be treated as "not found.")
            //
            // Preconditions:
            //     Must hold _lock, or be prepared to retry the search while holding _lock.
            //     Key already validated as non-null.
            //----------------------------------------------------------------------------------------
            internal int FindEntry(TKey key, out object value)
            {
                int hashCode = RuntimeHelpers.GetHashCode(key) & Int32.MaxValue;
                int bucket = hashCode & (_buckets.Length - 1);
                for (int entriesIndex = Volatile.Read(ref _buckets[bucket]); entriesIndex != -1; entriesIndex = _entries[entriesIndex].next)
                {
                    if (_entries[entriesIndex].hashCode == hashCode && _entries[entriesIndex].depHnd.GetPrimaryAndSecondary(out value) == key)
                    {
                        GC.KeepAlive(this); // ensure we don't get finalized while accessing DependentHandles.

                        return entriesIndex;
                    }
                }
                GC.KeepAlive(this); // ensure we don't get finalized while accessing DependentHandles.
                value = null;
                return -1;
            }

            internal bool Remove(TKey key)
            {
                VerifyIntegrity();

                object value;
                int entryIndex = FindEntry(key, out value);
                if (entryIndex != -1)
                {
                    //
                    // We do not free the handle here, as we may be racing with readers who already saw the hash code.
                    // Instead, we simply overwrite the entry's hash code, so subsequent reads will ignore it.
                    // The handle will be free'd in Container's finalizer, after the table is resized or discarded.
                    //
                    Volatile.Write(ref _entries[entryIndex].hashCode, -1);
                    return true;
                }
                return false;
            }

            //----------------------------------------------------------------------------------------
            // This does two things: resize and scrub expired keys off bucket lists.
            //
            // Precondition:
            //      Must hold _lock.
            //
            // Postcondition:
            //      _firstEntry is less than _entries.Length on exit, that is, the table has at least one free entry.
            //----------------------------------------------------------------------------------------
            internal Container Resize()
            {
                // Start by assuming we won't resize.
                int newSize = _buckets.Length;

                // If any expired or removed keys exist, we won't resize.
                bool hasExpiredEntries = false;
                for (int entriesIndex = 0; entriesIndex < _entries.Length; entriesIndex++)
                {
                    if (_entries[entriesIndex].hashCode == -1)
                    {
                        // the entry was removed
                        hasExpiredEntries = true;
                        break;
                    }

                    if (_entries[entriesIndex].depHnd.IsAllocated && _entries[entriesIndex].depHnd.GetPrimary() == null)
                    {
                        // the entry has expired
                        hasExpiredEntries = true;
                        break;
                    }
                }

                if (!hasExpiredEntries)
                {
                    // Not necessary to check for overflow here, the attempt to allocate new arrays will throw
                    newSize = _buckets.Length * 2;
                }

                return Resize(newSize);
            }
            
            internal Container Resize(int newSize)
            {
                Contract.Assert(IsPowerOfTwo(newSize));

                // Reallocate both buckets and entries and rebuild the bucket and entries from scratch.
                // This serves both to scrub entries with expired keys and to put the new entries in the proper bucket.
                int[] newBuckets = new int[newSize];
                for (int bucketIndex = 0; bucketIndex < newSize; bucketIndex++)
                {
                    newBuckets[bucketIndex] = -1;
                }
                Entry[] newEntries = new Entry[newSize];
                int newEntriesIndex = 0;

                // Migrate existing entries to the new table.
                for (int entriesIndex = 0; entriesIndex < _entries.Length; entriesIndex++)
                {
                    int hashCode = _entries[entriesIndex].hashCode;
                    DependentHandle depHnd = _entries[entriesIndex].depHnd;
                    if (hashCode != -1 && depHnd.IsAllocated && depHnd.GetPrimary() != null)
                    {
                        // Entry is used and has not expired. Link it into the appropriate bucket list.
                        // Note that we have to copy the DependentHandle, since the original copy will be freed
                        // by the Container's finalizer.  
                        newEntries[newEntriesIndex].hashCode = hashCode;
                        newEntries[newEntriesIndex].depHnd = depHnd.AllocateCopy();
                        int bucket = hashCode & (newBuckets.Length - 1);
                        newEntries[newEntriesIndex].next = newBuckets[bucket];
                        newBuckets[bucket] = newEntriesIndex;
                        newEntriesIndex++;
                    }
                }

                GC.KeepAlive(this); // ensure we don't get finalized while accessing DependentHandles.

                return new Container(newBuckets, newEntries, newEntriesIndex);
            }

            internal ICollection<TKey> Keys
            {
                get
                {
                    LowLevelListWithIList<TKey> list = new LowLevelListWithIList<TKey>();

                    for (int bucket = 0; bucket < _buckets.Length; ++bucket)
                    {
                        for (int entriesIndex = _buckets[bucket]; entriesIndex != -1; entriesIndex = _entries[entriesIndex].next)
                        {
                            TKey thisKey = RuntimeHelpers.UncheckedCast<TKey>(_entries[entriesIndex].depHnd.GetPrimary());
                            if (thisKey != null)
                            {
                                list.Add(thisKey);
                            }
                        }
                    }

                    GC.KeepAlive(this); // ensure we don't get finalized while accessing DependentHandles.
                    return list;
                }
            }

            internal ICollection<TValue> Values
            {
                get
                {
                    LowLevelListWithIList<TValue> list = new LowLevelListWithIList<TValue>();

                    for (int bucket = 0; bucket < _buckets.Length; ++bucket)
                    {
                        for (int entriesIndex = _buckets[bucket]; entriesIndex != -1; entriesIndex = _entries[entriesIndex].next)
                        {
                            Object primary = null;
                            Object secondary = null;

                            primary = _entries[entriesIndex].depHnd.GetPrimaryAndSecondary(out secondary);

                            // Now that we've secured a strong reference to the secondary, must check the primary again
                            // to ensure it didn't expire (otherwise, we open a race where TryGetValue misreports an
                            // expired key as a live key with a null value.)
                            if (primary != null)
                            {
                                list.Add(RuntimeHelpers.UncheckedCast<TValue>(secondary));
                            }
                        }
                    }

                    GC.KeepAlive(this); // ensure we don't get finalized while accessing DependentHandles.
                    return list;
                }
            }

            internal TKey FindEquivalentKeyUnsafe(TKey key, out TValue value)
            {
                for (int bucket = 0; bucket < _buckets.Length; ++bucket)
                {
                    for (int entriesIndex = _buckets[bucket]; entriesIndex != -1; entriesIndex = _entries[entriesIndex].next)
                    {
                        if (_entries[entriesIndex].hashCode == -1)
                        {
                            continue;   // removed entry whose handle is awaiting condemnation by the finalizer.
                        }

                        object thisKey, thisValue;
                        thisKey = _entries[entriesIndex].depHnd.GetPrimaryAndSecondary(out thisValue);
                        if (Object.Equals(thisKey, key))
                        {
                            GC.KeepAlive(this); // ensure we don't get finalized while accessing DependentHandles.
                            value = RuntimeHelpers.UncheckedCast<TValue>(thisValue);
                            return RuntimeHelpers.UncheckedCast<TKey>(thisKey);
                        }
                    }
                }

                GC.KeepAlive(this); // ensure we don't get finalized while accessing DependentHandles.
                value = default(TValue);
                return null;
            }

            //----------------------------------------------------------------------------------------
            // Precondition:
            //     Must hold _lock.
            //----------------------------------------------------------------------------------------
            private void VerifyIntegrity()
            {
                if (_invalid)
                {
                    throw new InvalidOperationException(SR.CollectionCorrupted);
                }
            }

            //----------------------------------------------------------------------------------------
            // Finalizer.
            //----------------------------------------------------------------------------------------
            ~Container()
            {
                // We're just freeing per-appdomain unmanaged handles here. If we're already shutting down the AD,
                // don't bother.
                //
                // (Despite its name, Environment.HasShutdownStart also returns true if the current AD is finalizing.)

                if (Environment.HasShutdownStarted)
                {
                    return;
                }

                if (_invalid)
                {
                    return;
                }
                Entry[] entries = _entries;

                // Make sure anyone sneaking into the table post-resurrection
                // gets booted before they can damage the native handle table.
                _invalid = true;
                _entries = null;
                _buckets = null;

                if (entries != null)
                {
                    for (int entriesIndex = 0; entriesIndex < entries.Length; entriesIndex++)
                    {
                        entries[entriesIndex].depHnd.Free();
                    }
                }
            }

            private int[] _buckets;             // _buckets[hashcode & (_buckets.Length - 1)] contains index of the first entry in bucket (-1 if empty)
            private Entry[] _entries;
            private int _firstFreeEntry;        // _firstFreeEntry < _entries.Length => table has capacity,  entries grow from the bottom of the table.
            private bool _invalid;              // flag detects if OOM or other background exception threw us out of the lock.
        }

        private volatile Container _container;
        private readonly Lock _lock;            // This lock protects all mutation of data in the table.  Readers do not take this lock.

        private const int InitialCapacity = 8;  // Must be a power of two
        #endregion
    }
    #endregion

    #region DependentHandle
    //=========================================================================================
    // This struct collects all operations on native DependentHandles. The DependentHandle
    // merely wraps an IntPtr so this struct serves mainly as a "managed typedef."
    //
    // DependentHandles exist in one of two states:
    //
    //    IsAllocated == false
    //        No actual handle is allocated underneath. Illegal to call GetPrimary
    //        or GetPrimaryAndSecondary(). Ok to call Free().
    //
    //        Initializing a DependentHandle using the nullary ctor creates a DependentHandle
    //        that's in the !IsAllocated state.
    //        (! Right now, we get this guarantee for free because (IntPtr)0 == NULL unmanaged handle.
    //         ! If that assertion ever becomes false, we'll have to add an _isAllocated field
    //         ! to compensate.)
    //        
    //
    //    IsAllocated == true
    //        There's a handle allocated underneath. You must call Free() on this eventually
    //        or you cause a native handle table leak.
    //
    // This struct intentionally does no self-synchronization. It's up to the caller to
    // to use DependentHandles in a thread-safe way.
    //=========================================================================================
    [ComVisible(false)]
    internal struct DependentHandle
    {
        #region Constructors
        public DependentHandle(Object primary, Object secondary)
        {
            _handle = RuntimeImports.RhHandleAllocDependent(primary, secondary);
        }
        #endregion

        #region Public Members
        public bool IsAllocated
        {
            get
            {
                return _handle != (IntPtr)0;
            }
        }

        // Getting the secondary object is more expensive than getting the first so
        // we provide a separate primary-only accessor for those times we only want the
        // primary.
        public Object GetPrimary()
        {
            return RuntimeImports.RhHandleGet(_handle);
        }

        public object GetPrimaryAndSecondary(out Object secondary)
        {
            return RuntimeImports.RhHandleGetDependent(_handle, out secondary);
        }

        //----------------------------------------------------------------------
        // Forces dependentHandle back to non-allocated state (if not already there)
        // and frees the handle if needed.
        //----------------------------------------------------------------------
        public void Free()
        {
            if (_handle != (IntPtr)0)
            {
                IntPtr handle = _handle;
                _handle = (IntPtr)0;
                RuntimeImports.RhHandleFree(handle);
            }
        }
        #endregion

        #region Internal Members
        internal DependentHandle AllocateCopy()
        {
            object primary, secondary;
            primary = GetPrimaryAndSecondary(out secondary);
            return new DependentHandle(primary, secondary);
        }
        #endregion

        #region Private Data Member
        private IntPtr _handle;
        #endregion

    } // struct DependentHandle
    #endregion
}
