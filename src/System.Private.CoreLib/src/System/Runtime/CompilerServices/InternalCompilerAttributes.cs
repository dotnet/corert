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

#if PROJECTN
    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class BoundAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class BoundsCheckingAttribute : Attribute { }
#endif

    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class StackOnlyAttribute : Attribute { }
}
