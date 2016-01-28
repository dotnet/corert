// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
