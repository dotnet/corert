// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal unsafe partial class Sys
    {
        [DllImport(Interop.Libraries.CoreLibNative, EntryPoint = "CoreLibNative_GetEnvironmentVariable")]
        internal static unsafe extern int GetEnvironmentVariable(string name, out IntPtr result);

        [DllImport(Interop.Libraries.CoreLibNative, EntryPoint = "CoreLibNative_GetMachineName")]
        internal static unsafe extern int GetMachineName(byte *hostNameBuffer, int hostNameBufferSize);

        [DllImport(Interop.Libraries.CoreLibNative, EntryPoint = "CoreLibNative_ExitProcess")]
        internal static extern void ExitProcess(int exitCode);
    }
}
