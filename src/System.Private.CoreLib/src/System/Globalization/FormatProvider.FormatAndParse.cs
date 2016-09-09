// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.Contracts;

namespace System.Globalization
{
    internal partial class FormatProvider
    {
        private partial class Number
        {
            private const Int32 INT32_PRECISION = 10;
            private const Int32 UINT32_PRECISION = INT32_PRECISION;
            private const Int32 INT64_PRECISION = 19;
            private const Int32 UINT64_PRECISION = 20;
            private const int FLOAT_PRECISION = 7;
            private const int DOUBLE_PRECISION = 15;

            private static Boolean HexNumberToInt32(ref NumberBuffer number, ref Int32 value)
            {
                UInt32 passedValue = 0;
                Boolean returnValue = HexNumberToUInt32(ref number, ref passedValue);
                value = (Int32)passedValue;
                return returnValue;
            }

            private static Boolean HexNumberToInt64(ref NumberBuffer number, ref Int64 value)
            {
                UInt64 passedValue = 0;
                Boolean returnValue = HexNumberToUInt64(ref number, ref passedValue);
                value = (Int64)passedValue;
                return returnValue;
            }

            private unsafe static Boolean HexNumberToUInt32(ref NumberBuffer number, ref UInt32 value)
            {
                Int32 i = number.scale;
                if (i > UINT32_PRECISION || i < number.precision)
                {
                    return false;
                }
                Char* p = number.digits;
                Debug.Assert(p != null, "");

                UInt32 n = 0;
                while (--i >= 0)
                {
                    if (n > ((UInt32)0xFFFFFFFF / 16))
                    {
                        return false;
                    }
                    n *= 16;
                    if (*p != '\0')
                    {
                        UInt32 newN = n;
                        if (*p != '\0')
                        {
                            if (*p >= '0' && *p <= '9')
                            {
                                newN += (UInt32)(*p - '0');
                            }
                            else
                            {
                                if (*p >= 'A' && *p <= 'F')
                                {
                                    newN += (UInt32)((*p - 'A') + 10);
                                }
                                else
                                {
                                    Debug.Assert(*p >= 'a' && *p <= 'f', "");
                                    newN += (UInt32)((*p - 'a') + 10);
                                }
                            }
                            p++;
                        }

                        // Detect an overflow here...
                        if (newN < n)
                        {
                            return false;
                        }
                        n = newN;
                    }
                }
                value = n;
                return true;
            }

            private unsafe static Boolean HexNumberToUInt64(ref NumberBuffer number, ref UInt64 value)
            {
                Int32 i = number.scale;
                if (i > UINT64_PRECISION || i < number.precision)
                {
                    return false;
                }
                Char* p = number.digits;
                Debug.Assert(p != null, "");

                UInt64 n = 0;
                while (--i >= 0)
                {
                    if (n > (0xFFFFFFFFFFFFFFFF / 16))
                    {
                        return false;
                    }
                    n *= 16;
                    if (*p != '\0')
                    {
                        UInt64 newN = n;
                        if (*p != '\0')
                        {
                            if (*p >= '0' && *p <= '9')
                            {
                                newN += (UInt64)(*p - '0');
                            }
                            else
                            {
                                if (*p >= 'A' && *p <= 'F')
                                {
                                    newN += (UInt64)((*p - 'A') + 10);
                                }
                                else
                                {
                                    Debug.Assert(*p >= 'a' && *p <= 'f', "");
                                    newN += (UInt64)((*p - 'a') + 10);
                                }
                            }
                            p++;
                        }

                        // Detect an overflow here...
                        if (newN < n)
                        {
                            return false;
                        }
                        n = newN;
                    }
                }
                value = n;
                return true;
            }

            private unsafe static Boolean NumberToInt32(ref NumberBuffer number, ref Int32 value)
            {
                Int32 i = number.scale;
                if (i > INT32_PRECISION || i < number.precision)
                {
                    return false;
                }
                char* p = number.digits;
                Debug.Assert(p != null, "");
                Int32 n = 0;
                while (--i >= 0)
                {
                    if ((UInt32)n > (0x7FFFFFFF / 10))
                    {
                        return false;
                    }
                    n *= 10;
                    if (*p != '\0')
                    {
                        n += (Int32)(*p++ - '0');
                    }
                }
                if (number.sign)
                {
                    n = -n;
                    if (n > 0)
                    {
                        return false;
                    }
                }
                else
                {
                    if (n < 0)
                    {
                        return false;
                    }
                }
                value = n;
                return true;
            }

            private unsafe static Boolean NumberToInt64(ref NumberBuffer number, ref Int64 value)
            {
                Int32 i = number.scale;
                if (i > INT64_PRECISION || i < number.precision)
                {
                    return false;
                }
                char* p = number.digits;
                Debug.Assert(p != null, "");
                Int64 n = 0;
                while (--i >= 0)
                {
                    if ((UInt64)n > (0x7FFFFFFFFFFFFFFF / 10))
                    {
                        return false;
                    }
                    n *= 10;
                    if (*p != '\0')
                    {
                        n += (Int32)(*p++ - '0');
                    }
                }
                if (number.sign)
                {
                    n = -n;
                    if (n > 0)
                    {
                        return false;
                    }
                }
                else
                {
                    if (n < 0)
                    {
                        return false;
                    }
                }
                value = n;
                return true;
            }

            private unsafe static Boolean NumberToUInt32(ref NumberBuffer number, ref UInt32 value)
            {
                Int32 i = number.scale;
                if (i > UINT32_PRECISION || i < number.precision || number.sign)
                {
                    return false;
                }
                char* p = number.digits;
                Debug.Assert(p != null, "");
                UInt32 n = 0;
                while (--i >= 0)
                {
                    if (n > (0xFFFFFFFF / 10))
                    {
                        return false;
                    }
                    n *= 10;
                    if (*p != '\0')
                    {
                        UInt32 newN = n + (UInt32)(*p++ - '0');
                        // Detect an overflow here...
                        if (newN < n)
                        {
                            return false;
                        }
                        n = newN;
                    }
                }
                value = n;
                return true;
            }

            private unsafe static Boolean NumberToUInt64(ref NumberBuffer number, ref UInt64 value)
            {
                Int32 i = number.scale;
                if (i > UINT64_PRECISION || i < number.precision || number.sign)
                {
                    return false;
                }
                char* p = number.digits;
                Debug.Assert(p != null, "");
                UInt64 n = 0;
                while (--i >= 0)
                {
                    if (n > (0xFFFFFFFFFFFFFFFF / 10))
                    {
                        return false;
                    }
                    n *= 10;
                    if (*p != '\0')
                    {
                        UInt64 newN = n + (UInt64)(*p++ - '0');
                        // Detect an overflow here...
                        if (newN < n)
                        {
                            return false;
                        }
                        n = newN;
                    }
                }
                value = n;
                return true;
            }

