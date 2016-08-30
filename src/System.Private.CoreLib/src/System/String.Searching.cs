// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace System
{
    public partial class String
    {
        public bool Contains(string value)
        {
            return (IndexOf(value, StringComparison.Ordinal) >= 0);
        }

        // Returns the index of the first occurrence of value in the current instance.
        // The search starts at startIndex and runs thorough the next count characters.
        //

        public int IndexOf(char value)
        {
            return IndexOf(value, 0, this.Length);
        }

        public int IndexOf(char value, int startIndex)
        {
            return IndexOf(value, startIndex, this.Length - startIndex);
        }

        public unsafe int IndexOf(char value, int startIndex, int count)
        {
            if (startIndex < 0 || startIndex > Length)
                throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_Index);

            if (count < 0 || count > Length - startIndex)
                throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_Count);

            fixed (char* pChars = &_firstChar)
            {
                char* pCh = pChars + startIndex;

                while (count >= 4)
                {
                    if (*pCh == value) goto ReturnIndex;
                    if (*(pCh + 1) == value) goto ReturnIndex1;
                    if (*(pCh + 2) == value) goto ReturnIndex2;
                    if (*(pCh + 3) == value) goto ReturnIndex3;

                    count -= 4;
                    pCh += 4;
                }

                while (count > 0)
                {
                    if (*pCh == value)
                        goto ReturnIndex;

                    count--;
                    pCh++;
                }

                return -1;

                ReturnIndex3: pCh++;
                ReturnIndex2: pCh++;
                ReturnIndex1: pCh++;
                ReturnIndex:
                return (int)(pCh - pChars);
            }
        }

        // Returns the index of the first occurrence of any specified character in the current instance.
        // The search starts at startIndex and runs to startIndex + count - 1.
        //

        public int IndexOfAny(char[] anyOf)
        {
            return IndexOfAny(anyOf, 0, this.Length);
        }

        public int IndexOfAny(char[] anyOf, int startIndex)
        {
            return IndexOfAny(anyOf, startIndex, this.Length - startIndex);
        }

        public unsafe int IndexOfAny(char[] anyOf, int startIndex, int count)
        {
            if (anyOf == null)
                throw new ArgumentNullException("anyOf");

            if (startIndex < 0 || startIndex > Length)
                throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_Index);

            if (count < 0 || count > Length - startIndex)
                throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_Count);

            // use probabilistic map, see InitializeProbabilisticMap
            uint* charMap = stackalloc uint[PROBABILISTICMAP_SIZE];
            InitializeProbabilisticMap(charMap, anyOf);

            fixed (char* pChars = &_firstChar)
            {
                char* pCh = pChars + startIndex;
                for (int i = 0; i < count; i++)
                {
                    char thisChar = *pCh++;
                    if (ProbablyContains(charMap, thisChar))
                        if (ArrayContains(thisChar, anyOf) >= 0)
                            return i + startIndex;
                }
            }

            return -1;
        }

        private const int PROBABILISTICMAP_BLOCK_INDEX_MASK = 0x7;
        private const int PROBABILISTICMAP_BLOCK_INDEX_SHIFT = 0x3;
        private const int PROBABILISTICMAP_SIZE = 0x8;

        // A probabilistic map is an optimization that is used in IndexOfAny/
        // LastIndexOfAny methods. The idea is to create a bit map of the characters we
        // are searching for and use this map as a "cheap" check to decide if the
        // current character in the string exists in the array of input characters.
        // There are 256 bits in the map, with each character mapped to 2 bits. Every
        // character is divided into 2 bytes, and then every byte is mapped to 1 bit.
        // The character map is an array of 8 integers acting as map blocks. The 3 lsb
        // in each byte in the character is used to index into this map to get the
        // right block, the value of the remaining 5 msb are used as the bit position
        // inside this block. 
        private static unsafe void InitializeProbabilisticMap(uint* charMap, char[] anyOf)
        {
            for (int i = 0; i < anyOf.Length; ++i)
            {
                uint hi, lo;
                char c = anyOf[i];
                hi = ((uint)c >> 8) & 0xFF;
                lo = (uint)c & 0xFF;

                uint* value = &charMap[lo & PROBABILISTICMAP_BLOCK_INDEX_MASK];
                *value |= (1u << (int)(lo >> PROBABILISTICMAP_BLOCK_INDEX_SHIFT));

                value = &charMap[hi & PROBABILISTICMAP_BLOCK_INDEX_MASK];
                *value |= (1u << (int)(hi >> PROBABILISTICMAP_BLOCK_INDEX_SHIFT));
            }
        }

        // Use the probabilistic map to decide if the character value exists in the
        // map. When this method return false, we are certain the character doesn't
        // exist, however a true return means it *may* exist.
        private static unsafe bool ProbablyContains(uint* charMap, char searchValue)
        {
            uint lo, hi;

            lo = (uint)searchValue & 0xFF;
            uint value = charMap[lo & PROBABILISTICMAP_BLOCK_INDEX_MASK];

            if ((value & (1u << (int)(lo >> PROBABILISTICMAP_BLOCK_INDEX_SHIFT))) != 0)
            {
                hi = ((uint)searchValue >> 8) & 0xFF;
                value = charMap[hi & PROBABILISTICMAP_BLOCK_INDEX_MASK];

                return (value & (1u << (int)(hi >> PROBABILISTICMAP_BLOCK_INDEX_SHIFT))) != 0;
            }

            return false;
        }

        private static int ArrayContains(char searchChar, char[] anyOf)
        {
            for (int i = 0; i < anyOf.Length; i++)
            {
                if (anyOf[i] == searchChar)
                    return i;
            }
            return -1;
        }

        public int IndexOf(String value)
        {
            return IndexOf(value, StringComparison.CurrentCulture);
        }

        public int IndexOf(String value, int startIndex, int count)
        {
            if (startIndex < 0 || startIndex > this.Length)
            {
                throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_Index);
            }

            if (count < 0 || count > this.Length - startIndex)
            {
                throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_Count);
            }

            return IndexOf(value, startIndex, count, StringComparison.CurrentCulture);
        }

        public int IndexOf(String value, int startIndex)
        {
            return IndexOf(value, startIndex, StringComparison.CurrentCulture);
        }

        public int IndexOf(String value, StringComparison comparisonType)
        {
            return IndexOf(value, 0, this.Length, comparisonType);
        }

        public int IndexOf(String value, int startIndex, StringComparison comparisonType)
        {
            return IndexOf(value, startIndex, this.Length - startIndex, comparisonType);
        }

        public int IndexOf(String value, int startIndex, int count, StringComparison comparisonType)
        {
            // Validate inputs
            if (value == null)
                throw new ArgumentNullException("value");

            if (startIndex < 0 || startIndex > this.Length)
                throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_Index);

            if (count < 0 || startIndex > this.Length - count)
                throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_Count);

            switch (comparisonType)
            {
                case StringComparison.CurrentCulture:
                    return FormatProvider.IndexOf(this, value, startIndex, count);

                case StringComparison.CurrentCultureIgnoreCase:
                    return FormatProvider.IndexOfIgnoreCase(this, value, startIndex, count);

                case StringComparison.Ordinal:
                    return FormatProvider.OrdinalIndexOf(this, value, startIndex, count);

                case StringComparison.OrdinalIgnoreCase:
                    return FormatProvider.OrdinalIndexOfIgnoreCase(this, value, startIndex, count);

                default:
                    throw new ArgumentException(SR.NotSupported_StringComparison, "comparisonType");
            }
        }

        // Returns the index of the last occurrence of a specified character in the current instance.
        // The search starts at startIndex and runs backwards to startIndex - count + 1.
        // The character at position startIndex is included in the search.  startIndex is the larger
        // index within the string.
        //

        public int LastIndexOf(char value)
        {
            return LastIndexOf(value, this.Length - 1, this.Length);
        }

        public int LastIndexOf(char value, int startIndex)
        {
            return LastIndexOf(value, startIndex, startIndex + 1);
        }

        public unsafe int LastIndexOf(char value, int startIndex, int count)
        {
            if (Length == 0)
                return -1;

            if (startIndex < 0 || startIndex >= Length)
                throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_Index);

            if (count < 0 || count - 1 > startIndex)
                throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_Count);

            fixed (char* pChars = &_firstChar)
            {
                char* pCh = pChars + startIndex;

                //We search [startIndex..EndIndex]
                while (count >= 4)
                {
                    if (*pCh == value) goto ReturnIndex;
                    if (*(pCh - 1) == value) goto ReturnIndex1;
                    if (*(pCh - 2) == value) goto ReturnIndex2;
                    if (*(pCh - 3) == value) goto ReturnIndex3;

                    count -= 4;
                    pCh -= 4;
                }

                while (count > 0)
                {
                    if (*pCh == value)
                        goto ReturnIndex;

                    count--;
                    pCh--;
                }

                return -1;

                ReturnIndex3: pCh--;
                ReturnIndex2: pCh--;
                ReturnIndex1: pCh--;
                ReturnIndex:
                return (int)(pCh - pChars);
            }
        }

        // Returns the index of the last occurrence of any specified character in the current instance.
        // The search starts at startIndex and runs backwards to startIndex - count + 1.
        // The character at position startIndex is included in the search.  startIndex is the larger
        // index within the string.
        //

        //ForceInline ... Jit can't recognize String.get_Length to determine that this is "fluff"
        public int LastIndexOfAny(char[] anyOf)
        {
            return LastIndexOfAny(anyOf, this.Length - 1, this.Length);
        }

        //ForceInline ... Jit can't recognize String.get_Length to determine that this is "fluff"
        public int LastIndexOfAny(char[] anyOf, int startIndex)
        {
            return LastIndexOfAny(anyOf, startIndex, startIndex + 1);
        }

        public unsafe int LastIndexOfAny(char[] anyOf, int startIndex, int count)
        {
            if (anyOf == null)
                throw new ArgumentNullException("anyOf");

            if (Length == 0)
                return -1;

            if ((startIndex < 0) || (startIndex >= Length))
                throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_Index);

            if ((count < 0) || ((count - 1) > startIndex))
            {
                throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_Count);
            }

            // use probabilistic map, see InitializeProbabilisticMap
            uint* charMap = stackalloc uint[PROBABILISTICMAP_SIZE];
            InitializeProbabilisticMap(charMap, anyOf);

            fixed (char* pChars = &_firstChar)
            {
                char* pCh = pChars + startIndex;

                for (int i = 0; i < count; i++)
                {
                    char thisChar = *pCh--;
                    if (ProbablyContains(charMap, thisChar))
                        if (ArrayContains(thisChar, anyOf) >= 0)
                            return startIndex - i;
                }
            }

            return -1;
        }

        // Returns the index of the last occurrence of any character in value in the current instance.
        // The search starts at startIndex and runs backwards to startIndex - count + 1.
        // The character at position startIndex is included in the search.  startIndex is the larger
        // index within the string.
        //
        public int LastIndexOf(String value)
        {
            return LastIndexOf(value, this.Length - 1, this.Length, StringComparison.CurrentCulture);
        }

        public int LastIndexOf(String value, int startIndex)
        {
            return LastIndexOf(value, startIndex, startIndex + 1, StringComparison.CurrentCulture);
        }

        public int LastIndexOf(String value, int startIndex, int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_Count);
            }

            return LastIndexOf(value, startIndex, count, StringComparison.CurrentCulture);
        }

        public int LastIndexOf(String value, StringComparison comparisonType)
        {
            return LastIndexOf(value, this.Length - 1, this.Length, comparisonType);
        }

        public int LastIndexOf(String value, int startIndex, StringComparison comparisonType)
        {
            return LastIndexOf(value, startIndex, startIndex + 1, comparisonType);
        }

        public int LastIndexOf(String value, int startIndex, int count, StringComparison comparisonType)
        {
            if (value == null)
                throw new ArgumentNullException("value");

            // Special case for 0 length input strings
            if (this.Length == 0 && (startIndex == -1 || startIndex == 0))
                return (value.Length == 0) ? 0 : -1;

            // Now after handling empty strings, make sure we're not out of range
            if (startIndex < 0 || startIndex > this.Length)
                throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_Index);

            // Make sure that we allow startIndex == this.Length
            if (startIndex == this.Length)
            {
                startIndex--;
                if (count > 0)
                    count--;
            }

            // If we are looking for nothing, just return 0
            if (value.Length == 0 && count >= 0 && startIndex - count + 1 >= 0)
                return startIndex;

            // 2nd half of this also catches when startIndex == MAXINT, so MAXINT - 0 + 1 == -1, which is < 0.
            if (count < 0 || startIndex - count + 1 < 0)
                throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_Count);

            switch (comparisonType)
            {
                case StringComparison.CurrentCulture:
                    return FormatProvider.LastIndexOf(this, value, startIndex, count);

                case StringComparison.CurrentCultureIgnoreCase:
                    return FormatProvider.LastIndexOfIgnoreCase(this, value, startIndex, count);

                case StringComparison.Ordinal:
                    return FormatProvider.OrdinalLastIndexOf(this, value, startIndex, count);

                case StringComparison.OrdinalIgnoreCase:
                    return FormatProvider.OrdinalLastIndexOfIgnoreCase(this, value, startIndex, count);
                default:
                    throw new ArgumentException(SR.NotSupported_StringComparison, "comparisonType");
            }
        }
    }
}
