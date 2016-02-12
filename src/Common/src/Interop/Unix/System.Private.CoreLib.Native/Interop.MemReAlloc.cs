// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal unsafe partial class Sys
    {
        [DllImport(Interop.Libraries.CoreLibNative, EntryPoint = "CoreLibNative_MemReAlloc")]
        internal static extern IntPtr MemReAlloc(IntPtr ptr, UIntPtr newSize);
        
        [DllImport(Interop.Libraries.CoreLibNative, EntryPoint = "CoreLibNative_MemSet")]
        internal static extern IntPtr MemSet(IntPtr ptr, int c, UIntPtr newSize);
    }

    internal unsafe static IntPtr MemReAlloc(IntPtr ptr, UIntPtr newSize)
    {
        return MemReAlloc(ptr, UIntPtr.Zero, newSize , 0);
    }
    
    internal static unsafe IntPtr MemReAlloc(IntPtr ptr, UIntPtr oldSize, UIntPtr newSize, uint flags)
    {
        IntPtr allocatedMemory = Interop.Sys.MemReAlloc(ptr, newSize);
        if (allocatedMemory == IntPtr.Zero)
        {
            throw new OutOfMemoryException();
        }
        
        if ((flags & HEAP_ZERO_MEMORY) != 0 && (int) newSize > (int) oldSize)
        {
            IntPtr pBuffer = (IntPtr) (((byte *) allocatedMemory) + (int) oldSize);
            Interop.Sys.MemSet(pBuffer, 0, (UIntPtr) ((int) newSize - (int) oldSize));
        }
        return allocatedMemory;
    }
}
