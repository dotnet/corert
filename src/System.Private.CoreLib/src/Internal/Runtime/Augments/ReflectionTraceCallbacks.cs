// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Internal.Runtime.Augments
{
    public abstract class ReflectionTraceCallbacks
    {
        public abstract bool Enabled { get; }
#if DEBUG
        public abstract String GetTraceString(Type type);
#endif

        public abstract void Type_MakeGenericType(Type type, Type[] typeArguments);
        public abstract void Type_MakeArrayType(Type type);
        public abstract void Type_FullName(Type type);
        public abstract void Type_Namespace(Type type);
        public abstract void Type_AssemblyQualifiedName(Type type);
        public abstract void Type_Name(Type type);
    }
}

