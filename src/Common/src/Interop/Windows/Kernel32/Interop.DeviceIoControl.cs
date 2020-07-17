// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;

internal partial class Interop
{
    internal partial class Kernel32
    {
        [DllImport(Libraries.Kernel32, CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool DeviceIoControl
        (
            SafeFileHandle fileHandle,
            uint ioControlCode,
            IntPtr inBuffer,
            uint cbInBuffer,
            IntPtr outBuffer,
            uint cbOutBuffer,
            out uint cbBytesReturned,
            IntPtr overlapped
        );
    }
}
