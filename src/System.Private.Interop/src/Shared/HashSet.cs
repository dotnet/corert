// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

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

        public HashSet(int capacity)
        {
            if (capacity < MinimalSize)
            {
                capacity = MinimalSize;
            }

            Initialize(capacity);
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
    }
}
