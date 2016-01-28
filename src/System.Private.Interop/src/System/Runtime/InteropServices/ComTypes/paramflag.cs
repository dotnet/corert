// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
