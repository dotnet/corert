// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal static partial class mincore
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct MEMORY_BASIC_INFORMATION
        {
            internal IntPtr BaseAddress;
            internal IntPtr AllocationBase;
            internal uint AllocationProtect;
            internal UIntPtr RegionSize;
            internal uint State;
            internal uint Protect;
            internal uint Type;
        }

        [DllImport(Interop.Libraries.Memory)]
        internal extern static UIntPtr VirtualQuery(IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, UIntPtr dwLength);
    }
}
