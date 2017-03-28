// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace System.Globalization
{
    // Internal Contract for all Globalization APIs that are needed by lower levels of System.Private.CoreLib.
    // This is class acts as a gateway between everything in System.Private.CoreLib and System.Globalization. 
    internal partial class FormatProvider
    {
        public static IFormatProvider InvariantCulture { get { return CultureInfo.InvariantCulture; } }

        #region Char/String Conversions
        public static string ToLower(string s)
        {
            return CultureInfo.CurrentCulture.TextInfo.ToLower(s);
        }
        public static char ToLower(char c)
        {
            return CultureInfo.CurrentCulture.TextInfo.ToLower(c);
        }
        public static string ToLowerInvariant(string s)
        {
            return CultureInfo.InvariantCulture.TextInfo.ToLower(s);
        }
        public static char ToLowerInvariant(char c)
        {
            return CultureInfo.InvariantCulture.TextInfo.ToLower(c);
        }
        public static string ToUpper(string s)
        {
            return CultureInfo.CurrentCulture.TextInfo.ToUpper(s);
        }
        public static char ToUpper(char c)
        {
            return CultureInfo.CurrentCulture.TextInfo.ToUpper(c);
        }
        public static string ToUpperInvariant(string s)
        {
            return CultureInfo.InvariantCulture.TextInfo.ToUpper(s);
        }
        public static char ToUpperInvariant(char c)
        {
            return CultureInfo.InvariantCulture.TextInfo.ToUpper(c);
        }
        public static UnicodeCategory GetUnicodeCategory(char c)
        {
            return CharUnicodeInfo.GetUnicodeCategory(c);
        }
        public static UnicodeCategory GetUnicodeCategory(string s, int index)
        {
            return CharUnicodeInfo.GetUnicodeCategory(s, index);
        }
        public static double GetNumericValue(char c)
        {
            return CharUnicodeInfo.GetNumericValue(c);
        }
        public static double GetNumericValue(string s, int index)
        {
            return CharUnicodeInfo.GetNumericValue(s, index);
        }
        #endregion

        #region Culture Comparisons
        public static int GetHashCodeInvariantIgnoreCase(string source)
        {
            return CultureInfo.InvariantCulture.CompareInfo.GetHashCodeOfString(source, CompareOptions.IgnoreCase);
        }
        public static int GetHashCodeOrdinalIgnoreCase(string source)
        {
            return TextInfo.GetHashCodeOrdinalIgnoreCase(source);
        }
        public static int Compare(String string1, int offset1, int length1, String string2, int offset2, int length2)
        {
            return CultureInfo.CurrentCulture.CompareInfo.Compare(string1, offset1, length1, string2, offset2, length2, CompareOptions.None);
        }
        public static int CompareIgnoreCase(String string1, int offset1, int length1, String string2, int offset2, int length2)
        {
            return CultureInfo.CurrentCulture.CompareInfo.Compare(string1, offset1, length1, string2, offset2, length2, CompareOptions.IgnoreCase);
        }
        public static int CompareOrdinalIgnoreCase(String string1, int offset1, int length1, String string2, int offset2, int length2)
        {
            return CompareInfo.CompareOrdinalIgnoreCase(string1, offset1, length1, string2, offset2, length2);
        }
        public static StringComparer GetCultureAwareStringComparer(bool ignoreCase)
        {
            return new CultureAwareComparer(CultureInfo.CurrentCulture, ignoreCase);
        }
        public static int IndexOf(String source, String value, int startIndex, int count)
        {
            return CultureInfo.CurrentCulture.CompareInfo.IndexOf(source, value, startIndex, count, CompareOptions.None);
        }
        public static int IndexOfIgnoreCase(String source, String value, int startIndex, int count)
        {
            return CultureInfo.CurrentCulture.CompareInfo.IndexOf(source, value, startIndex, count, CompareOptions.IgnoreCase);
        }
        public static bool IsPrefix(String source, String prefix)
        {
            return CultureInfo.CurrentCulture.CompareInfo.IsPrefix(source, prefix, CompareOptions.None);
        }
        public static bool IsPrefixIgnoreCase(String source, String prefix)
        {
            return CultureInfo.CurrentCulture.CompareInfo.IsPrefix(source, prefix, CompareOptions.IgnoreCase);
        }
        public static bool IsSuffix(String source, String suffix)
        {
            return CultureInfo.CurrentCulture.CompareInfo.IsSuffix(source, suffix, CompareOptions.None);
        }
        public static bool IsSuffixIgnoreCase(String source, String suffix)
        {
            return CultureInfo.CurrentCulture.CompareInfo.IsSuffix(source, suffix, CompareOptions.IgnoreCase);
        }
        public static int LastIndexOf(String source, String value, int startIndex, int count)
        {
            return CultureInfo.CurrentCulture.CompareInfo.LastIndexOf(source, value, startIndex, count, CompareOptions.None);
        }
        public static int LastIndexOfIgnoreCase(String source, String value, int startIndex, int count)
        {
            return CultureInfo.CurrentCulture.CompareInfo.LastIndexOf(source, value, startIndex, count, CompareOptions.IgnoreCase);
        }
        public static int OrdinalIndexOf(String source, String value, int startIndex, int count)
        {
            return CultureInfo.InvariantCulture.CompareInfo.IndexOf(source, value, startIndex, count, CompareOptions.Ordinal);
        }
        public static int OrdinalIndexOfIgnoreCase(String source, String value, int startIndex, int count)
        {
            return TextInfo.IndexOfStringOrdinalIgnoreCase(source, value, startIndex, count);
        }
        public static int OrdinalLastIndexOf(String source, String value, int startIndex, int count)
        {
            return CultureInfo.InvariantCulture.CompareInfo.LastIndexOf(source, value, startIndex, count, CompareOptions.Ordinal);
        }
        public static int OrdinalLastIndexOfIgnoreCase(String source, String value, int startIndex, int count)
        {
            return TextInfo.LastIndexOfStringOrdinalIgnoreCase(source, value, startIndex, count);
        }
        #endregion

        #region Formatting
        // provider if null means we use NumberFormatInfo.CurrenctInfo otherwise we use NumberFormatInfo.GetInstance(provider)
        public static String FormatDateTime(DateTime value, String format, IFormatProvider provider)
        {
            return DateTimeFormat.Format(value, format, DateTimeFormatInfo.GetInstance(provider));
        }
        public static String FormatDateTime(DateTime value, String format, IFormatProvider provider, TimeSpan offSet)
        {
            return DateTimeFormat.Format(value, format, DateTimeFormatInfo.GetInstance(provider), offSet);
        }
        public static String FormatDecimal(Decimal value, String format, IFormatProvider provider)
        {
            return FormatProvider.Number.FormatDecimal(value, format, provider);
        }
        public static String FormatDouble(double value, String format, IFormatProvider provider)
        {
            return FormatProvider.Number.FormatDouble(value, format, provider);
        }
        public static String FormatInt32(int value, String format, IFormatProvider provider)
        {
            return FormatProvider.Number.FormatInt32(value, format, provider);
        }
        public static String FormatInt64(long value, String format, IFormatProvider provider)
        {
            return FormatProvider.Number.FormatInt64(value, format, provider);
        }
        public static String FormatSingle(float value, String format, IFormatProvider provider)
        {
            return FormatProvider.Number.FormatSingle(value, format, provider);
        }
        public static String FormatTimeSpan(TimeSpan value, String format, IFormatProvider provider)
        {
            return FormatProvider.TimeSpanFormat.Format(value, format, provider);
        }
        public static String FormatUInt32(uint value, String format, IFormatProvider provider)
        {
            return FormatProvider.Number.FormatUInt32(value, format, provider);
        }
        public static String FormatUInt64(ulong value, String format, IFormatProvider provider)
        {
            return FormatProvider.Number.FormatUInt64(value, format, provider);
        }
        public static string[] GetAllDateTimes(DateTime value, char format, IFormatProvider provider)
        {
            if (format == (char)0)
                return DateTimeFormat.GetAllDateTimes(value, DateTimeFormatInfo.GetInstance(provider));
            else
                return DateTimeFormat.GetAllDateTimes(value, format, DateTimeFormatInfo.GetInstance(provider));
        }
        #endregion

        #region Parsing
        public static DateTime ParseDateTime(String value, IFormatProvider provider, DateTimeStyles styles)
        {
            return DateTimeParse.Parse(value, DateTimeFormatInfo.GetInstance(provider), styles);
        }
        public static DateTime ParseDateTime(String value, IFormatProvider provider, DateTimeStyles styles, out TimeSpan offset)
        {
            return DateTimeParse.Parse(value, DateTimeFormatInfo.GetInstance(provider), styles, out offset);
        }
        public static DateTime ParseDateTimeExact(String value, String format, IFormatProvider provider, DateTimeStyles styles)
        {
            return DateTimeParse.ParseExact(value, format, DateTimeFormatInfo.GetInstance(provider), styles);
        }
        public static DateTime ParseDateTimeExact(String value, String format, IFormatProvider provider, DateTimeStyles styles, out TimeSpan offset)
        {
            return DateTimeParse.ParseExact(value, format, DateTimeFormatInfo.GetInstance(provider), styles, out offset);
        }
        public static DateTime ParseDateTimeExactMultiple(String value, String[] formats, IFormatProvider provider, DateTimeStyles styles)
        {
            return DateTimeParse.ParseExactMultiple(value, formats, DateTimeFormatInfo.GetInstance(provider), styles);
        }
        public static DateTime ParseDateTimeExactMultiple(String value, String[] formats, IFormatProvider provider, DateTimeStyles styles, out TimeSpan offset)
        {
            return DateTimeParse.ParseExactMultiple(value, formats, DateTimeFormatInfo.GetInstance(provider), styles, out offset);
        }
        public static Decimal ParseDecimal(String value, NumberStyles options, IFormatProvider provider)
        {
            return FormatProvider.Number.ParseDecimal(value, options, provider);
        }
        public static Double ParseDouble(String value, NumberStyles options, IFormatProvider provider)
        {
            return FormatProvider.Number.ParseDouble(value, options, provider);
        }
        public static int ParseInt32(String s, NumberStyles styles, IFormatProvider provider)
        {
            return FormatProvider.Number.ParseInt32(s, styles, provider);
        }
        public static Int64 ParseInt64(String value, NumberStyles options, IFormatProvider provider)
        {
            return FormatProvider.Number.ParseInt64(value, options, provider);
        }
        public static Single ParseSingle(String value, NumberStyles options, IFormatProvider provider)
        {
            return FormatProvider.Number.ParseSingle(value, options, provider);
        }
        public static TimeSpan ParseTimeSpan(String value, IFormatProvider provider)
        {
            return FormatProvider.TimeSpanParse.Parse(value, provider);
        }
        public static TimeSpan ParseTimeSpanExact(String value, String format, IFormatProvider provider, TimeSpanStyles styles)
        {
            return FormatProvider.TimeSpanParse.ParseExact(value, format, provider, styles);
        }
        public static TimeSpan ParseTimeSpanExactMultiple(String value, String[] formats, IFormatProvider provider, TimeSpanStyles styles)
        {
            return FormatProvider.TimeSpanParse.ParseExactMultiple(value, formats, provider, styles);
        }
        public static UInt32 ParseUInt32(String value, NumberStyles options, IFormatProvider provider)
        {
            return FormatProvider.Number.ParseUInt32(value, options, provider);
        }
        public static UInt64 ParseUInt64(String value, NumberStyles options, IFormatProvider provider)
        {
            return FormatProvider.Number.ParseUInt64(value, options, provider);
        }
        public static Boolean TryParseDateTime(String value, IFormatProvider provider, DateTimeStyles styles, out DateTime result)
        {
            return DateTimeParse.TryParse(value, DateTimeFormatInfo.GetInstance(provider), styles, out result);
        }
        public static Boolean TryParseDateTime(String value, IFormatProvider provider, DateTimeStyles styles, out DateTime result, out TimeSpan offset)
        {
            return DateTimeParse.TryParse(value, DateTimeFormatInfo.GetInstance(provider), styles, out result, out offset);
        }
        public static Boolean TryParseDateTimeExact(String value, String format, IFormatProvider provider, DateTimeStyles styles, out DateTime result)
        {
            return DateTimeParse.TryParseExact(value, format, DateTimeFormatInfo.GetInstance(provider), styles, out result);
        }
        public static Boolean TryParseDateTimeExact(String value, String format, IFormatProvider provider, DateTimeStyles styles, out DateTime result, out TimeSpan offset)
        {
            return DateTimeParse.TryParseExact(value, format, DateTimeFormatInfo.GetInstance(provider), styles, out result, out offset);
        }
        public static Boolean TryParseDateTimeExactMultiple(String value, String[] formats, IFormatProvider provider, DateTimeStyles styles, out DateTime result)
        {
            return DateTimeParse.TryParseExactMultiple(value, formats, DateTimeFormatInfo.GetInstance(provider), styles, out result);
        }
        public static Boolean TryParseDateTimeExactMultiple(String value, String[] formats, IFormatProvider provider, DateTimeStyles styles, out DateTime result, out TimeSpan offset)
        {
            return DateTimeParse.TryParseExactMultiple(value, formats, DateTimeFormatInfo.GetInstance(provider), styles, out result, out offset);
        }
        public static Boolean TryParseDecimal(String value, NumberStyles options, IFormatProvider provider, out Decimal result)
        {
            return FormatProvider.Number.TryParseDecimal(value, options, provider, out result);
        }
        public static Boolean TryParseDouble(String value, NumberStyles options, IFormatProvider provider, out Double result)
        {
            return FormatProvider.Number.TryParseDouble(value, options, provider, out result);
        }
        public static Boolean TryParseInt32(String s, NumberStyles style, IFormatProvider provider, out Int32 result)
        {
            return FormatProvider.Number.TryParseInt32(s, style, provider, out result);
        }
        public static Boolean TryParseInt64(String s, NumberStyles style, IFormatProvider provider, out Int64 result)
        {
            return FormatProvider.Number.TryParseInt64(s, style, provider, out result);
        }
        public static Boolean TryParseSingle(String value, NumberStyles options, IFormatProvider provider, out Single result)
        {
            return FormatProvider.Number.TryParseSingle(value, options, provider, out result);
        }
        public static Boolean TryParseTimeSpan(String value, IFormatProvider provider, out TimeSpan result)
        {
            return FormatProvider.TimeSpanParse.TryParse(value, provider, out result);
        }
        public static Boolean TryParseTimeSpanExact(String value, String format, IFormatProvider provider, TimeSpanStyles styles, out TimeSpan result)
        {
            return FormatProvider.TimeSpanParse.TryParseExact(value, format, provider, styles, out result);
        }
        public static Boolean TryParseTimeSpanExactMultiple(String value, String[] formats, IFormatProvider provider, TimeSpanStyles styles, out TimeSpan result)
        {
            return FormatProvider.TimeSpanParse.TryParseExactMultiple(value, formats, provider, styles, out result);
        }
        public static Boolean TryParseUInt32(String s, NumberStyles style, IFormatProvider provider, out UInt32 result)
        {
            return FormatProvider.Number.TryParseUInt32(s, style, provider, out result);
        }
        public static Boolean TryParseUInt64(String s, NumberStyles style, IFormatProvider provider, out UInt64 result)
        {
            return FormatProvider.Number.TryParseUInt64(s, style, provider, out result);
        }
        public static bool IsPositiveInfinity(string s, IFormatProvider provider)
        {
            return FormatProvider.Number.IsPositiveInfinity(s, provider);
        }
        public static bool IsNegativeInfinity(string s, IFormatProvider provider)
        {
            return FormatProvider.Number.IsNegativeInfinity(s, provider);
        }
        public static bool IsNaNSymbol(string s, IFormatProvider provider)
        {
            return FormatProvider.Number.IsNaNSymbol(s, provider);
        }
        #endregion
    }
}
