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
        Charset name: CP50220

        Charset aliases: csCP50220

        Suitability for use in MIME text:

          Yes, CP50220 is suitable for use with subtypes of the "text" Content-Type.

          Since the "CP50220" is 7bit encoding, Content-Transfer-Encoding is not needed.
          Based64 or Quoted-Printable encoding MAY break this encoding.

        Published specification(s):

          CP50220 is consisted by following character sets:

            reg#  character set       ESC sequence  designated to
            ------------------------------------------------------
            6     US-ASCII            ESC ( B       G0
            13    JIS X 0201-Katakana ESC ( I       G0
            14    JIS X 0201-Roman    ESC ( J       G0
            42    JIS X 0208-1978     ESC $ @       G0
            87    JIS X 0208-1983     ESC $ B       G0
            13    JIS X 0201-Katakana ESC ) I       G1
            reg#  character set       shift in with designated to
            ------------------------------------------------------
            6     US-ASCII            SI            G0
            13    JIS X 0201-Katakana SO            G0

          * The beggining of a text is assumed to have "ESC ( B ESC ) I".
          * Each line of CP50220 text MUST end with ASCII.
          * There are two kinds of shifts: SI and SO. Shift functions
            specify how to interpret the subsequent bytes.
          * The shift SI (one byte with hexadecimal value 0F) declares that
            subsequent bytes are interpreted in US-ASCII.
          * The shift SO (one byte with hexadecimal value 0E) declares that
            subsequent bytes are interpreted in JIS X 0201 Katakana.
          * On receiving JIS X 0201-Katakana characters MAY be encoded as
            * GL with the escape sequence: ESC ( I
            * GL with the shifts: SI / SO
            * GR
          * On sending JIS X 0201-Katakana, it MUST be converted to related
            character of JIS X 0208.

    */
    internal class DecoderIso2022 : Decoder
    {
        // Remember our encoding
        protected Encoding m_encoding;
        internal int m_bytesUsed;

        DecoderState m_currentState;
        int m_escapeSequence;
        int m_escapeSequenceCount;
        byte m_leftOver;
        bool m_leftOverExist;

        const byte ESC = 0x1B;
        const byte DOLLAR_SIGN = (byte)'$';
        const byte AT_SIGN = (byte)'@';
        const byte AMPERSAND_SIGN = (byte)'&';
        const byte LEFT_PARANTHESIS = (byte)'(';
        const byte CAPITAL_B = (byte)'B';
        const byte CAPITAL_D = (byte)'D';
        const byte CAPITAL_H = (byte)'H';
        const byte CAPITAL_I = (byte)'I';
        const byte CAPITAL_J = (byte)'J';

        const byte SHIFT_OUT = 0x0E;
        const byte SHIFT_IN = 0x0F;

        enum DecoderState
        {
            Ascii,
            JIS0208_1,              // <Esc>$@
            JIS0208_2,              // <Esc>$B
            JIS0208_3,              // <Esc>$(D
            HalfWidthKatakana       // <Esc>(I
        }

        internal DecoderIso2022(Encoding encoding)
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

            int prependBytes = 0;
            int prependBytesCount = 0;
            DecoderState esc = DecoderState.Ascii;
            bool incomplete = false;
            int length = 0;

            if (m_escapeSequenceCount > 0)
            {
                int c = m_escapeSequenceCount;
                int bc = 0;

                while (c < 4 && bc < byteCount)
                {
                    m_escapeSequence |= (int)(bytes[bc++] << (8 * c++));
                }

                fixed (int* pBytes = &m_escapeSequence)
                {
                    int escSequance = 0;

                    if (CheckEscSequenceAt((byte*)pBytes, c, ref escSequance, ref esc, ref incomplete, ref length))
                    {
                        if (incomplete)
                        {
                            m_escapeSequence = escSequance;
                            m_escapeSequenceCount = length;
                            return 0;
                        }

                        m_currentState = esc;
                        Debug.Assert(length > m_escapeSequenceCount);
                        bytes += length - m_escapeSequenceCount;
                        byteCount -= length - m_escapeSequenceCount;
                        Debug.Assert(byteCount >= 0);
                        m_escapeSequenceCount = 0;
                        if (byteCount == 0)
                            return 0;
                    }
                    else
                    {
                        prependBytes = m_escapeSequence;
                        prependBytesCount = m_escapeSequenceCount;
                        m_escapeSequenceCount = 0;
                    }
                }
            }

            byte leftOver = 0;
            bool leftOverExist = false;

            int index = GetLastValidEsc(bytes, byteCount, out esc, out incomplete, out length);
            if (index < 0) // no esc sequence found in the buffer
            {
                if ((m_currentState == DecoderState.JIS0208_1 || m_currentState == DecoderState.JIS0208_2 || m_currentState == DecoderState.JIS0208_3) &&
                    (byteCount & 1) == 1) // odd count
                {
                    leftOver = bytes[byteCount - 1];
                    leftOverExist = true;
                    byteCount--;
                }
                esc = m_currentState;
            }
            else if (incomplete) // found partial esc sequence at the end of the buffer
            {
                for (int i = 0; i < length; i++)
                {
                    m_escapeSequence |= ((int)(bytes[byteCount - length]) << (8 * i));
                }

                m_escapeSequenceCount = length;
                byteCount -= length;
                esc = m_currentState;
            }
            else // found complete esc sequence
            {
                if ((esc == DecoderState.JIS0208_1 || esc == DecoderState.JIS0208_2 || esc == DecoderState.JIS0208_3) &&
                    ((byteCount - index - length) & 1) == 1)
                {
                    leftOver = bytes[byteCount - 1];
                    leftOverExist = true;
                    byteCount--;
                }

                if (length == 1 && index == byteCount - 1) // usually this is SHIFT_IN or SHIFT_OUT
                {
                    byteCount--; // don't include it at the end of the buffer
                }
            }

            if (prependBytesCount == 0 && byteCount == 0 && (!m_leftOverExist || !leftOverExist))
            {
                m_currentState = esc;

                if (leftOverExist)
                {
                    m_leftOver = leftOver;
                    m_leftOverExist = true;
                }
                return 0;
            }

            int count = prependBytesCount + byteCount + 2; // +2 for leftover 

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

            for (int i = 0; i < prependBytesCount; i++)
            {
                buffer[index++] = (byte)(prependBytes >> (i * 8));
            }

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
            Debug.Assert(state != DecoderState.Ascii);
            if (state != DecoderState.JIS0208_3)
                return 4;
            else
                return 3;
        }

        // return number of bytes copied to the buffer
        private int CopyEscSequenceToBuffer(byte[] bytesBuffer, DecoderState state)
        {
            bytesBuffer[0] = ESC;

            int index = 0;

            switch (state)
            {
                case DecoderState.JIS0208_1:        // <Esc>$@
                    bytesBuffer[1] = DOLLAR_SIGN;
                    bytesBuffer[2] = AT_SIGN;
                    index = 3;
                    break;

                case DecoderState.JIS0208_2:        // <Esc>$B
                    bytesBuffer[1] = DOLLAR_SIGN;
                    bytesBuffer[2] = CAPITAL_B;
                    index = 3;
                    break;

                case DecoderState.HalfWidthKatakana: // <Esc>(I
                    bytesBuffer[1] = LEFT_PARANTHESIS;
                    bytesBuffer[2] = CAPITAL_I;
                    index = 3; break;

                case DecoderState.JIS0208_3:        // <Esc>$(D
                    bytesBuffer[1] = DOLLAR_SIGN;
                    bytesBuffer[2] = LEFT_PARANTHESIS;
                    bytesBuffer[3] = CAPITAL_D;
                    index = 4;
                    break;

                default:
                    Debug.Assert(false); // we are not expecting any state here
                    break;
            }

            return index;
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
                if (bytes[index] == ESC)
                {
                    if (CheckEscSequenceAt(bytes + index, byteCount - index, ref escSequence, ref esc, ref incomplete, ref length))
                    {
                        if (incomplete)
                        {
                            if (lastIncompleteSequance < 0)
                            {
                                lastIncompleteSequance = index;
                                m_escapeSequence = escSequence;
                                m_escapeSequenceCount = length;
                            }
                            continue;
                        }

                        return index; // found one
                    }
                }
                else if (bytes[index] == SHIFT_OUT)
                {
                    esc = DecoderState.HalfWidthKatakana;
                    length = 1;
                    return index;
                }
                else if (bytes[index] == SHIFT_IN)
                {
                    esc = DecoderState.Ascii;
                    length = 1;
                    return index;
                }
            }

            return lastIncompleteSequance; // either -1 for not found any or positive number for index of incomplete esc sequence
        }

        unsafe bool CheckEscSequenceAt(byte* bytes, int byteCount, ref int escSequance, ref DecoderState esc, ref bool incomplete, ref int length)
        {
            Debug.Assert(bytes[0] == ESC);

            incomplete = false;
            length = 0;

            if (byteCount > 1)
            {
                if (bytes[1] == LEFT_PARANTHESIS)
                {
                    if (byteCount > 2)
                    {
                        if (bytes[2] == CAPITAL_B ||    // <Esc>(B
                            bytes[2] == CAPITAL_H ||    // <Esc>(H
                            bytes[2] == CAPITAL_J)      // <Esc>(J
                        {
                            esc = DecoderState.Ascii;
                            length = 3;
                            return true;
                        }

                        if (bytes[2] == CAPITAL_I)
                        {
                            length = 3;
                            esc = DecoderState.HalfWidthKatakana; // <Esc>(I
                            return true;
                        }

                        return false;
                    }
                    else
                    {
                        escSequance = (int)ESC;
                        escSequance |= ((int)LEFT_PARANTHESIS) << 8;
                        length = 2;
                        incomplete = true;
                        return true;
                    }
                }
                else if (bytes[1] == DOLLAR_SIGN)
                {
                    if (byteCount > 2)
                    {
                        if (bytes[2] == AT_SIGN)    // <Esc>$@
                        {
                            length = 3;
                            esc = DecoderState.JIS0208_1;
                            return true;
                        }

                        if (bytes[2] == CAPITAL_B)    // <Esc>$B
                        {
                            length = 3;
                            esc = DecoderState.JIS0208_2;
                            return true;
                        }

                        if (bytes[2] == LEFT_PARANTHESIS)
                        {
                            if (byteCount > 3)
                            {
                                if (bytes[3] == CAPITAL_D)
                                {
                                    length = 4;
                                    esc = DecoderState.JIS0208_3;  // <Esc>$(D
                                    return true;
                                }

                                return false;
                            }
                            else
                            {
                                escSequance = (int)ESC;
                                escSequance |= ((int)DOLLAR_SIGN) << 8;
                                escSequance |= ((int)LEFT_PARANTHESIS) << 16;

                                length = 3;
                                incomplete = true;
                                return true;
                            }
                        }
                        return false;
                    }
                    else
                    {
                        escSequance = (int)ESC;
                        escSequance |= ((int)DOLLAR_SIGN) << 8;
                        length = 2;
                        incomplete = true;
                        return true;
                    }
                }
                else if (bytes[1] == AMPERSAND_SIGN)
                {
                    if (byteCount > 2)
                    {
                        return false; // we ignore <Esc>& <anything else>
                    }
                    else
                    {
                        // we'll ignore it later but we need to send the full sequence together
                        escSequance = (int)ESC;
                        escSequance |= ((int)AMPERSAND_SIGN) << 8;

                        length = 2;
                        incomplete = true;
                        return true;
                    }
                }
                else
                {
                    return false;
                }
            }

            // we just have One character
            escSequance = (int)ESC;
            length = 1;
            incomplete = true;
            return true;
        }
    }
}
