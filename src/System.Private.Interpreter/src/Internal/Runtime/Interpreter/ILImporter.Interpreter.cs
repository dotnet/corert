// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Internal.Runtime.Interpreter;
using Internal.TypeSystem;

namespace Internal.IL
{
    unsafe partial class ILImporter
    {
        private class BasicBlock
        {
            // Common fields
            public enum ImportState : byte
            {
                Unmarked,
                IsPending
            }

            public BasicBlock Next;

            public int StartOffset;
            public ImportState State = ImportState.Unmarked;

            public bool TryStart;
            public bool FilterStart;
            public bool HandlerStart;
        }

        private class ExceptionRegion
        {
            public ILExceptionRegion ILRegion;
        }

        private readonly byte[] _ilBytes;
        private readonly MethodDesc _method;
        private readonly MethodIL _methodIL;
        private readonly ILInterpreter _interpreter;
        private ExceptionRegion[] _exceptionRegions;

        public ILImporter(ILInterpreter interpreter, MethodDesc method, MethodIL methodIL)
        {
            _ilBytes = methodIL.GetILBytes();
            _method = method;
            _methodIL = methodIL;
            _interpreter = interpreter;

            var ilExceptionRegions = methodIL.GetExceptionRegions();
            _exceptionRegions = new ExceptionRegion[methodIL.GetExceptionRegions().Length];
            for (int i = 0; i < ilExceptionRegions.Length; i++)
            {
                _exceptionRegions[i] = new ExceptionRegion() { ILRegion = ilExceptionRegions[i] };
            }
        }

        public void Interpret()
        {
            FindBasicBlocks();
            ImportBasicBlocks();
        }

        private void MarkInstructionBoundary() { }

        private void StartImportingInstruction() { }

        private void EndImportingInstruction() { }

        private void StartImportingBasicBlock(BasicBlock basicBlock) { }

        private void EndImportingBasicBlock(BasicBlock basicBlock) { }

        private void ReportInvalidBranchTarget(int targetOffset)
        {
            ThrowHelper.ThrowInvalidProgramException();
        }

        private void ReportFallthroughAtEndOfMethod()
        {
            ThrowHelper.ThrowInvalidProgramException();
        }

        private void ReportMethodEndInsideInstruction()
        {
            ThrowHelper.ThrowInvalidProgramException();
        }

        private void ReportInvalidInstruction(ILOpcode opcode)
        {
            ThrowHelper.ThrowInvalidProgramException();
        }

        private TypeDesc ResolveTypeToken(int token)
        {
            return (TypeDesc)_methodIL.GetObject(token);
        }

        private TypeDesc GetWellKnownType(WellKnownType wellKnownType)
        {
            return _interpreter.TypeSystemContext.GetWellKnownType(wellKnownType);
        }

        public StackItem PopWithValidation()
        {
            bool hasStackItem = _interpreter.EvaluationStack.TryPop(out StackItem stackItem);
            if (!hasStackItem)
            {
                ThrowHelper.ThrowInvalidProgramException();
            }

            return stackItem;
        }

        public StackItem PeekWithValidation()
        {
            bool hasStackItem = _interpreter.EvaluationStack.TryPeek(out StackItem stackItem);
            if (!hasStackItem)
            {
                ThrowHelper.ThrowInvalidProgramException();
            }

            return stackItem;
        }

        private void ImportNop()
        {
            // Do nothing!
        }

        private void ImportBreak()
        {
            throw new NotImplementedException();
        }

        private void ImportLoadVar(int index, bool argument)
        {
            if (argument)
            {
                // TODO: Support ldarg.* opcodes
                throw new NotImplementedException();
            }

            StackItem stackItem = _interpreter.GetVariable(index);
            _interpreter.EvaluationStack.Push(stackItem);
        }

        private void ImportStoreVar(int index, bool argument)
        {
            if (argument)
            {
                // TODO: Support starg.* opcodes
                throw new NotImplementedException();
            }

            _interpreter.SetVariable(index, PopWithValidation());
        }

        private void ImportAddressOfVar(int index, bool argument)
        {
            throw new NotImplementedException();
        }

        private void ImportDup()
        {
            _interpreter.EvaluationStack.Push(PeekWithValidation());
        }

        private void ImportPop()
        {
            PopWithValidation();
        }

        private void ImportCalli(int token)
        {
            throw new NotImplementedException();
        }

        private void ImportLoadNull()
        {
            _interpreter.EvaluationStack.Push(StackItem.FromObjectRef(null));
        }

