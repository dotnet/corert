// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Text;
using ILCompiler.Compiler.CppCodeGen;
using Internal.TypeSystem;

namespace Internal.IL
{
    /// <summary>
    /// Abstraction of a variable size last-in-first-out (LIFO) collection of instances of the same specified type
    /// implemented via an array.
    /// </summary>
    /// <typeparam name="T">Type of elements in the stack.</typeparam>
    internal class EvaluationStack<T>
    {
        /// <summary>
        /// Initializes a new instance of the stack that is empty and has the specified initial capacity <paramref name="n"/>.
        /// </summary>
        /// <param name="n">Initial number of elements that the stack can contain.</param>
        public EvaluationStack(int n)
        {
            Debug.Assert(n >= 0, "Count should be non-negative");

            _stack = n > 0 ? new T[n] : s_emptyStack;
            _top = 0;

            Debug.Assert(n == _stack.Length, "Stack length does not match requested capacity");
            Debug.Assert(_top == 0, "Top of stack is at bottom");
        }

        /// <summary>
        /// Value for all stacks of length 0.
        /// </summary>
        private static readonly T[] s_emptyStack = new T[0];

        /// <summary>
        /// Storage for current stack.
        /// </summary>
        private T[] _stack;

        /// <summary>
        /// Position in <see cref="_stack"/> where next element will be pushed.
        /// </summary>
        private int _top;

        /// <summary>
        /// Position in stack where next element will be pushed.
        /// </summary>
        public int Top
        {
            get { return _top; }
        }

        /// <summary>
        /// Number of elements contained in the stack.
        /// </summary>
        public int Length
        {
            get { return _top; }
        }

        /// <summary>
        /// Push <paramref name="value"/> at the top of the stack.
        /// </summary>
        /// <param name="value">Element to push onto the stack.</param>
        public void Push(T value)
        {
            if (_top >= _stack.Length)
            {
                Array.Resize(ref _stack, 2 * _top + 3);
            }
            _stack[_top++] = value;
        }

        /// <summary>
        /// Insert <paramref name="v"/> at position <paramref name="pos"/> in current stack, shifting all
        /// elements after or at <paramref name="pos"/> by one.
        /// </summary>
        /// <param name="v">Element to insert</param>
        /// <param name="pos">Position where to insert <paramref name="v"/></param>
        public void InsertAt(T v, int pos)
        {
            Debug.Assert(pos < _top, "Invalid insertion point");

            if (_top >= _stack.Length)
            {
                Array.Resize(ref _stack, 2 * _top + 3);
            }
            for (int i = _top - 1; i >= pos; i--)
            {
                _stack[i + 1] = _stack[i];
            }
            _top++;
            _stack[pos] = v;
        }

        /// <summary>
        /// Access and set 
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public T this[int index]
        {
            get
            {
                Debug.Assert(index >= 0 && index < _top, "Index not in range");
                return _stack[index];
            }
            set
            {
                Debug.Assert(index >= 0 && index < _top, "Index not in range");
                _stack[index] = value;
            }
        }

        /// <summary>
        /// Return element of the top of the stack.
        /// </summary>
        /// <returns>Element at the top of the stack</returns>
        public T Peek()
        {
            Debug.Assert(_top > 0, "Stack is not empty");
            return _stack[_top - 1];
        }

        /// <summary>
        /// Remove top element from stack and return it.
        /// </summary>
        /// <returns>Element formerly at the top of the stack</returns>
        public T Pop()
        {
            Debug.Assert(_top > 0, "Stack is not empty");
            return _stack[--_top];
        }

        /// <summary>
        /// Remove <paramref name="n"/> elements from the stack.
        /// </summary>
        /// <param name="n">Number of elements to remove from the stack</param>
        public void PopN(int n)
        {
            Debug.Assert(n <= _top, "Too many elements to remove");
            _top -= n;
        }

        /// <summary>
        /// Remove all elements from the stack.
        /// </summary>
        public void Clear()
        {
            _top = 0;
        }
    }

    /// <summary>
    /// Abstract representation of a stack entry
    /// </summary>
    internal abstract class StackEntry
    {
        /// <summary>
        /// Evaluation stack kind of the entry. 
        /// </summary>
        public StackValueKind Kind { get; }

        /// <summary>
        /// Managed type if any of the entry.
        /// </summary>
        public TypeDesc Type { get; }

        /// <summary>
        /// Initializes a new instance of StackEntry.
        /// </summary>
        /// <param name="kind">Kind of entry.</param>
        /// <param name="type">Type if any of entry.</param>
        protected StackEntry(StackValueKind kind, TypeDesc type = null)
        {
            Kind = kind;
            Type = type;
        }

        /// <summary>
        /// Add representation of current entry in <paramref name="builder"/>.
        /// </summary>
        /// <param name="builder">Generation buffer used for appending new content.</param>
        public abstract void Append(CppGenerationBuffer builder);

