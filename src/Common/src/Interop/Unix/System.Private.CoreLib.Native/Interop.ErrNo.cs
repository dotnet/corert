// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal unsafe partial class Sys
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(Interop.Libraries.CoreLibNative, "CoreLibNative_GetLastErrNo")]
        internal static extern int GetLastErrNo();

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(Interop.Libraries.CoreLibNative, "CoreLibNative_SetLastErrNo")]
        internal static extern void SetLastErrNo(int error);
    }
}
