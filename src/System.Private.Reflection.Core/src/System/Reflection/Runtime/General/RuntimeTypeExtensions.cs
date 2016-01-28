// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Reflection.Runtime.TypeInfos;

using global::Internal.Reflection.Core;
using global::Internal.Reflection.Core.Execution;
using global::Internal.Reflection.Core.NonPortable;

namespace System.Reflection.Runtime.General
{
    internal static class RuntimeTypeExtensions
    {
        public static RuntimeTypeInfo GetRuntimeTypeInfo(this RuntimeType runtimeType)
        {
            return RuntimeTypeInfo.GetRuntimeTypeInfo(runtimeType);
        }

        public static RUNTIMETYPEINFO GetRuntimeTypeInfo<RUNTIMETYPEINFO>(this Type type)
            where RUNTIMETYPEINFO : RuntimeTypeInfo
        {
            Debug.Assert(type is RuntimeType);
            return (RUNTIMETYPEINFO)(type.GetTypeInfo());
        }

        public static ReflectionDomain GetReflectionDomain(this RuntimeType runtimeType)
        {
            return ReflectionCoreExecution.ExecutionDomain; //@todo: User Reflection Domains not yet supported.
        }
    }
}


