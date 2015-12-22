// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
    }
}
