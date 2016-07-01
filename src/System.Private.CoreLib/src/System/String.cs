// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*============================================================
**
** Purpose: Your favorite String class.  Native methods 
** are implemented in StringNative.cpp
**
===========================================================*/

using System.Text;
using System.Runtime;
using System.Diagnostics;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Diagnostics.Contracts;
using System.Security;

namespace System
{
    //
    // The String class represents a static string of characters.  Many of
    // the String methods perform some type of transformation on the current
    // instance and return the result as a new String. All comparison methods are
    // implemented as a part of String.  As with arrays, character positions
    // (indices) are zero-based.
    //
    // When passing a null string into a constructor in VJ and VC, the null should be
    // explicitly type cast to a String.
    // For Example:
    // String s = new String((String)null);
    // Console.WriteLine(s);
    //

    // STRING LAYOUT
    // -------------
    // Strings are null-terminated for easy interop with native, but the value returned by String.Length 
    // does NOT include this null character in its count.  As a result, there's some trickiness here in the 
    // layout and allocation of strings that needs explanation...
    //
    // String is allocated like any other array, using the RhNewArray API.  It is essentially a very special 
    // char[] object.  In order to be an array, the String EEType must have an 'array element size' of 2, 
    // which is setup by a special case in the binder.  Strings must also have a typical array instance 
    // layout, which means that the first field after the m_pEEType field is the 'number of array elements' 
    // field.  However, here, it is called _stringLength because it contains the number of characters in the
    // string (NOT including the terminating null element) and, thus, directly represents both the array 
    // length and String.Length.
    //
    // As with all arrays, the GC calculates the size of an object using the following formula:  
    //
    //      obj_size = align(base_size + (num_elements * element_size), sizeof(void*))
    //
    // The values 'base_size' and 'element_size' are both stored in the EEType for String and 'num_elements'
    // is _stringLength.
    //
    // Our base_size is the size of the fixed portion of the string defined below.  It, therefore, contains 
    // the size of the _firstChar field in it.  This means that, since our string data actually starts 
    // inside the fixed 'base_size' area, and our num_elements is equal to String.Length, we end up with one 
    // extra character at the end.  This is how we get our extra null terminator which allows us to pass a 
    // pinned string out to native code as a null-terminated string.  This is also why we don't increment the
    // requested string length by one before passing it to RhNewArray.  There is no need to allocate an extra
    // array element, it is already allocated here in the fixed layout of the String.
    //
    // Typically, the base_size of an array type is aligned up to the nearest pointer size multiple (so that
    // array elements start out aligned in case they need alignment themselves), but we don't want to do that 
    // with String because we are allocating String.Length components with RhNewArray and the overall object 
    // size will then need another alignment, resulting in wasted space.  So the binder specially shrinks the
    // base_size of String, leaving it unaligned in order to allow the use of that otherwise wasted space.  
    //
    // One more note on base_size -- on 64-bit, the base_size ends up being 22 bytes, which is less than the
    // min_obj_size of (3 * sizeof(void*)).  This is OK because our array allocator will still align up the
    // overall object size, so a 0-length string will end up with an object size of 24 bytes, which meets the
    // min_obj_size requirement.
    //
    // NOTE: This class is marked EagerStaticClassConstruction because McgCurrentModule class being eagerly
    // constructed itself depends on this class also being eagerly constructed. Plus, it's nice to have this
    // eagerly constructed to avoid the cost of defered ctors. I can't imagine any app that doesn't use string
    //
    [ComVisible(true)]
    [StructLayout(LayoutKind.Sequential)]
    [System.Runtime.CompilerServices.EagerStaticClassConstructionAttribute]
    public sealed class String : IComparable, IEnumerable, IEnumerable<char>, IComparable<String>, IEquatable<String>, IConvertible
    {
#if BIT64
        private const int POINTER_SIZE = 8;
#else
        private const int POINTER_SIZE = 4;
#endif
        //                                        m_pEEType    + _stringLength
        internal const int FIRST_CHAR_OFFSET = POINTER_SIZE + sizeof(int);

        // CS0169: The private field '{blah}' is never used
        // CS0649: Field '{blah}' is never assigned to, and will always have its default value
#pragma warning disable 169, 649

#if !CORERT
        [Bound]
#endif
        // WARNING: We allow diagnostic tools to directly inspect these two members (_stringLength, _firstChar)
        // See https://github.com/dotnet/corert/blob/master/Documentation/design-docs/diagnostics/diagnostics-tools-contract.md for more details. 
        // Please do not change the type, the name, or the semantic usage of this member without understanding the implication for tools. 
        // Get in touch with the diagnostics team if you have questions.
        private int _stringLength;
        private char _firstChar;

#pragma warning restore

        // String constructors
        // These are special. the implementation methods for these have a different signature from the
        // declared constructors. We use a RuntimeImport/RuntimeExport combination to workaround this difference.
        // TODO: Determine a more reasonable solution for this.

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
#if CORERT
        public extern String(char[] value);   // CtorCharArray

        private static String Ctor(object unusedThis, char[] value)
#else
        [RuntimeImport(".", "CtorCharArray")]
        public extern String(char[] value);   // CtorCharArray

        [RuntimeExport("CtorCharArray")]
        private static String CtorCharArray(char[] value)
#endif
        {
            if (value != null && value.Length != 0)
            {
                String result = FastAllocateString(value.Length);

                unsafe
                {
                    fixed (char* dest = &result._firstChar, source = value)
                    {
                        wstrcpy(dest, source, value.Length);
                    }
                }
                return result;
            }
            else
                return String.Empty;
        }

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
#if CORERT
        public extern String(char[] value, int startIndex, int length);   // CtorCharArrayStartLength

        private static String Ctor(object unusedThis, char[] value, int startIndex, int length)
#else
        [RuntimeImport(".", "CtorCharArrayStartLength")]
        public extern String(char[] value, int startIndex, int length);   // CtorCharArrayStartLength

        [RuntimeExport("CtorCharArrayStartLength")]
        private static String CtorCharArrayStartLength(char[] value, int startIndex, int length)
#endif
        {
            if (value == null)
                throw new ArgumentNullException("value");

            if (startIndex < 0)
                throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_StartIndex);

            if (length < 0)
                throw new ArgumentOutOfRangeException("length", SR.ArgumentOutOfRange_NegativeLength);

            if (startIndex > value.Length - length)
                throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_Index);
            Contract.EndContractBlock();

