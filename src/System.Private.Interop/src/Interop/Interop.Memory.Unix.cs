// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Runtime.InteropServices
{
    internal static class Libraries
    {
        internal const string SystemPrivateCoreLibNative = "System.Private.CoreLib.Native";
    }

    [CLSCompliant(false)]
    public partial class ExternalInterop
    {
        [DllImport(Libraries.SystemPrivateCoreLibNative)]
        internal static unsafe extern IntPtr MemAlloc(UIntPtr sizeInBytes);

        [DllImport(Libraries.SystemPrivateCoreLibNative)]
        internal static unsafe extern void MemFree(IntPtr ptr);

        [DllImport(Libraries.SystemPrivateCoreLibNative)]
        internal static unsafe extern IntPtr MemReAlloc(IntPtr ptr, UIntPtr newSize);
    }
}
