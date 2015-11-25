// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using global::System;
using global::System.Diagnostics;
using global::System.Runtime.InteropServices;

namespace Internal.StackGenerator.Dia
{
    [McgIntrinsics]
    internal static class S
    {
        public static unsafe T StdCall<T>(IntPtr pMethod, IntPtr pThis) { throw NotImplemented.ByDesign; }
        public static unsafe T StdCall<T>(IntPtr pMethod, IntPtr pThis, char* pc) { throw NotImplemented.ByDesign; }
        public static unsafe T StdCall<T>(IntPtr pMethod, IntPtr pThis, out IntPtr ppOut) { throw NotImplemented.ByDesign; }
        public static unsafe T StdCall<T>(IntPtr pMethod, IntPtr pThis, out long ppOut) { throw NotImplemented.ByDesign; }
        public static unsafe T StdCall<T>(IntPtr pMethod, IntPtr pThis, out int pOut) { throw NotImplemented.ByDesign; }
        public static unsafe T StdCall<T>(IntPtr pMethod, IntPtr pThis, int i, out IntPtr ppOut) { throw NotImplemented.ByDesign; }
        public static unsafe T StdCall<T>(IntPtr pMethod, IntPtr pThis, int i, int j, out IntPtr ppOut) { throw NotImplemented.ByDesign; }
        public static unsafe T StdCall<T>(IntPtr pMethod, IntPtr pThis, IntPtr parent, int symTag, char* name, int compareFlags, out IntPtr ppResult) { throw NotImplemented.ByDesign; }
    }
}



