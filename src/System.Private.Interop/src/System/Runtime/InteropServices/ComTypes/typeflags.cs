// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

//

//

using System;

namespace System.Runtime.InteropServices.ComTypes
{
    [Flags]
    public enum TYPEFLAGS : short
    {
        TYPEFLAG_FAPPOBJECT = 0x1,
        TYPEFLAG_FCANCREATE = 0x2,
        TYPEFLAG_FLICENSED = 0x4,
        TYPEFLAG_FPREDECLID = 0x8,
        TYPEFLAG_FHIDDEN = 0x10,
        TYPEFLAG_FCONTROL = 0x20,
        TYPEFLAG_FDUAL = 0x40,
        TYPEFLAG_FNONEXTENSIBLE = 0x80,
        TYPEFLAG_FOLEAUTOMATION = 0x100,
        TYPEFLAG_FRESTRICTED = 0x200,
        TYPEFLAG_FAGGREGATABLE = 0x400,
        TYPEFLAG_FREPLACEABLE = 0x800,
        TYPEFLAG_FDISPATCHABLE = 0x1000,
        TYPEFLAG_FREVERSEBIND = 0x2000,
        TYPEFLAG_FPROXY = 0x4000
    }
}
