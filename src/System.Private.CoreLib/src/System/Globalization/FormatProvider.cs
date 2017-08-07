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
        public static string ToLowerInvariant(string s)
        {
            return CultureInfo.InvariantCulture.TextInfo.ToLower(s);
        }
        public static string ToUpper(string s)
        {
            return CultureInfo.CurrentCulture.TextInfo.ToUpper(s);
        }
        public static string ToUpperInvariant(string s)
        {
            return CultureInfo.InvariantCulture.TextInfo.ToUpper(s);
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
        public static String FormatDecimal(Decimal value, String format, IFormatProvider provider)
        {
            return Number.FormatDecimal(value, format, provider);
        }
        #endregion

        #region Parsing
        public static Decimal ParseDecimal(String value, NumberStyles options, IFormatProvider provider)
        {
            return Number.ParseDecimal(value, options, provider);
        }
        public static Boolean TryParseDecimal(String value, NumberStyles options, IFormatProvider provider, out Decimal result)
        {
            return Number.TryParseDecimal(value, options, provider, out result);
        }
        public static bool IsPositiveInfinity(string s, IFormatProvider provider)
        {
            return Number.IsPositiveInfinity(s, provider);
        }
        public static bool IsNegativeInfinity(string s, IFormatProvider provider)
        {
            return Number.IsNegativeInfinity(s, provider);
        }
        public static bool IsNaNSymbol(string s, IFormatProvider provider)
        {
            return Number.IsNaNSymbol(s, provider);
        }
        #endregion
    }
}
