// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

namespace Internal.Runtime.CompilerHelpers
{
    /// <summary>
    /// These methods are used to throw exceptions from generated code.
    /// </summary>
    internal static class InteropHelpers
    {
        internal static unsafe byte[] StringToAnsi(String str)
        {
            if (str == null)
                return null;

            // CORERT-TODO: Use same encoding as the rest of the interop
            var encoding = Encoding.UTF8;

            fixed (char * pStr = str)
            {
                int stringLength = str.Length;
                int bufferLength = encoding.GetByteCount(pStr, stringLength);
                var buffer = new byte[bufferLength + 1];
                fixed (byte * pBuffer = buffer)
                {
                    encoding.GetBytes(pStr, stringLength, pBuffer, bufferLength);
                    return buffer;
                }
            }
        }

        internal static char[] GetEmptyStringBuilderBuffer(StringBuilder sb)
        {
            // CORERT-TODO: Reuse buffer from string builder where possible?
            return new char[sb.Capacity + 1];
        }

        internal static unsafe void ReplaceStringBuilderBuffer(StringBuilder sb, char[] buf)
        {
            // CORERT-TODO: Reuse buffer from string builder where possible?
            fixed (char* p = buf)
                sb.ReplaceBuffer(p);
        }
    }
}
