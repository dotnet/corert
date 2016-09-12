// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*============================================================
**
**
**
** Purpose: Home for static conversion methods.
**
**
===========================================================*/

using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;

namespace System
{
    [Flags]
    internal enum Base64FormattingOptions
    {
        None = 0,
        InsertLineBreaks = 1
    }

    // Returns the type code of this object. An implementation of this method
    // must not return TypeCode.Empty (which represents a null reference) or
    // TypeCode.Object (which represents an object that doesn't implement the
    // IConvertible interface). An implementation of this method should return
    // TypeCode.DBNull if the value of this object is a database null. For
    // example, a nullable integer type should return TypeCode.DBNull if the
    // value of the object is the database null. Otherwise, an implementation
    // of this method should return the TypeCode that best describes the
    // internal representation of the object.
    // The Value class provides conversion and querying methods for values. The
    // Value class contains static members only, and it is not possible to create
    // instances of the class.
    //
    // The statically typed conversion methods provided by the Value class are all
    // of the form:
    //
    //    public static XXX ToXXX(YYY value)
    //
    // where XXX is the target type and YYY is the source type. The matrix below
    // shows the set of supported conversions. The set of conversions is symmetric
    // such that for every ToXXX(YYY) there is also a ToYYY(XXX).
    //
    // From:  To: Bol Chr SBy Byt I16 U16 I32 U32 I64 U64 Sgl Dbl Dec Dat Str
    // ----------------------------------------------------------------------
    // Boolean     x       x   x   x   x   x   x   x   x   x   x   x       x
    // Char            x   x   x   x   x   x   x   x   x                   x
    // SByte       x   x   x   x   x   x   x   x   x   x   x   x   x       x
    // Byte        x   x   x   x   x   x   x   x   x   x   x   x   x       x
    // Int16       x   x   x   x   x   x   x   x   x   x   x   x   x       x
    // UInt16      x   x   x   x   x   x   x   x   x   x   x   x   x       x
    // Int32       x   x   x   x   x   x   x   x   x   x   x   x   x       x
    // UInt32      x   x   x   x   x   x   x   x   x   x   x   x   x       x
    // Int64       x   x   x   x   x   x   x   x   x   x   x   x   x       x
    // UInt64      x   x   x   x   x   x   x   x   x   x   x   x   x       x
    // Single      x       x   x   x   x   x   x   x   x   x   x   x       x
    // Double      x       x   x   x   x   x   x   x   x   x   x   x       x
    // Decimal     x       x   x   x   x   x   x   x   x   x   x   x       x
    // DateTime                                                        x   x
    // String      x   x   x   x   x   x   x   x   x   x   x   x   x   x   x
    // ----------------------------------------------------------------------
    //
    // For dynamic conversions, the Value class provides a set of methods of the
    // form:
    //
    //    public static XXX ToXXX(object value)
    //
    // where XXX is the target type (Boolean, Char, SByte, Byte, Int16, UInt16,
    // Int32, UInt32, Int64, UInt64, Single, Double, Decimal, DateTime,
    // or String). The implementations of these methods all take the form:
    //
    //    public static XXX toXXX(object value) {
    //        return value == null? XXX.Default: ((IConvertible)value).ToXXX();
    //    }
    //
    // The code first checks if the given value is a null reference (which is the
    // same as Value.Empty), in which case it returns the default value for type
    // XXX. Otherwise, a cast to IConvertible is performed, and the appropriate ToXXX()
    // method is invoked on the object. An InvalidCastException is thrown if the
    // cast to IConvertible fails, and that exception is simply allowed to propagate out
    // of the conversion method.

    // Constant representing the database null value. This value is used in
    // database applications to indicate the absense of a known value. Note
    // that Value.DBNull is NOT the same as a null object reference, which is
    // represented by Value.Empty.
    //
    // The Equals() method of DBNull always returns false, even when the
    // argument is itself DBNull.
    //
    // When passed Value.DBNull, the Value.GetTypeCode() method returns
    // TypeCode.DBNull.
    //
    // When passed Value.DBNull, the Value.ToXXX() methods all throw an
    // InvalidCastException.

    public static class Convert
    {
        private const int BooleanTrue = 1;
        private const int BooleanFalse = 0;

        internal static readonly char[] base64Table = {'A','B','C','D','E','F','G','H','I','J','K','L','M','N','O',
                                                       'P','Q','R','S','T','U','V','W','X','Y','Z','a','b','c','d',
                                                       'e','f','g','h','i','j','k','l','m','n','o','p','q','r','s',
                                                       't','u','v','w','x','y','z','0','1','2','3','4','5','6','7',
                                                       '8','9','+','/','=' };

        private const Int32 base64LineBreakPosition = 76;

        public static Object ChangeType(Object value, Type conversionType)
        {
            return ChangeType(value, conversionType, null);
        }

        public static Object ChangeType(Object value, Type conversionType, IFormatProvider provider)
        {
            if (conversionType == null)
            {
                throw new ArgumentNullException("conversionType");
            }

            Contract.EndContractBlock();

            if (value == null)
            {
                if (conversionType.TypeHandle.ToEETypePtr().IsValueType)
                {
                    throw new InvalidCastException(SR.InvalidCast_CannotCastNullToValueType);
                }
                return null;
            }

            IConvertible ic = value as IConvertible;
            if (ic == null)
            {
                if (value.GetType() == conversionType)
                    return value;

                throw new InvalidCastException(SR.InvalidCast_IConvertible);
            }

            if (conversionType == CommonRuntimeTypes.Boolean)
                return ic.ToBoolean(provider);
            else if (conversionType == CommonRuntimeTypes.Char)
                return ic.ToChar(provider);
            else if (conversionType == CommonRuntimeTypes.SByte)
                return ic.ToSByte(provider);
            else if (conversionType == CommonRuntimeTypes.Byte)
                return ic.ToByte(provider);
            else if (conversionType == CommonRuntimeTypes.Int16)
                return ic.ToInt16(provider);
            else if (conversionType == CommonRuntimeTypes.UInt16)
                return ic.ToUInt16(provider);
            else if (conversionType == CommonRuntimeTypes.Int32)
                return ic.ToInt32(provider);
            else if (conversionType == CommonRuntimeTypes.UInt32)
                return ic.ToUInt32(provider);
            else if (conversionType == CommonRuntimeTypes.Int64)
                return ic.ToInt64(provider);
            else if (conversionType == CommonRuntimeTypes.UInt64)
                return ic.ToUInt64(provider);
            else if (conversionType == CommonRuntimeTypes.Single)
                return ic.ToSingle(provider);
            else if (conversionType == CommonRuntimeTypes.Double)
                return ic.ToDouble(provider);
            else if (conversionType == CommonRuntimeTypes.Decimal)
                return ic.ToDecimal(provider);
            else if (conversionType == CommonRuntimeTypes.DateTime)
                return ic.ToDateTime(provider);
            else if (conversionType == CommonRuntimeTypes.String)
                return ic.ToString(provider);
            else if (conversionType == CommonRuntimeTypes.Object)
                return (Object)value;

            return ic.ToType(conversionType, provider);
        }

        public static Object ChangeType(Object value, TypeCode typeCode, IFormatProvider provider)
        {
            if (value == null && (typeCode == TypeCode.Empty || typeCode == TypeCode.String || typeCode == TypeCode.Object))
            {
                return null;
            }

            IConvertible v = value as IConvertible;
            if (v == null)
            {
                throw new InvalidCastException(SR.InvalidCast_IConvertible);
            }

            switch (typeCode)
            {
                case TypeCode.Boolean:
                    return v.ToBoolean(provider);
                case TypeCode.Char:
                    return v.ToChar(provider);
                case TypeCode.SByte:
                    return v.ToSByte(provider);
                case TypeCode.Byte:
                    return v.ToByte(provider);
                case TypeCode.Int16:
                    return v.ToInt16(provider);
                case TypeCode.UInt16:
                    return v.ToUInt16(provider);
                case TypeCode.Int32:
                    return v.ToInt32(provider);
                case TypeCode.UInt32:
                    return v.ToUInt32(provider);
                case TypeCode.Int64:
                    return v.ToInt64(provider);
                case TypeCode.UInt64:
                    return v.ToUInt64(provider);
                case TypeCode.Single:
                    return v.ToSingle(provider);
                case TypeCode.Double:
                    return v.ToDouble(provider);
                case TypeCode.Decimal:
                    return v.ToDecimal(provider);
                case TypeCode.DateTime:
                    return v.ToDateTime(provider);
                case TypeCode.String:
                    return v.ToString(provider);
                case TypeCode.Object:
                    return value;
                case TypeCode.Empty:
                    throw new InvalidCastException(SR.InvalidCast_Empty);
                default:
                    throw new ArgumentException(SR.Arg_UnknownTypeCode);
            }
        }

        public static TypeCode GetTypeCode(object value)
        {
            if (value == null) return TypeCode.Empty;
            IConvertible temp = value as IConvertible;
            if (temp != null)
            {
                return temp.GetTypeCode();
            }
            return TypeCode.Object;
        }

