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

        [DllImport(Libraries.Kernel32, EntryPoint = "ExitProcess")]
        internal static extern void ExitProcess(int exitCode);
    }

    internal static void ExitProcess(int exitCode)
    {
        mincore.ExitProcess(exitCode);
    }
}
