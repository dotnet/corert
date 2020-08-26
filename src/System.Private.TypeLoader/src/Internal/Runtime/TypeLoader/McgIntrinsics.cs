// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;

namespace System.Runtime.CompilerServices
{
    internal sealed class IntrinsicAttribute : Attribute
    {
    }
}

namespace System.Runtime.InteropServices
{
    [AttributeUsage((System.AttributeTargets.Method | System.AttributeTargets.Class))]
    internal class McgIntrinsicsAttribute : Attribute
    {
    }
}

namespace Internal.Runtime.TypeLoader
{
    [System.Runtime.InteropServices.McgIntrinsics]
    internal static partial class Intrinsics
    {
        internal static IntPtr AddrOf<T>(T ftn)
        {
            // This method is implemented elsewhere in the toolchain
            throw new PlatformNotSupportedException();
        }

        public static void Call(System.IntPtr pfn, object obj)
        {
            // This method is implemented elsewhere in the toolchain
            throw new PlatformNotSupportedException();
        }
    }
}
