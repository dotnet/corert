// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using global::System;
using global::System.Diagnostics;
using global::System.Runtime.InteropServices;

namespace Internal.LightweightInterop
{
    [McgIntrinsics]
    internal static class S
    {
        public static T StdCall<T>(IntPtr pMethod, IntPtr pThis) { throw NotImplemented.ByDesign; }
    }
}

