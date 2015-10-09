// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics.Contracts;

namespace System.Text
{
    internal class CodepageEncoding : Encoding
    {
        public CodepageEncoding(int codePage)
            : base(codePage)
        {
            // just call WebName to validate the codepage. if the codepage is invalid it'll throw 
            // proper exception

            Contract.Assert(!String.IsNullOrEmpty(this.WebName));
        }

        public override unsafe int GetByteCount(char[] chars, int index, int count)
        {
            // Validate input parameters
            if (chars == null)
                throw new ArgumentNullException("chars", SR.ArgumentNull_Array);

            if (index < 0 || count < 0)
                throw new ArgumentOutOfRangeException((index < 0 ? "index" : "count"),
                      SR.ArgumentOutOfRange_NeedNonNegNum);

            if (chars.Length - index < count)
                throw new ArgumentOutOfRangeException("chars", SR.ArgumentOutOfRange_IndexCountBuffer);
            Contract.EndContractBlock();

            if (chars.Length == 0)
                return 0;

            // Just call the pointer version
            return Interop.mincore.GetByteCount(m_codePage, chars, index, count);
        }

        public override unsafe int GetByteCount(String s)
        {
            // Validate input
            if (s == null)
                throw new ArgumentNullException("s");
            Contract.EndContractBlock();

            return Interop.mincore.GetByteCount(m_codePage, s);
        }

        public override unsafe int GetBytes(String s, int charIndex, int charCount,
                                              byte[] bytes, int byteIndex)
        {
            if (s == null || bytes == null)
                throw new ArgumentNullException((s == null ? "s" : "bytes"),
                      SR.ArgumentNull_Array);

            if (charIndex < 0 || charCount < 0)
                throw new ArgumentOutOfRangeException((charIndex < 0 ? "charIndex" : "charCount"),
                      SR.ArgumentOutOfRange_NeedNonNegNum);

            if (s.Length - charIndex < charCount)
                throw new ArgumentOutOfRangeException("s",
                      SR.ArgumentOutOfRange_IndexCount);

            if (byteIndex < 0 || byteIndex > bytes.Length)
                throw new ArgumentOutOfRangeException("byteIndex",
                    SR.ArgumentOutOfRange_Index);
            Contract.EndContractBlock();

            // Fixed doesn't like empty arrays
            if (bytes.Length == 0)
                bytes = new byte[1];

            return Interop.mincore.GetBytes(m_codePage, s, charIndex, charCount, bytes, byteIndex);
        }

        public override unsafe int GetBytes(char[] chars, int charIndex, int charCount,
                                               byte[] bytes, int byteIndex)
        {
            // Validate parameters
            if (chars == null || bytes == null)
                throw new ArgumentNullException((chars == null ? "chars" : "bytes"),
                      SR.ArgumentNull_Array);

            if (charIndex < 0 || charCount < 0)
                throw new ArgumentOutOfRangeException((charIndex < 0 ? "charIndex" : "charCount"),
                      SR.ArgumentOutOfRange_NeedNonNegNum);

            if (chars.Length - charIndex < charCount)
                throw new ArgumentOutOfRangeException("chars",
                      SR.ArgumentOutOfRange_IndexCountBuffer);

            if (byteIndex < 0 || byteIndex > bytes.Length)
                throw new ArgumentOutOfRangeException("byteIndex",
                     SR.ArgumentOutOfRange_Index);
            Contract.EndContractBlock();

            // If nothing to encode return 0, avoid fixed problem
            if (chars.Length == 0)
                return 0;

            // Fixed doesn't like empty arrays
            if (bytes.Length == 0)
                bytes = new byte[1];

            return Interop.mincore.GetBytes(m_codePage, chars, charIndex, charCount, bytes, byteIndex);
        }

        public override unsafe int GetCharCount(byte[] bytes, int index, int count)
        {
            // Validate Parameters
            if (bytes == null)
                throw new ArgumentNullException("bytes",
                    SR.ArgumentNull_Array);

            if (index < 0 || count < 0)
                throw new ArgumentOutOfRangeException((index < 0 ? "index" : "count"),
                    SR.ArgumentOutOfRange_NeedNonNegNum);

            if (bytes.Length - index < count)
                throw new ArgumentOutOfRangeException("bytes",
                    SR.ArgumentOutOfRange_IndexCountBuffer);
            Contract.EndContractBlock();

            // If no input just return 0, fixed doesn't like 0 length arrays
            if (bytes.Length == 0)
                return 0;

            return Interop.mincore.GetCharCount(m_codePage, bytes, index, count);
        }

