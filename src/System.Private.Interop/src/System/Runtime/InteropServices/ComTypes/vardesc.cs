// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
            [FieldOffset(0)]
            public IntPtr lpvarValue;
        };

        public DESCUNION desc;

        public ELEMDESC elemdescVar;
        public short wVarFlags;
        public VARKIND varkind;
    }
}