            internal static Decimal ParseDecimal(String value, NumberStyles options, IFormatProvider provider)
            {
                NumberFormatInfo numfmt = provider == null ? NumberFormatInfo.CurrentInfo : NumberFormatInfo.GetInstance(provider);
                NumberBuffer number = new NumberBuffer();
                Decimal result = 0;

                StringToNumber(value, options, ref number, numfmt, true);

                if (!NumberBufferToDecimal(number, ref result))
                    throw new OverflowException(SR.Overflow_Decimal);

                return result;
            }

            internal unsafe static Double ParseDouble(String value, NumberStyles options, IFormatProvider provider)
            {
                if (value == null)
                {
                    throw new ArgumentNullException("value");
                }

                NumberFormatInfo numfmt = provider == null ? NumberFormatInfo.CurrentInfo : NumberFormatInfo.GetInstance(provider);
                NumberBuffer number = new NumberBuffer();
                Double d = 0;

                if (!TryStringToNumber(value, options, ref number, numfmt, false))
                {
                    //If we failed TryStringToNumber, it may be from one of our special strings.
                    //Check the three with which we're concerned and rethrow if it's not one of
                    //those strings.
                    String sTrim = value.Trim();
                    if (sTrim.Equals(numfmt.PositiveInfinitySymbol))
                    {
                        return Double.PositiveInfinity;
                    }
                    if (sTrim.Equals(numfmt.NegativeInfinitySymbol))
                    {
                        return Double.NegativeInfinity;
                    }
                    if (sTrim.Equals(numfmt.NaNSymbol))
                    {
                        return Double.NaN;
                    }
                    throw new FormatException(SR.Format_InvalidString);
                }

                if (!NumberBufferToDouble(number, ref d))
                {
                    throw new OverflowException(SR.Overflow_Double);
                }

                return d;
            }

            internal unsafe static Int32 ParseInt32(String s, NumberStyles style, IFormatProvider provider)
            {
                NumberFormatInfo info = provider == null ? NumberFormatInfo.CurrentInfo : NumberFormatInfo.GetInstance(provider);

                NumberBuffer number = new NumberBuffer();
                Int32 i = 0;

                StringToNumber(s, style, ref number, info, false);

                if ((style & NumberStyles.AllowHexSpecifier) != 0)
                {
                    if (!HexNumberToInt32(ref number, ref i))
                    {
                        throw new OverflowException(SR.Overflow_Int32);
                    }
                }
                else
                {
                    if (!NumberToInt32(ref number, ref i))
                    {
                        throw new OverflowException(SR.Overflow_Int32);
                    }
                }
                return i;
            }

            internal unsafe static Int64 ParseInt64(String value, NumberStyles options, IFormatProvider provider)
            {
                NumberFormatInfo numfmt = provider == null ? NumberFormatInfo.CurrentInfo : NumberFormatInfo.GetInstance(provider);

                NumberBuffer number = new NumberBuffer();
                Int64 i = 0;

                StringToNumber(value, options, ref number, numfmt, false);

                if ((options & NumberStyles.AllowHexSpecifier) != 0)
                {
                    if (!HexNumberToInt64(ref number, ref i))
                    {
                        throw new OverflowException(SR.Overflow_Int64);
                    }
                }
                else
                {
                    if (!NumberToInt64(ref number, ref i))
                    {
                        throw new OverflowException(SR.Overflow_Int64);
                    }
                }
                return i;
            }

            internal unsafe static Single ParseSingle(String value, NumberStyles options, IFormatProvider provider)
            {
                if (value == null)
                {
                    throw new ArgumentNullException("value");
                }

                NumberFormatInfo numfmt = provider == null ? NumberFormatInfo.CurrentInfo : NumberFormatInfo.GetInstance(provider);
                NumberBuffer number = new NumberBuffer();
                Double d = 0;

                if (!TryStringToNumber(value, options, ref number, numfmt, false))
                {
                    //If we failed TryStringToNumber, it may be from one of our special strings.
                    //Check the three with which we're concerned and rethrow if it's not one of
                    //those strings.
                    String sTrim = value.Trim();
                    if (sTrim.Equals(numfmt.PositiveInfinitySymbol))
                    {
                        return Single.PositiveInfinity;
                    }
                    if (sTrim.Equals(numfmt.NegativeInfinitySymbol))
                    {
                        return Single.NegativeInfinity;
                    }
                    if (sTrim.Equals(numfmt.NaNSymbol))
                    {
                        return Single.NaN;
                    }
                    throw new FormatException(SR.Format_InvalidString);
                }

                if (!NumberBufferToDouble(number, ref d))
                {
                    throw new OverflowException(SR.Overflow_Single);
                }
                Single castSingle = (Single)d;
                if (Single.IsInfinity(castSingle))
                {
                    throw new OverflowException(SR.Overflow_Single);
                }
                return castSingle;
            }

            internal unsafe static UInt32 ParseUInt32(String value, NumberStyles options, IFormatProvider provider)
            {
                NumberFormatInfo numfmt = provider == null ? NumberFormatInfo.CurrentInfo : NumberFormatInfo.GetInstance(provider);

                NumberBuffer number = new NumberBuffer();
                UInt32 i = 0;

                StringToNumber(value, options, ref number, numfmt, false);

                if ((options & NumberStyles.AllowHexSpecifier) != 0)
                {
                    if (!HexNumberToUInt32(ref number, ref i))
                    {
                        throw new OverflowException(SR.Overflow_UInt32);
                    }
                }
                else
                {
                    if (!NumberToUInt32(ref number, ref i))
                    {
                        throw new OverflowException(SR.Overflow_UInt32);
                    }
                }

                return i;
            }

            internal unsafe static UInt64 ParseUInt64(String value, NumberStyles options, IFormatProvider provider)
            {
                NumberFormatInfo numfmt = provider == null ? NumberFormatInfo.CurrentInfo : NumberFormatInfo.GetInstance(provider);
                NumberBuffer number = new NumberBuffer();
                UInt64 i = 0;

                StringToNumber(value, options, ref number, numfmt, false);
                if ((options & NumberStyles.AllowHexSpecifier) != 0)
                {
                    if (!HexNumberToUInt64(ref number, ref i))
                    {
                        throw new OverflowException(SR.Overflow_UInt64);
                    }
                }
                else
                {
                    if (!NumberToUInt64(ref number, ref i))
                    {
                        throw new OverflowException(SR.Overflow_UInt64);
                    }
                }
                return i;
            }

            private unsafe static void StringToNumber(String str, NumberStyles options, ref NumberBuffer number, NumberFormatInfo info, Boolean parseDecimal)
            {
                if (str == null)
                {
                    throw new ArgumentNullException("String");
                }
                Contract.EndContractBlock();
                Debug.Assert(info != null, "");
                fixed (char* stringPointer = str)
                {
                    char* p = stringPointer;
                    if (!ParseNumber(ref p, options, ref number, null, info, parseDecimal)
                        || (p - stringPointer < str.Length && !TrailingZeros(str, (int)(p - stringPointer))))
                    {
                        throw new FormatException(SR.Format_InvalidString);
                    }
                }
            }

