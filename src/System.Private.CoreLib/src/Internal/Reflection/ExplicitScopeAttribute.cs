// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Internal.Reflection
{
    //
    // This attribute is used to affect the generation of Project N metadata. When applied to a class,
    // it will cause the metadata generator to emit the type into a user-specified scope (creating it if necessary.)
    //
    // For Fx, One use of this is to allow us to generate metadata for non-public types for private use by other FX components.
    // By throwing it into a private scope that Reflection knows about, we can prevent Reflection's own consumers
    // from seeing the internal type.
    // For FX, it doesn't use(or reference) this attribute in this contact. Each FX assembly via a transform will inject a definition of this attribute and reference that one

    // For Interop, We use it to pass native winmd winrt type's winmd infomation to CreateMetadata Transform, so it knows where this 
    // native winmd winrt type originally comes from.    
    // For Interop, it will use this attribute in this contract during Mcg Transform.
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Struct | AttributeTargets.Delegate, Inherited = false)]
    public sealed class ExplicitScopeAttribute : Attribute
    {
        public ExplicitScopeAttribute(string assemblyIdentity)
        {
        }
    }
}