        private void ImportReturn()
        {
            var returnType = _method.Signature.ReturnType;
            if (returnType.IsVoid)
                return;

            StackItem stackItem = PopWithValidation();
            TypeFlags category = returnType.Category;

            switch (category)
            {
                case TypeFlags.Boolean:
                    _interpreter.SetReturnValue(stackItem.AsInt32() != 0);
                    break;
                case TypeFlags.Char:
                    _interpreter.SetReturnValue((char)stackItem.AsInt32());
                    break;
                case TypeFlags.SByte:
                    _interpreter.SetReturnValue((sbyte)stackItem.AsInt32());
                    break;
                case TypeFlags.Byte:
                    _interpreter.SetReturnValue((byte)stackItem.AsInt32());
                    break;
                case TypeFlags.Int16:
                    _interpreter.SetReturnValue((short)stackItem.AsInt32());
                    break;
                case TypeFlags.UInt16:
                    _interpreter.SetReturnValue((ushort)stackItem.AsInt32());
                    break;
                case TypeFlags.Int32:
                case TypeFlags.UInt32:
                    _interpreter.SetReturnValue(stackItem.AsInt32());
                    break;
                case TypeFlags.Int64:
                case TypeFlags.UInt64:
                    _interpreter.SetReturnValue(stackItem.AsInt64());
                    break;
                case TypeFlags.IntPtr:
                case TypeFlags.UIntPtr:
                    _interpreter.SetReturnValue(stackItem.AsNativeInt());
                    break;
                case TypeFlags.Single:
                    _interpreter.SetReturnValue((float)stackItem.AsDouble());
                    break;
                case TypeFlags.Double:
                    _interpreter.SetReturnValue(stackItem.AsDouble());
                    break;
                case TypeFlags.ValueType:
                    _interpreter.SetReturnValue(stackItem.AsValueType());
                    break;
                case TypeFlags.Interface:
                case TypeFlags.Class:
                case TypeFlags.Array:
                case TypeFlags.SzArray:
                    _interpreter.SetReturnValue(stackItem.AsObjectRef());
                    break;
                case TypeFlags.Enum:
                case TypeFlags.Nullable:
                case TypeFlags.ByRef:
                case TypeFlags.Pointer:
                case TypeFlags.FunctionPointer:
                case TypeFlags.GenericParameter:
                default:
                    // TODO: Support more complex return types
                    throw new NotImplementedException();
            }
        }

        private void ImportLoadInt(long value, StackValueKind kind)
        {
            if (kind == StackValueKind.Int32)
            {
                _interpreter.EvaluationStack.Push(StackItem.FromInt32((int)value));
            }
            else if (kind == StackValueKind.Int64)
            {
                _interpreter.EvaluationStack.Push(StackItem.FromInt64(value));
            }
        }

        private void ImportLoadFloat(double value)
        {
            _interpreter.EvaluationStack.Push(StackItem.FromDouble(value));
        }

        private void ImportShiftOperation(ILOpcode opcode)
        {
            StackItem op1 = PopWithValidation();
            StackItem op2 = PopWithValidation();

            if (op1.Kind > StackValueKind.NativeInt)
            {
                ThrowHelper.ThrowInvalidProgramException();
            }

            int shiftBy = op1.AsInt32Unchecked();
            switch (op2.Kind)
            {
                case StackValueKind.Int32:
                    {
                        int value = op2.AsInt32();
                        switch (opcode)
                        {
                            case ILOpcode.shl:
                                value = value << shiftBy;
                                break;
                            case ILOpcode.shr:
                                value = value >> shiftBy;
                                break;
                            case ILOpcode.shr_un:
                                value = (int)((uint)value >> shiftBy);
                                break;
                            default:
                                Debug.Assert(false);
                                break;
                        }

                        _interpreter.EvaluationStack.Push(StackItem.FromInt32(value));
                    }
                    break;
                case StackValueKind.Int64:
                    {
                        long value = op2.AsInt64();
                        switch (opcode)
                        {
                            case ILOpcode.shl:
                                value = value << shiftBy;
                                break;
                            case ILOpcode.shr:
                                value = value >> shiftBy;
                                break;
                            case ILOpcode.shr_un:
                                value = (long)((ulong)value >> shiftBy);
                                break;
                            default:
                                Debug.Assert(false);
                                break;
                        }

                        _interpreter.EvaluationStack.Push(StackItem.FromInt64(value));
                    }
                    break;
                case StackValueKind.NativeInt:
                    {
                        IntPtr value = op2.AsNativeInt();
                        switch (opcode)
                        {
                            case ILOpcode.shl:
                                value = (IntPtr)((long)value << shiftBy);
                                break;
                            case ILOpcode.shr:
                                value = (IntPtr)((long)value >> shiftBy);
                                break;
                            case ILOpcode.shr_un:
                                UIntPtr uintPtr = (UIntPtr)value.ToPointer();
                                value = (IntPtr)(long)((ulong)uintPtr >> shiftBy);
                                break;
                            default:
                                Debug.Assert(false);
                                break;
                        }

                        _interpreter.EvaluationStack.Push(StackItem.FromNativeInt(value));
                    }
                    break;
                case StackValueKind.Unknown:
                case StackValueKind.Float:
                case StackValueKind.ByRef:
                case StackValueKind.ObjRef:
                case StackValueKind.ValueType:
                default:
                    ThrowHelper.ThrowInvalidProgramException();
                    break;
            }
        }

