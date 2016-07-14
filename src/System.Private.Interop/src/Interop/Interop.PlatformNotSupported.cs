// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;


// These are all winrt specific , we need placeholder since the root of these calls are
// are from McgMarshal , refactoring WinRT marshal API is TODO
namespace System.Runtime.InteropServices
{
#if CORECLR  

    public static partial class McgMarshal
    {

        public static unsafe void StringToHStringReference(
        char* pchPinnedSourceString,
        string sourceString,
        HSTRING_HEADER* pHeader,
        HSTRING* phString)
        {
            throw new PlatformNotSupportedException("StringToHStringReference");
        }

        public static string HStringToString(IntPtr hString)
        {
            throw new PlatformNotSupportedException("HStringToString");
        }

        public static string HStringToString(HSTRING hString)
        {
            throw new PlatformNotSupportedException("HStringToString");
        }

        public static void FreeHString(IntPtr pHString)
        {
            throw new PlatformNotSupportedException("FreeHString");
        }

        public static unsafe IntPtr ActivateInstance(string typeName)
        {
            throw new PlatformNotSupportedException("ActivateInstance");
        }

        public static HSTRING StringToHString(string sourceString)
        {
            throw new PlatformNotSupportedException("StringToHString");
        }

        internal static unsafe int StringToHStringNoNullCheck(string sourceString, HSTRING* hstring)
        {
            throw new PlatformNotSupportedException("StringToHStringNoNullCheck");
        }

        public static unsafe HSTRING StringToHStringForField(string sourceString)
        {
             throw new PlatformNotSupportedException("StringToHStringForField");
        }
    }
#endif
}

