// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//

//
// All P/invokes used by System.Private.Interop and MCG generated code goes here.
//
// !!IMPORTANT!!
//
// Do not rely on MCG to generate marshalling code for these p/invokes as MCG might not see them at all
// due to not seeing dependency to those calls (before the MCG generated code is generated). Instead,
// always manually marshal the arguments

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Runtime.InteropServices
{
    public static partial class ExternalInterop
    {
        public static partial class Constants
        {
            public const uint WC_NO_BEST_FIT_CHARS = 0x00000400;
            public const uint CP_ACP = 0;
            public const uint MB_PRECOMPOSED = 1;
        }

        private static partial class Libraries
        {
#if TARGET_CORE_API_SET
            internal const string CORE_STRING = "api-ms-win-core-string-l1-1-0.dll";
#else
            internal const string CORE_STRING = "kernel32.dll";
#endif //TARGET_CORE_API_SET
        }

        [DllImport(Libraries.CORE_STRING, CallingConvention = CallingConvention.StdCall)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [McgGeneratedNativeCallCodeAttribute]
        unsafe private extern static int WideCharToMultiByte(
                    uint CodePage,
                    uint dwFlags,
                    char* lpWideCharStr,
                    int cchWideChar,
                    IntPtr lpMultiByteStr,
                    int cbMultiByte,
                    IntPtr lpDefaultChar,
                    IntPtr lpUsedDefaultChar);

        [DllImport(Libraries.CORE_STRING, CallingConvention = CallingConvention.StdCall)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [McgGeneratedNativeCallCodeAttribute]
        private extern static int MultiByteToWideChar(
                    uint CodePage,
                    uint dwFlags,
                    IntPtr lpMultiByteStr,
                    int cbMultiByte,
                    IntPtr lpWideCharStr,
                    int cchWideChar);
        
        // Convert a UTF16 string to ANSI byte array
        unsafe public static int ConvertWideCharToMultiByte(char* wideCharStr, int wideCharLen, IntPtr multiByteStr, int multiByteLen)
        {
            return WideCharToMultiByte(Constants.CP_ACP,
                                       0,
                                       wideCharStr,
                                       wideCharLen,
                                       multiByteStr,
                                       multiByteLen,
                                       default(IntPtr),
                                       default(IntPtr)
                                       );
        }

        // Return size in bytes required to convert a UTF16 string to byte array.
        unsafe public static int GetByteCount(char* wideStr, int wideStrLen)
        {
            return WideCharToMultiByte(Constants.CP_ACP,
                                       0,
                                       wideStr,
                                       wideStrLen,
                                       IntPtr.Zero,
                                       0,
                                       default(IntPtr),
                                       default(IntPtr)
                                       );
        }

        // Convert a native byte array to UTF16
        public static int ConvertMultiByteToWideChar(IntPtr multiByteStr, int multiByteLen, IntPtr wideCharStr, int wideCharLen)
        {
            return MultiByteToWideChar(Constants.CP_ACP, 0, multiByteStr, multiByteLen, wideCharStr, wideCharLen);
        }

        // Return number of charaters encoded in native byte array lpMultiByteStr

        unsafe public static int GetCharCount(IntPtr multiByteStr, int multiByteLen)
        {
            return MultiByteToWideChar(Constants.CP_ACP, 0, multiByteStr, multiByteLen, IntPtr.Zero, 0);
        }

        // Convert a UTF16 string to ANSI byte array using flags
        unsafe public static int ConvertWideCharToMultiByte(char* wideCharStr, 
                                                            int   wideCharLen,
                                                            IntPtr multiByteStr, 
                                                            int multiByteLen,
                                                            uint flags,
                                                            IntPtr usedDefaultChar)
        {
            return WideCharToMultiByte(Constants.CP_ACP,
                                       flags,
                                       wideCharStr,
                                       wideCharLen,
                                       multiByteStr,
                                       multiByteLen,
                                       default(IntPtr),
                                       usedDefaultChar
                                       );
        }
    }
}
