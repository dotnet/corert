// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;


#if !ENABLE_WINRT
// These are all winrt specific , we need placeholder since the root of these calls are
// are from McgMarshal , refactoring WinRT marshal API is TODO
namespace System.Runtime.InteropServices
{
    public partial class ExternalInterop
    {
        static internal unsafe void RoGetActivationFactory(string className, ref Guid iid, out IntPtr ppv)
        {
            throw new PlatformNotSupportedException("RoGetActivationFactory");
        }
    }

    public static partial class McgMarshal
    {
        public static string HStringToString(IntPtr hString)
        {
            throw new PlatformNotSupportedException("HStringToString");
        }

        public static string HStringToString(HSTRING hString)
        {
            throw new PlatformNotSupportedException("HStringToString");
        }

        public static HSTRING StringToHString(string sourceString)
        {
            throw new PlatformNotSupportedException("StringToHString");
        }

        internal static unsafe int StringToHStringNoNullCheck(string sourceString, HSTRING* hstring)
        {
            throw new PlatformNotSupportedException("StringToHStringNoNullCheck");
        }

        public static void FreeHString(IntPtr pHString)
        {
            throw new PlatformNotSupportedException("FreeHString");
        }
    }
}
#endif
