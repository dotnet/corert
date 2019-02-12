﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Internal.IL;
using Internal.Runtime.Augments;
using Internal.Runtime.CallInterceptor;
using Internal.Runtime.CompilerServices;
using Internal.TypeSystem;


namespace Internal.Runtime.Interpreter
{
    internal unsafe class ILInterpreter
    {
        private readonly MethodDesc _method;
        private readonly MethodIL _methodIL;
        private readonly TypeSystemContext _context;
        private readonly LowLevelStack<StackItem> _stack;

        private StackItem[] _locals;

        private CallInterceptorArgs _callInterceptorArgs;

        public LowLevelStack<StackItem> EvaluationStack => _stack;

        public TypeSystemContext TypeSystemContext => _context;

        public ILInterpreter(TypeSystemContext context, MethodDesc method, MethodIL methodIL)
        {
            _context = context;
            _method = method;
            _methodIL = methodIL;
            _locals = new StackItem[methodIL.GetLocals().Length];
            _stack = new LowLevelStack<StackItem>();
        }

        public void SetReturnValue<T>(T value)
        {
            _callInterceptorArgs.ArgumentsAndReturnValue.SetVar<T>(0, value);
        }

        public T GetArgument<T>(int index)
        {
            return _callInterceptorArgs.ArgumentsAndReturnValue.GetVar<T>(index + 1);
        }

        public void SetArgument<T>(int index, T value)
        {
            _callInterceptorArgs.ArgumentsAndReturnValue.SetVar<T>(index + 1, value);
        }

        public StackItem PopWithValidation()
        {
            bool hasStackItem = _stack.TryPop(out StackItem stackItem);
            if (!hasStackItem)
            {
                ThrowHelper.ThrowInvalidProgramException();
            }

            return stackItem;
        }

        public StackItem PeekWithValidation()
        {
            bool hasStackItem = _stack.TryPeek(out StackItem stackItem);
            if (!hasStackItem)
            {
                ThrowHelper.ThrowInvalidProgramException();
            }

            return stackItem;
        }

