// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace System.Runtime.InteropServices.ComTypes
{
    [Flags]
    public enum IMPLTYPEFLAGS
    {
        IMPLTYPEFLAG_FDEFAULT = 0x1,
        IMPLTYPEFLAG_FSOURCE = 0x2,
        IMPLTYPEFLAG_FRESTRICTED = 0x4,
        IMPLTYPEFLAG_FDEFAULTVTABLE = 0x8,
    }
}