        private void ImportCompareOperation(ILOpcode opcode)
        {
            bool result = default(bool);
            StackItem op1 = PopWithValidation();
            StackItem op2 = PopWithValidation();

            StackValueKind kind = (op1.Kind > op2.Kind) ? op1.Kind : op2.Kind;
            switch (kind)
            {
                case StackValueKind.Int32:
                    {
                        int val1 = op1.AsInt32Unchecked();
                        int val2 = op2.AsInt32Unchecked();

                        switch (opcode)
                        {
                            case ILOpcode.ceq:
                                result = val1 == val2;
                                break;
                            case ILOpcode.cgt:
                                result = val2 > val1;
                                break;
                            case ILOpcode.cgt_un:
                                result = (uint)val2 > (uint)val1;
                                break;
                            case ILOpcode.clt:
                                result = val2 < val1;
                                break;
                            case ILOpcode.clt_un:
                                result = (uint)val2 < (uint)val1;
                                break;
                            default:
                                Debug.Assert(false);
                                break;
                        }
                    }
                    break;
                case StackValueKind.Int64:
                    {
                        long val1 = op1.AsInt64Unchecked();
                        long val2 = op2.AsInt64Unchecked();

                        switch (opcode)
                        {
                            case ILOpcode.ceq:
                                result = val1 == val2;
                                break;
                            case ILOpcode.cgt:
                                result = val2 > val1;
                                break;
                            case ILOpcode.cgt_un:
                                result = (ulong)val2 > (ulong)val1;
                                break;
                            case ILOpcode.clt:
                                result = val2 < val1;
                                break;
                            case ILOpcode.clt_un:
                                result = (ulong)val2 < (ulong)val1;
                                break;
                            default:
                                Debug.Assert(false);
                                break;
                        }
                    }
                    break;
                case StackValueKind.NativeInt:
                    {
                        IntPtr val1 = op1.AsNativeIntUnchecked();
                        IntPtr val2 = op2.AsNativeIntUnchecked();
#if BIT64
                        if (opcode == ILOpcode.ceq || opcode == ILOpcode.cgt || opcode == ILOpcode.clt)
                        {
                            if (op1.Kind == StackValueKind.Int32)
                            {
                                val1 = (IntPtr)op1.AsInt32();
                            }
                            else if (op2.Kind == StackValueKind.Int32)
                            {
                                val2 = (IntPtr)op2.AsInt32();
                            }
                        }
#endif
                        switch (opcode)
                        {
                            case ILOpcode.ceq:
                                result = val1 == val2;
                                break;
                            case ILOpcode.cgt:
                                result = (long)val2 > (long)val1;
                                break;
                            case ILOpcode.cgt_un:
                                result = (ulong)((UIntPtr)val2.ToPointer()) > (ulong)((UIntPtr)val1.ToPointer());
                                break;
                            case ILOpcode.clt:
                                result = (long)val2 < (long)val1;
                                break;
                            case ILOpcode.clt_un:
                                result = (ulong)((UIntPtr)val2.ToPointer()) < (ulong)((UIntPtr)val1.ToPointer());
                                break;
                            default:
                                Debug.Assert(false);
                                break;
                        }
                    }
                    break;
                case StackValueKind.Float:
                    {
                        if (op1.Kind < StackValueKind.Float || op2.Kind < StackValueKind.Float)
                        {
                            ThrowHelper.ThrowInvalidProgramException();
                        }

                        double val1 = op1.AsDouble();
                        double val2 = op2.AsDouble();

                        switch (opcode)
                        {
                            case ILOpcode.ceq:
                                result = val1 == val2;
                                break;
                            case ILOpcode.cgt:
                                result = val2 > val1;
                                break;
                            case ILOpcode.cgt_un:
                                result = val2 > val1;
                                break;
                            case ILOpcode.clt:
                                result = val2 < val1;
                                break;
                            case ILOpcode.clt_un:
                                result = val2 < val1;
                                break;
                            default:
                                Debug.Assert(false);
                                break;
                        }
                    }
                    break;
                case StackValueKind.ObjRef:
                    {
                        if (op1.Kind < StackValueKind.ObjRef || op2.Kind < StackValueKind.ObjRef)
                        {
                            ThrowHelper.ThrowInvalidProgramException();
                        }

                        object val1 = op1.AsObjectRef();
                        object val2 = op2.AsObjectRef();

                        if (opcode == ILOpcode.ceq)
                        {
                            result = Object.ReferenceEquals(val1, val2);
                        }
                        else
                        {
                            // TODO: Find GC addresses of objects and compare them
                            throw new NotImplementedException();
                        }
                    }
                    break;
                case StackValueKind.ByRef:
                    // TODO: Add support for ByRef to StackItem
                    throw new NotImplementedException();
                case StackValueKind.Unknown:
                case StackValueKind.ValueType:
                default:
                    ThrowHelper.ThrowInvalidProgramException();
                    break;
            }

            _interpreter.EvaluationStack.Push(StackItem.FromInt32(result ? 1 : 0));
        }