        public void InterpretMethod(ref CallInterceptorArgs callInterceptorArgs)
        {
            _callInterceptorArgs = callInterceptorArgs;
            ILReader reader = new ILReader(_methodIL.GetILBytes());

            while (reader.HasNext)
            {
                ILOpcode opcode = reader.ReadILOpcode();
                switch (opcode)
                {
                    case ILOpcode.nop:
                        // Do nothing!
                        break;
                    case ILOpcode.break_:
                        throw new NotImplementedException();
                    case ILOpcode.ldarg_0:
                    case ILOpcode.ldarg_1:
                    case ILOpcode.ldarg_2:
                    case ILOpcode.ldarg_3:
                        InterpretLoadArgument(opcode - ILOpcode.ldarg_0);
                        break;
                    case ILOpcode.ldloc_0:
                    case ILOpcode.ldloc_1:
                    case ILOpcode.ldloc_2:
                    case ILOpcode.ldloc_3:
                        InterpretLoadLocal(opcode - ILOpcode.ldloc_0);
                        break;
                    case ILOpcode.stloc_0:
                    case ILOpcode.stloc_1:
                    case ILOpcode.stloc_2:
                    case ILOpcode.stloc_3:
                        InterpretStoreLocal(opcode - ILOpcode.stloc_0);
                        break;
                    case ILOpcode.ldarg_s:
                        InterpretLoadArgument(reader.ReadILByte());
                        break;
                    case ILOpcode.ldarga_s:
                        throw new NotImplementedException();
                    case ILOpcode.starg_s:
                        InterpretStoreArgument(reader.ReadILByte());
                        break;
                    case ILOpcode.ldloc_s:
                        InterpretLoadLocal(reader.ReadILByte());
                        break;
                    case ILOpcode.ldloca_s:
                        throw new NotImplementedException();
                    case ILOpcode.stloc_s:
                        InterpretStoreLocal(reader.ReadILByte());
                        break;
                    case ILOpcode.ldnull:
                        InterpretLoadNull();
                        break;
                    case ILOpcode.ldc_i4_m1:
                        InterpretLoadConstant(-1);
                        break;
                    case ILOpcode.ldc_i4_0:
                    case ILOpcode.ldc_i4_1:
                    case ILOpcode.ldc_i4_2:
                    case ILOpcode.ldc_i4_3:
                    case ILOpcode.ldc_i4_4:
                    case ILOpcode.ldc_i4_5:
                    case ILOpcode.ldc_i4_6:
                    case ILOpcode.ldc_i4_7:
                    case ILOpcode.ldc_i4_8:
                        InterpretLoadConstant(opcode - ILOpcode.ldc_i4_0);
                        break;
                    case ILOpcode.ldc_i4_s:
                        InterpretLoadConstant((sbyte)reader.ReadILByte());
                        break;
                    case ILOpcode.ldc_i4:
                        InterpretLoadConstant((int)reader.ReadILUInt32());
                        break;
                    case ILOpcode.ldc_i8:
                        InterpretLoadConstant((long)reader.ReadILUInt64());
                        break;
                    case ILOpcode.ldc_r4:
                        InterpretLoadConstant(reader.ReadILFloat());
                        break;
                    case ILOpcode.ldc_r8:
                        InterpretLoadConstant(reader.ReadILDouble());
                        break;
                    case ILOpcode.dup:
                        InterpretDup();
                        break;
                    case ILOpcode.pop:
                        InterpretPop();
                        break;
                    case ILOpcode.jmp:
                        throw new NotImplementedException();
                    case ILOpcode.call:
                        throw new NotImplementedException();
                    case ILOpcode.calli:
                        throw new NotImplementedException();
                    case ILOpcode.ret:
                        InterpretReturn();
                        break;
                    case ILOpcode.br_s:
                    case ILOpcode.brfalse_s:
                    case ILOpcode.brtrue_s:
                    case ILOpcode.beq_s:
                    case ILOpcode.bge_s:
                    case ILOpcode.bgt_s:
                    case ILOpcode.ble_s:
                    case ILOpcode.blt_s:
                    case ILOpcode.bne_un_s:
                    case ILOpcode.bge_un_s:
                    case ILOpcode.bgt_un_s:
                    case ILOpcode.ble_un_s:
                    case ILOpcode.blt_un_s:
                        {
                            int delta = (sbyte)reader.ReadILByte();
                            InterpretBranch(ref reader, opcode, reader.Offset + delta);
                        }
                        break;
                    case ILOpcode.br:
                    case ILOpcode.brfalse:
                    case ILOpcode.brtrue:
                    case ILOpcode.beq:
                    case ILOpcode.bge:
                    case ILOpcode.bgt:
                    case ILOpcode.ble:
                    case ILOpcode.blt:
                    case ILOpcode.bne_un:
                    case ILOpcode.bge_un:
                    case ILOpcode.bgt_un:
                    case ILOpcode.ble_un:
                    case ILOpcode.blt_un:
                        {
                            int delta = (int)reader.ReadILUInt32();
                            InterpretBranch(ref reader, opcode, reader.Offset + delta);
                        }
                        break;
                    case ILOpcode.switch_:
                        {
                            var count = reader.ReadILUInt32();
                            var jmpBase = reader.Offset + (int)(4 * count);
                            var jmpDelta = new int[count];

                            for (uint i = 0; i < count; i++)
                                jmpDelta[i] = (int)reader.ReadILUInt32();

                            InterpretSwitch(ref reader, jmpBase, jmpDelta);
                        }
                        break;
                    case ILOpcode.ldind_i1:
                        throw new NotImplementedException();
                    case ILOpcode.ldind_u1:
                        throw new NotImplementedException();
                    case ILOpcode.ldind_i2:
                        throw new NotImplementedException();
                    case ILOpcode.ldind_u2:
                        throw new NotImplementedException();
                    case ILOpcode.ldind_i4:
                        throw new NotImplementedException();
                    case ILOpcode.ldind_u4:
                        throw new NotImplementedException();
                    case ILOpcode.ldind_i8:
                        throw new NotImplementedException();
                    case ILOpcode.ldind_i:
                        throw new NotImplementedException();
                    case ILOpcode.ldind_r4:
                        throw new NotImplementedException();
                    case ILOpcode.ldind_r8:
                        throw new NotImplementedException();
                    case ILOpcode.ldind_ref:
                        throw new NotImplementedException();
                    case ILOpcode.stind_ref:
                        throw new NotImplementedException();
                    case ILOpcode.stind_i1:
                        throw new NotImplementedException();
                    case ILOpcode.stind_i2:
                        throw new NotImplementedException();
                    case ILOpcode.stind_i4:
                        throw new NotImplementedException();
                    case ILOpcode.stind_i8:
                        throw new NotImplementedException();
                    case ILOpcode.stind_r4:
                        throw new NotImplementedException();
                    case ILOpcode.stind_r8:
                        throw new NotImplementedException();
                    case ILOpcode.add:
                    case ILOpcode.sub:
                    case ILOpcode.mul:
                    case ILOpcode.div:
                    case ILOpcode.div_un:
                    case ILOpcode.rem:
                    case ILOpcode.rem_un:
                    case ILOpcode.and:
                    case ILOpcode.or:
                    case ILOpcode.xor:
                        InterpretBinaryOperation(opcode);
                        break;
                    case ILOpcode.shl:
                    case ILOpcode.shr:
                    case ILOpcode.shr_un:
                        InterpretShiftOperation(opcode);
                        break;
                    case ILOpcode.neg:
                    case ILOpcode.not:
                        InterpretUnaryOperation(opcode);
                        break;
                    case ILOpcode.conv_i1:
                        InterpretConvertOperation(WellKnownType.SByte, false, false);
                        break;
                    case ILOpcode.conv_i2:
                        InterpretConvertOperation(WellKnownType.Int16, false, false);
                        break;
                    case ILOpcode.conv_i4:
                        InterpretConvertOperation(WellKnownType.Int32, false, false);
                        break;
                    case ILOpcode.conv_i8:
                        InterpretConvertOperation(WellKnownType.Int64, false, false);
                        break;
                    case ILOpcode.conv_r4:
                        InterpretConvertOperation(WellKnownType.Single, false, false);
                        break;
                    case ILOpcode.conv_r8:
                        InterpretConvertOperation(WellKnownType.Double, false, false);
                        break;
                    case ILOpcode.conv_u4:
                        InterpretConvertOperation(WellKnownType.UInt32, false, false);
                        break;
                    case ILOpcode.conv_u8:
                        InterpretConvertOperation(WellKnownType.UInt64, false, false);
                        break;
                    case ILOpcode.callvirt:
                        throw new NotImplementedException();
                    case ILOpcode.cpobj:
                        throw new NotImplementedException();
                    case ILOpcode.ldobj:
                        throw new NotImplementedException();
                    case ILOpcode.ldstr:
                        InterpretLoadConstant((string)_methodIL.GetObject(reader.ReadILToken()));
                        break;
                    case ILOpcode.newobj:
                        throw new NotImplementedException();
                    case ILOpcode.castclass:
                        throw new NotImplementedException();
                    case ILOpcode.isinst:
                        throw new NotImplementedException();
                    case ILOpcode.conv_r_un:
                        InterpretConvertOperation(WellKnownType.Double, false, true);
                        break;
                    case ILOpcode.unbox:
                        throw new NotImplementedException();
                    case ILOpcode.throw_:
                        throw new NotImplementedException();
                    case ILOpcode.ldfld:
                        throw new NotImplementedException();
                    case ILOpcode.ldflda:
                        throw new NotImplementedException();
                    case ILOpcode.stfld:
                        throw new NotImplementedException();
                    case ILOpcode.ldsfld:
                        throw new NotImplementedException();
                    case ILOpcode.ldsflda:
                        throw new NotImplementedException();
                    case ILOpcode.stsfld:
                        throw new NotImplementedException();
                    case ILOpcode.stobj:
                        throw new NotImplementedException();
                    case ILOpcode.conv_ovf_i1_un:
                        InterpretConvertOperation(WellKnownType.SByte, true, true);
                        break;
                    case ILOpcode.conv_ovf_i2_un:
                        InterpretConvertOperation(WellKnownType.Int16, true, true);
                        break;
                    case ILOpcode.conv_ovf_i4_un:
                        InterpretConvertOperation(WellKnownType.Int32, true, true);
                        break;
                    case ILOpcode.conv_ovf_i8_un:
                        InterpretConvertOperation(WellKnownType.Int64, true, true);
                        break;
                    case ILOpcode.conv_ovf_u1_un:
                        InterpretConvertOperation(WellKnownType.Byte, true, true);
                        break;
                    case ILOpcode.conv_ovf_u2_un:
                        InterpretConvertOperation(WellKnownType.UInt16, true, true);
                        break;
                    case ILOpcode.conv_ovf_u4_un:
                        InterpretConvertOperation(WellKnownType.UInt32, true, true);
                        break;
                    case ILOpcode.conv_ovf_u8_un:
                        InterpretConvertOperation(WellKnownType.UInt64, true, true);
                        break;
                    case ILOpcode.conv_ovf_i_un:
                        InterpretConvertOperation(WellKnownType.IntPtr, true, true);
                        break;
                    case ILOpcode.conv_ovf_u_un:
                        InterpretConvertOperation(WellKnownType.UIntPtr, true, true);
                        break;
                    case ILOpcode.box:
                        throw new NotImplementedException();
                    case ILOpcode.newarr:
                        InterpretNewArray(reader.ReadILToken());
                        break;
                    case ILOpcode.ldlen:
                        InterpretLoadLength();
                        break;
                    case ILOpcode.ldelema:
                        throw new NotImplementedException();
                    case ILOpcode.ldelem_i1:
                    case ILOpcode.ldelem_u1:
                    case ILOpcode.ldelem_i2:
                    case ILOpcode.ldelem_u2:
                    case ILOpcode.ldelem_i4:
                    case ILOpcode.ldelem_u4:
                    case ILOpcode.ldelem_i8:
                    case ILOpcode.ldelem_i:
                    case ILOpcode.ldelem_r4:
                    case ILOpcode.ldelem_r8:
                    case ILOpcode.ldelem_ref:
                        InterpretLoadElement(opcode);
                        break;
                    case ILOpcode.stelem_i:
                    case ILOpcode.stelem_i1:
                    case ILOpcode.stelem_i2:
                    case ILOpcode.stelem_i4:
                    case ILOpcode.stelem_i8:
                    case ILOpcode.stelem_r4:
                    case ILOpcode.stelem_r8:
                    case ILOpcode.stelem_ref:
                        InterpretStoreElement(opcode);
                        break;
                    case ILOpcode.ldelem:
                        InterpretLoadElement(reader.ReadILToken());
                        break;
                    case ILOpcode.stelem:
                        InterpretStoreElement(reader.ReadILToken());
                        break;
                    case ILOpcode.unbox_any:
                        throw new NotImplementedException();
                    case ILOpcode.conv_ovf_i1:
                        InterpretConvertOperation(WellKnownType.SByte, true, false);
                        break;
                    case ILOpcode.conv_ovf_u1:
                        InterpretConvertOperation(WellKnownType.Byte, true, false);
                        break;
                    case ILOpcode.conv_ovf_i2:
                        InterpretConvertOperation(WellKnownType.Int16, true, false);
                        break;
                    case ILOpcode.conv_ovf_u2:
                        InterpretConvertOperation(WellKnownType.UInt16, true, false);
                        break;
                    case ILOpcode.conv_ovf_i4:
                        InterpretConvertOperation(WellKnownType.Int32, true, false);
                        break;
                    case ILOpcode.conv_ovf_u4:
                        InterpretConvertOperation(WellKnownType.UInt32, true, false);
                        break;
                    case ILOpcode.conv_ovf_i8:
                        InterpretConvertOperation(WellKnownType.Int64, true, false);
                        break;
                    case ILOpcode.conv_ovf_u8:
                        InterpretConvertOperation(WellKnownType.UInt64, true, false);
                        break;
                    case ILOpcode.refanyval:
                        throw new NotImplementedException();
                    case ILOpcode.ckfinite:
                        throw new NotImplementedException();
                    case ILOpcode.mkrefany:
                        throw new NotImplementedException();
                    case ILOpcode.ldtoken:
                        throw new NotImplementedException();
                    case ILOpcode.conv_u2:
                        InterpretConvertOperation(WellKnownType.UInt16, false, false);
                        break;
                    case ILOpcode.conv_u1:
                        InterpretConvertOperation(WellKnownType.Byte, false, false);
                        break;
                    case ILOpcode.conv_i:
                        InterpretConvertOperation(WellKnownType.IntPtr, false, false);
                        break;
                    case ILOpcode.conv_ovf_i:
                        InterpretConvertOperation(WellKnownType.IntPtr, true, false);
                        break;
                    case ILOpcode.conv_ovf_u:
                        InterpretConvertOperation(WellKnownType.UIntPtr, true, false);
                        break;
                    case ILOpcode.add_ovf:
                    case ILOpcode.add_ovf_un:
                    case ILOpcode.mul_ovf:
                    case ILOpcode.mul_ovf_un:
                    case ILOpcode.sub_ovf:
                    case ILOpcode.sub_ovf_un:
                        InterpretBinaryOperation(opcode);
                        break;
                    case ILOpcode.endfinally:
                        throw new NotImplementedException();
                    case ILOpcode.leave:
                        throw new NotImplementedException();
                    case ILOpcode.leave_s:
                        throw new NotImplementedException();
                    case ILOpcode.stind_i:
                        throw new NotImplementedException();
                    case ILOpcode.conv_u:
                        InterpretConvertOperation(WellKnownType.UIntPtr, false, false);
                        break;
                    case ILOpcode.arglist:
                        throw new NotImplementedException();
                    case ILOpcode.ceq:
                    case ILOpcode.cgt:
                    case ILOpcode.cgt_un:
                    case ILOpcode.clt:
                    case ILOpcode.clt_un:
                        InterpretCompareOperation(opcode);
                        break;
                    case ILOpcode.ldftn:
                        throw new NotImplementedException();
                    case ILOpcode.ldvirtftn:
                        throw new NotImplementedException();
                    case ILOpcode.ldarg:
                        InterpretLoadArgument(reader.ReadILUInt16());
                        break;
                    case ILOpcode.ldarga:
                        throw new NotImplementedException();
                    case ILOpcode.starg:
                        InterpretStoreArgument(reader.ReadILUInt16());
                        break;
                    case ILOpcode.ldloc:
                        InterpretLoadLocal(reader.ReadILUInt16());
                        break;
                    case ILOpcode.ldloca:
                        throw new NotImplementedException();
                    case ILOpcode.stloc:
                        InterpretStoreLocal(reader.ReadILUInt16());
                        break;
                    case ILOpcode.localloc:
                        throw new NotImplementedException();
                    case ILOpcode.endfilter:
                        throw new NotImplementedException();
                    case ILOpcode.unaligned:
                        throw new NotImplementedException();
                    case ILOpcode.volatile_:
                        throw new NotImplementedException();
                    case ILOpcode.tail:
                        throw new NotImplementedException();
                    case ILOpcode.initobj:
                        throw new NotImplementedException();
                    case ILOpcode.constrained:
                        throw new NotImplementedException();
                    case ILOpcode.cpblk:
                        throw new NotImplementedException();
                    case ILOpcode.initblk:
                        throw new NotImplementedException();
                    case ILOpcode.no:
                        throw new NotImplementedException();
                    case ILOpcode.rethrow:
                        throw new NotImplementedException();
                    case ILOpcode.sizeof_:
                        throw new NotImplementedException();
                    case ILOpcode.refanytype:
                        throw new NotImplementedException();
                    case ILOpcode.readonly_:
                        throw new NotImplementedException();
                    default:
                        Debug.Assert(false);
                        break;
                }
            }
        }

