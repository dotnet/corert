// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace System.Runtime.InteropServices.ComTypes
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct EXCEPINFO
    {
        public Int16 wCode;
        public Int16 wReserved;
        [MarshalAs(UnmanagedType.BStr)]
        public String bstrSource;
        [MarshalAs(UnmanagedType.BStr)]
        public String bstrDescription;
        [MarshalAs(UnmanagedType.BStr)]
        public String bstrHelpFile;
        public int dwHelpContext;
        public IntPtr pvReserved;
        public IntPtr pfnDeferredFillIn;
        public Int32 scode;
    }
}
