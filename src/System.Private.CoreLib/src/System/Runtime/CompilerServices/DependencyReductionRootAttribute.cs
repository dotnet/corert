// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Runtime.CompilerServices
{
    // When applied to a type this custom attribute will cause the type to become a new dependency
    // reduction root.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
    public class DependencyReductionRootAttribute : Attribute
    {
    }
}
