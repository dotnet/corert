// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
        private unsafe static bool OrdinalCompareEqualLengthStrings(String strA, String strB)
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

        private unsafe static bool StartsWithOrdinalHelper(String str, String startsWith)
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

        private unsafe static int CompareOrdinalHelper(String strA, String strB)
        {
            Contract.Requires(strA != null);
            Contract.Requires(strB != null);

            // NOTE: This may be subject to change if eliminating the check
            // in the callers makes them small enough to be inlined
            Contract.Assert(strA._firstChar == strB._firstChar,
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
                Contract.Assert(*(a + 1) != *(b + 1), "This char must be different if we reach here!");
                return *(a + 1) - *(b + 1);
            }
        }

        internal unsafe static int CompareOrdinalHelper(string strA, int indexA, int countA, string strB, int indexB, int countB)
        {
            // Argument validation should be handled by callers.
            Contract.Assert(strA != null && strB != null);
            Contract.Assert(indexA >= 0 && indexB >= 0);
            Contract.Assert(countA >= 0 && countB >= 0);
            Contract.Assert(countA <= strA.Length - indexA);
            Contract.Assert(countB <= strB.Length - indexB);

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
                throw new ArgumentException(SR.NotSupported_StringComparison, "comparisonType");
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
                    return FormatProvider.Compare(strA, 0, strA.Length, strB, 0, strB.Length);

                case StringComparison.CurrentCultureIgnoreCase:
                    return FormatProvider.CompareIgnoreCase(strA, 0, strA.Length, strB, 0, strB.Length);

                case StringComparison.Ordinal:
                    // Most common case: first character is different.
                    // Returns false for empty strings.
                    if (strA._firstChar != strB._firstChar)
                    {
                        return strA._firstChar - strB._firstChar;
                    }

                    return CompareOrdinalHelper(strA, strB);

                case StringComparison.OrdinalIgnoreCase:
                    return FormatProvider.CompareOrdinalIgnoreCase(strA, 0, strA.Length, strB, 0, strB.Length);

                default:
                    throw new NotSupportedException(SR.NotSupported_StringComparison);
            }
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
            
            return ignoreCase ?
                FormatProvider.CompareIgnoreCase(strA, indexA, lengthA, strB, indexB, lengthB) :
                FormatProvider.Compare(strA, indexA, lengthA, strB, indexB, lengthB);
        }

        public static int Compare(String strA, int indexA, String strB, int indexB, int length, StringComparison comparisonType)
        {
            if (comparisonType < StringComparison.CurrentCulture || comparisonType > StringComparison.OrdinalIgnoreCase)
            {
                throw new ArgumentException(SR.NotSupported_StringComparison, "comparisonType");
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
                throw new ArgumentOutOfRangeException("length", SR.ArgumentOutOfRange_NegativeLength);
            }

            if (indexA < 0 || indexB < 0)
            {
                string paramName = indexA < 0 ? "indexA" : "indexB";
                throw new ArgumentOutOfRangeException(paramName, SR.ArgumentOutOfRange_Index);
            }

            if (strA.Length - indexA < 0 || strB.Length - indexB < 0)
            {
                string paramName = strA.Length - indexA < 0 ? "indexA" : "indexB";
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
                    return FormatProvider.Compare(strA, indexA, lengthA, strB, indexB, lengthB);

                case StringComparison.CurrentCultureIgnoreCase:
                    return FormatProvider.CompareIgnoreCase(strA, indexA, lengthA, strB, indexB, lengthB);

                case StringComparison.Ordinal:
                    return CompareOrdinalHelper(strA, indexA, lengthA, strB, indexB, lengthB);

                case StringComparison.OrdinalIgnoreCase:
                    return FormatProvider.CompareOrdinalIgnoreCase(strA, indexA, lengthA, strB, indexB, lengthB);

                default:
                    throw new ArgumentException(SR.NotSupported_StringComparison);
            }
        }

        // Compares this String to another String (cast as object), returning an integer that
        // indicates the relationship. This method returns a value less than 0 if this is less than value, 0
        // if this is equal to value, or a value greater than 0 if this is greater than value.
        //

        int IComparable.CompareTo(Object value)
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
    }
}
