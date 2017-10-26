// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*============================================================
**
** Purpose: Your favorite String class.  Native methods 
** are implemented in StringNative.cpp
**
===========================================================*/

using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace System
{
    // The String class represents a static string of characters.  Many of
    // the String methods perform some type of transformation on the current
    // instance and return the result as a new String. All comparison methods are
    // implemented as a part of String.  As with arrays, character positions
    // (indices) are zero-based.

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
    [StructLayout(LayoutKind.Sequential)]
    [System.Runtime.CompilerServices.EagerStaticClassConstructionAttribute]
    [Serializable]
    [System.Runtime.CompilerServices.TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    public sealed partial class String : IComparable, IEnumerable, IEnumerable<char>, IComparable<String>, IEquatable<String>, IConvertible, ICloneable
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

#if PROJECTN
        [Bound]
#endif
        // WARNING: We allow diagnostic tools to directly inspect these two members (_stringLength, _firstChar)
        // See https://github.com/dotnet/corert/blob/master/Documentation/design-docs/diagnostics/diagnostics-tools-contract.md for more details. 
        // Please do not change the type, the name, or the semantic usage of this member without understanding the implication for tools. 
        // Get in touch with the diagnostics team if you have questions.
        [NonSerialized]
        private int _stringLength;
        [NonSerialized]
        private char _firstChar;

#pragma warning restore

        // String constructors
        // These are special. the implementation methods for these have a different signature from the
        // declared constructors.

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public extern String(char[] value);

        [System.Runtime.CompilerServices.DependencyReductionRoot]
        private static String Ctor(char[] value)
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
        public extern String(char[] value, int startIndex, int length);

        [System.Runtime.CompilerServices.DependencyReductionRoot]
        private static String Ctor(char[] value, int startIndex, int length)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (startIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(startIndex), SR.ArgumentOutOfRange_StartIndex);

            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), SR.ArgumentOutOfRange_NegativeLength);

            if (startIndex > value.Length - length)
                throw new ArgumentOutOfRangeException(nameof(startIndex), SR.ArgumentOutOfRange_Index);

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
        unsafe public extern String(char* value);

        [System.Runtime.CompilerServices.DependencyReductionRoot]
        private static unsafe String Ctor(char* ptr)
        {
            if (ptr == null)
                return String.Empty;

#if !PLATFORM_UNIX
            if (ptr < (char*)64000)
                throw new ArgumentException(SR.Arg_MustBeStringPtrNotAtom);
#endif // PLATFORM_UNIX

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
                throw new ArgumentOutOfRangeException(nameof(ptr), SR.ArgumentOutOfRange_PartialWCHAR);
            }
        }

        [CLSCompliant(false)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        unsafe public extern String(char* value, int startIndex, int length);

        [System.Runtime.CompilerServices.DependencyReductionRoot]
        private static unsafe String Ctor(char* ptr, int startIndex, int length)
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), SR.ArgumentOutOfRange_NegativeLength);
            }

            if (startIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex), SR.ArgumentOutOfRange_StartIndex);
            }

            char* pFrom = ptr + startIndex;
            if (pFrom < ptr)
            {
                // This means that the pointer operation has had an overflow
                throw new ArgumentOutOfRangeException(nameof(startIndex), SR.ArgumentOutOfRange_PartialWCHAR);
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
                throw new ArgumentOutOfRangeException(nameof(ptr), SR.ArgumentOutOfRange_PartialWCHAR);
            }
        }

        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern unsafe String(sbyte* value);

        [DependencyReductionRoot]
        private static unsafe string Ctor(sbyte* value)
        {
            byte* pb = (byte*)value;
            if (pb == null)
                return string.Empty;  // Compatibility

            int numBytes = Buffer.IndexOfByte(pb, 0, 0, int.MaxValue);
            if (numBytes < 0) // This check covers the "-1 = not-found" case and "negative length" case (downstream code assumes length is non-negative).
                throw new ArgumentException(SR.Arg_ExpectedNulTermination); // We'll likely have AV'd before we get to this point, but just in case...

            return CreateStringForSByteConstructor(pb, numBytes);
        }

        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern unsafe String(sbyte* value, int startIndex, int length);

        [DependencyReductionRoot]
        private static unsafe string Ctor(sbyte* value, int startIndex, int length)
        {
            byte* pb = (byte*)value;

            if (startIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(startIndex), SR.ArgumentOutOfRange_StartIndex);

            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), SR.ArgumentOutOfRange_NegativeLength);

            if (pb == null)
                throw new ArgumentNullException(nameof(value));

            byte* pStart = pb + startIndex;
            if (pStart < pb)
                throw new ArgumentOutOfRangeException(nameof(value), SR.ArgumentOutOfRange_PartialWCHAR);

            return CreateStringForSByteConstructor(pStart, length);
        }

        // Encoder for String..ctor(sbyte*) and String..ctor(sbyte*, int, int). One of the last bastions of ANSI in the framework...
        private static unsafe string CreateStringForSByteConstructor(byte *pb, int numBytes)
        {
            Debug.Assert(numBytes >= 0);
            Debug.Assert(pb <= (pb + numBytes));

            if (numBytes == 0)
                return string.Empty;

#if PLATFORM_UNIX
            return Encoding.UTF8.GetString(pb, numBytes);
#else
            int numCharsRequired = Interop.Kernel32.MultiByteToWideChar(Interop.Kernel32.CP_ACP, Interop.Kernel32.MB_PRECOMPOSED, pb, numBytes, (char*)null, 0);
            if (numCharsRequired == 0)
                throw new ArgumentException(SR.Arg_InvalidANSIString);

            string newString = FastAllocateString(numCharsRequired);
            fixed (char *pFirstChar = &newString._firstChar)
            {
                numCharsRequired = Interop.Kernel32.MultiByteToWideChar(Interop.Kernel32.CP_ACP, Interop.Kernel32.MB_PRECOMPOSED, pb, numBytes, pFirstChar, numCharsRequired);
            }
            if (numCharsRequired == 0)
                throw new ArgumentException(SR.Arg_InvalidANSIString);
            return newString;
#endif
        }

        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern unsafe String(sbyte* value, int startIndex, int length, Encoding enc);

        [DependencyReductionRoot]
        private static unsafe string Ctor(sbyte* value, int startIndex, int length, Encoding enc)
        {
            if (enc == null)
                return new string(value, startIndex, length);

            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), SR.ArgumentOutOfRange_NeedNonNegNum);
            if (startIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(startIndex), SR.ArgumentOutOfRange_StartIndex);

            byte* pStart = (byte*)(value + startIndex);
            if (pStart < value)
            {
                // overflow check
                throw new ArgumentOutOfRangeException(nameof(startIndex), SR.ArgumentOutOfRange_PartialWCHAR);
            }

            byte[] copyOfValue = new byte[length];
            fixed (byte* pCopyOfValue = copyOfValue)
            {
                try
                {
                    Buffer.Memcpy(dest: pCopyOfValue, src: pStart, len: length);
                }
                catch (Exception)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), SR.ArgumentOutOfRange_PartialWCHAR);
                }
            }

            return enc.GetString(copyOfValue);
        }

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public extern String(char c, int count);

        [System.Runtime.CompilerServices.DependencyReductionRoot]
        private static String Ctor(char c, int count)
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
                throw new ArgumentOutOfRangeException(nameof(count), SR.ArgumentOutOfRange_NegativeCount);
        }

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public extern String(ReadOnlySpan<char> value);

        [DependencyReductionRoot]
        private unsafe static string Ctor(ReadOnlySpan<char> value)
        {
            if (value.Length == 0)
            {
                return Empty;
            }

            string result = FastAllocateString(value.Length);
            fixed (char* dest = &result._firstChar, src = &value.DangerousGetPinnableReference())
            {
                wstrcpy(dest, src, value.Length);
            }
            return result;
        }

        public static string Create<TState>(int length, TState state, SpanAction<char, TState> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (length > 0)
            {
                string result = FastAllocateString(length);
                action(new Span<char>(ref result.GetRawStringData(), length), state);
                return result;
            }

            if (length == 0)
            {
                return Empty;
            }

            throw new ArgumentOutOfRangeException(nameof(length));
        }

        public object Clone()
        {
            return this;
        }

        public static unsafe String Copy(String str)
        {
            if (str == null)
            {
                throw new ArgumentNullException(nameof(str));
            }

            int length = str.Length;

            String result = FastAllocateString(length);

            fixed (char* dest = &result._firstChar)
            fixed (char* src = &str._firstChar)
            {
                wstrcpy(dest, src, length);
            }
            return result;
        }

        public static readonly String Empty = "";

        // Gets the character at a specified position.
        //
        // Spec#: Apply the precondition here using a contract assembly.  Potential perf issue.
        [System.Runtime.CompilerServices.IndexerName("Chars")]
        public unsafe char this[int index]
        {
#if PROJECTN
            [BoundsChecking]
            get
            {
                return Unsafe.Add(ref _firstChar, index);
            }
#else
            [Intrinsic]
            get
            {
                if ((uint)index >= _stringLength)
                    ThrowHelper.ThrowIndexOutOfRangeException();
                return Unsafe.Add(ref _firstChar, index);
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
                throw new ArgumentNullException(nameof(destination));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), SR.ArgumentOutOfRange_NegativeCount);
            if (sourceIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(sourceIndex), SR.ArgumentOutOfRange_Index);
            if (count > Length - sourceIndex)
                throw new ArgumentOutOfRangeException(nameof(sourceIndex), SR.ArgumentOutOfRange_IndexCount);
            if (destinationIndex > destination.Length - count || destinationIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(destinationIndex), SR.ArgumentOutOfRange_IndexCount);

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
                fixed (char* dest = &chars[0])
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
                throw new ArgumentOutOfRangeException(nameof(startIndex), SR.ArgumentOutOfRange_Index);
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), SR.ArgumentOutOfRange_Index);

            if (length > 0)
            {
                char[] chars = new char[length];
                fixed (char* src = &_firstChar)
                fixed (char* dest = &chars[0])
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

        internal ref char GetRawStringData()
        {
            return ref _firstChar;
        }

        // Helper for encodings so they can talk to our buffer directly
        // stringLength must be the exact size we'll expect
        unsafe internal static String CreateStringFromEncoding(
            byte* bytes, int byteLength, Encoding encoding)
        {
            Debug.Assert(bytes != null);
            Debug.Assert(byteLength >= 0);

            // Get our string length
            int stringLength = encoding.GetCharCount(bytes, byteLength, null);
            Debug.Assert(stringLength >= 0, "stringLength >= 0");

            // They gave us an empty string if they needed one
            // 0 bytelength might be possible if there's something in an encoder
            if (stringLength == 0)
                return String.Empty;

            String s = FastAllocateString(stringLength);
            fixed (char* pTempChars = &s._firstChar)
            {
                int doubleCheck = encoding.GetChars(bytes, byteLength, pTempChars, stringLength, null);
                Debug.Assert(stringLength == doubleCheck,
                    "Expected encoding.GetChars to return same length as encoding.GetCharCount");
            }

            return s;
        }

        // This is only intended to be used by char.ToString.
        // It is necessary to put the code in this class instead of Char, since _firstChar is a private member.
        // Making _firstChar internal would be dangerous since it would make it much easier to break String's immutability.
        internal static string CreateFromChar(char c)
        {
            string result = FastAllocateString(1);
            result._firstChar = c;
            return result;
        }

        internal static String FastAllocateString(int length)
        {
            // We allocate one extra char as an interop convenience so that our strings are null-
            // terminated, however, we don't pass the extra +1 to the string allocation because the base
            // size of this object includes the _firstChar field.
            string newStr = RuntimeImports.RhNewString(EETypePtr.EETypePtrOf<string>(), length);
            Debug.Assert(newStr._stringLength == length);
            return newStr;
        }

        internal static unsafe void wstrcpy(char* dmem, char* smem, int charCount)
        {
            Buffer.Memmove((byte*)dmem, (byte*)smem, ((uint)charCount) * 2);
        }


        // Returns this string.
        public override String ToString()
        {
            return this;
        }

        // Returns this string.
        public String ToString(IFormatProvider provider)
        {
            return this;
        }

        public CharEnumerator GetEnumerator()
        {
            return new CharEnumerator(this);
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

            // First make sure our pointer is aligned on a word boundary
            int alignment = IntPtr.Size - 1;

            // If ptr is at an odd address (e.g. 0x5), this loop will simply iterate all the way
            while (((uint)end & (uint)alignment) != 0)
            {
                if (*end == 0) goto FoundZero;
                end++;
            }

#if !BIT64
            // The loop condition below works because if "end[0] & end[1]" is non-zero, that means
            // neither operand can have been zero. If is zero, we have to look at the operands individually,
            // but we hope this going to fairly rare.

            // In general, it would be incorrect to access end[1] if we haven't made sure
            // end[0] is non-zero. However, we know the ptr has been aligned by the loop above
            // so end[0] and end[1] must be in the same word (and therefore page), so they're either both accessible, or both not.

            while ((end[0] & end[1]) != 0 || (end[0] != 0 && end[1] != 0))
            {
                end += 2;
            }

            Debug.Assert(end[0] == 0 || end[1] == 0);
            if (end[0] != 0) end++;
#else // !BIT64
            // Based on https://graphics.stanford.edu/~seander/bithacks.html#ZeroInWord

            // 64-bit implementation: process 1 ulong (word) at a time

            // What we do here is add 0x7fff from each of the
            // 4 individual chars within the ulong, using MagicMask.
            // If the char > 0 and < 0x8001, it will have its high bit set.
            // We then OR with MagicMask, to set all the other bits.
            // This will result in all bits set (ulong.MaxValue) for any
            // char that fits the above criteria, and something else otherwise.

            // Note that for any char > 0x8000, this will be a false
            // positive and we will fallback to the slow path and
            // check each char individually. This is OK though, since
            // we optimize for the common case (ASCII chars, which are < 0x80).

            // NOTE: We can access a ulong a time since the ptr is aligned,
            // and therefore we're only accessing the same word/page. (See notes
            // for the 32-bit version above.)
            
            const ulong MagicMask = 0x7fff7fff7fff7fff;

            while (true)
            {
                ulong word = *(ulong*)end;
                word += MagicMask; // cause high bit to be set if not zero, and <= 0x8000
                word |= MagicMask; // set everything besides the high bits

                if (word == ulong.MaxValue) // 0xffff...
                {
                    // all of the chars have their bits set (and therefore none can be 0)
                    end += 4;
                    continue;
                }

                // at least one of them didn't have their high bit set!
                // go through each char and check for 0.

                if (end[0] == 0) goto EndAt0;
                if (end[1] == 0) goto EndAt1;
                if (end[2] == 0) goto EndAt2;
                if (end[3] == 0) goto EndAt3;

                // if we reached here, it was a false positive-- just continue
                end += 4;
            }

            EndAt3: end++;
            EndAt2: end++;
            EndAt1: end++;
            EndAt0:
#endif // !BIT64

            FoundZero:
            Debug.Assert(*end == 0);

            int count = (int)(end - ptr);

            return count;
        }

        //
        // IConvertible implementation
        // 

        public TypeCode GetTypeCode()
        {
            return TypeCode.String;
        }

        bool IConvertible.ToBoolean(IFormatProvider provider)
        {
            return Convert.ToBoolean(this, provider);
        }

        char IConvertible.ToChar(IFormatProvider provider)
        {
            return Convert.ToChar(this, provider);
        }

        sbyte IConvertible.ToSByte(IFormatProvider provider)
        {
            return Convert.ToSByte(this, provider);
        }

        byte IConvertible.ToByte(IFormatProvider provider)
        {
            return Convert.ToByte(this, provider);
        }

        short IConvertible.ToInt16(IFormatProvider provider)
        {
            return Convert.ToInt16(this, provider);
        }

        ushort IConvertible.ToUInt16(IFormatProvider provider)
        {
            return Convert.ToUInt16(this, provider);
        }

        int IConvertible.ToInt32(IFormatProvider provider)
        {
            return Convert.ToInt32(this, provider);
        }

        uint IConvertible.ToUInt32(IFormatProvider provider)
        {
            return Convert.ToUInt32(this, provider);
        }

        long IConvertible.ToInt64(IFormatProvider provider)
        {
            return Convert.ToInt64(this, provider);
        }

        ulong IConvertible.ToUInt64(IFormatProvider provider)
        {
            return Convert.ToUInt64(this, provider);
        }

        float IConvertible.ToSingle(IFormatProvider provider)
        {
            return Convert.ToSingle(this, provider);
        }

        double IConvertible.ToDouble(IFormatProvider provider)
        {
            return Convert.ToDouble(this, provider);
        }

        Decimal IConvertible.ToDecimal(IFormatProvider provider)
        {
            return Convert.ToDecimal(this, provider);
        }

        DateTime IConvertible.ToDateTime(IFormatProvider provider)
        {
            return Convert.ToDateTime(this, provider);
        }

        Object IConvertible.ToType(Type type, IFormatProvider provider)
        {
            return Convert.DefaultToType((IConvertible)this, type, provider);
        }

        // Normalization Methods
        // These just wrap calls to Normalization class
        public bool IsNormalized()
        {
            return IsNormalized(NormalizationForm.FormC);
        }

        public bool IsNormalized(NormalizationForm normalizationForm)
        {
            return Normalization.IsNormalized(this, normalizationForm);
        }

        public String Normalize()
        {
            return Normalize(NormalizationForm.FormC);
        }

        public String Normalize(NormalizationForm normalizationForm)
        {
            return Normalization.Normalize(this, normalizationForm);
        }
    }
}
