// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime;

namespace System.Text
{
    /*
     If hz-gb-2312 lead is 0x7E, set hz-gb-2312 lead to 0x00, and based on byte: 
        0x7B    Set the hz-gb-2312 flag and continue. 
        0x7D    Unset the hz-gb-2312 flag and continue.  
        0x7E    Emit code point U+007E. 
        0x0A    Continue. 
     */

    internal class Decoder2312HZ : Decoder
    {
        // Remember our encoding
        protected Encoding m_encoding;
        internal int m_bytesUsed;

        DecoderState m_currentState;
        int m_escapeSequence;
        int m_escapeSequenceCount;
        byte m_leftOver;
        bool m_leftOverExist;

        const byte TILDE_SIGN = (byte)'~';
        const byte RIGHT_CURLY_PARANTHESIS = (byte)'}';
        const byte LEFT_CURLY_PARANTHESIS = (byte)'{';
        const byte LINE_FEED = 0xA;

        enum DecoderState
        {
            Ascii,
            HZ
        }

        internal Decoder2312HZ(Encoding encoding)
        {
            this.m_encoding = encoding;
            this.m_fallback = this.m_encoding.DecoderFallback;
            this.Reset();
        }

        public override void Reset()
        {
            m_currentState = DecoderState.Ascii;
            m_escapeSequenceCount = 0;
            m_leftOverExist = false;
        }

        public override unsafe int GetCharCount(byte[] bytes, int index, int count)
        {
            return GetCharCount(bytes, index, count, false);
        }

        [System.Security.SecuritySafeCritical]  // auto-generated
        public override unsafe int GetCharCount(byte[] bytes, int index, int count, bool flush)
        {
            // Validate Parameters
            if (bytes == null)
                throw new ArgumentNullException("bytes", SR.ArgumentNull_Array);

            if (index < 0 || count < 0)
                throw new ArgumentOutOfRangeException((index < 0 ? "index" : "count"), SR.ArgumentOutOfRange_NeedNonNegNum);

            if (bytes.Length - index < count)
                throw new ArgumentOutOfRangeException("bytes", SR.ArgumentOutOfRange_IndexCountBuffer);

            Contract.EndContractBlock();

            // Avoid null fixed problem
            if (bytes.Length == 0)
                bytes = new byte[1];

            // Just call pointer version
            fixed (byte* pBytes = bytes)
                return GetCharCount(pBytes + index, count, flush);
        }

        [System.Security.SecurityCritical]  // auto-generated
        internal unsafe override int GetCharCount(byte* bytes, int count, bool flush)
        {
            // Validate parameters
            if (bytes == null)
                throw new ArgumentNullException("bytes", SR.ArgumentNull_Array);

            if (count < 0)
                throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_NeedNonNegNum);
            Contract.EndContractBlock();

            // By default just call the encoding version, no flush by default
            // return m_encoding.GetCharCount(bytes, count, this);
            return Interop.mincore.GetCharCount(m_encoding.CodePage, bytes, count);
        }

        public override unsafe int GetChars(byte[] bytes, int byteIndex, int byteCount,
                                             char[] chars, int charIndex)
        {
            return GetChars(bytes, byteIndex, byteCount, chars, charIndex, false);
        }

        [System.Security.SecuritySafeCritical]  // auto-generated
        public override unsafe int GetChars(byte[] bytes, int byteIndex, int byteCount,
                                             char[] chars, int charIndex, bool flush)
        {
            // Validate Parameters
            if (bytes == null || chars == null)
                throw new ArgumentNullException(bytes == null ? "bytes" : "chars", SR.ArgumentNull_Array);

            if (byteIndex < 0 || byteCount < 0)
                throw new ArgumentOutOfRangeException((byteIndex < 0 ? "byteIndex" : "byteCount"),
                    SR.ArgumentOutOfRange_NeedNonNegNum);

            if (bytes.Length - byteIndex < byteCount)
                throw new ArgumentOutOfRangeException("bytes", SR.ArgumentOutOfRange_IndexCountBuffer);

            if (charIndex < 0 || charIndex > chars.Length)
                throw new ArgumentOutOfRangeException("charIndex", SR.ArgumentOutOfRange_Index);

            Contract.EndContractBlock();

            // Avoid empty input fixed problem
            if (bytes.Length == 0)
                bytes = new byte[1];

            int charCount = chars.Length - charIndex;
            if (chars.Length == 0)
                chars = new char[1];

            // Just call pointer version
            fixed (byte* pBytes = bytes)
                fixed (char* pChars = chars)
                    return GetChars(pBytes + byteIndex, byteCount, pChars + charIndex, charCount, flush);
        }

