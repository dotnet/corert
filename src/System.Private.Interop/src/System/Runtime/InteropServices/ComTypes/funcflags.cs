// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace System.Runtime.InteropServices.ComTypes
{
    [Flags]
    public enum FUNCFLAGS : short
    {
        FUNCFLAG_FRESTRICTED = 0x1,
        FUNCFLAG_FSOURCE = 0x2,
        FUNCFLAG_FBINDABLE = 0x4,
        FUNCFLAG_FREQUESTEDIT = 0x8,
        FUNCFLAG_FDISPLAYBIND = 0x10,
        FUNCFLAG_FDEFAULTBIND = 0x20,
        FUNCFLAG_FHIDDEN = 0x40,
        FUNCFLAG_FUSESGETLASTERROR = 0x80,
        FUNCFLAG_FDEFAULTCOLLELEM = 0x100,
        FUNCFLAG_FUIDEFAULT = 0x200,
        FUNCFLAG_FNONBROWSABLE = 0x400,
        FUNCFLAG_FREPLACEABLE = 0x800,
        FUNCFLAG_FIMMEDIATEBIND = 0x1000
    }
}
