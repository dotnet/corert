// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// ----------------------------------------------------------------------------------
// Interop library code
//
// NOTE:
//   These source code are being published to InternalAPIs and consumed by RH builds
//   Use PublishInteropAPI.bat to keep the InternalAPI copies in sync
// ----------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Text;
using System.Runtime;
using Internal.NativeFormat;

namespace System.Runtime.InteropServices
{
    [StructLayout(LayoutKind.Sequential)]
    public struct HSTRING
    {
        public IntPtr handle;

        public HSTRING(IntPtr hndl)
        {
            handle = hndl;
        }
    }

    // 24 a bit overkill in x86 (which needs 20) but it doesn't really matter as we always allocate
    // it in stack
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public struct HSTRING_HEADER
    {
    }

    /// <summary>
    /// The native DECIMAL struct
    /// The reason we have our own DECIMAL struct is for the 8-byte alignment caused by the ulong, other than
    /// that it is considered blittable. Note that managed System.Decimal is 4-byte aligned
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DECIMAL
    {
        ulong Hi64;
        ulong Lo64;

        internal unsafe DECIMAL(Decimal dec)
        {
            ulong* p = (ulong*)&dec;
            Hi64 = *p;
            Lo64 = *(p + 1);
        }

        internal unsafe Decimal ToDecimal()
        {
            Decimal dec;
            ulong* p = (ulong*)&dec;
            *p = Hi64;
            *(p + 1) = Lo64;

            return dec;
        }

        public static implicit operator Decimal(DECIMAL dec)
        {
            return dec.ToDecimal();
        }

        public static implicit operator DECIMAL(Decimal dec)
        {
            return new DECIMAL(dec);
        }
    }

    internal enum TypeKind
    {
        Primitive,
        Metadata,
        Custom,
    }

#pragma warning disable 649 // The fields of TypeName are never used in RhTestCl, but still need to be there
    internal struct TypeName
    {
        public HSTRING Name;
        public TypeKind Kind;
    }
#pragma warning restore 649

    internal enum TrustLevel
    {
        BaseTrust = 0,
        PartialTrust = 1,
        FullTrust = 2
    };

    //
    // An array-based list type built for fast concurrent access.  We support only adding items and enumerating
    // existing items.  However, enumerations can run conconcurrently on multiple threads, even while a write is
    // in progress.  (We only allow a single write at a time.)
    //
    // LightweightList is a struct to avoid extra object allocations.  However, be careful not to copy these around
    // accidentally.  That will result in very bad behavior.
    //
    // An additional nested type, LightweightList<T>.WithInlineStorage, provides space for one item in the struct itself.
    // For lists that often contain just one item, this eliminates allocations altogether.
    //
    internal struct LightweightList<T>
    {
        //
        // Holds the data.  When we run out of capacity, we double the size of the array.
        //
        T[] m_array;

        //
        // Bit 0: set if there is a thread modifying this list.  Only one writer is allowed at any given time.
        // Remaining bits: the number of items in the list.
        //
        int m_countAndLock;

        const int InitialCapacity = 2;

        private void AssertLockHeld()
        {
            Debug.Assert((Volatile.Read(ref m_countAndLock) & 1) == 1);
        }

        private int AcquireLockAndGetCount()
        {
            SpinWait spin = new SpinWait();

            while (true)
            {
                int oldCountAndLock = Volatile.Read(ref m_countAndLock);

                if ((oldCountAndLock & 1) == 0)
                {
                    if (Interlocked.CompareExchange(ref m_countAndLock, oldCountAndLock | 1, oldCountAndLock) == oldCountAndLock)
                    {
                        return oldCountAndLock >> 1;
                    }
                }

                spin.SpinOnce();
            }
        }

        private void ReleaseLockAndSetCount(int newCount)
        {
            AssertLockHeld();

            //
            // Volatile write to prevent reordering with previous writes.
            //
            Volatile.Write(ref m_countAndLock, newCount << 1);
        }

