// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;
using System.Text;

internal static partial class Interop
{
    internal static partial class mincore
    {
        [DllImport("api-ms-win-core-sysinfo-l1-2-1", EntryPoint = "GetSystemDirectoryW", CharSet = CharSet.Unicode)]
        internal static extern int GetSystemDirectory([Out]StringBuilder sb, int length);
    }
}
