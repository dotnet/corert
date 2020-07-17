// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

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



