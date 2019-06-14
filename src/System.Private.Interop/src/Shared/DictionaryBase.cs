// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Collections.Generic.Internal
{
    internal class SR
    {
        internal static string ArgumentOutOfRange_NeedNonNegNum = "ArgumentOutOfRange_NeedNonNegNum";
        internal static string Arg_WrongType = "Arg_WrongType";
        internal static string Arg_ArrayPlusOffTooSmall = "Arg_ArrayPlusOffTooSmall";
        internal static string Arg_RankMultiDimNotSupported = "Arg_RankMultiDimNotSupported";
        internal static string Arg_NonZeroLowerBound = "Arg_NonZeroLowerBound";
        internal static string Argument_InvalidArrayType = "Argument_InvalidArrayType";
        internal static string Argument_AddingDuplicate = "Argument_AddingDuplicate";
        internal static string InvalidOperation_EnumFailedVersion = "InvalidOperation_EnumFailedVersion";
        internal static string InvalidOperation_EnumOpCantHappen = "InvalidOperation_EnumOpCantHappen";
        internal static string NotSupported_KeyCollectionSet = "NotSupported_KeyCollectionSet";
        internal static string NotSupported_ValueCollectionSet = "NotSupported_ValueCollectionSet";
        internal static string ArgumentOutOfRange_SmallCapacity = "ArgumentOutOfRange_SmallCapacity";
        internal static string Argument_InvalidOffLen = "Argument_InvalidOffLen";
    }

    internal class HashHelpers
    {
        public static readonly int[] primes = {
            3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239, 293, 353, 431, 521, 631, 761, 919,
            1103, 1327, 1597, 1931, 2333, 2801, 3371, 4049, 4861, 5839, 7013, 8419, 10103, 12143, 14591,
            17519, 21023, 25229, 30293, 36353, 43627, 52361, 62851, 75431, 90523, 108631, 130363, 156437,
            187751, 225307, 270371, 324449, 389357, 467237, 560689, 672827, 807403, 968897, 1162687, 1395263,
            1674319, 2009191, 2411033, 2893249, 3471899, 4166287, 4999559, 5999471, 7199369, 8639249, 10367101,
            12440537, 14928671, 17914409, 21497293, 25796759, 30956117, 37147349, 44576837, 53492207, 64190669,
            77028803, 92434613, 110921543, 133105859, 159727031, 191672443, 230006941, 276008387, 331210079,
            397452101, 476942527, 572331049, 686797261, 824156741, 988988137, 1186785773, 1424142949, 1708971541,
            2050765853, MaxPrimeArrayLength };

        public static int GetPrime(int min)
        {
            if (min < 0)
            {
                throw new ArgumentException("Arg_HTCapacityOverflow");
            }

            for (int i = 0; i < primes.Length; i++)
            {
                int prime = primes[i];
                if (prime >= min) return prime;
            }

            throw new ArgumentException("Arg_HTCapacityOverflow");
        }

        // Returns size of hashtable to grow to.
        public static int ExpandPrime(int oldSize)
        {
            int newSize = 2 * oldSize;

            // Allow the hashtables to grow to maximum possible size (~2G elements) before encoutering capacity overflow.
            // Note that this check works even when _items.Length overflowed thanks to the (uint) cast
            if ((uint)newSize > MaxPrimeArrayLength && MaxPrimeArrayLength > oldSize)
            {
                Debug.Assert(MaxPrimeArrayLength == GetPrime(MaxPrimeArrayLength), "Invalid MaxPrimeArrayLength");

                return MaxPrimeArrayLength;
            }

            return GetPrime(newSize);
        }

        // This is the maximum prime smaller than Array.MaxArrayLength
        public const int MaxPrimeArrayLength = 0x7FEFFFFD;
    }

    // Non-generic part of Dictionary, single copy
    internal class DictionaryBase
    {
        protected struct Entry
        {
            public int hashCode;    // Lower 31 bits of hash code, -1 if unused
            public int next;        // Index of next entry, -1 if last
            public int bucket;
        }

        protected int count;
        protected int version;

        protected int freeList;
        protected int freeCount;

        protected Entry[] entries;

        public int Count
        {
            get
            {
                return count - freeCount;
            }
        }

        /// <summary>
        /// Allocate entry array, clear bucket
        /// </summary>
        protected int InitializeBase(int capacity)
        {
            int size = HashHelpers.GetPrime(capacity);

            entries = new Entry[size];

            for (int i = 0; i < entries.Length; i++)
            {
                entries[i].bucket = -1;
            }

            freeList = -1;

            return size;
        }

        /// <summary>
        /// Clear entry array, bucket, counts
        /// </summary>
        protected void ClearBase()
        {
            Array.Clear(entries, 0, count);

            for (int i = 0; i < entries.Length; i++)
            {
                entries[i].bucket = -1;
            }

            freeList = -1;
            count = 0;
            freeCount = 0;
            version++;
        }

        /// <summary>
        /// Resize entry array, clean bucket, but do not set entries yet
        /// </summary>
        protected Entry[] ResizeBase1(int newSize)
        {
            Entry[] newEntries = new Entry[newSize];
            Array.Copy(entries, 0, newEntries, 0, count);

            for (int i = 0; i < newEntries.Length; i++)
            {
                newEntries[i].bucket = -1;
            }

            return newEntries;
        }

        /// <summary>
        /// Relink buckets, set new entry array
        /// </summary>
        protected void ResizeBase2(Entry[] newEntries, int newSize)
        {
            for (int i = 0; i < count; i++)
            {
                int bucket = newEntries[i].hashCode % newSize;
                newEntries[i].next = newEntries[bucket].bucket;
                newEntries[bucket].bucket = i;
            }

            entries = newEntries;
        }

#if !RHTESTCL
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
#endif
        protected int ModLength(int hashCode)
        {
            // uint % operator is faster than int % operator
            return (int)((uint)hashCode % (uint)entries.Length);
        }

        /// <summary>
        /// Find the first entry with hashCode
        /// </summary>
        protected int FindFirstEntry(int hashCode)
        {
            if (entries != null)
            {
                hashCode = hashCode & 0x7FFFFFFF;

                for (int i = entries[ModLength(hashCode)].bucket; i >= 0; i = entries[i].next)
                {
                    if (entries[i].hashCode == hashCode)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// Find the next entry with the same hashCode as entry
        /// </summary>
        protected int FindNextEntry(int entry)
        {
            if ((entry >= 0) && entries != null)
            {
                int hashCode = entries[entry].hashCode;

                for (int i = entries[entry].next; i >= 0; i = entries[i].next)
                {
                    if (entries[i].hashCode == hashCode)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }
    }
}
