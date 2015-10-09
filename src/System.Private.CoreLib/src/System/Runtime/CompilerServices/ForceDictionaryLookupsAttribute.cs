// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.Runtime.CompilerServices
{
    // When applied to a type this custom attribute will force use of statically precompiled dictionary looks that
    // do not depend on lazy resolution by the template type loader
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
    public class ForceDictionaryLookupsAttribute : Attribute
    {
    }
}
