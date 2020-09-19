// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using Internal.TypeSystem;
using LLVMSharp.Interop;

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
            Debug.Assert(pos <= _top, "Invalid insertion point");

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
            if (_top <= 0)
            {
                ThrowHelper.ThrowInvalidProgramException();
            }

            return _stack[_top - 1];
        }

        /// <summary>
        /// Remove top element from stack and return it.
        /// </summary>
        /// <returns>Element formerly at the top of the stack</returns>
        public T Pop()
        {
            if (_top <= 0)
            {
                ThrowHelper.ThrowInvalidProgramException();
            }

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

        public LLVMValueRef ValueAsType(LLVMTypeRef type, LLVMBuilderRef builder)
        {
            return ValueAsTypeInternal(type, builder, Type != null && (Type.IsWellKnownType(WellKnownType.SByte) || Type.IsWellKnownType(WellKnownType.Int16)));
        }

        public LLVMValueRef ValueAsType(TypeDesc type, LLVMBuilderRef builder)
        {
            return ValueAsType(ILImporter.GetLLVMTypeForTypeDesc(type), builder);
        }

        public LLVMValueRef ValueForStackKind(StackValueKind kind, LLVMBuilderRef builder, bool signExtend)
        {
            if (kind == StackValueKind.Int32)
                return ValueAsInt32(builder, signExtend);
            else if (kind == StackValueKind.Int64)
                return ValueAsInt64(builder, signExtend);
            else if (kind == StackValueKind.Float)
                return ValueAsType(Type.IsWellKnownType(WellKnownType.Single) ? ILImporter.Context.FloatType : ILImporter.Context.DoubleType, builder);
            else if (kind == StackValueKind.NativeInt || kind == StackValueKind.ByRef || kind == StackValueKind.ObjRef)
                return ValueAsInt32(builder, false);
            else
                throw new NotImplementedException();
        }

        public LLVMValueRef ValueAsInt32(LLVMBuilderRef builder, bool signExtend)
        {
            return ValueAsTypeInternal(ILImporter.Context.Int32Type, builder, signExtend);
        }

        public LLVMValueRef ValueAsInt64(LLVMBuilderRef builder, bool signExtend)
        {
            return ValueAsTypeInternal(ILImporter.Context.Int64Type, builder, signExtend);
        }

        protected abstract LLVMValueRef ValueAsTypeInternal(LLVMTypeRef type, LLVMBuilderRef builder, bool signExtend);

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
        //public abstract void Append(CppGenerationBuffer builder);

        /// <summary>
        /// Create a new copy of current entry.
        /// </summary>
        /// <returns>A new instance of the same type as the current entry.</returns>
        public abstract StackEntry Duplicate(LLVMBuilderRef builder);
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
    }

    internal class Int32ConstantEntry : ConstantEntry<int>
    {
        public Int32ConstantEntry(int value, TypeDesc type = null) : base(StackValueKind.Int32, value, type)
        {
        }

        protected override LLVMValueRef ValueAsTypeInternal(LLVMTypeRef type, LLVMBuilderRef builder, bool signExtend)
        {
            if (type.Kind == LLVMTypeKind.LLVMPointerTypeKind && Value == 0)
            {
                return LLVMValueRef.CreateConstPointerNull(type); 
            }
            else if (type.Kind == LLVMTypeKind.LLVMPointerTypeKind && Value != 0)
            {
                return LLVMValueRef.CreateConstIntToPtr(LLVMValueRef.CreateConstInt(ILImporter.Context.Int32Type, (ulong)Value), type);
            }
            else if (type.Kind != LLVMTypeKind.LLVMIntegerTypeKind)
            {
                throw new NotImplementedException();
            }
            else
            {
                return LLVMValueRef.CreateConstInt(type, (ulong)Value);
            }
        }

        public override StackEntry Duplicate(LLVMBuilderRef builder)
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

        public override StackEntry Duplicate(LLVMBuilderRef builder)
        {
            return new Int64ConstantEntry(Value, Type);
        }

        protected override LLVMValueRef ValueAsTypeInternal(LLVMTypeRef type, LLVMBuilderRef builder, bool signExtend)
        {
            if (type.Kind == LLVMTypeKind.LLVMPointerTypeKind && Value == 0)
            {
                return LLVMValueRef.CreateConstPointerNull(type);
            }
            else if (type.Kind == LLVMTypeKind.LLVMPointerTypeKind && Value != 0)
            {
                return LLVMValueRef.CreateConstIntToPtr(LLVMValueRef.CreateConstInt(ILImporter.Context.Int64Type, (ulong)Value), type);
            }
            else if (type.Kind != LLVMTypeKind.LLVMIntegerTypeKind)
            {
                throw new NotImplementedException();
            }
            else
            {
                return LLVMValueRef.CreateConstInt(type, (ulong)Value);
            }
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

        protected override LLVMValueRef ValueAsTypeInternal(LLVMTypeRef type, LLVMBuilderRef builder, bool signExtend)
        {
            return LLVMValueRef.CreateConstReal(type, Value);
        }

        public override StackEntry Duplicate(LLVMBuilderRef builder)
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
        public LLVMValueRef RawLLVMValue { get; set; }
        /// <summary>
        /// Initializes new instance of ExpressionEntry
        /// </summary>
        /// <param name="kind">Kind of entry</param>
        /// <param name="name">String representation of entry</param>
        /// <param name="type">Type if any of entry</param>
        public ExpressionEntry(StackValueKind kind, string name, LLVMValueRef llvmValue, TypeDesc type = null) : base(kind, type)
        {
            Name = name;
            RawLLVMValue = llvmValue;
        }

        public override StackEntry Duplicate(LLVMBuilderRef builder)
        {
            return new ExpressionEntry(Kind, Name, RawLLVMValue, Type);
        }

        protected override LLVMValueRef ValueAsTypeInternal(LLVMTypeRef type, LLVMBuilderRef builder, bool signExtend)
        {
            if (type.IsPackedStruct && type.Handle != RawLLVMValue.TypeOf.Handle)
            {
                var destStruct = type.Undef;
                for (uint elemNo = 0; elemNo < RawLLVMValue.TypeOf.StructElementTypesCount; elemNo++)
                {
                    var elemValRef = builder.BuildExtractValue(RawLLVMValue, 0, "ex" + elemNo);
                    destStruct = builder.BuildInsertValue(destStruct, elemValRef, elemNo, "st" + elemNo);
                }
                return destStruct;
            }
            return ILImporter.CastIfNecessary(builder, RawLLVMValue, type, Name, !signExtend);
        }
    }

    internal class LoadExpressionEntry : ExpressionEntry
    {
        /// <summary>
        /// Initializes new instance of ExpressionEntry
        /// </summary>
        /// <param name="kind">Kind of entry</param>
        /// <param name="name">String representation of entry</param>
        /// <param name="type">Type if any of entry</param>
        public LoadExpressionEntry(StackValueKind kind, string name, LLVMValueRef llvmValue, TypeDesc type = null) : base(kind, name, llvmValue, type)
        {
        }

        public override StackEntry Duplicate(LLVMBuilderRef builder)
        {
            return new ExpressionEntry(Kind, "duplicate_" + Name, ILImporter.LoadValue(builder, RawLLVMValue, Type, ILImporter.GetLLVMTypeForTypeDesc(Type), false, "load_duplicate_" + Name), Type);
        }

        protected override LLVMValueRef ValueAsTypeInternal(LLVMTypeRef type, LLVMBuilderRef builder, bool signExtend)
        {
            return ILImporter.LoadValue(builder, RawLLVMValue, Type, type, signExtend, $"Load{Name}");
        }
    }

    internal class AddressExpressionEntry : ExpressionEntry
    {
        /// <summary>
        /// Initializes new instance of ExpressionEntry
        /// </summary>
        /// <param name="kind">Kind of entry</param>
        /// <param name="name">String representation of entry</param>
        /// <param name="type">Type if any of entry</param>
        public AddressExpressionEntry(StackValueKind kind, string name, LLVMValueRef llvmValue, TypeDesc type = null) : base(kind, name, llvmValue, type)
        {
        }

        public override StackEntry Duplicate(LLVMBuilderRef builder)
        {
            return new AddressExpressionEntry(Kind, Name, RawLLVMValue, Type);
        }

        protected override LLVMValueRef ValueAsTypeInternal(LLVMTypeRef type, LLVMBuilderRef builder, bool signExtend)
        {
            return ILImporter.CastIfNecessary(builder, RawLLVMValue, type, Name);
        }
    }

    /// <summary>
    /// Represents the result of a ldftn or ldvirtftn
    /// </summary>
    internal class FunctionPointerEntry : ExpressionEntry
    {
        /// <summary>
        /// True if the function pointer was loaded as a virtual function pointer
        /// </summary>
        public bool IsVirtual { get; }

        public MethodDesc Method { get; }

        public FunctionPointerEntry(string name, MethodDesc method, LLVMValueRef llvmValue, TypeDesc type, bool isVirtual) : base(StackValueKind.NativeInt, name, llvmValue, type)
        {
            Method = method;
            IsVirtual = isVirtual;
        }

        public override StackEntry Duplicate(LLVMBuilderRef builder)
        {
            return new FunctionPointerEntry(Name, Method, RawLLVMValue, Type, IsVirtual);
        }
    }

    /// <summary>
    /// Entry representing some token (either of TypeDesc, MethodDesc or FieldDesc) along with its string representation
    /// </summary>
    internal class LdTokenEntry<T> : ExpressionEntry
    {
        public T LdToken { get; }

        public LdTokenEntry(StackValueKind kind, string name, T token, LLVMValueRef llvmValue, TypeDesc type = null) : base(kind, name, llvmValue, type)
        {
            LdToken = token;
        }

        public override StackEntry Duplicate(LLVMBuilderRef builder)
        {
            return new LdTokenEntry<T>(Kind, Name, LdToken, RawLLVMValue, Type);
        }

        protected override LLVMValueRef ValueAsTypeInternal(LLVMTypeRef type, LLVMBuilderRef builder, bool signExtend)
        {
            if (RawLLVMValue.Handle == IntPtr.Zero)
                throw new NullReferenceException();

            return ILImporter.CastIfNecessary(builder, RawLLVMValue, type, Name);
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

        public override StackEntry Duplicate(LLVMBuilderRef builder)
        {
            return this;
        }

        protected override LLVMValueRef ValueAsTypeInternal(LLVMTypeRef type, LLVMBuilderRef builder, bool signExtend)
        {
            throw new InvalidOperationException();
        }
    }

    /// <summary>
    /// Entry representing a writable sharable stack entry that can survive from one basic block to another
    /// </summary>
    internal class SpilledExpressionEntry : ExpressionEntry
    {
        public int LocalIndex;
        private ILImporter _importer;
        public SpilledExpressionEntry(StackValueKind kind, string name, TypeDesc type, int localIndex, ILImporter importer) : base(kind, name, new LLVMValueRef(IntPtr.Zero), type)
        {
            LocalIndex = localIndex;
            _importer = importer;
        }

        protected override LLVMValueRef ValueAsTypeInternal(LLVMTypeRef type, LLVMBuilderRef builder, bool signExtend)
        {
            LLVMTypeRef origLLVMType = ILImporter.GetLLVMTypeForTypeDesc(Type);
            LLVMValueRef value = _importer.LoadTemp(LocalIndex, origLLVMType);

            return ILImporter.CastIfNecessary(builder, value, type, unsigned: !signExtend);
        }

        public override StackEntry Duplicate(LLVMBuilderRef builder)
        {
            return new SpilledExpressionEntry(Kind, Name, Type, LocalIndex, _importer);
        }
    }

    internal static class StackEntryExtensions
    {
        public static string Name(this StackEntry entry)
        {
            return (entry as ExpressionEntry)?.Name;
        }
    }
}
