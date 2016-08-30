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
