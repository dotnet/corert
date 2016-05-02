// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal static unsafe partial class mincore
    {
        // TODO: Once we have marshalling setup we probably want to revisit these PInvokes
        [DllImport(Libraries.ProcessEnvironment, EntryPoint = "GetEnvironmentVariableW")]
        internal static unsafe extern int GetEnvironmentVariable(char* lpName, char* lpValue, int size);

        [DllImport(Libraries.ProcessEnvironment, EntryPoint = "ExpandEnvironmentStringsW")]
        internal static unsafe extern int ExpandEnvironmentStrings(char* lpSrc, char* lpDst, int nSize);

        [DllImport(Libraries.Kernel32, EntryPoint = "GetComputerNameW")]
        internal static unsafe extern int GetComputerName(char* nameBuffer, ref int bufferSize);

        [DllImport(Libraries.Kernel32, EntryPoint = "ExitProcess")]
        internal static extern void ExitProcess(int exitCode);
    }
}
