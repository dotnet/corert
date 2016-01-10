// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Runtime.InteropServices
{
    internal static class Libraries
    {
        internal const string CoreLibNative = "System.Private.CoreLib.Native";
    }

    [CLSCompliant(false)]
    public partial class ExternalInterop
    {
        [DllImport(Libraries.CoreLibNative, EntryPoint = "CoreLibNative_MemAlloc")]
        internal static unsafe extern IntPtr MemAlloc(UIntPtr sizeInBytes);

        [DllImport(Libraries.CoreLibNative, EntryPoint = "CoreLibNative_MemFree")]
        internal static unsafe extern void MemFree(IntPtr ptr);

        [DllImport(Libraries.CoreLibNative, EntryPoint = "CoreLibNative_MemReAlloc")]
        internal static unsafe extern IntPtr MemReAlloc(IntPtr ptr, UIntPtr newSize);
    }
}
