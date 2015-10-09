// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.Runtime.CompilerServices
{
    // When applied to a type this custom attribute cause the type to be treated as reflection blocked.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Delegate | AttributeTargets.Enum | AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
    [DependencyReductionRoot]
    public class ReflectionBlockedAttribute : Attribute
    {
    }
}
