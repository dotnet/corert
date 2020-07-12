// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal unsafe partial class Sys
    {
        [DllImport(Interop.Libraries.CoreLibNative, EntryPoint = "CoreLibNative_MemReAlloc")]
        internal static extern IntPtr MemReAlloc(IntPtr ptr, UIntPtr newSize);
    }

    internal static unsafe IntPtr MemReAlloc(IntPtr ptr, UIntPtr newSize)
    {
        IntPtr allocatedMemory = Interop.Sys.MemReAlloc(ptr, newSize);
        if (allocatedMemory == IntPtr.Zero)
        {
            throw new OutOfMemoryException();
        }
        return allocatedMemory;
    }
}
