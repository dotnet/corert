// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;

namespace Internal.Runtime.CompilerServices
{
    [StructLayout(LayoutKind.Sequential)]
    [CLSCompliant(false)]
    public unsafe struct RuntimeFieldHandleInfo
    {
        public IntPtr NativeLayoutInfoSignature;
    }
}