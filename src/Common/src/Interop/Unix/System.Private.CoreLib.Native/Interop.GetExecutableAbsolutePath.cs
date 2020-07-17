// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal unsafe partial class Sys
    {
        [DllImport(Interop.Libraries.CoreLibNative, EntryPoint = "CoreLibNative_GetExecutableAbsolutePath", SetLastError = true)]
        internal static extern unsafe int GetExecutableAbsolutePath([Out] char[] buffer, int bufferSize);
    }
}
