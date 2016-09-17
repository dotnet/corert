// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;

namespace System.Runtime.CompilerServices
{
    // This attribute is only for use in a Class Library 
    [AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field)]
    internal sealed class IntrinsicAttribute : Attribute { }

#if !CORERT
    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class BoundAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class BoundsCheckingAttribute : Attribute { }
#endif

    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class StackOnlyAttribute : Attribute { }

#if false // Unused right now. It is likely going to be useful for Span<T> implementation.
    // This is a dummy class to be replaced by the compiler with a ref T
    // It has to be a dummy class to avoid complicated type substitution
    // and other complications in the compiler.
    public sealed class ByReference<T>
    {
        //
        // Managed pointer creation
        //
        [Intrinsic]
        public static extern ByReference<T> FromRef(ref T source);

        [Intrinsic]
        public static extern ref T ToRef(ByReference<T> source);
    }
#endif
}
