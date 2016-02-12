// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal const int HEAP_ZERO_MEMORY = 0x8;
    
    internal unsafe partial class Sys
    {
        [DllImport(Interop.Libraries.CoreLibNative, EntryPoint = "CoreLibNative_MemAlloc")]
        internal static extern IntPtr MemAlloc(UIntPtr sizeInBytes);
                
        [DllImport(Interop.Libraries.CoreLibNative, EntryPoint = "CoreLibNative_MemAllocWithZeroInitialize")]
        internal static extern IntPtr MemAllocWithZeroInitialize(UIntPtr sizeInBytes);
        
        [DllImport(Interop.Libraries.CoreLibNative, EntryPoint = "CoreLibNative_MemFree")]
        internal static extern void MemFree(IntPtr ptr);
    }
    
    internal static IntPtr MemAlloc(UIntPtr sizeInBytes)
    {
        return MemAlloc(sizeInBytes, 0); 
    }
    
    internal static IntPtr MemAlloc(UIntPtr sizeInBytes, uint flags)
    {
        IntPtr allocatedMemory = (flags & HEAP_ZERO_MEMORY) == 0 ? Interop.Sys.MemAlloc(sizeInBytes) : Interop.Sys.MemAllocWithZeroInitialize(sizeInBytes);
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
