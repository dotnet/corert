// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;

namespace Internal.Reflection.Core.NonPortable
{
    public static class ReflectionCoreNonPortable
    {
        public static TypeLoadException CreateTypeLoadException(String message, String typeName)
        {
            return new TypeLoadException(message, typeName);
        }

        internal static Type GetTypeForRuntimeTypeHandle(RuntimeTypeHandle runtimeTypeHandle)
        {
            return RuntimeTypeUnifier.GetTypeForRuntimeTypeHandle(runtimeTypeHandle);
        }

        internal static Type GetRuntimeTypeForEEType(EETypePtr eeType)
        {
            return RuntimeTypeUnifier.GetTypeForRuntimeTypeHandle(new RuntimeTypeHandle(eeType));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsRuntimeImplemented(this Type type)
        {
            return type is IRuntimeImplementedType;
        }
    }
}