        private void ImportConvert(WellKnownType wellKnownType, bool checkOverflow, bool unsigned)
        {
            StackItem stackItem = PopWithValidation();
            switch (wellKnownType)
            {
                case WellKnownType.SByte:
                    {
                        sbyte result = default(sbyte);
                        if (stackItem.Kind == StackValueKind.Int32)
                        {
                            if (unsigned)
                            {
                                uint value = (uint)stackItem.AsInt32();
                                result = checkOverflow ? checked((sbyte)value) : (sbyte)value;
                            }
                            else
                            {
                                int value = stackItem.AsInt32();
                                result = checkOverflow ? checked((sbyte)value) : (sbyte)value;
                            }
                        }
                        else if (stackItem.Kind == StackValueKind.Int64)
                        {
                            if (unsigned)
                            {
                                ulong value = (ulong)stackItem.AsInt64();
                                result = checkOverflow ? checked((sbyte)value) : (sbyte)value;
                            }
                            else
                            {
                                long value = stackItem.AsInt64();
                                result = checkOverflow ? checked((sbyte)value) : (sbyte)value;
                            }
                        }
                        else if (stackItem.Kind == StackValueKind.Float)
                        {
                            double value = stackItem.AsDouble();
                            result = checkOverflow ? checked((sbyte)value) : (sbyte)value;
                        }
                        else if (stackItem.Kind == StackValueKind.NativeInt)
                        {
                            if (unsigned)
                            {
                                UIntPtr value = (UIntPtr)stackItem.AsNativeInt().ToPointer();
                                result = checkOverflow ? checked((sbyte)value.ToUInt64()) : (sbyte)value;
                            }
                            else
                            {
                                IntPtr value = stackItem.AsNativeInt();
                                result = checkOverflow ? checked((sbyte)value.ToInt64()) : (sbyte)value;
                            }
                        }
                        else
                        {
                            ThrowHelper.ThrowInvalidProgramException();
                        }

                        _interpreter.EvaluationStack.Push(StackItem.FromInt32(result));
                    }
                    break;
                case WellKnownType.Byte:
                    {
                        byte result = default(byte);
                        if (stackItem.Kind == StackValueKind.Int32)
                        {
                            if (unsigned)
                            {
                                uint value = (uint)stackItem.AsInt32();
                                result = checkOverflow ? checked((byte)value) : (byte)value;
                            }
                            else
                            {
                                int value = stackItem.AsInt32();
                                result = checkOverflow ? checked((byte)value) : (byte)value;
                            }
                        }
                        else if (stackItem.Kind == StackValueKind.Int64)
                        {
                            if (unsigned)
                            {
                                ulong value = (ulong)stackItem.AsInt64();
                                result = checkOverflow ? checked((byte)value) : (byte)value;
                            }
                            else
                            {
                                long value = stackItem.AsInt64();
                                result = checkOverflow ? checked((byte)value) : (byte)value;
                            }
                        }
                        else if (stackItem.Kind == StackValueKind.Float)
                        {
                            double value = stackItem.AsDouble();
                            result = checkOverflow ? checked((byte)value) : (byte)value;
                        }
                        else if (stackItem.Kind == StackValueKind.NativeInt)
                        {
                            if (unsigned)
                            {
                                UIntPtr value = (UIntPtr)stackItem.AsNativeInt().ToPointer();
                                result = checkOverflow ? checked((byte)value.ToUInt64()) : (byte)value;
                            }
                            else
                            {
                                IntPtr value = stackItem.AsNativeInt();
                                result = checkOverflow ? checked((byte)value.ToInt64()) : (byte)value;
                            }
                        }
                        else
                        {
                            ThrowHelper.ThrowInvalidProgramException();
                        }

                        _interpreter.EvaluationStack.Push(StackItem.FromInt32(result));
                    }
                    break;
                case WellKnownType.Int16:
                    {
                        short result = default(short);
                        if (stackItem.Kind == StackValueKind.Int32)
                        {
                            if (unsigned)
                            {
                                uint value = (uint)stackItem.AsInt32();
                                result = checkOverflow ? checked((short)value) : (short)value;
                            }
                            else
                            {
                                int value = stackItem.AsInt32();
                                result = checkOverflow ? checked((short)value) : (short)value;
                            }
                        }
                        else if (stackItem.Kind == StackValueKind.Int64)
                        {
                            if (unsigned)
                            {
                                ulong value = (ulong)stackItem.AsInt64();
                                result = checkOverflow ? checked((short)value) : (short)value;
                            }
                            else
                            {
                                long value = stackItem.AsInt64();
                                result = checkOverflow ? checked((short)value) : (short)value;
                            }
                        }
                        else if (stackItem.Kind == StackValueKind.Float)
                        {
                            double value = stackItem.AsDouble();
                            result = checkOverflow ? checked((short)value) : (short)value;
                        }
                        else if (stackItem.Kind == StackValueKind.NativeInt)
                        {
                            if (unsigned)
                            {
                                UIntPtr value = (UIntPtr)stackItem.AsNativeInt().ToPointer();
                                result = checkOverflow ? checked((short)value.ToUInt64()) : (short)value;
                            }
                            else
                            {
                                IntPtr value = stackItem.AsNativeInt();
                                result = checkOverflow ? checked((short)value.ToInt64()) : (short)value;
                            }
                        }
                        else
                        {
                            ThrowHelper.ThrowInvalidProgramException();
                        }

                        _interpreter.EvaluationStack.Push(StackItem.FromInt32(result));
                    }
                    break;
                case WellKnownType.UInt16:
                    {
                        ushort result = default(ushort);
                        if (stackItem.Kind == StackValueKind.Int32)
                        {
                            if (unsigned)
                            {
                                uint value = (uint)stackItem.AsInt32();
                                result = checkOverflow ? checked((ushort)value) : (ushort)value;
                            }
                            else
                            {
                                int value = stackItem.AsInt32();
                                result = checkOverflow ? checked((ushort)value) : (ushort)value;
                            }
                        }
                        else if (stackItem.Kind == StackValueKind.Int64)
                        {
                            if (unsigned)
                            {
                                ulong value = (ulong)stackItem.AsInt64();
                                result = checkOverflow ? checked((ushort)value) : (ushort)value;
                            }
                            else
                            {
                                long value = stackItem.AsInt64();
                                result = checkOverflow ? checked((ushort)value) : (ushort)value;
                            }
                        }
                        else if (stackItem.Kind == StackValueKind.Float)
                        {
                            double value = stackItem.AsDouble();
                            result = checkOverflow ? checked((ushort)value) : (ushort)value;
                        }
                        else if (stackItem.Kind == StackValueKind.NativeInt)
                        {
                            if (unsigned)
                            {
                                UIntPtr value = (UIntPtr)stackItem.AsNativeInt().ToPointer();
                                result = checkOverflow ? checked((ushort)value.ToUInt64()) : (ushort)value;
                            }
                            else
                            {
                                IntPtr value = stackItem.AsNativeInt();
                                result = checkOverflow ? checked((ushort)value.ToInt64()) : (ushort)value;
                            }
                        }
                        else
                        {
                            ThrowHelper.ThrowInvalidProgramException();
                        }

                        _interpreter.EvaluationStack.Push(StackItem.FromInt32(result));
                    }
                    break;
                case WellKnownType.Int32:
                    {
                        int result = default(int);
                        if (stackItem.Kind == StackValueKind.Int32)
                        {
                            if (unsigned)
                            {
                                uint value = (uint)stackItem.AsInt32();
                                result = checkOverflow ? checked((int)value) : (int)value;
                            }
                            else
                            {
                                result = stackItem.AsInt32();
                            }
                        }
                        else if (stackItem.Kind == StackValueKind.Int64)
                        {
                            if (unsigned)
                            {
                                ulong value = (ulong)stackItem.AsInt64();
                                result = checkOverflow ? checked((int)value) : (int)value;
                            }
                            else
                            {
                                long value = stackItem.AsInt64();
                                result = checkOverflow ? checked((int)value) : (int)value;
                            }
                        }
                        else if (stackItem.Kind == StackValueKind.Float)
                        {
                            double value = stackItem.AsDouble();
                            result = checkOverflow ? checked((int)value) : (int)value;
                        }
                        else if (stackItem.Kind == StackValueKind.NativeInt)
                        {
                            if (unsigned)
                            {
                                UIntPtr value = (UIntPtr)stackItem.AsNativeInt().ToPointer();
                                result = checkOverflow ? checked((int)value.ToUInt64()) : (int)value;
                            }
                            else
                            {
                                IntPtr value = stackItem.AsNativeInt();
                                result = checkOverflow ? checked((int)value.ToInt64()) : (int)value;
                            }
                        }
                        else
                        {
                            ThrowHelper.ThrowInvalidProgramException();
                        }

                        _interpreter.EvaluationStack.Push(StackItem.FromInt32(result));
                    }
                    break;
                case WellKnownType.UInt32:
                    {
                        uint result = default(uint);
                        if (stackItem.Kind == StackValueKind.Int32)
                        {
                            if (unsigned)
                            {
                                result = (uint)stackItem.AsInt32();
                            }
                            else
                            {
                                int value = stackItem.AsInt32();
                                result = checkOverflow ? checked((uint)value) : (uint)value;
                            }
                        }
                        else if (stackItem.Kind == StackValueKind.Int64)
                        {
                            if (unsigned)
                            {
                                ulong value = (ulong)stackItem.AsInt64();
                                result = checkOverflow ? checked((uint)value) : (uint)value;
                            }
                            else
                            {
                                long value = stackItem.AsInt64();
                                result = checkOverflow ? checked((uint)value) : (uint)value;
                            }
                        }
                        else if (stackItem.Kind == StackValueKind.Float)
                        {
                            double value = stackItem.AsDouble();
                            result = checkOverflow ? checked((uint)value) : (uint)value;
                        }
                        else if (stackItem.Kind == StackValueKind.NativeInt)
                        {
                            if (unsigned)
                            {
                                UIntPtr value = (UIntPtr)stackItem.AsNativeInt().ToPointer();
                                result = checkOverflow ? checked((uint)value.ToUInt64()) : (uint)value;
                            }
                            else
                            {
                                IntPtr value = stackItem.AsNativeInt();
                                result = checkOverflow ? checked((uint)value.ToInt64()) : (uint)value;
                            }
                        }
                        else
                        {
                            ThrowHelper.ThrowInvalidProgramException();
                        }

                        _interpreter.EvaluationStack.Push(StackItem.FromInt32((int)result));
                    }
                    break;
                case WellKnownType.Int64:
                    {
                        long result = default(long);
                        if (stackItem.Kind == StackValueKind.Int32)
                        {
                            if (unsigned)
                            {
                                result = (uint)stackItem.AsInt32();
                            }
                            else
                            {
                                result = stackItem.AsInt32();
                            }
                        }
                        else if (stackItem.Kind == StackValueKind.Int64)
                        {
                            if (unsigned)
                            {
                                ulong value = (ulong)stackItem.AsInt64();
                                result = checkOverflow ? checked((long)value) : (long)value;
                            }
                            else
                            {
                                result = stackItem.AsInt64();
                            }
                        }
                        else if (stackItem.Kind == StackValueKind.Float)
                        {
                            double value = stackItem.AsDouble();
                            result = checkOverflow ? checked((long)value) : (long)value;
                        }
                        else if (stackItem.Kind == StackValueKind.NativeInt)
                        {
                            if (unsigned)
                            {
                                UIntPtr value = (UIntPtr)stackItem.AsNativeInt().ToPointer();
                                result = (long)value.ToUInt64();
                            }
                            else
                            {
                                result = stackItem.AsNativeInt().ToInt64();
                            }
                        }
                        else
                        {
                            ThrowHelper.ThrowInvalidProgramException();
                        }

                        _interpreter.EvaluationStack.Push(StackItem.FromInt64(result));
                    }
                    break;
                case WellKnownType.UInt64:
                    {
                        ulong result = default(ulong);
                        if (stackItem.Kind == StackValueKind.Int32)
                        {
                            if (unsigned)
                            {
                                uint value = (uint)stackItem.AsInt32();
                                result = checkOverflow ? checked((ulong)value) : (ulong)value;
                            }
                            else
                            {
                                int value = stackItem.AsInt32();
                                result = checkOverflow ? checked((ulong)value) : (ulong)value;
                            }
                        }
                        else if (stackItem.Kind == StackValueKind.Int64)
                        {
                            if (unsigned)
                            {
                                ulong value = (ulong)stackItem.AsInt64();
                                result = checkOverflow ? checked((ulong)value) : (ulong)value;
                            }
                            else
                            {
                                long value = stackItem.AsInt64();
                                result = checkOverflow ? checked((ulong)value) : (ulong)value;
                            }
                        }
                        else if (stackItem.Kind == StackValueKind.Float)
                        {
                            double value = stackItem.AsDouble();
                            result = checkOverflow ? checked((ulong)value) : (ulong)value;
                        }
                        else if (stackItem.Kind == StackValueKind.NativeInt)
                        {
                            if (unsigned)
                            {
                                UIntPtr value = (UIntPtr)stackItem.AsNativeInt().ToPointer();
                                result = value.ToUInt64();
                            }
                            else
                            {
                                IntPtr value = stackItem.AsNativeInt();
                                result = checkOverflow ? checked((ulong)value.ToInt64()) : (ulong)value;
                            }
                        }
                        else
                        {
                            ThrowHelper.ThrowInvalidProgramException();
                        }

                        _interpreter.EvaluationStack.Push(StackItem.FromInt64((long)result));
                    }
                    break;
                case WellKnownType.Single:
                case WellKnownType.Double:
                    {
                        double result = default(double);
                        if (stackItem.Kind == StackValueKind.Int32)
                        {
                            if (unsigned)
                            {
                                uint value = (uint)stackItem.AsInt32();
                                result = (double)value;
                            }
                            else
                            {
                                int value = stackItem.AsInt32();
                                result = (double)value;
                            }
                        }
                        else if (stackItem.Kind == StackValueKind.Int64)
                        {
                            if (unsigned)
                            {
                                ulong value = (ulong)stackItem.AsInt64();
                                result = (double)value;
                            }
                            else
                            {
                                result = (double)stackItem.AsInt64();
                            }
                        }
                        else if (stackItem.Kind == StackValueKind.Float)
                        {
                            result = stackItem.AsDouble();
                        }
                        else if (stackItem.Kind == StackValueKind.NativeInt)
                        {
                            if (unsigned)
                            {
                                UIntPtr value = (UIntPtr)stackItem.AsNativeInt().ToPointer();
                                result = (double)value;
                            }
                            else
                            {
                                result = (double)stackItem.AsNativeInt();
                            }
                        }
                        else
                        {
                            ThrowHelper.ThrowInvalidProgramException();
                        }

                        _interpreter.EvaluationStack.Push(StackItem.FromDouble(result));
                    }
                    break;
                case WellKnownType.IntPtr:
                    {
                        IntPtr result = default(IntPtr);
                        if (stackItem.Kind == StackValueKind.Int32)
                        {
                            if (unsigned)
                            {
                                uint value = (uint)stackItem.AsInt32();
                                result = (IntPtr)value;
                            }
                            else
                            {
                                int value = stackItem.AsInt32();
                                result = (IntPtr)value;
                            }
                        }
                        else if (stackItem.Kind == StackValueKind.Int64)
                        {
                            if (unsigned)
                            {
                                ulong value = (ulong)stackItem.AsInt64();
                                result = (IntPtr)value;
                            }
                            else
                            {
                                result = (IntPtr)stackItem.AsInt64();
                            }
                        }
                        else if (stackItem.Kind == StackValueKind.Float)
                        {
                            result = (IntPtr)stackItem.AsDouble();
                        }
                        else if (stackItem.Kind == StackValueKind.NativeInt)
                        {
                            if (unsigned)
                            {
                                UIntPtr value = (UIntPtr)stackItem.AsNativeInt().ToPointer();
                                result = (IntPtr)value.ToPointer();
                            }
                            else
                            {
                                result = stackItem.AsNativeInt();
                            }
                        }
                        else
                        {
                            ThrowHelper.ThrowInvalidProgramException();
                        }

                        _interpreter.EvaluationStack.Push(StackItem.FromNativeInt(result));
                    }
                    break;
                case WellKnownType.UIntPtr:
                    {
                        UIntPtr result = default(UIntPtr);
                        if (stackItem.Kind == StackValueKind.Int32)
                        {
                            if (unsigned)
                            {
                                uint value = (uint)stackItem.AsInt32();
                                result = (UIntPtr)value;
                            }
                            else
                            {
                                int value = stackItem.AsInt32();
                                result = (UIntPtr)value;
                            }
                        }
                        else if (stackItem.Kind == StackValueKind.Int64)
                        {
                            if (unsigned)
                            {
                                ulong value = (ulong)stackItem.AsInt64();
                                result = (UIntPtr)value;
                            }
                            else
                            {
                                result = (UIntPtr)stackItem.AsInt64();
                            }
                        }
                        else if (stackItem.Kind == StackValueKind.Float)
                        {
                            result = (UIntPtr)stackItem.AsDouble();
                        }
                        else if (stackItem.Kind == StackValueKind.NativeInt)
                        {
                            if (unsigned)
                            {
                                result = (UIntPtr)stackItem.AsNativeInt().ToPointer();
                            }
                            else
                            {
                                result = (UIntPtr)stackItem.AsNativeInt().ToPointer();
                            }
                        }
                        else
                        {
                            ThrowHelper.ThrowInvalidProgramException();
                        }

                        _interpreter.EvaluationStack.Push(StackItem.FromNativeInt((IntPtr)result.ToPointer()));
                    }
                    break;
                default:
                    ThrowHelper.ThrowInvalidProgramException();
                    break;
            }
        }

