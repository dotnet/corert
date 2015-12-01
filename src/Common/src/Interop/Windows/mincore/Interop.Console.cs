// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    private static class Libraries
    {
        internal const string Process = "api-ms-win-core-processenvironment-l1-1-0.dll";
        internal const string Console = "api-ms-win-core-console-l1-1-0.dll";
    }

    internal static unsafe partial class mincore
    {
        [DllImport(Libraries.Process)]
        internal static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport(Libraries.Console, EntryPoint = "WriteConsoleW")]
        internal static unsafe extern bool WriteConsole(IntPtr hConsoleOutput, byte* lpBuffer, int nNumberOfCharsToWrite, out int lpNumberOfCharsWritten, IntPtr lpReservedMustBeNull);
    }
}