            if (length > 0)
            {
                String result = FastAllocateString(length);

                unsafe
                {
                    fixed (char* dest = &result._firstChar, source = value)
                    {
                        wstrcpy(dest, source + startIndex, length);
                    }
                }
                return result;
            }
            else
                return String.Empty;
        }

        [CLSCompliant(false)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
#if CORERT
        unsafe public extern String(char* value);   // CtorCharPtr

        private static unsafe String Ctor(object unusedThis, char* ptr)
#else
        [RuntimeImport(".", "CtorCharPtr")]
        unsafe public extern String(char* value);   // CtorCharPtr

        [RuntimeExport("CtorCharPtr")]
        private static unsafe String CtorCharPtr(char* ptr)
#endif
        {
            if (ptr == null)
                return String.Empty;

#if !FEATURE_PAL
            if (ptr < (char*)64000)
                throw new ArgumentException(SR.Arg_MustBeStringPtrNotAtom);
#endif // FEATURE_PAL

            try
            {
                int count = wcslen(ptr);
                if (count == 0)
                    return String.Empty;

                String result = FastAllocateString(count);
                fixed (char* dest = &result._firstChar)
                    wstrcpy(dest, ptr, count);
                return result;
            }
            catch (NullReferenceException)
            {
                throw new ArgumentOutOfRangeException("ptr", SR.ArgumentOutOfRange_PartialWCHAR);
            }
        }

        [CLSCompliant(false)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
#if CORERT
        unsafe public extern String(char* value, int startIndex, int length);   // CtorCharPtrStartLength

        private static unsafe String Ctor(object unusedThis, char* ptr, int startIndex, int length)
#else
        [RuntimeImport(".", "CtorCharPtrStartLength")]
        unsafe public extern String(char* value, int startIndex, int length);   // CtorCharPtrStartLength

        [RuntimeExport("CtorCharPtrStartLength")]
        private static unsafe String CtorCharPtrStartLength(char* ptr, int startIndex, int length)
#endif
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException("length", SR.ArgumentOutOfRange_NegativeLength);
            }

            if (startIndex < 0)
            {
                throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_StartIndex);
            }
            Contract.EndContractBlock();

            char* pFrom = ptr + startIndex;
            if (pFrom < ptr)
            {
                // This means that the pointer operation has had an overflow
                throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_PartialWCHAR);
            }

            if (length == 0)
                return String.Empty;

            String result = FastAllocateString(length);

            try
            {
                fixed (char* dest = &result._firstChar)
                    wstrcpy(dest, pFrom, length);
                return result;
            }
            catch (NullReferenceException)
            {
                throw new ArgumentOutOfRangeException("ptr", SR.ArgumentOutOfRange_PartialWCHAR);
            }
        }

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
#if CORERT
        public extern String(char c, int count);                          // CtorCharCount

        private static String Ctor(object unusedThis, char c, int count)
#else
        [RuntimeImport(".", "CtorCharCount")]
        public extern String(char c, int count);                          // CtorCharCount

        [RuntimeExport("CtorCharCount")]
        private static String CtorCharCount(char c, int count)