        [System.Security.SecurityCritical]  // auto-generated
        internal unsafe override int GetChars(byte* bytes, int byteCount, char* chars, int charCount, bool flush)
        {
            // Validate parameters
            if (chars == null || bytes == null)
                throw new ArgumentNullException((chars == null ? "chars" : "bytes"), SR.ArgumentNull_Array);

            if (byteCount < 0 || charCount < 0)
                throw new ArgumentOutOfRangeException((byteCount < 0 ? "byteCount" : "charCount"), SR.ArgumentOutOfRange_NeedNonNegNum);
            Contract.EndContractBlock();

            if (flush)
            {
                return Flush(bytes, byteCount, chars, charCount);
            }

            DecoderState esc = DecoderState.Ascii;
            bool incomplete = false;
            int length = 0;
            bool prependTilde = false;

            if (m_escapeSequenceCount > 0)
            {
                if (bytes[0] == TILDE_SIGN)
                {
                    m_escapeSequenceCount = 0;
                    prependTilde = true;
                }
                else
                {
                    m_escapeSequence |= (int)(bytes[0] << 8);

                    fixed (int* pBytes = &m_escapeSequence)
                    {
                        int escSequance = 0;

                        if (CheckEscSequenceAt((byte*)pBytes, 2, ref escSequance, ref esc, ref incomplete, ref length))
                        {
                            Debug.Assert(incomplete == false);

                            m_currentState = esc;
                            Debug.Assert(length >= m_escapeSequenceCount);
                            bytes += length - m_escapeSequenceCount;
                            byteCount -= length - m_escapeSequenceCount;
                            Debug.Assert(byteCount >= 0);
                            m_escapeSequenceCount = 0;
                            if (byteCount == 0)
                                return 0;
                        }
                        else
                        {
                            prependTilde = true; // invalid sequence
                            m_escapeSequenceCount = 0;
                        }
                    }
                }
            }

            byte leftOver = 0;
            bool leftOverExist = false;

            int index = GetLastValidEsc(bytes, byteCount, out esc, out incomplete, out length);
            if (index < 0) // no esc sequence found in the buffer
            {
                if (m_currentState == DecoderState.HZ && (byteCount & 1) == 1) // odd count
                {
                    leftOver = bytes[byteCount - 1];
                    leftOverExist = true;
                    byteCount--;
                }
                esc = m_currentState;
            }
            else if (incomplete) // found partial esc sequence at the end of the buffer
            {
                // the incomplete sequence should be at the end of the buffer. we'll not treat it as esc sequence in the cases
                //      o       m_leftOverExist is true, means ~ is just the trail byte
                //      o       prependTilde is true and index is 0, this means we are in case of having ~~
                if (!m_leftOverExist && (!prependTilde || index != 0))
                {
                    Debug.Assert(length == 1);
                    Debug.Assert(bytes[byteCount - 1] == TILDE_SIGN);

                    m_escapeSequence = (int)TILDE_SIGN;
                    m_escapeSequenceCount = 1;
                    byteCount--;
                }
                esc = m_currentState;
            }
            else // found complete esc sequence
            {
                Debug.Assert(length == 2);
                if (esc == DecoderState.HZ && ((byteCount - index - 2) & 1) == 1)
                {
                    leftOver = bytes[byteCount - 1];
                    leftOverExist = true;
                    byteCount--;
                }
            }

            if (!prependTilde && byteCount == 0 && (!m_leftOverExist || !leftOverExist))
            {
                m_currentState = esc;

                if (leftOverExist)
                {
                    m_leftOver = leftOver;
                    m_leftOverExist = true;
                }
                return 0;
            }

            int count = byteCount + 3; // +2 for leftover, +1 for prependTilde

            if (m_currentState != DecoderState.Ascii)
            {
                count += GetEscSequanceLength(m_currentState);
            }

            index = 0;
            byte[] buffer = new byte[count];

            if (m_currentState != DecoderState.Ascii)
            {
                index = CopyEscSequenceToBuffer(buffer, m_currentState);
            }
            m_currentState = esc; // set new state

            if (prependTilde)
                buffer[index++] = TILDE_SIGN;

            if (m_leftOverExist)
            {
                buffer[index++] = m_leftOver;
            }

            if (byteCount > 0)
            {
                fixed (byte* pBuffer = buffer)
                {
                    RuntimeImports.memmove(pBuffer + index, bytes, byteCount);
                    index += byteCount;
                }
            }

            if (leftOverExist)
            {
                if (m_leftOverExist)
                {
                    buffer[index++] = leftOver;
                    m_leftOverExist = false;
                }
                else
                {
                    m_leftOverExist = true;
                    m_leftOver = leftOver;
                }
            }
            else
            {
                m_leftOverExist = false;
            }

            fixed (byte* pBuffer = buffer)
            {
                return Interop.mincore.GetChars(m_encoding.CodePage, pBuffer, index, chars, charCount);
            }
        }