        private void ImportUnaryOperation(ILOpcode opCode)
        {
            StackItem stackItem = PopWithValidation();
            switch (opCode)
            {
                case ILOpcode.neg:
                    {
                        if (stackItem.Kind == StackValueKind.Int32)
                        {
                            int value = stackItem.AsInt32();
                            _interpreter.EvaluationStack.Push(StackItem.FromInt32(-value));
                        }
                        else if (stackItem.Kind == StackValueKind.Int64)
                        {
                            long value = stackItem.AsInt64();
                            _interpreter.EvaluationStack.Push(StackItem.FromInt64(-value));
                        }
                        else if (stackItem.Kind == StackValueKind.NativeInt)
                        {
                            IntPtr value = stackItem.AsNativeInt();
                            _interpreter.EvaluationStack.Push(StackItem.FromNativeInt((IntPtr)(-(long)value)));
                        }
                        else if (stackItem.Kind == StackValueKind.Float)
                        {
                            double value = stackItem.AsDouble();
                            _interpreter.EvaluationStack.Push(StackItem.FromDouble(-value));
                        }
                        else
                        {
                            ThrowHelper.ThrowInvalidProgramException();
                        }
                    }
                    break;
                case ILOpcode.not:
                    {
                        if (stackItem.Kind == StackValueKind.Int32)
                        {
                            int value = stackItem.AsInt32();
                            _interpreter.EvaluationStack.Push(StackItem.FromInt32(~value));
                        }
                        else if (stackItem.Kind == StackValueKind.Int64)
                        {
                            long value = stackItem.AsInt64();
                            _interpreter.EvaluationStack.Push(StackItem.FromInt64(~value));
                        }
                        else if (stackItem.Kind == StackValueKind.NativeInt)
                        {
                            IntPtr value = stackItem.AsNativeInt();
                            _interpreter.EvaluationStack.Push(StackItem.FromNativeInt((IntPtr)(~(long)value)));
                        }
                        else
                        {
                            ThrowHelper.ThrowInvalidProgramException();
                        }
                    }
                    break;
                default:
                    ThrowHelper.ThrowInvalidProgramException();
                    break;
            }
        }

