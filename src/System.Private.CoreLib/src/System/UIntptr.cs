// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
using System.Runtime.Versioning;
using System.Security;

namespace System
{
    // CONTRACT with Runtime
    // The UIntPtr type is one of the primitives understood by the compilers and runtime
    // Data Contract: Single field of type void *
    [CLSCompliant(false)]
    public struct UIntPtr : IEquatable<UIntPtr>
    {
        unsafe private void* _value;

        [Intrinsic]
        public static readonly UIntPtr Zero;

        [Intrinsic]
        [NonVersionable]
        public unsafe UIntPtr(uint value)
        {
            _value = (void*)value;
        }

        [Intrinsic]
        [NonVersionable]
        public unsafe UIntPtr(ulong value)
        {
#if BIT64
            _value = (void*)value;
#else
            _value = (void*)checked((uint)value);
#endif
        }

        [Intrinsic]
        [NonVersionable]
        public unsafe UIntPtr(void* value)
        {
            _value = value;
        }

        [Intrinsic]
        [NonVersionable]
        public unsafe void* ToPointer()
        {
            return _value;
        }

        [Intrinsic]
        [NonVersionable]
        public unsafe uint ToUInt32()
        {
#if BIT64
            return checked((uint)_value);
#else
            return (uint)_value;
#endif
        }

        [Intrinsic]
        [NonVersionable]
        public unsafe ulong ToUInt64()
        {
            return (ulong)_value;
        }

        [Intrinsic]
        [NonVersionable]
        public static explicit operator UIntPtr(uint value)
        {
            return new UIntPtr(value);
        }

        [Intrinsic]
        [NonVersionable]
        public static explicit operator UIntPtr(ulong value)
        {
            return new UIntPtr(value);
        }

        [Intrinsic]
        [NonVersionable]
        public static unsafe explicit operator UIntPtr(void* value)
        {
            return new UIntPtr(value);
        }

        [Intrinsic]
        [NonVersionable]
        public static unsafe explicit operator void* (UIntPtr value)
        {
            return value._value;
        }

        [Intrinsic]
        [NonVersionable]
        public unsafe static explicit operator uint (UIntPtr value)
        {
#if BIT64
            return checked((uint)value._value);
#else
            return (uint)value._value;
#endif
        }

        [Intrinsic]
        [NonVersionable]
        public unsafe static explicit operator ulong (UIntPtr value)
        {
            return (ulong)value._value;
        }

        unsafe bool IEquatable<UIntPtr>.Equals(UIntPtr value)
        {
            return _value == value._value;
        }

        [Intrinsic]
        [NonVersionable]
        public unsafe static bool operator ==(UIntPtr value1, UIntPtr value2)
        {
            return value1._value == value2._value;
        }

        [Intrinsic]
        [NonVersionable]
        public unsafe static bool operator !=(UIntPtr value1, UIntPtr value2)
        {
            return value1._value != value2._value;
        }

        public static unsafe int Size
        {
            [Intrinsic]
            [NonVersionable]
            get
            {
#if BIT64
                return 8;
#else
                return 4;
#endif
            }
        }

        public unsafe override String ToString()
        {
#if BIT64
            return ((ulong)_value).ToString(FormatProvider.InvariantCulture);
#else
            return ((uint)_value).ToString(FormatProvider.InvariantCulture);
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
#if BIT64
            ulong l = (ulong)_value;
            return (unchecked((int)l) ^ (int)(l >> 32));
#else
            return unchecked((int)_value);
#endif
        }

        [NonVersionable]
        public static UIntPtr Add(UIntPtr pointer, int offset)
        {
            return pointer + offset;
        }

        [Intrinsic]
        [NonVersionable]
        public static UIntPtr operator +(UIntPtr pointer, int offset)
        {
#if BIT64
            return new UIntPtr(pointer.ToUInt64() + (ulong)offset);
#else
            return new UIntPtr(pointer.ToUInt32() + (uint)offset);
#endif
        }

        [NonVersionable]
        public static UIntPtr Subtract(UIntPtr pointer, int offset)
        {
            return pointer - offset;
        }

        [Intrinsic]
        [NonVersionable]
        public static UIntPtr operator -(UIntPtr pointer, int offset)
        {
#if BIT64
            return new UIntPtr(pointer.ToUInt64() - (ulong)offset);
#else
            return new UIntPtr(pointer.ToUInt32() - (uint)offset);
#endif
        }
    }
}


