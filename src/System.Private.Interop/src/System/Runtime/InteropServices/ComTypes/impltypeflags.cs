// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
