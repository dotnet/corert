// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace System.Runtime.InteropServices.ComTypes
{
    [Flags]
    public enum LIBFLAGS : short
    {
        LIBFLAG_FRESTRICTED = 0x1,
        LIBFLAG_FCONTROL = 0x2,
        LIBFLAG_FHIDDEN = 0x4,
        LIBFLAG_FHASDISKIMAGE = 0x8
    }
}
