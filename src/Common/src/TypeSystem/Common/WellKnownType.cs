// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Internal.TypeSystem
{
    public enum WellKnownType
    {
        Unknown,

        // Primitive types are first - keep in sync with type flags
        Void,
        Boolean,
        Char,
        SByte,
        Byte,
        Int16,
        UInt16,
        Int32,
        UInt32,
        Int64,
        UInt64,
        IntPtr,
        UIntPtr,
        Single,
        Double,

        ValueType,
        Enum,
        Nullable,

        Object,
        String,
        Array,
        MulticastDelegate,

        RuntimeTypeHandle,
        RuntimeMethodHandle,
        RuntimeFieldHandle,

        Exception,
    }
}