            internal unsafe static Boolean TryParseDecimal(String value, NumberStyles options, IFormatProvider provider, out Decimal result)
            {
                NumberFormatInfo numfmt = provider == null ? NumberFormatInfo.CurrentInfo : NumberFormatInfo.GetInstance(provider);

                NumberBuffer number = new NumberBuffer();
                result = 0;

                if (!TryStringToNumber(value, options, ref number, numfmt, true))
                {
                    return false;
                }

                if (!NumberBufferToDecimal(number, ref result))
                {
                    return false;
                }
                return true;
            }

            internal unsafe static Boolean TryParseDouble(String value, NumberStyles options, IFormatProvider provider, out Double result)
            {
                NumberFormatInfo numfmt = provider == null ? NumberFormatInfo.CurrentInfo : NumberFormatInfo.GetInstance(provider);
                NumberBuffer number = new NumberBuffer();
                result = 0;


                if (!TryStringToNumber(value, options, ref number, numfmt, false))
                {
                    return false;
                }
                if (!NumberBufferToDouble(number, ref result))
                {
                    return false;
                }
                return true;
            }

            internal unsafe static Boolean TryParseInt32(String s, NumberStyles style, IFormatProvider provider, out Int32 result)
            {
                NumberFormatInfo info = provider == null ? NumberFormatInfo.CurrentInfo : NumberFormatInfo.GetInstance(provider);

                NumberBuffer number = new NumberBuffer();
                result = 0;

                if (!TryStringToNumber(s, style, ref number, info, false))
                {
                    return false;
                }

                if ((style & NumberStyles.AllowHexSpecifier) != 0)
                {
                    if (!HexNumberToInt32(ref number, ref result))
                    {
                        return false;
                    }
                }
                else
                {
                    if (!NumberToInt32(ref number, ref result))
                    {
                        return false;
                    }
                }
                return true;
            }

            internal unsafe static Boolean TryParseInt64(String s, NumberStyles style, IFormatProvider provider, out Int64 result)
            {
                NumberFormatInfo info = provider == null ? NumberFormatInfo.CurrentInfo : NumberFormatInfo.GetInstance(provider);

                NumberBuffer number = new NumberBuffer();
                result = 0;

                if (!TryStringToNumber(s, style, ref number, info, false))
                {
                    return false;
                }

                if ((style & NumberStyles.AllowHexSpecifier) != 0)
                {
                    if (!HexNumberToInt64(ref number, ref result))
                    {
                        return false;
                    }
                }
                else
                {
                    if (!NumberToInt64(ref number, ref result))
                    {
                        return false;
                    }
                }
                return true;
            }

            internal unsafe static Boolean TryParseSingle(String value, NumberStyles options, IFormatProvider provider, out Single result)
            {
                NumberFormatInfo numfmt = provider == null ? NumberFormatInfo.CurrentInfo : NumberFormatInfo.GetInstance(provider);
                NumberBuffer number = new NumberBuffer();
                result = 0;
                Double d = 0;

                if (!TryStringToNumber(value, options, ref number, numfmt, false))
                {
                    return false;
                }
                if (!NumberBufferToDouble(number, ref d))
                {
                    return false;
                }
                Single castSingle = (Single)d;
                if (Single.IsInfinity(castSingle))
                {
                    return false;
                }

                result = castSingle;
                return true;
            }

            internal unsafe static Boolean TryParseUInt32(String s, NumberStyles style, IFormatProvider provider, out UInt32 result)
            {
                NumberFormatInfo info = provider == null ? NumberFormatInfo.CurrentInfo : NumberFormatInfo.GetInstance(provider);

                NumberBuffer number = new NumberBuffer();
                result = 0;

                if (!TryStringToNumber(s, style, ref number, info, false))
                {
                    return false;
                }

                if ((style & NumberStyles.AllowHexSpecifier) != 0)
                {
                    if (!HexNumberToUInt32(ref number, ref result))
                    {
                        return false;
                    }
                }
                else
                {
                    if (!NumberToUInt32(ref number, ref result))
                    {
                        return false;
                    }
                }
                return true;
            }

            internal unsafe static Boolean TryParseUInt64(String s, NumberStyles style, IFormatProvider provider, out UInt64 result)
            {
                NumberFormatInfo info = provider == null ? NumberFormatInfo.CurrentInfo : NumberFormatInfo.GetInstance(provider);

                NumberBuffer number = new NumberBuffer();
                result = 0;

                if (!TryStringToNumber(s, style, ref number, info, false))
                {
                    return false;
                }

                if ((style & NumberStyles.AllowHexSpecifier) != 0)
                {
                    if (!HexNumberToUInt64(ref number, ref result))
                    {
                        return false;
                    }
                }
                else
                {
                    if (!NumberToUInt64(ref number, ref result))
                    {
                        return false;
                    }
                }
                return true;
            }

            internal static Boolean TryStringToNumber(String str, NumberStyles options, ref NumberBuffer number, NumberFormatInfo numfmt, Boolean parseDecimal)
            {
                return TryStringToNumber(str, options, ref number, null, numfmt, parseDecimal);
            }

            // **********************************************************************************************************
            //
            // The remaining code in this module is an almost direct translation from the original unmanaged version in
            // the CLR. The code uses NumberBuffer directly instead of an analog of the NUMBER unmanaged data structure
            // but this causes next to no differences since we've modified NumberBuffer to take account of the changes (it
            // has an inline array of digits and no need of a pack operation to prepare for use by the "unmanaged" code).
            //
            // Some minor cleanup has been done (e.g. taking advantage of StringBuilder instead of having to precompute
            // string buffer sizes) but there's still plenty of opportunity to further C#'ize this code and potentially
            // better unify it with the code above.
            //

            private const int SCALE_NAN = unchecked((int)0x80000000);
            private const int SCALE_INF = 0x7FFFFFFF;

            private const int _CVTBUFSIZE = 349;

            public static String FormatDecimal(Decimal value, String format, IFormatProvider provider)
            {
                NumberFormatInfo info = provider == null ? NumberFormatInfo.CurrentInfo : NumberFormatInfo.GetInstance(provider);

                NumberBuffer number = new NumberBuffer();
                DecimalToNumber(value, ref number);

                int digits;
                char fmt = ParseFormatSpecifier(format, out digits);
                if (fmt != 0)
                    return NumberToString(number, fmt, digits, info, true);
                else
                    return NumberToStringFormat(number, format, info);
            }

