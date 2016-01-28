// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Runtime.InteropServices
{
    public partial class ExternalInterop
    {
        public static partial class Constants
        {
            // TODO: These are windows specific , unfortunately
            // the API signature is same for Windows and non-Windows and we need
            // these defined to make the compiler happy.These are not used on non-windows
            // platforms.
            public const uint WC_NO_BEST_FIT_CHARS = 0x00000400;
            public const uint CP_ACP = 0;
            public const uint MB_PRECOMPOSED = 1;
        }


        internal static unsafe uint SysStringLen(void* pBSTR)
        {
            throw new PlatformNotSupportedException("SysStringLen");
        }

        internal static unsafe uint SysStringLen(IntPtr pBSTR)
        {
            throw new PlatformNotSupportedException("SysStringLen");
        }

        unsafe public static int ConvertWideCharToMultiByte(char* wideCharStr, int wideCharLen, IntPtr multiByteStr, int multiByteLen)
        {
            return System.Text.Encoding.UTF8.GetBytes(wideCharStr, wideCharLen,(byte*)multiByteStr, multiByteLen);
        }

        unsafe public static int GetByteCount(char* wideCharStr, int wideCharLen)
        {
            return System.Text.Encoding.UTF8.GetByteCount(wideCharStr, wideCharLen);
        }

        unsafe public static int ConvertMultiByteToWideChar(IntPtr multiByteStr,
                                                            int multiByteLen,
                                                            IntPtr wideCharStr,
                                                            int wideCharLen)
        {
            return System.Text.Encoding.UTF8.GetChars((byte*)multiByteStr, multiByteLen, (char*)wideCharStr, wideCharLen);
        }

        public static unsafe int GetCharCount(byte* multiByteStr, int multiByteLen)
        {
            return System.Text.Encoding.UTF8.GetCharCount(multiByteStr, multiByteLen);
        }

    }
}