        private void ImportCpOpj(int token)
        {
            throw new NotImplementedException();
        }

        private void ImportCkFinite()
        {
            throw new NotImplementedException();
        }

        private void ImportLocalAlloc()
        {
            throw new NotImplementedException();
        }

        private void ImportEndFilter()
        {
            throw new NotImplementedException();
        }

        private void ImportCpBlk()
        {
            throw new NotImplementedException();
        }

        private void ImportInitBlk()
        {
            throw new NotImplementedException();
        }

        private void ImportRethrow()
        {
            throw new NotImplementedException();
        }

        private void ImportSizeOf(int token)
        {
            throw new NotImplementedException();
        }

        private void ImportUnalignedPrefix(byte alignment)
        {
            throw new NotImplementedException();
        }

        private void ImportVolatilePrefix()
        {
            throw new NotImplementedException();
        }

        private void ImportTailPrefix()
        {
            throw new NotImplementedException();
        }

        private void ImportNoPrefix(byte mask)
        {
            throw new NotImplementedException();
        }

        private void ImportThrow()
        {
            throw new NotImplementedException();
        }

        private void ImportInitObj(int token)
        {
            throw new NotImplementedException();
        }

        private void ImportLoadLength()
        {
            throw new NotImplementedException();
        }

        private void ImportEndFinally()
        {
            throw new NotImplementedException();
        }

