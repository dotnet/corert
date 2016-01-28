// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Internal.LightweightInterop
{
    [McgIntrinsics]
    internal static class S
    {
        public static T StdCall<T>(IntPtr pMethod, IntPtr pThis) { throw NotImplemented.ByDesign; }
    }
}

