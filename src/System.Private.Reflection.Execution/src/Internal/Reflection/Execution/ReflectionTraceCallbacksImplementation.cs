// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;
using global::System.Runtime.CompilerServices;

using global::Internal.Runtime.Augments;
using global::Internal.Reflection.Tracing;


namespace Internal.Reflection.Execution
{
    //
    // ReflectionTrace initialization is in its own class so that non-devexperience builds can remove it.
    //
    [DeveloperExperienceModeOnly]
    internal static class ReflectionTracingInitializer
    {
        public static void Initialize()
        {
            ReflectionTraceConnector.Initialize(new ReflectionTraceCallbacksImplementation());
        }

        private sealed class ReflectionTraceCallbacksImplementation : ReflectionTraceCallbacks
        {
            public sealed override bool Enabled
            {
                get
                {
                    return ReflectionTrace.Enabled;
                }
            }

            public sealed override String GetTraceString(Type type)
            {
                return type.GetTypeInfo().GetTraceString();
            }

            public sealed override void Type_MakeGenericType(Type type, Type[] typeArguments)
            {
                ReflectionTrace.Type_MakeGenericType(type, typeArguments);
            }

            public sealed override void Type_MakeArrayType(Type type)
            {
                ReflectionTrace.Type_MakeArrayType(type);
            }

            public sealed override void Type_FullName(Type type)
            {
                ReflectionTrace.Type_FullName(type);
            }

            public sealed override void Type_Namespace(Type type)
            {
                ReflectionTrace.Type_Namespace(type);
            }

            public sealed override void Type_AssemblyQualifiedName(Type type)
            {
                ReflectionTrace.Type_AssemblyQualifiedName(type);
            }

            public sealed override void Type_Name(Type type)
            {
                ReflectionTrace.Type_Name(type);
            }
        }
    }
}

