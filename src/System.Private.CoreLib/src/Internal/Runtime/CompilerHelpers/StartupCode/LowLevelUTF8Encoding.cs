// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#define FASTLOOP

using System;
using System.Globalization;
using System.Text;

using Debug = Internal.Runtime.CompilerHelpers.StartupDebug;

namespace Internal.Runtime.CompilerHelpers
{
    // This code is primarily copied from UTF8Encoding.cs' GetCharCount and GetChars
    // but has all the string literals removed. The code is used for runtime startup and
    // primarily for loading static string literals. So do not put anything here
    // that would require the runtime or strings to be already initialized.
    class LowLevelUTF8Encoding
    {
        class UTF8DecodeException : Exception
        {
        };

        // These are bitmasks used to maintain the state in the decoder. They occupy the higher bits
        // while the actual character is being built in the lower bits. They are shifted together
        // with the actual bits of the character.

        // bits 30 & 31 are used for pending bits fixup
        private const int FinalByte = 1 << 29;
        private const int SupplimentarySeq = 1 << 28;
        private const int ThreeByteSeq = 1 << 27;


        private static bool InRange(int ch, int start, int end)
        {
            return (uint)(ch - start) <= (uint)(end - start);
        }
        private unsafe static int PtrDiff(byte* a, byte* b)
        {
            return (int)(a - b);
        }
        private unsafe static int PtrDiff(char* a, char* b)
        {
            return (int)(((uint)((byte*)a - (byte*)b)) >> 1);
        }

