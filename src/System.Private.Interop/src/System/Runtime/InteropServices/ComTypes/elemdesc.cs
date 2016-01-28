// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace System.Runtime.InteropServices.ComTypes
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct ELEMDESC
    {
        public TYPEDESC tdesc;

        [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
        public struct DESCUNION
        {
            [FieldOffset(0)]
            public IDLDESC idldesc;
            [FieldOffset(0)]
            public PARAMDESC paramdesc;
        };
        public DESCUNION desc;
    }
}
