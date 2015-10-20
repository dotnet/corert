// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.Runtime.CompilerServices
{
    // When applied to a type this custom attribute will cause the type to be necessary
    // if dependencyType is necessary
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = true)]
    public class DependencyReductionConditionallyDependentAttribute : Attribute
    {
        public DependencyReductionConditionallyDependentAttribute(System.Type dependencyType)
        {
        }
    }
}
