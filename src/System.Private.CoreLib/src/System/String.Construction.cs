// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;

namespace System
{
    partial class String
    {
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
            fixed (char* dest = &result._firstChar, src = &MemoryMarshal.GetReference(value))
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

        public static implicit operator ReadOnlySpan<char>(string value) =>
            value != null ? new ReadOnlySpan<char>(ref value.GetRawStringData(), value.Length) : default;

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

        internal static unsafe void wstrcpy(char* dmem, char* smem, int charCount)
        {
            Buffer.Memmove((byte*)dmem, (byte*)smem, ((uint)charCount) * 2);
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
    }
}
