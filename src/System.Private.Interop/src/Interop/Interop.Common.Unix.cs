// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;


// TODO : Split this file , now it contains anything other than string and memoryreleated.

namespace System.Runtime.InteropServices
{
    public partial class ExternalInterop
    {
        static internal int GetLastWin32Error()
        {
            throw new PlatformNotSupportedException("GetLastWin32Error");
        }

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