        private void SetArrayElement(int index, T value)
        {
            AssertLockHeld();

            T[] oldArray = m_array;
            T[] newArray;

            if (oldArray == null)
            {
                newArray = new T[InitialCapacity];
            }
            else if (oldArray.Length <= index)
            {
                newArray = new T[oldArray.Length * 2];

                for (int i = 0; i < oldArray.Length; i++)
                {
                    newArray[i] = oldArray[i];
                }
            }
            else
            {
                newArray = oldArray;
            }

            newArray[index] = value;

            if (newArray != oldArray)
            {
                //
                // Volatile write to ensure all previous writes are complete.
                //
                Volatile.Write(ref m_array, newArray);
            }
        }

        private void GetArrayAndCount(out T[] array, out int count)
        {
            //
            // Read the count, then the array.  We have to do it in this order, so that we won't read
            // a count that's larger than array we previously found.  Note that the read from m_countAndLock
            // is volatile, preventing reordering with the subsequent read from m_array.
            //
            count = Volatile.Read(ref m_countAndLock) >> 1;
            array = m_array;
        }

        public void Add(T value)
        {
            int oldCount = AcquireLockAndGetCount();
            SetArrayElement(oldCount, value);
            ReleaseLockAndSetCount(oldCount + 1);
        }

        public Enumerator GetEnumerator()
        {
            T[] array;
            int count;
            GetArrayAndCount(out array, out count);

            return new Enumerator(count, array);
        }

        public struct Enumerator
        {
            int m_current;
            int m_count;
            T[] m_array;

            internal Enumerator(int count, T[] array)
            {
                m_current = -1;
                m_count = count;
                m_array = array;
            }

            public bool MoveNext()
            {
                return ++m_current < m_count;
            }

            public T Current
            {
                get
                {
                    Debug.Assert(m_array != null);
                    return m_array[m_current];
                }
            }
        }

        //
        // Wrapper around LightweightList to provide a single slot of inline storage.  This prevents allocations
        // for lists of just one item.
        //
        public struct WithInlineStorage
        {
            /// <summary>
            /// NOTE: Managed debugger depends on field name: "m_list" and field type:LightweightList
            /// Update managed debugger whenever field name/field type is changed.
            /// See CordbObjectValue::GetInterfaceData in debug\dbi\values.cpp
            /// </summary>
            LightweightList<T> m_list;
            /// <summary>
            /// NOTE: Managed debugger depends on field name: "m_item0"
            /// Update managed debugger whenever field name/field type is changed.
            /// See CordbObjectValue::GetInterfaceData in debug\dbi\values.cpp
            /// </summary>
            T m_item0;

            public void Add(T value)
            {
                int oldCount = m_list.AcquireLockAndGetCount();

                if (oldCount == 0)
                    m_item0 = value;
                else
                    m_list.SetArrayElement(oldCount - 1, value);

                m_list.ReleaseLockAndSetCount(oldCount + 1);
            }

            /// <summary>
            /// Add first entry for a new ComCallableObject, no locking needed
            /// </summary>
            /// <param name="value"></param>
            public void AddFirst(T value)
            {
                Debug.Assert(m_list.m_countAndLock == 0);

                m_item0 = value;

                m_list.m_countAndLock = 1 << 1;
            }

            public Enumerator GetEnumerator()
            {
                T[] array;
                int count;
                m_list.GetArrayAndCount(out array, out count);
                T item0 = m_item0;

                return new Enumerator(count, array, item0);
            }

            public struct Enumerator
            {
                int m_current;
                int m_count;
                T[] m_array;
                T m_item0;

                internal Enumerator(int count, T[] array, T item0)
                {
                    m_current = -1;
                    m_count = count;
                    m_array = array;
                    m_item0 = item0;
                }

                public bool MoveNext()
                {
                    return ++m_current < m_count;
                }

                public T Current
                {
                    get
                    {
                        if (m_current == 0)
                        {
                            return m_item0;
                        }
                        else
                        {
                            Debug.Assert(m_array != null);
                            return m_array[m_current - 1];
                        }
                    }
                }
            }
        }
    }

    internal enum InterfaceCheckResult
    {
        Supported,
        Rejected,
        NotFound
    }
}

namespace System.Runtime.InteropServices.WindowsRuntime
{
    public interface IManagedActivationFactory
    {
        void RunClassConstructor();
    }
}
