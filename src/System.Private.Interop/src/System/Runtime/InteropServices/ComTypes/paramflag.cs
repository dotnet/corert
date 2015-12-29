// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace System.Runtime.InteropServices.ComTypes
{
    [Flags]
    public enum PARAMFLAG : short
    {
        PARAMFLAG_NONE = 0,
        PARAMFLAG_FIN = 0x1,
        PARAMFLAG_FOUT = 0x2,
        PARAMFLAG_FLCID = 0x4,
        PARAMFLAG_FRETVAL = 0x8,
        PARAMFLAG_FOPT = 0x10,
        PARAMFLAG_FHASDEFAULT = 0x20,
        PARAMFLAG_FHASCUSTDATA = 0x40
    }
}
