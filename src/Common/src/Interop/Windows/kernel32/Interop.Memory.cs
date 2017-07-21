// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

internal partial class Interop
{
    internal partial class Kernel32
    {
        [DllImport(Libraries.Kernel32)]
        unsafe internal static extern bool VirtualFree(void* address, UIntPtr numBytes, int pageFreeMode);

        unsafe internal static bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer)
        {
            buffer.length = sizeof(MEMORYSTATUSEX);
            return GlobalMemoryStatusExNative(ref buffer);
        }

        [DllImport(Libraries.Kernel32, SetLastError = true, EntryPoint = "GlobalMemoryStatusEx")]
        private static extern bool GlobalMemoryStatusExNative(ref MEMORYSTATUSEX buffer);

        [DllImport(Libraries.Kernel32, SetLastError = true)]
        unsafe internal static extern UIntPtr VirtualQuery(void* address, ref MEMORY_BASIC_INFORMATION buffer, UIntPtr sizeOfBuffer);

        [DllImport(Libraries.Kernel32, SetLastError = true)]
        unsafe internal static extern UIntPtr GetSystemInfo(ref SYSTEM_INFO info);

        internal const int MEM_COMMIT = 0x1000;
        internal const int MEM_RESERVE = 0x2000;
        internal const int MEM_RELEASE = 0x8000;
        internal const int MEM_FREE = 0x10000;
        internal const int PAGE_READWRITE = 0x04;

        [StructLayout(LayoutKind.Sequential)]
        internal struct SYSTEM_INFO
        {
            internal int dwOemId;
            internal int dwPageSize;
            internal UIntPtr lpMinimumApplicationAddress;
            internal UIntPtr lpMaximumApplicationAddress;
            internal UIntPtr dwActiveProcessorMask;
            internal int dwNumberOfProcessors;
            internal int dwProcessorType;
            internal int dwAllocationGranularity;
            internal short wProcessorLevel;
            internal short wProcessorRevision;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MEMORYSTATUSEX
        {
            // The length field must be set to the size of this data structure.
            internal int length;
            internal int memoryLoad;
            internal ulong totalPhys;
            internal ulong availPhys;
            internal ulong totalPageFile;
            internal ulong availPageFile;
            internal ulong totalVirtual;
            internal ulong availVirtual;
            internal ulong availExtendedVirtual;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct MEMORY_BASIC_INFORMATION
        {
            internal void* BaseAddress;
            internal void* AllocationBase;
            internal uint AllocationProtect;
            internal UIntPtr RegionSize;
            internal uint State;
            internal uint Protect;
            internal uint Type;
        }
    }
}
