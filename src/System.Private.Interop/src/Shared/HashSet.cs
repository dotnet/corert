// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Threading;

namespace System.Collections.Generic.Internal
{
    /// <summary>
    /// HashSet; Dictionary without Value
    /// 1. Deriving from DictionaryBase to share common code
    /// 2. Hashing/bucket moved to DictionaryBase
    /// 3. No interface implementation. You pay for the methods called
    /// 4. Support FindFirstKey/FindNextKey, hash code based search (returning key to caller for key comparison)
    /// 5. Support GetNext for simple enumeration of items
    /// 6. No comparer, no interface dispatching calls
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    internal class HashSet<TKey> : DictionaryBase where TKey : IEquatable<TKey>
    {
        const int MinimalSize = 11; // Have non-zero minimal size so that we do not need to check for null entries

        private TKey[] keyArray;

        private Lock m_lock;

        public HashSet(int capacity) : this(capacity, false)
        {
        }

        public HashSet(int capacity, bool sync)
        {
            if (capacity < MinimalSize)
            {
                capacity = MinimalSize;
            }
            if (sync)
            {
                m_lock = new Lock();
            }

            Initialize(capacity);
        }

        public void LockAcquire()
        {
            Debug.Assert(m_lock != null);

            m_lock.Acquire();
        }

        public void LockRelease()
        {
            Debug.Assert(m_lock != null);

            m_lock.Release();
        }

        public void Clear()
        {
            if (count > 0)
            {
                ClearBase();
                Array.Clear(keyArray, 0, count);
            }
        }

        public bool ContainsKey(TKey key, int hashCode)
        {
            return FindEntry(key, hashCode) >= 0;
        }

        private int FindEntry(TKey key, int hashCode)
        {
            hashCode = hashCode & 0x7FFFFFFF;

            for (int i = entries[ModLength(hashCode)].bucket; i >= 0; i = entries[i].next)
            {
                if (entries[i].hashCode == hashCode && key.Equals(keyArray[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// First first matching entry, returning index, update key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public int FindFirstKey(ref TKey key, int hashCode)
        {
            int entry = FindFirstEntry(hashCode & 0x7FFFFFFF);

            if (entry >= 0)
            {
                key = keyArray[entry];
            }

            return entry;
        }

        /// <summary>
        /// Find next matching entry, returning index, update key
        /// </summary>
        /// <param name="key"></param>
        /// <param name="entry"></param>
        /// <returns></returns>
        public int FindNextKey(ref TKey key, int entry)
        {
            entry = FindNextEntry(entry);

            if (entry >= 0)
            {
                key = keyArray[entry];
            }

            return entry;
        }

        /// <summary>
        /// Enumeration of items
        /// </summary>
        [System.Runtime.InteropServices.GCCallback]
        internal bool GetNext(ref TKey key, ref int index)
        {
            for (int i = index + 1; i < this.count; i++)
            {
                if (entries[i].hashCode >= 0)
                {
                    key = keyArray[i];

                    index = i;

                    return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Initialize(int capacity)
        {
            int size = InitializeBase(capacity);

            keyArray = new TKey[size];
        }

        public KeyCollection Keys
        {
            get
            {
                Contract.Ensures(Contract.Result<KeyCollection>() != null);

                return new KeyCollection(this);
            }
        }

        public bool Add(TKey key, int hashCode)
        {
            hashCode = hashCode & 0x7FFFFFFF;
            int targetBucket = ModLength(hashCode);

            for (int i = entries[targetBucket].bucket; i >= 0; i = entries[i].next)
            {
                if (entries[i].hashCode == hashCode && key.Equals(keyArray[i]))
                {
                    return true;
                }
            }

            int index;

            if (freeCount > 0)
            {
                index = freeList;
                freeList = entries[index].next;
                freeCount--;
            }
            else
            {
                if (count == entries.Length)
                {
                    Resize();
                    targetBucket = ModLength(hashCode);
                }

                index = count;
                count++;
            }

            entries[index].hashCode = hashCode;
            entries[index].next = entries[targetBucket].bucket;
            keyArray[index] = key;
            entries[targetBucket].bucket = index;

            return false;
        }

        private void Resize()
        {
            Resize(HashHelpers.ExpandPrime(count));
        }

        private void Resize(int newSize)
        {
#if !RHTESTCL
            Contract.Assert(newSize >= entries.Length);
#endif

            Entry[] newEntries = ResizeBase1(newSize);

            TKey[] newKeys = new TKey[newSize];
            Array.Copy(keyArray, 0, newKeys, 0, count);

            ResizeBase2(newEntries, newSize);

            keyArray = newKeys;
        }

        public bool Remove(TKey key, int hashCode)
        {
            hashCode = hashCode & 0x7FFFFFFF;
            int bucket = ModLength(hashCode);
            int last = -1;

            for (int i = entries[bucket].bucket; i >= 0; last = i, i = entries[i].next)
            {
                if (entries[i].hashCode == hashCode && key.Equals(keyArray[i]))
                {
                    if (last < 0)
                    {
                        entries[bucket].bucket = entries[i].next;
                    }
                    else
                    {
                        entries[last].next = entries[i].next;
                    }

                    entries[i].hashCode = -1;
                    entries[i].next = freeList;
                    keyArray[i] = default(TKey);
                    freeList = i;
                    freeCount++;

                    return true;
                }
            }

            return false;
        }

        public sealed class KeyCollection : ICollection<TKey>, ICollection
        {
            private HashSet<TKey> m_hashSet;

            public KeyCollection(HashSet<TKey> hashSet)
            {
                if (hashSet == null)
                {
                    throw new ArgumentNullException("hashSet");
                }

                this.m_hashSet = hashSet;
            }

            public Enumerator GetEnumerator()
            {
                return new Enumerator(m_hashSet);
            }

            public void CopyTo(TKey[] array, int index)
            {
                if (array == null)
                {
                    throw new ArgumentNullException("array");
                }

                if (index < 0 || index > array.Length)
                {
                    throw new ArgumentOutOfRangeException("index", SR.ArgumentOutOfRange_NeedNonNegNum);
                }

                if (array.Length - index < m_hashSet.Count)
                {
                    throw new ArgumentException(SR.Arg_ArrayPlusOffTooSmall);
                }

                int count = m_hashSet.count;
                TKey[] keys = m_hashSet.keyArray;
                Entry[] entries = m_hashSet.entries;

                for (int i = 0; i < count; i++)
                {
                    if (entries[i].hashCode >= 0)
                    {
                        array[index++] = keys[i];
                    }
                }
            }

            public int Count
            {
                get { return m_hashSet.Count; }
            }

            bool ICollection<TKey>.IsReadOnly
            {
                get { return true; }
            }

            void ICollection<TKey>.Add(TKey item)
            {
                throw new NotSupportedException(SR.NotSupported_KeyCollectionSet);
            }

            void ICollection<TKey>.Clear()
            {
                throw new NotSupportedException(SR.NotSupported_KeyCollectionSet);
            }

            bool ICollection<TKey>.Contains(TKey item)
            {
                throw new NotSupportedException(SR.NotSupported_KeyCollectionSet);
            }

            bool ICollection<TKey>.Remove(TKey item)
            {
                throw new NotSupportedException(SR.NotSupported_KeyCollectionSet);
            }

            IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator()
            {
                return new Enumerator(m_hashSet);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new Enumerator(m_hashSet);
            }

            void ICollection.CopyTo(Array array, int index)
            {
                if (array == null)
                {
                    throw new ArgumentNullException("array");
                }

                if (array.Rank != 1)
                {
                    throw new ArgumentException(SR.Arg_RankMultiDimNotSupported);
                }

                if (array.GetLowerBound(0) != 0)
                {
                    throw new ArgumentException(SR.Arg_NonZeroLowerBound);
                }

                if (index < 0 || index > array.Length)
                {
                    throw new ArgumentOutOfRangeException("index", SR.ArgumentOutOfRange_NeedNonNegNum);
                }

                if (array.Length - index < m_hashSet.Count)
                {
                    throw new ArgumentException(SR.Arg_ArrayPlusOffTooSmall);
                }

                TKey[] keys = array as TKey[];

                if (keys != null)
                {
                    CopyTo(keys, index);
                }
                else
                {
                    object[] objects = array as object[];

                    if (objects == null)
                    {
                        throw new ArgumentException(SR.Argument_InvalidArrayType);
                    }

                    int count = m_hashSet.count;
                    Entry[] entries = m_hashSet.entries;
                    TKey[] ks = m_hashSet.keyArray;

                    try
                    {
                        for (int i = 0; i < count; i++)
                        {
                            if (entries[i].hashCode >= 0)
                            {
                                objects[index++] = ks[i];
                            }
                        }
                    }
                    catch (ArrayTypeMismatchException)
                    {
                        throw new ArgumentException(SR.Argument_InvalidArrayType);
                    }
                }
            }

            bool ICollection.IsSynchronized
            {
                get { return false; }
            }

            Object ICollection.SyncRoot
            {
                get { return ((ICollection)m_hashSet).SyncRoot; }
            }

            public struct Enumerator : IEnumerator<TKey>, System.Collections.IEnumerator
            {
                private HashSet<TKey> hashSet;
                private int index;
                private TKey currentKey;

                internal Enumerator(HashSet<TKey> _hashSet)
                {
                    this.hashSet = _hashSet;
                    index = 0;
                    currentKey = default(TKey);
                }

                public void Dispose()
                {
                }

                public bool MoveNext()
                {
                    while ((uint)index < (uint)hashSet.count)
                    {
                        if (hashSet.entries[index].hashCode >= 0)
                        {
                            currentKey = hashSet.keyArray[index];
                            index++;

                            return true;
                        }

                        index++;
                    }

                    index = hashSet.count + 1;
                    currentKey = default(TKey);

                    return false;
                }

                public TKey Current
                {
                    get
                    {
                        return currentKey;
                    }
                }

                Object System.Collections.IEnumerator.Current
                {
                    get
                    {
                        if (index == 0 || (index == hashSet.count + 1))
                        {
                            throw new InvalidOperationException(SR.InvalidOperation_EnumOpCantHappen);
                        }

                        return currentKey;
                    }
                }

                void System.Collections.IEnumerator.Reset()
                {
                    index = 0;
                    currentKey = default(TKey);
                }
            }
        }
    }
}
