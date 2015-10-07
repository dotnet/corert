// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Internal.TypeSystem
{
    [Flags]
    public enum TypeFlags
    {
        CategoryMask    = 0x3F,

        // Primitive
        Unknown         = 0x00,
        Void            = 0x01,
        Boolean         = 0x02,
        Char            = 0x03,
        SByte           = 0x04,
        Byte            = 0x05,
        Int16           = 0x06,
        UInt16          = 0x07,
        Int32           = 0x08,
        UInt32          = 0x09,
        Int64           = 0x0A,
        UInt64          = 0x0B,
        IntPtr          = 0x0C,
        UIntPtr         = 0x0D,
        Single          = 0x0E,
        Double          = 0x0F,

        ValueType       = 0x10,
        Enum            = 0x11, // Parent is enum
        Nullable        = 0x12, // Nullable instantiation
        // Unused         0x13

        Class           = 0x14,
        Interface       = 0x15,
        // Unused         0x16

        Array           = 0x17,
        ByRef           = 0x18,
        Pointer         = 0x19,

        GenericParameter = 0x1C,

        ContainsGenericVariables         = 0x100,
        ContainsGenericVariablesComputed = 0x200,
    }
}
