// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal static unsafe partial class mincore
    {
        [DllImport(Libraries.ProcessEnvironment, CharSet = CharSet.Unicode, EntryPoint = "GetEnvironmentVariableW")]
        internal static extern unsafe int GetEnvironmentVariable(string lpName, [Out] char[] lpValue, int size);
    }
}
