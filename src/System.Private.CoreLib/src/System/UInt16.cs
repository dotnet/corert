// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*============================================================
**
**
** Purpose: This class will encapsulate a short and provide an
**          Object representation of it.
**
** 
===========================================================*/

using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Diagnostics.Contracts;

namespace System
{
    // Wrapper for unsigned 16 bit integers.
    [CLSCompliant(false)]
    [System.Runtime.InteropServices.StructLayout(LayoutKind.Sequential)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public struct UInt16 : IComparable, IFormattable, IComparable<UInt16>, IEquatable<UInt16>, IConvertible
    {
        private ushort _value;

        public const ushort MaxValue = (ushort)0xFFFF;
        public const ushort MinValue = 0;


        // Compares this object to another object, returning an integer that
        // indicates the relationship. 
        // Returns a value less than zero if this  object
        // null is considered to be less than any instance.
        // If object is not of type UInt16, this method throws an ArgumentException.
        // 
        int IComparable.CompareTo(Object value)
        {
            if (value == null)
            {
                return 1;
            }
            if (value is UInt16)
            {
                return ((int)_value - (int)(((UInt16)value)._value));
            }
            throw new ArgumentException(SR.Arg_MustBeUInt16);
        }

        public int CompareTo(UInt16 value)
        {
            return ((int)_value - (int)value);
        }

        public override bool Equals(Object obj)
        {
            if (!(obj is UInt16))
            {
                return false;
            }
            return _value == ((UInt16)obj)._value;
        }

        [NonVersionable]
        public bool Equals(UInt16 obj)
        {
            return _value == obj;
        }

        // Returns a HashCode for the UInt16
        public override int GetHashCode()
        {
            return (int)_value;
        }

        // Converts the current value to a String in base-10 with no extra padding.
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
        public static ushort Parse(String s)
        {
            return Parse(s, NumberStyles.Integer, null);
        }

        [CLSCompliant(false)]
        public static ushort Parse(String s, NumberStyles style)
        {
            UInt32.ValidateParseStyleInteger(style);
            return Parse(s, style, null);
        }


        [CLSCompliant(false)]
        public static ushort Parse(String s, IFormatProvider provider)
        {
            return Parse(s, NumberStyles.Integer, provider);
        }

        [CLSCompliant(false)]
        public static ushort Parse(String s, NumberStyles style, IFormatProvider provider)
        {
            UInt32.ValidateParseStyleInteger(style);
            uint i = 0;
            try
            {
                i = FormatProvider.ParseUInt32(s, style, provider);
            }
            catch (OverflowException e)
            {
                throw new OverflowException(SR.Overflow_UInt16, e);
            }

            if (i > MaxValue) throw new OverflowException(SR.Overflow_UInt16);
            return (ushort)i;
        }

        [CLSCompliant(false)]
        public static bool TryParse(String s, out UInt16 result)
        {
            return TryParse(s, NumberStyles.Integer, null, out result);
        }

        [CLSCompliant(false)]
        public static bool TryParse(String s, NumberStyles style, IFormatProvider provider, out UInt16 result)
        {
            UInt32.ValidateParseStyleInteger(style);
            result = 0;
            UInt32 i;
            if (!FormatProvider.TryParseUInt32(s, style, provider, out i))
            {
                return false;
            }
            if (i > MaxValue)
            {
                return false;
            }
            result = (UInt16)i;
            return true;
        }

        //
        // IConvertible implementation
        // 

        TypeCode IConvertible.GetTypeCode()
        {
            return TypeCode.UInt16;
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
            return _value;
        }

        /// <internalonly/>
        int IConvertible.ToInt32(IFormatProvider provider)
        {
            return Convert.ToInt32(_value);
        }

        /// <internalonly/>
        uint IConvertible.ToUInt32(IFormatProvider provider)
        {
            return Convert.ToUInt32(_value);
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
            throw new InvalidCastException(String.Format(SR.InvalidCast_FromTo, "UInt16", "DateTime"));
        }

        /// <internalonly/>
        Object IConvertible.ToType(Type type, IFormatProvider provider)
        {
            return Convert.DefaultToType((IConvertible)this, type, provider);
        }
    }
}
