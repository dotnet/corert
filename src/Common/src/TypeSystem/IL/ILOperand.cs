// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Internal.IL
{
    [StructLayout(LayoutKind.Explicit)]
    public struct ILOperand
    {
        [FieldOffset(0)]
        private int _int32;

        [FieldOffset(0)]
        private long _int64;

        [FieldOffset(0)]
        private double _double;

        public static ILOperand FromInt32(int int32)
        {
            return new ILOperand { _int32 = int32 };
        }

        public static ILOperand FromInt64(long int64)
        {
            return new ILOperand { _int64 = int64 };
        }

        public static ILOperand FromDouble(double d)
        {
            return new ILOperand { _double = d };
        }

        public int AsInt32()
        {
            return _int32;
        }

        public long AsInt64()
        {
            return _int64;
        }

        public double AsDouble()
        {
            return _double;
        }
    }
}
