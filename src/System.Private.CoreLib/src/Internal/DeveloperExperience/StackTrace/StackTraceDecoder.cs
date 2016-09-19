// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// #define DUMP_STACKTRACE_DECODE // uncomment this to print the stack trace blob's decoded bytes to the debug console.

using System.Diagnostics;

namespace Internal.DeveloperExperience.StackTrace
{
    internal class StackTraceDecoder
    {
        public static uint MAX_DECODE_BYTES = 5;

        // Decodes a uint (with variable length) from the byte buffer using MSB decoding.
        // returns an uint32_t representing the decoded uint, and the number of bytes read.
        public static unsafe uint DecodeUInt(byte* buffer, uint size, ref uint bytesRead)
        {
            uint decodedId = 0;
            uint originalBytesRead = bytesRead;
            uint i = 0;
            for (; i < size; i++)
            {
                // extracts the 7 bits into the decoded id.
                decodedId |= (uint)(buffer[i] & ((byte)((1 << 7) - 1))) << ((byte)(7 * i));

                // If the MSB flag is not set, break.
                if ((buffer[i] & ((byte)(1 << 7))) == 0)
                    break;
            }
            bytesRead += i + 1;
#if DUMP_STACKTRACE_DECODE
            Debug.WriteLine(decodedId.ToString() + " in " + (bytesRead - originalBytesRead) + " bytes");
#endif
            return decodedId;
        }

        public static unsafe byte DecodeByte(byte* buffer, ref uint bytesRead)
        {
        	bytesRead++;
#if DUMP_STACKTRACE_DECODE
            Debug.WriteLine(buffer[0].ToString() + " in 1 bytes");
#endif
            return buffer[0];
        }
    }
}