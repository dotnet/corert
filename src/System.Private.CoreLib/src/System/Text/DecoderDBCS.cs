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
     * Decode DBCS text to Unicode. DBCS could have Ascii characters in addition ranges which formatted as lead byte followed by trail byte. 
     * The lead bytes ranges is different with different DBCS codepages. Instead of hardcoding the lead byte ranges we call the OS API GetCPInfoEx 
     * to get the range for different codepages 
     */

    internal class DecoderDBCS : Decoder
    {
        // Remember our encoding
        protected Encoding m_encoding;
        internal int m_bytesUsed;

        private byte[] _leadyByteRanges = new byte[10]; // Max 5 ranges
        private int _rangesCount;
        private byte _leftOverLeadByte;

        internal DecoderDBCS(Encoding encoding)
        {
            this.m_encoding = encoding;
            this.m_fallback = this.m_encoding.DecoderFallback;
            InitRanges();
            this.Reset();
        }

        private unsafe void InitRanges()
        {
            /*
             * GetCPInfoEx takes the following struct. but for simplicity we just passing it as array of bytes 
             
                    struct CPINFOEX 
                    {
                        uint MaxCharSize;                    //     4-bytes
                        byte DefaultChar[MAX_DEFAULTCHAR];   //     2-bytes
                        BYTE  LeadByte[MAX_LEADBYTES];       //     12-bytes
                        char UnicodeDefaultChar;             //     2-bytes
                        uint CodePage;                       //     4-bytes
                        char CodePageName[MAX_PATH];         //     2 * 260 = 520-bytes
                    } ;
            */

            byte* codepageInfo = stackalloc byte[544];

            int res = Interop.mincore.GetCPInfoExW(m_encoding.CodePage, 0, codepageInfo);
            if (res == 0)
                throw new InvalidOperationException(SR.InvalidOperation_GetCodepageInfo);

            for (int i = 0; i < 5; i += 2)
            {
                // 6 is the offset of LeadByte[MAX_LEADBYTES]
                if (codepageInfo[6 + i] == 0)
                    break;

                _leadyByteRanges[i] = codepageInfo[6 + i];
                _leadyByteRanges[i + 1] = codepageInfo[6 + i + 1];
                _rangesCount++;
            }
            Debug.Assert(_rangesCount > 0);
        }

        private bool IsLeadByte(byte b)
        {
            if (b < _leadyByteRanges[0])
                return false;
            int i = 0;
            while (i < _rangesCount)
            {
                if (b >= _leadyByteRanges[i * 2] && b <= _leadyByteRanges[i * 2 + 1])
                    return true;
                i++;
            }
            return false;
        }

        public override void Reset()
        {
            _leftOverLeadByte = 0;
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

            int index = 0;
            byte newLeftOverLeadByte = 0;

            if (_leftOverLeadByte != 0)
                index++;

            while (index < byteCount)
            {
                if (IsLeadByte(bytes[index]))
                {
                    if (index >= byteCount - 1)
                    {
                        newLeftOverLeadByte = bytes[index];
                        break;
                    }
                    index++;
                }
                index++;
            }

            int res;
            if (_leftOverLeadByte != 0)
            {
                byte[] inputBytes = new byte[byteCount + 1];
                inputBytes[0] = _leftOverLeadByte;
                _leftOverLeadByte = newLeftOverLeadByte;
                fixed (byte* pBytes = inputBytes)
                {
                    RuntimeImports.memmove(pBytes + 1, bytes, byteCount);
                    res = Interop.mincore.GetChars(m_encoding.CodePage, pBytes, newLeftOverLeadByte != 0 ? byteCount : byteCount + 1, chars, charCount);
                }
            }
            else
            {
                if (newLeftOverLeadByte != 0)
                {
                    if (byteCount <= 1)
                        res = 0;
                    else
                        res = Interop.mincore.GetChars(m_encoding.CodePage, bytes, byteCount - 1, chars, charCount);

                    _leftOverLeadByte = newLeftOverLeadByte;
                }
                else
                    res = Interop.mincore.GetChars(m_encoding.CodePage, bytes, byteCount, chars, charCount);
            }

            return res;
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
            if (_leftOverLeadByte != 0)
            {
                byte[] inputBytes = new byte[byteCount + 1];
                inputBytes[0] = _leftOverLeadByte;

                Reset();

                fixed (byte* pBytes = inputBytes)
                {
                    RuntimeImports.memmove(pBytes + 1, bytes, byteCount);
                    return Interop.mincore.GetChars(m_encoding.CodePage, pBytes, byteCount + 1, chars, charCount);
                }
            }

            Reset();
            return Interop.mincore.GetChars(m_encoding.CodePage, bytes, byteCount, chars, charCount);
        }
    }
}
