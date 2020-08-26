// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Internal.IL;
using Internal.TypeSystem;

namespace Internal.Runtime.Interpreter
{
    [StructLayout(LayoutKind.Explicit)]
    internal unsafe struct StackItem
    {
        [FieldOffset(0)]
        private StackValueKind _kind;

        [FieldOffset(8)]
        private int _int32;

        [FieldOffset(8)]
        private long _int64;

        [FieldOffset(8)]
        private IntPtr _nativeInt;

        [FieldOffset(8)]
        private double _double;

        [FieldOffset(16)]
        private ValueType _valueType;

        [FieldOffset(16)]
        private object _objref;

        public StackValueKind Kind => _kind;

        public static StackItem FromInt32(int int32)
        {
            return new StackItem { _int32 = int32, _kind = StackValueKind.Int32 };
        }

        public int AsInt32()
        {
            Debug.Assert(_kind == StackValueKind.Int32);
            return _int32;
        }

        public int AsInt32Unchecked()
        {
            return _int32;
        }

        public static StackItem FromInt64(long int64)
        {
            return new StackItem { _int64 = int64, _kind = StackValueKind.Int64 };
        }

        public long AsInt64()
        {
            Debug.Assert(_kind == StackValueKind.Int64);
            return _int64;
        }

        public long AsInt64Unchecked()
        {
            return _int64;
        }

        public static StackItem FromNativeInt(IntPtr nativeInt)
        {
            return new StackItem { _nativeInt = nativeInt, _kind = StackValueKind.NativeInt };
        }

        public IntPtr AsNativeInt()
        {
            Debug.Assert(_kind == StackValueKind.NativeInt);
            return _nativeInt;
        }

        public IntPtr AsNativeIntUnchecked()
        {
            return _nativeInt;
        }

        public static StackItem FromDouble(double d)
        {
            return new StackItem { _double = d, _kind = StackValueKind.Float };
        }

        public double AsDouble()
        {
            Debug.Assert(_kind == StackValueKind.Float);
            return _double;
        }

        public double AsDoubleUnchecked()
        {
            return _double;
        }

        public static StackItem FromValueType(ValueType valueType)
        {
            return new StackItem { _valueType = valueType, _kind = StackValueKind.ValueType };
        }

        public ValueType AsValueType()
        {
            Debug.Assert(_kind == StackValueKind.ValueType);
            return _valueType;
        }

        public ValueType AsValueTypeUnchecked()
        {
            return _valueType;
        }

        public static StackItem FromObjectRef(object obj)
        {
            return new StackItem { _objref = obj, _kind = StackValueKind.ObjRef };
        }

        public object AsObjectRef()
        {
            Debug.Assert(_kind == StackValueKind.ObjRef);
            return _objref;
        }

        public object AsObjectRefUnchecked()
        {
            return _objref;
        }
    }
}
