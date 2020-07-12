// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal static partial class Crypt32
    {
        internal static unsafe bool CryptEncodeObject(MsgEncodingType dwCertEncodingType, CryptDecodeObjectStructType lpszStructType, void* pvStructInfo, byte[]? pbEncoded, ref int pcbEncoded)
        {
            return CryptEncodeObject(dwCertEncodingType, (IntPtr)lpszStructType, pvStructInfo, pbEncoded, ref pcbEncoded);
        }

        [DllImport(Libraries.Crypt32, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern unsafe bool CryptEncodeObject(
            MsgEncodingType dwCertEncodingType,
            IntPtr lpszStructType,
            void* pvStructInfo,
            [Out] byte[]? pbEncoded,
            [In, Out] ref int pcbEncoded);
    }
}