        // This method is used when the output buffer might not be big enough.
        // Just call the pointer version.  (This gets chars)
        [System.Security.SecuritySafeCritical]  // auto-generated
        public override unsafe void Convert(byte[] bytes, int byteIndex, int byteCount,
                                              char[] chars, int charIndex, int charCount, bool flush,
                                              out int bytesUsed, out int charsUsed, out bool completed)
        {
            // Validate parameters
            if (bytes == null || chars == null)
                throw new ArgumentNullException((bytes == null ? "bytes" : "chars"), SR.ArgumentNull_Array);

            if (byteIndex < 0 || byteCount < 0)
                throw new ArgumentOutOfRangeException((byteIndex < 0 ? "byteIndex" : "byteCount"), SR.ArgumentOutOfRange_NeedNonNegNum);

            if (charIndex < 0 || charCount < 0)
                throw new ArgumentOutOfRangeException((charIndex < 0 ? "charIndex" : "charCount"), SR.ArgumentOutOfRange_NeedNonNegNum);

            if (bytes.Length - byteIndex < byteCount)
                throw new ArgumentOutOfRangeException("bytes", SR.ArgumentOutOfRange_IndexCountBuffer);

            if (chars.Length - charIndex < charCount)
                throw new ArgumentOutOfRangeException("chars", SR.ArgumentOutOfRange_IndexCountBuffer);

            Contract.EndContractBlock();

            // Avoid empty input problem
            if (bytes.Length == 0)
                bytes = new byte[1];
            if (chars.Length == 0)
                chars = new char[1];

            // Just call the pointer version (public overrides can't do this)
            fixed (byte* pBytes = bytes)
            {
                fixed (char* pChars = chars)
                {
                    Convert(pBytes + byteIndex, byteCount, pChars + charIndex, charCount, flush, out bytesUsed, out charsUsed, out completed);
                }
            }
        }

        // This is the version that used pointers.  We call the base encoding worker function
        // after setting our appropriate internal variables.  This is getting chars
        [System.Security.SecurityCritical]  // auto-generated
        internal unsafe void Convert(byte* bytes, int byteCount,
                                              char* chars, int charCount, bool flush,
                                              out int bytesUsed, out int charsUsed, out bool completed)
        {
            // Validate input parameters
            if (chars == null || bytes == null)
                throw new ArgumentNullException(chars == null ? "chars" : "bytes", SR.ArgumentNull_Array);
            if (byteCount < 0 || charCount < 0)
                throw new ArgumentOutOfRangeException((byteCount < 0 ? "byteCount" : "charCount"), SR.ArgumentOutOfRange_NeedNonNegNum);
            Contract.EndContractBlock();

            // We don't want to throw
            this.m_bytesUsed = 0;

            // Do conversion
            charsUsed = Interop.mincore.GetChars(m_encoding.CodePage, bytes, byteCount, chars, charCount);

            bytesUsed = this.m_bytesUsed;

            // Its completed if they've used what they wanted AND if they didn't want flush or if we are flushed
            completed = (bytesUsed == byteCount) && !flush;
        }

        private int GetEscSequanceLength(DecoderState state)
        {
            Debug.Assert(state == DecoderState.HZ);
            return 2; // ~{
        }

        // return number of bytes copied to the buffer
        private int CopyEscSequenceToBuffer(byte[] bytesBuffer, DecoderState state)
        {
            Debug.Assert(state == DecoderState.HZ);

            bytesBuffer[0] = TILDE_SIGN;
            bytesBuffer[1] = LEFT_CURLY_PARANTHESIS;
            return 2;
        }

