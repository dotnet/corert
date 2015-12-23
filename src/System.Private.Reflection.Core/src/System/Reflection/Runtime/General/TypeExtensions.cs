// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using global::System;
using global::System.Diagnostics;
using global::Internal.Reflection.Core.NonPortable;

namespace System.Reflection.Runtime.General
{
    internal static class TypeExtensions
    {
        public static RuntimeType AsConfirmedRuntimeType(this Type type)
        {
            RuntimeType runtimeType = type as RuntimeType;
            if (runtimeType == null)
                throw new InvalidOperationException();
            return runtimeType;
        }
    }
}