        private void InterpretLoadConstant(int constant)
        {
            _stack.Push(StackItem.FromInt32(constant));
        }

        private void InterpretLoadConstant(long constant)
        {
            _stack.Push(StackItem.FromInt64(constant));
        }

        private void InterpretLoadConstant(double constant)
        {
            _stack.Push(StackItem.FromDouble(constant));
        }

        private void InterpretLoadConstant(string constant)
        {
            _stack.Push(StackItem.FromObjectRef(constant));
        }

        private void InterpretLoadNull()
        {
            _stack.Push(StackItem.FromObjectRef(null));
        }

        private void InterpretPop()
        {
            PopWithValidation();
        }

        private void InterpretDup()
        {
            _stack.Push(PeekWithValidation());
        }

        private void InterpretLoadLocal(int index)
        {
            Debug.Assert(index >= 0);
            _stack.Push(_locals[index]);
        }

        private void InterpretStoreLocal(int index)
        {
            Debug.Assert(index >= 0);
            _locals[index] = PopWithValidation();
        }

        private void InterpretLoadArgument(int index)
        {
            Debug.Assert(index >= 0);

            TypeDesc argument = default(TypeDesc);

            if (!_method.Signature.IsStatic)
            {
                if (index == 0)
                    argument = _method.OwningType;
                else
                    argument = _method.Signature[index - 1];
            }
            else
            {
                argument = _method.Signature[index];
            }

        again:
            switch (argument.Category)
            {
                case TypeFlags.Boolean:
                    _stack.Push(StackItem.FromInt32(GetArgument<bool>(index) ? 1 : 0));
                    break;
                case TypeFlags.Char:
                    _stack.Push(StackItem.FromInt32(GetArgument<char>(index)));
                    break;
                case TypeFlags.SByte:
                case TypeFlags.Byte:
                    _stack.Push(StackItem.FromInt32(GetArgument<byte>(index)));
                    break;
                case TypeFlags.Int16:
                case TypeFlags.UInt16:
                    _stack.Push(StackItem.FromInt32(GetArgument<short>(index)));
                    break;
                case TypeFlags.Int32:
                case TypeFlags.UInt32:
                    _stack.Push(StackItem.FromInt32(GetArgument<int>(index)));
                    break;
                case TypeFlags.Int64:
                case TypeFlags.UInt64:
                    _stack.Push(StackItem.FromInt64(GetArgument<long>(index)));
                    break;
                case TypeFlags.IntPtr:
                case TypeFlags.UIntPtr:
                    _stack.Push(StackItem.FromNativeInt(GetArgument<IntPtr>(index)));
                    break;
                case TypeFlags.Single:
                case TypeFlags.Double:
                    _stack.Push(StackItem.FromDouble(GetArgument<double>(index)));
                    break;
                case TypeFlags.ValueType:
                case TypeFlags.Nullable:
                    _stack.Push(StackItem.FromValueType(GetArgument<ValueType>(index)));
                    break;
                case TypeFlags.Enum:
                    argument = argument.UnderlyingType;
                    goto again;
                case TypeFlags.Class:
                case TypeFlags.Interface:
                case TypeFlags.Array:
                case TypeFlags.SzArray:
                    _stack.Push(StackItem.FromObjectRef(GetArgument<object>(index)));
                    break;
                default:
                    // TODO: Support more complex return types
                    throw new NotImplementedException();
            }
        }

