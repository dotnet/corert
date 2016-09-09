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
    /// Simplified version of Dictionary<K,V>
    /// 1. Deriving from DictionaryBase to share common code
    /// 2. Hashing/bucket moved to DictionaryBase
    /// 3. Seperate TKey,TValue array. This will get rid of array with both TKey and TValue
    /// 4. No interface implementation. You pay for the methods called
    /// 5. Support FindFirstKey/FindNextKey, hash code based search (returning key to caller for key comparison)
    /// 6. If comparer is provided, it has to be non null. This allows reducing dependency on EqualityComparer<TKey>.Default
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    internal class Dictionary<TKey, TValue> : DictionaryBase
    {
        private TKey[] keyArray;
        private TValue[] valueArray;

        private IEqualityComparer<TKey> comparer;
        private Lock m_lock;

        public Dictionary()
            : this(0, EqualityComparer<TKey>.Default, false)
        {
        }

        public Dictionary(bool sync) : this(0, EqualityComparer<TKey>.Default, sync)
        {
        }

        public Dictionary(int capacity)
            : this(capacity, EqualityComparer<TKey>.Default, false)
        {
        }

        public Dictionary(int capacity, bool sync) : this(capacity, EqualityComparer<TKey>.Default, sync)
        {
        }

        public Dictionary(IEqualityComparer<TKey> comparer)
            : this(0, comparer, false)
        {
        }

        public Dictionary(IEqualityComparer<TKey> comparer, bool sync) : this(0, comparer, sync)
        {
        }

        public Dictionary(int capacity, IEqualityComparer<TKey> comparer) : this(capacity, comparer, false)
        {
        }

        public Dictionary(int capacity, IEqualityComparer<TKey> comparer, bool sync)
        {
            // If comparer parameter is passed in, it can't be null
            // This removes dependency on EqualityComparer<TKey>.Default which brings bunch of cost
            Debug.Assert(comparer != null);

            if (capacity > 0)
            {
                Initialize(capacity);
            }

            this.comparer = comparer;

            if (sync)
            {
                m_lock = new Lock();
            }
        }

        public Dictionary(IDictionary<TKey, TValue> dictionary) : this(dictionary, EqualityComparer<TKey>.Default)
        {
        }

        public Dictionary(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer) :
            this(dictionary != null ? dictionary.Count : 0, comparer)
        {
            if (dictionary == null)
            {
                throw new ArgumentNullException("dictionary");
            }

            foreach (KeyValuePair<TKey, TValue> pair in dictionary)
            {
                Add(pair.Key, pair.Value);
            }
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

        public IEqualityComparer<TKey> Comparer
        {
            get
            {
                return comparer;
            }
        }

        public KeyCollection Keys
        {
            get
            {
                Contract.Ensures(Contract.Result<KeyCollection>() != null);

                return new KeyCollection(this);
            }
        }

        public ValueCollection Values
        {
            get
            {
                Contract.Ensures(Contract.Result<ValueCollection>() != null);

                return new ValueCollection(this);
            }
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(this, Enumerator.KeyValuePair);
        }

        public TValue this[TKey key]
        {
            get
            {
                int i = FindEntry(key);

                if (i >= 0)
                {
                    return valueArray[i];
                }

                throw new KeyNotFoundException();
            }
            set
            {
                Insert(key, value, false);
            }
        }

        public void Add(TKey key, TValue value)
        {
            if (!Insert(key, value, true))
            {
                throw new ArgumentException(SR.Argument_AddingDuplicate);
            }
        }

        public void Add(TKey key, TValue value, int hashCode)
        {
            if (!Insert(key, value, true, hashCode))
            {
                throw new ArgumentException(SR.Argument_AddingDuplicate);
            }
        }

        public void Clear()
        {
            if (count > 0)
            {
                ClearBase();
                Array.Clear(keyArray, 0, count);
                Array.Clear(valueArray, 0, count);
            }
        }

        public bool ContainsKey(TKey key)
        {
            return FindEntry(key) >= 0;
        }

        public bool ContainsValue(TValue value)
        {
            if (value == null)
            {
                for (int i = 0; i < count; i++)
                {
                    if (entries[i].hashCode >= 0 && valueArray[i] == null)
                    {
                        return true;
                    }
                }
            }
            else
            {
                EqualityComparer<TValue> c = EqualityComparer<TValue>.Default;

                for (int i = 0; i < count; i++)
                {
                    if (entries[i].hashCode >= 0 && c.Equals(valueArray[i], value))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void CopyTo(KeyValuePair<TKey, TValue>[] array, int index)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            if (index < 0 || index > array.Length)
            {
                throw new ArgumentOutOfRangeException("index", SR.ArgumentOutOfRange_NeedNonNegNum);
            }

            if (array.Length - index < Count)
            {
                throw new ArgumentException(SR.Arg_ArrayPlusOffTooSmall);
            }

            int count = this.count;
            Entry[] entries = this.entries;

            for (int i = 0; i < count; i++)
            {
                if (entries[i].hashCode >= 0)
                {
                    array[index++] = new KeyValuePair<TKey, TValue>(keyArray[i], valueArray[i]);
                }
            }
        }

        /// <summary>
        /// Get total count of items, including free cells
        /// </summary>
        /// <remarks>
        /// WARNING: This function will be called under a GC callback. Please read the comments in
        /// GCCallbackAttribute to understand all the implications before you make any changes
        /// </remarks>
        [System.Runtime.InteropServices.GCCallback]
        public int GetMaxCount()
        {
            return this.count;
        }

        /// <summary>
        /// Get Key[i], return true if not free
        /// </summary>
        public bool GetKey(int index, ref TKey key)
        {
            Debug.Assert((index >= 0) && (index < this.count));

            if (entries[index].hashCode >= 0)
            {
                key = keyArray[index];

                return true;
            }

            return false;
        }

        /// <summary>
        /// Get Value[i], return true if not free
        /// </summary>
        /// <remarks>
        /// WARNING: This function will be called under a GC callback. Please read the comments in
        /// GCCallbackAttribute to understand all the implications before you make any changes
        /// </remarks>
        [System.Runtime.InteropServices.GCCallback]
        public bool GetValue(int index, ref TValue value)
        {
            Debug.Assert((index >= 0) && (index < this.count));

            if (entries[index].hashCode >= 0)
            {
                value = valueArray[index];

                return true;
            }

            return false;
        }

        private int FindEntry(TKey key)
        {
            if (entries != null)
            {
                int hashCode = comparer.GetHashCode(key) & 0x7FFFFFFF;

                for (int i = entries[ModLength(hashCode)].bucket; i >= 0; i = entries[i].next)
                {
                    if (entries[i].hashCode == hashCode && comparer.Equals(keyArray[i], key))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        private int FindEntry(TKey key, int hashCode)
        {
            if (entries != null)
            {
                hashCode = hashCode & 0x7FFFFFFF;

                for (int i = entries[ModLength(hashCode)].bucket; i >= 0; i = entries[i].next)
                {
                    if (entries[i].hashCode == hashCode && comparer.Equals(keyArray[i], key))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// First first matching entry, returning index, update key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public int FindFirstKey(ref TKey key)
        {
            int hashCode = comparer.GetHashCode(key) & 0x7FFFFFFF;

            int entry = FindFirstEntry(hashCode);

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

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Initialize(int capacity)
        {
            int size = InitializeBase(capacity);

            keyArray = new TKey[size];
            valueArray = new TValue[size];
        }

        private bool Insert(TKey key, TValue value, bool add)
        {
            return Insert(key, value, add, comparer.GetHashCode(key));
        }

        private bool Insert(TKey key, TValue value, bool add, int hashCode)
        {
            if (entries == null)
            {
                Initialize(0);
            }

            hashCode = hashCode & 0x7FFFFFFF;
            int targetBucket = ModLength(hashCode);

            for (int i = entries[targetBucket].bucket; i >= 0; i = entries[i].next)
            {
                if (entries[i].hashCode == hashCode && comparer.Equals(keyArray[i], key))
                {
                    if (add)
                    {
                        return false;
                    }

                    valueArray[i] = value;
                    version++;

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
            valueArray[index] = value;
            entries[targetBucket].bucket = index;
            version++;

            return true;
        }

        private void Resize()
        {
            Resize(HashHelpers.ExpandPrime(count));
        }

        private void Resize(int newSize)
        {
#if !RHTESTCL
            Debug.Assert(newSize >= entries.Length);
#endif

            Entry[] newEntries = ResizeBase1(newSize);

            TKey[] newKeys = new TKey[newSize];
            Array.Copy(keyArray, 0, newKeys, 0, count);

            TValue[] newValues = new TValue[newSize];
            Array.Copy(valueArray, 0, newValues, 0, count);

            ResizeBase2(newEntries, newSize);

            keyArray = newKeys;
            valueArray = newValues;
        }

        public bool Remove(TKey key)
        {
            if (entries != null)
            {
                int hashCode = comparer.GetHashCode(key) & 0x7FFFFFFF;
                int bucket = ModLength(hashCode);
                int last = -1;

                for (int i = entries[bucket].bucket; i >= 0; last = i, i = entries[i].next)
                {
                    if (entries[i].hashCode == hashCode && comparer.Equals(keyArray[i], key))
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
                        valueArray[i] = default(TValue);
                        freeList = i;
                        freeCount++;
                        version++;

                        return true;
                    }
                }
            }

            return false;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            int i = FindEntry(key);

            if (i >= 0)
            {
                value = valueArray[i];
                return true;
            }

            value = default(TValue);

            return false;
        }

        public bool TryGetValue(TKey key, int hashCode, out TValue value)
        {
            int i = FindEntry(key, hashCode);

            if (i >= 0)
            {
                value = valueArray[i];
                return true;
            }

            value = default(TValue);

            return false;
        }

        /// <summary>
        /// Return matching key
        /// </summary>
        public bool TryGetKey(ref TKey key)
        {
            int i = FindEntry(key);

            if (i >= 0)
            {
                key = keyArray[i];
                return true;
            }

            return false;
        }

        public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>,
            IDictionaryEnumerator
        {
            private Dictionary<TKey, TValue> dictionary;
            private int version;
            private int index;
            private KeyValuePair<TKey, TValue> current;
            private int getEnumeratorRetType;  // What should Enumerator.Current return?

            internal const int DictEntry = 1;
            internal const int KeyValuePair = 2;

            internal Enumerator(Dictionary<TKey, TValue> dictionary, int getEnumeratorRetType)
            {
                this.dictionary = dictionary;
                version = dictionary.version;
                index = 0;
                this.getEnumeratorRetType = getEnumeratorRetType;
                current = new KeyValuePair<TKey, TValue>();
            }

            public bool MoveNext()
            {
                if (version != dictionary.version)
                {
                    throw new InvalidOperationException(SR.InvalidOperation_EnumFailedVersion);
                }

                // Use unsigned comparison since we set index to dictionary.count+1 when the enumeration ends.
                // dictionary.count+1 could be negative if dictionary.count is Int32.MaxValue
                while ((uint)index < (uint)dictionary.count)
                {
                    if (dictionary.entries[index].hashCode >= 0)
                    {
                        current = new KeyValuePair<TKey, TValue>(dictionary.keyArray[index], dictionary.valueArray[index]);
                        index++;

                        return true;
                    }

                    index++;
                }

                index = dictionary.count + 1;
                current = new KeyValuePair<TKey, TValue>();

                return false;
            }

            public KeyValuePair<TKey, TValue> Current
            {
                get { return current; }
            }

            public void Dispose()
            {
            }

            object IEnumerator.Current
            {
                get
                {
                    if (index == 0 || (index == dictionary.count + 1))
                    {
                        throw new InvalidOperationException(SR.InvalidOperation_EnumOpCantHappen);
                    }

                    if (getEnumeratorRetType == DictEntry)
                    {
                        return new System.Collections.DictionaryEntry(current.Key, current.Value);
                    }
                    else
                    {
                        return new KeyValuePair<TKey, TValue>(current.Key, current.Value);
                    }
                }
            }

            void IEnumerator.Reset()
            {
                if (version != dictionary.version)
                {
                    throw new InvalidOperationException(SR.InvalidOperation_EnumFailedVersion);
                }

                index = 0;
                current = new KeyValuePair<TKey, TValue>();
            }

            DictionaryEntry IDictionaryEnumerator.Entry
            {
                get
                {
                    if (index == 0 || (index == dictionary.count + 1))
                    {
                        throw new InvalidOperationException(SR.InvalidOperation_EnumOpCantHappen);
                    }

                    return new DictionaryEntry(current.Key, current.Value);
                }
            }

            object IDictionaryEnumerator.Key
            {
                get
                {
                    if (index == 0 || (index == dictionary.count + 1))
                    {
                        throw new InvalidOperationException(SR.InvalidOperation_EnumOpCantHappen);
                    }

                    return current.Key;
                }
            }

            object IDictionaryEnumerator.Value
            {
                get
                {
                    if (index == 0 || (index == dictionary.count + 1))
                    {
                        throw new InvalidOperationException(SR.InvalidOperation_EnumOpCantHappen);
                    }

                    return current.Value;
                }
            }
        }

        public sealed class KeyCollection : ICollection<TKey>, ICollection
        {
            private Dictionary<TKey, TValue> dictionary;

            public KeyCollection(Dictionary<TKey, TValue> dictionary)
            {
                if (dictionary == null)
                {
                    throw new ArgumentNullException("dictionary");
                }

                this.dictionary = dictionary;
            }

            public Enumerator GetEnumerator()
            {
                return new Enumerator(dictionary);
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

                if (array.Length - index < dictionary.Count)
                {
                    throw new ArgumentException(SR.Arg_ArrayPlusOffTooSmall);
                }

                int count = dictionary.count;
                TKey[] keys = dictionary.keyArray;
                Entry[] entries = dictionary.entries;

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
                get { return dictionary.Count; }
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
                return dictionary.ContainsKey(item);
            }

            bool ICollection<TKey>.Remove(TKey item)
            {
                throw new NotSupportedException(SR.NotSupported_KeyCollectionSet);
            }

            IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator()
            {
                return new Enumerator(dictionary);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new Enumerator(dictionary);
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

                if (array.Length - index < dictionary.Count)
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

                    int count = dictionary.count;
                    Entry[] entries = dictionary.entries;
                    TKey[] ks = dictionary.keyArray;

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
                get { return ((ICollection)dictionary).SyncRoot; }
            }

            public struct Enumerator : IEnumerator<TKey>, System.Collections.IEnumerator
            {
                private Dictionary<TKey, TValue> dictionary;
                private int index;
                private int version;
                private TKey currentKey;

                internal Enumerator(Dictionary<TKey, TValue> dictionary)
                {
                    this.dictionary = dictionary;
                    version = dictionary.version;
                    index = 0;
                    currentKey = default(TKey);
                }

                public void Dispose()
                {
                }

                public bool MoveNext()
                {
                    if (version != dictionary.version)
                    {
                        throw new InvalidOperationException(SR.InvalidOperation_EnumFailedVersion);
                    }

                    while ((uint)index < (uint)dictionary.count)
                    {
                        if (dictionary.entries[index].hashCode >= 0)
                        {
                            currentKey = dictionary.keyArray[index];
                            index++;

                            return true;
                        }

                        index++;
                    }

                    index = dictionary.count + 1;
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
                        if (index == 0 || (index == dictionary.count + 1))
                        {
                            throw new InvalidOperationException(SR.InvalidOperation_EnumOpCantHappen);
                        }

                        return currentKey;
                    }
                }

                void System.Collections.IEnumerator.Reset()
                {
                    if (version != dictionary.version)
                    {
                        throw new InvalidOperationException(SR.InvalidOperation_EnumFailedVersion);
                    }

                    index = 0;
                    currentKey = default(TKey);
                }
            }
        }

        public sealed class ValueCollection : ICollection<TValue>, ICollection
        {
            private Dictionary<TKey, TValue> dictionary;

            public ValueCollection(Dictionary<TKey, TValue> dictionary)
            {
                if (dictionary == null)
                {
                    throw new ArgumentNullException("dictionary");
                }

                this.dictionary = dictionary;
            }

            public Enumerator GetEnumerator()
            {
                return new Enumerator(dictionary);
            }

            public void CopyTo(TValue[] array, int index)
            {
                if (array == null)
                {
                    throw new ArgumentNullException("array");
                }

                if (index < 0 || index > array.Length)
                {
                    throw new ArgumentOutOfRangeException("index", SR.ArgumentOutOfRange_NeedNonNegNum);
                }

                if (array.Length - index < dictionary.Count)
                {
                    throw new ArgumentException(SR.Arg_ArrayPlusOffTooSmall);
                }

                int count = dictionary.count;
                Entry[] entries = dictionary.entries;
                TValue[] values = dictionary.valueArray;

                for (int i = 0; i < count; i++)
                {
                    if (entries[i].hashCode >= 0)
                    {
                        array[index++] = values[i];
                    }
                }
            }

            public int Count
            {
                get { return dictionary.Count; }
            }

            bool ICollection<TValue>.IsReadOnly
            {
                get { return true; }
            }

            void ICollection<TValue>.Add(TValue item)
            {
                throw new NotSupportedException(SR.NotSupported_ValueCollectionSet);
            }

            bool ICollection<TValue>.Remove(TValue item)
            {
                throw new NotSupportedException(SR.NotSupported_ValueCollectionSet);
            }

            void ICollection<TValue>.Clear()
            {
                throw new NotSupportedException(SR.NotSupported_ValueCollectionSet);
            }

            bool ICollection<TValue>.Contains(TValue item)
            {
                return dictionary.ContainsValue(item);
            }

            IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator()
            {
                return new Enumerator(dictionary);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new Enumerator(dictionary);
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

                if (array.Length - index < dictionary.Count)
                    throw new ArgumentException(SR.Arg_ArrayPlusOffTooSmall);

                TValue[] values = array as TValue[];

                if (values != null)
                {
                    CopyTo(values, index);
                }
                else
                {
                    object[] objects = array as object[];

                    if (objects == null)
                    {
                        throw new ArgumentException(SR.Argument_InvalidArrayType);
                    }

                    int count = dictionary.count;
                    Entry[] entries = dictionary.entries;
                    TValue[] vs = dictionary.valueArray;

                    try
                    {
                        for (int i = 0; i < count; i++)
                        {
                            if (entries[i].hashCode >= 0)
                            {
                                objects[index++] = vs[i];
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
                get { return ((ICollection)dictionary).SyncRoot; }
            }

            public struct Enumerator : IEnumerator<TValue>, System.Collections.IEnumerator
            {
                private Dictionary<TKey, TValue> dictionary;
                private int index;
                private int version;
                private TValue currentValue;

                internal Enumerator(Dictionary<TKey, TValue> dictionary)
                {
                    this.dictionary = dictionary;
                    version = dictionary.version;
                    index = 0;
                    currentValue = default(TValue);
                }

                public void Dispose()
                {
                }

                public bool MoveNext()
                {
                    if (version != dictionary.version)
                    {
                        throw new InvalidOperationException(SR.InvalidOperation_EnumFailedVersion);
                    }

                    while ((uint)index < (uint)dictionary.count)
                    {
                        if (dictionary.entries[index].hashCode >= 0)
                        {
                            currentValue = dictionary.valueArray[index];
                            index++;

                            return true;
                        }

                        index++;
                    }

                    index = dictionary.count + 1;
                    currentValue = default(TValue);

                    return false;
                }

                public TValue Current
                {
                    get
                    {
                        return currentValue;
                    }
                }

                Object System.Collections.IEnumerator.Current
                {
                    get
                    {
                        if (index == 0 || (index == dictionary.count + 1))
                        {
                            throw new InvalidOperationException(SR.InvalidOperation_EnumOpCantHappen);
                        }

                        return currentValue;
                    }
                }

                void System.Collections.IEnumerator.Reset()
                {
                    if (version != dictionary.version)
                    {
                        throw new InvalidOperationException(SR.InvalidOperation_EnumFailedVersion);
                    }

                    index = 0;
                    currentValue = default(TValue);
                }
            }
        }
    }
}
