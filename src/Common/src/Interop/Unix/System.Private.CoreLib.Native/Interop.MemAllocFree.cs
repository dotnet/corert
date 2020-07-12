// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal unsafe partial class Sys
    {
        [DllImport(Interop.Libraries.CoreLibNative, EntryPoint = "CoreLibNative_MemAlloc")]
        internal static extern IntPtr MemAlloc(UIntPtr sizeInBytes);

        [DllImport(Interop.Libraries.CoreLibNative, EntryPoint = "CoreLibNative_MemFree")]
        internal static extern void MemFree(IntPtr ptr);
    }

    internal static IntPtr MemAlloc(UIntPtr sizeInBytes)
    {
        IntPtr allocatedMemory = Interop.Sys.MemAlloc(sizeInBytes);
        if (allocatedMemory == IntPtr.Zero)
        {
            throw new OutOfMemoryException();
        }
        return allocatedMemory;
    }

    internal static void MemFree(IntPtr allocatedMemory)
    {
        Interop.Sys.MemFree(allocatedMemory);
    }
}
