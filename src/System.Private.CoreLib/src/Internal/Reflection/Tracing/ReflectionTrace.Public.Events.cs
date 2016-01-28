// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Collections.Generic;

using ReflectionTraceCallbacks = Internal.Runtime.Augments.ReflectionTraceCallbacks;

namespace Internal.Reflection.Tracing
{
    //
    // The individual event methods. These are in a separate file to allow them to be tool-generated.
    //
    internal static partial class ReflectionTrace
    {
        public static void Type_MakeGenericType(Type type, Type[] typeArguments)
        {
            ReflectionTraceCallbacks callbacks = s_callbacks;
            if (callbacks == null)
                return;
            callbacks.Type_MakeGenericType(type, typeArguments);
            return;
        }

        public static void Type_MakeArrayType(Type type)
        {
            ReflectionTraceCallbacks callbacks = s_callbacks;
            if (callbacks == null)
                return;
            callbacks.Type_MakeArrayType(type);
            return;
        }

        public static void Type_FullName(Type type)
        {
            ReflectionTraceCallbacks callbacks = s_callbacks;
            if (callbacks == null)
                return;
            callbacks.Type_FullName(type);
            return;
        }

        public static void Type_Namespace(Type type)
        {
            ReflectionTraceCallbacks callbacks = s_callbacks;
            if (callbacks == null)
                return;
            callbacks.Type_Namespace(type);
            return;
        }

        public static void Type_AssemblyQualifiedName(Type type)
        {
            ReflectionTraceCallbacks callbacks = s_callbacks;
            if (callbacks == null)
                return;
            callbacks.Type_AssemblyQualifiedName(type);
            return;
        }

        public static void Type_Name(Type type)
        {
            ReflectionTraceCallbacks callbacks = s_callbacks;
            if (callbacks == null)
                return;
            callbacks.Type_Name(type);
            return;
        }
    }
}

