// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2111:PointersShouldNotBeVisible",  Justification="Backwards compatibility")]
        public IntPtr pvReserved;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2111:PointersShouldNotBeVisible",  Justification="Backwards compatibility")]
        public IntPtr pfnDeferredFillIn;
        public Int32 scode;
    }
}