#endif
        {
            if (count > 0)
            {
                String result = FastAllocateString(count);

                if (c == '\0')
                    return result;                                       // Fast path null char string

                unsafe
                {
                    fixed (char* dest = &result._firstChar)
                    {
                        uint cc = (uint)((c << 16) | c);
                        uint* dmem = (uint*)dest;
                        if (count >= 4)
                        {
                            count -= 4;
                            do
                            {
                                dmem[0] = cc;
                                dmem[1] = cc;
                                dmem += 2;
                                count -= 4;
                            } while (count >= 0);
                        }
                        if ((count & 2) != 0)
                        {
                            *dmem = cc;
                            dmem++;
                        }
                        if ((count & 1) != 0)
                            ((char*)dmem)[0] = c;
                    }
                }
                return result;
            }
            else if (count == 0)
                return String.Empty;
            else
                throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_NegativeCount);
        }

        private const int TrimHead = 0;
        private const int TrimTail = 1;
        private const int TrimBoth = 2;

        public static readonly String Empty = "";

        //
        //Native Static Methods
        //

        // Joins an array of strings together as one string with a separator between each original string.
        //
        public static String Join(String separator, params String[] value)
        {
            if (value == null)
                throw new ArgumentNullException("value");
            return Join(separator, value, 0, value.Length);
        }

        public static String Join(String separator, params Object[] values)
        {
            if (values == null)
                throw new ArgumentNullException("values");

            if (values.Length == 0 || values[0] == null)
                return String.Empty;

            StringBuilder result = StringBuilderCache.Acquire();

            result.Append(values[0].ToString());

            for (int i = 1; i < values.Length; i++)
            {
                result.Append(separator);
                if (values[i] != null)
                {
                    result.Append(values[i].ToString());
                }
            }
            return StringBuilderCache.GetStringAndRelease(result);
        }

        public static String Join<T>(String separator, IEnumerable<T> values)
        {
            if (values == null)
                throw new ArgumentNullException("values");

            using (IEnumerator<T> en = values.GetEnumerator())
            {
                if (!en.MoveNext())
                    return String.Empty;

                StringBuilder result = StringBuilderCache.Acquire();
                T currentValue = en.Current;

                if (currentValue != null)
                {
                    result.Append(currentValue.ToString());
                }

                while (en.MoveNext())
                {
                    currentValue = en.Current;

                    result.Append(separator);
                    if (currentValue != null)
                    {
                        result.Append(currentValue.ToString());
                    }
                }
                return StringBuilderCache.GetStringAndRelease(result);
            }
        }

        public static String Join(String separator, IEnumerable<String> values)
        {
            if (values == null)
                throw new ArgumentNullException("values");

            using (IEnumerator<String> en = values.GetEnumerator())
            {
                if (!en.MoveNext())
                    return String.Empty;

                String firstValue = en.Current;

                if (!en.MoveNext())
                {
                    // Only one value available
                    return firstValue ?? String.Empty;
                }

                // Null separator and values are handled by the StringBuilder
                StringBuilder result = StringBuilderCache.Acquire();
                result.Append(firstValue);

                do
                {
                    result.Append(separator);
                    result.Append(en.Current);
                } while (en.MoveNext());
                return StringBuilderCache.GetStringAndRelease(result);
            }
        }

        // Joins an array of strings together as one string with a separator between each original string.
        //
        public unsafe static String Join(String separator, String[] value, int startIndex, int count)
        {
            //Range check the array
            if (value == null)
                throw new ArgumentNullException("value");

            if (startIndex < 0)
                throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_StartIndex);
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_NegativeCount);

            if (startIndex > value.Length - count)
                throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_IndexCountBuffer);

            //Treat null as empty string.
            if (separator == null)
            {
                separator = String.Empty;
            }

            //If count is 0, that skews a whole bunch of the calculations below, so just special case that.
            if (count == 0)
            {
                return String.Empty;
            }

            if (count == 1)
            {
                return value[startIndex] ?? String.Empty;
            }

            int endIndex = startIndex + count - 1;
            StringBuilder result = StringBuilderCache.Acquire();
            // Append the first string first and then append each following string prefixed by the separator.
            result.Append(value[startIndex]);
            for (int stringToJoinIndex = startIndex + 1; stringToJoinIndex <= endIndex; stringToJoinIndex++)
            {
                result.Append(separator);
                result.Append(value[stringToJoinIndex]);
            }

            return StringBuilderCache.GetStringAndRelease(result);
        }

        internal static unsafe int nativeCompareOrdinalEx(String strA, int indexA, String strB, int indexB, int count)
        {
            // If any of our indices are negative throw an exception.
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_NegativeCount);
            if (indexA < 0)
                throw new ArgumentOutOfRangeException("indexA", SR.ArgumentOutOfRange_Index);
            if (indexB < 0)
                throw new ArgumentOutOfRangeException("indexB", SR.ArgumentOutOfRange_Index);

            int countA = count;
            int countB = count;

            // Do a lot of range checking to make sure that everything is kosher and legit.
            if (count > (strA.Length - indexA))
            {
                countA = strA.Length - indexA;
                if (countA < 0)
                    throw new ArgumentOutOfRangeException("indexA", SR.ArgumentOutOfRange_Index);
            }

            if (count > (strB.Length - indexB))
            {
                countB = strB.Length - indexB;
                if (countB < 0)
                    throw new ArgumentOutOfRangeException("indexB", SR.ArgumentOutOfRange_Index);
            }

            // Set up the loop variables.
            fixed (char* pStrA = &strA._firstChar, pStrB = &strB._firstChar)
            {
                char* strAChars = pStrA + indexA;
                char* strBChars = pStrB + indexB;
                return FastCompareStringHelper((uint*)strAChars, countA, (uint*)strBChars, countB);
            }
        }

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
            int length = Math.Min(strA.Length, strB.Length);
            int diffOffset = -1;

            fixed (char* ap = &strA._firstChar) fixed (char* bp = &strB._firstChar)
            {
                char* a = ap;
                char* b = bp;

                // unroll the loop
                while (length >= 10)
                {
                    if (*(int*)a != *(int*)b)
                    {
                        diffOffset = 0;
                        break;
                    }

                    if (*(int*)(a + 2) != *(int*)(b + 2))
                    {
                        diffOffset = 2;
                        break;
                    }

                    if (*(int*)(a + 4) != *(int*)(b + 4))
                    {
                        diffOffset = 4;
                        break;
                    }

                    if (*(int*)(a + 6) != *(int*)(b + 6))
                    {
                        diffOffset = 6;
                        break;
                    }

                    if (*(int*)(a + 8) != *(int*)(b + 8))
                    {
                        diffOffset = 8;
                        break;
                    }
                    length -= 10;
                    a += 10;
                    b += 10;
                }

                if (diffOffset != -1)
                {
                    // we already see a difference in the unrolled loop above
                    a += diffOffset;
                    b += diffOffset;
                    int order;
                    if ((order = (int)*a - (int)*b) != 0)
                    {
                        return order;
                    }
                    return ((int)*(a + 1) - (int)*(b + 1));
                }

                // now go back to slower code path and do comparison on 4 bytes at a time.  
                // This depends on the fact that the String objects are  
                // always zero terminated and that the terminating zero is not included  
                // in the length. For odd string sizes, the last compare will include  
                // the zero terminator.  

                while (length > 0)
                {
                    if (*(int*)a != *(int*)b)
                    {
                        break;
                    }
                    length -= 2;
                    a += 2;
                    b += 2;
                }

                if (length > 0)
                {
                    int c;
                    // found a different int on above loop
                    if ((c = (int)*a - (int)*b) != 0)
                    {
                        return c;
                    }
                    return ((int)*(a + 1) - (int)*(b + 1));
                }

                // At this point, we have compared all the characters in at least one string.
                // The longer string will be larger.
                return strA.Length - strB.Length;
            }
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

            return OrdinalCompareEqualLengthStrings(this, str);
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

            return OrdinalCompareEqualLengthStrings(this, value);
        }


        public bool Equals(String value, StringComparison comparisonType)
        {
            if (comparisonType < StringComparison.CurrentCulture || comparisonType > StringComparison.OrdinalIgnoreCase)
                throw new ArgumentException(SR.NotSupported_StringComparison, "comparisonType");

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
                    return (FormatProvider.Compare(this, 0, this.Length, value, 0, value.Length) == 0);

                case StringComparison.CurrentCultureIgnoreCase:
                    return (FormatProvider.CompareIgnoreCase(this, 0, this.Length, value, 0, value.Length) == 0);

                case StringComparison.Ordinal:
                    if (this.Length != value.Length)
                        return false;
                    return OrdinalCompareEqualLengthStrings(this, value);

                case StringComparison.OrdinalIgnoreCase:
                    if (this.Length != value.Length)
                        return false;
                    else
                    {
                        return FormatProvider.CompareOrdinalIgnoreCase(this, 0, this.Length, value, 0, value.Length) == 0;
                    }

                default:
                    throw new ArgumentException(SR.NotSupported_StringComparison, "comparisonType");
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

            return OrdinalCompareEqualLengthStrings(a, b);
        }

        public static bool Equals(String a, String b, StringComparison comparisonType)
        {
            if (comparisonType < StringComparison.CurrentCulture || comparisonType > StringComparison.OrdinalIgnoreCase)
                throw new ArgumentException(SR.NotSupported_StringComparison, "comparisonType");

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
                    return (FormatProvider.Compare(a, 0, a.Length, b, 0, b.Length) == 0);

                case StringComparison.CurrentCultureIgnoreCase:
                    return (FormatProvider.CompareIgnoreCase(a, 0, a.Length, b, 0, b.Length) == 0);

                case StringComparison.Ordinal:
                    if (a.Length != b.Length)
                        return false;
                    return OrdinalCompareEqualLengthStrings(a, b);

                case StringComparison.OrdinalIgnoreCase:
                    if (a.Length != b.Length)
                        return false;
                    else
                    {
                        return FormatProvider.CompareOrdinalIgnoreCase(a, 0, a.Length, b, 0, b.Length) == 0;
                    }
                default:
                    throw new ArgumentException(SR.NotSupported_StringComparison, "comparisonType");
            }
        }

        public static bool operator ==(String a, String b)
        {
            if (Object.ReferenceEquals(a, b))
                return true;
            if (a == null || b == null || a.Length != b.Length)
                return false;
            return OrdinalCompareEqualLengthStrings(a, b);
        }

        public static bool operator !=(String a, String b)
        {
            if (Object.ReferenceEquals(a, b))
                return false;
            if (a == null || b == null || a.Length != b.Length)
                return true;
            return !OrdinalCompareEqualLengthStrings(a, b);
        }

        // Gets the character at a specified position.
        //
        // Spec#: Apply the precondition here using a contract assembly.  Potential perf issue.
        [System.Runtime.CompilerServices.IndexerName("Chars")]
        public unsafe char this[int index]
        {
            [NonVersionable]
#if CORERT
            [Intrinsic]
            get
            {
                if ((uint)index >= _stringLength)
                    throw new IndexOutOfRangeException();
                fixed (char* s = &_firstChar)
                    return s[index];
            }
#else
            [BoundsChecking]
            get
            {
                System.Runtime.CompilerServices.ByReference<char> mgdPtr = System.Runtime.CompilerServices.ByReference<char>.FromRef(ref _firstChar);
                return System.Runtime.CompilerServices.ByReference<char>.LoadAtIndex(mgdPtr, index);
            }
#endif
        }

        // Converts a substring of this string to an array of characters.  Copies the
        // characters of this string beginning at position sourceIndex and ending at
        // sourceIndex + count - 1 to the character array buffer, beginning
        // at destinationIndex.
        //
        unsafe public void CopyTo(int sourceIndex, char[] destination, int destinationIndex, int count)
        {
            if (destination == null)
                throw new ArgumentNullException("destination");
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_NegativeCount);
            if (sourceIndex < 0)
                throw new ArgumentOutOfRangeException("sourceIndex", SR.ArgumentOutOfRange_Index);
            if (count > Length - sourceIndex)
                throw new ArgumentOutOfRangeException("sourceIndex", SR.ArgumentOutOfRange_IndexCount);
            if (destinationIndex > destination.Length - count || destinationIndex < 0)
                throw new ArgumentOutOfRangeException("destinationIndex", SR.ArgumentOutOfRange_IndexCount);

            // Note: fixed does not like empty arrays
            if (count > 0)
            {
                fixed (char* src = &_firstChar)
                    fixed (char* dest = destination)
                        wstrcpy(dest + destinationIndex, src + sourceIndex, count);
            }
        }

        // Returns the entire string as an array of characters.
        unsafe public char[] ToCharArray()
        {
            // Huge performance improvement for short strings by doing this.
            int length = Length;
            if (length > 0)
            {
                char[] chars = new char[length];
                fixed (char* src = &_firstChar)
                    fixed (char* dest = chars)
                {
                    wstrcpy(dest, src, length);
                }
                return chars;
            }
            return Array.Empty<char>();
        }

        // Returns a substring of this string as an array of characters.
        //
        unsafe public char[] ToCharArray(int startIndex, int length)
        {
            // Range check everything.
            if (startIndex < 0 || startIndex > Length || startIndex > Length - length)
                throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_Index);
            if (length < 0)
                throw new ArgumentOutOfRangeException("length", SR.ArgumentOutOfRange_Index);

            if (length > 0)
            {
                char[] chars = new char[length];
                fixed (char* src = &_firstChar)
                    fixed (char* dest = chars)
                {
                    wstrcpy(dest, src + startIndex, length);
                }
                return chars;
            }
            return Array.Empty<char>();
        }

        public static bool IsNullOrEmpty(String value)
        {
            return (value == null || value.Length == 0);
        }

        public static bool IsNullOrWhiteSpace(String value)
        {
            if (value == null) return true;

            for (int i = 0; i < value.Length; i++)
            {
                if (!Char.IsWhiteSpace(value[i])) return false;
            }

            return true;
        }

        // Gets a hash code for this string.  If strings A and B are such that A.Equals(B), then
        // they will return the same hash code.
        public override int GetHashCode()
        {
            unsafe
            {
                fixed (char* src = &_firstChar)
                {
#if BIT64
                    int hash1 = 5381;
#else
                    int hash1 = (5381 << 16) + 5381;
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
#else
                    // 32bit machines.
                    int* pint = (int*)src;
                    int len = this.Length;
                    while (len > 0)
                    {
                        hash1 = ((hash1 << 5) + hash1 + (hash1 >> 27)) ^ pint[0];
                        if (len <= 2)
                        {
                            break;
                        }
                        hash2 = ((hash2 << 5) + hash2 + (hash2 >> 27)) ^ pint[1];
                        pint += 2;
                        len -= 4;
                    }
#endif
                    return hash1 + (hash2 * 1566083941);
                }
            }
        }

        // Gets the length of this string
        //
        /// This is a EE implemented function so that the JIT can recognise is specially
        /// and eliminate checks on character fetchs in a loop like:
        ///        for(int i = 0; i < str.Length; i++) str[i]  
        /// The actually code generated for this will be one instruction and will be inlined.
        //
        // Spec#: Add postcondition in a contract assembly.  Potential perf problem.
        public int Length
        {
            get { return _stringLength; }
        }

        // Creates an array of strings by splitting this string at each
        // occurrence of a separator.  The separator is searched for, and if found,
        // the substring preceding the occurrence is stored as the first element in
        // the array of strings.  We then continue in this manner by searching
        // the substring that follows the occurrence.  On the other hand, if the separator
        // is not found, the array of strings will contain this instance as its only element.
        // If the separator is null
        // whitespace (i.e., Character.IsWhitespace) is used as the separator.
        //
        public String[] Split(params char[] separator)
        {
            return Split(separator, Int32.MaxValue, StringSplitOptions.None);
        }

        // Creates an array of strings by splitting this string at each
        // occurrence of a separator.  The separator is searched for, and if found,
        // the substring preceding the occurrence is stored as the first element in
        // the array of strings.  We then continue in this manner by searching
        // the substring that follows the occurrence.  On the other hand, if the separator
        // is not found, the array of strings will contain this instance as its only element.
        // If the separator is the empty string (i.e., String.Empty), then
        // whitespace (i.e., Character.IsWhitespace) is used as the separator.
        // If there are more than count different strings, the last n-(count-1)
        // elements are concatenated and added as the last String.
        //
        public string[] Split(char[] separator, int count)
        {
            return Split(separator, count, StringSplitOptions.None);
        }

        public String[] Split(char[] separator, StringSplitOptions options)
        {
            return Split(separator, Int32.MaxValue, options);
        }

        public String[] Split(char[] separator, int count, StringSplitOptions options)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException("count",
                    SR.ArgumentOutOfRange_NegativeCount);

            if (options < StringSplitOptions.None || options > StringSplitOptions.RemoveEmptyEntries)
                throw new ArgumentException(SR.Format(SR.Arg_EnumIllegalVal, options));

            bool omitEmptyEntries = (options == StringSplitOptions.RemoveEmptyEntries);

            if ((count == 0) || (omitEmptyEntries && this.Length == 0))
            {
                return Array.Empty<String>();
            }

            if (count == 1)
            {
                return new String[] { this };
            }

            int[] sepList = new int[Length];
            int numReplaces = MakeSeparatorList(separator, sepList);

            // Handle the special case of no replaces.
            if (0 == numReplaces)
            {
                return new String[] { this };
            }

            if (omitEmptyEntries)
            {
                return SplitOmitEmptyEntries(sepList, null, numReplaces, count);
            }
            else
            {
                return SplitKeepEmptyEntries(sepList, null, numReplaces, count);
            }
        }

        public String[] Split(String[] separator, StringSplitOptions options)
        {
            return Split(separator, Int32.MaxValue, options);
        }

        public String[] Split(String[] separator, Int32 count, StringSplitOptions options)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count",
                    SR.ArgumentOutOfRange_NegativeCount);
            }

            if (options < StringSplitOptions.None || options > StringSplitOptions.RemoveEmptyEntries)
            {
                throw new ArgumentException(SR.Format(SR.Arg_EnumIllegalVal, (int)options));
            }

            bool omitEmptyEntries = (options == StringSplitOptions.RemoveEmptyEntries);

            if (separator == null || separator.Length == 0)
            {
                return Split((char[])null, count, options);
            }

            if ((count == 0) || (omitEmptyEntries && this.Length == 0))
            {
                return Array.Empty<String>();
            }

            if (count == 1)
            {
                return new String[] { this };
            }

            int[] sepList = new int[Length];
            int[] lengthList = new int[Length];
            int numReplaces = MakeSeparatorList(separator, sepList, lengthList);

            //Handle the special case of no replaces.
            if (0 == numReplaces)
            {
                return new String[] { this };
            }

            if (omitEmptyEntries)
            {
                return SplitOmitEmptyEntries(sepList, lengthList, numReplaces, count);
            }
            else
            {
                return SplitKeepEmptyEntries(sepList, lengthList, numReplaces, count);
            }
        }

        // Note a special case in this function:
        //     If there is no separator in the string, a string array which only contains 
        //     the original string will be returned regardless of the count. 
        //

        private String[] SplitKeepEmptyEntries(Int32[] sepList, Int32[] lengthList, Int32 numReplaces, int count)
        {
            int currIndex = 0;
            int arrIndex = 0;

            count--;
            int numActualReplaces = (numReplaces < count) ? numReplaces : count;

            //Allocate space for the new array.
            //+1 for the string from the end of the last replace to the end of the String.
            String[] splitStrings = new String[numActualReplaces + 1];

            for (int i = 0; i < numActualReplaces && currIndex < Length; i++)
            {
                splitStrings[arrIndex++] = Substring(currIndex, sepList[i] - currIndex);
                currIndex = sepList[i] + ((lengthList == null) ? 1 : lengthList[i]);
            }

            //Handle the last string at the end of the array if there is one.
            if (currIndex < Length && numActualReplaces >= 0)
            {
                splitStrings[arrIndex] = Substring(currIndex);
            }
            else if (arrIndex == numActualReplaces)
            {
                //We had a separator character at the end of a string.  Rather than just allowing
                //a null character, we'll replace the last element in the array with an empty string.
                splitStrings[arrIndex] = String.Empty;
            }

            return splitStrings;
        }


        // This function will not keep the Empty String 
        private String[] SplitOmitEmptyEntries(Int32[] sepList, Int32[] lengthList, Int32 numReplaces, int count)
        {
            // Allocate array to hold items. This array may not be 
            // filled completely in this function, we will create a 
            // new array and copy string references to that new array.

            int maxItems = (numReplaces < count) ? (numReplaces + 1) : count;
            String[] splitStrings = new String[maxItems];

            int currIndex = 0;
            int arrIndex = 0;

            for (int i = 0; i < numReplaces && currIndex < Length; i++)
            {
                if (sepList[i] - currIndex > 0)
                {
                    splitStrings[arrIndex++] = Substring(currIndex, sepList[i] - currIndex);
                }
                currIndex = sepList[i] + ((lengthList == null) ? 1 : lengthList[i]);
                if (arrIndex == count - 1)
                {
                    // If all the remaining entries at the end are empty, skip them
                    while (i < numReplaces - 1 && currIndex == sepList[++i])
                    {
                        currIndex += ((lengthList == null) ? 1 : lengthList[i]);
                    }
                    break;
                }
            }

            //Handle the last string at the end of the array if there is one.
            if (currIndex < Length)
            {
                splitStrings[arrIndex++] = Substring(currIndex);
            }

            String[] stringArray = splitStrings;
            if (arrIndex != maxItems)
            {
                stringArray = new String[arrIndex];
                for (int j = 0; j < arrIndex; j++)
                {
                    stringArray[j] = splitStrings[j];
                }
            }
            return stringArray;
        }

        //--------------------------------------------------------------------    
        // This function returns the number of the places within this instance where 
        // characters in Separator occur.
        // Args: separator  -- A string containing all of the split characters.
        //       sepList    -- an array of ints for split char indicies.
        //--------------------------------------------------------------------    
        private unsafe int MakeSeparatorList(char[] separator, int[] sepList)
        {
            int foundCount = 0;

            if (separator == null || separator.Length == 0)
            {
                fixed (char* pwzChars = &_firstChar)
                {
                    //If they passed null or an empty string, look for whitespace.
                    for (int i = 0; i < Length && foundCount < sepList.Length; i++)
                    {
                        if (Char.IsWhiteSpace(pwzChars[i]))
                        {
                            sepList[foundCount++] = i;
                        }
                    }
                }
            }
            else
            {
                int sepListCount = sepList.Length;
                int sepCount = separator.Length;
                //If they passed in a string of chars, actually look for those chars.
                fixed (char* pwzChars = &_firstChar, pSepChars = separator)
                {
                    for (int i = 0; i < Length && foundCount < sepListCount; i++)
                    {
                        char* pSep = pSepChars;
                        for (int j = 0; j < sepCount; j++, pSep++)
                        {
                            if (pwzChars[i] == *pSep)
                            {
                                sepList[foundCount++] = i;
                                break;
                            }
                        }
                    }
                }
            }
            return foundCount;
        }

        //--------------------------------------------------------------------    
        // This function returns the number of the places within this instance where 
        // instances of separator strings occur.
        // Args: separators -- An array containing all of the split strings.
        //       sepList    -- an array of ints for split string indicies.
        //       lengthList -- an array of ints for split string lengths.
        //--------------------------------------------------------------------    
        private unsafe int MakeSeparatorList(String[] separators, int[] sepList, int[] lengthList)
        {
            int foundCount = 0;
            int sepListCount = sepList.Length;
            int sepCount = separators.Length;

            fixed (char* pwzChars = &_firstChar)
            {
                for (int i = 0; i < Length && foundCount < sepListCount; i++)
                {
                    for (int j = 0; j < separators.Length; j++)
                    {
                        String separator = separators[j];
                        if (String.IsNullOrEmpty(separator))
                        {
                            continue;
                        }
                        Int32 currentSepLength = separator.Length;
                        if (pwzChars[i] == separator[0] && currentSepLength <= Length - i)
                        {
                            if (currentSepLength == 1
                                || String.CompareOrdinal(this, i, separator, 0, currentSepLength) == 0)
                            {
                                sepList[foundCount] = i;
                                lengthList[foundCount] = currentSepLength;
                                foundCount++;
                                i += currentSepLength - 1;
                                break;
                            }
                        }
                    }
                }
            }
            return foundCount;
        }

        // Returns a substring of this string.
        //
        public String Substring(int startIndex)
        {
            return this.Substring(startIndex, Length - startIndex);
        }

        // Returns a substring of this string.
        //
        public String Substring(int startIndex, int length)
        {
            //Bounds Checking.
            if (startIndex < 0)
            {
                throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_StartIndex);
            }

            if (startIndex > Length)
            {
                throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_StartIndexLargerThanLength);
            }

            if (length < 0)
            {
                throw new ArgumentOutOfRangeException("length", SR.ArgumentOutOfRange_NegativeLength);
            }

            if (startIndex > Length - length)
            {
                throw new ArgumentOutOfRangeException("length", SR.ArgumentOutOfRange_IndexLength);
            }

            if (length == 0)
            {
                return String.Empty;
            }

            if (startIndex == 0 && length == this.Length)
            {
                return this;
            }

            return InternalSubString(startIndex, length);
        }

        private unsafe string InternalSubString(int startIndex, int length)
        {
            String result = FastAllocateString(length);

            fixed (char* dest = &result._firstChar)
                fixed (char* src = &_firstChar)
            {
                wstrcpy(dest, src + startIndex, length);
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

        // Helper for encodings so they can talk to our buffer directly
        // stringLength must be the exact size we'll expect
        unsafe static internal String CreateStringFromEncoding(
            byte* bytes, int byteLength, Encoding encoding)
        {
            Contract.Requires(bytes != null);
            Contract.Requires(byteLength >= 0);

            // Get our string length
            int stringLength = encoding.GetCharCount(bytes, byteLength, null);
            Contract.Assert(stringLength >= 0, "stringLength >= 0");

            // They gave us an empty string if they needed one
            // 0 bytelength might be possible if there's something in an encoder
            if (stringLength == 0)
                return String.Empty;

            String s = FastAllocateString(stringLength);
            fixed (char* pTempChars = &s._firstChar)
            {
                int doubleCheck = encoding.GetChars(bytes, byteLength, pTempChars, stringLength, null);
                Contract.Assert(stringLength == doubleCheck,
                    "Expected encoding.GetChars to return same length as encoding.GetCharCount");
            }

            return s;
        }

        internal static String FastAllocateString(int length)
        {
            try
            {
                // We allocate one extra char as an interop convenience so that our strings are null-
                // terminated, however, we don't pass the extra +1 to the array allocation because the base
                // size of this object includes the _firstChar field.
                string newStr = RuntimeImports.RhNewArrayAsString(EETypePtr.EETypePtrOf<string>(), length);
                Debug.Assert(newStr._stringLength == length);
                return newStr;
            }
            catch (OverflowException)
            {
                throw new OutOfMemoryException();
            }
        }

        unsafe private static void FillStringChecked(String dest, int destPos, String src)
        {
            if (src.Length > dest.Length - destPos)
            {
                throw new IndexOutOfRangeException();
            }

            fixed (char* pDest = &dest._firstChar)
                fixed (char* pSrc = &src._firstChar)
            {
                wstrcpy(pDest + destPos, pSrc, src.Length);
            }
        }

        internal static unsafe void wstrcpy(char* dmem, char* smem, int charCount)
        {
            Buffer.Memmove((byte*)dmem, (byte*)smem, ((uint)charCount) * 2);
        }


        public static int Compare(String strA, String strB)
        {
            if ((Object)strA == (Object)strB)
            {
                return 0;
            }

            //they can't both be null;
            if (strA == null)
            {
                return -1;
            }

            if (strB == null)
            {
                return 1;
            }

            return FormatProvider.Compare(strA, 0, strA.Length, strB, 0, strB.Length);
        }

        public static int Compare(String strA, String strB, Boolean ignoreCase)
        {
            if ((Object)strA == (Object)strB)
            {
                return 0;
            }

            //they can't both be null;
            if (strA == null)
            {
                return -1;
            }

            if (strB == null)
            {
                return 1;
            }

            if (ignoreCase)
            {
                return FormatProvider.CompareIgnoreCase(strA, 0, strA.Length, strB, 0, strB.Length);
            }
            else
            {
                return FormatProvider.Compare(strA, 0, strA.Length, strB, 0, strB.Length);
            }
        }

        // Provides a more flexible function for string comparision. See StringComparison 
        // for meaning of different comparisonType.
        public static int Compare(String strA, String strB, StringComparison comparisonType)
        {
            if (comparisonType < StringComparison.CurrentCulture || comparisonType > StringComparison.OrdinalIgnoreCase)
            {
                throw new ArgumentException(SR.NotSupported_StringComparison, "comparisonType");
            }

            if ((Object)strA == (Object)strB)
            {
                return 0;
            }

            //they can't both be null;
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
            int lengthA = length;
            int lengthB = length;

            if (strA != null)
            {
                if (strA.Length - indexA < lengthA)
                {
                    lengthA = (strA.Length - indexA);
                }
            }

            if (strB != null)
            {
                if (strB.Length - indexB < lengthB)
                {
                    lengthB = (strB.Length - indexB);
                }
            }
            return FormatProvider.Compare(strA, indexA, lengthA, strB, indexB, lengthB);
        }

        public static int Compare(String strA, int indexA, String strB, int indexB, int length, StringComparison comparisonType)
        {
            if (comparisonType < StringComparison.CurrentCulture || comparisonType > StringComparison.OrdinalIgnoreCase)
            {
                throw new ArgumentException(SR.NotSupported_StringComparison, "comparisonType");
            }

            if (strA == null || strB == null)
            {
                if ((Object)strA == (Object)strB)
                { //they're both null;
                    return 0;
                }

                return (strA == null) ? -1 : 1; //-1 if A is null, 1 if B is null.
            }

            // @TODO: Spec#: Figure out what to do here with the return statement above.
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException("length",
                                                      SR.ArgumentOutOfRange_NegativeLength);
            }

            if (indexA < 0)
            {
                throw new ArgumentOutOfRangeException("indexA",
                                                     SR.ArgumentOutOfRange_Index);
            }

            if (indexB < 0)
            {
                throw new ArgumentOutOfRangeException("indexB",
                                                     SR.ArgumentOutOfRange_Index);
            }

            if (strA.Length - indexA < 0)
            {
                throw new ArgumentOutOfRangeException("indexA",
                                                      SR.ArgumentOutOfRange_Index);
            }

            if (strB.Length - indexB < 0)
            {
                throw new ArgumentOutOfRangeException("indexB",
                                                      SR.ArgumentOutOfRange_Index);
            }

            if ((length == 0) ||
                ((strA == strB) && (indexA == indexB)))
            {
                return 0;
            }

            int lengthA = length;
            int lengthB = length;

            if (strA != null)
            {
                if (strA.Length - indexA < lengthA)
                {
                    lengthA = (strA.Length - indexA);
                }
            }

            if (strB != null)
            {
                if (strB.Length - indexB < lengthB)
                {
                    lengthB = (strB.Length - indexB);
                }
            }

            switch (comparisonType)
            {
                case StringComparison.CurrentCulture:
                    return FormatProvider.Compare(strA, indexA, lengthA, strB, indexB, lengthB);

                case StringComparison.CurrentCultureIgnoreCase:
                    return FormatProvider.CompareIgnoreCase(strA, indexA, lengthA, strB, indexB, lengthB);

                case StringComparison.Ordinal:
                    return nativeCompareOrdinalEx(strA, indexA, strB, indexB, length);

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

            if (!(value is String))
            {
                throw new ArgumentException(SR.Arg_MustBeString);
            }

            return String.Compare(this, (String)value, StringComparison.CurrentCulture);
        }

        // Determines the sorting relation of StrB to the current instance.
        //

        public int CompareTo(String strB)
        {
            if (strB == null)
            {
                return 1;
            }

            return FormatProvider.Compare(this, 0, this.Length, strB, 0, strB.Length);
        }

        // Compares strA and strB using an ordinal (code-point) comparison.
        //

        public static int CompareOrdinal(String strA, String strB)
        {
            if ((Object)strA == (Object)strB)
            {
                return 0;
            }

            //they can't both be null;
            if (strA == null)
            {
                return -1;
            }

            if (strB == null)
            {
                return 1;
            }

            return CompareOrdinalHelper(strA, strB);
        }

        public static int CompareOrdinal(String strA, int indexA, String strB, int indexB, int length)
        {
            if (strA == null || strB == null)
            {
                if ((Object)strA == (Object)strB)
                { //they're both null;
                    return 0;
                }

                return (strA == null) ? -1 : 1; //-1 if A is null, 1 if B is null.
            }

            return nativeCompareOrdinalEx(strA, indexA, strB, indexB, length);
        }

        public bool Contains(string value)
        {
            return (IndexOf(value, StringComparison.Ordinal) >= 0);
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
                throw new ArgumentNullException("value");
            }

            if (comparisonType < StringComparison.CurrentCulture || comparisonType > StringComparison.OrdinalIgnoreCase)
            {
                throw new ArgumentException(SR.NotSupported_StringComparison, "comparisonType");
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
                    return FormatProvider.IsSuffix(this, value);

                case StringComparison.CurrentCultureIgnoreCase:
                    return FormatProvider.IsSuffixIgnoreCase(this, value);

                case StringComparison.Ordinal:
                    return this.Length < value.Length ? false : (nativeCompareOrdinalEx(this, this.Length - value.Length, value, 0, value.Length) == 0);

                case StringComparison.OrdinalIgnoreCase:
                    return this.Length < value.Length ? false : (FormatProvider.CompareOrdinalIgnoreCase(this, this.Length - value.Length, value.Length, value, 0, value.Length) == 0);

                default:
                    throw new ArgumentException(SR.NotSupported_StringComparison, "comparisonType");
            }
        }

        internal bool EndsWith(char value)
        {
            int thisLen = this.Length;
            if (thisLen != 0)
            {
                if (this[thisLen - 1] == value)
                    return true;
            }
            return false;
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

        public String PadLeft(int totalWidth)
        {
            return PadLeft(totalWidth, ' ');
        }

        public String PadLeft(int totalWidth, char paddingChar)
        {
            if (totalWidth < 0)
                throw new ArgumentOutOfRangeException("totalWidth", SR.ArgumentOutOfRange_NeedNonNegNum);
            int oldLength = Length;
            int count = totalWidth - oldLength;
            if (count <= 0)
                return this;
            String result = FastAllocateString(totalWidth);
            unsafe
            {
                fixed (char* dst = &result._firstChar)
                {
                    for (int i = 0; i < count; i++)
                        dst[i] = paddingChar;
                    fixed (char* src = &_firstChar)
                    {
                        wstrcpy(dst + count, src, oldLength);
                    }
                }
            }
            return result;
        }

        public String PadRight(int totalWidth)
        {
            return PadRight(totalWidth, ' ');
        }

        public String PadRight(int totalWidth, char paddingChar)
        {
            if (totalWidth < 0)
                throw new ArgumentOutOfRangeException("totalWidth", SR.ArgumentOutOfRange_NeedNonNegNum);
            int oldLength = Length;
            int count = totalWidth - oldLength;
            if (count <= 0)
                return this;
            String result = FastAllocateString(totalWidth);
            unsafe
            {
                fixed (char* dst = &result._firstChar)
                {
                    fixed (char* src = &_firstChar)
                    {
                        wstrcpy(dst, src, oldLength);
                    }
                    for (int i = 0; i < count; i++)
                        dst[oldLength + i] = paddingChar;
                }
            }
            return result;
        }


        // Determines whether a specified string is a prefix of the current instance
        //
        public Boolean StartsWith(String value)
        {
            if ((Object)value == null)
            {
                throw new ArgumentNullException("value");
            }
            return StartsWith(value, StringComparison.CurrentCulture);
        }

        public Boolean StartsWith(String value, StringComparison comparisonType)
        {
            if ((Object)value == null)
            {
                throw new ArgumentNullException("value");
            }

            if (comparisonType < StringComparison.CurrentCulture || comparisonType > StringComparison.OrdinalIgnoreCase)
            {
                throw new ArgumentException(SR.NotSupported_StringComparison, "comparisonType");
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
                    return FormatProvider.IsPrefix(this, value);

                case StringComparison.CurrentCultureIgnoreCase:
                    return FormatProvider.IsPrefixIgnoreCase(this, value);

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
                    return FormatProvider.CompareOrdinalIgnoreCase(this, 0, value.Length, value, 0, value.Length) == 0;

                default:
                    throw new ArgumentException(SR.NotSupported_StringComparison, "comparisonType");
            }
        }

        // Creates a copy of this string in lower case.  The culture is set by culture.
        public String ToLower()
        {
            return FormatProvider.ToLower(this);
        }

        // Creates a copy of this string in lower case based on invariant culture.
        public String ToLowerInvariant()
        {
            return FormatProvider.ToLowerInvariant(this);
        }

        public String ToUpper()
        {
            return FormatProvider.ToUpper(this);
        }

        //Creates a copy of this string in upper case based on invariant culture.
        public String ToUpperInvariant()
        {
            return FormatProvider.ToUpperInvariant(this);
        }


        // Returns this string.
        public override String ToString()
        {
            return this;
        }

        // Returns this string.
        String IConvertible.ToString(IFormatProvider provider)
        {
            return this;
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

        // Replaces all instances of oldChar with newChar.
        //
        public String Replace(char oldChar, char newChar)
        {
            if (oldChar == newChar)
                return this;

            unsafe
            {
                int remainingLength = Length;

                fixed (char* pChars = &_firstChar)
                {
                    char* pSrc = pChars;

                    while (remainingLength > 0)
                    {
                        if (*pSrc == oldChar)
                        {
                            break;
                        }

                        remainingLength--;
                        pSrc++;
                    }
                }

                if (remainingLength == 0)
                    return this;

                String result = FastAllocateString(Length);

                fixed (char* pChars = &_firstChar)
                {
                    fixed (char* pResult = &result._firstChar)
                    {
                        int copyLength = Length - remainingLength;

                        //Copy the characters already proven not to match.
                        if (copyLength > 0)
                        {
                            wstrcpy(pResult, pChars, copyLength);
                        }

                        //Copy the remaining characters, doing the replacement as we go.
                        char* pSrc = pChars + copyLength;
                        char* pDst = pResult + copyLength;

                        do
                        {
                            char currentChar = *pSrc;
                            if (currentChar == oldChar)
                                currentChar = newChar;
                            *pDst = currentChar;

                            remainingLength--;
                            pSrc++;
                            pDst++;
                        } while (remainingLength > 0);
                    }
                }

                return result;
            }
        }

        public String Replace(String oldValue, String newValue)
        {
            unsafe
            {
                if (oldValue == null)
                    throw new ArgumentNullException("oldValue");
                if (oldValue.Length == 0)
                    throw new ArgumentException(SR.Format(SR.Argument_StringZeroLength, "oldValue"));
                // Api behavior: if newValue is null, instances of oldValue are to be removed.
                if (newValue == null)
                    newValue = String.Empty;

                int numOccurrences = 0;
                int[] replacementIndices = new int[this.Length];
                fixed (char* pThis = &_firstChar)
                {
                    fixed (char* pOldValue = &oldValue._firstChar)
                    {
                        int idx = 0;
                        int lastPossibleMatchIdx = this.Length - oldValue.Length;
                        while (idx <= lastPossibleMatchIdx)
                        {
                            int probeIdx = idx;
                            int oldValueIdx = 0;
                            bool foundMismatch = false;
                            while (oldValueIdx < oldValue.Length)
                            {
                                Debug.Assert(probeIdx >= 0 && probeIdx < this.Length);
                                Debug.Assert(oldValueIdx >= 0 && oldValueIdx < oldValue.Length);
                                if (pThis[probeIdx] != pOldValue[oldValueIdx])
                                {
                                    foundMismatch = true;
                                    break;
                                }
                                probeIdx++;
                                oldValueIdx++;
                            }
                            if (!foundMismatch)
                            {
                                // Found a match for the string. Record the location of the match and skip over the "oldValue."
                                replacementIndices[numOccurrences++] = idx;
                                Debug.Assert(probeIdx == idx + oldValue.Length);
                                idx = probeIdx;
                            }
                            else
                            {
                                idx++;
                            }
                        }
                    }
                }

                if (numOccurrences == 0)
                    return this;

                int dstLength = checked(this.Length + (newValue.Length - oldValue.Length) * numOccurrences);
                String dst = FastAllocateString(dstLength);
                fixed (char* pThis = &_firstChar)
                {
                    fixed (char* pDst = &dst._firstChar)
                    {
                        fixed (char* pNewValue = &newValue._firstChar)
                        {
                            int dstIdx = 0;
                            int thisIdx = 0;

                            for (int r = 0; r < numOccurrences; r++)
                            {
                                int replacementIdx = replacementIndices[r];

                                // Copy over the non-matching portion of the original that precedes this occurrence of oldValue.
                                int count = replacementIdx - thisIdx;
                                Debug.Assert(count >= 0);
                                Debug.Assert(thisIdx >= 0 && thisIdx <= this.Length - count);
                                Debug.Assert(dstIdx >= 0 && dstIdx <= dst.Length - count);
                                if (count != 0)
                                {
                                    wstrcpy(&(pDst[dstIdx]), &(pThis[thisIdx]), count);
                                    dstIdx += count;
                                }
                                thisIdx = replacementIdx + oldValue.Length;

                                // Copy over newValue to replace the oldValue.
                                Debug.Assert(thisIdx >= 0 && thisIdx <= this.Length);
                                Debug.Assert(dstIdx >= 0 && dstIdx <= dst.Length - newValue.Length);
                                wstrcpy(&(pDst[dstIdx]), pNewValue, newValue.Length);
                                dstIdx += newValue.Length;
                            }

                            // Copy over the final non-matching portion at the end of the string.
                            int tailLength = this.Length - thisIdx;
                            Debug.Assert(tailLength >= 0);
                            Debug.Assert(thisIdx == this.Length - tailLength);
                            Debug.Assert(dstIdx == dst.Length - tailLength);
                            wstrcpy(&(pDst[dstIdx]), &(pThis[thisIdx]), tailLength);
                        }
                    }
                }

                return dst;
            }
        }

        public String Remove(int startIndex, int count)
        {
            if (startIndex < 0)
                throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_StartIndex);
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_NegativeCount);
            int oldLength = this.Length;
            if (count > oldLength - startIndex)
                throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_IndexCount);
            
            if (count == 0)
                return this;
            int newLength = oldLength - count;
            if (newLength == 0)
                return string.Empty;
            
            String result = FastAllocateString(newLength);
            unsafe
            {
                fixed (char* src = &_firstChar)
                {
                    fixed (char* dst = &result._firstChar)
                    {
                        wstrcpy(dst, src, startIndex);
                        wstrcpy(dst + startIndex, src + startIndex + count, newLength - startIndex);
                    }
                }
            }
            return result;
        }

        // a remove that just takes a startindex. 
        public string Remove(int startIndex)
        {
            if (startIndex < 0)
            {
                throw new ArgumentOutOfRangeException("startIndex",
                        SR.ArgumentOutOfRange_StartIndex);
            }

            if (startIndex >= Length)
            {
                throw new ArgumentOutOfRangeException("startIndex",
                        SR.ArgumentOutOfRange_StartIndexLessThanLength);
            }

            return Substring(0, startIndex);
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

        public static String Concat(params Object[] args)
        {
            if (args == null)
            {
                throw new ArgumentNullException("args");
            }

            String[] sArgs = new String[args.Length];
            int totalLength = 0;

            for (int i = 0; i < args.Length; i++)
            {
                object value = args[i];
                sArgs[i] = ((value == null) ? (String.Empty) : (value.ToString()));
                if (sArgs[i] == null) sArgs[i] = String.Empty; // value.ToString() above could have returned null
                totalLength += sArgs[i].Length;
                // check for overflow
                if (totalLength < 0)
                {
                    throw new OutOfMemoryException();
                }
            }

            string result = FastAllocateString(totalLength);
            int currPos = 0;
            for (int i = 0; i < sArgs.Length; i++)
            {
                FillStringChecked(result, currPos, sArgs[i]);
                currPos += sArgs[i].Length;
            }

            return result;
        }

        public static String Concat<T>(IEnumerable<T> values)
        {
            if (values == null)
                throw new ArgumentNullException("values");

            StringBuilder result = StringBuilderCache.Acquire();
            using (IEnumerator<T> en = values.GetEnumerator())
            {
                while (en.MoveNext())
                {
                    T currentValue = en.Current;

                    if (currentValue != null)
                    {
                        result.Append(currentValue.ToString());
                    }
                }
            }
            return StringBuilderCache.GetStringAndRelease(result);
        }

        public static String Concat(IEnumerable<String> values)
        {
            if (values == null)
                throw new ArgumentNullException("values");

            StringBuilder result = StringBuilderCache.Acquire();
            using (IEnumerator<String> en = values.GetEnumerator())
            {
                while (en.MoveNext())
                {
                    result.Append(en.Current);
                }
            }
            return StringBuilderCache.GetStringAndRelease(result);
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

        IEnumerator<char> IEnumerable<char>.GetEnumerator()
        {
            return new CharEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new CharEnumerator(this);
        }

        internal static unsafe int wcslen(char* ptr)
        {
            char* end = ptr;

            // The following code is (somewhat surprisingly!) significantly faster than a naive loop,
            // at least on x86 and the current jit.

            // First make sure our pointer is aligned on a dword boundary
            while (((uint)end & 3) != 0 && *end != 0)
                end++;
            if (*end != 0)
            {
                // The loop condition below works because if "end[0] & end[1]" is non-zero, that means
                // neither operand can have been zero. If is zero, we have to look at the operands individually,
                // but we hope this going to fairly rare.

                // In general, it would be incorrect to access end[1] if we haven't made sure
                // end[0] is non-zero. However, we know the ptr has been aligned by the loop above
                // so end[0] and end[1] must be in the same page, so they're either both accessible, or both not.

                while ((end[0] & end[1]) != 0 || (end[0] != 0 && end[1] != 0))
                {
                    end += 2;
                }
            }
            // finish up with the naive loop
            for (; *end != 0; end++)
                ;

            int count = (int)(end - ptr);

            return count;
        }

#if !CORERT
        // This method give you access raw access to a unpinned (i.e. don't hand out via interop) 
        // string data to do efficent string indexing and substring operations.
        internal StringPointer GetStringPointer(int startIndex = 0)
        {
            return new StringPointer(this, startIndex);
        }

        [System.Runtime.CompilerServices.StackOnly]
        internal struct StringPointer
        {
            private string _theString;
            private int _index;

            public StringPointer(string s, int startIndex = 0)
            {
                _theString = s;
                _index = startIndex;
            }

            public char this[int offset]
            {
                get
                {
                    System.Runtime.CompilerServices.ByReference<char> mgdPtr = System.Runtime.CompilerServices.ByReference<char>.FromRef(ref _theString._firstChar);
                    return System.Runtime.CompilerServices.ByReference<char>.LoadAtIndex(mgdPtr, offset + _index);
                }
                set
                {
                    System.Runtime.CompilerServices.ByReference<char> mgdPtr = System.Runtime.CompilerServices.ByReference<char>.FromRef(ref _theString._firstChar);
                    System.Runtime.CompilerServices.ByReference<char>.StoreAtIndex(mgdPtr, offset + _index, value);
                }
            }
        }
#endif

        //
        // IConvertible implementation
        // 

        TypeCode IConvertible.GetTypeCode()
        {
            return TypeCode.String;
        }

        /// <internalonly/>
        bool IConvertible.ToBoolean(IFormatProvider provider)
        {
            return Convert.ToBoolean(this, provider);
        }

        /// <internalonly/>
        char IConvertible.ToChar(IFormatProvider provider)
        {
            return Convert.ToChar(this, provider);
        }

        /// <internalonly/>
        sbyte IConvertible.ToSByte(IFormatProvider provider)
        {
            return Convert.ToSByte(this, provider);
        }

        /// <internalonly/>
        byte IConvertible.ToByte(IFormatProvider provider)
        {
            return Convert.ToByte(this, provider);
        }

        /// <internalonly/>
        short IConvertible.ToInt16(IFormatProvider provider)
        {
            return Convert.ToInt16(this, provider);
        }

        /// <internalonly/>
        ushort IConvertible.ToUInt16(IFormatProvider provider)
        {
            return Convert.ToUInt16(this, provider);
        }

        /// <internalonly/>
        int IConvertible.ToInt32(IFormatProvider provider)
        {
            return Convert.ToInt32(this, provider);
        }

        /// <internalonly/>
        uint IConvertible.ToUInt32(IFormatProvider provider)
        {
            return Convert.ToUInt32(this, provider);
        }

        /// <internalonly/>
        long IConvertible.ToInt64(IFormatProvider provider)
        {
            return Convert.ToInt64(this, provider);
        }

        /// <internalonly/>
        ulong IConvertible.ToUInt64(IFormatProvider provider)
        {
            return Convert.ToUInt64(this, provider);
        }

        /// <internalonly/>
        float IConvertible.ToSingle(IFormatProvider provider)
        {
            return Convert.ToSingle(this, provider);
        }

        /// <internalonly/>
        double IConvertible.ToDouble(IFormatProvider provider)
        {
            return Convert.ToDouble(this, provider);
        }

        /// <internalonly/>
        Decimal IConvertible.ToDecimal(IFormatProvider provider)
        {
            return Convert.ToDecimal(this, provider);
        }

        /// <internalonly/>
        DateTime IConvertible.ToDateTime(IFormatProvider provider)
        {
            return Convert.ToDateTime(this, provider);
        }

        /// <internalonly/>
        Object IConvertible.ToType(Type type, IFormatProvider provider)
        {
            return Convert.DefaultToType((IConvertible)this, type, provider);
        }
    }

    [Flags]
    public enum StringSplitOptions
    {
        None = 0,
        RemoveEmptyEntries = 1
    }
}
