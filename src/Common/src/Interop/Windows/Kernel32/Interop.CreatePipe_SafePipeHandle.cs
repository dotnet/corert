// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;

internal partial class Interop
{
    internal partial class Kernel32
    {
        [DllImport(Libraries.Kernel32, SetLastError = true)]
        internal static extern bool CreatePipe(out SafePipeHandle hReadPipe, out SafePipeHandle hWritePipe, ref SECURITY_ATTRIBUTES lpPipeAttributes, int nSize);
    }
}
