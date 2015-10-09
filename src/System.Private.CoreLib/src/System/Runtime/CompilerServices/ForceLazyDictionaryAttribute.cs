// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.Runtime.CompilerServices
{
    //
    // When applied to a generic type or a method, this custom attribute forces use of lazy dictionaries. This allows static compilation
    // to succeed in the presence of constructs that trigger infinite generic expansion.
    //
    [DependencyReductionRoot]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Delegate | AttributeTargets.Enum | AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class ForceLazyDictionaryAttribute : Attribute
    {
        public ForceLazyDictionaryAttribute() { }
    }
}
