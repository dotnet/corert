// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal static unsafe partial class mincore
    {
        [DllImport("api-ms-win-core-heap-l1-1-0.dll")]
        internal static extern IntPtr GetProcessHeap();

        [DllImport("api-ms-win-core-heap-l1-1-0.dll")]
        internal static extern IntPtr HeapAlloc(IntPtr hHeap, uint dwFlags, UIntPtr dwBytes);

        [DllImport("api-ms-win-core-heap-l1-1-0.dll")]
        internal static extern int HeapFree(IntPtr hHeap, uint dwFlags, IntPtr lpMem);
    }

    internal static IntPtr MemAlloc(UIntPtr sizeInBytes)
    {
        IntPtr allocatedMemory = Interop.mincore.HeapAlloc(Interop.mincore.GetProcessHeap(), 0, sizeInBytes);
        if (allocatedMemory == IntPtr.Zero)
        {
            throw new OutOfMemoryException();
        }
        return allocatedMemory;
    }

    internal static void MemFree(IntPtr allocatedMemory)
    {
        Interop.mincore.HeapFree(Interop.mincore.GetProcessHeap(), 0, allocatedMemory);
    }
}
