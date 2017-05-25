﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

internal static partial class Interop
{
    internal static unsafe partial class mincore
    {
        [DllImport("api-ms-win-core-libraryloader-l1-2-0.dll")]
        internal static extern IntPtr GetProcAddress(IntPtr hModule, byte* lpProcName);

        [DllImport("api-ms-win-core-libraryloader-l1-2-0.dll", EntryPoint = "LoadLibraryExW", CharSet = CharSet.Unicode)]
        internal static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, int dwFlags);

        [DllImport("api-ms-win-core-libraryloader-l1-2-0.dll", EntryPoint = "LoadLibraryExW", CharSet = CharSet.Unicode)]
        internal static extern SafeLibraryHandle LoadLibraryEx_SafeHandle(string lpFileName, IntPtr hFile, int dwFlags);

        [DllImport("api-ms-win-core-libraryloader-l1-2-0.dll")]
        internal static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("api-ms-win-core-libraryloader-l1-2-0.dll", EntryPoint = "LoadStringW", CharSet = CharSet.Unicode)]
        internal static extern int LoadString(SafeLibraryHandle handle, int id, StringBuilder buffer, int bufferLength);

        internal const int LOAD_LIBRARY_AS_DATAFILE = 0x00000002;
        internal const int LOAD_STRING_MAX_LENGTH = 500;
    }
}
