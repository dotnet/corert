// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal static unsafe partial class mincore
    {
        [DllImport(Libraries.ProcessEnvironment, EntryPoint = "GetCommandLineW")]
        internal static unsafe extern char* GetCommandLine();
    }
}
