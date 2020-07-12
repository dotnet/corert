// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal unsafe partial class Sys
    {
        [DllImport(Interop.Libraries.CoreLibNative, EntryPoint = "CoreLibNative_MemAllocWithZeroInitialize")]
        internal static extern IntPtr MemAllocWithZeroInitialize(UIntPtr sizeInBytes);

        [DllImport(Interop.Libraries.CoreLibNative, EntryPoint = "CoreLibNative_MemSet")]
        internal static extern IntPtr MemSet(IntPtr ptr, int c, UIntPtr newSize);
    }

    internal static IntPtr MemAllocWithZeroInitializeNoThrow(UIntPtr sizeInBytes)
    {
        return Interop.Sys.MemAllocWithZeroInitialize(sizeInBytes);
    }

    internal static unsafe IntPtr MemReAllocWithZeroInitializeNoThrow(IntPtr ptr, UIntPtr oldSize, UIntPtr newSize)
    {
        IntPtr allocatedMemory = Interop.Sys.MemReAlloc(ptr, newSize);
        if (allocatedMemory != IntPtr.Zero && (long) newSize > (long) oldSize)
        {
            IntPtr pBuffer = (IntPtr) (((byte *) allocatedMemory) + (long) oldSize);
            Interop.Sys.MemSet(pBuffer, 0, (UIntPtr) ((long) newSize - (long) oldSize));
        }
        return allocatedMemory;
    }
}