        private void ImportFallthrough(BasicBlock nextBasicBlock)
        {
            throw new NotImplementedException();
        }

        private void ImportReadOnlyPrefix()
        {
            throw new NotImplementedException();
        }

        private void ImportRefAnyType()
        {
            throw new NotImplementedException();
        }

        private void ImportConstrainedPrefix(int v)
        {
            throw new NotImplementedException();
        }

        private void ImportLdFtn(int v, ILOpcode opCode)
        {
            throw new NotImplementedException();
        }

        private void ImportArgList()
        {
            throw new NotImplementedException();
        }

        private void ImportLeave(BasicBlock basicBlock)
        {
            throw new NotImplementedException();
        }

        private void ImportLdToken(int v)
        {
            throw new NotImplementedException();
        }

        private void ImportMkRefAny(int v)
        {
            throw new NotImplementedException();
        }

        private void ImportRefAnyVal(int v)
        {
            throw new NotImplementedException();
        }

        private void ImportAddressOfElement(int v)
        {
            throw new NotImplementedException();
        }

        private void ImportNewArray(int v)
        {
            throw new NotImplementedException();
        }

        private void ImportBox(int v)
        {
            throw new NotImplementedException();
        }

        private void ImportStoreField(int v1, bool v2)
        {
            throw new NotImplementedException();
        }

