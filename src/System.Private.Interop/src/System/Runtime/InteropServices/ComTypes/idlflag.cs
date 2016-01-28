// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace System.Runtime.InteropServices.ComTypes
{
    [Flags]
    public enum IDLFLAG : short
    {
        IDLFLAG_NONE = PARAMFLAG.PARAMFLAG_NONE,
        IDLFLAG_FIN = PARAMFLAG.PARAMFLAG_FIN,
        IDLFLAG_FOUT = PARAMFLAG.PARAMFLAG_FOUT,
        IDLFLAG_FLCID = PARAMFLAG.PARAMFLAG_FLCID,
        IDLFLAG_FRETVAL = PARAMFLAG.PARAMFLAG_FRETVAL
    }
}