            public static String FormatDouble(double value, String format, IFormatProvider provider)
            {
                NumberFormatInfo info = provider == null ? NumberFormatInfo.CurrentInfo : NumberFormatInfo.GetInstance(provider);

                int digits;
                char fmt = ParseFormatSpecifier(format, out digits);

                int precision = DOUBLE_PRECISION;
                NumberBuffer number = new NumberBuffer();

                switch (fmt)
                {
                    case 'R':
                    case 'r':
                        {
                            // In order to give numbers that are both friendly to display and round-trippable, we parse
                            // the number using 15 digits and then determine if it round trips to the same value. If it
                            // does, we convert that NUMBER to a string, otherwise we reparse using 17 digits and display
                            // that.
                            DoubleToNumber(value, DOUBLE_PRECISION, ref number);
                            if (number.scale == SCALE_NAN)
                                return info.NaNSymbol;
                            if (number.scale == SCALE_INF)
                                return number.sign ? info.NegativeInfinitySymbol : info.PositiveInfinitySymbol;

                            if (NumberToDouble(number) == value)
                                return NumberToString(number, 'G', DOUBLE_PRECISION, info, false);

                            DoubleToNumber(value, 17, ref number);

                            return NumberToString(number, 'G', 17, info, false);
                        }

                    case 'E':
                    case 'e':
                        // Here we round values less than E14 to 15 digits
                        if (digits > 14)
                            precision = 17;
                        break;

                    case 'G':
                    case 'g':
                        // Here we round values less than G15 to 15 digits, G16 and G17 will not be touched
                        if (digits > 15)
                            precision = 17;
                        break;
                }

                DoubleToNumber(value, precision, ref number);
                if (number.scale == SCALE_NAN)
                    return info.NaNSymbol;
                if (number.scale == SCALE_INF)
                    return number.sign ? info.NegativeInfinitySymbol : info.PositiveInfinitySymbol;

                if (fmt != 0)
                    return NumberToString(number, fmt, digits, info, false);
                else
                    return NumberToStringFormat(number, format, info);
            }

            public static String FormatSingle(float value, String format, IFormatProvider provider)
            {
                NumberFormatInfo info = provider == null ? NumberFormatInfo.CurrentInfo : NumberFormatInfo.GetInstance(provider);

                int digits;
                char fmt = ParseFormatSpecifier(format, out digits);

                int precision = FLOAT_PRECISION;
                NumberBuffer number = new NumberBuffer();

                switch (fmt)
                {
                    case 'R':
                    case 'r':
                        {
                            // In order to give numbers that are both friendly to display and round-trippable, we parse
                            // the number using 7 digits and then determine if it round trips to the same value. If it
                            // does, we convert that NUMBER to a string, otherwise we reparse using 9 digits and display
                            // that.
                            DoubleToNumber(value, FLOAT_PRECISION, ref number);
                            if (number.scale == SCALE_NAN)
                                return info.NaNSymbol;
                            if (number.scale == SCALE_INF)
                                return number.sign ? info.NegativeInfinitySymbol : info.PositiveInfinitySymbol;

                            if ((float)NumberToDouble(number) == value)
                                return NumberToString(number, 'G', FLOAT_PRECISION, info, false);

                            DoubleToNumber(value, 9, ref number);

                            return NumberToString(number, 'G', 9, info, false);
                        }

                    case 'E':
                    case 'e':
                        // Here we round values less than E14 to 15 digits
                        if (digits > 6)
                            precision = 9;
                        break;

                    case 'G':
                    case 'g':
                        // Here we round values less than G15 to 15 digits, G16 and G17 will not be touched
                        if (digits > 7)
                            precision = 9;
                        break;
                }

                DoubleToNumber(value, precision, ref number);
                if (number.scale == SCALE_NAN)
                    return info.NaNSymbol;
                if (number.scale == SCALE_INF)
                    return number.sign ? info.NegativeInfinitySymbol : info.PositiveInfinitySymbol;

                if (fmt != 0)
                    return NumberToString(number, fmt, digits, info, false);
                else
                    return NumberToStringFormat(number, format, info);
            }

            public static String FormatInt32(int value, String format, IFormatProvider provider)
            {
                NumberFormatInfo info = provider == null ? NumberFormatInfo.CurrentInfo : NumberFormatInfo.GetInstance(provider);

                int digits;
                char fmt = ParseFormatSpecifier(format, out digits);

                // ANDing fmt with FFDF has the effect of uppercasing the character because we've removed the bit
                // that marks lower-case.
                switch (fmt)
                {
                    case 'G':
                    case 'g':
                        if (digits > 0)
                        {
                            NumberBuffer number = new NumberBuffer();
                            Int32ToNumber(value, ref number);
                            if (fmt != 0)
                                return NumberToString(number, fmt, digits, info, false);
                            return NumberToStringFormat(number, format, info);
                        }
                        // fall through
                        goto case 'D';

                    case 'D':
                    case 'd':
                        return Int32ToDecStr(value, digits, info.NegativeSign);

                    case 'X':
                    case 'x':
                        // The fmt-(X-A+10) hack has the effect of dictating whether we produce uppercase or lowercase
                        // hex numbers for a-f. 'X' as the fmt code produces uppercase. 'x' as the format code
                        // produces lowercase.
                        return Int32ToHexStr(value, (char)(fmt - ('X' - 'A' + 10)), digits);

                    default:
                        {
                            NumberBuffer number = new NumberBuffer();
                            Int32ToNumber(value, ref number);
                            if (fmt != 0)
                                return NumberToString(number, fmt, digits, info, false);
                            return NumberToStringFormat(number, format, info);
                        }
                }
            }

            public static String FormatUInt32(uint value, String format, IFormatProvider provider)
            {
                NumberFormatInfo info = provider == null ? NumberFormatInfo.CurrentInfo : NumberFormatInfo.GetInstance(provider);

                int digits;
                char fmt = ParseFormatSpecifier(format, out digits);

                // ANDing fmt with FFDF has the effect of uppercasing the character because we've removed the bit
                // that marks lower-case.
                switch (fmt)
                {
                    case 'G':
                    case 'g':
                        if (digits > 0)
                        {
                            NumberBuffer number = new NumberBuffer();
                            UInt32ToNumber(value, ref number);
                            if (fmt != 0)
                                return NumberToString(number, fmt, digits, info, false);
                            return NumberToStringFormat(number, format, info);
                        }
                        // fall through
                        goto case 'D';

                    case 'D':
                    case 'd':
                        return UInt32ToDecStr(value, digits);

                    case 'X':
                    case 'x':
                        // The fmt-(X-A+10) hack has the effect of dictating whether we produce uppercase or lowercase
                        // hex numbers for a-f. 'X' as the fmt code produces uppercase. 'x' as the format code
                        // produces lowercase.
                        return Int32ToHexStr((int)value, (char)(fmt - ('X' - 'A' + 10)), digits);

                    default:
                        {
                            NumberBuffer number = new NumberBuffer();
                            UInt32ToNumber(value, ref number);
                            if (fmt != 0)
                                return NumberToString(number, fmt, digits, info, false);
                            return NumberToStringFormat(number, format, info);
                        }
                }
            }

