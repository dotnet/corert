// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal static unsafe partial class mincore
    {
        [DllImport(Libraries.ProcessEnvironment, EntryPoint = "GetCommandLineW")]
        internal static extern unsafe char* GetCommandLine();
    }
}
