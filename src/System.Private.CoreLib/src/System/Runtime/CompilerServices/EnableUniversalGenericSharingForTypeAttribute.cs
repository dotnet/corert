// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.Runtime.CompilerServices
{
    //
    // Types specified by this attribute will have a universal generic instantiation form, which can
    // be used by the runtime as a template to dynamically create specific instantiations.
    //
    // The types by this attribute *must* be generic types.
    //
    // This attribute is temporary and will be removed once the work on the universal generics features
    // is complete, and we implement a policy that defines which types will get universal generic 
    // instantiations.
    //
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
    public class EnableUniversalGenericSharingForTypeAttribute : Attribute
    {
        public EnableUniversalGenericSharingForTypeAttribute(System.Type genericTypeDef) { /* nothing to do */ }
    }
}