        private void InterpretStoreArgument(int index)
        {
            Debug.Assert(index >= 0);
            
            StackItem stackItem = PopWithValidation();
            switch (stackItem.Kind)
            {
                case StackValueKind.Int32:
                    SetArgument(index, stackItem.AsInt32());
                    break;
                case StackValueKind.Int64:
                    SetArgument(index, stackItem.AsInt64());
                    break;
                case StackValueKind.NativeInt:
                    SetArgument(index, stackItem.AsNativeInt());
                    break;
                case StackValueKind.Float:
                    SetArgument(index, stackItem.AsDouble());
                    break;
                case StackValueKind.ByRef:
                    // TODO: Add support for ByRef
                    throw new NotImplementedException();
                case StackValueKind.ObjRef:
                    SetArgument(index, stackItem.AsObjectRef());
                    break;
                case StackValueKind.ValueType:
                    SetArgument(index, stackItem.AsValueType());
                    break;
                default:
                    ThrowHelper.ThrowInvalidProgramException();
                    break;
            }
        }

        private void InterpretReturn()
        {
            var returnType = _method.Signature.ReturnType;
            if (returnType.IsVoid)
                return;

            StackItem stackItem = PopWithValidation();

        again:
            switch (returnType.Category)
            {
                case TypeFlags.Boolean:
                    SetReturnValue(stackItem.AsInt32() != 0);
                    break;
                case TypeFlags.Char:
                    SetReturnValue((char)stackItem.AsInt32());
                    break;
                case TypeFlags.SByte:
                case TypeFlags.Byte:
                    SetReturnValue((sbyte)stackItem.AsInt32());
                    break;
                case TypeFlags.Int16:
                case TypeFlags.UInt16:
                    SetReturnValue((short)stackItem.AsInt32());
                    break;
                case TypeFlags.Int32:
                case TypeFlags.UInt32:
                    SetReturnValue(stackItem.AsInt32());
                    break;
                case TypeFlags.Int64:
                case TypeFlags.UInt64:
                    SetReturnValue(stackItem.AsInt64());
                    break;
                case TypeFlags.IntPtr:
                case TypeFlags.UIntPtr:
                    SetReturnValue(stackItem.AsNativeInt());
                    break;
                case TypeFlags.Single:
                    SetReturnValue((float)stackItem.AsDouble());
                    break;
                case TypeFlags.Double:
                    SetReturnValue(stackItem.AsDouble());
                    break;
                case TypeFlags.ValueType:
                case TypeFlags.Nullable:
                    SetReturnValue(stackItem.AsValueType());
                    break;
                case TypeFlags.Enum:
                    returnType = returnType.UnderlyingType;
                    goto again;
                case TypeFlags.Class:
                case TypeFlags.Interface:
                case TypeFlags.Array:
                case TypeFlags.SzArray:
                    SetReturnValue(stackItem.AsObjectRef());
                    break;
                default:
                    // TODO: Support more complex return types
                    throw new NotImplementedException();
            }
        }

        private void InterpretShiftOperation(ILOpcode opcode)
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

                        _stack.Push(StackItem.FromInt32(value));
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

                        _stack.Push(StackItem.FromInt64(value));
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

                        _stack.Push(StackItem.FromNativeInt(value));
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

        private void InterpretCompareOperation(ILOpcode opcode)
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

            _stack.Push(StackItem.FromInt32(result ? 1 : 0));
        }

        private void InterpretConvertOperation(WellKnownType wellKnownType, bool checkOverflow, bool unsigned)
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

                        _stack.Push(StackItem.FromInt32(result));
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

                        _stack.Push(StackItem.FromInt32(result));
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

                        _stack.Push(StackItem.FromInt32(result));
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

