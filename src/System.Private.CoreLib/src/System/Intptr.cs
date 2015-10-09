// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*============================================================
**
**
**
** Purpose: Platform independent integer
**
** 
===========================================================*/

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security;

namespace System
{
    // CONTRACT with Runtime
    // The IntPtr type is one of the primitives understood by the compilers and runtime
    // Data Contract: Single field of type void *

    public struct IntPtr
    {
        unsafe private void* _value; // The compiler treats void* closest to uint hence explicit casts are required to preserve int behavior

        [Intrinsic]
        public static readonly IntPtr Zero;

        [Intrinsic]
        public extern IntPtr(int value);

        [Intrinsic]
        public extern IntPtr(long value);

        [CLSCompliant(false)]
        [Intrinsic]
        [SecurityCritical] // required to match contract
        public extern unsafe IntPtr(void* value);

        [CLSCompliant(false)]
        [Intrinsic]
        public unsafe void* ToPointer()
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return ToPointer();
        }

        [Intrinsic]
        public int ToInt32()
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return ToInt32();
        }

        [Intrinsic]
        public long ToInt64()
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return ToInt64();
        }

        [Intrinsic]
        public static explicit operator IntPtr(int value)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return (IntPtr)value;
        }

        [Intrinsic]
        public static explicit operator IntPtr(long value)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return (IntPtr)value;
        }


        [CLSCompliant(false)]
        [Intrinsic]
        [SecurityCritical] // required to match contract
        public static unsafe explicit operator IntPtr(void* value)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return (IntPtr)value;
        }

        [CLSCompliant(false)]
        [Intrinsic]
        public static unsafe explicit operator void* (IntPtr value)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return (void*)value;
        }

        [Intrinsic]
        public unsafe static explicit operator int (IntPtr value)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return (int)value;
        }


        [Intrinsic]
        public static explicit operator long (IntPtr value)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return (long)value;
        }

        [Intrinsic]
        public static bool operator ==(IntPtr value1, IntPtr value2)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return value1 == value2;
        }

        [Intrinsic]
        public unsafe static bool operator !=(IntPtr value1, IntPtr value2)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return value1 != value2;
        }

        internal unsafe bool IsNull()
        {
            return (_value == null);
        }

        public static IntPtr Add(IntPtr pointer, int offset)
        {
            return pointer + offset;
        }

        [Intrinsic]
        public static IntPtr operator +(IntPtr pointer, int offset)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return pointer + offset;
        }

        public static IntPtr Subtract(IntPtr pointer, int offset)
        {
            return pointer - offset;
        }

        [Intrinsic]
        public static IntPtr operator -(IntPtr pointer, int offset)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return pointer - offset;
        }

        public static unsafe int Size
        {
            // Have to provide a body since csc doesn't like extern properties
            // on value types
            [Intrinsic(IgnoreBody = true)]
            get
            { return sizeof(IntPtr); }
        }

        public unsafe override String ToString()
        {
#if WIN32
            return ((int)_value).ToString(FormatProvider.InvariantCulture);
#else
            return ((long)_value).ToString(FormatProvider.InvariantCulture);
#endif
        }

        public unsafe String ToString(String format)
        {
#if WIN32
            return ((int)_value).ToString(format, FormatProvider.InvariantCulture);
#else
            return ((long)_value).ToString(format, FormatProvider.InvariantCulture);
#endif
        }

        public unsafe override bool Equals(Object obj)
        {
            if (obj is IntPtr)
            {
                return (_value == ((IntPtr)obj)._value);
            }
            return false;
        }

        public unsafe override int GetHashCode()
        {
            // QUESTION: This HashCode seems to neglect the high order bits in calculating the hashcode?
            return unchecked((int)((long)_value));
        }
    }
}