        internal static Object DefaultToType(IConvertible value, Type targetType, IFormatProvider provider)
        {
            Debug.Assert(value != null, "[Convert.DefaultToType]value!=null");
            if (targetType == null)
            {
                throw new ArgumentNullException("targetType");
            }
            Contract.EndContractBlock();

            if (value.GetType() == targetType)
                return value;

            if (targetType == CommonRuntimeTypes.Boolean)
                return value.ToBoolean(provider);
            if (targetType == CommonRuntimeTypes.Char)
                return value.ToChar(provider);
            if (targetType == CommonRuntimeTypes.SByte)
                return value.ToSByte(provider);
            if (targetType == CommonRuntimeTypes.Byte)
                return value.ToByte(provider);
            if (targetType == CommonRuntimeTypes.Int16)
                return value.ToInt16(provider);
            if (targetType == CommonRuntimeTypes.UInt16)
                return value.ToUInt16(provider);
            if (targetType == CommonRuntimeTypes.Int32)
                return value.ToInt32(provider);
            if (targetType == CommonRuntimeTypes.UInt32)
                return value.ToUInt32(provider);
            if (targetType == CommonRuntimeTypes.Int64)
                return value.ToInt64(provider);
            if (targetType == CommonRuntimeTypes.UInt64)
                return value.ToUInt64(provider);
            if (targetType == CommonRuntimeTypes.Single)
                return value.ToSingle(provider);
            if (targetType == CommonRuntimeTypes.Double)
                return value.ToDouble(provider);
            if (targetType == CommonRuntimeTypes.Decimal)
                return value.ToDecimal(provider);
            if (targetType == CommonRuntimeTypes.DateTime)
                return value.ToDateTime(provider);
            if (targetType == CommonRuntimeTypes.String)
                return value.ToString(provider);
            if (targetType == CommonRuntimeTypes.Object)
                return (Object)value;
            if (targetType == CommonRuntimeTypes.Enum)
                return (Enum)value;

            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, value.GetType().ToString(), targetType.Name));
        }

        // Conversions to Boolean
        public static bool ToBoolean(Object value)
        {
            return value == null ? false : ((IConvertible)value).ToBoolean(null);
        }

        public static bool ToBoolean(Object value, IFormatProvider provider)
        {
            return value == null ? false : ((IConvertible)value).ToBoolean(provider);
        }


        public static bool ToBoolean(bool value)
        {
            return value;
        }

        [CLSCompliant(false)]
        public static bool ToBoolean(sbyte value)
        {
            return value != 0;
        }

        // To be consistent with IConvertible in the base data types else we get different semantics
        // with widening operations. Without this operator this widen succeeds,with this API the widening throws.
        internal static bool ToBoolean(char value)
        {
            return ((IConvertible)value).ToBoolean(null);
        }

        public static bool ToBoolean(byte value)
        {
            return value != 0;
        }


        public static bool ToBoolean(short value)
        {
            return value != 0;
        }

        [CLSCompliant(false)]
        public static bool ToBoolean(ushort value)
        {
            return value != 0;
        }

        public static bool ToBoolean(int value)
        {
            return value != 0;
        }

        [CLSCompliant(false)]
        public static bool ToBoolean(uint value)
        {
            return value != 0;
        }

        public static bool ToBoolean(long value)
        {
            return value != 0;
        }

        [CLSCompliant(false)]
        public static bool ToBoolean(ulong value)
        {
            return value != 0;
        }

        public static bool ToBoolean(String value)
        {
            if (value == null)
                return false;
            return Boolean.Parse(value);
        }

        public static bool ToBoolean(String value, IFormatProvider provider)
        {
            if (value == null)
                return false;
            return Boolean.Parse(value);
        }

        public static bool ToBoolean(float value)
        {
            return value != 0;
        }

        public static bool ToBoolean(double value)
        {
            return value != 0;
        }

        public static bool ToBoolean(decimal value)
        {
            return value != 0;
        }

        internal static bool ToBoolean(DateTime value)
        {
            return ((IConvertible)value).ToBoolean(null);
        }

        // Disallowed conversions to Boolean
        // public static bool ToBoolean(TimeSpan value)

        // Conversions to Char


        public static char ToChar(object value)
        {
            return value == null ? (char)0 : ((IConvertible)value).ToChar(null);
        }

        public static char ToChar(object value, IFormatProvider provider)
        {
            return value == null ? (char)0 : ((IConvertible)value).ToChar(provider);
        }

        internal static char ToChar(bool value)
        {
            return ((IConvertible)value).ToChar(null);
        }

        internal static char ToChar(char value)
        {
            return value;
        }

        [CLSCompliant(false)]
        public static char ToChar(sbyte value)
        {
            if (value < 0) throw new OverflowException(SR.Overflow_Char);
            Contract.EndContractBlock();
            return (char)value;
        }

        public static char ToChar(byte value)
        {
            return (char)value;
        }

        public static char ToChar(short value)
        {
            if (value < 0) throw new OverflowException(SR.Overflow_Char);
            Contract.EndContractBlock();
            return (char)value;
        }

        [CLSCompliant(false)]
        public static char ToChar(ushort value)
        {
            return (char)value;
        }

        public static char ToChar(int value)
        {
            if (value < 0 || value > Char.MaxValue) throw new OverflowException(SR.Overflow_Char);
            Contract.EndContractBlock();
            return (char)value;
        }

        [CLSCompliant(false)]
        public static char ToChar(uint value)
        {
            if (value > Char.MaxValue) throw new OverflowException(SR.Overflow_Char);
            Contract.EndContractBlock();
            return (char)value;
        }

        public static char ToChar(long value)
        {
            if (value < 0 || value > Char.MaxValue) throw new OverflowException(SR.Overflow_Char);
            Contract.EndContractBlock();
            return (char)value;
        }

        [CLSCompliant(false)]
        public static char ToChar(ulong value)
        {
            if (value > Char.MaxValue) throw new OverflowException(SR.Overflow_Char);
            Contract.EndContractBlock();
            return (char)value;
        }

        //
        // @VariantSwitch
        // Remove FormatExceptions;
        //
        public static char ToChar(String value)
        {
            return ToChar(value, null);
        }

        public static char ToChar(String value, IFormatProvider provider)
        {
            if (value == null)
                throw new ArgumentNullException("value");
            Contract.EndContractBlock();

            if (value.Length != 1)
                throw new FormatException(SR.Format_NeedSingleChar);

            return value[0];
        }

        // To be consistent with IConvertible in the base data types else we get different semantics
        // with widening operations. Without this operator this widen succeeds,with this API the widening throws.
        internal static char ToChar(float value)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "Single", "Char"));
        }

        // To be consistent with IConvertible in the base data types else we get different semantics
        // with widening operations. Without this operator this widen succeeds,with this API the widening throws.
        internal static char ToChar(double value)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "Double", "Char"));
        }

        // To be consistent with IConvertible in the base data types else we get different semantics
        // with widening operations. Without this operator this widen succeeds,with this API the widening throws.
        internal static char ToChar(decimal value)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "Decimal", "Char"));
        }

        internal static char ToChar(DateTime value)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "DateTime", "Char"));
        }


        // Disallowed conversions to Char
        // public static char ToChar(TimeSpan value)

        // Conversions to SByte

        [CLSCompliant(false)]
        public static sbyte ToSByte(object value)
        {
            return value == null ? (sbyte)0 : ((IConvertible)value).ToSByte(null);
        }

        [CLSCompliant(false)]
        public static sbyte ToSByte(object value, IFormatProvider provider)
        {
            return value == null ? (sbyte)0 : ((IConvertible)value).ToSByte(provider);
        }

        [CLSCompliant(false)]
        public static sbyte ToSByte(bool value)
        {
            return value ? (sbyte)BooleanTrue : (sbyte)BooleanFalse;
        }

        [CLSCompliant(false)]
        public static sbyte ToSByte(sbyte value)
        {
            return value;
        }

        [CLSCompliant(false)]
        public static sbyte ToSByte(char value)
        {
            if (value > SByte.MaxValue) throw new OverflowException(SR.Overflow_SByte);
            Contract.EndContractBlock();
            return (sbyte)value;
        }

        [CLSCompliant(false)]
        public static sbyte ToSByte(byte value)
        {
            if (value > SByte.MaxValue) throw new OverflowException(SR.Overflow_SByte);
            Contract.EndContractBlock();
            return (sbyte)value;
        }

        [CLSCompliant(false)]
        public static sbyte ToSByte(short value)
        {
            if (value < SByte.MinValue || value > SByte.MaxValue) throw new OverflowException(SR.Overflow_SByte);
            Contract.EndContractBlock();
            return (sbyte)value;
        }

        [CLSCompliant(false)]
        public static sbyte ToSByte(ushort value)
        {
            if (value > SByte.MaxValue) throw new OverflowException(SR.Overflow_SByte);
            Contract.EndContractBlock();
            return (sbyte)value;
        }

        [CLSCompliant(false)]
        public static sbyte ToSByte(int value)
        {
            if (value < SByte.MinValue || value > SByte.MaxValue) throw new OverflowException(SR.Overflow_SByte);
            Contract.EndContractBlock();
            return (sbyte)value;
        }

        [CLSCompliant(false)]
        public static sbyte ToSByte(uint value)
        {
            if (value > SByte.MaxValue) throw new OverflowException(SR.Overflow_SByte);
            Contract.EndContractBlock();
            return (sbyte)value;
        }

        [CLSCompliant(false)]
        public static sbyte ToSByte(long value)
        {
            if (value < SByte.MinValue || value > SByte.MaxValue) throw new OverflowException(SR.Overflow_SByte);
            Contract.EndContractBlock();
            return (sbyte)value;
        }

        [CLSCompliant(false)]
        public static sbyte ToSByte(ulong value)
        {
            if (value > (ulong)SByte.MaxValue) throw new OverflowException(SR.Overflow_SByte);
            Contract.EndContractBlock();
            return (sbyte)value;
        }

        [CLSCompliant(false)]
        public static sbyte ToSByte(float value)
        {
            return ToSByte((double)value);
        }

        [CLSCompliant(false)]
        public static sbyte ToSByte(double value)
        {
            return ToSByte(ToInt32(value));
        }

        [CLSCompliant(false)]
        public static sbyte ToSByte(decimal value)
        {
            return Decimal.ToSByte(Decimal.Round(value, 0));
        }

        [CLSCompliant(false)]
        public static sbyte ToSByte(String value)
        {
            if (value == null)
                return 0;
            return SByte.Parse(value);
        }

        [CLSCompliant(false)]
        public static sbyte ToSByte(String value, IFormatProvider provider)
        {
            return SByte.Parse(value, NumberStyles.Integer, provider);
        }

        //[CLSCompliant(false)]
        internal static sbyte ToSByte(DateTime value)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "DateTime", "SByte"));
        }

        // Disallowed conversions to SByte
        // public static sbyte ToSByte(TimeSpan value)

        // Conversions to Byte

        public static byte ToByte(object value)
        {
            return value == null ? (byte)0 : ((IConvertible)value).ToByte(null);
        }

        public static byte ToByte(object value, IFormatProvider provider)
        {
            return value == null ? (byte)0 : ((IConvertible)value).ToByte(provider);
        }

        public static byte ToByte(bool value)
        {
            return value ? (byte)BooleanTrue : (byte)BooleanFalse;
        }

        public static byte ToByte(byte value)
        {
            return value;
        }

        public static byte ToByte(char value)
        {
            if (value > Byte.MaxValue) throw new OverflowException(SR.Overflow_Byte);
            Contract.EndContractBlock();
            return (byte)value;
        }

        [CLSCompliant(false)]
        public static byte ToByte(sbyte value)
        {
            if (value < Byte.MinValue) throw new OverflowException(SR.Overflow_Byte);
            Contract.EndContractBlock();
            return (byte)value;
        }

        public static byte ToByte(short value)
        {
            if (value < Byte.MinValue || value > Byte.MaxValue) throw new OverflowException(SR.Overflow_Byte);
            Contract.EndContractBlock();
            return (byte)value;
        }

        [CLSCompliant(false)]
        public static byte ToByte(ushort value)
        {
            if (value > Byte.MaxValue) throw new OverflowException(SR.Overflow_Byte);
            Contract.EndContractBlock();
            return (byte)value;
        }

        public static byte ToByte(int value)
        {
            if (value < Byte.MinValue || value > Byte.MaxValue) throw new OverflowException(SR.Overflow_Byte);
            Contract.EndContractBlock();
            return (byte)value;
        }

        [CLSCompliant(false)]
        public static byte ToByte(uint value)
        {
            if (value > Byte.MaxValue) throw new OverflowException(SR.Overflow_Byte);
            Contract.EndContractBlock();
            return (byte)value;
        }

        public static byte ToByte(long value)
        {
            if (value < Byte.MinValue || value > Byte.MaxValue) throw new OverflowException(SR.Overflow_Byte);
            Contract.EndContractBlock();
            return (byte)value;
        }

        [CLSCompliant(false)]
        public static byte ToByte(ulong value)
        {
            if (value > Byte.MaxValue) throw new OverflowException(SR.Overflow_Byte);
            Contract.EndContractBlock();
            return (byte)value;
        }

        public static byte ToByte(float value)
        {
            return ToByte((double)value);
        }

        public static byte ToByte(double value)
        {
            return ToByte(ToInt32(value));
        }

        public static byte ToByte(decimal value)
        {
            return Decimal.ToByte(Decimal.Round(value, 0));
        }

        public static byte ToByte(String value)
        {
            if (value == null)
                return 0;
            return Byte.Parse(value);
        }

        public static byte ToByte(String value, IFormatProvider provider)
        {
            if (value == null)
                return 0;
            return Byte.Parse(value, NumberStyles.Integer, provider);
        }

        internal static byte ToByte(DateTime value)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "DateTime", "Byte"));
        }


        // Disallowed conversions to Byte
        // public static byte ToByte(TimeSpan value)

        // Conversions to Int16

        public static short ToInt16(object value)
        {
            return value == null ? (short)0 : ((IConvertible)value).ToInt16(null);
        }

        public static short ToInt16(object value, IFormatProvider provider)
        {
            return value == null ? (short)0 : ((IConvertible)value).ToInt16(provider);
        }

        public static short ToInt16(bool value)
        {
            return value ? (short)BooleanTrue : (short)BooleanFalse;
        }

        public static short ToInt16(char value)
        {
            if (value > Int16.MaxValue) throw new OverflowException(SR.Overflow_Int16);
            Contract.EndContractBlock();
            return (short)value;
        }

        [CLSCompliant(false)]
        public static short ToInt16(sbyte value)
        {
            return value;
        }

        public static short ToInt16(byte value)
        {
            return value;
        }

        [CLSCompliant(false)]
        public static short ToInt16(ushort value)
        {
            if (value > Int16.MaxValue) throw new OverflowException(SR.Overflow_Int16);
            Contract.EndContractBlock();
            return (short)value;
        }

        public static short ToInt16(int value)
        {
            if (value < Int16.MinValue || value > Int16.MaxValue) throw new OverflowException(SR.Overflow_Int16);
            Contract.EndContractBlock();
            return (short)value;
        }

        [CLSCompliant(false)]
        public static short ToInt16(uint value)
        {
            if (value > Int16.MaxValue) throw new OverflowException(SR.Overflow_Int16);
            Contract.EndContractBlock();
            return (short)value;
        }

        public static short ToInt16(short value)
        {
            return value;
        }

        public static short ToInt16(long value)
        {
            if (value < Int16.MinValue || value > Int16.MaxValue) throw new OverflowException(SR.Overflow_Int16);
            Contract.EndContractBlock();
            return (short)value;
        }

        [CLSCompliant(false)]
        public static short ToInt16(ulong value)
        {
            if (value > (ulong)Int16.MaxValue) throw new OverflowException(SR.Overflow_Int16);
            Contract.EndContractBlock();
            return (short)value;
        }

        public static short ToInt16(float value)
        {
            return ToInt16((double)value);
        }

        public static short ToInt16(double value)
        {
            return ToInt16(ToInt32(value));
        }

        public static short ToInt16(decimal value)
        {
            return Decimal.ToInt16(Decimal.Round(value, 0));
        }

        public static short ToInt16(String value)
        {
            if (value == null)
                return 0;
            return Int16.Parse(value);
        }

        public static short ToInt16(String value, IFormatProvider provider)
        {
            if (value == null)
                return 0;
            return Int16.Parse(value, NumberStyles.Integer, provider);
        }

        internal static short ToInt16(DateTime value)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "DateTime", "Int16"));
        }


        // Disallowed conversions to Int16
        // public static short ToInt16(TimeSpan value)

        // Conversions to UInt16

        [CLSCompliant(false)]
        public static ushort ToUInt16(object value)
        {
            return value == null ? (ushort)0 : ((IConvertible)value).ToUInt16(null);
        }

        [CLSCompliant(false)]
        public static ushort ToUInt16(object value, IFormatProvider provider)
        {
            return value == null ? (ushort)0 : ((IConvertible)value).ToUInt16(provider);
        }


        [CLSCompliant(false)]
        public static ushort ToUInt16(bool value)
        {
            return value ? (ushort)BooleanTrue : (ushort)BooleanFalse;
        }

        [CLSCompliant(false)]
        public static ushort ToUInt16(char value)
        {
            return value;
        }

        [CLSCompliant(false)]
        public static ushort ToUInt16(sbyte value)
        {
            if (value < 0) throw new OverflowException(SR.Overflow_UInt16);
            Contract.EndContractBlock();
            return (ushort)value;
        }

        [CLSCompliant(false)]
        public static ushort ToUInt16(byte value)
        {
            return value;
        }

        [CLSCompliant(false)]
        public static ushort ToUInt16(short value)
        {
            if (value < 0) throw new OverflowException(SR.Overflow_UInt16);
            Contract.EndContractBlock();
            return (ushort)value;
        }

        [CLSCompliant(false)]
        public static ushort ToUInt16(int value)
        {
            if (value < 0 || value > UInt16.MaxValue) throw new OverflowException(SR.Overflow_UInt16);
            Contract.EndContractBlock();
            return (ushort)value;
        }

        [CLSCompliant(false)]
        public static ushort ToUInt16(ushort value)
        {
            return value;
        }

        [CLSCompliant(false)]
        public static ushort ToUInt16(uint value)
        {
            if (value > UInt16.MaxValue) throw new OverflowException(SR.Overflow_UInt16);
            Contract.EndContractBlock();
            return (ushort)value;
        }


        [CLSCompliant(false)]
        public static ushort ToUInt16(long value)
        {
            if (value < 0 || value > UInt16.MaxValue) throw new OverflowException(SR.Overflow_UInt16);
            Contract.EndContractBlock();
            return (ushort)value;
        }

        [CLSCompliant(false)]
        public static ushort ToUInt16(ulong value)
        {
            if (value > UInt16.MaxValue) throw new OverflowException(SR.Overflow_UInt16);
            Contract.EndContractBlock();
            return (ushort)value;
        }

        [CLSCompliant(false)]
        public static ushort ToUInt16(float value)
        {
            return ToUInt16((double)value);
        }

        [CLSCompliant(false)]
        public static ushort ToUInt16(double value)
        {
            return ToUInt16(ToInt32(value));
        }

        [CLSCompliant(false)]
        public static ushort ToUInt16(decimal value)
        {
            return Decimal.ToUInt16(Decimal.Round(value, 0));
        }

        [CLSCompliant(false)]
        public static ushort ToUInt16(String value)
        {
            if (value == null)
                return 0;
            return UInt16.Parse(value);
        }

        [CLSCompliant(false)]
        public static ushort ToUInt16(String value, IFormatProvider provider)
        {
            if (value == null)
                return 0;
            return UInt16.Parse(value, NumberStyles.Integer, provider);
        }

        //[CLSCompliant(false)]
        internal static ushort ToUInt16(DateTime value)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "DateTime", "UInt16"));
        }

        // Disallowed conversions to UInt16
        // public static ushort ToUInt16(TimeSpan value)

        // Conversions to Int32

        public static int ToInt32(object value)
        {
            return value == null ? 0 : ((IConvertible)value).ToInt32(null);
        }

        public static int ToInt32(object value, IFormatProvider provider)
        {
            return value == null ? 0 : ((IConvertible)value).ToInt32(provider);
        }


        public static int ToInt32(bool value)
        {
            return value ? BooleanTrue : BooleanFalse;
        }

        public static int ToInt32(char value)
        {
            return value;
        }

        [CLSCompliant(false)]
        public static int ToInt32(sbyte value)
        {
            return value;
        }

        public static int ToInt32(byte value)
        {
            return value;
        }

        public static int ToInt32(short value)
        {
            return value;
        }

        [CLSCompliant(false)]
        public static int ToInt32(ushort value)
        {
            return value;
        }

        [CLSCompliant(false)]
        public static int ToInt32(uint value)
        {
            if (value > Int32.MaxValue) throw new OverflowException(SR.Overflow_Int32);
            Contract.EndContractBlock();
            return (int)value;
        }

        public static int ToInt32(int value)
        {
            return value;
        }

        public static int ToInt32(long value)
        {
            if (value < Int32.MinValue || value > Int32.MaxValue) throw new OverflowException(SR.Overflow_Int32);
            Contract.EndContractBlock();
            return (int)value;
        }

        [CLSCompliant(false)]
        public static int ToInt32(ulong value)
        {
            if (value > Int32.MaxValue) throw new OverflowException(SR.Overflow_Int32);
            Contract.EndContractBlock();
            return (int)value;
        }

        public static int ToInt32(float value)
        {
            return ToInt32((double)value);
        }

        public static int ToInt32(double value)
        {
            if (value >= 0)
            {
                if (value < 2147483647.5)
                {
                    int result = (int)value;
                    double dif = value - result;
                    if (dif > 0.5 || dif == 0.5 && (result & 1) != 0) result++;
                    return result;
                }
            }
            else
            {
                if (value >= -2147483648.5)
                {
                    int result = (int)value;
                    double dif = value - result;
                    if (dif < -0.5 || dif == -0.5 && (result & 1) != 0) result--;
                    return result;
                }
            }
            throw new OverflowException(SR.Overflow_Int32);
        }

        public static int ToInt32(decimal value)
        {
            // FxOverRh port note: BCL version calls a private Decimal entrypoint called uglily enough FCallToInt32().
            // This FCall is just a flattened version of invoking Decimal.Round() followed by Decimal.ToInt32()
            Decimal rounded = Decimal.Round(value, 0);
            return Decimal.ToInt32(rounded);
        }

        public static int ToInt32(String value)
        {
            if (value == null)
                return 0;
            return Int32.Parse(value);
        }

        public static int ToInt32(String value, IFormatProvider provider)
        {
            if (value == null)
                return 0;
            return Int32.Parse(value, NumberStyles.Integer, provider);
        }

        internal static int ToInt32(DateTime value)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "DateTime", "Int32"));
        }


        // Disallowed conversions to Int32
        // public static int ToInt32(TimeSpan value)

        // Conversions to UInt32

        [CLSCompliant(false)]
        public static uint ToUInt32(object value)
        {
            return value == null ? 0 : ((IConvertible)value).ToUInt32(null);
        }

        [CLSCompliant(false)]
        public static uint ToUInt32(object value, IFormatProvider provider)
        {
            return value == null ? 0 : ((IConvertible)value).ToUInt32(provider);
        }


        [CLSCompliant(false)]
        public static uint ToUInt32(bool value)
        {
            return value ? (uint)BooleanTrue : (uint)BooleanFalse;
        }

        [CLSCompliant(false)]
        public static uint ToUInt32(char value)
        {
            return value;
        }

        [CLSCompliant(false)]
        public static uint ToUInt32(sbyte value)
        {
            if (value < 0) throw new OverflowException(SR.Overflow_UInt32);
            Contract.EndContractBlock();
            return (uint)value;
        }

        [CLSCompliant(false)]
        public static uint ToUInt32(byte value)
        {
            return value;
        }

        [CLSCompliant(false)]
        public static uint ToUInt32(short value)
        {
            if (value < 0) throw new OverflowException(SR.Overflow_UInt32);
            Contract.EndContractBlock();
            return (uint)value;
        }

        [CLSCompliant(false)]
        public static uint ToUInt32(ushort value)
        {
            return value;
        }

        [CLSCompliant(false)]
        public static uint ToUInt32(int value)
        {
            if (value < 0) throw new OverflowException(SR.Overflow_UInt32);
            Contract.EndContractBlock();
            return (uint)value;
        }

        [CLSCompliant(false)]
        public static uint ToUInt32(uint value)
        {
            return value;
        }

        [CLSCompliant(false)]
        public static uint ToUInt32(long value)
        {
            if (value < 0 || value > UInt32.MaxValue) throw new OverflowException(SR.Overflow_UInt32);
            Contract.EndContractBlock();
            return (uint)value;
        }

        [CLSCompliant(false)]
        public static uint ToUInt32(ulong value)
        {
            if (value > UInt32.MaxValue) throw new OverflowException(SR.Overflow_UInt32);
            Contract.EndContractBlock();
            return (uint)value;
        }

        [CLSCompliant(false)]
        public static uint ToUInt32(float value)
        {
            return ToUInt32((double)value);
        }

        [CLSCompliant(false)]
        public static uint ToUInt32(double value)
        {
            if (value >= -0.5 && value < 4294967295.5)
            {
                uint result = (uint)value;
                double dif = value - result;
                if (dif > 0.5 || dif == 0.5 && (result & 1) != 0) result++;
                return result;
            }
            throw new OverflowException(SR.Overflow_UInt32);
        }

        [CLSCompliant(false)]
        public static uint ToUInt32(decimal value)
        {
            return Decimal.ToUInt32(Decimal.Round(value, 0));
        }

        [CLSCompliant(false)]
        public static uint ToUInt32(String value)
        {
            if (value == null)
                return 0;
            return UInt32.Parse(value);
        }

        [CLSCompliant(false)]
        public static uint ToUInt32(String value, IFormatProvider provider)
        {
            if (value == null)
                return 0;
            return UInt32.Parse(value, NumberStyles.Integer, provider);
        }

        //[CLSCompliant(false)]
        internal static uint ToUInt32(DateTime value)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "DateTime", "UInt32"));
        }

        // Disallowed conversions to UInt32
        // public static uint ToUInt32(TimeSpan value)

        // Conversions to Int64

        public static long ToInt64(object value)
        {
            return value == null ? 0 : ((IConvertible)value).ToInt64(null);
        }

        public static long ToInt64(object value, IFormatProvider provider)
        {
            return value == null ? 0 : ((IConvertible)value).ToInt64(provider);
        }


        public static long ToInt64(bool value)
        {
            return value ? BooleanTrue : BooleanFalse;
        }

        public static long ToInt64(char value)
        {
            return value;
        }

        [CLSCompliant(false)]
        public static long ToInt64(sbyte value)
        {
            return value;
        }

        public static long ToInt64(byte value)
        {
            return value;
        }

        public static long ToInt64(short value)
        {
            return value;
        }

        [CLSCompliant(false)]
        public static long ToInt64(ushort value)
        {
            return value;
        }

        public static long ToInt64(int value)
        {
            return value;
        }

        [CLSCompliant(false)]
        public static long ToInt64(uint value)
        {
            return value;
        }

        [CLSCompliant(false)]
        public static long ToInt64(ulong value)
        {
            if (value > Int64.MaxValue) throw new OverflowException(SR.Overflow_Int64);
            Contract.EndContractBlock();
            return (long)value;
        }

        public static long ToInt64(long value)
        {
            return value;
        }


        public static long ToInt64(float value)
        {
            return ToInt64((double)value);
        }

        public static long ToInt64(double value)
        {
            return checked((long)Math.Round(value));
        }

        public static long ToInt64(decimal value)
        {
            return Decimal.ToInt64(Decimal.Round(value, 0));
        }

        public static long ToInt64(string value)
        {
            if (value == null)
                return 0;
            return Int64.Parse(value);
        }

        public static long ToInt64(String value, IFormatProvider provider)
        {
            if (value == null)
                return 0;
            return Int64.Parse(value, NumberStyles.Integer, provider);
        }

        internal static long ToInt64(DateTime value)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "DateTime", "Int64"));
        }

        // Disallowed conversions to Int64
        // public static long ToInt64(TimeSpan value)

        // Conversions to UInt64

        [CLSCompliant(false)]
        public static ulong ToUInt64(object value)
        {
            return value == null ? 0 : ((IConvertible)value).ToUInt64(null);
        }

        [CLSCompliant(false)]
        public static ulong ToUInt64(object value, IFormatProvider provider)
        {
            return value == null ? 0 : ((IConvertible)value).ToUInt64(provider);
        }

        [CLSCompliant(false)]
        public static ulong ToUInt64(bool value)
        {
            return value ? (ulong)BooleanTrue : (ulong)BooleanFalse;
        }

        [CLSCompliant(false)]
        public static ulong ToUInt64(char value)
        {
            return value;
        }


        [CLSCompliant(false)]
        public static ulong ToUInt64(sbyte value)
        {
            if (value < 0) throw new OverflowException(SR.Overflow_UInt64);
            Contract.EndContractBlock();
            return (ulong)value;
        }

        [CLSCompliant(false)]
        public static ulong ToUInt64(byte value)
        {
            return value;
        }

        [CLSCompliant(false)]
        public static ulong ToUInt64(short value)
        {
            if (value < 0) throw new OverflowException(SR.Overflow_UInt64);
            Contract.EndContractBlock();
            return (ulong)value;
        }

        [CLSCompliant(false)]
        public static ulong ToUInt64(ushort value)
        {
            return value;
        }

        [CLSCompliant(false)]
        public static ulong ToUInt64(int value)
        {
            if (value < 0) throw new OverflowException(SR.Overflow_UInt64);
            Contract.EndContractBlock();
            return (ulong)value;
        }

        [CLSCompliant(false)]
        public static ulong ToUInt64(uint value)
        {
            return value;
        }

        [CLSCompliant(false)]
        public static ulong ToUInt64(long value)
        {
            if (value < 0) throw new OverflowException(SR.Overflow_UInt64);
            Contract.EndContractBlock();
            return (ulong)value;
        }

        [CLSCompliant(false)]
        public static ulong ToUInt64(UInt64 value)
        {
            return value;
        }

        [CLSCompliant(false)]
        public static ulong ToUInt64(float value)
        {
            return ToUInt64((double)value);
        }

        [CLSCompliant(false)]
        public static ulong ToUInt64(double value)
        {
            return checked((ulong)Math.Round(value));
        }

        [CLSCompliant(false)]
        public static ulong ToUInt64(decimal value)
        {
            return Decimal.ToUInt64(Decimal.Round(value, 0));
        }

        [CLSCompliant(false)]
        public static ulong ToUInt64(String value)
        {
            if (value == null)
                return 0;
            return UInt64.Parse(value);
        }

        [CLSCompliant(false)]
        public static ulong ToUInt64(String value, IFormatProvider provider)
        {
            if (value == null)
                return 0;
            return UInt64.Parse(value, NumberStyles.Integer, provider);
        }

        //[CLSCompliant(false)]
        internal static ulong ToUInt64(DateTime value)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "DateTime", "UInt64"));
        }

        // Disallowed conversions to UInt64
        // public static ulong ToUInt64(TimeSpan value)

        // Conversions to Single

        public static float ToSingle(object value)
        {
            return value == null ? 0 : ((IConvertible)value).ToSingle(null);
        }

        public static float ToSingle(object value, IFormatProvider provider)
        {
            return value == null ? 0 : ((IConvertible)value).ToSingle(provider);
        }

        [CLSCompliant(false)]
        public static float ToSingle(sbyte value)
        {
            return value;
        }

        public static float ToSingle(byte value)
        {
            return value;
        }

        internal static float ToSingle(char value)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "Char", "Single"));
        }

        public static float ToSingle(short value)
        {
            return value;
        }

        [CLSCompliant(false)]
        public static float ToSingle(ushort value)
        {
            return value;
        }

        public static float ToSingle(int value)
        {
            return value;
        }

        [CLSCompliant(false)]
        public static float ToSingle(uint value)
        {
            return value;
        }

        public static float ToSingle(long value)
        {
            return value;
        }

        [CLSCompliant(false)]
        public static float ToSingle(ulong value)
        {
            return value;
        }

        public static float ToSingle(float value)
        {
            return value;
        }

        public static float ToSingle(double value)
        {
            return (float)value;
        }

        public static float ToSingle(decimal value)
        {
            return (float)value;
        }

        public static float ToSingle(String value)
        {
            if (value == null)
                return 0;
            return Single.Parse(value);
        }

        public static float ToSingle(String value, IFormatProvider provider)
        {
            if (value == null)
                return 0;
            return Single.Parse(value, NumberStyles.Float | NumberStyles.AllowThousands, provider);
        }


        public static float ToSingle(bool value)
        {
            return value ? BooleanTrue : BooleanFalse;
        }

        internal static float ToSingle(DateTime value)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "DateTime", "Single"));
        }

        // Disallowed conversions to Single
        // public static float ToSingle(TimeSpan value)

        // Conversions to Double

        public static double ToDouble(object value)
        {
            return value == null ? 0 : ((IConvertible)value).ToDouble(null);
        }

        public static double ToDouble(object value, IFormatProvider provider)
        {
            return value == null ? 0 : ((IConvertible)value).ToDouble(provider);
        }


        [CLSCompliant(false)]
        public static double ToDouble(sbyte value)
        {
            return value;
        }

        public static double ToDouble(byte value)
        {
            return value;
        }

        public static double ToDouble(short value)
        {
            return value;
        }

        internal static double ToDouble(char value)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "Char", "Double"));
        }

        [CLSCompliant(false)]
        public static double ToDouble(ushort value)
        {
            return value;
        }

        public static double ToDouble(int value)
        {
            return value;
        }

        [CLSCompliant(false)]
        public static double ToDouble(uint value)
        {
            return value;
        }

        public static double ToDouble(long value)
        {
            return value;
        }

        [CLSCompliant(false)]
        public static double ToDouble(ulong value)
        {
            return value;
        }

        public static double ToDouble(float value)
        {
            return value;
        }

        public static double ToDouble(double value)
        {
            return value;
        }

        public static double ToDouble(decimal value)
        {
            return (double)value;
        }

        public static double ToDouble(String value)
        {
            if (value == null)
                return 0;
            return Double.Parse(value);
        }

        public static double ToDouble(String value, IFormatProvider provider)
        {
            if (value == null)
                return 0;
            return Double.Parse(value, NumberStyles.Float | NumberStyles.AllowThousands, provider);
        }

        public static double ToDouble(bool value)
        {
            return value ? BooleanTrue : BooleanFalse;
        }

        internal static double ToDouble(DateTime value)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "DateTime", "Double"));
        }

        // Disallowed conversions to Double
        // public static double ToDouble(TimeSpan value)

        // Conversions to Decimal

        public static decimal ToDecimal(object value)
        {
            return value == null ? 0 : ((IConvertible)value).ToDecimal(null);
        }

        public static decimal ToDecimal(object value, IFormatProvider provider)
        {
            return value == null ? 0 : ((IConvertible)value).ToDecimal(provider);
        }

        [CLSCompliant(false)]
        public static decimal ToDecimal(sbyte value)
        {
            return value;
        }

        public static decimal ToDecimal(byte value)
        {
            return value;
        }

        internal static decimal ToDecimal(char value)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "Char", "Decimal"));
        }

        public static decimal ToDecimal(short value)
        {
            return value;
        }

        [CLSCompliant(false)]
        public static decimal ToDecimal(ushort value)
        {
            return value;
        }

        public static decimal ToDecimal(int value)
        {
            return value;
        }

        [CLSCompliant(false)]
        public static decimal ToDecimal(uint value)
        {
            return value;
        }

        public static decimal ToDecimal(long value)
        {
            return value;
        }

        [CLSCompliant(false)]
        public static decimal ToDecimal(ulong value)
        {
            return value;
        }

        public static decimal ToDecimal(float value)
        {
            return (decimal)value;
        }

        public static decimal ToDecimal(double value)
        {
            return (decimal)value;
        }

        public static decimal ToDecimal(String value)
        {
            if (value == null)
                return 0m;
            return Decimal.Parse(value);
        }

        public static Decimal ToDecimal(String value, IFormatProvider provider)
        {
            if (value == null)
                return 0m;
            return Decimal.Parse(value, NumberStyles.Number, provider);
        }

        public static decimal ToDecimal(decimal value)
        {
            return value;
        }

        public static decimal ToDecimal(bool value)
        {
            return value ? BooleanTrue : BooleanFalse;
        }

        internal static decimal ToDecimal(DateTime value)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "DateTime", "Decimal"));
        }

        // Disallowed conversions to Decimal
        // public static decimal ToDecimal(TimeSpan value)

        // Conversions to DateTime

        internal static DateTime ToDateTime(DateTime value)
        {
            return value;
        }

        public static DateTime ToDateTime(object value)
        {
            return value == null ? DateTime.MinValue : ((IConvertible)value).ToDateTime(null);
        }

        public static DateTime ToDateTime(object value, IFormatProvider provider)
        {
            return value == null ? DateTime.MinValue : ((IConvertible)value).ToDateTime(provider);
        }

        public static DateTime ToDateTime(String value)
        {
            if (value == null)
                return new DateTime(0);
            return DateTime.Parse(value);
        }

        public static DateTime ToDateTime(String value, IFormatProvider provider)
        {
            if (value == null)
                return new DateTime(0);
            return DateTime.Parse(value, provider);
        }

        //[CLSCompliant(false)]
        internal static DateTime ToDateTime(sbyte value)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "SByte", "DateTime"));
        }

        internal static DateTime ToDateTime(byte value)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "Byte", "DateTime"));
        }
        internal static DateTime ToDateTime(short value)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "Int16", "DateTime"));
        }

        //[CLSCompliant(false)]
        internal static DateTime ToDateTime(ushort value)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "UInt16", "DateTime"));
        }

        internal static DateTime ToDateTime(int value)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "Int32", "DateTime"));
        }

        //[CLSCompliant(false)]
        internal static DateTime ToDateTime(uint value)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "UInt32", "DateTime"));
        }

        internal static DateTime ToDateTime(long value)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "Int64", "DateTime"));
        }

        //[CLSCompliant(false)]
        internal static DateTime ToDateTime(ulong value)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "UInt64", "DateTime"));
        }

        internal static DateTime ToDateTime(bool value)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "Boolean", "DateTime"));
        }

        internal static DateTime ToDateTime(char value)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "Char", "DateTime"));
        }

        internal static DateTime ToDateTime(float value)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "Single", "DateTime"));
        }

        internal static DateTime ToDateTime(double value)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "Double", "DateTime"));
        }

        internal static DateTime ToDateTime(decimal value)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "Decimal", "DateTime"));
        }

        // Disallowed conversions to DateTime
        // public static DateTime ToDateTime(TimeSpan value)

        // Conversions to String

        public static string ToString(Object value)
        {
            return ToString(value, null);
        }

        public static string ToString(Object value, IFormatProvider provider)
        {
            IConvertible ic = value as IConvertible;
            if (ic != null)
                return ic.ToString(provider);
            IFormattable formattable = value as IFormattable;
            if (formattable != null)
                return formattable.ToString(null, provider);
            return value == null ? String.Empty : value.ToString();
        }

        public static string ToString(bool value)
        {
            Contract.Ensures(Contract.Result<string>() != null);
            return value.ToString();
        }

        public static string ToString(bool value, IFormatProvider provider)
        {
            Contract.Ensures(Contract.Result<string>() != null);
            IConvertible ic = value;
            return ic.ToString(provider);
        }

        public static string ToString(char value)
        {
            Contract.Ensures(Contract.Result<string>() != null);
            return Char.ToString(value);
        }

        public static string ToString(char value, IFormatProvider provider)
        {
            Contract.Ensures(Contract.Result<string>() != null);
            IConvertible ic = value;
            return ic.ToString(provider);
        }

        [CLSCompliant(false)]
        public static string ToString(sbyte value)
        {
            Contract.Ensures(Contract.Result<string>() != null);
            return value.ToString();
        }

        [CLSCompliant(false)]
        public static string ToString(sbyte value, IFormatProvider provider)
        {
            Contract.Ensures(Contract.Result<string>() != null);
            return value.ToString(provider);
        }

        public static string ToString(byte value)
        {
            Contract.Ensures(Contract.Result<string>() != null);
            return value.ToString();
        }

        public static string ToString(byte value, IFormatProvider provider)
        {
            Contract.Ensures(Contract.Result<string>() != null);
            return value.ToString(provider);
        }

        public static string ToString(short value)
        {
            Contract.Ensures(Contract.Result<string>() != null);
            return value.ToString();
        }

        public static string ToString(short value, IFormatProvider provider)
        {
            Contract.Ensures(Contract.Result<string>() != null);
            return value.ToString(provider);
        }

        [CLSCompliant(false)]
        public static string ToString(ushort value)
        {
            Contract.Ensures(Contract.Result<string>() != null);
            return value.ToString();
        }

        [CLSCompliant(false)]
        public static string ToString(ushort value, IFormatProvider provider)
        {
            Contract.Ensures(Contract.Result<string>() != null);
            return value.ToString(provider);
        }

        public static string ToString(int value)
        {
            Contract.Ensures(Contract.Result<string>() != null);
            return value.ToString();
        }

        public static string ToString(int value, IFormatProvider provider)
        {
            Contract.Ensures(Contract.Result<string>() != null);
            return value.ToString(provider);
        }

        [CLSCompliant(false)]
        public static string ToString(uint value)
        {
            Contract.Ensures(Contract.Result<string>() != null);
            return value.ToString();
        }

        [CLSCompliant(false)]
        public static string ToString(uint value, IFormatProvider provider)
        {
            Contract.Ensures(Contract.Result<string>() != null);
            return value.ToString(provider);
        }

        public static string ToString(long value)
        {
            Contract.Ensures(Contract.Result<string>() != null);
            return value.ToString();
        }

        public static string ToString(long value, IFormatProvider provider)
        {
            Contract.Ensures(Contract.Result<string>() != null);
            return value.ToString(provider);
        }

        [CLSCompliant(false)]
        public static string ToString(ulong value)
        {
            Contract.Ensures(Contract.Result<string>() != null);
            return value.ToString();
        }

        [CLSCompliant(false)]
        public static string ToString(ulong value, IFormatProvider provider)
        {
            Contract.Ensures(Contract.Result<string>() != null);
            return value.ToString(provider);
        }

        public static string ToString(float value)
        {
            Contract.Ensures(Contract.Result<string>() != null);
            return value.ToString();
        }

        public static string ToString(float value, IFormatProvider provider)
        {
            Contract.Ensures(Contract.Result<string>() != null);
            return value.ToString(provider);
        }

        public static string ToString(double value)
        {
            Contract.Ensures(Contract.Result<string>() != null);
            return value.ToString();
        }

        public static string ToString(double value, IFormatProvider provider)
        {
            Contract.Ensures(Contract.Result<string>() != null);
            return value.ToString(provider);
        }

        public static string ToString(decimal value)
        {
            Contract.Ensures(Contract.Result<string>() != null);
            return value.ToString();
        }

        public static string ToString(Decimal value, IFormatProvider provider)
        {
            Contract.Ensures(Contract.Result<string>() != null);
            return value.ToString(provider);
        }

        public static string ToString(DateTime value)
        {
            Contract.Ensures(Contract.Result<string>() != null);
            return value.ToString();
        }

        public static string ToString(DateTime value, IFormatProvider provider)
        {
            Contract.Ensures(Contract.Result<string>() != null);
            return value.ToString(provider);
        }

        internal static String ToString(String value)
        {
            Contract.Ensures(Contract.Result<string>() == value);  // We were always skipping the null check here.
            return value;
        }

        internal static String ToString(String value, IFormatProvider provider)
        {
            Contract.Ensures(Contract.Result<string>() == value);  // We were always skipping the null check here.
            return value; // avoid the null check
        }


        //
        // Conversions which understand Base XXX numbers.
        //
        // Parses value in base base.  base can only
        // be 2, 8, 10, or 16.  If base is 16, the number may be preceded
        // by 0x; any other leading or trailing characters cause an error.
        //
        public static byte ToByte(String value, int fromBase)
        {
            if (fromBase != 2 && fromBase != 8 && fromBase != 10 && fromBase != 16)
            {
                throw new ArgumentException(SR.Arg_InvalidBase);
            }
            Contract.EndContractBlock();
            int r = ParseNumbers.StringToInt(value, fromBase, ParseNumbers.IsTight | ParseNumbers.TreatAsUnsigned);
            if (r < Byte.MinValue || r > Byte.MaxValue)
                throw new OverflowException(SR.Overflow_Byte);
            return (byte)r;
        }

        // Parses value in base fromBase.  fromBase can only
        // be 2, 8, 10, or 16.  If fromBase is 16, the number may be preceded
        // by 0x; any other leading or trailing characters cause an error.
        //
        [CLSCompliant(false)]
        public static sbyte ToSByte(String value, int fromBase)
        {
            if (fromBase != 2 && fromBase != 8 && fromBase != 10 && fromBase != 16)
            {
                throw new ArgumentException(SR.Arg_InvalidBase);
            }
            Contract.EndContractBlock();
            int r = ParseNumbers.StringToInt(value, fromBase, ParseNumbers.IsTight | ParseNumbers.TreatAsI1);
            if (fromBase != 10 && r <= Byte.MaxValue)
                return (sbyte)r;

            if (r < SByte.MinValue || r > SByte.MaxValue)
                throw new OverflowException(SR.Overflow_SByte);
            return (sbyte)r;
        }

        // Parses value in base fromBase.  fromBase can only
        // be 2, 8, 10, or 16.  If fromBase is 16, the number may be preceded
        // by 0x; any other leading or trailing characters cause an error.
        //
        public static short ToInt16(String value, int fromBase)
        {
            if (fromBase != 2 && fromBase != 8 && fromBase != 10 && fromBase != 16)
            {
                throw new ArgumentException(SR.Arg_InvalidBase);
            }
            Contract.EndContractBlock();
            int r = ParseNumbers.StringToInt(value, fromBase, ParseNumbers.IsTight | ParseNumbers.TreatAsI2);
            if (fromBase != 10 && r <= UInt16.MaxValue)
                return (short)r;

            if (r < Int16.MinValue || r > Int16.MaxValue)
                throw new OverflowException(SR.Overflow_Int16);
            return (short)r;
        }

        // Parses value in base fromBase.  fromBase can only
        // be 2, 8, 10, or 16.  If fromBase is 16, the number may be preceded
        // by 0x; any other leading or trailing characters cause an error.
        //
        [CLSCompliant(false)]
        public static ushort ToUInt16(String value, int fromBase)
        {
            if (fromBase != 2 && fromBase != 8 && fromBase != 10 && fromBase != 16)
            {
                throw new ArgumentException(SR.Arg_InvalidBase);
            }
            Contract.EndContractBlock();
            int r = ParseNumbers.StringToInt(value, fromBase, ParseNumbers.IsTight | ParseNumbers.TreatAsUnsigned);
            if (r < UInt16.MinValue || r > UInt16.MaxValue)
                throw new OverflowException(SR.Overflow_UInt16);
            return (ushort)r;
        }

        // Parses value in base fromBase.  fromBase can only
        // be 2, 8, 10, or 16.  If fromBase is 16, the number may be preceded
        // by 0x; any other leading or trailing characters cause an error.
        //
        public static int ToInt32(String value, int fromBase)
        {
            if (fromBase != 2 && fromBase != 8 && fromBase != 10 && fromBase != 16)
            {
                throw new ArgumentException(SR.Arg_InvalidBase);
            }
            Contract.EndContractBlock();
            return ParseNumbers.StringToInt(value, fromBase, ParseNumbers.IsTight);
        }

        // Parses value in base fromBase.  fromBase can only
        // be 2, 8, 10, or 16.  If fromBase is 16, the number may be preceded
        // by 0x; any other leading or trailing characters cause an error.
        //
        [CLSCompliant(false)]
        public static uint ToUInt32(String value, int fromBase)
        {
            if (fromBase != 2 && fromBase != 8 && fromBase != 10 && fromBase != 16)
            {
                throw new ArgumentException(SR.Arg_InvalidBase);
            }
            Contract.EndContractBlock();
            return (uint)ParseNumbers.StringToInt(value, fromBase, ParseNumbers.TreatAsUnsigned | ParseNumbers.IsTight);
        }

        // Parses value in base fromBase.  fromBase can only
        // be 2, 8, 10, or 16.  If fromBase is 16, the number may be preceded
        // by 0x; any other leading or trailing characters cause an error.
        //
        public static long ToInt64(String value, int fromBase)
        {
            if (fromBase != 2 && fromBase != 8 && fromBase != 10 && fromBase != 16)
            {
                throw new ArgumentException(SR.Arg_InvalidBase);
            }
            Contract.EndContractBlock();
            return ParseNumbers.StringToLong(value, fromBase, ParseNumbers.IsTight);
        }

        // Parses value in base fromBase.  fromBase can only
        // be 2, 8, 10, or 16.  If fromBase is 16, the number may be preceded
        // by 0x; any other leading or trailing characters cause an error.
        //
        [CLSCompliant(false)]
        public static ulong ToUInt64(String value, int fromBase)
        {
            if (fromBase != 2 && fromBase != 8 && fromBase != 10 && fromBase != 16)
            {
                throw new ArgumentException(SR.Arg_InvalidBase);
            }
            Contract.EndContractBlock();
            return (ulong)ParseNumbers.StringToLong(value, fromBase, ParseNumbers.TreatAsUnsigned | ParseNumbers.IsTight);
        }

        // Convert the byte value to a string in base fromBase
        public static String ToString(byte value, int toBase)
        {
            if (toBase != 2 && toBase != 8 && toBase != 10 && toBase != 16)
            {
                throw new ArgumentException(SR.Arg_InvalidBase);
            }
            Contract.EndContractBlock();
            return ParseNumbers.IntToString((int)value, toBase, -1, ' ', ParseNumbers.PrintAsI1);
        }

        // Convert the Int16 value to a string in base fromBase
        public static String ToString(short value, int toBase)
        {
            if (toBase != 2 && toBase != 8 && toBase != 10 && toBase != 16)
            {
                throw new ArgumentException(SR.Arg_InvalidBase);
            }
            Contract.EndContractBlock();
            return ParseNumbers.IntToString((int)value, toBase, -1, ' ', ParseNumbers.PrintAsI2);
        }

        // Convert the Int32 value to a string in base toBase
        public static String ToString(int value, int toBase)
        {
            if (toBase != 2 && toBase != 8 && toBase != 10 && toBase != 16)
            {
                throw new ArgumentException(SR.Arg_InvalidBase);
            }
            Contract.EndContractBlock();
            return ParseNumbers.IntToString(value, toBase, -1, ' ', 0);
        }

        // Convert the Int64 value to a string in base toBase
        public static String ToString(long value, int toBase)
        {
            if (toBase != 2 && toBase != 8 && toBase != 10 && toBase != 16)
            {
                throw new ArgumentException(SR.Arg_InvalidBase);
            }
            Contract.EndContractBlock();
            return ParseNumbers.LongToString(value, toBase, -1, ' ', 0);
        }

        public static String ToBase64String(byte[] inArray)
        {
            if (inArray == null)
            {
                throw new ArgumentNullException("inArray");
            }
            Contract.Ensures(Contract.Result<string>() != null);
            Contract.EndContractBlock();
            return ToBase64String(inArray, 0, inArray.Length, Base64FormattingOptions.None);
        }

        [System.Runtime.InteropServices.ComVisible(false)]
        internal static String ToBase64String(byte[] inArray, Base64FormattingOptions options)
        {
            if (inArray == null)
            {
                throw new ArgumentNullException("inArray");
            }
            Contract.Ensures(Contract.Result<string>() != null);
            Contract.EndContractBlock();
            return ToBase64String(inArray, 0, inArray.Length, options);
        }

        public static String ToBase64String(byte[] inArray, int offset, int length)
        {
            return ToBase64String(inArray, offset, length, Base64FormattingOptions.None);
        }

        [System.Runtime.InteropServices.ComVisible(false)]
        internal static unsafe String ToBase64String(byte[] inArray, int offset, int length, Base64FormattingOptions options)
        {
            //Do data verfication
            if (inArray == null)
                throw new ArgumentNullException("inArray");
            if (length < 0)
                throw new ArgumentOutOfRangeException("length", SR.ArgumentOutOfRange_Index);
            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset", SR.ArgumentOutOfRange_GenericPositive);
            if (options < Base64FormattingOptions.None || options > Base64FormattingOptions.InsertLineBreaks)
                throw new ArgumentException(SR.Format(SR.Arg_EnumIllegalVal, (int)options));
            Contract.Ensures(Contract.Result<string>() != null);
            Contract.EndContractBlock();

            int inArrayLength;
            int stringLength;

            inArrayLength = inArray.Length;
            if (offset > (inArrayLength - length))
                throw new ArgumentOutOfRangeException("offset", SR.ArgumentOutOfRange_OffsetLength);

            if (inArrayLength == 0)
                return String.Empty;

            bool insertLineBreaks = (options == Base64FormattingOptions.InsertLineBreaks);
            //Create the new string.  This is the maximally required length.
            stringLength = ToBase64_CalculateAndValidateOutputLength(length, insertLineBreaks);

            string returnString = new string('\0', stringLength);
            fixed (char* outChars = returnString)
            {
                fixed (byte* inData = inArray)
                {
                    int j = ConvertToBase64Array(outChars, inData, offset, length, insertLineBreaks);
                    System.Diagnostics.Debug.Assert(returnString.Length == j, "returnString.Length == j");
                    return returnString;
                }
            }
        }

        public static int ToBase64CharArray(byte[] inArray, int offsetIn, int length, char[] outArray, int offsetOut)
        {
            Contract.Ensures(Contract.Result<int>() >= 0);
            Contract.Ensures(Contract.Result<int>() <= outArray.Length);
            Contract.EndContractBlock();

            return ToBase64CharArray(inArray, offsetIn, length, outArray, offsetOut, Base64FormattingOptions.None);
        }

        [System.Runtime.InteropServices.ComVisible(false)]
        internal static unsafe int ToBase64CharArray(byte[] inArray, int offsetIn, int length, char[] outArray, int offsetOut, Base64FormattingOptions options)
        {
            //Do data verfication
            if (inArray == null)
                throw new ArgumentNullException("inArray");
            if (outArray == null)
                throw new ArgumentNullException("outArray");
            if (length < 0)
                throw new ArgumentOutOfRangeException("length", SR.ArgumentOutOfRange_Index);
            if (offsetIn < 0)
                throw new ArgumentOutOfRangeException("offsetIn", SR.ArgumentOutOfRange_GenericPositive);
            if (offsetOut < 0)
                throw new ArgumentOutOfRangeException("offsetOut", SR.ArgumentOutOfRange_GenericPositive);

            if (options < Base64FormattingOptions.None || options > Base64FormattingOptions.InsertLineBreaks)
            {
                throw new ArgumentException(SR.Format(SR.Arg_EnumIllegalVal, (int)options));
            }
            Contract.Ensures(Contract.Result<int>() >= 0);
            Contract.Ensures(Contract.Result<int>() <= outArray.Length);
            Contract.EndContractBlock();


            int retVal;

            int inArrayLength;
            int outArrayLength;
            int numElementsToCopy;

            inArrayLength = inArray.Length;

            if (offsetIn > (int)(inArrayLength - length))
                throw new ArgumentOutOfRangeException("offsetIn", SR.ArgumentOutOfRange_OffsetLength);

            if (inArrayLength == 0)
                return 0;

            bool insertLineBreaks = (options == Base64FormattingOptions.InsertLineBreaks);
            //This is the maximally required length that must be available in the char array
            outArrayLength = outArray.Length;

            // Length of the char buffer required
            numElementsToCopy = ToBase64_CalculateAndValidateOutputLength(length, insertLineBreaks);

            if (offsetOut > (int)(outArrayLength - numElementsToCopy))
                throw new ArgumentOutOfRangeException("offsetOut", SR.ArgumentOutOfRange_OffsetOut);

            fixed (char* outChars = &outArray[offsetOut])
            {
                fixed (byte* inData = inArray)
                {
                    retVal = ConvertToBase64Array(outChars, inData, offsetIn, length, insertLineBreaks);
                }
            }

            return retVal;
        }

        private static unsafe int ConvertToBase64Array(char* outChars, byte* inData, int offset, int length, bool insertLineBreaks)
        {
            int lengthmod3 = length % 3;
            int calcLength = offset + (length - lengthmod3);
            int j = 0;
            int charcount = 0;
            //Convert three bytes at a time to base64 notation.  This will consume 4 chars.
            int i;

            // get a pointer to the base64Table to avoid unnecessary range checking
            fixed (char* base64 = base64Table)
            {
                for (i = offset; i < calcLength; i += 3)
                {
                    if (insertLineBreaks)
                    {
                        if (charcount == base64LineBreakPosition)
                        {
                            outChars[j++] = '\r';
                            outChars[j++] = '\n';
                            charcount = 0;
                        }
                        charcount += 4;
                    }
                    outChars[j] = base64[(inData[i] & 0xfc) >> 2];
                    outChars[j + 1] = base64[((inData[i] & 0x03) << 4) | ((inData[i + 1] & 0xf0) >> 4)];
                    outChars[j + 2] = base64[((inData[i + 1] & 0x0f) << 2) | ((inData[i + 2] & 0xc0) >> 6)];
                    outChars[j + 3] = base64[(inData[i + 2] & 0x3f)];
                    j += 4;
                }

                //Where we left off before
                i = calcLength;

                if (insertLineBreaks && (lengthmod3 != 0) && (charcount == base64LineBreakPosition))
                {
                    outChars[j++] = '\r';
                    outChars[j++] = '\n';
                }

                switch (lengthmod3)
                {
                    case 2: //One character padding needed
                        outChars[j] = base64[(inData[i] & 0xfc) >> 2];
                        outChars[j + 1] = base64[((inData[i] & 0x03) << 4) | ((inData[i + 1] & 0xf0) >> 4)];
                        outChars[j + 2] = base64[(inData[i + 1] & 0x0f) << 2];
                        outChars[j + 3] = base64[64]; //Pad
                        j += 4;
                        break;
                    case 1: // Two character padding needed
                        outChars[j] = base64[(inData[i] & 0xfc) >> 2];
                        outChars[j + 1] = base64[(inData[i] & 0x03) << 4];
                        outChars[j + 2] = base64[64]; //Pad
                        outChars[j + 3] = base64[64]; //Pad
                        j += 4;
                        break;
                }
            }

            return j;
        }

        private static int ToBase64_CalculateAndValidateOutputLength(int inputLength, bool insertLineBreaks)
        {
            long outlen = ((long)inputLength) / 3 * 4;          // the base length - we want integer division here. 
            outlen += ((inputLength % 3) != 0) ? 4 : 0;         // at most 4 more chars for the remainder

            if (outlen == 0)
                return 0;

            if (insertLineBreaks)
            {
                long newLines = outlen / base64LineBreakPosition;
                if ((outlen % base64LineBreakPosition) == 0)
                {
                    --newLines;
                }
                outlen += newLines * 2;              // the number of line break chars we'll add, "\r\n"
            }

            // If we overflow an int then we cannot allocate enough
            // memory to output the value so throw
            if (outlen > int.MaxValue)
                throw new OutOfMemoryException();

            return (int)outlen;
        }


        /// <summary>
        /// Converts the specified string, which encodes binary data as Base64 digits, to the equivalent byte array.
        /// </summary>
        /// <param name="s">The string to convert</param>
        /// <returns>The array of bytes represented by the specifed Base64 string.</returns>
        public static Byte[] FromBase64String(String s)
        {
            // "s" is an unfortunate parameter name, but we need to keep it for backward compat.

            if (s == null)
                throw new ArgumentNullException("s");

            Contract.EndContractBlock();

            unsafe
            {
                fixed (Char* sPtr = s)
                {
                    return FromBase64CharPtr(sPtr, s.Length);
                }
            }
        }


        /// <summary>
        /// Converts the specified range of a Char array, which encodes binary data as Base64 digits, to the equivalent byte array.     
        /// </summary>
        /// <param name="inArray">Chars representing Base64 encoding characters</param>
        /// <param name="offset">A position within the input array.</param>
        /// <param name="length">Number of element to convert.</param>
        /// <returns>The array of bytes represented by the specified Base64 encoding characters.</returns>
        public static Byte[] FromBase64CharArray(Char[] inArray, Int32 offset, Int32 length)
        {
            if (inArray == null)
                throw new ArgumentNullException("inArray");

            if (length < 0)
                throw new ArgumentOutOfRangeException("length", SR.ArgumentOutOfRange_Index);

            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset", SR.ArgumentOutOfRange_GenericPositive);

            if (offset > inArray.Length - length)
                throw new ArgumentOutOfRangeException("offset", SR.ArgumentOutOfRange_OffsetLength);

            Contract.EndContractBlock();

            unsafe
            {
                fixed (Char* inArrayPtr = inArray)
                {
                    return FromBase64CharPtr(inArrayPtr + offset, length);
                }
            }
        }



        /// <summary>
        /// Convert Base64 encoding characters to bytes:
        ///  - Compute result length exactly by actually walking the input;
        ///  - Allocate new result array based on computation;
        ///  - Decode input into the new array;
        /// </summary>
        /// <param name="inputPtr">Pointer to the first input char</param>
        /// <param name="inputLength">Number of input chars</param>
        /// <returns></returns>
        private static unsafe Byte[] FromBase64CharPtr(Char* inputPtr, Int32 inputLength)
        {
            // The validity of parameters much be checked by callers, thus we are Critical here.

            Debug.Assert(0 <= inputLength);

            // We need to get rid of any trailing white spaces.
            // Otherwise we would be rejecting input such as "abc= ":
            while (inputLength > 0)
            {
                Int32 lastChar = inputPtr[inputLength - 1];
                if (lastChar != (Int32)' ' && lastChar != (Int32)'\n' && lastChar != (Int32)'\r' && lastChar != (Int32)'\t')
                    break;
                inputLength--;
            }

            // Compute the output length:
            Int32 resultLength = FromBase64_ComputeResultLength(inputPtr, inputLength);

            Debug.Assert(0 <= resultLength);

            // resultLength can be zero. We will still enter FromBase64_Decode and process the input.
            // It may either simply write no bytes (e.g. input = " ") or throw (e.g. input = "ab").

            // Create result byte blob:
            Byte[] decodedBytes = new Byte[resultLength];

            // Convert Base64 chars into bytes:
            Int32 actualResultLength;
            fixed (Byte* decodedBytesPtr = decodedBytes)
                actualResultLength = FromBase64_Decode(inputPtr, inputLength, decodedBytesPtr, resultLength);

            // Note that actualResultLength can differ from resultLength if the caller is modifying the array
            // as it is being converted. Silently ignore the failure.
            // Consider throwing exception in an non in-place release.

            // We are done:
            return decodedBytes;
        }


        /// <summary>
        /// Decode characters representing a Base64 encoding into bytes:
        /// Walk the input. Every time 4 chars are read, convert them to the 3 corresponding output bytes.
        /// This method is a bit lengthy on purpose. We are trying to avoid jumps to helpers in the loop
        /// to aid performance.
        /// </summary>
        /// <param name="inputPtr">Pointer to first input char</param>
        /// <param name="inputLength">Number of input chars</param>
        /// <param name="destPtr">Pointer to location for teh first result byte</param>
        /// <param name="destLength">Max length of the preallocated result buffer</param>
        /// <returns>If the result buffer was not large enough to write all result bytes, return -1;
        /// Otherwise return the number of result bytes actually produced.</returns>
        private static unsafe Int32 FromBase64_Decode(Char* startInputPtr, Int32 inputLength, Byte* startDestPtr, Int32 destLength)
        {
            // You may find this method weird to look at. It’s written for performance, not aesthetics.
            // You will find unrolled loops label jumps and bit manipulations.

            const UInt32 intA = (UInt32)'A';
            const UInt32 inta = (UInt32)'a';
            const UInt32 int0 = (UInt32)'0';
            const UInt32 intEq = (UInt32)'=';
            const UInt32 intPlus = (UInt32)'+';
            const UInt32 intSlash = (UInt32)'/';
            const UInt32 intSpace = (UInt32)' ';
            const UInt32 intTab = (UInt32)'\t';
            const UInt32 intNLn = (UInt32)'\n';
            const UInt32 intCRt = (UInt32)'\r';
            const UInt32 intAtoZ = (UInt32)('Z' - 'A');  // = ('z' - 'a')
            const UInt32 int0to9 = (UInt32)('9' - '0');

            Char* inputPtr = startInputPtr;
            Byte* destPtr = startDestPtr;

            // Pointers to the end of input and output:
            Char* endInputPtr = inputPtr + inputLength;
            Byte* endDestPtr = destPtr + destLength;

            // Current char code/value:
            UInt32 currCode;

            // This 4-byte integer will contain the 4 codes of the current 4-char group.
            // Eeach char codes for 6 bits = 24 bits.
            // The remaining byte will be FF, we use it as a marker when 4 chars have been processed.            
            UInt32 currBlockCodes = 0x000000FFu;

            unchecked
            {
                while (true)
                {
                    // break when done:
                    if (inputPtr >= endInputPtr)
                        goto _AllInputConsumed;

                    // Get current char:
                    currCode = (UInt32)(*inputPtr);
                    inputPtr++;

                    // Determine current char code:

                    if (currCode - intA <= intAtoZ)
                        currCode -= intA;

                    else if (currCode - inta <= intAtoZ)
                        currCode -= (inta - 26u);

                    else if (currCode - int0 <= int0to9)
                        currCode -= (int0 - 52u);

                    else
                    {
                        // Use the slower switch for less common cases:
                        switch (currCode)
                        {
                            // Significant chars:
                            case intPlus:
                                currCode = 62u;
                                break;

                            case intSlash:
                                currCode = 63u;
                                break;

                            // Legal no-value chars (we ignore these):
                            case intCRt:
                            case intNLn:
                            case intSpace:
                            case intTab:
                                continue;

                            // The equality char is only legal at the end of the input.
                            // Jump after the loop to make it easier for the JIT register predictor to do a good job for the loop itself:
                            case intEq:
                                goto _EqualityCharEncountered;

                            // Other chars are illegal:
                            default:
                                throw new FormatException(SR.Format_BadBase64Char);
                        }
                    }

                    // Ok, we got the code. Save it:
                    currBlockCodes = (currBlockCodes << 6) | currCode;

                    // Last bit in currBlockCodes will be on after in shifted right 4 times:
                    if ((currBlockCodes & 0x80000000u) != 0u)
                    {
                        if ((Int32)(endDestPtr - destPtr) < 3)
                            return -1;

                        *(destPtr) = (Byte)(currBlockCodes >> 16);
                        *(destPtr + 1) = (Byte)(currBlockCodes >> 8);
                        *(destPtr + 2) = (Byte)(currBlockCodes);
                        destPtr += 3;

                        currBlockCodes = 0x000000FFu;
                    }
                }
            }  // unchecked while

        // 'd be nice to have an assert that we never get here, but CS0162: Unreachable code detected.
        // Debug.Assert(false, "We only leave the above loop by jumping; should never get here.");

        // We jump here out of the loop if we hit an '=':
        _EqualityCharEncountered:

            Debug.Assert(currCode == intEq);

            // Recall that inputPtr is now one position past where '=' was read.
            // '=' can only be at the last input pos:
            if (inputPtr == endInputPtr)
            {
                // Code is zero for trailing '=':
                currBlockCodes <<= 6;

                // The '=' did not complete a 4-group. The input must be bad:
                if ((currBlockCodes & 0x80000000u) == 0u)
                    throw new FormatException(SR.Format_BadBase64CharArrayLength);

                if ((int)(endDestPtr - destPtr) < 2)  // Autch! We underestimated the output length!
                    return -1;

                // We are good, store bytes form this past group. We had a single "=", so we take two bytes:
                *(destPtr++) = (Byte)(currBlockCodes >> 16);
                *(destPtr++) = (Byte)(currBlockCodes >> 8);

                currBlockCodes = 0x000000FFu;
            }
            else
            { // '=' can also be at the pre-last position iff the last is also a '=' excluding the white spaces:
                // We need to get rid of any intermediate white spaces.
                // Otherwise we would be rejecting input such as "abc= =":
                while (inputPtr < (endInputPtr - 1))
                {
                    Int32 lastChar = *(inputPtr);
                    if (lastChar != (Int32)' ' && lastChar != (Int32)'\n' && lastChar != (Int32)'\r' && lastChar != (Int32)'\t')
                        break;
                    inputPtr++;
                }

                if (inputPtr == (endInputPtr - 1) && *(inputPtr) == '=')
                {
                    // Code is zero for each of the two '=':
                    currBlockCodes <<= 12;

                    // The '=' did not complete a 4-group. The input must be bad:
                    if ((currBlockCodes & 0x80000000u) == 0u)
                        throw new FormatException(SR.Format_BadBase64CharArrayLength);

                    if ((Int32)(endDestPtr - destPtr) < 1)  // Autch! We underestimated the output length!
                        return -1;

                    // We are good, store bytes form this past group. We had a "==", so we take only one byte:
                    *(destPtr++) = (Byte)(currBlockCodes >> 16);

                    currBlockCodes = 0x000000FFu;
                }
                else  // '=' is not ok at places other than the end:
                    throw new FormatException(SR.Format_BadBase64Char);
            }

        // We get here either from above or by jumping out of the loop:
        _AllInputConsumed:

            // The last block of chars has less than 4 items
            if (currBlockCodes != 0x000000FFu)
                throw new FormatException(SR.Format_BadBase64CharArrayLength);

            // Return how many bytes were actually recovered:
            return (Int32)(destPtr - startDestPtr);
        } // Int32 FromBase64_Decode(...)


        /// <summary>
        /// Compute the number of bytes encoded in the specified Base 64 char array:
        /// Walk the entire input counting white spaces and padding chars, then compute result length
        /// based on 3 bytes per 4 chars.
        /// </summary>
        private static unsafe Int32 FromBase64_ComputeResultLength(Char* inputPtr, Int32 inputLength)
        {
            const UInt32 intEq = (UInt32)'=';
            const UInt32 intSpace = (UInt32)' ';

            Debug.Assert(0 <= inputLength);

            Char* inputEndPtr = inputPtr + inputLength;
            Int32 usefulInputLength = inputLength;
            Int32 padding = 0;

            while (inputPtr < inputEndPtr)
            {
                UInt32 c = (UInt32)(*inputPtr);
                inputPtr++;

                // We want to be as fast as possible and filter out spaces with as few comparisons as possible.
                // We end up accepting a number of illegal chars as legal white-space chars.
                // This is ok: as soon as we hit them during actual decode we will recognise them as illegal and throw.
                if (c <= intSpace)
                    usefulInputLength--;

                else if (c == intEq)
                {
                    usefulInputLength--;
                    padding++;
                }
            }

            Debug.Assert(0 <= usefulInputLength);

            // For legal input, we can assume that 0 <= padding < 3. But it may be more for illegal input.
            // We will notice it at decode when we see a '=' at the wrong place.
            Debug.Assert(0 <= padding);

            // Perf: reuse the variable that stored the number of '=' to store the number of bytes encoded by the
            // last group that contains the '=':
            if (padding != 0)
            {
                if (padding == 1)
                    padding = 2;
                else if (padding == 2)
                    padding = 1;
                else
                    throw new FormatException(SR.Format_BadBase64Char);
            }

            // Done:
            return (usefulInputLength / 4) * 3 + padding;
        }
    }  // class Convert
}  // namespace

