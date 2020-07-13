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
        internal static extern unsafe IntPtr HeapReAlloc(IntPtr hHeap, uint dwFlags, IntPtr lpMem, UIntPtr dwBytes);
    }

    internal static unsafe IntPtr MemReAlloc(IntPtr ptr, UIntPtr newSize)
    {
        IntPtr allocatedMemory = Interop.mincore.HeapReAlloc(Interop.mincore.GetProcessHeap(), 0, ptr, newSize);
        if (allocatedMemory == IntPtr.Zero)
        {
            throw new OutOfMemoryException();
        }
        return allocatedMemory;
    }
}
