// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal unsafe partial class Sys
    {
        [DllImport(Interop.Libraries.CoreLibNative, EntryPoint = "CoreLibNative_LoadLibrary")]
        internal static extern IntPtr LoadLibrary(string filename);

        [DllImport(Interop.Libraries.CoreLibNative, EntryPoint = "CoreLibNative_GetProcAddress")]
        internal static extern IntPtr GetProcAddress(IntPtr handle, byte* symbol);

        [DllImport(Interop.Libraries.CoreLibNative, EntryPoint = "CoreLibNative_GetProcAddress")]
        internal static extern IntPtr GetProcAddress(IntPtr handle, string symbol);

        [DllImport(Interop.Libraries.CoreLibNative, EntryPoint = "CoreLibNative_FreeLibrary")]
        internal static extern void FreeLibrary(IntPtr handle);
    }
}
