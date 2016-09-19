// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

namespace Internal.DeveloperExperience.StackTrace
{
    internal class StackTraceStringDescriptor
    {
        private static UTF8Encoding encoding;

        public static unsafe string CreateString(byte* buffer, uint offset)
        {
            uint bytesRead = 0;
            return ReadString(true, buffer, offset, out bytesRead);
        }

        public static unsafe uint CalculateSize(byte* buffer, uint offset)
        {
            uint bytesRead = 0;
            ReadString(false, buffer, offset, out bytesRead);
            return bytesRead;
        }

        private static unsafe string ReadString(bool constructInstance, byte* buffer, uint offset, out uint bytesRead)
        {
            byte* pMyBuffer = buffer + offset;
            bytesRead = 0;
            uint size = StackTraceDecoder.DecodeUInt(pMyBuffer + bytesRead, StackTraceDecoder.MAX_DECODE_BYTES, ref bytesRead);
            if (constructInstance)
            {
                if (encoding == null)
                {
                    encoding = new UTF8Encoding();
                }

                int charCount = encoding.GetCharCount(pMyBuffer + bytesRead, (int)size);
                var d = new char[charCount];
                fixed (char* c = d)
                {
                    encoding.GetChars(pMyBuffer + bytesRead, (int)size, c, charCount);
                    return new String(c);
                }
            }
            else
            {
                bytesRead += size;
                return null;
            }
        }
    }
}