// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text;

internal static partial class Interop
{
    internal unsafe partial class StringHelper
    {
        public static unsafe byte[] GetBytesFromUTF8string(string value)
        {
            int bytecount = Encoding.UTF8.GetByteCount(value);

            // GetByteCount does not account for the trailing zero that is needed 
            // Add one to allocate the trailing zero for the string.
            // Note: The runtime will zero-initialize the buffer
            byte[] result = new byte[bytecount + 1]; 
            fixed (char* pValue = value)
                fixed (byte* pResult = result)
                    Encoding.UTF8.GetBytes(pValue, value.Length, pResult, bytecount);

            return result;
        }
    }
}