        private void CopyIncompleteEscSequence(byte[] bytesBuffer, ref int index)
        {
            for (int i = 0; i < m_escapeSequenceCount; i++)
                bytesBuffer[index++] = (byte)(m_escapeSequence >> (8 * i));
        }

        private unsafe int Flush(byte* bytes, int byteCount, char* chars, int charCount)
        {
            int moreBytesToAllocate;
            byte[] bytesBuffer;

            // we just need to flush everything 
            if (m_currentState != DecoderState.Ascii)
            {
                moreBytesToAllocate = GetEscSequanceLength(m_currentState) + m_escapeSequenceCount;

                bytesBuffer = new byte[byteCount + moreBytesToAllocate + 1]; //+1: one byte for leftover

                int index = CopyEscSequenceToBuffer(bytesBuffer, m_currentState);

                if (m_leftOverExist)
                    bytesBuffer[index++] = m_leftOver;
                CopyIncompleteEscSequence(bytesBuffer, ref index);

                Reset();

                fixed (byte* pBytes = bytesBuffer)
                {
                    RuntimeImports.memmove(pBytes + index, bytes, byteCount);
                    return Interop.mincore.GetChars(m_encoding.CodePage, pBytes, byteCount + index, chars, charCount);
                }
            }
            else if (m_escapeSequenceCount > 0)
            {
                Debug.Assert(m_escapeSequenceCount == 1);
                bytesBuffer = new byte[byteCount + m_escapeSequenceCount];
                int index = 0;
                CopyIncompleteEscSequence(bytesBuffer, ref index);

                Reset();

                fixed (byte* pBytes = bytesBuffer)
                {
                    RuntimeImports.memmove(pBytes + m_escapeSequenceCount, bytes, byteCount);
                    return Interop.mincore.GetChars(m_encoding.CodePage, pBytes, byteCount + index, chars, charCount);
                }
            }
            return Interop.mincore.GetChars(m_encoding.CodePage, bytes, byteCount, chars, charCount);
        }

        unsafe int GetLastValidEsc(byte* bytes, int byteCount, out DecoderState esc, out bool incomplete, out int length)
        {
            incomplete = false;
            esc = DecoderState.Ascii;

            length = 0;
            int escSequence = 0;
            int lastIncompleteSequance = -1;

            int index = byteCount;

            while (--index >= 0)
            {
                if (bytes[index] == TILDE_SIGN)
                {
                    int tildeCount = 1;

                    while (index - tildeCount >= 0 && bytes[index - tildeCount] == TILDE_SIGN)
                    {
                        tildeCount++;
                    }

                    if ((tildeCount & 1) == 0) //even number then ignore all ~~ pattern
                    {
                        index -= tildeCount - 1;
                        continue;
                    }

                    if (CheckEscSequenceAt(bytes + index, byteCount - index, ref escSequence, ref esc, ref incomplete, ref length))
                    {
                        if (incomplete)
                        {
                            Debug.Assert(length == 1);
                            if (lastIncompleteSequance < 0)
                            {
                                lastIncompleteSequance = index;
                            }

                            index -= tildeCount - 1;
                            continue;
                        }

                        Debug.Assert(length > 1);
                        return index; // found one
                    }

                    index -= tildeCount - 1;
                }
            }

            return lastIncompleteSequance; // either -1 for not found any or positive number for index of incomplete esc sequence
        }

        unsafe bool CheckEscSequenceAt(byte* bytes, int byteCount, ref int escSequance, ref DecoderState esc, ref bool incomplete, ref int length)
        {
            Debug.Assert(bytes[0] == TILDE_SIGN);

            incomplete = false;
            length = 0;

            if (byteCount > 1)
            {
                Debug.Assert(bytes[1] != TILDE_SIGN); // we shouldn't get this case

                if (bytes[1] == LEFT_CURLY_PARANTHESIS)
                {
                    length = 2;
                    // we don't change the current state
                    esc = DecoderState.HZ;  // ~{
                    return true;
                }
                else if (bytes[1] == RIGHT_CURLY_PARANTHESIS)
                {
                    length = 2;
                    esc = DecoderState.Ascii;  // ~}
                    return true;
                }
                else if (bytes[1] == LINE_FEED)
                {
                    length = 2;
                    esc = m_currentState;  // ~\n we just ignore it
                    return true;
                }
                else
                {
                    return false; // invalid sequence
                }
            }

            // we just have One character
            escSequance = (int)TILDE_SIGN;
            length = 1;
            incomplete = true;
            return true;
        }
    }
}
