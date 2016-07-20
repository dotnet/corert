// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Reflection.Runtime.Types;
using global::System.Reflection.Runtime.General;
using global::System.Reflection.Runtime.TypeInfos;
using global::System.Runtime.CompilerServices;

using global::Internal.Reflection.Core;
using global::Internal.Reflection.Core.Execution;
using global::Internal.Reflection.Core.NonPortable;

namespace System.Reflection.Runtime.General
{
    internal static class RuntimeTypeExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RUNTIMETYPEINFO GetRuntimeTypeInfo<RUNTIMETYPEINFO>(this Type type)
            where RUNTIMETYPEINFO : RuntimeTypeInfo
        {
            Debug.Assert(type != null);
            Debug.Assert(type.IsRuntimeImplemented());
            return (RUNTIMETYPEINFO)(type.GetTypeInfo());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RUNTIMETYPEINFO GetRuntimeTypeInfo<RUNTIMETYPEINFO>(this TypeInfo type)
            where RUNTIMETYPEINFO : RuntimeTypeInfo
        {
            Debug.Assert(type != null);
            Debug.Assert(type is RUNTIMETYPEINFO);
            return (RUNTIMETYPEINFO)type;
        }

        public static ReflectionDomain GetReflectionDomain(this RuntimeTypeInfo runtimeType)
        {
            return ReflectionCoreExecution.ExecutionDomain; //@todo: User Reflection Domains not yet supported.
        }
    }
}


