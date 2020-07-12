// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

internal static partial class Interop
{
    internal static unsafe partial class mincore
    {
        public const uint ERROR_INSUFFICIENT_BUFFER = 0x7a;
        [DllImport(Libraries.Kernel32, EntryPoint = "GetModuleFileNameW", CharSet = CharSet.Unicode)]
        public extern static int GetModuleFileName(IntPtr hModule, StringBuilder lpFilename, int nSize);
    }
}
