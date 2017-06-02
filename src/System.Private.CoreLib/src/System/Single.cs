// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*============================================================
**
**
**
** Purpose: A wrapper class for the primitive type float.
**
**
===========================================================*/

using System.Globalization;
using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Diagnostics.Contracts;

namespace System
{
    [System.Runtime.InteropServices.StructLayout(LayoutKind.Sequential)]
    [Serializable]
    public struct Single : IComparable, IFormattable, IComparable<Single>, IEquatable<Single>, IConvertible
    {
        private float _value;

        //
        // Public constants
        //
        public const float MinValue = (float)-3.40282346638528859e+38;
        public const float Epsilon = (float)1.4e-45;
        public const float MaxValue = (float)3.40282346638528859e+38;
        public const float PositiveInfinity = (float)1.0 / (float)0.0;
        public const float NegativeInfinity = (float)-1.0 / (float)0.0;
        public const float NaN = (float)0.0 / (float)0.0;

        [Pure]
        public static unsafe bool IsInfinity(float f)
        {
            return (*(int*)(&f) & 0x7FFFFFFF) == 0x7F800000;
        }

        [Pure]
        public static unsafe bool IsPositiveInfinity(float f)
        {
            return *(int*)(&f) == 0x7F800000;
        }

        [Pure]
        public static unsafe bool IsNegativeInfinity(float f)
        {
            return *(int*)(&f) == unchecked((int)0xFF800000);
        }

        [Pure]
        public static unsafe bool IsNaN(float f)
        {
            return (*(int*)(&f) & 0x7FFFFFFF) > 0x7F800000;
        }

        // Compares this object to another object, returning an integer that
        // indicates the relationship.
        // Returns a value less than zero if this  object
        // null is considered to be less than any instance.
        // If object is not of type Single, this method throws an ArgumentException.
        //
        public int CompareTo(Object value)
        {
            if (value == null)
            {
                return 1;
            }
            if (value is Single)
            {
                float f = (float)value;
                if (_value < f) return -1;
                if (_value > f) return 1;
                if (_value == f) return 0;

                // At least one of the values is NaN.
                if (IsNaN(_value))
                    return (IsNaN(f) ? 0 : -1);
                else // f is NaN.
                    return 1;
            }
            throw new ArgumentException(SR.Arg_MustBeSingle);
        }


        public int CompareTo(Single value)
        {
            if (_value < value) return -1;
            if (_value > value) return 1;
            if (_value == value) return 0;

            // At least one of the values is NaN.
            if (IsNaN(_value))
                return (IsNaN(value) ? 0 : -1);
            else // f is NaN.
                return 1;
        }

        public static bool operator ==(Single left, Single right)
        {
            return left == right;
        }

        public static bool operator !=(Single left, Single right)
        {
            return left != right;
        }

        public static bool operator <(Single left, Single right)
        {
            return left < right;
        }

        public static bool operator >(Single left, Single right)
        {
            return left > right;
        }

        public static bool operator <=(Single left, Single right)
        {
            return left <= right;
        }

        public static bool operator >=(Single left, Single right)
        {
            return left >= right;
        }

        public override bool Equals(Object obj)
        {
            if (!(obj is Single))
            {
                return false;
            }
            float temp = ((Single)obj)._value;
            if (temp == _value)
            {
                return true;
            }

            return IsNaN(temp) && IsNaN(_value);
        }

        public bool Equals(Single obj)
        {
            if (obj == _value)
            {
                return true;
            }

            return IsNaN(obj) && IsNaN(_value);
        }

        public unsafe override int GetHashCode()
        {
            float f = _value;
            if (f == 0)
            {
                // Ensure that 0 and -0 have the same hash code
                return 0;
            }
            int v = *(int*)(&f);
            return v;
        }

        public override String ToString()
        {
            Contract.Ensures(Contract.Result<String>() != null);
            return FormatProvider.FormatSingle(_value, null, null);
        }

        public String ToString(IFormatProvider provider)
        {
            Contract.Ensures(Contract.Result<String>() != null);
            return FormatProvider.FormatSingle(_value, null, provider);
        }

        public String ToString(String format)
        {
            Contract.Ensures(Contract.Result<String>() != null);
            return FormatProvider.FormatSingle(_value, format, null);
        }

        public String ToString(String format, IFormatProvider provider)
        {
            Contract.Ensures(Contract.Result<String>() != null);
            return FormatProvider.FormatSingle(_value, format, provider);
        }

        // Parses a float from a String in the given style.  If
        // a NumberFormatInfo isn't specified, the current culture's
        // NumberFormatInfo is assumed.
        //
        // This method will not throw an OverflowException, but will return
        // PositiveInfinity or NegativeInfinity for a number that is too
        // large or too small.
        //
        public static float Parse(String s)
        {
            return Parse(s, NumberStyles.Float | NumberStyles.AllowThousands, null);
        }

        public static float Parse(String s, NumberStyles style)
        {
            Decimal.ValidateParseStyleFloatingPoint(style);
            return Parse(s, style, null);
        }

        public static float Parse(String s, IFormatProvider provider)
        {
            return Parse(s, NumberStyles.Float | NumberStyles.AllowThousands, provider);
        }

        public static float Parse(String s, NumberStyles style, IFormatProvider provider)
        {
            Decimal.ValidateParseStyleFloatingPoint(style);
            return FormatProvider.ParseSingle(s, style, provider);
        }

        public static Boolean TryParse(String s, out Single result)
        {
            return TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, null, out result);
        }

        public static Boolean TryParse(String s, NumberStyles style, IFormatProvider provider, out Single result)
        {
            Decimal.ValidateParseStyleFloatingPoint(style);
            if (s == null)
            {
                result = 0;
                return false;
            }
            bool success = FormatProvider.TryParseSingle(s, style, provider, out result);
            if (!success)
            {
                String sTrim = s.Trim();
                if (FormatProvider.IsPositiveInfinity(sTrim, provider))
                {
                    result = PositiveInfinity;
                }
                else if (FormatProvider.IsNegativeInfinity(sTrim, provider))
                {
                    result = NegativeInfinity;
                }
                else if (FormatProvider.IsNaNSymbol(sTrim, provider))
                {
                    result = NaN;
                }
                else
                    return false; // We really failed
            }
            return true;
        }

        //
        // IConvertible implementation
        //

        public TypeCode GetTypeCode()
        {
            return TypeCode.Single;
        }


        bool IConvertible.ToBoolean(IFormatProvider provider)
        {
            return Convert.ToBoolean(_value);
        }

        char IConvertible.ToChar(IFormatProvider provider)
        {
            throw new InvalidCastException(String.Format(SR.InvalidCast_FromTo, "Single", "Char"));
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
            return Convert.ToInt32(_value);
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
            return _value;
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
            throw new InvalidCastException(String.Format(SR.InvalidCast_FromTo, "Single", "DateTime"));
        }

        Object IConvertible.ToType(Type type, IFormatProvider provider)
        {
            return Convert.DefaultToType((IConvertible)this, type, provider);
        }
    }
}