            public static String FormatInt64(long value, String format, IFormatProvider provider)
            {
                NumberFormatInfo info = provider == null ? NumberFormatInfo.CurrentInfo : NumberFormatInfo.GetInstance(provider);

                int digits;
                char fmt = ParseFormatSpecifier(format, out digits);

                // ANDing fmt with FFDF has the effect of uppercasing the character because we've removed the bit
                // that marks lower-case.
                switch (fmt)
                {
                    case 'G':
                    case 'g':
                        if (digits > 0)
                        {
                            NumberBuffer number = new NumberBuffer();
                            Int64ToNumber(value, ref number);
                            if (fmt != 0)
                                return NumberToString(number, fmt, digits, info, false);
                            return NumberToStringFormat(number, format, info);
                        }
                        // fall through
                        goto case 'D';

                    case 'D':
                    case 'd':
                        return Int64ToDecStr(value, digits, info.NegativeSign);

                    case 'X':
                    case 'x':
                        // The fmt-(X-A+10) hack has the effect of dictating whether we produce uppercase or lowercase
                        // hex numbers for a-f. 'X' as the fmt code produces uppercase. 'x' as the format code
                        // produces lowercase.
                        return Int64ToHexStr(value, (char)(fmt - ('X' - 'A' + 10)), digits);

                    default:
                        {
                            NumberBuffer number = new NumberBuffer();
                            Int64ToNumber(value, ref number);
                            if (fmt != 0)
                                return NumberToString(number, fmt, digits, info, false);
                            return NumberToStringFormat(number, format, info);
                        }
                }
            }

            public static String FormatUInt64(ulong value, String format, IFormatProvider provider)
            {
                NumberFormatInfo info = provider == null ? NumberFormatInfo.CurrentInfo : NumberFormatInfo.GetInstance(provider);

                int digits;
                char fmt = ParseFormatSpecifier(format, out digits);

                // ANDing fmt with FFDF has the effect of uppercasing the character because we've removed the bit
                // that marks lower-case.
                switch (fmt)
                {
                    case 'G':
                    case 'g':
                        if (digits > 0)
                        {
                            NumberBuffer number = new NumberBuffer();
                            UInt64ToNumber(value, ref number);
                            if (fmt != 0)
                                return NumberToString(number, fmt, digits, info, false);
                            return NumberToStringFormat(number, format, info);
                        }
                        // fall through
                        goto case 'D';

                    case 'D':
                    case 'd':
                        return UInt64ToDecStr(value, digits);

                    case 'X':
                    case 'x':
                        // The fmt-(X-A+10) hack has the effect of dictating whether we produce uppercase or lowercase
                        // hex numbers for a-f. 'X' as the fmt code produces uppercase. 'x' as the format code
                        // produces lowercase.
                        return Int64ToHexStr((long)value, (char)(fmt - ('X' - 'A' + 10)), digits);

                    default:
                        {
                            NumberBuffer number = new NumberBuffer();
                            UInt64ToNumber(value, ref number);
                            if (fmt != 0)
                                return NumberToString(number, fmt, digits, info, false);
                            return NumberToStringFormat(number, format, info);
                        }
                }
            }

            internal static Boolean NumberBufferToDouble(NumberBuffer number, ref Double value)
            {
                double d = NumberToDouble(number);

                uint e = DoubleHelper.Exponent(d);
                ulong m = DoubleHelper.Mantissa(d);
                if (e == 0x7FF)
                    return false;
                if (e == 0 && m == 0)
                    d = 0;

                value = d;

                return true;
            }

            internal unsafe static void Int32ToDecChars(char[] buffer, ref int index, uint value, int digits)
            {
                while (--digits >= 0 || value != 0)
                {
                    buffer[--index] = (char)(value % 10 + '0');
                    value /= 10;
                }
            }

            static public bool IsPositiveInfinity(string s, IFormatProvider provider)
            {
                NumberFormatInfo nfi = provider == null ? NumberFormatInfo.CurrentInfo : NumberFormatInfo.GetInstance(provider);
                return s.Equals(nfi.PositiveInfinitySymbol);
            }

            static public bool IsNegativeInfinity(string s, IFormatProvider provider)
            {
                NumberFormatInfo nfi = provider == null ? NumberFormatInfo.CurrentInfo : NumberFormatInfo.GetInstance(provider);
                return s.Equals(nfi.NegativeInfinitySymbol);
            }

            static public bool IsNaNSymbol(string s, IFormatProvider provider)
            {
                NumberFormatInfo nfi = provider == null ? NumberFormatInfo.CurrentInfo : NumberFormatInfo.GetInstance(provider);
                return s.Equals(nfi.NaNSymbol);
            }

            #region Decimal Number Formatting Helpers
            private static unsafe bool NumberBufferToDecimal(Number.NumberBuffer number, ref Decimal value)
            {
                Decimal d = new Decimal();

                char* p = number.digits;
                int e = number.scale;
                if (*p == 0)
                {
                    // To avoid risking an app-compat issue with pre 4.5 (where some app was illegally using Reflection to examine the internal scale bits), we'll only force
                    // the scale to 0 if the scale was previously positive (previously, such cases were unparsable to a bug.)
                    if (e > 0)
                    {
                        e = 0;
                    }
                }
                else
                {
                    if (e > DECIMAL_PRECISION)
                        return false;

                    while (((e > 0) || ((*p != 0) && (e > -28))) &&
                           ((d.High < 0x19999999) || ((d.High == 0x19999999) &&
                                                      ((d.Mid < 0x99999999) || ((d.Mid == 0x99999999) &&
                                                                                ((d.Low < 0x99999999) || ((d.Low == 0x99999999) &&
                                                                                                          (*p <= '5'))))))))
                    {
                        Decimal.DecMul10(ref d);
                        if (*p != 0)
                            Decimal.DecAddInt32(ref d, (uint)(*p++ - '0'));
                        e--;
                    }

                    if (*p++ >= '5')
                    {
                        bool round = true;
                        if ((*(p - 1) == '5') && ((*(p - 2) % 2) == 0))
                        {
                            // Check if previous digit is even, only if the when we are unsure whether hows to do
                            // Banker's rounding. For digits > 5 we will be roundinp up anyway.
                            int count = 20; // Look at the next 20 digits to check to round
                            while ((*p == '0') && (count != 0))
                            {
                                p++;
                                count--;
                            }
                            if ((*p == '\0') || (count == 0))
                                round = false;// Do nothing
                        }

                        if (round)
                        {
                            Decimal.DecAddInt32(ref d, 1);
                            if ((d.High | d.Mid | d.Low) == 0)
                            {
                                d.High = 0x19999999;
                                d.Mid = 0x99999999;
                                d.Low = 0x9999999A;
                                e++;
                            }
                        }
                    }
                }

                if (e > 0)
                    return false;

                if (e <= -DECIMAL_PRECISION)
                {
                    // Parsing a large scale zero can give you more precision than fits in the decimal.
                    // This should only happen for actual zeros or very small numbers that round to zero.
                    d.High = 0;
                    d.Low = 0;
                    d.Mid = 0;
                    d.Scale = DECIMAL_PRECISION - 1;
                }
                else
                {
                    d.Scale = -e;
                }
                d.Sign = number.sign;

                value = d;
                return true;
            }

