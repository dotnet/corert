// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Reflection.Runtime.TypeInfos;

using Internal.Reflection.Core.NonPortable;
using Internal.Reflection.Core.Execution;

namespace System.Reflection.Runtime.General
{
    internal static class Helpers
    {
        // This helper helps reduce the temptation to write "h == default(RuntimeTypeHandle)" which causes boxing.
        public static bool IsNull(this RuntimeTypeHandle h)
        {
            return h.Equals(default(RuntimeTypeHandle));
        }

        public static Type[] CloneTypeArray(this Type[] types)
        {
            if (types.Length == 0)
                return Array.Empty<Type>();
            return (Type[])(types.Clone());
        }

        // TODO https://github.com/dotnet/corefx/issues/9805: This overload can and should be deleted once TypeInfo derives from Type again.
        public static Type[] CloneTypeArray(this TypeInfo[] types)
        {
            if (types.Length == 0)
                return Array.Empty<Type>();
            Type[] clonedTypes = new Type[types.Length];
            for (int i = 0; i < types.Length; i++)
            {
                clonedTypes[i] = types[i].AsType();
            }
            return clonedTypes;
        }

        public static bool IsRuntimeImplemented(this Type type)
        {
            return type is IRuntimeImplementedType;
        }

        public static RuntimeTypeInfo[] ToRuntimeTypeInfoArray(this Type[] types)
        {
            int count = types.Length;
            RuntimeTypeInfo[] typeInfos = new RuntimeTypeInfo[count];
            for (int i = 0; i < count; i++)
            {
                typeInfos[i] = types[i].GetRuntimeTypeInfo<RuntimeTypeInfo>();
            }
            return typeInfos;
        }

        public static string LastResortString(this RuntimeTypeHandle typeHandle)
        {
            return ReflectionCoreExecution.ExecutionEnvironment.GetLastResortString(typeHandle);
        }

        // TODO https://github.com/dotnet/corefx/issues/9805: Once TypeInfo derives from Type, this helper becomes a NOP and will go away entirely.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Type CastToType(this TypeInfo typeInfo)
        {
            return typeInfo == null ? null : typeInfo.AsType();
        }
    }
}
