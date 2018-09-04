// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.IL;
using Internal.TypeSystem;

namespace Internal.Runtime.Interpreter
{
    internal abstract class StackItem
    {
        public StackValueKind Kind { get; protected set; }
        public WellKnownType Type { get; protected set; }
    }

    internal class StackItem<T> : StackItem
    {
        public T Value { get; }

        public StackItem(T value, StackValueKind kind, WellKnownType type)
        {
            Value = value;
            Kind = kind;
            Type = type;
        }
    }

    internal class Int32StackItem : StackItem<int>
    {
        public Int32StackItem(int value) : base(value, StackValueKind.Int32, WellKnownType.Int32)
        {
        }
    }

    internal class Int64StackItem : StackItem<long>
    {
        public Int64StackItem(long value) : base(value, StackValueKind.Int64, WellKnownType.Int64)
        {
        }
    }

    internal class FloatStackItem : StackItem<float>
    {
        public FloatStackItem(float value) : base(value, StackValueKind.Float, WellKnownType.Single)
        {
        }
    }

    internal class DoubleStackItem : StackItem<double>
    {
        public DoubleStackItem(double value) : base(value, StackValueKind.Float, WellKnownType.Double)
        {
        }
    }

    internal class ValueTypeStackItem : StackItem<ValueType>
    {
        public ValueTypeStackItem(ValueType value) : base(value, StackValueKind.ValueType, WellKnownType.ValueType)
        {
        }
    }

    internal class ObjectRefStackItem : StackItem<Object>
    {
        public ObjectRefStackItem(Object value) : base(value, StackValueKind.ObjRef, WellKnownType.Object)
        {
        }
    }
}