            private static unsafe void DecimalToNumber(Decimal value, ref Number.NumberBuffer number)
            {
                Decimal d = value;

                char* buffer = number.digits;
                number.precision = DECIMAL_PRECISION;
                number.sign = d.Sign;

                int index = DECIMAL_PRECISION;
                while (d.Mid != 0 | d.High != 0)
                    Number.Int32ToDecChars(buffer, ref index, Decimal.DecDivMod1E9(ref d), 9);

                Number.Int32ToDecChars(buffer, ref index, d.Low, 0);

                int i = DECIMAL_PRECISION - index;
                number.scale = i - d.Scale;

                char* dst = number.digits;
                while (--i >= 0)
                    *dst++ = buffer[index++];
                *dst = '\0';
            }

            #endregion

            /*===========================================================
                Portable NumberToDouble implementation
                --------------------------------------

                - does the conversion with the best possible precision.
                - does not use any float arithmetic so it is not sensitive
                to differences in precision of floating point calculations
                across platforms.

                The internal integer representation of the float number is
                UINT64 mantissa + INT exponent. The mantissa is kept normalized
                ie with the most significant one being 63-th bit of UINT64.
            ===========================================================*/

            //
            // get 32-bit integer from at most 9 digits
            //
            private static unsafe uint DigitsToInt(char* p, int count)
            {
                char* end = p + count;
                uint res = (uint)*p - '0';
                for (p = p + 1; p < end; p++)
                    res = 10 * res + (uint)*p - '0';
                return res;
            }

            //
            // helper to multiply two 32-bit uints
            //
            private static ulong Mul32x32To64(uint a, uint b)
            {
                return (ulong)a * (ulong)b;
            }

            //
            // multiply two numbers in the internal integer representation
            //
            private static ulong Mul64Lossy(ulong a, ulong b, ref int pexp)
            {
                // it's ok to lose some precision here - Mul64 will be called
                // at most twice during the conversion, so the error won't propagate
                // to any of the 53 significant bits of the result
                ulong val = Mul32x32To64((uint)(a >> 32), (uint)(b >> 32)) +
                    (Mul32x32To64((uint)(a >> 32), (uint)(b)) >> 32) +
                    (Mul32x32To64((uint)(a), (uint)(b >> 32)) >> 32);

                // normalize
                if ((val & 0x8000000000000000) == 0)
                {
                    val <<= 1;
                    pexp -= 1;
                }

                return val;
            }

            //
            // precomputed tables with powers of 10. These allows us to do at most
            // two Mul64 during the conversion. This is important not only
            // for speed, but also for precision because of Mul64 computes with 1 bit error.
            //

            private static readonly ulong[] s_rgval64Power10 =
            {
                // powers of 10
                /*1*/ 0xa000000000000000,
                /*2*/ 0xc800000000000000,
                /*3*/ 0xfa00000000000000,
                /*4*/ 0x9c40000000000000,
                /*5*/ 0xc350000000000000,
                /*6*/ 0xf424000000000000,
                /*7*/ 0x9896800000000000,
                /*8*/ 0xbebc200000000000,
                /*9*/ 0xee6b280000000000,
                /*10*/ 0x9502f90000000000,
                /*11*/ 0xba43b74000000000,
                /*12*/ 0xe8d4a51000000000,
                /*13*/ 0x9184e72a00000000,
                /*14*/ 0xb5e620f480000000,
                /*15*/ 0xe35fa931a0000000,

                // powers of 0.1
                /*1*/ 0xcccccccccccccccd,
                /*2*/ 0xa3d70a3d70a3d70b,
                /*3*/ 0x83126e978d4fdf3c,
                /*4*/ 0xd1b71758e219652e,
                /*5*/ 0xa7c5ac471b478425,
                /*6*/ 0x8637bd05af6c69b7,
                /*7*/ 0xd6bf94d5e57a42be,
                /*8*/ 0xabcc77118461ceff,
                /*9*/ 0x89705f4136b4a599,
                /*10*/ 0xdbe6fecebdedd5c2,
                /*11*/ 0xafebff0bcb24ab02,
                /*12*/ 0x8cbccc096f5088cf,
                /*13*/ 0xe12e13424bb40e18,
                /*14*/ 0xb424dc35095cd813,
                /*15*/ 0x901d7cf73ab0acdc,
            };

            private static readonly sbyte[] s_rgexp64Power10 =
            {
                // exponents for both powers of 10 and 0.1
                /*1*/ 4,
                /*2*/ 7,
                /*3*/ 10,
                /*4*/ 14,
                /*5*/ 17,
                /*6*/ 20,
                /*7*/ 24,
                /*8*/ 27,
                /*9*/ 30,
                /*10*/ 34,
                /*11*/ 37,
                /*12*/ 40,
                /*13*/ 44,
                /*14*/ 47,
                /*15*/ 50,
            };

            private static readonly ulong[] s_rgval64Power10By16 =
            {
                // powers of 10^16
                /*1*/ 0x8e1bc9bf04000000,
                /*2*/ 0x9dc5ada82b70b59e,
                /*3*/ 0xaf298d050e4395d6,
                /*4*/ 0xc2781f49ffcfa6d4,
                /*5*/ 0xd7e77a8f87daf7fa,
                /*6*/ 0xefb3ab16c59b14a0,
                /*7*/ 0x850fadc09923329c,
                /*8*/ 0x93ba47c980e98cde,
                /*9*/ 0xa402b9c5a8d3a6e6,
                /*10*/ 0xb616a12b7fe617a8,
                /*11*/ 0xca28a291859bbf90,
                /*12*/ 0xe070f78d39275566,
                /*13*/ 0xf92e0c3537826140,
                /*14*/ 0x8a5296ffe33cc92c,
                /*15*/ 0x9991a6f3d6bf1762,
                /*16*/ 0xaa7eebfb9df9de8a,
                /*17*/ 0xbd49d14aa79dbc7e,
                /*18*/ 0xd226fc195c6a2f88,
                /*19*/ 0xe950df20247c83f8,
                /*20*/ 0x81842f29f2cce373,
                /*21*/ 0x8fcac257558ee4e2,

                // powers of 0.1^16
                /*1*/ 0xe69594bec44de160,
                /*2*/ 0xcfb11ead453994c3,
                /*3*/ 0xbb127c53b17ec165,
                /*4*/ 0xa87fea27a539e9b3,
                /*5*/ 0x97c560ba6b0919b5,
                /*6*/ 0x88b402f7fd7553ab,
                /*7*/ 0xf64335bcf065d3a0,
                /*8*/ 0xddd0467c64bce4c4,
                /*9*/ 0xc7caba6e7c5382ed,
                /*10*/ 0xb3f4e093db73a0b7,
                /*11*/ 0xa21727db38cb0053,
                /*12*/ 0x91ff83775423cc29,
                /*13*/ 0x8380dea93da4bc82,
                /*14*/ 0xece53cec4a314f00,
                /*15*/ 0xd5605fcdcf32e217,
                /*16*/ 0xc0314325637a1978,
                /*17*/ 0xad1c8eab5ee43ba2,
                /*18*/ 0x9becce62836ac5b0,
                /*19*/ 0x8c71dcd9ba0b495c,
                /*20*/ 0xfd00b89747823938,
                /*21*/ 0xe3e27a444d8d991a,
            };