                        _stack.Push(StackItem.FromInt32(result));
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

                        _stack.Push(StackItem.FromInt32(result));
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

                        _stack.Push(StackItem.FromInt32((int)result));
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

                        _stack.Push(StackItem.FromInt64(result));
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

                        _stack.Push(StackItem.FromInt64((long)result));
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

                        _stack.Push(StackItem.FromDouble(result));
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

                        _stack.Push(StackItem.FromNativeInt(result));
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

                        _stack.Push(StackItem.FromNativeInt((IntPtr)result.ToPointer()));
                    }
                    break;
                default:
                    ThrowHelper.ThrowInvalidProgramException();
                    break;
            }
        }

        private void InterpretUnaryOperation(ILOpcode opcode)
        {
            StackItem stackItem = PopWithValidation();
            switch (stackItem.Kind)
            {
                case StackValueKind.Int32:
                    {
                        int value = stackItem.AsInt32();
                        switch (opcode)
                        {
                            case ILOpcode.neg:
                                _stack.Push(StackItem.FromInt32(-value));
                                break;
                            case ILOpcode.not:
                                _stack.Push(StackItem.FromInt32(~value));
                                break;
                            default:
                                Debug.Assert(false);
                                break;
                        }
                    }
                    break;
                case StackValueKind.Int64:
                    {
                        long value = stackItem.AsInt64();
                        switch (opcode)
                        {
                            case ILOpcode.neg:
                                _stack.Push(StackItem.FromInt64(-value));
                                break;
                            case ILOpcode.not:
                                _stack.Push(StackItem.FromInt64(~value));
                                break;
                            default:
                                Debug.Assert(false);
                                break;
                        }
                    }
                    break;
                case StackValueKind.NativeInt:
                    {
                        IntPtr value = stackItem.AsNativeInt();
                        switch (opcode)
                        {
                            case ILOpcode.neg:
                                _stack.Push(StackItem.FromNativeInt((IntPtr)(-(long)value)));
                                break;
                            case ILOpcode.not:
                                _stack.Push(StackItem.FromNativeInt((IntPtr)(~(long)value)));
                                break;
                            default:
                                Debug.Assert(false);
                                break;
                        }
                    }
                    break;
                case StackValueKind.Float:
                    {
                        double value = stackItem.AsDouble();
                        switch (opcode)
                        {
                            case ILOpcode.neg:
                                _stack.Push(StackItem.FromDouble(-value));
                                break;
                            case ILOpcode.not:
                                ThrowHelper.ThrowInvalidProgramException();
                                break;
                            default:
                                Debug.Assert(false);
                                break;
                        }
                    }
                    break;
                case StackValueKind.Unknown:
                case StackValueKind.ByRef:
                case StackValueKind.ObjRef:
                case StackValueKind.ValueType:
                default:
                    ThrowHelper.ThrowInvalidProgramException();
                    break;
            }
        }

        private void InterpretBinaryOperation(ILOpcode opcode)
        {
            StackItem op1 = PopWithValidation();
            StackItem op2 = PopWithValidation();

            StackValueKind kind = (op1.Kind > op2.Kind) ? op1.Kind : op2.Kind;

            switch (kind)
            {
                case StackValueKind.Int32:
                    {
                        int val1 = op1.AsInt32Unchecked();
                        int val2 = op2.AsInt32Unchecked();
                        int result = default(int);

                        switch (opcode)
                        {
                            case ILOpcode.add:
                                result = val1 + val2;
                                break;
                            case ILOpcode.add_ovf:
                                result = checked(val1 + val2);
                                break;
                            case ILOpcode.add_ovf_un:
                                result = (int)(checked((uint)val1 + (uint)val2));
                                break;
                            case ILOpcode.sub:
                                result = val2 - val1;
                                break;
                            case ILOpcode.sub_ovf:
                                result = checked(val2 - val1);
                                break;
                            case ILOpcode.sub_ovf_un:
                                result = (int)(checked((uint)val2 - (uint)val1));
                                break;
                            case ILOpcode.mul:
                                result = val1 * val2;
                                break;
                            case ILOpcode.mul_ovf:
                                result = checked(val1 * val2);
                                break;
                            case ILOpcode.mul_ovf_un:
                                result = (int)(checked((uint)val1 * (uint)val2));
                                break;
                            case ILOpcode.div:
                                result = val2 / val1;
                                break;
                            case ILOpcode.div_un:
                                result = (int)((uint)val2 / (uint)val1);
                                break;
                            case ILOpcode.rem:
                                result = val2 % val1;
                                break;
                            case ILOpcode.rem_un:
                                result = (int)((uint)val2 % (uint)val1);
                                break;
                            case ILOpcode.and:
                                result = val1 & val2;
                                break;
                            case ILOpcode.or:
                                result = val1 | val2;
                                break;
                            case ILOpcode.xor:
                                result = val1 ^ val2;
                                break;
                            default:
                                Debug.Assert(false);
                                break;
                        }

                        _stack.Push(StackItem.FromInt32(result));
                    }
                    break;
                case StackValueKind.Int64:
                    {
                        long val1 = op1.AsInt64Unchecked();
                        long val2 = op2.AsInt64Unchecked();
                        long result = default(long);

                        switch (opcode)
                        {
                            case ILOpcode.add:
                                result = val1 + val2;
                                break;
                            case ILOpcode.add_ovf:
                                result = checked(val1 + val2);
                                break;
                            case ILOpcode.add_ovf_un:
                                result = (long)(checked((ulong)val1 + (ulong)val2));
                                break;
                            case ILOpcode.sub:
                                result = val2 - val1;
                                break;
                            case ILOpcode.sub_ovf:
                                result = checked(val2 - val1);
                                break;
                            case ILOpcode.sub_ovf_un:
                                result = (long)(checked((ulong)val2 - (ulong)val1));
                                break;
                            case ILOpcode.mul:
                                result = val1 * val2;
                                break;
                            case ILOpcode.mul_ovf:
                                result = checked(val1 * val2);
                                break;
                            case ILOpcode.mul_ovf_un:
                                result = (long)(checked((ulong)val1 * (ulong)val2));
                                break;
                            case ILOpcode.div:
                                result = val2 / val1;
                                break;
                            case ILOpcode.div_un:
                                result = (long)((ulong)val2 / (ulong)val1);
                                break;
                            case ILOpcode.rem:
                                result = val2 % val1;
                                break;
                            case ILOpcode.rem_un:
                                result = (long)((ulong)val2 % (ulong)val1);
                                break;
                            case ILOpcode.and:
                                result = val1 & val2;
                                break;
                            case ILOpcode.or:
                                result = val1 | val2;
                                break;
                            case ILOpcode.xor:
                                result = val1 ^ val2;
                                break;
                            default:
                                Debug.Assert(false);
                                break;
                        }

                        _stack.Push(StackItem.FromInt64(result));
                    }
                    break;
                case StackValueKind.NativeInt:
                    {
                        IntPtr val1 = op1.AsNativeIntUnchecked();
                        IntPtr val2 = op2.AsNativeIntUnchecked();
                        IntPtr result = default(IntPtr);
#if BIT64
                        if (opcode == ILOpcode.add || opcode == ILOpcode.add_ovf
                            || opcode == ILOpcode.sub || opcode == ILOpcode.sub_ovf
                            || opcode == ILOpcode.mul || opcode == ILOpcode.mul_ovf
                            || opcode == ILOpcode.div || opcode == ILOpcode.rem)
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
                            case ILOpcode.add:
                                result = (IntPtr)((long)val1 + (long)val2);
                                break;
                            case ILOpcode.add_ovf:
                                result = (IntPtr)checked((long)val1 + (long)val2);
                                break;
                            case ILOpcode.add_ovf_un:
                                result = (IntPtr)(checked((ulong)(UIntPtr)val1.ToPointer() + (ulong)(UIntPtr)val2.ToPointer()));
                                break;
                            case ILOpcode.sub:
                                result = (IntPtr)((long)val2 - (long)val1);
                                break;
                            case ILOpcode.sub_ovf:
                                result = (IntPtr)checked((long)val2 - (long)val1);
                                break;
                            case ILOpcode.sub_ovf_un:
                                result = (IntPtr)(checked((ulong)(UIntPtr)val2.ToPointer() - (ulong)(UIntPtr)val1.ToPointer()));
                                break;
                            case ILOpcode.mul:
                                result = (IntPtr)((long)val1 * (long)val2);
                                break;
                            case ILOpcode.mul_ovf:
                                result = (IntPtr)checked((long)val1 * (long)val2);
                                break;
                            case ILOpcode.mul_ovf_un:
                                result = (IntPtr)checked((ulong)(UIntPtr)val1.ToPointer() * (ulong)(UIntPtr)val2.ToPointer());
                                break;
                            case ILOpcode.div:
                                result = (IntPtr)((long)val2 / (long)val1);
                                break;
                            case ILOpcode.div_un:
                                result = (IntPtr)((ulong)(UIntPtr)val2.ToPointer() / (ulong)(UIntPtr)val1.ToPointer());
                                break;
                            case ILOpcode.rem:
                                result = (IntPtr)((long)val2 % (long)val1);
                                break;
                            case ILOpcode.rem_un:
                                result = (IntPtr)((ulong)(UIntPtr)val2.ToPointer() % (ulong)(UIntPtr)val1.ToPointer());
                                break;
                            case ILOpcode.and:
                                result = (IntPtr)((long)val1 & (long)val2);
                                break;
                            case ILOpcode.or:
                                result = (IntPtr)((long)val1 | (long)val2);
                                break;
                            case ILOpcode.xor:
                                result = (IntPtr)((long)val1 ^ (long)val2);
                                break;
                            default:
                                Debug.Assert(false);
                                break;
                        }

                        _stack.Push(StackItem.FromNativeInt(result));
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
                        double result = default(double);

                        switch (opcode)
                        {
                            case ILOpcode.add:
                                result = val1 + val2;
                                break;
                            case ILOpcode.add_ovf:
                            case ILOpcode.add_ovf_un:
                                result = checked(val1 + val2);
                                break;
                            case ILOpcode.sub:
                                result = val2 - val1;
                                break;
                            case ILOpcode.sub_ovf:
                            case ILOpcode.sub_ovf_un:
                                result = checked(val2 - val1);
                                break;
                            case ILOpcode.mul:
                                result = val1 * val2;
                                break;
                            case ILOpcode.mul_ovf:
                            case ILOpcode.mul_ovf_un:
                                result = checked(val1 * val2);
                                break;
                            case ILOpcode.div:
                            case ILOpcode.div_un:
                                result = val2 / val1;
                                break;
                            case ILOpcode.rem:
                            case ILOpcode.rem_un:
                                result = val2 % val1;
                                break;
                            case ILOpcode.and:
                            case ILOpcode.or:
                            case ILOpcode.xor:
                                ThrowHelper.ThrowInvalidProgramException();
                                break;
                            default:
                                Debug.Assert(false);
                                break;
                        }

                        _stack.Push(StackItem.FromDouble(result));
                    }
                    break;
                case StackValueKind.ByRef:
                    // TODO: Add support for ByRef to StackItem
                    throw new NotImplementedException();
                case StackValueKind.Unknown:
                case StackValueKind.ObjRef:
                case StackValueKind.ValueType:
                default:
                    ThrowHelper.ThrowInvalidProgramException();
                    break;
            }
        }

        private void InterpretBranch(ref ILReader reader, ILOpcode opcode, int target)
        {
            switch (opcode)
            {
                case ILOpcode.br_s:
                case ILOpcode.br:
                    reader.Seek(target);
                    break;
                case ILOpcode.brfalse_s:
                case ILOpcode.brfalse:
                case ILOpcode.brtrue_s:
                case ILOpcode.brtrue:
                    {
                        bool value = default(bool);
                        StackItem stackItem = PopWithValidation();

                        switch (stackItem.Kind)
                        {
                            case StackValueKind.Int32:
                                value = stackItem.AsInt32() != 0;
                                break;
                            case StackValueKind.Int64:
                                value = stackItem.AsInt64() != 0;
                                break;
                            case StackValueKind.NativeInt:
                                value = stackItem.AsNativeInt() != IntPtr.Zero;
                                break;
                            case StackValueKind.ByRef:
                                // TODO: Add support for ByRef types
                                throw new NotImplementedException();
                            case StackValueKind.ObjRef:
                                value = !(stackItem.AsObjectRef() is null);
                                break;
                            case StackValueKind.Unknown:
                            case StackValueKind.Float:
                            case StackValueKind.ValueType:
                            default:
                                ThrowHelper.ThrowInvalidProgramException();
                                break;
                        }

                        if (value)
                        {
                            if (opcode == ILOpcode.brtrue_s || opcode == ILOpcode.brtrue)
                                reader.Seek(target);
                        }
                        else
                        {
                            if (opcode == ILOpcode.brfalse_s || opcode == ILOpcode.brfalse)
                                reader.Seek(target);
                        }
                    }
                    break;
                case ILOpcode.beq_s:
                case ILOpcode.beq:
                    {
                        InterpretCompareOperation(ILOpcode.ceq);
                        InterpretBranch(ref reader, ILOpcode.brtrue, target);
                    }
                    break;
                case ILOpcode.bge_s:
                case ILOpcode.bge:
                    {
                        InterpretCompareOperation(ILOpcode.clt);
                        InterpretBranch(ref reader, ILOpcode.brfalse, target);
                    }
                    break;
                case ILOpcode.bgt_s:
                case ILOpcode.bgt:
                    {
                        InterpretCompareOperation(ILOpcode.cgt);
                        InterpretBranch(ref reader, ILOpcode.brtrue, target);
                    }
                    break;
                case ILOpcode.ble_s:
                case ILOpcode.ble:
                    {
                        InterpretCompareOperation(ILOpcode.cgt);
                        InterpretBranch(ref reader, ILOpcode.brfalse, target);
                    }
                    break;
                case ILOpcode.blt_s:
                case ILOpcode.blt:
                    {
                        InterpretCompareOperation(ILOpcode.clt);
                        InterpretBranch(ref reader, ILOpcode.brtrue, target);
                    }
                    break;
                case ILOpcode.bne_un_s:
                case ILOpcode.bne_un:
                    {
                        InterpretCompareOperation(ILOpcode.ceq);
                        InterpretBranch(ref reader, ILOpcode.brfalse, target);
                    }
                    break;
                case ILOpcode.bge_un_s:
                case ILOpcode.bge_un:
                    {
                        InterpretCompareOperation(ILOpcode.clt_un);
                        InterpretBranch(ref reader, ILOpcode.brfalse, target);
                    }
                    break;
                case ILOpcode.bgt_un_s:
                case ILOpcode.bgt_un:
                    {
                        InterpretCompareOperation(ILOpcode.cgt_un);
                        InterpretBranch(ref reader, ILOpcode.brtrue, target);
                    }
                    break;
                case ILOpcode.ble_un_s:
                case ILOpcode.ble_un:
                    {
                        InterpretCompareOperation(ILOpcode.cgt_un);
                        InterpretBranch(ref reader, ILOpcode.brfalse, target);
                    }
                    break;
                case ILOpcode.blt_un_s:
                case ILOpcode.blt_un:
                    {
                        InterpretCompareOperation(ILOpcode.clt_un);
                        InterpretBranch(ref reader, ILOpcode.brtrue, target);
                    }
                    break;
                default:
                    Debug.Assert(false);
                    break;
            }
        }

        private void InterpretSwitch(ref ILReader reader, int jmpBase, int[] jmpDelta)
        {
            int value = PopWithValidation().AsInt32();
            for (int i = 0; i < jmpDelta.Length; i++)
            {
                if (value == i)
                    reader.Seek(jmpBase + jmpDelta[i]);
            }
        }

        private void InterpretNewArray(int token)
        {
            int length = 0;
            StackItem stackItem = PopWithValidation();

            switch (stackItem.Kind)
            {
                case StackValueKind.Int32:
                    length = stackItem.AsInt32();
                    break;
                case StackValueKind.NativeInt:
                    length = (int)stackItem.AsNativeInt();
                    break;
                default:
                    ThrowHelper.ThrowInvalidProgramException();
                    break;
            }

            Debug.Assert(length >= 0);

            TypeDesc arrayType = ((TypeDesc)_methodIL.GetObject(token)).MakeArrayType();

            // TODO: Add support for arbitrary  non-primitive types
            Array array = RuntimeAugments.NewArray(arrayType.GetRuntimeTypeHandle(), length);

            _stack.Push(StackItem.FromObjectRef(array));
        }

        private void InterpretLoadLength()
        {
            Array array = (Array)PopWithValidation().AsObjectRef();
            _stack.Push(StackItem.FromInt32(array.Length));
        }

        private void InterpretStoreElement(ILOpcode opcode)
        {
            StackItem valueItem = PopWithValidation();
            StackItem indexItem = PopWithValidation();
            Array array = (Array)PopWithValidation().AsObjectRef();

            int index = 0;
            switch (indexItem.Kind)
            {
                case StackValueKind.Int32:
                    index = indexItem.AsInt32();
                    break;
                case StackValueKind.NativeInt:
                    {
                        long value = (long)indexItem.AsNativeInt();
                        if ((int)value != value)
                            throw new IndexOutOfRangeException();
                        index = (int)value;
                    }
                    break;
                default:
                    ThrowHelper.ThrowInvalidProgramException();
                    break;
            }

            ref byte address = ref RuntimeAugments.GetSzArrayElementAddress(array, index);

            switch (opcode)
            {
                case ILOpcode.stelem_i:
                    Unsafe.Write(ref address, valueItem.AsNativeInt());
                    break;
                case ILOpcode.stelem_i1:
                    Unsafe.Write(ref address, (sbyte)valueItem.AsInt32());
                    break;
                case ILOpcode.stelem_i2:
                    Unsafe.Write(ref address, (short)valueItem.AsInt32());
                    break;
                case ILOpcode.stelem_i4:
                    Unsafe.Write(ref address, valueItem.AsInt32());
                    break;
                case ILOpcode.stelem_i8:
                    Unsafe.Write(ref address, valueItem.AsInt64());
                    break;
                case ILOpcode.stelem_r4:
                    Unsafe.Write(ref address, (float)valueItem.AsDouble());
                    break;
                case ILOpcode.stelem_r8:
                    Unsafe.Write(ref address, valueItem.AsDouble());
                    break;
                case ILOpcode.stelem_ref:
                    Unsafe.As<Object[]>(array)[index] = valueItem.AsObjectRef();
                    break;
                default:
                    Debug.Assert(false);
                    break;
            }
        }

        private void InterpretStoreElement(int token)
        {
            StackItem valueItem = PopWithValidation();
            StackItem indexItem = PopWithValidation();
            Array array = (Array)PopWithValidation().AsObjectRef();

            int index = 0;

            switch (indexItem.Kind)
            {
                case StackValueKind.Int32:
                    index = indexItem.AsInt32();
                    break;
                case StackValueKind.NativeInt:
                    {
                        long value = (long)indexItem.AsNativeInt();
                        if ((int)value != value)
                            throw new IndexOutOfRangeException();
                        index = (int)value;
                    }
                    break;
                default:
                    ThrowHelper.ThrowInvalidProgramException();
                    break;
            }

            TypeDesc elementType = (TypeDesc)_methodIL.GetObject(token);
            ref byte address = ref RuntimeAugments.GetSzArrayElementAddress(array, index);

        again:
            switch (elementType.Category)
            {
                case TypeFlags.Boolean:
                    Unsafe.Write(ref address, valueItem.AsInt32() != 0);
                    break;
                case TypeFlags.Char:
                    Unsafe.Write(ref address, (char)valueItem.AsInt32());
                    break;
                case TypeFlags.SByte:
                case TypeFlags.Byte:
                    Unsafe.Write(ref address, (sbyte)valueItem.AsInt32());
                    break;
                case TypeFlags.Int16:
                case TypeFlags.UInt16:
                    Unsafe.Write(ref address, (short)valueItem.AsInt32());
                    break;
                case TypeFlags.Int32:
                case TypeFlags.UInt32:
                    Unsafe.Write(ref address, valueItem.AsInt32());
                    break;
                case TypeFlags.Int64:
                case TypeFlags.UInt64:
                    Unsafe.Write(ref address, valueItem.AsInt64());
                    break;
                case TypeFlags.IntPtr:
                case TypeFlags.UIntPtr:
                    Unsafe.Write(ref address, valueItem.AsNativeInt());
                    break;
                case TypeFlags.Single:
                    Unsafe.Write(ref address, (float)valueItem.AsDouble());
                    break;
                case TypeFlags.Double:
                    Unsafe.Write(ref address, valueItem.AsDouble());
                    break;
                case TypeFlags.ValueType:
                case TypeFlags.Nullable:
                    Unsafe.Write(ref address, valueItem.AsValueType());
                    break;
                case TypeFlags.Enum:
                    elementType = elementType.UnderlyingType;
                    goto again;
                case TypeFlags.Class:
                case TypeFlags.Interface:
                case TypeFlags.Array:
                case TypeFlags.SzArray:
                    Unsafe.As<Object[]>(array)[index] = valueItem.AsObjectRef();
                    break;
                default:
                    // TODO: Support more complex return types
                    throw new NotImplementedException();
            }
        }

        private void InterpretLoadElement(ILOpcode opcode)
        {
            StackItem indexItem = PopWithValidation();
            Array array = (Array)PopWithValidation().AsObjectRef();

            int index = 0;
            switch (indexItem.Kind)
            {
                case StackValueKind.Int32:
                    index = indexItem.AsInt32();
                    break;
                case StackValueKind.NativeInt:
                    {
                        long value = (long)indexItem.AsNativeInt();
                        if ((int)value != value)
                            throw new IndexOutOfRangeException();
                        index = (int)value;
                    }
                    break;
                default:
                    ThrowHelper.ThrowInvalidProgramException();
                    break;
            }

            ref byte address = ref RuntimeAugments.GetSzArrayElementAddress(array, index);

            switch (opcode)
            {
                case ILOpcode.ldelem_i1:
                    _stack.Push(StackItem.FromInt32(Unsafe.Read<sbyte>(ref address)));
                    break;
                case ILOpcode.ldelem_u1:
                    _stack.Push(StackItem.FromInt32(Unsafe.Read<byte>(ref address)));
                    break;
                case ILOpcode.ldelem_i2:
                    _stack.Push(StackItem.FromInt32(Unsafe.Read<short>(ref address)));
                    break;
                case ILOpcode.ldelem_u2:
                    _stack.Push(StackItem.FromInt32(Unsafe.Read<ushort>(ref address)));
                    break;
                case ILOpcode.ldelem_i4:
                    _stack.Push(StackItem.FromInt32(Unsafe.Read<int>(ref address)));
                    break;
                case ILOpcode.ldelem_u4:
                    _stack.Push(StackItem.FromInt32((int)Unsafe.Read<uint>(ref address)));
                    break;
                case ILOpcode.ldelem_i8:
                    _stack.Push(StackItem.FromInt64(Unsafe.Read<long>(ref address)));
                    break;
                case ILOpcode.ldelem_i:
                    _stack.Push(StackItem.FromNativeInt(Unsafe.Read<IntPtr>(ref address)));
                    break;
                case ILOpcode.ldelem_r4:
                    _stack.Push(StackItem.FromDouble(Unsafe.Read<float>(ref address)));
                    break;
                case ILOpcode.ldelem_r8:
                    _stack.Push(StackItem.FromDouble(Unsafe.Read<double>(ref address)));
                    break;
                case ILOpcode.ldelem_ref:
                    _stack.Push(StackItem.FromObjectRef(Unsafe.Read<object>(ref address)));
                    break;
                default:
                    Debug.Assert(false);
                    break;
            }
        }

        private void InterpretLoadElement(int token)
        {
            StackItem indexItem = PopWithValidation();
            Array array = (Array)PopWithValidation().AsObjectRef();

            int index = 0;
            switch (indexItem.Kind)
            {
                case StackValueKind.Int32:
                    index = indexItem.AsInt32();
                    break;
                case StackValueKind.NativeInt:
                    {
                        long value = (long)indexItem.AsNativeInt();
                        if ((int)value != value)
                            throw new IndexOutOfRangeException();
                        index = (int)value;
                    }
                    break;
                default:
                    ThrowHelper.ThrowInvalidProgramException();
                    break;
            }

            TypeDesc elementType = (TypeDesc)_methodIL.GetObject(token);
            ref byte address = ref RuntimeAugments.GetSzArrayElementAddress(array, index);

        again:
            switch (elementType.Category)
            {
                case TypeFlags.Boolean:
                    _stack.Push(StackItem.FromInt32(Unsafe.Read<bool>(ref address) ? 1 : 0));
                    break;
                case TypeFlags.Char:
                    _stack.Push(StackItem.FromInt32(Unsafe.Read<char>(ref address)));
                    break;
                case TypeFlags.SByte:
                case TypeFlags.Byte:
                    _stack.Push(StackItem.FromInt32(Unsafe.Read<sbyte>(ref address)));
                    break;
                case TypeFlags.Int16:
                case TypeFlags.UInt16:
                    _stack.Push(StackItem.FromInt32(Unsafe.Read<short>(ref address)));
                    break;
                case TypeFlags.Int32:
                case TypeFlags.UInt32:
                    _stack.Push(StackItem.FromInt32(Unsafe.Read<int>(ref address)));
                    break;
                case TypeFlags.Int64:
                case TypeFlags.UInt64:
                    _stack.Push(StackItem.FromInt64(Unsafe.Read<long>(ref address)));
                    break;
                case TypeFlags.IntPtr:
                case TypeFlags.UIntPtr:
                    _stack.Push(StackItem.FromNativeInt(Unsafe.Read<IntPtr>(ref address)));
                    break;
                case TypeFlags.Single:
                    _stack.Push(StackItem.FromDouble(Unsafe.Read<float>(ref address)));
                    break;
                case TypeFlags.Double:
                    _stack.Push(StackItem.FromDouble(Unsafe.Read<double>(ref address)));
                    break;
                case TypeFlags.ValueType:
                case TypeFlags.Nullable:
                    _stack.Push(StackItem.FromValueType(Unsafe.Read<ValueType>(ref address)));
                    break;
                case TypeFlags.Enum:
                    elementType = elementType.UnderlyingType;
                    goto again;
                case TypeFlags.Class:
                case TypeFlags.Interface:
                case TypeFlags.Array:
                case TypeFlags.SzArray:
                    _stack.Push(StackItem.FromObjectRef(Unsafe.Read<object>(ref address)));
                    break;
                default:
                    // TODO: Support more complex return types
                    throw new NotImplementedException();
            }
        }
    }
}
