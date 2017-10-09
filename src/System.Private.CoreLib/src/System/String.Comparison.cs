// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace System
{
    public partial class String
    {
        //
        //Native Static Methods
        //

        private static unsafe int FastCompareStringHelper(uint* strAChars, int countA, uint* strBChars, int countB)
        {
            int count = (countA < countB) ? countA : countB;

#if BIT64
            long diff = (long)((byte*)strAChars - (byte*)strBChars);
#else
            int diff = (int)((byte*)strAChars - (byte*)strBChars);
#endif

#if BIT64
            int alignmentA = (int)((long)strAChars) & (sizeof(IntPtr) - 1);
            int alignmentB = (int)((long)strBChars) & (sizeof(IntPtr) - 1);

            if (alignmentA == alignmentB)
            {
                if ((alignmentA == 2 || alignmentA == 6) && (count >= 1))
                {
                    char* ptr2 = (char*)strBChars;

                    if ((*((char*)((byte*)ptr2 + diff)) - *ptr2) != 0)
                        return ((int)*((char*)((byte*)ptr2 + diff)) - (int)*ptr2);

                    strBChars = (uint*)(++ptr2);
                    count -= 1;
                    alignmentA = (alignmentA == 2 ? 4 : 0);
                }

                if ((alignmentA == 4) && (count >= 2))
                {
                    uint* ptr2 = (uint*)strBChars;

                    if ((*((uint*)((byte*)ptr2 + diff)) - *ptr2) != 0)
                    {
                        char* chkptr1 = (char*)((byte*)strBChars + diff);
                        char* chkptr2 = (char*)strBChars;

                        if (*chkptr1 != *chkptr2)
                            return ((int)*chkptr1 - (int)*chkptr2);
                        return ((int)*(chkptr1 + 1) - (int)*(chkptr2 + 1));
                    }
                    strBChars = ++ptr2;
                    count -= 2;
                    alignmentA = 0;
                }

                if ((alignmentA == 0))
                {
                    while (count >= 4)
                    {
                        long* ptr2 = (long*)strBChars;

                        if ((*((long*)((byte*)ptr2 + diff)) - *ptr2) != 0)
                        {
                            if ((*((uint*)((byte*)ptr2 + diff)) - *(uint*)ptr2) != 0)
                            {
                                char* chkptr1 = (char*)((byte*)strBChars + diff);
                                char* chkptr2 = (char*)strBChars;

                                if (*chkptr1 != *chkptr2)
                                    return ((int)*chkptr1 - (int)*chkptr2);
                                return ((int)*(chkptr1 + 1) - (int)*(chkptr2 + 1));
                            }
                            else
                            {
                                char* chkptr1 = (char*)((uint*)((byte*)strBChars + diff) + 1);
                                char* chkptr2 = (char*)((uint*)strBChars + 1);

                                if (*chkptr1 != *chkptr2)
                                    return ((int)*chkptr1 - (int)*chkptr2);
                                return ((int)*(chkptr1 + 1) - (int)*(chkptr2 + 1));
                            }
                        }
                        strBChars = (uint*)(++ptr2);
                        count -= 4;
                    }
                }

                {
                    char* ptr2 = (char*)strBChars;
                    while ((count -= 1) >= 0)
                    {
                        if ((*((char*)((byte*)ptr2 + diff)) - *ptr2) != 0)
                            return ((int)*((char*)((byte*)ptr2 + diff)) - (int)*ptr2);
                        ++ptr2;
                    }
                }
            }
            else
#endif // BIT64
            {
#if BIT64
                if (Math.Abs(alignmentA - alignmentB) == 4)
                {
                    if ((alignmentA == 2) || (alignmentB == 2))
                    {
                        char* ptr2 = (char*)strBChars;

                        if ((*((char*)((byte*)ptr2 + diff)) - *ptr2) != 0)
                            return ((int)*((char*)((byte*)ptr2 + diff)) - (int)*ptr2);
                        strBChars = (uint*)(++ptr2);
                        count -= 1;
                    }
                }
#endif // BIT64

                // Loop comparing a DWORD at a time.
                // Reads are potentially unaligned
                while ((count -= 2) >= 0)
                {
                    if ((*((uint*)((byte*)strBChars + diff)) - *strBChars) != 0)
                    {
                        char* ptr1 = (char*)((byte*)strBChars + diff);
                        char* ptr2 = (char*)strBChars;
                        if (*ptr1 != *ptr2)
                            return ((int)*ptr1 - (int)*ptr2);
                        return ((int)*(ptr1 + 1) - (int)*(ptr2 + 1));
                    }
                    ++strBChars;
                }

                int c;
                if (count == -1)
                    if ((c = *((char*)((byte*)strBChars + diff)) - *((char*)strBChars)) != 0)
                        return c;
            }

            return countA - countB;
        }

        //
        //
        // NATIVE INSTANCE METHODS
        //
        //

        //
        // Search/Query methods
        //

        //
        // Common worker for the various Equality methods. The caller must have already ensured that 
        // both strings are non-null and that their lengths are equal. Ther caller should also have
        // done the Object.ReferenceEquals() fastpath check as we won't repeat it here.
        //
        private static unsafe bool EqualsHelper(String strA, String strB)
        {
            Debug.Assert(strA != null);
            Debug.Assert(strB != null);
            Debug.Assert(strA.Length == strB.Length);

            int length = strA.Length;
            fixed (char* ap = &strA._firstChar) fixed (char* bp = &strB._firstChar)
            {
                char* a = ap;
                char* b = bp;

#if BIT64
                // Single int read aligns pointers for the following long reads
                // PERF: No length check needed as there is always an int32 worth of string allocated
                //       This read can also include the null terminator which both strings will have
                if (*(int*)a != *(int*)b) return false;
                length -= 2; a += 2; b += 2;

                // for 64-bit platforms we unroll by 12 and
                // check 3 qword at a time. This is less code
                // than the 32 bit case and is a shorter path length

                while (length >= 12)
                {
                    if (*(long*)a != *(long*)b) goto ReturnFalse;
                    if (*(long*)(a + 4) != *(long*)(b + 4)) goto ReturnFalse;
                    if (*(long*)(a + 8) != *(long*)(b + 8)) goto ReturnFalse;
                    length -= 12; a += 12; b += 12;
                }
#else
                while (length >= 10)
                {
                    if (*(int*)a != *(int*)b) goto ReturnFalse;
                    if (*(int*)(a + 2) != *(int*)(b + 2)) goto ReturnFalse;
                    if (*(int*)(a + 4) != *(int*)(b + 4)) goto ReturnFalse;
                    if (*(int*)(a + 6) != *(int*)(b + 6)) goto ReturnFalse;
                    if (*(int*)(a + 8) != *(int*)(b + 8)) goto ReturnFalse;
                    length -= 10; a += 10; b += 10;
                }
#endif

                // This depends on the fact that the String objects are
                // always zero terminated and that the terminating zero is not included
                // in the length. For odd string sizes, the last compare will include
                // the zero terminator.
                while (length > 0)
                {
                    if (*(int*)a != *(int*)b) goto ReturnFalse;
                    length -= 2; a += 2; b += 2;
                }

                return true;

            ReturnFalse:
                return false;
            }
        }

        private static unsafe bool StartsWithOrdinalHelper(String str, String startsWith)
        {
            Debug.Assert(str != null);
            Debug.Assert(startsWith != null);
            Debug.Assert(str.Length >= startsWith.Length);

            int length = startsWith.Length;

            fixed (char* ap = &str._firstChar) fixed (char* bp = &startsWith._firstChar)
            {
                char* a = ap;
                char* b = bp;

#if BIT64
                // Single int read aligns pointers for the following long reads
                // No length check needed as this method is called when length >= 2
                Debug.Assert(length >= 2);
                if (*(int*)a != *(int*)b) goto ReturnFalse;
                length -= 2; a += 2; b += 2;

                while (length >= 12)
                {
                    if (*(long*)a != *(long*)b) goto ReturnFalse;
                    if (*(long*)(a + 4) != *(long*)(b + 4)) goto ReturnFalse;
                    if (*(long*)(a + 8) != *(long*)(b + 8)) goto ReturnFalse;
                    length -= 12; a += 12; b += 12;
                }
#else
                while (length >= 10)
                {
                    if (*(int*)a != *(int*)b) goto ReturnFalse;
                    if (*(int*)(a + 2) != *(int*)(b + 2)) goto ReturnFalse;
                    if (*(int*)(a + 4) != *(int*)(b + 4)) goto ReturnFalse;
                    if (*(int*)(a + 6) != *(int*)(b + 6)) goto ReturnFalse;
                    if (*(int*)(a + 8) != *(int*)(b + 8)) goto ReturnFalse;
                    length -= 10; a += 10; b += 10;
                }
#endif

                while (length >= 2)
                {
                    if (*(int*)a != *(int*)b) goto ReturnFalse;
                    length -= 2; a += 2; b += 2;
                }

                // PERF: This depends on the fact that the String objects are always zero terminated 
                // and that the terminating zero is not included in the length. For even string sizes
                // this compare can include the zero terminator. Bitwise OR avoids a branch.
                return length == 0 | *a == *b;

            ReturnFalse:
                return false;
            }
        }

        private static unsafe int CompareOrdinalHelper(String strA, String strB)
        {
            Debug.Assert(strA != null);
            Debug.Assert(strB != null);

            // NOTE: This may be subject to change if eliminating the check
            // in the callers makes them small enough to be inlined
            Debug.Assert(strA._firstChar == strB._firstChar,
                "For performance reasons, callers of this method should " +
                "check/short-circuit beforehand if the first char is the same.");

            int length = Math.Min(strA.Length, strB.Length);

            fixed (char* ap = &strA._firstChar) fixed (char* bp = &strB._firstChar)
            {
                char* a = ap;
                char* b = bp;

                // Check if the second chars are different here
                // The reason we check if _firstChar is different is because
                // it's the most common case and allows us to avoid a method call
                // to here.
                // The reason we check if the second char is different is because
                // if the first two chars the same we can increment by 4 bytes,
                // leaving us word-aligned on both 32-bit (12 bytes into the string)
                // and 64-bit (16 bytes) platforms.

                // For empty strings, the second char will be null due to padding.
                // The start of the string is the EE type pointer + string length,
                // which takes up 8 bytes on 32-bit, 12 on x64. For empty strings,
                // the null terminator immediately follows, leaving us with an object
                // 10/14 bytes in size. Since everything needs to be a multiple
                // of 4/8, this will get padded and zeroed out.

                // For one-char strings the second char will be the null terminator.

                // NOTE: If in the future there is a way to read the second char
                // without pinning the string (e.g. System.Runtime.CompilerServices.Unsafe
                // is exposed to mscorlib, or a future version of C# allows inline IL),
                // then do that and short-circuit before the fixed.

                if (*(a + 1) != *(b + 1)) goto DiffOffset1;

                // Since we know that the first two chars are the same,
                // we can increment by 2 here and skip 4 bytes.
                // This leaves us 8-byte aligned, which results
                // on better perf for 64-bit platforms.
                length -= 2; a += 2; b += 2;

                // unroll the loop
#if BIT64
                while (length >= 12)
                {
                    if (*(long*)a != *(long*)b) goto DiffOffset0;
                    if (*(long*)(a + 4) != *(long*)(b + 4)) goto DiffOffset4;
                    if (*(long*)(a + 8) != *(long*)(b + 8)) goto DiffOffset8;
                    length -= 12; a += 12; b += 12;
                }
#else // BIT64
                while (length >= 10)
                {
                    if (*(int*)a != *(int*)b) goto DiffOffset0;
                    if (*(int*)(a + 2) != *(int*)(b + 2)) goto DiffOffset2;
                    if (*(int*)(a + 4) != *(int*)(b + 4)) goto DiffOffset4;
                    if (*(int*)(a + 6) != *(int*)(b + 6)) goto DiffOffset6;
                    if (*(int*)(a + 8) != *(int*)(b + 8)) goto DiffOffset8;
                    length -= 10; a += 10; b += 10;
                }
#endif // BIT64

                // Fallback loop:
                // go back to slower code path and do comparison on 4 bytes at a time.
                // This depends on the fact that the String objects are
                // always zero terminated and that the terminating zero is not included
                // in the length. For odd string sizes, the last compare will include
                // the zero terminator.
                while (length > 0)
                {
                    if (*(int*)a != *(int*)b) goto DiffNextInt;
                    length -= 2;
                    a += 2;
                    b += 2;
                }

                // At this point, we have compared all the characters in at least one string.
                // The longer string will be larger.
                return strA.Length - strB.Length;

#if BIT64
                DiffOffset8: a += 4; b += 4;
                DiffOffset4: a += 4; b += 4;
#else // BIT64
            // Use jumps instead of falling through, since
            // otherwise going to DiffOffset8 will involve
            // 8 add instructions before getting to DiffNextInt
            DiffOffset8: a += 8; b += 8; goto DiffOffset0;
            DiffOffset6: a += 6; b += 6; goto DiffOffset0;
            DiffOffset4: a += 2; b += 2;
            DiffOffset2: a += 2; b += 2;
#endif // BIT64

            DiffOffset0:
            // If we reached here, we already see a difference in the unrolled loop above
#if BIT64
                if (*(int*)a == *(int*)b)
                {
                    a += 2; b += 2;
                }
#endif // BIT64

            DiffNextInt:
                if (*a != *b) return *a - *b;

                DiffOffset1:
                Debug.Assert(*(a + 1) != *(b + 1), "This char must be different if we reach here!");
                return *(a + 1) - *(b + 1);
            }
        }

        internal static unsafe int CompareOrdinalHelper(string strA, int indexA, int countA, string strB, int indexB, int countB)
        {
            // Argument validation should be handled by callers.
            Debug.Assert(strA != null && strB != null);
            Debug.Assert(indexA >= 0 && indexB >= 0);
            Debug.Assert(countA >= 0 && countB >= 0);
            Debug.Assert(countA <= strA.Length - indexA);
            Debug.Assert(countB <= strB.Length - indexB);

            // Set up the loop variables.
            fixed (char* pStrA = &strA._firstChar, pStrB = &strB._firstChar)
            {
                char* strAChars = pStrA + indexA;
                char* strBChars = pStrB + indexB;
                return FastCompareStringHelper((uint*)strAChars, countA, (uint*)strBChars, countB);
            }
        }

        public static int Compare(String strA, String strB)
        {
            return Compare(strA, strB, StringComparison.CurrentCulture);
        }

        public static int Compare(String strA, String strB, bool ignoreCase)
        {
            var comparisonType = ignoreCase ? StringComparison.CurrentCultureIgnoreCase : StringComparison.CurrentCulture;
            return Compare(strA, strB, comparisonType);
        }

        // Provides a more flexible function for string comparision. See StringComparison 
        // for meaning of different comparisonType.
        public static int Compare(String strA, String strB, StringComparison comparisonType)
        {
            if (comparisonType < StringComparison.CurrentCulture || comparisonType > StringComparison.OrdinalIgnoreCase)
            {
                throw new ArgumentException(SR.NotSupported_StringComparison, nameof(comparisonType));
            }

            if (object.ReferenceEquals(strA, strB))
            {
                return 0;
            }

            // They can't both be null at this point.
            if (strA == null)
            {
                return -1;
            }
            if (strB == null)
            {
                return 1;
            }


            switch (comparisonType)
            {
                case StringComparison.CurrentCulture:
                    return CultureInfo.CurrentCulture.CompareInfo.Compare(strA, 0, strA.Length, strB, 0, strB.Length, CompareOptions.None);

                case StringComparison.CurrentCultureIgnoreCase:
                    return CultureInfo.CurrentCulture.CompareInfo.Compare(strA, 0, strA.Length, strB, 0, strB.Length, CompareOptions.IgnoreCase);

                case StringComparison.Ordinal:
                    // Most common case: first character is different.
                    // Returns false for empty strings.
                    if (strA._firstChar != strB._firstChar)
                    {
                        return strA._firstChar - strB._firstChar;
                    }

                    return CompareOrdinalHelper(strA, strB);

                case StringComparison.OrdinalIgnoreCase:
                    return CompareInfo.CompareOrdinalIgnoreCase(strA, 0, strA.Length, strB, 0, strB.Length);

                case StringComparison.InvariantCulture:
                    return CultureInfo.InvariantCulture.CompareInfo.Compare(strA, strB, CompareOptions.None);

                case StringComparison.InvariantCultureIgnoreCase:
                    return CultureInfo.InvariantCulture.CompareInfo.Compare(strA, strB, CompareOptions.IgnoreCase);

                default:
                    throw new NotSupportedException(SR.NotSupported_StringComparison);
            }
        }

        // Provides a culture-correct string comparison. strA is compared to strB
        // to determine whether it is lexicographically less, equal, or greater, and then a
        // negative integer, 0, or a positive integer is returned; respectively.
        //
        public static int Compare(String strA, String strB, CultureInfo culture, CompareOptions options)
        {
            if (culture == null)
            {
                throw new ArgumentNullException(nameof(culture));
            }

            return culture.CompareInfo.Compare(strA, strB, options);
        }

        // Provides a culture-correct string comparison. strA is compared to strB
        // to determine whether it is lexicographically less, equal, or greater, and then a
        // negative integer, 0, or a positive integer is returned; respectively.
        // The case-sensitive option is set by ignoreCase, and the culture is set
        // by culture
        //
        public static int Compare(String strA, String strB, bool ignoreCase, CultureInfo culture)
        {
            CompareOptions options = ignoreCase ? CompareOptions.IgnoreCase : CompareOptions.None;
            return Compare(strA, strB, culture, options);
        }

        // Determines whether two string regions match.  The substring of strA beginning
        // at indexA of length length is compared with the substring of strB
        // beginning at indexB of the same length.
        //

        public static int Compare(String strA, int indexA, String strB, int indexB, int length)
        {
            // NOTE: It's important we call the boolean overload, and not the StringComparison
            // one. The two have some subtly different behavior (see notes in the former).
            return Compare(strA, indexA, strB, indexB, length, ignoreCase: false);
        }

        // Determines whether two string regions match.  The substring of strA beginning
        // at indexA of length count is compared with the substring of strB
        // beginning at indexB of the same length.  Case sensitivity is determined by the ignoreCase boolean.
        //

        public static int Compare(String strA, int indexA, String strB, int indexB, int length, bool ignoreCase)
        {
            // Ideally we would just forward to the string.Compare overload that takes
            // a StringComparison parameter, and just pass in CurrentCulture/CurrentCultureIgnoreCase.
            // That function will return early if an optimization can be applied, e.g. if
            // (object)strA == strB && indexA == indexB then it will return 0 straightaway.
            // There are a couple of subtle behavior differences that prevent us from doing so
            // however:
            // - string.Compare(null, -1, null, -1, -1, StringComparison.CurrentCulture) works
            //   since that method also returns early for nulls before validation. It shouldn't
            //   for this overload.
            // - Since we originally forwarded to FormatProvider for all of the argument
            //   validation logic, the ArgumentOutOfRangeExceptions thrown will contain different
            //   parameter names.
            // Therefore, we have to duplicate some of the logic here.

            int lengthA = length;
            int lengthB = length;

            if (strA != null)
            {
                lengthA = Math.Min(lengthA, strA.Length - indexA);
            }

            if (strB != null)
            {
                lengthB = Math.Min(lengthB, strB.Length - indexB);
            }

            CompareOptions options = ignoreCase ? CompareOptions.IgnoreCase : CompareOptions.None;
            return CultureInfo.CurrentCulture.CompareInfo.Compare(strA, indexA, lengthA, strB, indexB, lengthB, options);
        }

        // Determines whether two string regions match.  The substring of strA beginning
        // at indexA of length length is compared with the substring of strB
        // beginning at indexB of the same length.  Case sensitivity is determined by the ignoreCase boolean,
        // and the culture is set by culture.
        //
        public static int Compare(String strA, int indexA, String strB, int indexB, int length, bool ignoreCase, CultureInfo culture)
        {
            CompareOptions options = ignoreCase ? CompareOptions.IgnoreCase : CompareOptions.None;
            return Compare(strA, indexA, strB, indexB, length, culture, options);
        }

        // Determines whether two string regions match.  The substring of strA beginning
        // at indexA of length length is compared with the substring of strB
        // beginning at indexB of the same length.
        //
        public static int Compare(String strA, int indexA, String strB, int indexB, int length, CultureInfo culture, CompareOptions options)
        {
            if (culture == null)
            {
                throw new ArgumentNullException(nameof(culture));
            }

            int lengthA = length;
            int lengthB = length;

            if (strA != null)
            {
                lengthA = Math.Min(lengthA, strA.Length - indexA);
            }

            if (strB != null)
            {
                lengthB = Math.Min(lengthB, strB.Length - indexB);
            }

            return culture.CompareInfo.Compare(strA, indexA, lengthA, strB, indexB, lengthB, options);
        }

        public static int Compare(String strA, int indexA, String strB, int indexB, int length, StringComparison comparisonType)
        {
            if (comparisonType < StringComparison.CurrentCulture || comparisonType > StringComparison.OrdinalIgnoreCase)
            {
                throw new ArgumentException(SR.NotSupported_StringComparison, nameof(comparisonType));
            }

            if (strA == null || strB == null)
            {
                if (object.ReferenceEquals(strA, strB))
                {
                    // They're both null
                    return 0;
                }

                return strA == null ? -1 : 1;
            }

            // @TODO: Spec#: Figure out what to do here with the return statement above.
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), SR.ArgumentOutOfRange_NegativeLength);
            }

            if (indexA < 0 || indexB < 0)
            {
                string paramName = indexA < 0 ? nameof(indexA) : nameof(indexB);
                throw new ArgumentOutOfRangeException(paramName, SR.ArgumentOutOfRange_Index);
            }

            if (strA.Length - indexA < 0 || strB.Length - indexB < 0)
            {
                string paramName = strA.Length - indexA < 0 ? nameof(indexA) : nameof(indexB);
                throw new ArgumentOutOfRangeException(paramName, SR.ArgumentOutOfRange_Index);
            }

            if (length == 0 || (object.ReferenceEquals(strA, strB) && indexA == indexB))
            {
                return 0;
            }

            int lengthA = Math.Min(length, strA.Length - indexA);
            int lengthB = Math.Min(length, strB.Length - indexB);

            switch (comparisonType)
            {
                case StringComparison.CurrentCulture:
                    return CultureInfo.CurrentCulture.CompareInfo.Compare(strA, indexA, lengthA, strB, indexB, lengthB, CompareOptions.None);

                case StringComparison.CurrentCultureIgnoreCase:
                    return CultureInfo.CurrentCulture.CompareInfo.Compare(strA, indexA, lengthA, strB, indexB, lengthB, CompareOptions.IgnoreCase);

                case StringComparison.Ordinal:
                    return CompareOrdinalHelper(strA, indexA, lengthA, strB, indexB, lengthB);

                case StringComparison.OrdinalIgnoreCase:
                    return CompareInfo.CompareOrdinalIgnoreCase(strA, indexA, lengthA, strB, indexB, lengthB);

                case StringComparison.InvariantCulture:
                    return CultureInfo.InvariantCulture.CompareInfo.Compare(strA, indexA, lengthA, strB, indexB, lengthB, CompareOptions.None);

                case StringComparison.InvariantCultureIgnoreCase:
                    return CultureInfo.InvariantCulture.CompareInfo.Compare(strA, indexA, lengthA, strB, indexB, lengthB, CompareOptions.IgnoreCase);

                default:
                    throw new ArgumentException(SR.NotSupported_StringComparison);
            }
        }

        // Compares strA and strB using an ordinal (code-point) comparison.
        //

        public static int CompareOrdinal(String strA, String strB)
        {
            if (object.ReferenceEquals(strA, strB))
            {
                return 0;
            }

            // They can't both be null at this point.
            if (strA == null)
            {
                return -1;
            }
            if (strB == null)
            {
                return 1;
            }

            // Most common case, first character is different.
            // This will return false for empty strings.
            if (strA._firstChar != strB._firstChar)
            {
                return strA._firstChar - strB._firstChar;
            }

            return CompareOrdinalHelper(strA, strB);
        }

        // TODO https://github.com/dotnet/corefx/issues/21395: Expose this publicly?
        internal static int CompareOrdinal(ReadOnlySpan<char> strA, ReadOnlySpan<char> strB)
        {
            // TODO: This needs to be optimized / unrolled.  It can't just use CompareOrdinalHelper(str, str)
            // (changed to accept spans) because its implementation is based on a string layout,
            // in a way that doesn't work when there isn't guaranteed to be a null terminator.

            int minLength = Math.Min(strA.Length, strB.Length);
            for (int i = 0; i < minLength; i++)
            {
                if (strA[i] != strB[i])
                {
                    return strA[i] - strB[i];
                }
            }

            return strA.Length - strB.Length;
        }

        public static int CompareOrdinal(String strA, int indexA, String strB, int indexB, int length)
        {
            if (strA == null || strB == null)
            {
                if (object.ReferenceEquals(strA, strB))
                {
                    // They're both null
                    return 0;
                }

                return strA == null ? -1 : 1;
            }

            // COMPAT: Checking for nulls should become before the arguments are validated,
            // but other optimizations which allow us to return early should come after.

            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), SR.ArgumentOutOfRange_NegativeCount);
            }

            if (indexA < 0 || indexB < 0)
            {
                string paramName = indexA < 0 ? nameof(indexA) : nameof(indexB);
                throw new ArgumentOutOfRangeException(paramName, SR.ArgumentOutOfRange_Index);
            }

            int lengthA = Math.Min(length, strA.Length - indexA);
            int lengthB = Math.Min(length, strB.Length - indexB);

            if (lengthA < 0 || lengthB < 0)
            {
                string paramName = lengthA < 0 ? nameof(indexA) : nameof(indexB);
                throw new ArgumentOutOfRangeException(paramName, SR.ArgumentOutOfRange_Index);
            }

            if (length == 0 || (object.ReferenceEquals(strA, strB) && indexA == indexB))
            {
                return 0;
            }

            return CompareOrdinalHelper(strA, indexA, lengthA, strB, indexB, lengthB);
        }

        // Compares this String to another String (cast as object), returning an integer that
        // indicates the relationship. This method returns a value less than 0 if this is less than value, 0
        // if this is equal to value, or a value greater than 0 if this is greater than value.
        //

        public int CompareTo(Object value)
        {
            if (value == null)
            {
                return 1;
            }

            string other = value as string;

            if (other == null)
            {
                throw new ArgumentException(SR.Arg_MustBeString);
            }

            return CompareTo(other); // will call the string-based overload
        }

        // Determines the sorting relation of StrB to the current instance.
        //

        public int CompareTo(String strB)
        {
            return string.Compare(this, strB, StringComparison.CurrentCulture);
        }

        // Determines whether a specified string is a suffix of the the current instance.
        //
        // The case-sensitive and culture-sensitive option is set by options,
        // and the default culture is used.
        //        

        public Boolean EndsWith(String value)
        {
            return EndsWith(value, StringComparison.CurrentCulture);
        }

        public Boolean EndsWith(String value, StringComparison comparisonType)
        {
            if ((Object)value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (comparisonType < StringComparison.CurrentCulture || comparisonType > StringComparison.OrdinalIgnoreCase)
            {
                throw new ArgumentException(SR.NotSupported_StringComparison, nameof(comparisonType));
            }

            if ((Object)this == (Object)value)
            {
                return true;
            }

            if (value.Length == 0)
            {
                return true;
            }

            switch (comparisonType)
            {
                case StringComparison.CurrentCulture:
                    return CultureInfo.CurrentCulture.CompareInfo.IsSuffix(this, value, CompareOptions.None);

                case StringComparison.CurrentCultureIgnoreCase:
                    return CultureInfo.CurrentCulture.CompareInfo.IsSuffix(this, value, CompareOptions.IgnoreCase);

                case StringComparison.Ordinal:
                    return this.Length < value.Length ? false : (CompareOrdinalHelper(this, this.Length - value.Length, value.Length, value, 0, value.Length) == 0);

                case StringComparison.OrdinalIgnoreCase:
                    return this.Length < value.Length ? false : (CompareInfo.CompareOrdinalIgnoreCase(this, this.Length - value.Length, value.Length, value, 0, value.Length) == 0);

                case StringComparison.InvariantCulture:
                    return CultureInfo.InvariantCulture.CompareInfo.IsSuffix(this, value, CompareOptions.None);

                case StringComparison.InvariantCultureIgnoreCase:
                    return CultureInfo.InvariantCulture.CompareInfo.IsSuffix(this, value, CompareOptions.IgnoreCase);

                default:
                    throw new ArgumentException(SR.NotSupported_StringComparison, nameof(comparisonType));
            }
        }

        public Boolean EndsWith(String value, Boolean ignoreCase, CultureInfo culture)
        {
            if (null == value)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if ((object)this == (object)value)
            {
                return true;
            }

            CultureInfo referenceCulture;
            if (culture == null)
                referenceCulture = CultureInfo.CurrentCulture;
            else
                referenceCulture = culture;

            return referenceCulture.CompareInfo.IsSuffix(this, value, ignoreCase ? CompareOptions.IgnoreCase : CompareOptions.None);
        }

        public bool EndsWith(char value)
        {
            int thisLen = Length;
            return thisLen != 0 && this[thisLen - 1] == value;
        }

        // Determines whether two strings match.

        public override bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj))
                return true;

            String str = obj as String;
            if (str == null)
                return false;

            if (this.Length != str.Length)
                return false;

            return EqualsHelper(this, str);
        }

        // Determines whether two strings match.


        public bool Equals(String value)
        {
            if (Object.ReferenceEquals(this, value))
                return true;

            // NOTE: No need to worry about casting to object here.
            // If either side of an == comparison between strings
            // is null, Roslyn generates a simple ceq instruction
            // instead of calling string.op_Equality.
            if (value == null)
                return false;

            if (this.Length != value.Length)
                return false;

            return EqualsHelper(this, value);
        }


        public bool Equals(String value, StringComparison comparisonType)
        {
            if (comparisonType < StringComparison.CurrentCulture || comparisonType > StringComparison.OrdinalIgnoreCase)
                throw new ArgumentException(SR.NotSupported_StringComparison, nameof(comparisonType));

            if ((Object)this == (Object)value)
            {
                return true;
            }

            if ((Object)value == null)
            {
                return false;
            }

            switch (comparisonType)
            {
                case StringComparison.CurrentCulture:
                    return (CultureInfo.CurrentCulture.CompareInfo.Compare(this, 0, this.Length, value, 0, value.Length, CompareOptions.None) == 0);

                case StringComparison.CurrentCultureIgnoreCase:
                    return (CultureInfo.CurrentCulture.CompareInfo.Compare(this, 0, this.Length, value, 0, value.Length, CompareOptions.IgnoreCase) == 0);

                case StringComparison.Ordinal:
                    if (this.Length != value.Length)
                        return false;
                    return EqualsHelper(this, value);

                case StringComparison.OrdinalIgnoreCase:
                    if (this.Length != value.Length)
                        return false;
                    else
                    {
                        return CompareInfo.CompareOrdinalIgnoreCase(this, 0, this.Length, value, 0, value.Length) == 0;
                    }

                case StringComparison.InvariantCulture:
                    return (CultureInfo.InvariantCulture.CompareInfo.Compare(this, value, CompareOptions.None) == 0);

                case StringComparison.InvariantCultureIgnoreCase:
                    return (CultureInfo.InvariantCulture.CompareInfo.Compare(this, value, CompareOptions.IgnoreCase) == 0);

                default:
                    throw new ArgumentException(SR.NotSupported_StringComparison, nameof(comparisonType));
            }
        }

        // Determines whether two Strings match.

        public static bool Equals(String a, String b)
        {
            if ((Object)a == (Object)b)
            {
                return true;
            }

            if ((Object)a == null || (Object)b == null || a.Length != b.Length)
            {
                return false;
            }

            return EqualsHelper(a, b);
        }

        public static bool Equals(String a, String b, StringComparison comparisonType)
        {
            if (comparisonType < StringComparison.CurrentCulture || comparisonType > StringComparison.OrdinalIgnoreCase)
                throw new ArgumentException(SR.NotSupported_StringComparison, nameof(comparisonType));

            if ((Object)a == (Object)b)
            {
                return true;
            }

            if ((Object)a == null || (Object)b == null)
            {
                return false;
            }

            switch (comparisonType)
            {
                case StringComparison.CurrentCulture:
                    return (CultureInfo.CurrentCulture.CompareInfo.Compare(a, 0, a.Length, b, 0, b.Length, CompareOptions.None) == 0);

                case StringComparison.CurrentCultureIgnoreCase:
                    return (CultureInfo.CurrentCulture.CompareInfo.Compare(a, 0, a.Length, b, 0, b.Length, CompareOptions.IgnoreCase) == 0);

                case StringComparison.Ordinal:
                    if (a.Length != b.Length)
                        return false;
                    return EqualsHelper(a, b);

                case StringComparison.OrdinalIgnoreCase:
                    if (a.Length != b.Length)
                        return false;
                    else
                    {
                        return CompareInfo.CompareOrdinalIgnoreCase(a, 0, a.Length, b, 0, b.Length) == 0;
                    }

                case StringComparison.InvariantCulture:
                    return (CultureInfo.InvariantCulture.CompareInfo.Compare(a, b, CompareOptions.None) == 0);

                case StringComparison.InvariantCultureIgnoreCase:
                    return (CultureInfo.InvariantCulture.CompareInfo.Compare(a, b, CompareOptions.IgnoreCase) == 0);

                default:
                    throw new ArgumentException(SR.NotSupported_StringComparison, nameof(comparisonType));
            }
        }

        public static bool operator ==(String a, String b)
        {
            if (Object.ReferenceEquals(a, b))
                return true;
            if (a == null || b == null || a.Length != b.Length)
                return false;
            return EqualsHelper(a, b);
        }

        public static bool operator !=(String a, String b)
        {
            if (Object.ReferenceEquals(a, b))
                return false;
            if (a == null || b == null || a.Length != b.Length)
                return true;
            return !EqualsHelper(a, b);
        }

        // Gets a hash code for this string.  If strings A and B are such that A.Equals(B), then
        // they will return the same hash code.
        public override int GetHashCode()
        {
            return Marvin.ComputeHash32(ref Unsafe.As<char, byte>(ref _firstChar), _stringLength * 2, Marvin.DefaultSeed);
        }

        // Gets a hash code for this string and this comparison. If strings A and B and comparison C are such
        // that String.Equals(A, B, C), then they will return the same hash code with this comparison C.
        public int GetHashCode(StringComparison comparisonType) => StringComparer.FromComparison(comparisonType).GetHashCode(this);

        // Use this if and only if you need the hashcode to not change across app domains (e.g. you have an app domain agile
        // hash table).
        internal int GetLegacyNonRandomizedHashCode()
        {
            unsafe
            {
                fixed (char* src = &_firstChar)
                {
                    Debug.Assert(src[this.Length] == '\0', "src[this.Length] == '\\0'");
                    Debug.Assert(((int)src) % 4 == 0, "Managed string should start at 4 bytes boundary");
#if BIT64
                    int hash1 = 5381;
#else // !BIT64 (32)
                    int hash1 = (5381<<16) + 5381;
#endif
                    int hash2 = hash1;

#if BIT64
                    int c;
                    char* s = src;
                    while ((c = s[0]) != 0)
                    {
                        hash1 = ((hash1 << 5) + hash1) ^ c;
                        c = s[1];
                        if (c == 0)
                            break;
                        hash2 = ((hash2 << 5) + hash2) ^ c;
                        s += 2;
                    }
#else // !BIT64 (32)
                    // 32 bit machines.
                    int* pint = (int *)src;
                    int len = this.Length;
                    while (len > 2)
                    {
                        hash1 = ((hash1 << 5) + hash1 + (hash1 >> 27)) ^ pint[0];
                        hash2 = ((hash2 << 5) + hash2 + (hash2 >> 27)) ^ pint[1];
                        pint += 2;
                        len  -= 4;
                    }

                    if (len > 0)
                    {
                        hash1 = ((hash1 << 5) + hash1 + (hash1 >> 27)) ^ pint[0];
                    }
#endif
                    return hash1 + (hash2 * 1566083941);
                }
            }
        }

        // Determines whether a specified string is a prefix of the current instance
        //
        public Boolean StartsWith(String value)
        {
            if ((Object)value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            return StartsWith(value, StringComparison.CurrentCulture);
        }

        public Boolean StartsWith(String value, StringComparison comparisonType)
        {
            if ((Object)value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (comparisonType < StringComparison.CurrentCulture || comparisonType > StringComparison.OrdinalIgnoreCase)
            {
                throw new ArgumentException(SR.NotSupported_StringComparison, nameof(comparisonType));
            }

            if ((Object)this == (Object)value)
            {
                return true;
            }

            if (value.Length == 0)
            {
                return true;
            }

            switch (comparisonType)
            {
                case StringComparison.CurrentCulture:
                    return CultureInfo.CurrentCulture.CompareInfo.IsPrefix(this, value, CompareOptions.None);

                case StringComparison.CurrentCultureIgnoreCase:
                    return CultureInfo.CurrentCulture.CompareInfo.IsPrefix(this, value, CompareOptions.IgnoreCase);

                case StringComparison.Ordinal:
                    if (this.Length < value.Length || _firstChar != value._firstChar)
                    {
                        return false;
                    }
                    return (value.Length == 1) ?
                            true :                 // First char is the same and thats all there is to compare  
                            StartsWithOrdinalHelper(this, value);

                case StringComparison.OrdinalIgnoreCase:
                    if (this.Length < value.Length)
                    {
                        return false;
                    }
                    return CompareInfo.CompareOrdinalIgnoreCase(this, 0, value.Length, value, 0, value.Length) == 0;

                case StringComparison.InvariantCulture:
                    return CultureInfo.InvariantCulture.CompareInfo.IsPrefix(this, value, CompareOptions.None);

                case StringComparison.InvariantCultureIgnoreCase:
                    return CultureInfo.InvariantCulture.CompareInfo.IsPrefix(this, value, CompareOptions.IgnoreCase);

                default:
                    throw new ArgumentException(SR.NotSupported_StringComparison, nameof(comparisonType));
            }
        }

        public Boolean StartsWith(String value, Boolean ignoreCase, CultureInfo culture)
        {
            if (null == value)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if ((object)this == (object)value)
            {
                return true;
            }

            CultureInfo referenceCulture;
            if (culture == null)
                referenceCulture = CultureInfo.CurrentCulture;
            else
                referenceCulture = culture;

            return referenceCulture.CompareInfo.IsPrefix(this, value, ignoreCase ? CompareOptions.IgnoreCase : CompareOptions.None);
        }

        public bool StartsWith(char value) => Length != 0 && _firstChar == value;
    }
}
