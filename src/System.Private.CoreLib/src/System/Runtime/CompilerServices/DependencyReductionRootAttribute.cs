// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.Runtime.CompilerServices
{
    // When applied to a type this custom attribute will cause the type to become a new dependency
    // reduction root.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Constructor, Inherited = false, AllowMultiple = false)]
    public class DependencyReductionRootAttribute : Attribute
    {
    }
}
