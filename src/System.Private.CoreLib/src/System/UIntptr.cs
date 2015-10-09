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
    // The UIntPtr type is one of the primitives understood by the compilers and runtime
    // Data Contract: Single field of type void *
    [CLSCompliant(false)]
    public struct UIntPtr
    {
        unsafe private void* _value;

        [Intrinsic]
        public static readonly UIntPtr Zero;

        [Intrinsic]
        public extern unsafe UIntPtr(uint value);

        [Intrinsic]
        public extern unsafe UIntPtr(ulong value);

        [Intrinsic]
        [SecurityCritical] // required to match contract
        public extern unsafe UIntPtr(void* value);

        [Intrinsic]
        public unsafe void* ToPointer()
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return ToPointer();
        }

        [Intrinsic]
        public uint ToUInt32()
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return ToUInt32();
        }

        [Intrinsic]
        public ulong ToUInt64()
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return ToUInt64();
        }

        [Intrinsic]
        public static explicit operator UIntPtr(uint value)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return (UIntPtr)value;
        }

        [Intrinsic]
        public static explicit operator UIntPtr(ulong value)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return (UIntPtr)value;
        }


        [Intrinsic]
        [SecurityCritical] // required to match contract
        public static unsafe explicit operator UIntPtr(void* value)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return (UIntPtr)value;
        }

        [Intrinsic]
        [SecurityCritical] // required to match contract
        public static unsafe explicit operator void* (UIntPtr value)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return (void*)value;
        }

        [Intrinsic]
        public static explicit operator uint (UIntPtr value)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return (uint)value;
        }

        [Intrinsic]
        public static explicit operator ulong (UIntPtr value)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return (ulong)value;
        }

        [Intrinsic]
        public unsafe static bool operator ==(UIntPtr value1, UIntPtr value2)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return value1 == value2;
        }

        [Intrinsic]
        public unsafe static bool operator !=(UIntPtr value1, UIntPtr value2)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return value1 != value2;
        }

        public static unsafe int Size
        {
            // Have to provide a body since csc doesn't like extern properties
            // on value types
            [Intrinsic(IgnoreBody = true)]
            get
            { return sizeof(UIntPtr); }
        }

        public unsafe override String ToString()
        {
#if WIN32
            return ((uint)_value).ToString(FormatProvider.InvariantCulture);
#else
            return ((ulong)_value).ToString(FormatProvider.InvariantCulture);
#endif
        }

        public unsafe override bool Equals(Object obj)
        {
            if (obj is UIntPtr)
            {
                return (_value == ((UIntPtr)obj)._value);
            }
            return false;
        }

        public unsafe override int GetHashCode()
        {
            // QUESTION: This HashCode seems to neglect the high order bits in calculating the hashcode?
            return unchecked((int)((long)_value)) & 0x7fffffff;
        }

        public static UIntPtr Add(UIntPtr pointer, int offset)
        {
            return pointer + offset;
        }

        [Intrinsic]
        public static UIntPtr operator +(UIntPtr pointer, int offset)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return pointer + offset;
        }

        public static UIntPtr Subtract(UIntPtr pointer, int offset)
        {
            return pointer - offset;
        }

        [Intrinsic]
        public static UIntPtr operator -(UIntPtr pointer, int offset)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return pointer - offset;
        }
    }
}


