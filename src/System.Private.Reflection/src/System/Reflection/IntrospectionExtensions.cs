// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


/*============================================================
**
  Type:  IntrospectionExtensions
**
==============================================================*/

using global::System;
using global::Internal.Reflection.Augments;

namespace System.Reflection
{
    public static class IntrospectionExtensions
    {
        public static TypeInfo GetTypeInfo(this Type type)
        {
            return ReflectionAugments.ReflectionCoreCallbacks.GetTypeInfo(type);
        }
    }
}