        private void ImportAddressOfField(int v1, bool v2)
        {
            throw new NotImplementedException();
        }

        private void ImportLoadField(int v1, bool v2)
        {
            throw new NotImplementedException();
        }

        private void ImportUnbox(int v, ILOpcode opCode)
        {
            throw new NotImplementedException();
        }

        private void ImportCasting(ILOpcode opCode, int v)
        {
            throw new NotImplementedException();
        }

        private void ImportLoadString(int token)
        {
            string str = (string)_methodIL.GetObject(token);
            _interpreter.EvaluationStack.Push(StackItem.FromObjectRef(str));
        }

        private void ImportBinaryOperation(ILOpcode opCode)
        {
            throw new NotImplementedException();
        }

        private void ImportSwitchJump(int jmpBase, int[] jmpDelta, BasicBlock basicBlock)
        {
            throw new NotImplementedException();
        }

        private void ImportBranch(ILOpcode iLOpcode, BasicBlock basicBlock1, BasicBlock basicBlock2)
        {
            throw new NotImplementedException();
        }

        private void ImportCall(ILOpcode opCode, int v)
        {
            throw new NotImplementedException();
        }

        private void ImportJmp(int v)
        {
            throw new NotImplementedException();
        }

        private void ImportLoadIndirect(int token)
        {
            ImportLoadIndirect(ResolveTypeToken(token));
        }

        private void ImportLoadIndirect(TypeDesc type)
        {
            throw new NotImplementedException();
        }

        private void ImportStoreIndirect(int token)
        {
            ImportStoreIndirect(ResolveTypeToken(token));
        }

        private void ImportStoreIndirect(TypeDesc type)
        {
            throw new NotImplementedException();
        }

        private void ImportLoadElement(int token)
        {
            ImportLoadElement(ResolveTypeToken(token));
        }

        private void ImportLoadElement(TypeDesc elementType)
        {
            throw new NotImplementedException();
        }

        private void ImportStoreElement(int token)
        {
            ImportStoreElement(ResolveTypeToken(token));
        }

        private void ImportStoreElement(TypeDesc elementType)
        {
            throw new NotImplementedException();
        }
    }
}