            private static readonly short[] s_rgexp64Power10By16 =
            {
                // exponents for both powers of 10^16 and 0.1^16
                /*1*/ 54,
                /*2*/ 107,
                /*3*/ 160,
                /*4*/ 213,
                /*5*/ 266,
                /*6*/ 319,
                /*7*/ 373,
                /*8*/ 426,
                /*9*/ 479,
                /*10*/ 532,
                /*11*/ 585,
                /*12*/ 638,
                /*13*/ 691,
                /*14*/ 745,
                /*15*/ 798,
                /*16*/ 851,
                /*17*/ 904,
                /*18*/ 957,
                /*19*/ 1010,
                /*20*/ 1064,
                /*21*/ 1117,
            };

            private static int abs(int value)
            {
                if (value < 0)
                    return -value;
                return value;
            }

            private static unsafe double NumberToDouble(NumberBuffer number)
            {
                ulong val;
                int exp;
                char* src = number.digits;
                int remaining;
                int total;
                int count;
                int scale;
                int absscale;
                int index;

                total = wcslen(src);
                remaining = total;

                // skip the leading zeros
                while (*src == '0')
                {
                    remaining--;
                    src++;
                }

                if (remaining == 0)
                    return 0;

                count = Math.Min(remaining, 9);
                remaining -= count;
                val = DigitsToInt(src, count);

                if (remaining > 0)
                {
                    count = Math.Min(remaining, 9);
                    remaining -= count;

                    // get the denormalized power of 10
                    uint mult = (uint)(s_rgval64Power10[count - 1] >> (64 - s_rgexp64Power10[count - 1]));
                    val = Mul32x32To64((uint)val, mult) + DigitsToInt(src + 9, count);
                }

                scale = number.scale - (total - remaining);
                absscale = abs(scale);
                if (absscale >= 22 * 16)
                {
                    // overflow / underflow
                    ulong result = (scale > 0) ? 0x7FF0000000000000 : 0ul;
                    if (number.sign)
                        result |= 0x8000000000000000;
                    return *(double*)&result;
                }

                exp = 64;

                // normalize the mantissa
                if ((val & 0xFFFFFFFF00000000) == 0) { val <<= 32; exp -= 32; }
                if ((val & 0xFFFF000000000000) == 0) { val <<= 16; exp -= 16; }
                if ((val & 0xFF00000000000000) == 0) { val <<= 8; exp -= 8; }
                if ((val & 0xF000000000000000) == 0) { val <<= 4; exp -= 4; }
                if ((val & 0xC000000000000000) == 0) { val <<= 2; exp -= 2; }
                if ((val & 0x8000000000000000) == 0) { val <<= 1; exp -= 1; }

                index = absscale & 15;
                if (index != 0)
                {
                    int multexp = s_rgexp64Power10[index - 1];
                    // the exponents are shared between the inverted and regular table
                    exp += (scale < 0) ? (-multexp + 1) : multexp;

                    ulong multval = s_rgval64Power10[index + ((scale < 0) ? 15 : 0) - 1];
                    val = Mul64Lossy(val, multval, ref exp);
                }

                index = absscale >> 4;
                if (index != 0)
                {
                    int multexp = s_rgexp64Power10By16[index - 1];
                    // the exponents are shared between the inverted and regular table
                    exp += (scale < 0) ? (-multexp + 1) : multexp;

                    ulong multval = s_rgval64Power10By16[index + ((scale < 0) ? 21 : 0) - 1];
                    val = Mul64Lossy(val, multval, ref exp);
                }


                // round & scale down
                if (((int)val & (1 << 10)) != 0)
                {
                    // IEEE round to even
                    ulong tmp = val + ((1 << 10) - 1) + (ulong)(((int)val >> 11) & 1);
                    if (tmp < val)
                    {
                        // overflow
                        tmp = (tmp >> 1) | 0x8000000000000000;
                        exp += 1;
                    }
                    val = tmp;
                }

                // return the exponent to a biased state
                exp += 0x3FE;

                // handle overflow, underflow, "Epsilon - 1/2 Epsilon", denormalized, and the normal case
                if (exp <= 0)
                {
                    if (exp == -52 && (val >= 0x8000000000000058))
                    {
                        // round X where {Epsilon > X >= 2.470328229206232730000000E-324} up to Epsilon (instead of down to zero)
                        val = 0x0000000000000001;
                    }
                    else if (exp <= -52)
                    {
                        // underflow
                        val = 0;
                    }
                    else
                    {
                        // denormalized
                        val >>= (-exp + 11 + 1);
                    }
                }
                else if (exp >= 0x7FF)
                {
                    // overflow
                    val = 0x7FF0000000000000;
                }
                else
                {
                    // normal postive exponent case
                    val = ((ulong)exp << 52) + ((val >> 11) & 0x000FFFFFFFFFFFFF);
                }

                if (number.sign)
                    val |= 0x8000000000000000;

                return *(double*)&val;
            }

            private static unsafe void Int32ToNumber(int value, ref NumberBuffer number)
            {
                number.precision = INT32_PRECISION;

                if (value >= 0)
                {
                    number.sign = false;
                }
                else
                {
                    number.sign = true;
                    value = -value;
                }

                char* buffer = number.digits;
                int index = INT32_PRECISION;
                Int32ToDecChars(buffer, ref index, (uint)value, 0);
                int i = INT32_PRECISION - index;

                number.scale = i;

                char* dst = number.digits;
                while (--i >= 0)
                    *dst++ = buffer[index++];
                *dst = '\0';
            }

