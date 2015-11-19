// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    /// <summary>
    /// A hash table which is lock free for readers and up to 1 writer at a time.
    /// It must be possible to compute the key's hashcode from a value.
    /// All values must be reference types.
    /// It must be possible to perform an equality check between a key and a value.
    /// It must be possible to perform an equality check between a value and a value.
    /// A LockFreeReaderKeyValueComparer must be provided to perform these operations.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    abstract public class LockFreeReaderHashtable<TKey, TValue> where TValue : class
    {
        private const int _fillPercentageBeforeResize = 60;

        /// <summary>
        /// _hashtable is the currently visible underlying array for the hashtable
        /// Any modifications to this array must be additive only, and there must
        /// never be a situation where the visible _hashtable has less data than
        /// it did at an earlier time. This value is initialized to an array of size
        /// 1. (That array is never mutated as any additions will trigger an Expand 
        /// operation, but we don't use an empty array as the
        /// initial step, as this approach allows the TryGetValue logic to always
        /// succeed without needing any length or null checks.)
        /// </summary>
        private TValue[] _hashtable = s_hashtableInitialArray;
        private static TValue[] s_hashtableInitialArray = new TValue[1];

        /// <summary>
        /// _count represents the current count of elements in the hashtable
        /// _count is used in combination with _resizeCount to control when the 
        /// hashtable should expand
        /// </summary>
        private int _count = 0;

        /// <summary>
        /// _resizeCount represents the size at which the hashtable should resize.
        /// </summary>
        private int _resizeCount = 0;

        /// <summary>
        /// Get the underlying array for the hashtable at this time. Implemented with
        /// MethodImplOptions.NoInlining to prohibit compiler optimizations that allow
        /// multiple reads from the _hashtable field when there is only one specified
        /// read without requiring the use of the volatile specifier which requires
        /// a more significant read barrier. Used by readers so that a reader thread
        /// looks at a consistent hashtable underlying array throughout the lifetime
        /// of a single read operation.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private TValue[] GetCurrentHashtable()
        {
            return _hashtable;
        }

        /// <summary>
        /// Set the newly visible hashtable underlying array. Used by writers after
        /// the new array is fully constructed. The volatile write is used to ensure
        /// that all writes to the contents of hashtable are completed before _hashtable
        /// is visible to readers.
        /// </summary>
        private void SetCurrentHashtable(TValue[] hashtable)
        {
            Volatile.Write(ref _hashtable, hashtable);
        }

        /// <summary>
        /// Used to ensure that the hashtable can function with
        /// fairly poor initial hash codes.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static int HashInt1(int key)
        {
            unchecked
            {
                int a = (int)0x9e3779b9 + key;
                int b = (int)0x9e3779b9;
                int c = 16777619;
                a -= b; a -= c; a ^= (c >> 13);
                b -= c; b -= a; b ^= (a << 8);
                c -= a; c -= b; c ^= (b >> 13);
                a -= b; a -= c; a ^= (c >> 12);
                b -= c; b -= a; b ^= (a << 16);
                c -= a; c -= b; c ^= (b >> 5);
                a -= b; a -= c; a ^= (c >> 3);
                b -= c; b -= a; b ^= (a << 10);
                c -= a; c -= b; c ^= (b >> 15);
                return c;
            }
        }

        /// <summary>
        /// Generate a somewhat independent hash value from another integer. This is used
        /// as part of a double hashing scheme. By being relatively prime with powers of 2
        /// this hash function can be reliably used as part of a double hashing scheme as it
        /// is garaunteed to eventually probe every slot in the table. (Table sizes are
        /// constrained to be a power of two)
        /// </summary>
        public static int HashInt2(int key)
        {
            unchecked
            {
                int hash = unchecked((int)0xB1635D64) + key;
                hash += (hash << 3);
                hash ^= (hash >> 11);
                hash += (hash << 15);
                hash |= 0x00000001; //  To make sure that this is relatively prime with power of 2
                return hash;
            }
        }

        /// <summary>
        /// Create the LockFreeReaderHashtable. This hash table is designed for GetOrCreateValue
        /// to be a generally lock free api (unless an add is necessary)
        /// </summary>
        public LockFreeReaderHashtable()
        {
        }

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="value">When this method returns, contains the value associated with 
        /// the specified key, if the key is found; otherwise, the default value for the type 
        /// of the value parameter. This parameter is passed uninitialized. This function is threadsafe,
        /// and wait-free</param>
        /// <returns>true if a value was found</returns>
        public bool TryGetValue(TKey key, out TValue value)
        {
            TValue[] hashTableLocal = GetCurrentHashtable();
            Debug.Assert(hashTableLocal.Length > 0);
            int mask = hashTableLocal.Length - 1;
            int hashCode = GetKeyHashCode(key);
            int tableIndex = HashInt1(hashCode) & mask;

            if (hashTableLocal[tableIndex] == null)
            {
                value = null;
                return false;
            }

            if (CompareKeyToValue(key, hashTableLocal[tableIndex]))
            {
                value = hashTableLocal[tableIndex];
                return true;
            }

            int hash2 = HashInt2(hashCode);
            tableIndex = (tableIndex + hash2) & mask;

            while (hashTableLocal[tableIndex] != null)
            {
                if (CompareKeyToValue(key, hashTableLocal[tableIndex]))
                {
                    value = hashTableLocal[tableIndex];
                    return true;
                }
                tableIndex = (tableIndex + hash2) & mask;
            }
            value = null;
            return false;
        }

        /// <summary>
        /// Make the underlying array of the hashtable bigger. This function
        /// does not change the contents of the hashtable.
        /// </summary>
        private void Expand()
        {
            int newSize = checked(_hashtable.Length * 2);

            // The hashtable only functions well when it has a certain minimum size
            if (newSize < 16)
                newSize = 16;

            TValue[] hashTableLocal = new TValue[newSize];

            int mask = hashTableLocal.Length - 1;
            foreach (TValue value in _hashtable)
            {
                if (value == null)
                    continue;

                int hashCode = GetValueHashCode(value);
                int tableIndex = HashInt1(hashCode) & mask;

                // Initial probe into hashtable found empty spot
                if (hashTableLocal[tableIndex] == null)
                {
                    // Add to hash
                    hashTableLocal[tableIndex] = value;
                    continue;
                }

                int hash2 = HashInt2(hashCode);
                tableIndex = (tableIndex + hash2) & mask;

                while (hashTableLocal[tableIndex] != null)
                {
                    tableIndex = (tableIndex + hash2) & mask;
                }

                // We've probed to find an empty spot
                // Add to hash
                hashTableLocal[tableIndex] = value;
            }

            _resizeCount = checked((newSize * _fillPercentageBeforeResize) / 100);
            SetCurrentHashtable(hashTableLocal);
        }

        /// <summary>
        /// Add a value to the hashtable, or find a value which is already present in the hashtable.
        /// Note that the key is not specified as it is implicit in the value. This function is thread-safe
        /// through the use of locking.
        /// </summary>
        /// <param name="value">Value to attempt to add to the hashtable, must not be null</param>
        /// <returns>newly added value, or a value which was already present in the hashtable which is equal to it.</returns>
        public TValue AddOrGetExisting(TValue value)
        {
            if (value == null)
                throw new ArgumentNullException();

            lock (this)
            {
                // Check to see if adding this value may require a resize. If so, expand
                // the table now.
                if (_count >= _resizeCount)
                {
                    Expand();
                    Debug.Assert(_count < _resizeCount);
                }

                TValue[] hashTableLocal = _hashtable;
                int mask = hashTableLocal.Length - 1;
                int hashCode = GetValueHashCode(value);
                int tableIndex = HashInt1(hashCode) & mask;

                // Initial probe into hashtable found empty spot
                if (hashTableLocal[tableIndex] == null)
                {
                    // Add to hash, use a volatile write to ensure that
                    // the contents of the value are fully published to all
                    // threads before adding to the hashtable
                    Volatile.Write(ref hashTableLocal[tableIndex], value);
                    _count++;
                    return value;
                }

                if (CompareValueToValue(value, hashTableLocal[tableIndex]))
                {
                    // Value is already present in hash, do not add
                    return hashTableLocal[tableIndex];
                }

                int hash2 = HashInt2(hashCode);
                tableIndex = (tableIndex + hash2) & mask;

                while (hashTableLocal[tableIndex] != null)
                {
                    if (CompareValueToValue(value, hashTableLocal[tableIndex]))
                    {
                        // Value is already present in hash, do not add
                        return hashTableLocal[tableIndex];
                    }
                    tableIndex = (tableIndex + hash2) & mask;
                }

                // We've probed to find an empty spot
                // Add to hash, use a volatile write to ensure that
                // the contents of the value are fully published to all
                // threads before adding to the hashtable
                Volatile.Write(ref hashTableLocal[tableIndex], value);
                _count++;
                return value;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private TValue CreateValueAndEnsureValueIsInTable(TKey key)
        {
            TValue newValue = CreateValueFromKey(key);

            return AddOrGetExisting(newValue);
        }

        /// <summary>
        /// Get the value associated with a key. If value is not present in dictionary, use the creator delegate passed in
        /// at object construction time to create the value, and attempt to add it to the table. (Create the value while not
        /// under the lock, but add it to the table while under the lock. This may result in a throw away object being constructed)
        /// This function is thread-safe, but will take a lock to perform its operations.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TValue GetOrCreateValue(TKey key)
        {
            TValue existingValue;
            if (TryGetValue(key, out existingValue))
                return existingValue;

            return CreateValueAndEnsureValueIsInTable(key);
        }

        /// <summary>
        /// Determine if this collection contains a value associated with a key. This function is thread-safe, and wait-free.
        /// </summary>
        public bool Contains(TKey key)
        {
            TValue dummyExistingValue;
            return TryGetValue(key, out dummyExistingValue);
        }

        /// <summary>
        /// Enumerator type for the LockFreeReaderHashtable
        /// This is threadsafe, but is not garaunteed to avoid torn state.
        /// In particular, the enumerator may report some newly added values
        /// but not others. All values in the hashtable as of enumerator
        /// creation will always be enumerated.
        /// </summary>
        public struct Enumerator
        {
            private TValue[] _hashtableContentsToEnumerate;
            private int _index;
            private TValue _current;

            /// <summary>
            /// Use this to get an enumerable collection from a LockFreeReaderHashtable. 
            /// Used instead of a GetEnumerator method on the LockFreeReaderHashtable to 
            /// reduce excess type creation. (By moving the method here, the generic dictionary for
            /// LockFreeReaderHashtable does not need to contain a reference to the
            /// enumerator type.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Enumerator Get(LockFreeReaderHashtable<TKey, TValue> hashtable)
            {
                return new Enumerator(hashtable);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Enumerator GetEnumerator()
            {
                return this;
            }

            internal Enumerator(LockFreeReaderHashtable<TKey, TValue> hashtable)
            {
                _hashtableContentsToEnumerate = hashtable._hashtable;
                _index = 0;
                _current = default(TValue);
            }

            public bool MoveNext()
            {
                if ((_hashtableContentsToEnumerate != null) && (_index < _hashtableContentsToEnumerate.Length))
                {
                    for (; _index < _hashtableContentsToEnumerate.Length; _index++)
                    {
                        if (_hashtableContentsToEnumerate[_index] != null)
                        {
                            _current = _hashtableContentsToEnumerate[_index];
                            _index++;
                            return true;
                        }
                    }
                }

                _current = default(TValue);
                return false;
            }

            public TValue Current
            {
                get
                {
                    return _current;
                }
            }
        }

        /// <summary>
        /// Given a key, compute a hash code. This function must be thread safe.
        /// </summary>
        protected abstract int GetKeyHashCode(TKey key);

        /// <summary>
        /// Given a value, compute a hash code which would be identical to the hash code
        /// for a key which should look up this value. This function must be thread safe.
        /// </summary>
        protected abstract int GetValueHashCode(TValue value);

        /// <summary>
        /// Compare a key and value. If the key refers to this value, return true.
        /// This function must be thread safe.
        /// </summary>
        protected abstract bool CompareKeyToValue(TKey key, TValue value);

        /// <summary>
        /// Compare a value with another value. Return true if values are equal.
        /// This function must be thread safe.
        /// </summary>
        protected abstract bool CompareValueToValue(TValue value1, TValue value2);

        /// <summary>
        /// Create a new value from a key. Must be threadsafe. Value may or may not be added
        /// to collection. Return value must not be null.
        /// </summary>
        protected abstract TValue CreateValueFromKey(TKey key);
    }
}
