// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Internal.Runtime.Augments
{
    public abstract class ReflectionTraceCallbacks
    {
        public abstract bool Enabled { get; }
        public abstract String GetTraceString(Type type);

        public abstract void Type_MakeGenericType(Type type, Type[] typeArguments);
        public abstract void Type_MakeArrayType(Type type);
        public abstract void Type_FullName(Type type);
        public abstract void Type_Namespace(Type type);
        public abstract void Type_AssemblyQualifiedName(Type type);
        public abstract void Type_Name(Type type);
    }
}

