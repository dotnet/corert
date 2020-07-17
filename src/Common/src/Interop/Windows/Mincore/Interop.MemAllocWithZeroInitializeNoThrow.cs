// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal static unsafe partial class mincore
    {
        internal const int HEAP_ZERO_MEMORY = 0x8;      // Flag to zero memory
    }

    internal static IntPtr MemAllocWithZeroInitializeNoThrow(UIntPtr sizeInBytes)
    {
        return Interop.mincore.HeapAlloc(Interop.mincore.GetProcessHeap(), Interop.mincore.HEAP_ZERO_MEMORY, sizeInBytes);
    }

    internal static IntPtr MemReAllocWithZeroInitializeNoThrow(IntPtr ptr, UIntPtr oldSize, UIntPtr newSize)
    {
        return Interop.mincore.HeapReAlloc(Interop.mincore.GetProcessHeap(), Interop.mincore.HEAP_ZERO_MEMORY, ptr, newSize);
    }
}
