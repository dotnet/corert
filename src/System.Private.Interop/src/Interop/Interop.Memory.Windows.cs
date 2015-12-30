// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;


namespace System.Runtime.InteropServices
{
    public static partial class ExternalInterop
    {
        private static partial class Libraries
        {
#if TARGET_CORE_API_SET
        internal const string CORE_HEAP = "api-ms-win-core-heap-l1-1-0.dll";
#else
        internal const string CORE_HEAP = "kernel32.dll";

#endif //TARGET_CORE_API_SET
        }

        [DllImport(Libraries.CORE_HEAP)]
        [McgGeneratedNativeCallCodeAttribute]
        private static extern IntPtr GetProcessHeap();


        [DllImport(Libraries.CORE_HEAP)]
        [McgGeneratedNativeCallCodeAttribute]
        private static unsafe extern IntPtr HeapAlloc(IntPtr hHeap, UInt32 dwFlags, UIntPtr sizeInBytes);


        [DllImport(Libraries.CORE_HEAP)]
        [McgGeneratedNativeCallCodeAttribute]
        private static unsafe extern int HeapFree(IntPtr hHeap, UInt32 dwFlags, IntPtr lpMem);


        [DllImport(Libraries.CORE_HEAP)]
        [McgGeneratedNativeCallCodeAttribute]
        private static unsafe extern IntPtr HeapReAlloc(IntPtr hHeap, UInt32 dwFlags, IntPtr lpMem, UIntPtr dwBytes);
        
        public static  IntPtr MemAlloc(UIntPtr sizeInBytes)
        {
            return HeapAlloc(GetProcessHeap(), 0, sizeInBytes);
        }
        public static unsafe void MemFree(IntPtr ptr)
        {
            HeapFree(GetProcessHeap(), 0, ptr);
        }

        public static unsafe IntPtr MemReAlloc(IntPtr ptr, UIntPtr newSize)
        {
            return HeapReAlloc(GetProcessHeap(), 0, ptr, newSize);
        }

        // Helper functions
        internal static IntPtr MemAlloc(UIntPtr sizeInBytes, uint flags)
        {
            return HeapAlloc(GetProcessHeap(), flags, sizeInBytes);
        }

        internal unsafe static IntPtr MemAlloc(IntPtr sizeInBytes)
        {
            return HeapAlloc(GetProcessHeap(), 0,(UIntPtr)(void*)sizeInBytes);
        }
        internal unsafe static IntPtr MemReAlloc(IntPtr ptr, IntPtr newSize)
        {
            return HeapReAlloc(GetProcessHeap(), 0, ptr, (UIntPtr)(void*)newSize);
        }
        internal static IntPtr MemReAlloc(IntPtr ptr, UIntPtr newSize ,uint flags)
        {
            return HeapReAlloc(GetProcessHeap(), flags, ptr, newSize);
        }
    }
}