        public override unsafe int GetChars(byte[] bytes, int byteIndex, int byteCount,
                                              char[] chars, int charIndex)
        {
            // Validate Parameters
            if (bytes == null || chars == null)
                throw new ArgumentNullException(bytes == null ? "bytes" : "chars",
                    SR.ArgumentNull_Array);

            if (byteIndex < 0 || byteCount < 0)
                throw new ArgumentOutOfRangeException((byteIndex < 0 ? "byteIndex" : "byteCount"),
                    SR.ArgumentOutOfRange_NeedNonNegNum);

            if (bytes.Length - byteIndex < byteCount)
                throw new ArgumentOutOfRangeException("bytes",
                    SR.ArgumentOutOfRange_IndexCountBuffer);

            if (charIndex < 0 || charIndex > chars.Length)
                throw new ArgumentOutOfRangeException("charIndex",
                    SR.ArgumentOutOfRange_Index);
            Contract.EndContractBlock();

            // If no input, return 0 & avoid fixed problem
            if (bytes.Length == 0)
                return 0;

            // Fixed doesn't like empty arrays
            if (chars.Length == 0)
                chars = new char[1];

            return Interop.mincore.GetChars(m_codePage, bytes, byteIndex, byteCount, chars, charIndex);
        }

        public override Decoder GetDecoder()
        {
            switch (CodePage)
            {
                case 50220: // iso-2022-jp 
                case 50221: // csISO2022JP 
                case 50222: // iso-2022-jp 
                    return new DecoderIso2022(this);

                case 50225: // iso-2022-kr 
                    return new DecoderIso2022KR(this);

                case 52936: // hz-gb-2312 
                    return new Decoder2312HZ(this);

                case 932:   // Japanese (Shift-JIS) 
                case 936:   // Chinese Simplified (GB2312)
                case 949:   // Korean                                   
                case 950:   // Chinese Traditional (Big5)
                case 1361:  // Korean (Johab)
                case 10001: // Japanese (Mac)
                case 10002: // Chinese Traditional (Mac)
                case 10003: // Korean (Mac)
                case 10008: // Chinese Simplified (Mac)
                case 20000: // Chinese Traditional (CNS)
                case 20001: // TCA Taiwan
                case 20002: // Chinese Traditional (Eten)
                case 20003: // IBM5550 Taiwan
                case 20004: // TeleText Taiwan
                case 20005: // Wang Taiwan
                case 20261: // T.61
                case 20932: // Japanese (JIS 0208-1990 and 0212-1990)
                case 20936: // Chinese Simplified (GB2312-80)
                case 51949: // Korean (EUC)
                    return new DecoderDBCS(this);

                case 54936: // Chinese Simplified (GB18030)
                    return new DecoderGB18030(this);

                default:
                    return new DefaultDecoder(this);
            }
        }

        public override Encoder GetEncoder()
        {
            return new EncoderNLS(this);
        }

        public override int GetMaxByteCount(int charCount)
        {
            if (charCount < 0)
                throw new ArgumentOutOfRangeException("charCount",
                     SR.ArgumentOutOfRange_NeedNonNegNum);
            Contract.EndContractBlock();

            // Characters would be # of characters + 1 in case high surrogate is ? * max fallback
            long byteCount = (long)charCount + 1;

            if (EncoderFallback.MaxCharCount > 1)
                byteCount *= EncoderFallback.MaxCharCount;

            // 2 to 1 is worst case.  Already considered surrogate fallback
            byteCount *= 2;

            if (byteCount > 0x7fffffff)
                throw new ArgumentOutOfRangeException("charCount", SR.ArgumentOutOfRange_GetByteCountOverflow);

            return (int)byteCount;
        }

        public override int GetMaxCharCount(int byteCount)
        {
            if (byteCount < 0)
                throw new ArgumentOutOfRangeException("byteCount",
                     SR.ArgumentOutOfRange_NeedNonNegNum);
            Contract.EndContractBlock();

            // DBCS is pretty much the same, but could have hanging high byte making extra ? and fallback for unknown
            long charCount = ((long)byteCount + 1);

            // 1 to 1 for most characters.  Only surrogates with fallbacks have less, unknown fallbacks could be longer.
            if (DecoderFallback.MaxCharCount > 1)
                charCount *= DecoderFallback.MaxCharCount;

            if (charCount > 0x7fffffff)
                throw new ArgumentOutOfRangeException("byteCount", SR.ArgumentOutOfRange_GetCharCountOverflow);

            return (int)charCount;
        }
    }
}
