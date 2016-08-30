// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System
{
    public partial class String
    {
        private const int TrimHead = 0;
        private const int TrimTail = 1;
        private const int TrimBoth = 2;

        public static String Concat(Object arg0)
        {
            if (arg0 == null)
            {
                return String.Empty;
            }
            return arg0.ToString();
        }

        public static String Concat(Object arg0, Object arg1)
        {
            if (arg0 == null)
            {
                arg0 = String.Empty;
            }

            if (arg1 == null)
            {
                arg1 = String.Empty;
            }
            return Concat(arg0.ToString(), arg1.ToString());
        }

        public static String Concat(Object arg0, Object arg1, Object arg2)
        {
            if (arg0 == null)
            {
                arg0 = String.Empty;
            }

            if (arg1 == null)
            {
                arg1 = String.Empty;
            }

            if (arg2 == null)
            {
                arg2 = String.Empty;
            }

            return Concat(arg0.ToString(), arg1.ToString(), arg2.ToString());
        }

        public static string Concat(params object[] args)
        {
            if (args == null)
            {
                throw new ArgumentNullException("args");
            }

            if (args.Length <= 1)
            {
                return args.Length == 0 ?
                    string.Empty :
                    args[0]?.ToString() ?? string.Empty;
            }

            // We need to get an intermediary string array
            // to fill with each of the args' ToString(),
            // and then just concat that in one operation.

            // This way we avoid any intermediary string representations,
            // or buffer resizing if we use StringBuilder (although the
            // latter case is partially alleviated due to StringBuilder's
            // linked-list style implementation)

            var strings = new string[args.Length];

            int totalLength = 0;

            for (int i = 0; i < args.Length; i++)
            {
                object value = args[i];

                string toString = value?.ToString() ?? string.Empty; // We need to handle both the cases when value or value.ToString() is null
                strings[i] = toString;

                totalLength += toString.Length;

                if (totalLength < 0) // Check for a positive overflow
                {
                    throw new OutOfMemoryException();
                }
            }

            // If all of the ToStrings are null/empty, just return string.Empty
            if (totalLength == 0)
            {
                return string.Empty;
            }

            string result = FastAllocateString(totalLength);
            int position = 0; // How many characters we've copied so far

            for (int i = 0; i < strings.Length; i++)
            {
                string s = strings[i];

                Contract.Assert(s != null);
                Contract.Assert(position <= totalLength - s.Length, "We didn't allocate enough space for the result string!");

                FillStringChecked(result, position, s);
                position += s.Length;
            }

            return result;
        }

        public static string Concat<T>(IEnumerable<T> values)
        {
            if (values == null)
                throw new ArgumentNullException("values");

            using (IEnumerator<T> en = values.GetEnumerator())
            {
                if (!en.MoveNext())
                    return string.Empty;

                // We called MoveNext once, so this will be the first item
                T currentValue = en.Current;

                // Call ToString before calling MoveNext again, since
                // we want to stay consistent with the below loop
                // Everything should be called in the order
                // MoveNext-Current-ToString, unless further optimizations
                // can be made, to avoid breaking changes
                string firstString = currentValue?.ToString();

                // If there's only 1 item, simply call ToString on that
                if (!en.MoveNext())
                {
                    // We have to handle the case of either currentValue
                    // or its ToString being null
                    return firstString ?? string.Empty;
                }

                StringBuilder result = StringBuilderCache.Acquire();

                result.Append(firstString);

                do
                {
                    currentValue = en.Current;

                    if (currentValue != null)
                    {
                        result.Append(currentValue.ToString());
                    }
                }
                while (en.MoveNext());

                return StringBuilderCache.GetStringAndRelease(result);
            }
        }

        public static string Concat(IEnumerable<string> values)
        {
            if (values == null)
                throw new ArgumentNullException("values");

            using (IEnumerator<string> en = values.GetEnumerator())
            {
                if (!en.MoveNext())
                    return string.Empty;

                string firstValue = en.Current;

                if (!en.MoveNext())
                {
                    return firstValue ?? string.Empty;
                }

                StringBuilder result = StringBuilderCache.Acquire();
                result.Append(firstValue);

                do
                {
                    result.Append(en.Current);
                }
                while (en.MoveNext());

                return StringBuilderCache.GetStringAndRelease(result);
            }
        }

        public static String Concat(String str0, String str1)
        {
            if (IsNullOrEmpty(str0))
            {
                if (IsNullOrEmpty(str1))
                {
                    return String.Empty;
                }
                return str1;
            }

            if (IsNullOrEmpty(str1))
            {
                return str0;
            }

            int str0Length = str0.Length;

            String result = FastAllocateString(str0Length + str1.Length);

            FillStringChecked(result, 0, str0);
            FillStringChecked(result, str0Length, str1);

            return result;
        }

        public static String Concat(String str0, String str1, String str2)
        {
            if (IsNullOrEmpty(str0))
            {
                return Concat(str1, str2);
            }

            if (IsNullOrEmpty(str1))
            {
                return Concat(str0, str2);
            }

            if (IsNullOrEmpty(str2))
            {
                return Concat(str0, str1);
            }

            int totalLength = str0.Length + str1.Length + str2.Length;

            String result = FastAllocateString(totalLength);
            FillStringChecked(result, 0, str0);
            FillStringChecked(result, str0.Length, str1);
            FillStringChecked(result, str0.Length + str1.Length, str2);

            return result;
        }

        public static String Concat(String str0, String str1, String str2, String str3)
        {
            if (IsNullOrEmpty(str0))
            {
                return Concat(str1, str2, str3);
            }

            if (IsNullOrEmpty(str1))
            {
                return Concat(str0, str2, str3);
            }

            if (IsNullOrEmpty(str2))
            {
                return Concat(str0, str1, str3);
            }

            if (IsNullOrEmpty(str3))
            {
                return Concat(str0, str1, str2);
            }

            int totalLength = str0.Length + str1.Length + str2.Length + str3.Length;

            String result = FastAllocateString(totalLength);
            FillStringChecked(result, 0, str0);
            FillStringChecked(result, str0.Length, str1);
            FillStringChecked(result, str0.Length + str1.Length, str2);
            FillStringChecked(result, str0.Length + str1.Length + str2.Length, str3);

            return result;
        }

        public static String Concat(params String[] values)
        {
            if (values == null)
                throw new ArgumentNullException("values");

            if (values.Length <= 1)
            {
                return values.Length == 0 ?
                    string.Empty :
                    values[0] ?? string.Empty;
            }

            // It's possible that the input values array could be changed concurrently on another
            // thread, such that we can't trust that each read of values[i] will be equivalent.
            // Worst case, we can make a defensive copy of the array and use that, but we first
            // optimistically try the allocation and copies assuming that the array isn't changing,
            // which represents the 99.999% case, in particular since string.Concat is used for
            // string concatenation by the languages, with the input array being a params array.

            // Sum the lengths of all input strings
            long totalLengthLong = 0;
            for (int i = 0; i < values.Length; i++)
            {
                string value = values[i];
                if (value != null)
                {
                    totalLengthLong += value.Length;
                }
            }

            // If it's too long, fail, or if it's empty, return an empty string.
            if (totalLengthLong > int.MaxValue)
            {
                throw new OutOfMemoryException();
            }
            int totalLength = (int)totalLengthLong;
            if (totalLength == 0)
            {
                return string.Empty;
            }

            // Allocate a new string and copy each input string into it
            string result = FastAllocateString(totalLength);
            int copiedLength = 0;
            for (int i = 0; i < values.Length; i++)
            {
                string value = values[i];
                if (!string.IsNullOrEmpty(value))
                {
                    int valueLen = value.Length;
                    if (valueLen > totalLength - copiedLength)
                    {
                        copiedLength = -1;
                        break;
                    }

                    FillStringChecked(result, copiedLength, value);
                    copiedLength += valueLen;
                }
            }

            // If we copied exactly the right amount, return the new string.  Otherwise,
            // something changed concurrently to mutate the input array: fall back to
            // doing the concatenation again, but this time with a defensive copy. This
            // fall back should be extremely rare.
            return copiedLength == totalLength ? result : Concat((string[])values.Clone());
        }

        public static String Format(String format, params Object[] args)
        {
            if (args == null)
            {
                // To preserve the original exception behavior, throw an exception about format if both
                // args and format are null. The actual null check for format is in FormatHelper.
                throw new ArgumentNullException((format == null) ? "format" : "args");
            }

            return FormatHelper(null, format, new ParamsArray(args));
        }

        public static String Format(String format, Object arg0)
        {
            return FormatHelper(null, format, new ParamsArray(arg0));
        }

        public static String Format(String format, Object arg0, Object arg1)
        {
            return FormatHelper(null, format, new ParamsArray(arg0, arg1));
        }

        public static String Format(String format, Object arg0, Object arg1, Object arg2)
        {
            return FormatHelper(null, format, new ParamsArray(arg0, arg1, arg2));
        }

        public static String Format(IFormatProvider provider, String format, params Object[] args)
        {
            if (args == null)
            {
                // To preserve the original exception behavior, throw an exception about format if both
                // args and format are null. The actual null check for format is in FormatHelper.
                throw new ArgumentNullException((format == null) ? "format" : "args");
            }

            return FormatHelper(provider, format, new ParamsArray(args));
        }

        public static String Format(IFormatProvider provider, String format, Object arg0)
        {
            return FormatHelper(provider, format, new ParamsArray(arg0));
        }

        public static String Format(IFormatProvider provider, String format, Object arg0, Object arg1)
        {
            return FormatHelper(provider, format, new ParamsArray(arg0, arg1));
        }

        public static String Format(IFormatProvider provider, String format, Object arg0, Object arg1, Object arg2)
        {
            return FormatHelper(provider, format, new ParamsArray(arg0, arg1, arg2));
        }

        private static String FormatHelper(IFormatProvider provider, String format, ParamsArray args)
        {
            if (format == null)
                throw new ArgumentNullException("format");

            return StringBuilderCache.GetStringAndRelease(
                StringBuilderCache
                    .Acquire(format.Length + args.Length * 8)
                    .AppendFormatHelper(provider, format, args));
        }

        public String Insert(int startIndex, String value)
        {
            if (value == null)
                throw new ArgumentNullException("value");
            if (startIndex < 0 || startIndex > this.Length)
                throw new ArgumentOutOfRangeException("startIndex");

            int oldLength = Length;
            int insertLength = value.Length;

            if (oldLength == 0)
                return value;
            if (insertLength == 0)
                return this;

            int newLength = oldLength + insertLength;
            if (newLength < 0)
                throw new OutOfMemoryException();
            String result = FastAllocateString(newLength);
            unsafe
            {
                fixed (char* srcThis = &_firstChar)
                {
                    fixed (char* srcInsert = &value._firstChar)
                    {
                        fixed (char* dst = &result._firstChar)
                        {
                            wstrcpy(dst, srcThis, startIndex);
                            wstrcpy(dst + startIndex, srcInsert, insertLength);
                            wstrcpy(dst + startIndex + insertLength, srcThis + startIndex, oldLength - startIndex);
                        }
                    }
                }
            }
            return result;
        }

        // Removes a set of characters from the end of this string.

        public String Trim(params char[] trimChars)
        {
            if (null == trimChars || trimChars.Length == 0)
            {
                return TrimHelper(TrimBoth);
            }
            return TrimHelper(trimChars, TrimBoth);
        }

        // Removes a set of characters from the beginning of this string.
        public String TrimStart(params char[] trimChars)
        {
            if (null == trimChars || trimChars.Length == 0)
            {
                return TrimHelper(TrimHead);
            }
            return TrimHelper(trimChars, TrimHead);
        }


        // Removes a set of characters from the end of this string.
        public String TrimEnd(params char[] trimChars)
        {
            if (null == trimChars || trimChars.Length == 0)
            {
                return TrimHelper(TrimTail);
            }
            return TrimHelper(trimChars, TrimTail);
        }

        // Trims the whitespace from both ends of the string.  Whitespace is defined by
        // Char.IsWhiteSpace.
        //
        public String Trim()
        {
            return TrimHelper(TrimBoth);
        }

        private String TrimHelper(int trimType)
        {
            //end will point to the first non-trimmed character on the right
            //start will point to the first non-trimmed character on the Left
            int end = this.Length - 1;
            int start = 0;

            //Trim specified characters.
            if (trimType != TrimTail)
            {
                for (start = 0; start < this.Length; start++)
                {
                    if (!Char.IsWhiteSpace(this[start])) break;
                }
            }

            if (trimType != TrimHead)
            {
                for (end = Length - 1; end >= start; end--)
                {
                    if (!Char.IsWhiteSpace(this[end])) break;
                }
            }

            return CreateTrimmedString(start, end);
        }

        private String TrimHelper(char[] trimChars, int trimType)
        {
            //end will point to the first non-trimmed character on the right
            //start will point to the first non-trimmed character on the Left
            int end = this.Length - 1;
            int start = 0;

            //Trim specified characters.
            if (trimType != TrimTail)
            {
                for (start = 0; start < this.Length; start++)
                {
                    int i = 0;
                    char ch = this[start];
                    for (i = 0; i < trimChars.Length; i++)
                    {
                        if (trimChars[i] == ch) break;
                    }
                    if (i == trimChars.Length)
                    { // the character is not white space
                        break;
                    }
                }
            }

            if (trimType != TrimHead)
            {
                for (end = Length - 1; end >= start; end--)
                {
                    int i = 0;
                    char ch = this[end];
                    for (i = 0; i < trimChars.Length; i++)
                    {
                        if (trimChars[i] == ch) break;
                    }
                    if (i == trimChars.Length)
                    { // the character is not white space
                        break;
                    }
                }
            }

            return CreateTrimmedString(start, end);
        }

        private String CreateTrimmedString(int start, int end)
        {
            int len = end - start + 1;
            if (len == this.Length)
            {
                // Don't allocate a new string as the trimmed string has not changed.
                return this;
            }
            else
            {
                if (len == 0)
                {
                    return String.Empty;
                }
                return InternalSubString(start, len);
            }
        }
    }
}
