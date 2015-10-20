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
     * GB 18030 is super set of Chinese characters code standard. one-byte, two-byte and four-byte encoding systems are adopted
     * 
     * -------------------------------------------------------------------------------------------------------------------------
     *                                      Allocation of Code Range
     * -------------------------------------------------------------------------------------------------------------------------
     *      Number of Bytes             Space of Code Positions Number of                           Codes
     * -------------------------------------------------------------------------------------------------------------------------
     *          One-byte                    0x00-0x80                                               129 codes
     * -------------------------------------------------------------------------------------------------------------------------
     *          Two-byte                First byte      Second byte                                 23,940 codes
     *                                  0x81~0xFE       0x40~0x7E
     *                                                  0x80~0xFE
     * -------------------------------------------------------------------------------------------------------------------------
     *          Four-byte               First byte      Second byte     Third byte  Fourth byte     1,587,600 codes
     *                                  0x81~0xFE       0x30~0x39       0x81~0xFE   0x30~0x39
     * -------------------------------------------------------------------------------------------------------------------------
     */

    internal class DecoderGB18030 : Decoder
    {
        // Remember our encoding
        protected Encoding m_encoding;
        internal int m_bytesUsed;

        const byte LEAD_BYTE_START = 0x81;
        const byte LEAD_BYTE_END = 0xFE;

        const byte SECOND_BYTE_IN_2BYTES_START_1 = 0x40;
        const byte SECOND_BYTE_IN_2BYTES_END_1 = 0x7E;

        const byte SECOND_BYTE_IN_2BYTES_START_2 = 0x80;
        const byte SECOND_BYTE_IN_2BYTES_END_2 = 0xFE;

        const byte SECOND_AND_FOURTH_BYTE_IN_4BYTES_START = 0x30;
        const byte SECOND_AND_FOURTH_BYTE_IN_4BYTES_END = 0x39;

        private int _leftOverByteCount;         // Max 3
        private byte[] _leftOverLeadBytes = new byte[3];

        internal DecoderGB18030(Encoding encoding)
        {
            this.m_encoding = encoding;
            this.m_fallback = this.m_encoding.DecoderFallback;
            this.Reset();
        }

        public override void Reset()
        {
            _leftOverByteCount = 0;
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

            if (byteCount == 0)
                return 0;

            //
            // Handle previous state first
            //

            int index = 0;
            if (_leftOverByteCount == 1)
            {
                Debug.Assert(IsLeadByte(_leftOverLeadBytes[0]));
                Debug.Assert(index < byteCount);

                // 2-bytes encoding
                if (IsSecond2BytesEncoding(bytes[index]))
                {
                    index++;
                }
                else if (IsSecond4BytesEncoding(bytes[index]))
                {
                    // 4-bytes
                    if (index + 1 < byteCount)
                    {
                        if (IsLeadByte(bytes[index + 1]))
                        {
                            if (index + 2 < byteCount)
                            {
                                if (IsSecond4BytesEncoding(bytes[index + 2]))
                                {
                                    index += 3;
                                }
                            }
                            else
                            {
                                _leftOverLeadBytes[1] = bytes[index];
                                _leftOverLeadBytes[2] = bytes[index + 1];
                                _leftOverByteCount += 2;
                                return 0;
                            }
                        }
                    }
                    else
                    {
                        _leftOverLeadBytes[1] = bytes[index];
                        _leftOverByteCount++;
                        return 0;
                    }
                }
                // invalid sequence
            }
            else if (_leftOverByteCount == 2)
            {
                // should be 4 bytes encoding 
                Debug.Assert(IsLeadByte(_leftOverLeadBytes[0]) && IsSecond4BytesEncoding(_leftOverLeadBytes[1]));
                Debug.Assert(index < byteCount);
                if (IsLeadByte(bytes[index]))
                {
                    if (index + 1 < byteCount)
                    {
                        if (IsSecond4BytesEncoding(bytes[index + 1]))
                        {
                            index += 2;
                        }
                    }
                    else
                    {
                        _leftOverLeadBytes[2] = bytes[index];
                        _leftOverByteCount++;
                        return 0;
                    }
                }
            }
            else if (_leftOverByteCount == 3)
            {
                // should be 4 bytes encoding too
                Debug.Assert(IsLeadByte(_leftOverLeadBytes[0]) && IsSecond4BytesEncoding(_leftOverLeadBytes[1]) && IsLeadByte(_leftOverLeadBytes[2]));
                if (IsSecond4BytesEncoding(bytes[index]))
                {
                    index++;
                }
            }

            //
            // We should get here with index pointing to position in the buffer where to start checking for new bytes encoding
            //

            int newLeftOverByteCount = 0;

            while (index < byteCount)
            {
                if (IsLeadByte(bytes[index]))
                {
                    if (index < byteCount - 1)
                    {
                        if (IsSecond2BytesEncoding(bytes[index + 1]))
                        {
                            index++; // 2 bytes encoding
                        }
                        else if (IsSecond4BytesEncoding(bytes[index + 1]))
                        {
                            if (index < byteCount - 2)
                            {
                                if (IsLeadByte(bytes[index + 2]))
                                {
                                    if (index < byteCount - 3)
                                    {
                                        if (IsSecond4BytesEncoding(bytes[index + 3]))
                                        {
                                            index += 3;
                                        }
                                    }
                                    else
                                    {
                                        newLeftOverByteCount = 3;
                                    }
                                }
                            }
                            else
                            {
                                newLeftOverByteCount = 2;
                            }
                        }
                    }
                    else
                    {
                        newLeftOverByteCount = 1;
                    }
                }
                index++;
            }

            //
            // now call encoding with the information we have 
            //
            int res;

            if (_leftOverByteCount > 0)
            {
                byte[] inputBytes = new byte[byteCount + _leftOverByteCount - newLeftOverByteCount];
                index = 0;
                while (index < _leftOverByteCount)
                {
                    inputBytes[index] = _leftOverLeadBytes[index];
                    index++;
                }

                fixed (byte* pBytes = inputBytes)
                {
                    RuntimeImports.memmove(pBytes + index, bytes, byteCount - newLeftOverByteCount);
                    res = Interop.mincore.GetChars(m_encoding.CodePage, pBytes, inputBytes.Length, chars, charCount);
                }
            }
            else
            {
                if (byteCount - newLeftOverByteCount > 0)
                    res = Interop.mincore.GetChars(m_encoding.CodePage, bytes, byteCount - newLeftOverByteCount, chars, charCount);
                else
                    res = 0;
            }

            _leftOverByteCount = newLeftOverByteCount;
            while (newLeftOverByteCount > 0)
            {
                _leftOverLeadBytes[_leftOverByteCount - newLeftOverByteCount] = bytes[byteCount - newLeftOverByteCount];
                newLeftOverByteCount--;
            }

            return res;
        }

        bool IsLeadByte(byte b)
        {
            return b >= LEAD_BYTE_START && b <= LEAD_BYTE_END;
        }

        bool IsSecond2BytesEncoding(byte b)
        {
            return (b >= SECOND_BYTE_IN_2BYTES_START_1 && b <= SECOND_BYTE_IN_2BYTES_END_1) ||
                   (b >= SECOND_BYTE_IN_2BYTES_START_2 && b <= SECOND_BYTE_IN_2BYTES_END_2);
        }

        bool IsSecond4BytesEncoding(byte b)
        {
            return b >= SECOND_AND_FOURTH_BYTE_IN_4BYTES_START && b <= SECOND_AND_FOURTH_BYTE_IN_4BYTES_END;
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

        private unsafe int Flush(byte* bytes, int byteCount, char* chars, int charCount)
        {
            if (_leftOverByteCount > 0)
            {
                byte[] inputBytes = new byte[byteCount + _leftOverByteCount];
                int i = 0;
                while (i < _leftOverByteCount)
                {
                    inputBytes[i] = _leftOverLeadBytes[i];
                    i++;
                }

                Reset();

                fixed (byte* pBytes = inputBytes)
                {
                    RuntimeImports.memmove(pBytes + i, bytes, byteCount);
                    return Interop.mincore.GetChars(m_encoding.CodePage, pBytes, inputBytes.Length, chars, charCount);
                }
            }

            Reset();
            return Interop.mincore.GetChars(m_encoding.CodePage, bytes, byteCount, chars, charCount);
        }
    }
}
