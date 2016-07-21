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

        // Clones a Type[] array for the purpose of returning it from an api.
        public static Type[] CloneTypeArray(this Type[] types)
        {
            int count = types.Length;
            if (count == 0)
                return Array.Empty<Type>();  // Ok not to clone empty arrays - those are immutable.

            Type[] clonedTypes = new Type[count];
            for (int i = 0; i < count; i++)
            {
                clonedTypes[i] = types[i];
            }
            return clonedTypes;
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
                typeInfos[i] = types[i].CastToRuntimeTypeInfo();
            }
            return typeInfos;
        }

        public static string LastResortString(this RuntimeTypeHandle typeHandle)
        {
            return ReflectionCoreExecution.ExecutionEnvironment.GetLastResortString(typeHandle);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RuntimeNamedTypeInfo CastToRuntimeNamedTypeInfo(this Type type)
        {
            Debug.Assert(type != null);
            TypeInfo typeInfo = type.GetTypeInfo();
            Debug.Assert(typeInfo is RuntimeNamedTypeInfo);
            return (RuntimeNamedTypeInfo)typeInfo;
        }

        // TODO https://github.com/dotnet/corefx/issues/9805: Once TypeInfo and Type are the same instance, this implementation should just cast and not call GetTypeInfo().
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RuntimeTypeInfo CastToRuntimeTypeInfo(this Type type)
        {
            Debug.Assert(type != null);
            TypeInfo typeInfo = type.GetTypeInfo();
            Debug.Assert(typeInfo is RuntimeTypeInfo);
            return (RuntimeTypeInfo)typeInfo;
        }

        // TODO https://github.com/dotnet/corefx/issues/9805: Once TypeInfo and Type are the same instance, this overload should away.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RuntimeTypeInfo CastToRuntimeTypeInfo(this TypeInfo type)
        {
            Debug.Assert(type != null);
            Debug.Assert(type is RuntimeTypeInfo);
            return (RuntimeTypeInfo)type;
        }

        // TODO https://github.com/dotnet/corefx/issues/9805: Once TypeInfo derives from Type, this helper becomes a NOP and will go away entirely.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Type CastToType(this TypeInfo typeInfo)
        {
            return typeInfo == null ? null : typeInfo.AsType();
        }
    }
}
