// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal static unsafe partial class Kernel32
    {
        [DllImport(Libraries.Kernel32, EntryPoint = "ExitProcess")]
        internal static extern void ExitProcess(int exitCode);
    }
}
