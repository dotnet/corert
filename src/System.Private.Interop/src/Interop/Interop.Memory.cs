// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;


namespace System.Runtime.InteropServices
{
    public static partial class ExternalInterop
    {
#if CORECLR

        public static  IntPtr MemAlloc(UIntPtr sizeInBytes)
        {
            return Marshal.AllocHGlobal(unchecked( (IntPtr) (long)(ulong)sizeInBytes));
        }

        public static unsafe void MemFree(IntPtr ptr)
        {
            Marshal.FreeHGlobal(ptr);
        }

        public static unsafe IntPtr MemReAlloc(IntPtr ptr, UIntPtr newSize)
        {
            return Marshal.ReAllocHGlobal(ptr, unchecked( (IntPtr) (long)(ulong)newSize));
        }

        internal static IntPtr MemAllocWithZeroInitializeNoThrow(UIntPtr sizeInBytes)
        {
            throw new NotSupportedException();
        }

        internal static IntPtr MemReAllocWithZeroInitializeNoThrow(IntPtr ptr, UIntPtr oldSize, UIntPtr newSize)
        {
            throw new NotSupportedException();
        }

#else
        // Helper functions
        public static  IntPtr MemAlloc(UIntPtr sizeInBytes)
        {
            return Interop.MemAlloc(sizeInBytes);
        }

        public static unsafe IntPtr MemAlloc(IntPtr sizeInBytes)
        {
            return Interop.MemAlloc((UIntPtr)(void *) sizeInBytes);
        }

        public static unsafe void MemFree(IntPtr ptr)
        {
            Interop.MemFree(ptr);
        }

        public static unsafe IntPtr MemReAlloc(IntPtr ptr, UIntPtr newSize)
        {
            return Interop.MemReAlloc(ptr, newSize);
        }
        
        public static unsafe IntPtr MemReAlloc(IntPtr ptr, IntPtr newSize)
        {
            return Interop.MemReAlloc(ptr, (UIntPtr)(void *)newSize);
        }

        internal static IntPtr MemAllocWithZeroInitializeNoThrow(UIntPtr sizeInBytes)
        {
            return Interop.MemAllocWithZeroInitializeNoThrow(sizeInBytes);
        }

        internal static IntPtr MemReAllocWithZeroInitializeNoThrow(IntPtr ptr, UIntPtr oldSize, UIntPtr newSize)
        {
            return Interop.MemReAllocWithZeroInitializeNoThrow(ptr, oldSize, newSize);
        }
#endif //CORECLR

    }
}