        // Note:  We throw exceptions on individually encoded surrogates and other non-shortest forms.
        //        If exceptions aren't turned on, then we drop all non-shortest &individual surrogates.
        //
        // To simplify maintenance, the structure of GetCharCount and GetChars should be
        // kept the same as much as possible
        internal unsafe static int GetCharCount(byte* bytes, int count)
        {
            Debug.Assert(count >= 0);
            Debug.Assert(bytes != null);

            // Initialize stuff
            byte* pSrc = bytes;
            byte* pEnd = pSrc + count;

            // Start by assuming we have as many as count, charCount always includes the adjustment
            // for the character being decoded
            int charCount = count;
            int ch = 0;
            DecoderFallbackBuffer fallback = null;

            for (;;)
            {
                // SLOWLOOP: does all range checks, handles all special cases, but it is slow

                if (pSrc >= pEnd)
                {
                    break;
                }

                if (ch == 0)
                {
                    // no pending bits
                    goto ReadChar;
                }

                // read next byte. The JIT optimization seems to be getting confused when
                // compiling "ch = *pSrc++;", so rather use "ch = *pSrc; pSrc++;" instead
                int cha = *pSrc;
                pSrc++;

                // we are expecting to see trailing bytes like 10vvvvvv
                if ((cha & unchecked((sbyte)0xC0)) != 0x80)
                {
                    // This can be a valid starting byte for another UTF8 byte sequence, so let's put
                    // the current byte back, and try to see if this is a valid byte for another UTF8 byte sequence
                    pSrc--;
                    charCount += (ch >> 30);
                    goto InvalidByteSequence;
                }

                // fold in the new byte
                ch = (ch << 6) | (cha & 0x3F);

                if ((ch & FinalByte) == 0)
                {
                    Debug.Assert((ch & (SupplimentarySeq | ThreeByteSeq)) != 0);

                    if ((ch & SupplimentarySeq) != 0)
                    {
                        if ((ch & (FinalByte >> 6)) != 0)
                        {
                            // this is 3rd byte (of 4 byte supplimentary) - nothing to do
                            continue;
                        }

                        // 2nd byte, check for non-shortest form of supplimentary char and the valid
                        // supplimentary characters in range 0x010000 - 0x10FFFF at the same time
                        if (!InRange(ch & 0x1F0, 0x10, 0x100))
                        {
                            goto InvalidByteSequence;
                        }
                    }
                    else
                    {
                        // Must be 2nd byte of a 3-byte sequence
                        // check for non-shortest form of 3 byte seq
                        if ((ch & (0x1F << 5)) == 0 ||                  // non-shortest form
                            (ch & (0xF800 >> 6)) == (0xD800 >> 6))     // illegal individually encoded surrogate
                        {
                            goto InvalidByteSequence;
                        }
                    }
                    continue;
                }

                // ready to punch

                // adjust for surrogates in non-shortest form
                if ((ch & (SupplimentarySeq | 0x1F0000)) == SupplimentarySeq)
                {
                    charCount--;
                }
                goto EncodeChar;

                InvalidByteSequence:
                // this code fragment should be close to the gotos referencing it
                // Have to do fallback for invalid bytes
                throw new UTF8DecodeException();
                // ch = 0;
                // continue;

                ReadChar:
                ch = *pSrc;
                pSrc++;
#if FASTLOOP
                ProcessChar:
#endif
                if (ch > 0x7F)
                {
                    // If its > 0x7F, its start of a new multi-byte sequence

                    // Long sequence, so unreserve our char.
                    charCount--;

                    // bit 6 has to be non-zero for start of multibyte chars.
                    if ((ch & 0x40) == 0)
                    {
                        // Unexpected trail byte
                        goto InvalidByteSequence;
                    }

                    // start a new long code
                    if ((ch & 0x20) != 0)
                    {
                        if ((ch & 0x10) != 0)
                        {
                            // 4 byte encoding - supplimentary character (2 surrogates)

                            ch &= 0x0F;

                            // check that bit 4 is zero and the valid supplimentary character
                            // range 0x000000 - 0x10FFFF at the same time
                            if (ch > 0x04)
                            {
                                ch |= 0xf0;
                                goto InvalidByteSequence;
                            }

                            // Add bit flags so that when we check new characters & rotate we'll be flagged correctly.
                            // Final byte flag, count fix if we don't make final byte & supplimentary sequence flag.
                            ch |= (FinalByte >> 3 * 6) |  // Final byte is 3 more bytes from now
                                  (1 << 30) |           // If it dies on next byte we'll need an extra char
                                  (3 << (30 - 2 * 6)) |     // If it dies on last byte we'll need to subtract a char
                                (SupplimentarySeq) | (SupplimentarySeq >> 6) |
                                (SupplimentarySeq >> 2 * 6) | (SupplimentarySeq >> 3 * 6);

                            // Our character count will be 2 characters for these 4 bytes, so subtract another char
                            charCount--;
                        }
                        else
                        {
                            // 3 byte encoding
                            // Add bit flags so that when we check new characters & rotate we'll be flagged correctly.
                            ch = (ch & 0x0F) | ((FinalByte >> 2 * 6) | (1 << 30) |
                                (ThreeByteSeq) | (ThreeByteSeq >> 6) | (ThreeByteSeq >> 2 * 6));

                            // We'll expect 1 character for these 3 bytes, so subtract another char.
                            charCount--;
                        }
                    }
                    else
                    {
                        // 2 byte encoding

                        ch &= 0x1F;

                        // check for non-shortest form
                        if (ch <= 1)
                        {
                            ch |= 0xc0;
                            goto InvalidByteSequence;
                        }

                        // Add bit flags so we'll be flagged correctly
                        ch |= (FinalByte >> 6);
                    }
                    continue;
                }

                EncodeChar:

#if FASTLOOP
                int availableBytes = PtrDiff(pEnd, pSrc);

                // don't fall into the fast decoding loop if we don't have enough bytes
                if (availableBytes <= 13)
                {
                    // try to get over the remainder of the ascii characters fast though
                    byte* pLocalEnd = pEnd; // hint to get pLocalEnd enregistered
                    while (pSrc < pLocalEnd)
                    {
                        ch = *pSrc;
                        pSrc++;

                        if (ch > 0x7F)
                            goto ProcessChar;
                    }
                    // we are done
                    ch = 0;
                    break;
                }

                // To compute the upper bound, assume that all characters are ASCII characters at this point,
                //  the boundary will be decreased for every non-ASCII character we encounter
                // Also, we need 7 chars reserve for the unrolled ansi decoding loop and for decoding of multibyte sequences
                byte* pStop = pSrc + availableBytes - 7;

                while (pSrc < pStop)
                {
                    ch = *pSrc;
                    pSrc++;

                    if (ch > 0x7F)
                    {
                        goto LongCode;
                    }

                    // get pSrc 2-byte aligned
                    if ((unchecked((int)pSrc) & 0x1) != 0)
                    {
                        ch = *pSrc;
                        pSrc++;
                        if (ch > 0x7F)
                        {
                            goto LongCode;
                        }
                    }

                    // get pSrc 4-byte aligned
                    if ((unchecked((int)pSrc) & 0x2) != 0)
                    {
                        ch = *(ushort*)pSrc;
                        if ((ch & 0x8080) != 0)
                        {
                            goto LongCodeWithMask16;
                        }
                        pSrc += 2;
                    }

                    // Run 8 + 8 characters at a time!
                    while (pSrc < pStop)
                    {
                        ch = *(int*)pSrc;
                        int chb = *(int*)(pSrc + 4);
                        if (((ch | chb) & unchecked((int)0x80808080)) != 0)
                        {
                            goto LongCodeWithMask32;
                        }
                        pSrc += 8;

                        // This is a really small loop - unroll it
                        if (pSrc >= pStop)
                            break;

                        ch = *(int*)pSrc;
                        chb = *(int*)(pSrc + 4);
                        if (((ch | chb) & unchecked((int)0x80808080)) != 0)
                        {
                            goto LongCodeWithMask32;
                        }
                        pSrc += 8;
                    }
                    break;

#if BIGENDIAN
                LongCodeWithMask32:
                    // be careful about the sign extension
                    ch = (int)(((uint)ch) >> 16);
                LongCodeWithMask16:
                    ch = (int)(((uint)ch) >> 8);
#else // BIGENDIAN
                LongCodeWithMask32:
                LongCodeWithMask16:
                    ch &= 0xFF;
#endif // BIGENDIAN
                    pSrc++;
                    if (ch <= 0x7F)
                    {
                        continue;
                    }

                LongCode:
                    int chc = *pSrc;
                    pSrc++;

                    if (
                        // bit 6 has to be zero
                        (ch & 0x40) == 0 ||
                        // we are expecting to see trailing bytes like 10vvvvvv
                        (chc & unchecked((sbyte)0xC0)) != 0x80)
                    {
                        goto BadLongCode;
                    }

                    chc &= 0x3F;

                    // start a new long code
                    if ((ch & 0x20) != 0)
                    {
                        // fold the first two bytes together
                        chc |= (ch & 0x0F) << 6;

                        if ((ch & 0x10) != 0)
                        {
                            // 4 byte encoding - surrogate
                            ch = *pSrc;
                            if (
                                // check that bit 4 is zero, the non-shortest form of surrogate
                                // and the valid surrogate range 0x000000 - 0x10FFFF at the same time
                                !InRange(chc >> 4, 0x01, 0x10) ||
                                // we are expecting to see trailing bytes like 10vvvvvv
                                (ch & unchecked((sbyte)0xC0)) != 0x80)
                            {
                                goto BadLongCode;
                            }

                            chc = (chc << 6) | (ch & 0x3F);

                            ch = *(pSrc + 1);
                            // we are expecting to see trailing bytes like 10vvvvvv
                            if ((ch & unchecked((sbyte)0xC0)) != 0x80)
                            {
                                goto BadLongCode;
                            }
                            pSrc += 2;

                            // extra byte
                            charCount--;
                        }
                        else
                        {
                            // 3 byte encoding
                            ch = *pSrc;
                            if (
                                // check for non-shortest form of 3 byte seq
                                (chc & (0x1F << 5)) == 0 ||
                                // Can't have surrogates here.
                                (chc & (0xF800 >> 6)) == (0xD800 >> 6) ||
                                // we are expecting to see trailing bytes like 10vvvvvv
                                (ch & unchecked((sbyte)0xC0)) != 0x80)
                            {
                                goto BadLongCode;
                            }
                            pSrc++;

                            // extra byte
                            charCount--;
                        }
                    }
                    else
                    {
                        // 2 byte encoding

                        // check for non-shortest form
                        if ((ch & 0x1E) == 0)
                        {
                            goto BadLongCode;
                        }
                    }

                    // extra byte
                    charCount--;
                }
#endif // FASTLOOP

                // no pending bits at this point
                ch = 0;
                continue;
#if FASTLOOP
                BadLongCode:
                pSrc -= 2;
                ch = 0;
                continue;
#endif
            }

            // May have a problem if we have to flush
            if (ch != 0)
            {
                // We were already adjusting for these, so need to unadjust
                charCount += (ch >> 30);
                
                // Fallback for invalid byte sequence
                throw new UTF8DecodeException();
            }

            // Shouldn't have anything in fallback buffer for GetCharCount
            // (don't have to check m_throwOnOverflow for count)
            Debug.Assert(fallback == null);

            return charCount;
        }

