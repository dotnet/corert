// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal static unsafe partial class mincore
    {
        [McgGeneratedNativeCallCodeAttribute]
        [DllImport("ole32.dll")]
        internal static extern IntPtr CoTaskMemAlloc(UIntPtr bytes);

        [McgGeneratedNativeCallCodeAttribute]
        [DllImport("ole32.dll")]
        internal static extern void CoTaskMemFree(IntPtr allocatedMemory);

        [McgGeneratedNativeCallCodeAttribute]
        [DllImport("ole32.dll")]
        internal static extern IntPtr CoTaskMemRealloc(IntPtr pv, IntPtr cb);
    }
}
