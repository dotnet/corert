// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*============================================================
**
**
**
** Purpose: This class will encapsulate an uint and 
**          provide an Object representation of it.
**
** 
===========================================================*/

using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Diagnostics.Contracts;

namespace System
{
    // * Wrapper for unsigned 32 bit integers.
    [CLSCompliant(false)]
    [System.Runtime.InteropServices.StructLayout(LayoutKind.Sequential)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public struct UInt32 : IComparable, IFormattable, IComparable<UInt32>, IEquatable<UInt32>, IConvertible
    {
        private uint _value;

        public const uint MaxValue = (uint)0xffffffff;
        public const uint MinValue = 0U;


        // Compares this object to another object, returning an integer that
        // indicates the relationship. 
        // Returns a value less than zero if this  object
        // null is considered to be less than any instance.
        // If object is not of type UInt32, this method throws an ArgumentException.
        // 
        int IComparable.CompareTo(Object value)
        {
            if (value == null)
            {
                return 1;
            }
            if (value is UInt32)
            {
                // Need to use compare because subtraction will wrap
                // to positive for very large neg numbers, etc.
                uint i = (uint)value;
                if (_value < i) return -1;
                if (_value > i) return 1;
                return 0;
            }
            throw new ArgumentException(SR.Arg_MustBeUInt32);
        }

        public int CompareTo(UInt32 value)
        {
            // Need to use compare because subtraction will wrap
            // to positive for very large neg numbers, etc.
            if (_value < value) return -1;
            if (_value > value) return 1;
            return 0;
        }

        public override bool Equals(Object obj)
        {
            if (!(obj is UInt32))
            {
                return false;
            }
            return _value == ((UInt32)obj)._value;
        }

        [NonVersionable]
        public bool Equals(UInt32 obj)
        {
            return _value == obj;
        }

        // The absolute value of the int contained.
        public override int GetHashCode()
        {
            return ((int)_value);
        }

        // The base 10 representation of the number with no extra padding.
        public override String ToString()
        {
            Contract.Ensures(Contract.Result<String>() != null);
            return FormatProvider.FormatUInt32(_value, null, null);
        }

        public String ToString(IFormatProvider provider)
        {
            Contract.Ensures(Contract.Result<String>() != null);
            return FormatProvider.FormatUInt32(_value, null, provider);
        }

        public String ToString(String format)
        {
            Contract.Ensures(Contract.Result<String>() != null);
            return FormatProvider.FormatUInt32(_value, format, null);
        }

        public String ToString(String format, IFormatProvider provider)
        {
            Contract.Ensures(Contract.Result<String>() != null);
            return FormatProvider.FormatUInt32(_value, format, provider);
        }

        [CLSCompliant(false)]
        public static uint Parse(String s)
        {
            return FormatProvider.ParseUInt32(s, NumberStyles.Integer, null);
        }

        internal static void ValidateParseStyleInteger(NumberStyles style)
        {
            // Check for undefined flags
            if ((style & Decimal.InvalidNumberStyles) != 0)
            {
                throw new ArgumentException(SR.Argument_InvalidNumberStyles, "style");
            }
            Contract.EndContractBlock();
            if ((style & NumberStyles.AllowHexSpecifier) != 0)
            { // Check for hex number
                if ((style & ~NumberStyles.HexNumber) != 0)
                {
                    throw new ArgumentException(SR.Arg_InvalidHexStyle);
                }
            }
        }

        [CLSCompliant(false)]
        public static uint Parse(String s, NumberStyles style)
        {
            ValidateParseStyleInteger(style);
            return FormatProvider.ParseUInt32(s, style, null);
        }


        [CLSCompliant(false)]
        public static uint Parse(String s, IFormatProvider provider)
        {
            return FormatProvider.ParseUInt32(s, NumberStyles.Integer, provider);
        }

        [CLSCompliant(false)]
        public static uint Parse(String s, NumberStyles style, IFormatProvider provider)
        {
            ValidateParseStyleInteger(style);
            return FormatProvider.ParseUInt32(s, style, provider);
        }

        [CLSCompliant(false)]
        public static bool TryParse(String s, out UInt32 result)
        {
            return FormatProvider.TryParseUInt32(s, NumberStyles.Integer, null, out result);
        }

        [CLSCompliant(false)]
        public static bool TryParse(String s, NumberStyles style, IFormatProvider provider, out UInt32 result)
        {
            ValidateParseStyleInteger(style);
            return FormatProvider.TryParseUInt32(s, style, provider, out result);
        }

        //
        // IConvertible implementation
        // 

        TypeCode IConvertible.GetTypeCode()
        {
            return TypeCode.UInt32;
        }

        /// <internalonly/>
        bool IConvertible.ToBoolean(IFormatProvider provider)
        {
            return Convert.ToBoolean(_value);
        }

        /// <internalonly/>
        char IConvertible.ToChar(IFormatProvider provider)
        {
            return Convert.ToChar(_value);
        }

        /// <internalonly/>
        sbyte IConvertible.ToSByte(IFormatProvider provider)
        {
            return Convert.ToSByte(_value);
        }

        /// <internalonly/>
        byte IConvertible.ToByte(IFormatProvider provider)
        {
            return Convert.ToByte(_value);
        }

        /// <internalonly/>
        short IConvertible.ToInt16(IFormatProvider provider)
        {
            return Convert.ToInt16(_value);
        }

        /// <internalonly/>
        ushort IConvertible.ToUInt16(IFormatProvider provider)
        {
            return Convert.ToUInt16(_value);
        }

        /// <internalonly/>
        int IConvertible.ToInt32(IFormatProvider provider)
        {
            return Convert.ToInt32(_value);
        }

        /// <internalonly/>
        uint IConvertible.ToUInt32(IFormatProvider provider)
        {
            return _value;
        }

        /// <internalonly/>
        long IConvertible.ToInt64(IFormatProvider provider)
        {
            return Convert.ToInt64(_value);
        }

        /// <internalonly/>
        ulong IConvertible.ToUInt64(IFormatProvider provider)
        {
            return Convert.ToUInt64(_value);
        }

        /// <internalonly/>
        float IConvertible.ToSingle(IFormatProvider provider)
        {
            return Convert.ToSingle(_value);
        }

        /// <internalonly/>
        double IConvertible.ToDouble(IFormatProvider provider)
        {
            return Convert.ToDouble(_value);
        }

        /// <internalonly/>
        Decimal IConvertible.ToDecimal(IFormatProvider provider)
        {
            return Convert.ToDecimal(_value);
        }

        /// <internalonly/>
        DateTime IConvertible.ToDateTime(IFormatProvider provider)
        {
            throw new InvalidCastException(String.Format(SR.InvalidCast_FromTo, "UInt32", "DateTime"));
        }

        /// <internalonly/>
        Object IConvertible.ToType(Type type, IFormatProvider provider)
        {
            return Convert.DefaultToType((IConvertible)this, type, provider);
        }
    }
}
