// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.Runtime.CompilerServices
{
    // When applied to a type this custom attribute will cause any static class constructor to be run eagerly
    // at module load time rather than deferred till just before the class is used.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
    public class EagerStaticClassConstructionAttribute : Attribute
    {
    }
}
