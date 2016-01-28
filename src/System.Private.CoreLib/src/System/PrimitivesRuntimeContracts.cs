// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


// This file contains the basic primitive type definitions (int etc)
// These types are well known to the compiler and the runtime and are basic interchange types that do not change

// CONTRACT with Runtime
// Each of the data types has a data contract with the runtime. See the contract in the type definition
//

using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace System
{
    // CONTRACT with Runtime
    // Place holder type for type hierarchy, Compiler/Runtime requires this class
    public abstract class ValueType
    {
    }

    // CONTRACT with Runtime, Compiler/Runtime requires this class
    // Place holder type for type hierarchy
    public abstract class Enum : ValueType
    {
    }

    /*============================================================
    **
    ** Class:  Boolean
    **
    **
    ** Purpose: The boolean class serves as a wrapper for the primitive
    ** type boolean.
    **
    ** 
    ===========================================================*/

    // CONTRACT with Runtime
    // The Boolean type is one of the primitives understood by the compilers and runtime
    // Data Contract: Single field of type bool

    public struct Boolean
    {
        private bool m_value;
    }


    /*============================================================
    **
    ** Class:  Char
    **
    **
    ** Purpose: This is the value class representing a Unicode character
    **
    **
    ===========================================================*/



    // CONTRACT with Runtime
    // The Char type is one of the primitives understood by the compilers and runtime
    // Data Contract: Single field of type char
    // This type is LayoutKind Sequential

    [StructLayout(LayoutKind.Sequential)]
    public struct Char
    {
        private char m_value;
    }


    /*============================================================
    **
    ** Class:  SByte
    **
    **
    ** Purpose: A representation of a 8 bit 2's complement integer.
    **
    ** 
    ===========================================================*/

    // CONTRACT with Runtime
    // The SByte type is one of the primitives understood by the compilers and runtime
    // Data Contract: Single field of type sbyte
    // This type is LayoutKind Sequential

    [StructLayout(LayoutKind.Sequential)]
    public struct SByte
    {
        private sbyte m_value;
    }


    /*============================================================
    **
    ** Class:  Byte
    **
    **
    ** Purpose: A representation of a 8 bit integer (byte)
    **
    ** 
    ===========================================================*/


    // CONTRACT with Runtime
    // The Byte type is one of the primitives understood by the compilers and runtime
    // Data Contract: Single field of type bool
    // This type is LayoutKind Sequential

    [StructLayout(LayoutKind.Sequential)]
    public struct Byte
    {
        private byte m_value;
    }


    /*============================================================
    **
    ** Class:  Int16
    **
    **
    ** Purpose: A representation of a 16 bit 2's complement integer.
    **
    ** 
    ===========================================================*/


    // CONTRACT with Runtime
    // The Int16 type is one of the primitives understood by the compilers and runtime
    // Data Contract: Single field of type short
    // This type is LayoutKind Sequential

    [StructLayout(LayoutKind.Sequential)]
    public struct Int16
    {
        private short m_value;
    }

    /*============================================================
    **
    ** Class:  UInt16
    **
    **
    ** Purpose: A representation of a short (unsigned 16-bit) integer.
    **
    ** 
    ===========================================================*/

    // CONTRACT with Runtime
    // The Uint16 type is one of the primitives understood by the compilers and runtime
    // Data Contract: Single field of type ushort
    // This type is LayoutKind Sequential

    [StructLayout(LayoutKind.Sequential)]
    public struct UInt16
    {
        private ushort m_value;
    }

    /*============================================================
    **
    ** Class:  Int32
    **
    **
    ** Purpose: A representation of a 32 bit 2's complement integer.
    **
    ** 
    ===========================================================*/

    // CONTRACT with Runtime
    // The Int32 type is one of the primitives understood by the compilers and runtime
    // Data Contract: Single field of type int
    // This type is LayoutKind Sequential

    [StructLayout(LayoutKind.Sequential)]
    public struct Int32
    {
        private int m_value;
    }


    /*============================================================
    **
    ** Class:  UInt32
    **
    **
    ** Purpose: A representation of a 32 bit unsigned integer.
    **
    ** 
    ===========================================================*/

    // CONTRACT with Runtime
    // The Uint32 type is one of the primitives understood by the compilers and runtime
    // Data Contract: Single field of type uint
    // This type is LayoutKind Sequential

    [StructLayout(LayoutKind.Sequential)]
    public struct UInt32
    {
        private uint m_value;
    }


    /*============================================================
    **
    ** Class:  Int64
    **
    **
    ** Purpose: A representation of a 64 bit 2's complement integer.
    **
    ** 
    ===========================================================*/

    // CONTRACT with Runtime
    // The Int64 type is one of the primitives understood by the compilers and runtime
    // Data Contract: Single field of type long
    // This type is LayoutKind Sequential

    [StructLayout(LayoutKind.Sequential)]
    public struct Int64
    {
        private long m_value;
    }


    /*============================================================
    **
    ** Class:  UInt64
    **
    **
    ** Purpose: A representation of a 64 bit unsigned integer.
    **
    ** 
    ===========================================================*/

    // CONTRACT with Runtime
    // The UInt64 type is one of the primitives understood by the compilers and runtime
    // Data Contract: Single field of type ulong
    // This type is LayoutKind Sequential

    [StructLayout(LayoutKind.Sequential)]
    public struct UInt64
    {
        private ulong m_value;
    }


    /*============================================================
    **
    ** Class:  Single
    **
    **
    ** Purpose: A wrapper class for the primitive type float.
    **
    **
    ===========================================================*/

    // CONTRACT with Runtime
    // The Single type is one of the primitives understood by the compilers and runtime
    // Data Contract: Single field of type float
    // This type is LayoutKind Sequential

    [StructLayout(LayoutKind.Sequential)]
    public struct Single
    {
        private float m_value;
    }


    /*============================================================
    **
    ** Class:  Double
    **
    **
    ** Purpose: A representation of an IEEE double precision
    **          floating point number.
    **
    **
    ===========================================================*/

    // CONTRACT with Runtime
    // The Double type is one of the primitives understood by the compilers and runtime
    // Data Contract: Single field of type double
    // This type is LayoutKind Sequential

    [StructLayout(LayoutKind.Sequential)]
    public struct Double
    {
        private double m_value;
    }



    /*============================================================
    **
    ** Class:  IntPtr
    **
    **
    ** Purpose: Platform independent integer
    **
    ** 
    ===========================================================*/

    // CONTRACT with Runtime
    // The IntPtr type is one of the primitives understood by the compilers and runtime
    // Data Contract: Single field of type void *

    // This type implements == without overriding GetHashCode, disable compiler warning
#pragma warning disable 0659, 0661
    public struct IntPtr
    {
        unsafe private void* m_value; // The compiler treats void* closest to uint hence explicit casts are required to preserve int behavior

        public static readonly IntPtr Zero;

        public unsafe IntPtr(void* value)
        {
            m_value = value;
        }

        public unsafe IntPtr(long value)
        {
#if BIT64
            m_value = (void*)value;
#else
            m_value = (void*)checked((int)value);
#endif
        }

        public unsafe override bool Equals(Object obj)
        {
            if (obj is IntPtr)
            {
                return (m_value == ((IntPtr)obj).m_value);
            }
            return false;
        }

        public unsafe bool Equals(IntPtr obj)
        {
            return (m_value == obj.m_value);
        }

        public static unsafe explicit operator IntPtr(void* value)
        {
            return new IntPtr(value);
        }

        public unsafe static explicit operator long (IntPtr value)
        {
#if BIT64
            return (long)value.m_value;
#else
            return (long)(int)value.m_value;
#endif
        }

        public unsafe static bool operator ==(IntPtr value1, IntPtr value2)
        {
            return value1.m_value == value2.m_value;
        }

        public unsafe static bool operator !=(IntPtr value1, IntPtr value2)
        {
            return value1.m_value != value2.m_value;
        }

        public unsafe void* ToPointer()
        {
            return m_value;
        }
    }
#pragma warning restore 0659, 0661


    /*============================================================
    **
    ** Class:  UIntPtr
    **
    **
    ** Purpose: Platform independent integer
    **
    ** 
    ===========================================================*/

    // CONTRACT with Runtime
    // The UIntPtr type is one of the primitives understood by the compilers and runtime
    // Data Contract: Single field of type void *

    public struct UIntPtr
    {
        // Disable compile warning about unused m_value field
#pragma warning disable 0169
        unsafe private void* m_value;
#pragma warning restore 0169

        public static readonly UIntPtr Zero;
    }

    // Decimal class is not supported in RH. Only here to keep compiler happy
    [TypeNeededIn(TypeNeededInOptions.SOURCE)]
    internal struct Decimal
    {
    }
}

