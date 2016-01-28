// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

