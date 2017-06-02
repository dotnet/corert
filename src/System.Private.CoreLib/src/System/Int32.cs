// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*============================================================
**
** Purpose: A representation of a 32 bit 2's complement 
**          integer.
** 
===========================================================*/

using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Diagnostics.Contracts;

namespace System
{
    // CONTRACT with Runtime
    // The Int32 type is one of the primitives understood by the compilers and runtime
    // Data Contract: Single field of type int
    // This type is LayoutKind Sequential

    [System.Runtime.InteropServices.StructLayout(LayoutKind.Sequential)]
    [Serializable]
    public struct Int32 : IComparable, IFormattable, IComparable<Int32>, IEquatable<Int32>, IConvertible
    {
        // _value is never assigned to by any of the methods.
        // Disabling the warning as this type is a built-in primitive that the compilers know about
#pragma warning disable 0649
        private int _value;
#pragma warning restore 0649

        public const int MaxValue = 0x7fffffff;
        public const int MinValue = unchecked((int)0x80000000);

        // Compares this object to another object, returning an integer that
        // indicates the relationship. 
        // Returns :
        // 0 if the values are equal
        // Negative number if _value is less than value
        // Positive number if _value is more than value
        // null is considered to be less than any instance, hence returns positive number
        // If object is not of type Int32, this method throws an ArgumentException.
        // 
        public int CompareTo(Object value)
        {
            if (value == null)
            {
                return 1;
            }
            if (value is Int32)
            {
                // NOTE: Cannot use return (_value - value) as this causes a wrap
                // around in cases where _value - value > MaxValue.
                int i = (int)value;
                if (_value < i) return -1;
                if (_value > i) return 1;
                return 0;
            }
            throw new ArgumentException(SR.Arg_MustBeInt32);
        }

        public int CompareTo(int value)
        {
            // NOTE: Cannot use return (_value - value) as this causes a wrap
            // around in cases where _value - value > MaxValue.
            if (_value < value) return -1;
            if (_value > value) return 1;
            return 0;
        }

        public override bool Equals(Object obj)
        {
            if (!(obj is Int32))
            {
                return false;
            }
            return _value == ((Int32)obj)._value;
        }

        [NonVersionable]
        public bool Equals(Int32 obj)
        {
            return _value == obj;
        }

        // The absolute value of the int contained.
        public override int GetHashCode()
        {
            return _value;
        }

        [Pure]
        public override String ToString()
        {
            Contract.Ensures(Contract.Result<String>() != null);
            return FormatProvider.FormatInt32(_value, null, null);
        }

        [Pure]
        public String ToString(String format)
        {
            Contract.Ensures(Contract.Result<String>() != null);
            return FormatProvider.FormatInt32(_value, format, null);
        }

        [Pure]
        public String ToString(IFormatProvider provider)
        {
            Contract.Ensures(Contract.Result<String>() != null);
            return FormatProvider.FormatInt32(_value, null, provider);
        }

        [Pure]
        public String ToString(String format, IFormatProvider provider)
        {
            Contract.Ensures(Contract.Result<String>() != null);
            return FormatProvider.FormatInt32(_value, format, provider);
        }

        // Parses an integer from a String in the given style.  If
        // a NumberFormatInfo isn't specified, the current culture's 
        // NumberFormatInfo is assumed.
        [Pure]
        public static int Parse(String s)
        {
            return FormatProvider.ParseInt32(s, NumberStyles.Integer, null);
        }

        [Pure]
        public static int Parse(String s, NumberStyles style)
        {
            UInt32.ValidateParseStyleInteger(style);
            return FormatProvider.ParseInt32(s, style, null);
        }

        // Parses an integer from a String in the given style.  If
        // a NumberFormatInfo isn't specified, the current culture's 
        // NumberFormatInfo is assumed.
        // 
        [Pure]
        public static int Parse(String s, IFormatProvider provider)
        {
            return FormatProvider.ParseInt32(s, NumberStyles.Integer, provider);
        }

        // Parses an integer from a String in the given style.  If
        // a NumberFormatInfo isn't specified, the current culture's 
        // NumberFormatInfo is assumed.
        // 
        [Pure]
        public static int Parse(String s, NumberStyles style, IFormatProvider provider)
        {
            UInt32.ValidateParseStyleInteger(style);
            return FormatProvider.ParseInt32(s, style, provider);
        }

        // Parses an integer from a String. Returns false rather
        // than throwing exceptin if input is invalid
        // 
        [Pure]
        public static bool TryParse(String s, out Int32 result)
        {
            return FormatProvider.TryParseInt32(s, NumberStyles.Integer, null, out result);
        }

        // Parses an integer from a String in the given style. Returns false rather
        // than throwing exceptin if input is invalid
        // 
        [Pure]
        public static bool TryParse(String s, NumberStyles style, IFormatProvider provider, out Int32 result)
        {
            UInt32.ValidateParseStyleInteger(style);
            return FormatProvider.TryParseInt32(s, style, provider, out result);
        }

        //
        // IConvertible implementation
        // 

        [Pure]
        public TypeCode GetTypeCode()
        {
            return TypeCode.Int32;
        }

        bool IConvertible.ToBoolean(IFormatProvider provider)
        {
            return Convert.ToBoolean(_value);
        }

        char IConvertible.ToChar(IFormatProvider provider)
        {
            return Convert.ToChar(_value);
        }

        sbyte IConvertible.ToSByte(IFormatProvider provider)
        {
            return Convert.ToSByte(_value);
        }

        byte IConvertible.ToByte(IFormatProvider provider)
        {
            return Convert.ToByte(_value);
        }

        short IConvertible.ToInt16(IFormatProvider provider)
        {
            return Convert.ToInt16(_value);
        }

        ushort IConvertible.ToUInt16(IFormatProvider provider)
        {
            return Convert.ToUInt16(_value);
        }

        int IConvertible.ToInt32(IFormatProvider provider)
        {
            return _value;
        }

        uint IConvertible.ToUInt32(IFormatProvider provider)
        {
            return Convert.ToUInt32(_value);
        }

        long IConvertible.ToInt64(IFormatProvider provider)
        {
            return Convert.ToInt64(_value);
        }

        ulong IConvertible.ToUInt64(IFormatProvider provider)
        {
            return Convert.ToUInt64(_value);
        }

        float IConvertible.ToSingle(IFormatProvider provider)
        {
            return Convert.ToSingle(_value);
        }

        double IConvertible.ToDouble(IFormatProvider provider)
        {
            return Convert.ToDouble(_value);
        }

        Decimal IConvertible.ToDecimal(IFormatProvider provider)
        {
            return Convert.ToDecimal(_value);
        }

        DateTime IConvertible.ToDateTime(IFormatProvider provider)
        {
            throw new InvalidCastException(String.Format(SR.InvalidCast_FromTo, "Int32", "DateTime"));
        }

        Object IConvertible.ToType(Type type, IFormatProvider provider)
        {
            return Convert.DefaultToType((IConvertible)this, type, provider);
        }
    }
}
