// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

internal static partial class Interop
{
    internal static unsafe partial class mincore
    {
        [DllImport("api-ms-win-core-libraryloader-l1-2-0.dll")]
        internal static extern IntPtr GetProcAddress(IntPtr hModule, byte* lpProcName);

        [DllImport("api-ms-win-core-libraryloader-l1-2-0.dll", EntryPoint = "LoadLibraryExW", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, int dwFlags);

        [DllImport("api-ms-win-core-libraryloader-l1-2-0.dll")]
        internal static extern bool FreeLibrary(IntPtr hModule);
    }
}
