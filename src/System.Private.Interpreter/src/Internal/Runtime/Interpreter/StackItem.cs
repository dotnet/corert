// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
        private WellKnownType _type;

        [FieldOffset(16)]
        private int _int32;

        [FieldOffset(16)]
        private long _int64;

        [FieldOffset(16)]
        private IntPtr _nativeInt;

        [FieldOffset(16)]
        private float _float;

        [FieldOffset(16)]
        private double _double;

        [FieldOffset(16)]
        private void* _ptr;

        [FieldOffset(24)]
        private ValueType _valueType;

        [FieldOffset(24)]
        private object _objref;

        public StackValueKind Kind => _kind;
        public WellKnownType Type => _type;

        public static implicit operator StackItem(int int32)
        {
            return new StackItem { _int32 = int32, _type = WellKnownType.Int32, _kind = StackValueKind.Int32 };
        }

        public static explicit operator int(StackItem stackItem)
        {
            if (stackItem._type != WellKnownType.Int32)
                throw new InvalidCastException();

            return stackItem._int32;
        }

        public static implicit operator StackItem(long int64)
        {
            return new StackItem { _int64 = int64, _type = WellKnownType.Int64, _kind = StackValueKind.Int64 };
        }

        public static explicit operator long(StackItem stackItem)
        {
            if (stackItem._type != WellKnownType.Int64)
                throw new InvalidCastException();

            return stackItem._int64;
        }

        public static implicit operator StackItem(IntPtr ptr)
        {
            return new StackItem { _nativeInt = ptr, _type = WellKnownType.IntPtr, _kind = StackValueKind.NativeInt };
        }

        public static explicit operator IntPtr(StackItem stackItem)
        {
            if (stackItem._type != WellKnownType.IntPtr)
                throw new InvalidCastException();

            return stackItem._nativeInt;
        }

        public static implicit operator StackItem(float single)
        {
            return new StackItem { _float = single, _type = WellKnownType.Single, _kind = StackValueKind.Float };
        }

        public static explicit operator float(StackItem stackItem)
        {
            if (stackItem._type != WellKnownType.Single)
                throw new InvalidCastException();

            return stackItem._float;
        }

        public static implicit operator StackItem(double d)
        {
            return new StackItem { _double = d, _type = WellKnownType.Double, _kind = StackValueKind.Float };
        }

        public static explicit operator double(StackItem stackItem)
        {
            if (stackItem._type != WellKnownType.Double)
                throw new InvalidCastException();

            return stackItem._double;
        }

        public static StackItem FromValueType(ValueType valueType)
        {
            return new StackItem { _valueType = valueType, _type = WellKnownType.ValueType, _kind = StackValueKind.ValueType };
        }

        public ValueType ToValueType()
        {
            if (_type != WellKnownType.ValueType)
                throw new InvalidCastException();

            return _valueType;
        }

        public static StackItem FromObjectRef(object obj)
        {
            return new StackItem { _objref = obj, _type = WellKnownType.Object, _kind = StackValueKind.ObjRef };
        }

        public object ToObjectRef()
        {
            if (_type != WellKnownType.Object)
                throw new InvalidCastException();

            return _objref;
        }
    }
}
