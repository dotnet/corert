// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace System.Runtime.InteropServices.ComTypes
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct VARDESC
    {
        public int memid;
        public String lpstrSchema;

        [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
        public struct DESCUNION
        {
            [FieldOffset(0)]
            public int oInst;
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2111:PointersShouldNotBeVisible",  Justification="Backwards compatibility")]
            [FieldOffset(0)]
            public IntPtr lpvarValue;
        };

        public DESCUNION desc;

        public ELEMDESC elemdescVar;
        public short wVarFlags;
        public VARKIND varkind;
    }
}
