// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime;

internal partial class Interop
{
    //
    // @todo: Can we get MCG to generate these. Do we even want it to?
    //
    internal class msvcrt
    {
        // This intrinsic does not work yet, but placing definition of it here so that we can build it in the NUTC.
        [Intrinsic]
        internal static unsafe extern void memmoveWithGCPointers(byte* dmem, byte* smem, int size);
    }
}
