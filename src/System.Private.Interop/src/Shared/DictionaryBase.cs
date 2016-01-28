// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace System.Collections.Generic.Internal
{
    internal class SR
    {
        static internal string ArgumentOutOfRange_NeedNonNegNum = "ArgumentOutOfRange_NeedNonNegNum";
        static internal string Arg_WrongType = "Arg_WrongType";
        static internal string Arg_ArrayPlusOffTooSmall = "Arg_ArrayPlusOffTooSmall";
        static internal string Arg_RankMultiDimNotSupported = "Arg_RankMultiDimNotSupported";
        static internal string Arg_NonZeroLowerBound = "Arg_NonZeroLowerBound";
        static internal string Argument_InvalidArrayType = "Argument_InvalidArrayType";
        static internal string Argument_AddingDuplicate = "Argument_AddingDuplicate";
        static internal string InvalidOperation_EnumFailedVersion = "InvalidOperation_EnumFailedVersion";
        static internal string InvalidOperation_EnumOpCantHappen = "InvalidOperation_EnumOpCantHappen";
        static internal string NotSupported_KeyCollectionSet = "NotSupported_KeyCollectionSet";
        static internal string NotSupported_ValueCollectionSet = "NotSupported_ValueCollectionSet";
        static internal string ArgumentOutOfRange_SmallCapacity = "ArgumentOutOfRange_SmallCapacity";
        static internal string Argument_InvalidOffLen = "Argument_InvalidOffLen";
    }

    internal class HashHelpers
    {
        private const Int32 HashPrime = 101;

        public static bool IsPrime(int candidate)
        {
            if ((candidate & 1) != 0)
            {
                for (int divisor = 3; divisor * divisor < candidate; divisor += 2)
                {
                    if ((candidate % divisor) == 0)
                        return false;
                }

                return true;
            }

            return (candidate == 2);
        }

        public static int GetPrime(int min)
        {
            if (min < 0)
            {
                throw new ArgumentException("Arg_HTCapacityOverflow");
            }

            for (int i = (min | 1); i < Int32.MaxValue; i += 2)
            {
                if (((i - 1) % HashPrime != 0) && IsPrime(i))
                {
                    return i;
                }
            }

            return min;
        }

        // Returns size of hashtable to grow to.
        public static int ExpandPrime(int oldSize)
        {
            int newSize = 2 * oldSize;

            // Allow the hashtables to grow to maximum possible size (~2G elements) before encoutering capacity overflow.
            // Note that this check works even when _items.Length overflowed thanks to the (uint) cast
            if ((uint)newSize > MaxPrimeArrayLength && MaxPrimeArrayLength > oldSize)
            {
                Contract.Assert(MaxPrimeArrayLength == GetPrime(MaxPrimeArrayLength), "Invalid MaxPrimeArrayLength");

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
