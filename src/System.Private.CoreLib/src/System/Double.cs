// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*============================================================
**
**
**
** Purpose: A representation of an IEEE double precision
**          floating point number.
**
**
===========================================================*/

using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Diagnostics.Contracts;

namespace System
{
    [StructLayout(LayoutKind.Sequential)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public struct Double : IComparable, IFormattable, IComparable<Double>, IEquatable<Double>, IConvertible
    {
        internal double m_value;

        //
        // Public Constants
        //
        public const double MinValue = -1.7976931348623157E+308;
        public const double MaxValue = 1.7976931348623157E+308;

        // Note Epsilon should be a double whose hex representation is 0x1
        // on little endian machines.
        public const double Epsilon = 4.9406564584124654E-324;
        public const double NegativeInfinity = (double)-1.0 / (double)(0.0);
        public const double PositiveInfinity = (double)1.0 / (double)(0.0);
        public const double NaN = (double)0.0 / (double)0.0;

        // 0x8000000000000000 is exactly same as -0.0. We use this explicit definition to avoid the confusion between 0.0 and -0.0.
        internal static double NegativeZero = Int64BitsToDouble(unchecked((long)0x8000000000000000));

        private static unsafe double Int64BitsToDouble(long value)
        {
            return *((double*)&value);
        }

        [Pure]
        public unsafe static bool IsInfinity(double d)
        {
            return (*(long*)(&d) & 0x7FFFFFFFFFFFFFFF) == 0x7FF0000000000000;
        }

        [Pure]
        public static bool IsPositiveInfinity(double d)
        {
            //Jit will generate inlineable code with this
            if (d == double.PositiveInfinity)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        [Pure]
        public static bool IsNegativeInfinity(double d)
        {
            //Jit will generate inlineable code with this
            if (d == double.NegativeInfinity)
            {
                return true;
            }
            else
            {
                return false;
            }
        }


        [Pure]
        internal unsafe static bool IsNegative(double d)
        {
            return (*(UInt64*)(&d) & 0x8000000000000000) == 0x8000000000000000;
        }


        [Pure]
        public unsafe static bool IsNaN(double d)
        {
            return (*(UInt64*)(&d) & 0x7FFFFFFFFFFFFFFFL) > 0x7FF0000000000000L;
        }

        // Compares this object to another object, returning an instance of System.Relation.
        // Null is considered less than any instance.
        //
        // If object is not of type Double, this method throws an ArgumentException.
        //
        // Returns a value less than zero if this  object
        //
        int IComparable.CompareTo(Object value)
        {
            if (value == null)
            {
                return 1;
            }
            if (value is Double)
            {
                double d = (double)value;
                if (m_value < d) return -1;
                if (m_value > d) return 1;
                if (m_value == d) return 0;

                // At least one of the values is NaN.
                if (IsNaN(m_value))
                    return (IsNaN(d) ? 0 : -1);
                else
                    return 1;
            }
            throw new ArgumentException(SR.Arg_MustBeDouble);
        }

        public int CompareTo(Double value)
        {
            if (m_value < value) return -1;
            if (m_value > value) return 1;
            if (m_value == value) return 0;

            // At least one of the values is NaN.
            if (IsNaN(m_value))
                return (IsNaN(value) ? 0 : -1);
            else
                return 1;
        }

        // True if obj is another Double with the same value as the current instance.  This is
        // a method of object equality, that only returns true if obj is also a double.
        public override bool Equals(Object obj)
        {
            if (!(obj is Double))
            {
                return false;
            }
            double temp = ((Double)obj).m_value;
            // This code below is written this way for performance reasons i.e the != and == check is intentional.
            if (temp == m_value)
            {
                return true;
            }
            return IsNaN(temp) && IsNaN(m_value);
        }

        [NonVersionable]
        public static bool operator ==(Double left, Double right)
        {
            return left == right;
        }

        [NonVersionable]
        public static bool operator !=(Double left, Double right)
        {
            return left != right;
        }

        [NonVersionable]
        public static bool operator <(Double left, Double right)
        {
            return left < right;
        }

        [NonVersionable]
        public static bool operator >(Double left, Double right)
        {
            return left > right;
        }

        [NonVersionable]
        public static bool operator <=(Double left, Double right)
        {
            return left <= right;
        }

        [NonVersionable]
        public static bool operator >=(Double left, Double right)
        {
            return left >= right;
        }

        public bool Equals(Double obj)
        {
            if (obj == m_value)
            {
                return true;
            }
            return IsNaN(obj) && IsNaN(m_value);
        }

        //The hashcode for a double is the absolute value of the integer representation
        //of that double.
        //
        public unsafe override int GetHashCode()
        {
            double d = m_value;
            if (d == 0)
            {
                // Ensure that 0 and -0 have the same hash code
                return 0;
            }
            long value = *(long*)(&d);
            return unchecked((int)value) ^ ((int)(value >> 32));
        }

        public override String ToString()
        {
            Contract.Ensures(Contract.Result<String>() != null);
            return FormatProvider.FormatDouble(m_value, null, null);
        }

        public String ToString(String format)
        {
            Contract.Ensures(Contract.Result<String>() != null);
            return FormatProvider.FormatDouble(m_value, format, null);
        }

        public String ToString(IFormatProvider provider)
        {
            Contract.Ensures(Contract.Result<String>() != null);
            return FormatProvider.FormatDouble(m_value, null, provider);
        }

        public String ToString(String format, IFormatProvider provider)
        {
            Contract.Ensures(Contract.Result<String>() != null);
            return FormatProvider.FormatDouble(m_value, format, provider);
        }

        public static double Parse(String s)
        {
            return Parse(s, NumberStyles.Float | NumberStyles.AllowThousands, null);
        }

        public static double Parse(String s, NumberStyles style)
        {
            Decimal.ValidateParseStyleFloatingPoint(style);
            return Parse(s, style, null);
        }

        public static double Parse(String s, IFormatProvider provider)
        {
            return Parse(s, NumberStyles.Float | NumberStyles.AllowThousands, provider);
        }

        public static double Parse(String s, NumberStyles style, IFormatProvider provider)
        {
            Decimal.ValidateParseStyleFloatingPoint(style);
            return FormatProvider.ParseDouble(s, style, provider);
        }

        // Parses a double from a String in the given style.  If
        // a NumberFormatInfo isn't specified, the current culture's
        // NumberFormatInfo is assumed.
        //
        // This method will not throw an OverflowException, but will return
        // PositiveInfinity or NegativeInfinity for a number that is too
        // large or too small.
        //
        public static bool TryParse(String s, out double result)
        {
            return TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, null, out result);
        }

        public static bool TryParse(String s, NumberStyles style, IFormatProvider provider, out double result)
        {
            Decimal.ValidateParseStyleFloatingPoint(style);
            if (s == null)
            {
                result = 0;
                return false;
            }
            bool success = FormatProvider.TryParseDouble(s, style, provider, out result);
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

        TypeCode IConvertible.GetTypeCode()
        {
            return TypeCode.Double;
        }

        /// <internalonly/>
        bool IConvertible.ToBoolean(IFormatProvider provider)
        {
            return Convert.ToBoolean(m_value);
        }

        /// <internalonly/>
        char IConvertible.ToChar(IFormatProvider provider)
        {
            throw new InvalidCastException(String.Format(SR.InvalidCast_FromTo, "Double", "Char"));
        }

        /// <internalonly/>
        sbyte IConvertible.ToSByte(IFormatProvider provider)
        {
            return Convert.ToSByte(m_value);
        }

        /// <internalonly/>
        byte IConvertible.ToByte(IFormatProvider provider)
        {
            return Convert.ToByte(m_value);
        }

        /// <internalonly/>
        short IConvertible.ToInt16(IFormatProvider provider)
        {
            return Convert.ToInt16(m_value);
        }

        /// <internalonly/>
        ushort IConvertible.ToUInt16(IFormatProvider provider)
        {
            return Convert.ToUInt16(m_value);
        }

        /// <internalonly/>
        int IConvertible.ToInt32(IFormatProvider provider)
        {
            return Convert.ToInt32(m_value);
        }

        /// <internalonly/>
        uint IConvertible.ToUInt32(IFormatProvider provider)
        {
            return Convert.ToUInt32(m_value);
        }

        /// <internalonly/>
        long IConvertible.ToInt64(IFormatProvider provider)
        {
            return Convert.ToInt64(m_value);
        }

        /// <internalonly/>
        ulong IConvertible.ToUInt64(IFormatProvider provider)
        {
            return Convert.ToUInt64(m_value);
        }

        /// <internalonly/>
        float IConvertible.ToSingle(IFormatProvider provider)
        {
            return Convert.ToSingle(m_value);
        }

        /// <internalonly/>
        double IConvertible.ToDouble(IFormatProvider provider)
        {
            return m_value;
        }

        /// <internalonly/>
        Decimal IConvertible.ToDecimal(IFormatProvider provider)
        {
            return Convert.ToDecimal(m_value);
        }

        /// <internalonly/>
        DateTime IConvertible.ToDateTime(IFormatProvider provider)
        {
            throw new InvalidCastException(String.Format(SR.InvalidCast_FromTo, "Double", "DateTime"));
        }

        /// <internalonly/>
        Object IConvertible.ToType(Type type, IFormatProvider provider)
        {
            return Convert.DefaultToType((IConvertible)this, type, provider);
        }
    }
}
