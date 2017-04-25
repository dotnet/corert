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

#if CORECLR

        public static unsafe uint SysStringLen(void* pBSTR)
        {
            throw new PlatformNotSupportedException("SysStringLen");
        }

        public static unsafe uint SysStringLen(IntPtr pBSTR)
        {
            throw new PlatformNotSupportedException("SysStringLen");
        }

        // Do nothing
        internal static unsafe void OutputDebugString(string outputString)
        {

        }
#endif //CORECLR
    }
}