        // WARNING:  If we throw an error, then System.Resources.ResourceReader calls this method.
        //           So if we're really broken, then that could also throw an error... recursively.
        //           So try to make sure GetChars can at least process all uses by
        //           System.Resources.ResourceReader!
        //
        // Note:  We throw exceptions on individually encoded surrogates and other non-shortest forms.
        //        If exceptions aren't turned on, then we drop all non-shortest &individual surrogates.
        //
        // To simplify maintenance, the structure of GetCharCount and GetChars should be
        // kept the same as much as possible
        internal unsafe static int GetChars(byte* bytes, int byteCount,
                                        char* chars, int charCount)
        {
            Debug.Assert(chars != null);
            Debug.Assert(byteCount >= 0);
            Debug.Assert(charCount >= 0);
            Debug.Assert(bytes != null);

            byte* pSrc = bytes;
            char* pTarget = chars;

            byte* pEnd = pSrc + byteCount;
            char* pAllocatedBufferEnd = pTarget + charCount;

            int ch = 0;

            DecoderFallbackBuffer fallback = null;

            for (;;)
            {
                // SLOWLOOP: does all range checks, handles all special cases, but it is slow

                if (pSrc >= pEnd)
                {
                    break;
                }

                if (ch == 0)
                {
                    // no pending bits
                    goto ReadChar;
                }

                // read next byte. The JIT optimization seems to be getting confused when
                // compiling "ch = *pSrc++;", so rather use "ch = *pSrc; pSrc++;" instead
                int cha = *pSrc;
                pSrc++;

                // we are expecting to see trailing bytes like 10vvvvvv
                if ((cha & unchecked((sbyte)0xC0)) != 0x80)
                {
                    // This can be a valid starting byte for another UTF8 byte sequence, so let's put
                    // the current byte back, and try to see if this is a valid byte for another UTF8 byte sequence
                    pSrc--;
                    goto InvalidByteSequence;
                }

                // fold in the new byte
                ch = (ch << 6) | (cha & 0x3F);

                if ((ch & FinalByte) == 0)
                {
                    // Not at last byte yet
                    Debug.Assert((ch & (SupplimentarySeq | ThreeByteSeq)) != 0);

                    if ((ch & SupplimentarySeq) != 0)
                    {
                        // Its a 4-byte supplimentary sequence
                        if ((ch & (FinalByte >> 6)) != 0)
                        {
                            // this is 3rd byte of 4 byte sequence - nothing to do
                            continue;
                        }

                        // 2nd byte of 4 bytes
                        // check for non-shortest form of surrogate and the valid surrogate
                        // range 0x000000 - 0x10FFFF at the same time
                        if (!InRange(ch & 0x1F0, 0x10, 0x100))
                        {
                            goto InvalidByteSequence;
                        }
                    }
                    else
                    {
                        // Must be 2nd byte of a 3-byte sequence
                        // check for non-shortest form of 3 byte seq
                        if ((ch & (0x1F << 5)) == 0 ||                  // non-shortest form
                            (ch & (0xF800 >> 6)) == (0xD800 >> 6))     // illegal individually encoded surrogate
                        {
                            goto InvalidByteSequence;
                        }
                    }
                    continue;
                }

                // ready to punch

                // surrogate in shortest form?
                // Might be possible to get rid of this?  Already did non-shortest check for 4-byte sequence when reading 2nd byte?
                if ((ch & (SupplimentarySeq | 0x1F0000)) > SupplimentarySeq)
                {
                    // let the range check for the second char throw the exception
                    if (pTarget < pAllocatedBufferEnd)
                    {
                        *pTarget = (char)(((ch >> 10) & 0x7FF) +
                            unchecked((short)((CharUnicodeInfo.HIGH_SURROGATE_START - (0x10000 >> 10)))));
                        pTarget++;

                        ch = (ch & 0x3FF) +
                            unchecked((int)(CharUnicodeInfo.LOW_SURROGATE_START));
                    }
                }

                goto EncodeChar;

                InvalidByteSequence:
                // this code fragment should be close to the gotos referencing it
                // Have to do fallback for invalid bytes
                throw new UTF8DecodeException();
                // ch = 0;
                // continue;

                ReadChar:
                ch = *pSrc;
                pSrc++;
#if FASTLOOP
                ProcessChar:
#endif
                if (ch > 0x7F)
                {
                    // If its > 0x7F, its start of a new multi-byte sequence

                    // bit 6 has to be non-zero
                    if ((ch & 0x40) == 0)
                    {
                        goto InvalidByteSequence;
                    }

                    // start a new long code
                    if ((ch & 0x20) != 0)
                    {
                        if ((ch & 0x10) != 0)
                        {
                            // 4 byte encoding - supplimentary character (2 surrogates)

                            ch &= 0x0F;

                            // check that bit 4 is zero and the valid supplimentary character
                            // range 0x000000 - 0x10FFFF at the same time
                            if (ch > 0x04)
                            {
                                ch |= 0xf0;
                                goto InvalidByteSequence;
                            }

                            ch |= (FinalByte >> 3 * 6) | (1 << 30) | (3 << (30 - 2 * 6)) |
                                (SupplimentarySeq) | (SupplimentarySeq >> 6) |
                                (SupplimentarySeq >> 2 * 6) | (SupplimentarySeq >> 3 * 6);
                        }
                        else
                        {
                            // 3 byte encoding
                            ch = (ch & 0x0F) | ((FinalByte >> 2 * 6) | (1 << 30) |
                                (ThreeByteSeq) | (ThreeByteSeq >> 6) | (ThreeByteSeq >> 2 * 6));
                        }
                    }
                    else
                    {
                        // 2 byte encoding

                        ch &= 0x1F;

                        // check for non-shortest form
                        if (ch <= 1)
                        {
                            ch |= 0xc0;
                            goto InvalidByteSequence;
                        }

                        ch |= (FinalByte >> 6);
                    }
                    continue;
                }

                EncodeChar:
                // write the pending character
                if (pTarget >= pAllocatedBufferEnd)
                {
                    // Fix chars so we make sure to throw if we didn't output anything
                    ch &= 0x1fffff;
                    if (ch > 0x7f)
                    {
                        if (ch > 0x7ff)
                        {
                            if (ch >= CharUnicodeInfo.LOW_SURROGATE_START &&
                                ch <= CharUnicodeInfo.LOW_SURROGATE_END)
                            {
                                pSrc--;     // It was 4 bytes
                                pTarget--;  // 1 was stored already, but we can't remember 1/2, so back up
                            }
                            else if (ch > 0xffff)
                            {
                                pSrc--;     // It was 4 bytes, nothing was stored
                            }
                            pSrc--;         // It was at least 3 bytes
                        }
                        pSrc--;             // It was at least 2 bytes
                    }
                    pSrc--;

                    // Throw that we don't have enough room (pSrc could be < chars if we had started to process
                    // a 4 byte sequence alredy)
                    Debug.Assert(pSrc >= bytes || pTarget == chars);
                    if (pTarget == chars)
                    {
                        // Overflow, buffer too short.
                        throw new UTF8DecodeException();
                    }

                    // Don't store ch in decoder, we already backed up to its start
                    ch = 0;

                    // Didn't throw, just use this buffer size.
                    break;
                }
                *pTarget = (char)ch;
                pTarget++;

#if FASTLOOP
                int availableChars = PtrDiff(pAllocatedBufferEnd, pTarget);
                int availableBytes = PtrDiff(pEnd, pSrc);

                // don't fall into the fast decoding loop if we don't have enough bytes
                // Test for availableChars is done because pStop would be <= pTarget.
                if (availableBytes <= 13)
                {
                    // we may need as many as 1 character per byte
                    if (availableChars < availableBytes)
                    {
                        // not enough output room.  no pending bits at this point
                        ch = 0;
                        continue;
                    }

                    // try to get over the remainder of the ascii characters fast though
                    byte* pLocalEnd = pEnd; // hint to get pLocalEnd enregistered
                    while (pSrc < pLocalEnd)
                    {
                        ch = *pSrc;
                        pSrc++;

                        if (ch > 0x7F)
                            goto ProcessChar;

                        *pTarget = (char)ch;
                        pTarget++;
                    }
                    // we are done
                    ch = 0;
                    break;
                }

                // we may need as many as 1 character per byte, so reduce the byte count if necessary.
                // If availableChars is too small, pStop will be before pTarget and we won't do fast loop.
                if (availableChars < availableBytes)
                {
                    availableBytes = availableChars;
                }

                // To compute the upper bound, assume that all characters are ASCII characters at this point,
                //  the boundary will be decreased for every non-ASCII character we encounter
                // Also, we need 7 chars reserve for the unrolled ansi decoding loop and for decoding of multibyte sequences
                char* pStop = pTarget + availableBytes - 7;

                while (pTarget < pStop)
                {
                    ch = *pSrc;
                    pSrc++;

                    if (ch > 0x7F)
                    {
                        goto LongCode;
                    }
                    *pTarget = (char)ch;
                    pTarget++;

                    // get pSrc to be 2-byte aligned
                    if ((unchecked((int)pSrc) & 0x1) != 0)
                    {
                        ch = *pSrc;
                        pSrc++;
                        if (ch > 0x7F)
                        {
                            goto LongCode;
                        }
                        *pTarget = (char)ch;
                        pTarget++;
                    }

                    // get pSrc to be 4-byte aligned
                    if ((unchecked((int)pSrc) & 0x2) != 0)
                    {
                        ch = *(ushort*)pSrc;
                        if ((ch & 0x8080) != 0)
                        {
                            goto LongCodeWithMask16;
                        }

                        // Unfortunately, this is endianess sensitive
#if BIGENDIAN
                        *pTarget = (char)((ch >> 8) & 0x7F);
                        pSrc += 2;
                        *(pTarget + 1) = (char)(ch & 0x7F);
                        pTarget += 2;
#else // BIGENDIAN
                        *pTarget = (char)(ch & 0x7F);
                        pSrc += 2;
                        *(pTarget + 1) = (char)((ch >> 8) & 0x7F);
                        pTarget += 2;
#endif // BIGENDIAN
                    }

                    // Run 8 characters at a time!
                    while (pTarget < pStop)
                    {
                        ch = *(int*)pSrc;
                        int chb = *(int*)(pSrc + 4);
                        if (((ch | chb) & unchecked((int)0x80808080)) != 0)
                        {
                            goto LongCodeWithMask32;
                        }

                        // Unfortunately, this is endianess sensitive
#if BIGENDIAN
                        *pTarget = (char)((ch >> 24) & 0x7F);
                        *(pTarget + 1) = (char)((ch >> 16) & 0x7F);
                        *(pTarget + 2) = (char)((ch >> 8) & 0x7F);
                        *(pTarget + 3) = (char)(ch & 0x7F);
                        pSrc += 8;
                        *(pTarget + 4) = (char)((chb >> 24) & 0x7F);
                        *(pTarget + 5) = (char)((chb >> 16) & 0x7F);
                        *(pTarget + 6) = (char)((chb >> 8) & 0x7F);
                        *(pTarget + 7) = (char)(chb & 0x7F);
                        pTarget += 8;
#else // BIGENDIAN
                        *pTarget = (char)(ch & 0x7F);
                        *(pTarget + 1) = (char)((ch >> 8) & 0x7F);
                        *(pTarget + 2) = (char)((ch >> 16) & 0x7F);
                        *(pTarget + 3) = (char)((ch >> 24) & 0x7F);
                        pSrc += 8;
                        *(pTarget + 4) = (char)(chb & 0x7F);
                        *(pTarget + 5) = (char)((chb >> 8) & 0x7F);
                        *(pTarget + 6) = (char)((chb >> 16) & 0x7F);
                        *(pTarget + 7) = (char)((chb >> 24) & 0x7F);
                        pTarget += 8;
#endif // BIGENDIAN
                    }
                    break;

#if BIGENDIAN
                LongCodeWithMask32:
                    // be careful about the sign extension
                    ch = (int)(((uint)ch) >> 16);
                LongCodeWithMask16:
                    ch = (int)(((uint)ch) >> 8);
#else // BIGENDIAN
                LongCodeWithMask32:
                LongCodeWithMask16:
                    ch &= 0xFF;
#endif // BIGENDIAN
                    pSrc++;
                    if (ch <= 0x7F)
                    {
                        *pTarget = (char)ch;
                        pTarget++;
                        continue;
                    }

                LongCode:
                    int chc = *pSrc;
                    pSrc++;

                    if (
                        // bit 6 has to be zero
                        (ch & 0x40) == 0 ||
                        // we are expecting to see trailing bytes like 10vvvvvv
                        (chc & unchecked((sbyte)0xC0)) != 0x80)
                    {
                        goto BadLongCode;
                    }

                    chc &= 0x3F;

                    // start a new long code
                    if ((ch & 0x20) != 0)
                    {
                        // fold the first two bytes together
                        chc |= (ch & 0x0F) << 6;

                        if ((ch & 0x10) != 0)
                        {
                            // 4 byte encoding - surrogate
                            ch = *pSrc;
                            if (
                                // check that bit 4 is zero, the non-shortest form of surrogate
                                // and the valid surrogate range 0x000000 - 0x10FFFF at the same time
                                !InRange(chc >> 4, 0x01, 0x10) ||
                                // we are expecting to see trailing bytes like 10vvvvvv
                                (ch & unchecked((sbyte)0xC0)) != 0x80)
                            {
                                goto BadLongCode;
                            }

                            chc = (chc << 6) | (ch & 0x3F);

                            ch = *(pSrc + 1);
                            // we are expecting to see trailing bytes like 10vvvvvv
                            if ((ch & unchecked((sbyte)0xC0)) != 0x80)
                            {
                                goto BadLongCode;
                            }
                            pSrc += 2;

                            ch = (chc << 6) | (ch & 0x3F);

                            *pTarget = (char)(((ch >> 10) & 0x7FF) +
                                unchecked((short)(CharUnicodeInfo.HIGH_SURROGATE_START - (0x10000 >> 10))));
                            pTarget++;

                            ch = (ch & 0x3FF) +
                                unchecked((short)(CharUnicodeInfo.LOW_SURROGATE_START));

                            // extra byte, we're already planning 2 chars for 2 of these bytes,
                            // but the big loop is testing the target against pStop, so we need
                            // to subtract 2 more or we risk overrunning the input.  Subtract 
                            // one here and one below.
                            pStop--;
                        }
                        else
                        {
                            // 3 byte encoding
                            ch = *pSrc;
                            if (
                                // check for non-shortest form of 3 byte seq
                                (chc & (0x1F << 5)) == 0 ||
                                // Can't have surrogates here.
                                (chc & (0xF800 >> 6)) == (0xD800 >> 6) ||
                                // we are expecting to see trailing bytes like 10vvvvvv
                                (ch & unchecked((sbyte)0xC0)) != 0x80)
                            {
                                goto BadLongCode;
                            }
                            pSrc++;

                            ch = (chc << 6) | (ch & 0x3F);

                            // extra byte, we're only expecting 1 char for each of these 3 bytes,
                            // but the loop is testing the target (not source) against pStop, so
                            // we need to subtract 2 more or we risk overrunning the input.
                            // Subtract 1 here and one more below
                            pStop--;
                        }
                    }
                    else
                    {
                        // 2 byte encoding

                        ch &= 0x1F;

                        // check for non-shortest form
                        if (ch <= 1)
                        {
                            goto BadLongCode;
                        }
                        ch = (ch << 6) | chc;
                    }

                    *pTarget = (char)ch;
                    pTarget++;

                    // extra byte, we're only expecting 1 char for each of these 2 bytes,
                    // but the loop is testing the target (not source) against pStop.
                    // subtract an extra count from pStop so that we don't overrun the input.
                    pStop--;
                }
#endif // FASTLOOP

                Debug.Assert(pTarget <= pAllocatedBufferEnd);

                // no pending bits at this point
                ch = 0;
                continue;
#if FASTLOOP
                BadLongCode:
                pSrc -= 2;
                ch = 0;
                continue;
#endif
            }

            if (ch != 0)
            {
                // Invalid byte sequence
                throw new UTF8DecodeException();
            }

            // Shouldn't have anything in fallback buffer for GetChars
            // (don't have to check m_throwOnOverflow for chars)
            Debug.Assert(fallback == null);

            return PtrDiff(pTarget, chars);
        }
    }
}
