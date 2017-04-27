// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;


// TODO : Split this file , now it contains anything other than string and memoryreleated.

namespace System.Runtime.InteropServices
{
    public partial class ExternalInterop
    {
        static unsafe internal int FormatMessage(
                int dwFlags,
                IntPtr lpSource,
                uint dwMessageId,
                uint dwLanguageId,
                char* lpBuffer,
                uint nSize,
                IntPtr Arguments)
        {
            // ??
            return 0;
            //throw new PlatformNotSupportedException("FormatMessage");
        }

        //TODO : implement in PAL
        internal static unsafe void OutputDebugString(string outputString)
        {
            throw new PlatformNotSupportedException("OutputDebugString");
        }
    }
}
