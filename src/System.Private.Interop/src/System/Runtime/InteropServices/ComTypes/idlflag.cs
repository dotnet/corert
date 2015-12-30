// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