        /// <summary>
        /// Create a new copy of current entry.
        /// </summary>
        /// <returns>A new instance of the same type as the current entry.</returns>
        public abstract StackEntry Duplicate();

        /// <summary>
        /// Overridden and sealed to force descendants to override <see cref="BuildRepresentation"/>.
        /// </summary>
        /// <returns>String representation of current entry</returns>
        public override sealed string ToString()
        {
            StringBuilder s = new StringBuilder();
            BuildRepresentation(s);
            return s.ToString();
        }

        /// <summary>
        /// Build a representation of current entry in <paramref name="s"/>.
        /// </summary>
        /// <param name="s">StringBuilder where representation will be saved.</param>
        protected virtual void BuildRepresentation(StringBuilder s)
        {
            Debug.Assert(s != null, "StringBuilder is null.");
            if (Type != null)
            {
                s.Append(Type);
                if (Kind != StackValueKind.Unknown)
                {
                    s.Append('(');
                    s.Append(Kind);
                    s.Append(')');
                }
            }
            else if (Kind != StackValueKind.Unknown)
            {
                if (Kind != StackValueKind.Unknown)
                {
                    s.Append('(');
                    s.Append(Kind);
                    s.Append(')');
                }
            }
        }
    }

    /// <summary>
    /// Abstract entry for all constant values.
    /// </summary>
    internal abstract class ConstantEntry : StackEntry
    {
        protected ConstantEntry(StackValueKind kind, TypeDesc type = null) : base(kind, type)
        {
        }

        /// <summary>
        /// Does current entry require a cast to be assigned to <paramref name="destType"/>?
        /// </summary>
        /// <param name="destType">Type of destination</param>
        /// <returns>True if a cast is required</returns>
        public virtual bool IsCastNecessary(TypeDesc destType)
        {
            return false;
        }
    }

    internal abstract class ConstantEntry<T> : ConstantEntry where T : IConvertible
    {
        public T Value { get; }

        protected ConstantEntry(StackValueKind kind, T value, TypeDesc type = null) : base(kind, type)
        {
            Value = value;
        }

        public override void Append(CppGenerationBuffer _builder)
        {
            _builder.Append(Value.ToStringInvariant());
        }

        protected override void BuildRepresentation(StringBuilder s)
        {
            base.BuildRepresentation(s);
            if (s.Length > 0)
            {
                s.Append(' ');
            }
            s.Append(Value);
        }
    }

    internal class Int32ConstantEntry : ConstantEntry<int>
    {
        public Int32ConstantEntry(int value, TypeDesc type = null) : base(StackValueKind.Int32, value, type)
        {
        }

        public override void Append(CppGenerationBuffer _builder)
        {
            if (Value == Int32.MinValue)
            {
                // Special case as if we were to print int.MinValue in decimal it would be
                // -2147483648 but C does not understand it this way, it understands
                // -(2147483648) and 2147483648 does not fit onto a 32-bit integer.
                // We use an hex value instead.
                _builder.Append("(int32_t)(0x80000000)");
            }
            else
            {
                _builder.Append(Value.ToStringInvariant());
            }
        }

        public override StackEntry Duplicate()
        {
            return new Int32ConstantEntry(Value, Type);
        }

        public override bool IsCastNecessary(TypeDesc destType)
        {
            switch (destType.UnderlyingType.Category)
            {
                case TypeFlags.SByte:
                    return Value >= sbyte.MaxValue || Value <= sbyte.MinValue;
                case TypeFlags.Byte:
                case TypeFlags.Boolean:
                    return Value >= byte.MaxValue || Value < 0;
                case TypeFlags.Int16:
                    return Value >= short.MaxValue || Value <= short.MinValue;
                case TypeFlags.UInt16:
                case TypeFlags.Char:
                    return Value >= ushort.MaxValue || Value < 0;
                case TypeFlags.Int32:
                    return false;
                case TypeFlags.UInt32:
                    return Value < 0;
                default:
                    return true;
            }
        }
    }

    internal class Int64ConstantEntry : ConstantEntry<long>
    {
        public Int64ConstantEntry(long value, TypeDesc type = null) : base(StackValueKind.Int64, value, type)
        {
        }

        public override void Append(CppGenerationBuffer _builder)
        {
            if (Value == long.MinValue)
            {
                // See comment on Int32ConstantEntry.Append
                _builder.Append("(int64_t)(INT64VAL(0x8000000000000000))");
            }
            else
            {
                _builder.Append("INT64VAL(");
                _builder.Append(Value.ToStringInvariant());
                _builder.Append(')');
            }
        }

        public override StackEntry Duplicate()
        {
            return new Int64ConstantEntry(Value, Type);
        }

