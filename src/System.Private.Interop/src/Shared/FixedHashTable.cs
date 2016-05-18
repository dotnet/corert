// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic.Internal;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Runtime.InteropServices
{
#if RHTESTCL

    static class InteropExtensions1
    {
        public static bool IsNull(this RuntimeTypeHandle handle)
        {
            return handle.Equals(default(RuntimeTypeHandle));
        }

        public static bool IsOfType(this Object obj, RuntimeTypeHandle handle)
        {
            return handle.Equals(obj.GetType().TypeHandle);
        }
    }

#endif

    /// <summary>
    /// Simple fixed-size hash table. Create once and use to speed table lookup.
    /// Good for tool time generated data table lookup. The hash table itself be generated at tool time, but runtime generation will take less disk space
    ///
    /// 1. Size is given in constructor and never changed afterwards
    /// 2. Only add is supported, but remove can be added quite easily
    /// 3. For each entry, an integer index can be stored and received. If index is always the same as inserting order, this can be removed too.
    /// 4. Value is not stored. It should be managed separately
    /// 5. Non-generic, there is single copy in memory
    /// 6. Searching is implemented using two methods: GetFirst and GetNext
    ///
    /// Check StringMap below for a Dictionary<string, int> like implementation where strings are stored elsewhere, possibly in compressed form
    /// </summary>
    internal class FixedHashTable
    {
        const int slot_bucket = 0;
        const int slot_next = 1;
        const int slot_index = 2;

        int[] m_entries;
        int m_size;
        int m_count;

        /// <summary>
        /// Construct empty hash table
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        internal FixedHashTable(int size)
        {
            // Prime number is essential to reduce hash collision
            // Add 10%, minimum 11 to make sure hash table has around 10% free entries to reduce collision
            m_size = HashHelpers.GetPrime(Math.Max(11, size * 11 / 10));

            // Using int array instead of creating an Entry[] array with three ints to avoid
            // adding a new array type, which costs around 3kb in binary size
            m_entries = new int[m_size * 3];
        }

        /// <summary>
        /// Add an entry: Dictionay<K,V>.Add(Key(index), index) = > FixedHashTable.Add(Key(index).GetHashCode(), index)
        /// </summary>
        /// <param name="hashCode">Hash code for data[slot]</param>
        /// <param name="index">Normally index to external table</param>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        internal void Add(int hashCode, int index)
        {
            int bucket = (hashCode & 0x7FFFFFFF) % m_size;

            m_entries[m_count * 3 + slot_index] = index; // This is not needed if m_count === index

            m_entries[m_count * 3 + slot_next] = m_entries[bucket * 3 + slot_bucket];
            m_entries[bucket * 3 + slot_bucket] = m_count + 1; // 0 for missing now

            m_count++;
        }

        /// <summary>
        /// Get first matching entry based on hash code for enumeration, -1 for missing
        /// </summary>
        internal int GetFirst(int hashCode)
        {
            int bucket = (hashCode & 0x7FFFFFFF) % m_size;

            return m_entries[bucket * 3 + slot_bucket] - 1;
        }

        internal int GetIndex(int bucket)
        {
            return m_entries[bucket * 3 + slot_index];
        }

        /// <summary>
        /// Get next entry for enumeration, -1 for missing
        /// </summary>
        internal int GetNext(int bucket)
        {
            return m_entries[bucket * 3 + slot_next] - 1;
        }
    }

    /// <summary>
    /// Virtual Dictionary<string, int> where strings are stored elsewhere, possibly in compressed form
    /// </summary>
    internal abstract class StringMap
    {
        int m_size;
        FixedHashTable m_map;

        internal StringMap(int size)
        {
            m_size = size;
        }

        internal abstract String GetString(int i);

        /// <summary>
        /// String(i).GetHashCode
        /// </summary>
        internal abstract int GetStringHash(int i);

        /// <summary>
        /// String(i) == name
        /// </summary>
        internal abstract bool IsStringEqual(string name, int i);

        /// <summary>
        /// Dictionary.TryGetValue(string)
        /// </summary>
        internal int FindString(string name)
        {
            if (m_map == null)
            {
                FixedHashTable map = new FixedHashTable(m_size);

                for (int i = 0; i < m_size; i++)
                {
                    map.Add(GetStringHash(i), i);
                }

                m_map = map;
            }

            int hash = StringPool.StableStringHash(name);

            // Search hash table
            for (int slot = m_map.GetFirst(hash); slot >= 0; slot = m_map.GetNext(slot))
            {
                int index = m_map.GetIndex(slot);

                if (IsStringEqual(name, index))
                {
                    return index;
                }
            }

            return -1;
        }
    }

    /// <summary>
    /// StringMap using 16-bit indices, for normal applications
    /// </summary>
    internal class StringMap16 : StringMap
    {
        StringPool m_pool;
        UInt16[] m_indices;

        internal StringMap16(StringPool pool, UInt16[] indices) : base(indices.Length)
        {
            m_pool = pool;
            m_indices = indices;
        }

        internal override string GetString(int i)
        {
            return m_pool.GetString(m_indices[i]);
        }

        internal override int GetStringHash(int i)
        {
            return m_pool.StableStringHash(m_indices[i]);
        }

        internal override bool IsStringEqual(string name, int i)
        {
            return m_pool.IsStringEqual(name, m_indices[i]);
        }
    }

    /// <summary>
    /// StringMap using 32-bit indices, for bigger applications
    /// </summary>
    internal class StringMap32 : StringMap
    {
        StringPool m_pool;
        UInt32[] m_indices;

        internal StringMap32(StringPool pool, UInt32[] indices) : base(indices.Length)
        {
            m_pool = pool;
            m_indices = indices;
        }

        internal override string GetString(int i)
        {
            return m_pool.GetString(m_indices[i]);
        }

        internal override int GetStringHash(int i)
        {
            return m_pool.StableStringHash(m_indices[i]);
        }

        internal override bool IsStringEqual(string name, int i)
        {
            return m_pool.IsStringEqual(name, m_indices[i]);
        }
    }


    /// <summary>
    /// Fixed Dictionary<RuntimeTypeHandle, int> using delegate Func<int, RuntimeTypeHandle> to provide data
    /// </summary>
    internal class RuntimeTypeHandleMap : FixedHashTable
    {
        Func<int, RuntimeTypeHandle> m_getHandle;

        internal RuntimeTypeHandleMap(int size, Func<int, RuntimeTypeHandle> getHandle) : base(size)
        {
            m_getHandle = getHandle;

            for (int i = 0; i < size; i++)
            {
                RuntimeTypeHandle handle = getHandle(i);

                if (!handle.Equals(McgModule.s_DependencyReductionTypeRemovedTypeHandle))
                {
                    Add(handle.GetHashCode(), i);
                }
            }
        }

        internal int Lookup(RuntimeTypeHandle handle)
        {
            for (int slot = GetFirst(handle.GetHashCode()); slot >= 0; slot = GetNext(slot))
            {
                int index = GetIndex(slot);

                if (handle.Equals(m_getHandle(index)))
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
