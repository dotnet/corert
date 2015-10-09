// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.Runtime.CompilerServices
{
    // Marks a type or a member as injected by the toolchain so that it can be treated specially by subsequent transforms.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Constructor, Inherited = false, AllowMultiple = false)]
    public sealed class ILTransformInjectedAttribute : Attribute
    {
    }
}