            private static unsafe string Int32ToDecStr(int value, int digits, string sNegative)
            {
                if (digits < 1)
                    digits = 1;

                int maxDigitsLength = (digits > 15) ? digits : 15; // Since an int32 can have maximum of 10 chars as a String
                int bufferLength = (maxDigitsLength > 100) ? maxDigitsLength : 100;
                int negLength = 0;
                string src = null;

                if (value < 0)
                {
                    src = sNegative;
                    negLength = sNegative.Length;
                    if (negLength > bufferLength - maxDigitsLength)
                        bufferLength = negLength + maxDigitsLength;
                }

                char* buffer = stackalloc char[bufferLength];

                int index = bufferLength;
                Int32ToDecChars(buffer, ref index, (uint)(value >= 0 ? value : -value), digits);

                if (value < 0)
                {
                    for (int i = negLength - 1; i >= 0; i--)
                        buffer[--index] = src[i];
                }

                return new string(buffer, index, bufferLength - index);
            }

            private static string Int32ToHexStr(int value, char hexBase, int digits)
            {
                if (digits < 1)
                    digits = 1;
                char[] buffer = new char[100];
                int index = 100;
                Int32ToHexChars(buffer, ref index, (uint)value, hexBase, digits);
                return new string(buffer, index, 100 - index);
            }

            private static void Int32ToHexChars(char[] buffer, ref int index, uint value, int hexBase, int digits)
            {
                while (--digits >= 0 || value != 0)
                {
                    byte digit = (byte)(value & 0xF);
                    buffer[--index] = (char)(digit + (digit < 10 ? (byte)'0' : hexBase));
                    value >>= 4;
                }
            }

            private static unsafe void UInt32ToNumber(uint value, ref NumberBuffer number)
            {
                number.precision = UINT32_PRECISION;
                number.sign = false;

                char* buffer = number.digits;
                int index = UINT32_PRECISION;
                Int32ToDecChars(buffer, ref index, value, 0);
                int i = UINT32_PRECISION - index;

                number.scale = i;

                char* dst = number.digits;
                while (--i >= 0)
                    *dst++ = buffer[index++];
                *dst = '\0';
            }

            private static unsafe string UInt32ToDecStr(uint value, int digits)
            {
                if (digits < 1)
                    digits = 1;

                char* buffer = stackalloc char[100];
                int index = 100;
                Int32ToDecChars(buffer, ref index, value, digits);

                return new string(buffer, index, 100 - index);
            }

            private static unsafe void Int64ToNumber(long input, ref NumberBuffer number)
            {
                ulong value = (ulong)input;
                number.sign = input < 0;
                number.precision = INT64_PRECISION;
                if (number.sign)
                {
                    value = (ulong)(-input);
                }

                char* buffer = number.digits;
                int index = INT64_PRECISION;
                while (High32(value) != 0)
                    Int32ToDecChars(buffer, ref index, Int64DivMod1E9(ref value), 9);
                Int32ToDecChars(buffer, ref index, Low32(value), 0);
                int i = INT64_PRECISION - index;

                number.scale = i;

                char* dst = number.digits;
                while (--i >= 0)
                    *dst++ = buffer[index++];
                *dst = '\0';
            }

            private static uint Low32(ulong value)
            {
                return (uint)value;
            }

            private static uint High32(ulong value)
            {
                return (uint)(((ulong)value & 0xFFFFFFFF00000000) >> 32);
            }

            private static uint Int64DivMod1E9(ref ulong value)
            {
                uint rem = (uint)(value % 1000000000);
                value /= 1000000000;
                return rem;
            }

            private static unsafe string Int64ToDecStr(long input, int digits, string sNegative)
            {
                if (digits < 1)
                    digits = 1;

                ulong value = (ulong)input;
                int sign = (int)High32(value);

                // digits as specified in the format string can be at most 99.
                int maxDigitsLength = (digits > 20) ? digits : 20;
                int bufferLength = (maxDigitsLength > 100) ? maxDigitsLength : 100;

                if (sign < 0)
                {
                    value = (ulong)(-input);
                    int negLength = sNegative.Length;
                    if (negLength > bufferLength - maxDigitsLength)
                        bufferLength = negLength + maxDigitsLength;
                }

                char* buffer = stackalloc char[bufferLength];
                int index = bufferLength;
                while (High32(value) != 0)
                {
                    Int32ToDecChars(buffer, ref index, Int64DivMod1E9(ref value), 9);
                    digits -= 9;
                }
                Int32ToDecChars(buffer, ref index, Low32(value), digits);

                if (sign < 0)
                {
                    for (int i = sNegative.Length - 1; i >= 0; i--)
                        buffer[--index] = sNegative[i];
                }

                return new string(buffer, index, bufferLength - index);
            }

            private static string Int64ToHexStr(long value, char hexBase, int digits)
            {
                char[] buffer = new char[100];
                int index = 100;

                if (High32((ulong)value) != 0)
                {
                    Int32ToHexChars(buffer, ref index, Low32((ulong)value), hexBase, 8);
                    Int32ToHexChars(buffer, ref index, High32((ulong)value), hexBase, digits - 8);
                }
                else
                {
                    if (digits < 1)
                        digits = 1;
                    Int32ToHexChars(buffer, ref index, Low32((ulong)value), hexBase, digits);
                }

                return new string(buffer, index, 100 - index);
            }

            private static unsafe void UInt64ToNumber(ulong value, ref NumberBuffer number)
            {
                number.precision = UINT64_PRECISION;
                number.sign = false;

                char* buffer = number.digits;
                int index = UINT64_PRECISION;

                while (High32(value) != 0)
                    Int32ToDecChars(buffer, ref index, Int64DivMod1E9(ref value), 9);
                Int32ToDecChars(buffer, ref index, Low32(value), 0);
                int i = UINT64_PRECISION - index;

                number.scale = i;

                char* dst = number.digits;
                while (--i >= 0)
                    *dst++ = buffer[index++];
                *dst = '\0';
            }

            private static unsafe string UInt64ToDecStr(ulong value, int digits)
            {
                if (digits < 1)
                    digits = 1;

                char* buffer = stackalloc char[100];
                int index = 100;
                while (High32(value) != 0)
                {
                    Int32ToDecChars(buffer, ref index, Int64DivMod1E9(ref value), 9);
                    digits -= 9;
                }
                Int32ToDecChars(buffer, ref index, Low32(value), digits);

                return new string(buffer, index, 100 - index);
            }

            private static class DoubleHelper
            {
                public static unsafe uint Exponent(double d)
                {
                    return (*((uint*)&d + 1) >> 20) & 0x000007ff;
                }

                public static unsafe ulong Mantissa(double d)
                {
                    return (ulong)*((uint*)&d) | ((ulong)(*((uint*)&d + 1) & 0x000fffff) << 32);
                }

                public static unsafe bool Sign(double d)
                {
                    return (*((uint*)&d + 1) >> 31) != 0;
                }
            }
        }
    }
}


