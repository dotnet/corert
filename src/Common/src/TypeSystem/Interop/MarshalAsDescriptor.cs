// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using System;

namespace Internal.TypeSystem
{
    [Flags]
    public enum NativeType : byte
    {
        Invalid = 0x0,
        Boolean = 0x2,
        I1 = 0x3,
        U1 = 0x4,
        I2 = 0x5,
        U2 = 0x6,
        I4 = 0x7,
        U4 = 0x8,
        I8 = 0x9,
        U8 = 0xa,
        R4 = 0xb,
        R8 = 0xc,
        LPStr = 0x14,
        LPWStr = 0x15,
        Int = 0x1f,
        UInt = 0x20,
        Func = 0x26,
        Array = 0x2a,
        LPStruct = 0x2b,    // This is not  defined in Ecma-335(II.23.4)
        Max = 0x50,         // The value is of this not defined in Ecma-335(II.23.4) either, the one defined in CoreCLR is used here
    }

    public class MarshalAsDescriptor
    {
        public NativeType Type { get; }
        public NativeType ArraySubType { get; }
        public uint SizeParamIndex { get; }
        public uint SizeConst { get; }

        public MarshalAsDescriptor(NativeType type, NativeType arraySubType, uint sizeParamIndex, uint sizeConst)
        {
            Type = type;
            ArraySubType = arraySubType;
            SizeParamIndex = sizeParamIndex;
            SizeConst = sizeConst;
        }
    }
}