        public override bool IsCastNecessary(TypeDesc destType)
        {
            switch (destType.UnderlyingType.Category)
            {
                case TypeFlags.SByte:
                    return Value >= sbyte.MaxValue || Value <= sbyte.MinValue;
                case TypeFlags.Byte:
                case TypeFlags.Boolean:
                    return Value >= byte.MaxValue || Value < 0;
                case TypeFlags.Int16:
                    return Value >= short.MaxValue || Value <= short.MinValue;
                case TypeFlags.UInt16:
                case TypeFlags.Char:
                    return Value >= ushort.MaxValue || Value < 0;
                case TypeFlags.Int32:
                    return Value >= int.MaxValue || Value <= int.MinValue;
                case TypeFlags.UInt32:
                    return Value >= uint.MaxValue || Value < 0;
                case TypeFlags.Int64:
                    return false;
                case TypeFlags.UInt64:
                    return Value < 0;
                default:
                    return true;
            }
        }
    }

    internal class FloatConstantEntry : ConstantEntry<double>
    {
        public FloatConstantEntry(double value, TypeDesc type = null) : base(StackValueKind.Float, value, type)
        {
        }

        public override void Append(CppGenerationBuffer _builder)
        {
            long val = BitConverter.DoubleToInt64Bits(Value);
            _builder.Append("__uint64_to_double(0x");
            _builder.Append(val.ToStringInvariant("x8"));
            _builder.Append(')');
            // Let's print the actual value as comment.
            _builder.Append("/* ");
            if (Double.IsNaN(Value))
            {
                _builder.Append("NaN");
            }
            else if (Double.IsPositiveInfinity(Value))
            {
                _builder.Append("+Inf");
            }
            else if (Double.IsNegativeInfinity(Value))
            {
                _builder.Append("-Inf");
            }
            else
            {
                _builder.Append(Value.ToStringInvariant());
            }
            _builder.Append(" */");
        }

        public override StackEntry Duplicate()
        {
            return new FloatConstantEntry(Value, Type);
        }
    }

    /// <summary>
    /// Entry representing some expression
    /// </summary>
    internal class ExpressionEntry : StackEntry
    {
        /// <summary>
        /// String representation of current expression
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Initializes new instance of ExpressionEntry
        /// </summary>
        /// <param name="kind">Kind of entry</param>
        /// <param name="name">String representation of entry</param>
        /// <param name="type">Type if any of entry</param>
        public ExpressionEntry(StackValueKind kind, string name, TypeDesc type = null) : base(kind, type)
        {
            Name = name;
        }
        public override void Append(CppGenerationBuffer _builder)
        {
            _builder.Append(Name);
        }

        public override StackEntry Duplicate()
        {
            return new ExpressionEntry(Kind, Name, Type);
        }

        protected override void BuildRepresentation(StringBuilder s)
        {
            base.BuildRepresentation(s);
            if (s.Length > 0)
            {
                s.Append(' ');
            }
            s.Append(Name);
        }
    }

    /// <summary>
    /// Entry representing some token (either of TypeDesc, MethodDesc or FieldDesc) along with its string representation
    /// </summary>
    internal class LdTokenEntry<T> : ExpressionEntry
    {
        public T LdToken { get; }

        public LdTokenEntry(StackValueKind kind, string name, T token, TypeDesc type = null) : base(kind, name, type)
        {
            LdToken = token;
        }

        public override StackEntry Duplicate()
        {
            return new LdTokenEntry<T>(Kind, Name, LdToken, Type);
        }

        protected override void BuildRepresentation(StringBuilder s)
        {
            base.BuildRepresentation(s);
            s.Append(' ');
            s.Append(LdToken);
        }
    }

    /// <summary>
    /// Entry representing some ftn token along with its string representation
    /// </summary>
    internal class LdFtnTokenEntry : LdTokenEntry<MethodDesc>
    {
        public bool IsVirtual { get; }

        public LdFtnTokenEntry(StackValueKind kind, string name, MethodDesc token, bool isVirtual, TypeDesc type = null) : base(kind, name, token, type)
        {
            IsVirtual = isVirtual;
        }

        public override StackEntry Duplicate()
        {
            return new LdFtnTokenEntry(Kind, Name, LdToken, IsVirtual, Type);
        }
    }

    internal class InvalidEntry : StackEntry
    {
        /// <summary>
        /// Entry to use to get an instance of InvalidEntry.
        /// </summary>
        public static InvalidEntry Entry = new InvalidEntry();

        protected InvalidEntry() : base(StackValueKind.Unknown, null)
        {
        }

        public override void Append(CppGenerationBuffer _builder)
        {
            _builder.Append("// FIXME: An invalid value was pushed onto the evaluation stack.");
            Debug.Fail("Invalid stack values shouldn't be appended.");
        }

        public override StackEntry Duplicate()
        {
            return this;
        }

        protected override void BuildRepresentation(StringBuilder s)
        {
            s.Append("Invalid Entry");
        }
    }
}
