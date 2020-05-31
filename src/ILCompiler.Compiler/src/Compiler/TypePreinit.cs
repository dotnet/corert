﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using ILCompiler.DependencyAnalysis;

using Internal.IL;
using Internal.TypeSystem;

namespace ILCompiler
{
    // Class that computes the initial state of static fields on a type by interpreting the static constructor.
    // 
    // Values are represented by instances of an abstract Value class. Several specialized descendants of
    // the Value class exist, representing value types (including e.g. a specialized class representing
    // RuntimeFieldHandle), or reference types (including e.g. specialized class representing an array).
    //
    // For simplicity, non-reference values are represented as byte arrays. This requires many short lived array
    // allocations, but makes a lot of things simpler (e.g. byrefs to values are essentially free because they
    // only carry a reference to the original array and an optional index).
    //
    // When dealing with non-reference types (valuetypes and unmanaged pointers) we need to be careful
    // about assignment semantics. Some operations need to make a copy of the valuetype bytes while others
    // are fine to reuse the original byte array. Whenever storing a value into a location, we need to assign
    // a new value to the existing Value instance to keep byrefs working.
    public class TypePreinit
    {
        private readonly MetadataType _type;
        private readonly CompilationModuleGroup _compilationGroup;
        private readonly ILProvider _ilProvider;
        private readonly Dictionary<FieldDesc, Value> _fieldValues = new Dictionary<FieldDesc, Value>();
        private readonly Dictionary<string, StringInstance> _internedStrings = new Dictionary<string, StringInstance>();

        private TypePreinit(MetadataType owningType, CompilationModuleGroup compilationGroup, ILProvider ilProvider)
        {
            _type = owningType;
            _compilationGroup = compilationGroup;
            _ilProvider = ilProvider;

            // Zero initialize all fields we model.
            foreach (var field in owningType.GetFields())
            {
                if (!field.IsStatic || field.IsLiteral || field.IsThreadStatic || field.HasRva)
                    continue;

               _fieldValues.Add(field, NewUninitializedLocationValue(field.FieldType));
            }
        }

        public static PreinitializationInfo ScanType(CompilationModuleGroup compilationGroup, ILProvider ilProvider, MetadataType type)
        {
            Debug.Assert(type.HasStaticConstructor);
            Debug.Assert(!type.IsGenericDefinition);
            Debug.Assert(!type.IsCanonicalSubtype(CanonicalFormKind.Any));

            var preinit = new TypePreinit(type, compilationGroup, ilProvider);

            Status status;
            try
            {
                status = preinit.TryScanMethod(type.GetStaticConstructor(), null, null, out _);
            }
            catch (TypeSystemException ex)
            {
                status = Status.Fail(type.GetStaticConstructor(), ex.Message);
            }

            if (status.IsSuccessful)
            {
                var values = new List<KeyValuePair<FieldDesc, ISerializableValue>>();
                foreach (var kvp in preinit._fieldValues)
                    values.Add(new KeyValuePair<FieldDesc, ISerializableValue>(kvp.Key, kvp.Value));

                return new PreinitializationInfo(type, values);
            }

            return new PreinitializationInfo(type, status.FailureReason);
        }

        private Status TryScanMethod(MethodDesc method, Value[] parameters, Stack<MethodDesc> recursionProtect, out Value returnValue)
        {
            MethodIL methodIL = _ilProvider.GetMethodIL(method);
            if (methodIL == null)
            {
                returnValue = null;
                return Status.Fail(method, "Extern method");
            }

            return TryScanMethod(methodIL, parameters, recursionProtect, out returnValue);
        }

        private Status TryScanMethod(MethodIL methodIL, Value[] parameters, Stack<MethodDesc> recursionProtect, out Value returnValue)
        {
            returnValue = default;

            if (recursionProtect != null && recursionProtect.Contains(methodIL.OwningMethod))
                return Status.Fail(methodIL.OwningMethod, "Recursion");

            ILExceptionRegion[] ehRegions = methodIL.GetExceptionRegions();
            if (ehRegions != null && ehRegions.Length > 0)
            {
                // We don't care about catch/filter/fault because those only run when an exception happens
                // (exceptions will never happen here). But finally needs to run in non-exceptional paths
                // and we don't model that yet.
                foreach (ILExceptionRegion ehRegion in ehRegions)
                {
                    if (ehRegion.Kind == ILExceptionRegionKind.Finally)
                        return Status.Fail(methodIL.OwningMethod, "Finally regions");
                }
            }

            var reader = new ILReader(methodIL.GetILBytes());

            TypeSystemContext context = methodIL.OwningMethod.Context;

            var stack = new Stack(methodIL.MaxStack, context.Target);

            LocalVariableDefinition[] localTypes = methodIL.GetLocals();
            Value[] locals = new Value[localTypes.Length];
            for (int i = 0; i < localTypes.Length; i++)
            {
                locals[i] = NewUninitializedLocationValue(localTypes[i].Type);
            }

            // Read IL opcodes and interpret their semantics.
            //
            // This is not a full interpreter and we're allowed to not interpret everything. If a semantic is
            // not implemented by the interpreter, we simply fail.
            //
            // We also need to do basic sanity checking for invalid IL to protect us from crashing. These
            // all throw the TypeSystem's InvalidProgramException. The exception doesn't need to exactly match
            // the runtime exception. We just need something reasonably catchable to abort interpreting.
            //
            // We throw instead of returning false to aid debuggability of the interpreter (we shouldn't see
            // exceptions in normal code so an exception is usually a bug).

            while (reader.HasNext)
            {
                ILOpcode opcode = reader.ReadILOpcode();
                switch (opcode)
                {
                    case ILOpcode.ldc_i4_m1:
                    case ILOpcode.ldc_i4_s:
                    case ILOpcode.ldc_i4:
                    case ILOpcode.ldc_i4_0:
                    case ILOpcode.ldc_i4_1:
                    case ILOpcode.ldc_i4_2:
                    case ILOpcode.ldc_i4_3:
                    case ILOpcode.ldc_i4_4:
                    case ILOpcode.ldc_i4_5:
                    case ILOpcode.ldc_i4_6:
                    case ILOpcode.ldc_i4_7:
                    case ILOpcode.ldc_i4_8:
                        {
                            int value = opcode switch
                            {
                                ILOpcode.ldc_i4_m1 => -1,
                                ILOpcode.ldc_i4_s => (sbyte)reader.ReadILByte(),
                                ILOpcode.ldc_i4 => (int)reader.ReadILUInt32(),
                                _ => opcode - ILOpcode.ldc_i4_0,
                            };
                            stack.Push(StackValueKind.Int32, ValueTypeValue.FromInt32(value));
                        }
                        break;
                    
                    case ILOpcode.ldc_i8:
                        stack.Push(StackValueKind.Int64, ValueTypeValue.FromInt64((long)reader.ReadILUInt64()));
                        break;

                    case ILOpcode.ldc_r4:
                    case ILOpcode.ldc_r8:
                        stack.Push(StackValueKind.Float, ValueTypeValue.FromDouble(
                            opcode == ILOpcode.ldc_r4 ? reader.ReadILFloat() : reader.ReadILDouble()));
                        break;

                    case ILOpcode.sizeof_:
                        {
                            TypeDesc type = (TypeDesc)methodIL.GetObject(reader.ReadILToken());
                            stack.Push(StackValueKind.Int32, ValueTypeValue.FromInt32(type.GetElementSize().AsInt));
                        }
                        break;

                    case ILOpcode.ldnull:
                        stack.Push((ReferenceTypeValue)null);
                        break;

                    case ILOpcode.newarr:
                        {
                            if (!stack.TryPopIntValue(out int elementCount))
                            {
                                ThrowHelper.ThrowInvalidProgramException();
                            }

                            const int MaximumInterpretedArraySize = 8192;

                            TypeDesc elementType = (TypeDesc)methodIL.GetObject(reader.ReadILToken());
                            if (elementCount > 0
                                && (elementType.IsGCPointer
                                || (elementType.IsValueType && ((DefType)elementType).ContainsGCPointers)))
                            {
                                return Status.Fail(methodIL.OwningMethod, opcode, "GC pointers");
                            }

                            if (elementCount < 0
                                || elementCount > MaximumInterpretedArraySize)
                            {
                                return Status.Fail(methodIL.OwningMethod, opcode, "Array out of bounds");
                            }

                            stack.Push(new ArrayInstance(elementType.MakeArrayType(), elementCount));
                        }
                        break;

                    case ILOpcode.dup:
                        if (stack.Count == 0)
                        {
                            ThrowHelper.ThrowInvalidProgramException();
                        }
                        stack.Push(stack.Peek());
                        break;

                    case ILOpcode.ldstr:
                        {
                            string s = (string)methodIL.GetObject(reader.ReadILToken());
                            if (!_internedStrings.TryGetValue(s, out StringInstance instance))
                            {
                                instance = new StringInstance(context.GetWellKnownType(WellKnownType.String), s);
                                _internedStrings.Add(s, instance);
                            }
                            stack.Push(instance);
                        }
                        break;

                    case ILOpcode.ret:
                        {
                            bool returnsVoid = methodIL.OwningMethod.Signature.ReturnType.IsVoid;
                            if ((returnsVoid && stack.Count > 0)
                                || (!returnsVoid && stack.Count != 1))
                            {
                                ThrowHelper.ThrowInvalidProgramException();
                            }

                            if (!returnsVoid)
                            {
                                returnValue = stack.PopIntoLocation(methodIL.OwningMethod.Signature.ReturnType);
                            }
                            return Status.Success;
                        }

                    case ILOpcode.nop:
                    case ILOpcode.volatile_:
                        break;

                    case ILOpcode.stsfld:
                        {
                            FieldDesc field = (FieldDesc)methodIL.GetObject(reader.ReadILToken());
                            if (!field.IsStatic || field.IsLiteral)
                            {
                                ThrowHelper.ThrowInvalidProgramException();
                            }

                            if (field.OwningType != _type)
                            {
                                return Status.Fail(methodIL.OwningMethod, opcode, "Store into other static");
                            }

                            if (field.IsThreadStatic || field.HasRva)
                            {
                                return Status.Fail(methodIL.OwningMethod, opcode, "Unsupported static");
                            }

                            if (_fieldValues[field] is IAssignableValue assignableField)
                                assignableField.Assign(stack.PopIntoLocation(field.FieldType));
                            else
                                _fieldValues[field] = stack.PopIntoLocation(field.FieldType);
                        }
                        break;

                    case ILOpcode.ldsfld:
                        {
                            FieldDesc field = (FieldDesc)methodIL.GetObject(reader.ReadILToken());
                            if (!field.IsStatic || field.IsLiteral)
                            {
                                ThrowHelper.ThrowInvalidProgramException();
                            }

                            if (field.OwningType != _type)
                            {
                                return Status.Fail(methodIL.OwningMethod, opcode, "Load from other static" + (field.IsInitOnly ? " initonly " : ""));
                            }

                            if (field.IsThreadStatic || field.HasRva)
                            {
                                return Status.Fail(methodIL.OwningMethod, opcode, "Unsupported static");
                            }

                            stack.PushFromLocation(field.FieldType, _fieldValues[field]);
                        }
                        break;

                    case ILOpcode.ldsflda:
                        {
                            FieldDesc field = (FieldDesc)methodIL.GetObject(reader.ReadILToken());
                            if (!field.IsStatic || field.IsLiteral)
                            {
                                ThrowHelper.ThrowInvalidProgramException();
                            }

                            if (field.OwningType != _type)
                            {
                                return Status.Fail(methodIL.OwningMethod, opcode, "Address of other static");
                            }

                            if (field.IsThreadStatic || field.HasRva)
                            {
                                return Status.Fail(methodIL.OwningMethod, opcode, "Unsupported static");
                            }

                            if (!(_fieldValues[field] is ValueTypeValue vtfield))
                            {
                                return Status.Fail(methodIL.OwningMethod, opcode, "Unsupported byref");
                            }

                            stack.Push(vtfield.CreateByRef());
                        }
                        break;

                    case ILOpcode.call:
                        {
                            MethodDesc method = (MethodDesc)methodIL.GetObject(reader.ReadILToken());
                            MethodSignature methodSig = method.Signature;
                            int paramOffset = methodSig.IsStatic ? 0 : 1;
                            int numParams = methodSig.Length + paramOffset;

                            TypeDesc owningType = method.OwningType;
                            if (!_compilationGroup.CanInline(methodIL.OwningMethod, method))
                            {
                                return Status.Fail(methodIL.OwningMethod, opcode, "Cannot inline");
                            }

                            if (owningType.HasStaticConstructor
                                    && owningType != methodIL.OwningMethod.OwningType
                                    && !((MetadataType)owningType).IsBeforeFieldInit)
                            {
                                return Status.Fail(methodIL.OwningMethod, opcode, "Static constructor");
                            }

                            Value[] methodParams = new Value[numParams];
                            for (int i = numParams - 1; i >= 0; i--)
                            {
                                methodParams[i] = stack.PopIntoLocation(GetArgType(method, i));
                            }

                            Value retVal;
                            if (!method.IsIntrinsic || !TryHandleIntrinsicCall(method, methodParams, out retVal))
                            {
                                recursionProtect ??= new Stack<MethodDesc>();
                                recursionProtect.Push(methodIL.OwningMethod);
                                Status callResult = TryScanMethod(method, methodParams, recursionProtect, out retVal);
                                if (!callResult.IsSuccessful)
                                {
                                    recursionProtect.Pop();
                                    return callResult;
                                }
                                recursionProtect.Pop();
                            }

                            if (!methodSig.ReturnType.IsVoid)
                                stack.PushFromLocation(methodSig.ReturnType, retVal);
                        }
                        break;

                    case ILOpcode.newobj:
                        {
                            MethodDesc ctor = (MethodDesc)methodIL.GetObject(reader.ReadILToken());
                            MethodSignature ctorSig = ctor.Signature;

                            TypeDesc owningType = ctor.OwningType;
                            if (!_compilationGroup.CanInline(methodIL.OwningMethod, ctor)
                                || !_compilationGroup.ContainsType(owningType))
                            {
                                return Status.Fail(methodIL.OwningMethod, opcode, "Cannot inline");
                            }

                            if (owningType.HasStaticConstructor
                                    && owningType != methodIL.OwningMethod.OwningType
                                    && !((MetadataType)owningType).IsBeforeFieldInit)
                            {
                                return Status.Fail(methodIL.OwningMethod, opcode, "Static constructor");
                            }

                            if (!owningType.IsDefType)
                            {
                                return Status.Fail(methodIL.OwningMethod, opcode, "Not a class or struct");
                            }

                            if (owningType.HasFinalizer)
                            {
                                // Finalizer might have observable side effects
                                return Status.Fail(methodIL.OwningMethod, opcode, "Finalizable class");
                            }

                            if (((DefType)owningType).ContainsGCPointers)
                            {
                                // We don't want to end up with GC pointers in the frozen region
                                // because write barriers can't handle that.
                                return Status.Fail(methodIL.OwningMethod, opcode, "GC pointers");
                            }

                            Value instance;
                            Value ctorArg0;
                            if (owningType.IsValueType)
                            {
                                instance = new ValueTypeValue(owningType);
                                ctorArg0 = ((ValueTypeValue)instance).CreateByRef();
                            }
                            else
                            {
                                instance = new ObjectInstance((DefType)owningType);
                                ctorArg0 = instance;
                            }

                            Value[] ctorParameters = new Value[ctorSig.Length + 1];
                            ctorParameters[0] = ctorArg0;
                            for (int i = ctorSig.Length - 1; i >= 0; i--)
                            {
                                ctorParameters[i + 1] = stack.PopIntoLocation(GetArgType(ctor, i + 1));
                            }
                            recursionProtect ??= new Stack<MethodDesc>();
                            recursionProtect.Push(methodIL.OwningMethod);
                            Status ctorCallResult = TryScanMethod(ctor, ctorParameters, recursionProtect, out _);
                            if (!ctorCallResult.IsSuccessful)
                            {
                                recursionProtect.Pop();
                                return ctorCallResult;
                            }
                                    
                            recursionProtect.Pop();

                            stack.PushFromLocation(owningType, instance);
                        }
                        break;

                    case ILOpcode.stfld:
                        {
                            FieldDesc field = (FieldDesc)methodIL.GetObject(reader.ReadILToken());

                            if (field.FieldType.IsGCPointer
                                || field.IsStatic)
                            {
                                return Status.Fail(methodIL.OwningMethod, opcode, "Reference field");
                            }

                            Value value = stack.PopIntoLocation(field.FieldType);
                            StackEntry instance = stack.Pop();

                            var settableInstance = instance.Value as IHasInstanceFields;
                            if (settableInstance == null)
                            {
                                return Status.Fail(methodIL.OwningMethod, opcode);
                            }

                            settableInstance.SetField(field, value);
                        }
                        break;

                    case ILOpcode.ldfld:
                        {
                            FieldDesc field = (FieldDesc)methodIL.GetObject(reader.ReadILToken());

                            if (field.FieldType.IsGCPointer
                                || field.IsStatic)
                            {
                                return Status.Fail(methodIL.OwningMethod, opcode);
                            }

                            StackEntry instance = stack.Pop();

                            var loadableInstance = instance.Value as IHasInstanceFields;
                            if (loadableInstance == null)
                            {
                                return Status.Fail(methodIL.OwningMethod, opcode);
                            }

                            Value fieldValue = loadableInstance.GetField(field);

                            stack.PushFromLocation(field.FieldType, fieldValue);
                        }
                        break;

                    case ILOpcode.ldflda:
                        {
                            FieldDesc field = (FieldDesc)methodIL.GetObject(reader.ReadILToken());
                            if (field.FieldType.IsGCPointer
                                || field.IsStatic)
                            {
                                return Status.Fail(methodIL.OwningMethod, opcode);
                            }

                            StackEntry instance = stack.Pop();

                            var loadableInstance = instance.Value as IHasInstanceFields;
                            if (loadableInstance == null)
                            {
                                return Status.Fail(methodIL.OwningMethod, opcode);
                            }

                            stack.Push(loadableInstance.GetFieldAddress(field));
                        }
                        break;

                    case ILOpcode.conv_i:
                    case ILOpcode.conv_u:
                    case ILOpcode.conv_i2:
                    case ILOpcode.conv_i4:
                    case ILOpcode.conv_i8:
                    case ILOpcode.conv_u2:
                    case ILOpcode.conv_u8:
                        {
                            StackEntry popped = stack.Pop();
                            if (popped.ValueKind == StackValueKind.Int32)
                            {
                                int val = popped.Value.AsInt32();
                                switch (opcode)
                                {
                                    case ILOpcode.conv_i:
                                        stack.Push(StackValueKind.NativeInt,
                                            context.Target.PointerSize == 8 ? ValueTypeValue.FromInt64(val) : ValueTypeValue.FromInt32(val));
                                        break;
                                    case ILOpcode.conv_u:
                                        stack.Push(StackValueKind.NativeInt,
                                            context.Target.PointerSize == 8 ? ValueTypeValue.FromInt64((uint)val) : ValueTypeValue.FromInt32(val));
                                        break;
                                    case ILOpcode.conv_i2:
                                        stack.Push(StackValueKind.Int32, ValueTypeValue.FromInt32((short)val));
                                        break;
                                    case ILOpcode.conv_i8:
                                        stack.Push(StackValueKind.Int64, ValueTypeValue.FromInt64(val));
                                        break;
                                    case ILOpcode.conv_u2:
                                        stack.Push(StackValueKind.Int32, ValueTypeValue.FromInt32((ushort)val));
                                        break;
                                    case ILOpcode.conv_u8:
                                        stack.Push(StackValueKind.Int64, ValueTypeValue.FromInt64((uint)val));
                                        break;
                                    default:
                                        return Status.Fail(methodIL.OwningMethod, opcode);
                                }
                            }
                            else if (popped.ValueKind == StackValueKind.NativeInt)
                            {
                                long val = context.Target.PointerSize == 8 ? popped.Value.AsInt64() : popped.Value.AsInt32();
                                switch (opcode)
                                {
                                    case ILOpcode.conv_i4:
                                        stack.Push(StackValueKind.Int32, ValueTypeValue.FromInt32((int)val));
                                        break;
                                    default:
                                        return Status.Fail(methodIL.OwningMethod, opcode);
                                }
                            }
                            else if (popped.ValueKind == StackValueKind.Int64)
                            {
                                long val = popped.Value.AsInt64();
                                switch (opcode)
                                {
                                    case ILOpcode.conv_u:
                                        stack.Push(StackValueKind.NativeInt,
                                            context.Target.PointerSize == 8 ? ValueTypeValue.FromInt64(val) : ValueTypeValue.FromInt32((int)val));
                                        break;
                                }
                            }
                            else if (popped.ValueKind == StackValueKind.Float)
                            {
                                double val = popped.Value.AsDouble();
                                switch (opcode)
                                {
                                    case ILOpcode.conv_i8:
                                        stack.Push(StackValueKind.Int64, ValueTypeValue.FromInt64((long)val));
                                        break;
                                    default:
                                        return Status.Fail(methodIL.OwningMethod, opcode);
                                }
                            }
                            else
                            {
                                return Status.Fail(methodIL.OwningMethod, opcode);
                            }
                        }
                        break;

                    case ILOpcode.ldarg_0:
                    case ILOpcode.ldarg_1:
                    case ILOpcode.ldarg_2:
                    case ILOpcode.ldarg_3:
                    case ILOpcode.ldarg_s:
                    case ILOpcode.ldarg:
                        {
                            int index = opcode switch
                            {
                                ILOpcode.ldarg_s => reader.ReadILByte(),
                                ILOpcode.ldarg => reader.ReadILUInt16(),
                                _ => opcode - ILOpcode.ldarg_0,
                            };
                            stack.PushFromLocation(GetArgType(methodIL.OwningMethod, index), parameters[index]);
                        }
                        break;

                    case ILOpcode.starg_s:
                    case ILOpcode.starg:
                        {
                            int index = opcode == ILOpcode.starg ? reader.ReadILUInt16() : reader.ReadILByte();
                            TypeDesc argType = GetArgType(methodIL.OwningMethod, index);
                            if (parameters[index] is IAssignableValue assignableParam)
                                assignableParam.Assign(stack.PopIntoLocation(argType));
                            else
                                parameters[index] = stack.PopIntoLocation(argType);
                        }
                        break;

                    case ILOpcode.ldtoken:
                        {
                            var token = methodIL.GetObject(reader.ReadILToken());
                            if (!(token is FieldDesc field))
                            {
                                return Status.Fail(methodIL.OwningMethod, opcode);
                            }

                            stack.Push(new StackEntry(StackValueKind.ValueType, new RuntimeFieldHandleValue(field)));
                        }
                        break;

                    case ILOpcode.ldloc_0:
                    case ILOpcode.ldloc_1:
                    case ILOpcode.ldloc_2:
                    case ILOpcode.ldloc_3:
                    case ILOpcode.ldloc_s:
                    case ILOpcode.ldloc:
                        {
                            int index = opcode switch
                            {
                                ILOpcode.ldloc_s => reader.ReadILByte(),
                                ILOpcode.ldloc => reader.ReadILUInt16(),
                                _ => opcode - ILOpcode.ldloc_0,
                            };

                            if (index >= locals.Length)
                            {
                                ThrowHelper.ThrowInvalidProgramException();
                            }

                            stack.PushFromLocation(localTypes[index].Type, locals[index]);
                        }
                        break;

                    case ILOpcode.stloc_0:
                    case ILOpcode.stloc_1:
                    case ILOpcode.stloc_2:
                    case ILOpcode.stloc_3:
                    case ILOpcode.stloc_s:
                    case ILOpcode.stloc:
                        {
                            int index = opcode switch
                            {
                                ILOpcode.stloc_s => reader.ReadILByte(),
                                ILOpcode.stloc => reader.ReadILUInt16(),
                                _ => opcode - ILOpcode.stloc_0,
                            };

                            if (index >= locals.Length)
                            {
                                ThrowHelper.ThrowInvalidProgramException();
                            }

                            TypeDesc localType = localTypes[index].Type;
                            if (locals[index] is IAssignableValue assignableLocal)
                                assignableLocal.Assign(stack.PopIntoLocation(localType));
                            else
                                locals[index] = stack.PopIntoLocation(localType);
                                
                        }
                        break;

                    case ILOpcode.ldloca_s:
                    case ILOpcode.ldloca:
                        {
                            int index = opcode switch
                            {
                                ILOpcode.ldloca_s => reader.ReadILByte(),
                                ILOpcode.ldloca => reader.ReadILUInt16(),
                                _ => throw new NotImplementedException(), // Unreachable
                            };

                            if (index >= locals.Length)
                            {
                                ThrowHelper.ThrowInvalidProgramException();
                            }

                            if (locals[index] is ValueTypeValue vtvalue)
                            {
                                stack.Push(vtvalue.CreateByRef());
                            }
                            else
                            {
                                return Status.Fail(methodIL.OwningMethod, opcode);
                            }
                        }
                        break;

                    case ILOpcode.initobj:
                        {
                            StackEntry popped = stack.Pop();
                            if (popped.ValueKind != StackValueKind.ByRef)
                            {
                                ThrowHelper.ThrowInvalidProgramException();
                            }

                            TypeDesc token = (TypeDesc)methodIL.GetObject(reader.ReadILToken());
                            if (token.IsGCPointer)
                            {
                                return Status.Fail(methodIL.OwningMethod, opcode);
                            }
                            ((ByRefValue)popped.Value).Initialize(token.GetElementSize().AsInt);
                        }
                        break;

                    case ILOpcode.br:
                    case ILOpcode.brfalse:
                    case ILOpcode.brtrue:
                    case ILOpcode.blt:
                    case ILOpcode.blt_un:
                    case ILOpcode.bgt:
                    case ILOpcode.bgt_un:
                    case ILOpcode.beq:
                    case ILOpcode.bne_un:
                    case ILOpcode.bge:
                    case ILOpcode.bge_un:
                    case ILOpcode.ble:
                    case ILOpcode.ble_un:
                    case ILOpcode.br_s:
                    case ILOpcode.brfalse_s:
                    case ILOpcode.brtrue_s:
                    case ILOpcode.blt_s:
                    case ILOpcode.blt_un_s:
                    case ILOpcode.bgt_s:
                    case ILOpcode.bgt_un_s:
                    case ILOpcode.beq_s:
                    case ILOpcode.bne_un_s:
                    case ILOpcode.bge_s:
                    case ILOpcode.bge_un_s:
                    case ILOpcode.ble_s:
                    case ILOpcode.ble_un_s:
                        {
                            int delta = opcode >= ILOpcode.br ?
                                (int)reader.ReadILUInt32() :
                                (sbyte)reader.ReadILByte();
                            int target = reader.Offset + delta;
                            if (target < 0
                                || target > reader.Size)
                            {
                                ThrowHelper.ThrowInvalidProgramException();
                            }

                            ILOpcode normalizedOpcode = opcode >= ILOpcode.br ?
                                opcode - ILOpcode.br + ILOpcode.br_s:
                                opcode;

                            bool branchTaken;
                            if (normalizedOpcode == ILOpcode.brtrue_s || normalizedOpcode == ILOpcode.brfalse_s)
                            {
                                StackEntry condition = stack.Pop();
                                if (condition.ValueKind == StackValueKind.Int32 || (condition.ValueKind == StackValueKind.NativeInt && context.Target.PointerSize == 4))
                                    branchTaken = normalizedOpcode == ILOpcode.brfalse_s
                                        ? condition.Value.AsInt32() == 0 : condition.Value.AsInt32() != 0;
                                else if (condition.ValueKind == StackValueKind.Int64 || (condition.ValueKind == StackValueKind.NativeInt && context.Target.PointerSize == 8))
                                    branchTaken = normalizedOpcode == ILOpcode.brfalse_s
                                        ? condition.Value.AsInt64() == 0 : condition.Value.AsInt64() != 0;
                                else if (condition.ValueKind == StackValueKind.ObjRef)
                                    branchTaken = normalizedOpcode == ILOpcode.brfalse_s
                                        ? condition.Value == null : condition.Value != null;
                                else
                                    return Status.Fail(methodIL.OwningMethod, opcode);
                            }
                            else if (normalizedOpcode == ILOpcode.blt_s || normalizedOpcode == ILOpcode.bgt_s
                                || normalizedOpcode == ILOpcode.bge_s || normalizedOpcode == ILOpcode.beq_s
                                || normalizedOpcode == ILOpcode.ble_s || normalizedOpcode == ILOpcode.blt_un_s
                                || normalizedOpcode == ILOpcode.ble_un_s || normalizedOpcode == ILOpcode.bge_un_s
                                || normalizedOpcode == ILOpcode.bgt_un_s || normalizedOpcode == ILOpcode.bne_un_s)
                            {
                                StackEntry value2 = stack.Pop();
                                StackEntry value1 = stack.Pop();

                                if (value1.ValueKind == StackValueKind.Int32 && value2.ValueKind == StackValueKind.Int32)
                                {
                                    branchTaken = normalizedOpcode switch
                                    {
                                        ILOpcode.blt_s => value1.Value.AsInt32() < value2.Value.AsInt32(),
                                        ILOpcode.blt_un_s => (uint)value1.Value.AsInt32() < (uint)value2.Value.AsInt32(),
                                        ILOpcode.bgt_s => value1.Value.AsInt32() > value2.Value.AsInt32(),
                                        ILOpcode.bgt_un_s => (uint)value1.Value.AsInt32() > (uint)value2.Value.AsInt32(),
                                        ILOpcode.bge_s => value1.Value.AsInt32() >= value2.Value.AsInt32(),
                                        ILOpcode.bge_un_s => (uint)value1.Value.AsInt32() >= (uint)value2.Value.AsInt32(),
                                        ILOpcode.beq_s => value1.Value.AsInt32() == value2.Value.AsInt32(),
                                        ILOpcode.bne_un_s => value1.Value.AsInt32() != value2.Value.AsInt32(),
                                        ILOpcode.ble_s => value1.Value.AsInt32() <= value2.Value.AsInt32(),
                                        ILOpcode.ble_un_s => (uint)value1.Value.AsInt32() <= (uint)value2.Value.AsInt32(),
                                        _ => throw new NotImplementedException() // unreachable
                                    };
                                }
                                else if (value1.ValueKind == StackValueKind.Int64 && value2.ValueKind == StackValueKind.Int64)
                                {
                                    branchTaken = normalizedOpcode switch
                                    {
                                        ILOpcode.blt_s => value1.Value.AsInt64() < value2.Value.AsInt64(),
                                        ILOpcode.blt_un_s => (ulong)value1.Value.AsInt64() < (ulong)value2.Value.AsInt64(),
                                        ILOpcode.bgt_s => value1.Value.AsInt64() > value2.Value.AsInt64(),
                                        ILOpcode.bgt_un_s => (ulong)value1.Value.AsInt64() > (ulong)value2.Value.AsInt64(),
                                        ILOpcode.bge_s => value1.Value.AsInt64() >= value2.Value.AsInt64(),
                                        ILOpcode.bge_un_s => (ulong)value1.Value.AsInt64() >= (ulong)value2.Value.AsInt64(),
                                        ILOpcode.beq_s => value1.Value.AsInt64() == value2.Value.AsInt64(),
                                        ILOpcode.bne_un_s => value1.Value.AsInt64() != value2.Value.AsInt64(),
                                        ILOpcode.ble_s => value1.Value.AsInt64() <= value2.Value.AsInt64(),
                                        ILOpcode.ble_un_s => (ulong)value1.Value.AsInt64() <= (ulong)value2.Value.AsInt64(),
                                        _ => throw new NotImplementedException() // unreachable
                                    };
                                }
                                else if (value1.ValueKind == StackValueKind.Float && value2.ValueKind == StackValueKind.Float)
                                {
                                    branchTaken = normalizedOpcode switch
                                    {
                                        ILOpcode.blt_s => value1.Value.AsDouble() < value2.Value.AsDouble(),
                                        ILOpcode.blt_un_s => !(value1.Value.AsDouble() >= value2.Value.AsDouble()),
                                        ILOpcode.bgt_s => value1.Value.AsDouble() > value2.Value.AsDouble(),
                                        ILOpcode.bgt_un_s => !(value1.Value.AsDouble() <= value2.Value.AsDouble()),
                                        ILOpcode.bge_s => value1.Value.AsDouble() >= value2.Value.AsDouble(),
                                        ILOpcode.bge_un_s => !(value1.Value.AsDouble() < value2.Value.AsDouble()),
                                        ILOpcode.beq_s => value1.Value.AsDouble() == value2.Value.AsDouble(),
                                        ILOpcode.bne_un_s => value1.Value.AsDouble() != value2.Value.AsDouble(),
                                        ILOpcode.ble_s => value1.Value.AsDouble() <= value2.Value.AsDouble(),
                                        ILOpcode.ble_un_s => !(value1.Value.AsDouble() > value2.Value.AsDouble()),
                                        _ => throw new NotImplementedException() // unreachable
                                    };
                                }
                                else
                                {
                                    return Status.Fail(methodIL.OwningMethod, opcode);
                                }
                            }
                            else
                            {
                                Debug.Assert(normalizedOpcode == ILOpcode.br_s);
                                branchTaken = true;
                            }

                            if (branchTaken)
                            {
                                // Don't allow backwards branches so that we don't have to worry about infinite loops
                                if (target < reader.Offset)
                                {
                                    return Status.Fail(methodIL.OwningMethod, opcode, "Backwards branch");
                                }

                                reader.Seek(target);
                            }
                        }
                        break;

                    case ILOpcode.leave:
                    case ILOpcode.leave_s:
                        {
                            stack.Clear();

                            // We assume no finally regions (would have to run them here)
                            // This is validated before, but we're being paranoid.
                            foreach (ILExceptionRegion ehRegion in ehRegions)
                            {
                                Debug.Assert(ehRegion.Kind != ILExceptionRegionKind.Finally);
                            }

                            int delta = opcode == ILOpcode.leave ?
                                (int)reader.ReadILUInt32() :
                                (sbyte)reader.ReadILByte();
                            int target = reader.Offset + delta;
                            if (target < 0
                                || target > reader.Size)
                            {
                                ThrowHelper.ThrowInvalidProgramException();
                            }

                            reader.Seek(target);
                        }
                        break;

                    case ILOpcode.clt:
                    case ILOpcode.clt_un:
                    case ILOpcode.cgt:
                    case ILOpcode.cgt_un:
                        {
                            StackEntry value1 = stack.Pop();
                            StackEntry value2 = stack.Pop();

                            bool condition;
                            if (value1.ValueKind == StackValueKind.Int32 && value2.ValueKind == StackValueKind.Int32)
                            {
                                if (opcode == ILOpcode.cgt)
                                    condition = value1.Value.AsInt32() < value2.Value.AsInt32();
                                else if (opcode == ILOpcode.cgt_un)
                                    condition = (uint)value1.Value.AsInt32() < (uint)value2.Value.AsInt32();
                                else if (opcode == ILOpcode.clt)
                                    condition = value1.Value.AsInt32() > value2.Value.AsInt32();
                                else if (opcode == ILOpcode.clt_un)
                                    condition = (uint)value1.Value.AsInt32() > (uint)value2.Value.AsInt32();
                                else
                                    return Status.Fail(methodIL.OwningMethod, opcode);
                            }
                            else if (value1.ValueKind == StackValueKind.Int64 && value2.ValueKind == StackValueKind.Int64)
                            {
                                if (opcode == ILOpcode.cgt)
                                    condition = value1.Value.AsInt64() < value2.Value.AsInt64();
                                else if (opcode == ILOpcode.cgt_un)
                                    condition = (ulong)value1.Value.AsInt64() < (ulong)value2.Value.AsInt64();
                                else if (opcode == ILOpcode.clt)
                                    condition = value1.Value.AsInt64() > value2.Value.AsInt64();
                                else if (opcode == ILOpcode.clt_un)
                                    condition = (ulong)value1.Value.AsInt64() > (ulong)value2.Value.AsInt64();
                                else
                                    return Status.Fail(methodIL.OwningMethod, opcode);
                            }
                            else if (value1.ValueKind == StackValueKind.Float && value2.ValueKind == StackValueKind.Float)
                            {
                                if (opcode == ILOpcode.cgt)
                                    condition = value1.Value.AsDouble() < value2.Value.AsDouble();
                                else if (opcode == ILOpcode.cgt_un)
                                    condition = !(value1.Value.AsDouble() >= value2.Value.AsDouble());
                                else if (opcode == ILOpcode.clt)
                                    condition = value1.Value.AsDouble() > value2.Value.AsDouble();
                                else if (opcode == ILOpcode.clt_un)
                                    condition = !(value1.Value.AsDouble() <= value2.Value.AsDouble());
                                else
                                    return Status.Fail(methodIL.OwningMethod, opcode);
                            }
                            else if (value1.ValueKind == StackValueKind.ObjRef && value2.ValueKind == StackValueKind.ObjRef)
                            {
                                if (opcode == ILOpcode.cgt_un)
                                    condition = value1.Value == null && value2.Value != null;
                                else
                                    return Status.Fail(methodIL.OwningMethod, opcode);
                            }
                            else
                            {
                                return Status.Fail(methodIL.OwningMethod, opcode);
                            }

                            stack.Push(StackValueKind.Int32, condition
                                    ? ValueTypeValue.FromInt32(1)
                                    : ValueTypeValue.FromInt32(0));
                        }
                        break;

                    case ILOpcode.ceq:
                        {
                            StackEntry value1 = stack.Pop();
                            StackEntry value2 = stack.Pop();

                            if (value1.ValueKind == value2.ValueKind)
                            {
                                stack.Push(StackValueKind.Int32,
                                    Value.Equals(value1.Value, value2.Value)
                                    ? ValueTypeValue.FromInt32(1)
                                    : ValueTypeValue.FromInt32(0));
                            }
                            else
                            {
                                return Status.Fail(methodIL.OwningMethod, opcode);
                            }
                        }
                        break;

                    case ILOpcode.neg:
                        {
                            StackEntry value = stack.Pop();
                            if (value.ValueKind == StackValueKind.Int32)
                                stack.Push(StackValueKind.Int32, ValueTypeValue.FromInt32(-value.Value.AsInt32()));
                            else
                                return Status.Fail(methodIL.OwningMethod, opcode);
                        }
                        break;

                    case ILOpcode.or:
                    case ILOpcode.shl:
                    case ILOpcode.add:
                    case ILOpcode.sub:
                    case ILOpcode.mul:
                    case ILOpcode.and:
                    case ILOpcode.div:
                    case ILOpcode.rem:
                        {
                            bool isDivRem = opcode == ILOpcode.div || opcode == ILOpcode.rem;

                            StackEntry value2 = stack.Pop();
                            StackEntry value1 = stack.Pop();
                            if (value1.ValueKind == StackValueKind.Int32 && value2.ValueKind == StackValueKind.Int32)
                            {
                                if (isDivRem && value2.Value.AsInt32() == 0)
                                    return Status.Fail(methodIL.OwningMethod, opcode, "Division by zero");

                                int result = opcode switch
                                {
                                    ILOpcode.or => value1.Value.AsInt32() | value2.Value.AsInt32(),
                                    ILOpcode.shl => value1.Value.AsInt32() << value2.Value.AsInt32(),
                                    ILOpcode.add => value1.Value.AsInt32() + value2.Value.AsInt32(),
                                    ILOpcode.sub => value1.Value.AsInt32() - value2.Value.AsInt32(),
                                    ILOpcode.and => value1.Value.AsInt32() & value2.Value.AsInt32(),
                                    ILOpcode.mul => value1.Value.AsInt32() * value2.Value.AsInt32(),
                                    ILOpcode.div => value1.Value.AsInt32() / value2.Value.AsInt32(),
                                    ILOpcode.rem => value1.Value.AsInt32() % value2.Value.AsInt32(),
                                    _ => throw new NotImplementedException(), // unreachable
                                };

                                stack.Push(StackValueKind.Int32, ValueTypeValue.FromInt32(result));
                            }
                            else if (value1.ValueKind == StackValueKind.Int64 && value2.ValueKind == StackValueKind.Int64)
                            {
                                if (isDivRem && value2.Value.AsInt64() == 0)
                                    return Status.Fail(methodIL.OwningMethod, opcode, "Division by zero");

                                long result = opcode switch
                                {
                                    ILOpcode.or => value1.Value.AsInt64() | value2.Value.AsInt64(),
                                    ILOpcode.add => value1.Value.AsInt64() + value2.Value.AsInt64(),
                                    ILOpcode.sub => value1.Value.AsInt64() - value2.Value.AsInt64(),
                                    ILOpcode.and => value1.Value.AsInt64() & value2.Value.AsInt64(),
                                    ILOpcode.mul => value1.Value.AsInt64() * value2.Value.AsInt64(),
                                    ILOpcode.div => value1.Value.AsInt64() / value2.Value.AsInt64(),
                                    ILOpcode.rem => value1.Value.AsInt64() % value2.Value.AsInt64(),
                                    _ => throw new NotImplementedException(), // unreachable
                                };

                                stack.Push(StackValueKind.Int64, ValueTypeValue.FromInt64(result));
                            }
                            else if (value1.ValueKind == StackValueKind.Float && value2.ValueKind == StackValueKind.Float)
                            {
                                if (isDivRem && value2.Value.AsDouble() == 0)
                                    return Status.Fail(methodIL.OwningMethod, opcode, "Division by zero");

                                if (opcode == ILOpcode.or || opcode == ILOpcode.shl || opcode == ILOpcode.and)
                                    ThrowHelper.ThrowInvalidProgramException();

                                double result = opcode switch
                                {
                                    ILOpcode.add => value1.Value.AsDouble() + value2.Value.AsDouble(),
                                    ILOpcode.sub => value1.Value.AsDouble() - value2.Value.AsDouble(),
                                    ILOpcode.mul => value1.Value.AsDouble() * value2.Value.AsDouble(),
                                    ILOpcode.div => value1.Value.AsDouble() / value2.Value.AsDouble(),
                                    ILOpcode.rem => value1.Value.AsDouble() % value2.Value.AsDouble(),
                                    _ => throw new NotImplementedException(), // unreachable
                                };

                                stack.Push(StackValueKind.Float, ValueTypeValue.FromDouble(result));
                            }
                            else if (value1.ValueKind == StackValueKind.Int64 && value2.ValueKind == StackValueKind.Int32
                                && opcode == ILOpcode.shl)
                            {
                                long result = value1.Value.AsInt64() << value2.Value.AsInt32();
                                stack.Push(StackValueKind.Int64, ValueTypeValue.FromInt64(result));
                            }
                            else
                            {
                                return Status.Fail(methodIL.OwningMethod, opcode);
                            }
                        }
                        break;

                    case ILOpcode.ldlen:
                        {
                            StackEntry popped = stack.Pop();
                            if (popped.Value is ArrayInstance arrayInstance)
                            {
                                stack.Push(StackValueKind.NativeInt, ValueTypeValue.FromInt64(arrayInstance.Length));
                            }
                            else if (popped.Value == null)
                            {
                                return Status.Fail(methodIL.OwningMethod, opcode, "Null array");
                            }
                            else
                            {
                                ThrowHelper.ThrowInvalidProgramException();
                            }
                        }
                        break;

                    case ILOpcode.stelem:
                    case ILOpcode.stelem_i:
                    case ILOpcode.stelem_i1:
                    case ILOpcode.stelem_i2:
                    case ILOpcode.stelem_i4:
                    case ILOpcode.stelem_i8:
                    case ILOpcode.stelem_r4:
                    case ILOpcode.stelem_r8:
                        {
                            TypeDesc elementType = opcode switch
                            {
                                ILOpcode.stelem_i => context.GetWellKnownType(WellKnownType.IntPtr),
                                ILOpcode.stelem_i1 => context.GetWellKnownType(WellKnownType.SByte),
                                ILOpcode.stelem_i2 => context.GetWellKnownType(WellKnownType.Int16),
                                ILOpcode.stelem_i4 => context.GetWellKnownType(WellKnownType.Int32),
                                ILOpcode.stelem_i8 => context.GetWellKnownType(WellKnownType.Int64),
                                ILOpcode.stelem_r4 => context.GetWellKnownType(WellKnownType.Single),
                                ILOpcode.stelem_r8 => context.GetWellKnownType(WellKnownType.Double),
                                _ => (TypeDesc)methodIL.GetObject(reader.ReadILToken()),
                            };

                            if (elementType.IsGCPointer)
                            {
                                return Status.Fail(methodIL.OwningMethod, opcode);
                            }

                            Value value = stack.PopIntoLocation(elementType);
                            if (!stack.TryPopIntValue(out int index))
                            {
                                ThrowHelper.ThrowInvalidProgramException();
                            }
                            StackEntry array = stack.Pop();
                            if (array.Value is ArrayInstance arrayInstance)
                            {
                                if (!arrayInstance.TryStoreElement(index, value))
                                    return Status.Fail(methodIL.OwningMethod, opcode, "Out of range access");
                            }
                            else if (array.Value == null)
                            {
                                return Status.Fail(methodIL.OwningMethod, opcode, "Null array");
                            }
                            else
                            {
                                ThrowHelper.ThrowInvalidProgramException();
                            }
                        }
                        break;

                    case ILOpcode.box:
                        {
                            TypeDesc type = (TypeDesc)methodIL.GetObject(reader.ReadILToken());
                            if (type.IsValueType)
                            {
                                if (type.IsNullable)
                                    return Status.Fail(methodIL.OwningMethod, opcode);

                                Value value = stack.PopIntoLocation(type);
                                stack.Push(StackValueKind.ObjRef, ObjectInstance.Box((DefType)type, ((ValueTypeValue)value).InstanceBytes));
                            }
                        }
                        break;

                    case ILOpcode.unbox_any:
                        {
                            TypeDesc type = (TypeDesc)methodIL.GetObject(reader.ReadILToken());
                            StackEntry entry = stack.Pop();
                            if (entry.Value is ObjectInstance objInst
                                && objInst.TryUnboxAny(type, out Value unboxed))
                            {
                                stack.PushFromLocation(type, unboxed);
                            }
                            else
                            {
                                ThrowHelper.ThrowInvalidProgramException();
                            }
                        }
                        break;

                    default:
                        return Status.Fail(methodIL.OwningMethod, opcode);
                }

            }

            return Status.Fail(methodIL.OwningMethod, "Control fell through");
        }

        private static Value NewUninitializedLocationValue(TypeDesc locationType)
        {
            if (locationType.IsGCPointer || locationType.IsByRef)
            {
                return null;
            }
            else
            {
                Debug.Assert(locationType.IsValueType || locationType.IsPointer || locationType.IsFunctionPointer);
                return new ValueTypeValue(locationType);
            }
        }

        private bool TryHandleIntrinsicCall(MethodDesc method, Value[] parameters, out Value retVal)
        {
            retVal = default;

            switch (method.Name)
            {
                case "InitializeArray":
                    if (method.OwningType is MetadataType mdType
                        && mdType.Name == "RuntimeHelpers" && mdType.Namespace == "System.Runtime.CompilerServices"
                        && mdType.Module == mdType.Context.SystemModule
                        && parameters[0] is ArrayInstance array
                        && parameters[1] is RuntimeFieldHandleValue fieldHandle
                        && fieldHandle.Field.IsStatic && fieldHandle.Field.HasRva
                        && fieldHandle.Field is Internal.TypeSystem.Ecma.EcmaField ecmaField)
                    {
                        byte[] rvaData = Internal.TypeSystem.Ecma.EcmaFieldExtensions.GetFieldRvaData(ecmaField);
                        return array.TryInitialize(rvaData);
                    }
                    return false;
            }

            return false;
        }

        private TypeDesc GetArgType(MethodDesc method, int index)
        {
            var sig = method.Signature;
            int offset = 0;
            if (!sig.IsStatic)
            {
                if (index == 0)
                    return method.OwningType.IsValueType ? method.OwningType.MakeByRefType() : method.OwningType;
                offset = 1;
            }

            if ((uint)(index - offset) >= (uint)sig.Length)
                ThrowHelper.ThrowInvalidProgramException();

            return sig[index - offset];
        }

        class Stack : Stack<StackEntry>
        {
            private readonly TargetDetails _target;

            public Stack(int capacity, TargetDetails target) : base(capacity)
            {
                _target = target;
            }

            public new StackEntry Pop()
            {
                if (Count < 1)
                {
                    ThrowHelper.ThrowInvalidProgramException();
                }

                return base.Pop();
            }

            public bool TryPopIntValue(out int value)
            {
                if (Count == 0)
                {
                    value = 0;
                    return false;
                }

                StackEntry entry = Pop();
                if (entry.ValueKind == StackValueKind.Int32)
                {
                    value = entry.Value.AsInt32();
                    return true;
                }
                else if (entry.ValueKind == StackValueKind.NativeInt)
                {
                    if (_target.PointerSize == 8)
                    {
                        long longValue = entry.Value.AsInt64();
                        if (longValue < int.MinValue || longValue > int.MaxValue)
                        {
                            value = 0;
                            return false;
                        }
                        value = (int)longValue;
                        return true;
                    }

                    value = entry.Value.AsInt32();
                    return true;
                }

                value = 0;
                return false;
            }

            public void Push(StackValueKind kind, Value val)
            {
                Push(new StackEntry(kind, val));
            }

            public void Push(ReferenceTypeValue value)
            {
                Push(StackValueKind.ObjRef, value);
            }

            public void Push(ByRefValue value)
            {
                Push(StackValueKind.ByRef, value);
            }

            public void PushFromLocation(TypeDesc locationType, Value value)
            {
                switch (locationType.UnderlyingType.Category)
                {
                    case TypeFlags.Boolean:
                    case TypeFlags.Byte:
                        Push(StackValueKind.Int32, ValueTypeValue.FromInt32((byte)value.AsSByte())); break;
                    case TypeFlags.Char:
                    case TypeFlags.UInt16:
                        Push(StackValueKind.Int32, ValueTypeValue.FromInt32((ushort)value.AsInt16())); break;
                    case TypeFlags.SByte:
                        Push(StackValueKind.Int32, ValueTypeValue.FromInt32(value.AsSByte())); break;
                    case TypeFlags.Int16:
                        Push(StackValueKind.Int32, ValueTypeValue.FromInt32(value.AsInt16())); break;
                    case TypeFlags.Int32:
                    case TypeFlags.UInt32:
                        Push(StackValueKind.Int32, value); break;
                    case TypeFlags.Int64:
                    case TypeFlags.UInt64:
                        Push(StackValueKind.Int64, value); break;
                    case TypeFlags.IntPtr:
                    case TypeFlags.UIntPtr:
                    case TypeFlags.Pointer:
                    case TypeFlags.FunctionPointer:
                        Push(StackValueKind.NativeInt, value); break;
                    case TypeFlags.Single:
                        Push(StackValueKind.Float, ValueTypeValue.FromDouble(value.AsSingle())); break;
                    case TypeFlags.Double:
                        Push(StackValueKind.Float, value); break;
                    case TypeFlags.ValueType:
                    case TypeFlags.Nullable:
                        Push(StackValueKind.ValueType, value); break;
                    case TypeFlags.Class:
                    case TypeFlags.Interface:
                    case TypeFlags.Array:
                    case TypeFlags.SzArray:
                        Push(StackValueKind.ObjRef, value); break;
                    case TypeFlags.ByRef:
                        Push(StackValueKind.ByRef, value); break;
                    default:
                        throw new NotImplementedException();
                }
            }

            public Value PopIntoLocation(TypeDesc locationType)
            {
                if (Count == 0)
                {
                    ThrowHelper.ThrowInvalidProgramException();
                }

                locationType = locationType.UnderlyingType;

                StackEntry popped = Pop();

                switch (popped.ValueKind)
                {
                    case StackValueKind.Int64:
                        if (!locationType.IsWellKnownType(WellKnownType.Int64)
                            && !locationType.IsWellKnownType(WellKnownType.UInt64))
                        {
                            ThrowHelper.ThrowInvalidProgramException();
                        }
                        return popped.Value;

                    case StackValueKind.Int32:
                        if (!locationType.IsWellKnownType(WellKnownType.Int32)
                            && !locationType.IsWellKnownType(WellKnownType.UInt32))
                        {
                            int value = popped.Value.AsInt32();
                            switch (locationType.Category)
                            {
                                case TypeFlags.SByte:
                                case TypeFlags.Byte:
                                case TypeFlags.Boolean:
                                    return ValueTypeValue.FromSByte((sbyte)value);
                                case TypeFlags.Int16:
                                case TypeFlags.UInt16:
                                case TypeFlags.Char:
                                    return ValueTypeValue.FromInt16((short)value);
                                // case TypeFlags.IntPtr: sign extend
                                // case TypeFlags.UIntPtr: zero extend
                            }
                            ThrowHelper.ThrowInvalidProgramException();
                        }
                        return popped.Value;

                    case StackValueKind.NativeInt:
                        // If it's none of the natural pointer types, we might need to truncate.
                        if (!locationType.IsPointer
                            && !locationType.IsFunctionPointer
                            && !locationType.IsWellKnownType(WellKnownType.IntPtr)
                            && !locationType.IsWellKnownType(WellKnownType.UIntPtr))
                        {
                            long value = _target.PointerSize == 8 ? popped.Value.AsInt64() : popped.Value.AsInt32();
                            switch (locationType.Category)
                            {
                                case TypeFlags.SByte:
                                case TypeFlags.Byte:
                                case TypeFlags.Boolean:
                                    return ValueTypeValue.FromSByte((sbyte)value);
                                case TypeFlags.Int16:
                                case TypeFlags.UInt16:
                                case TypeFlags.Char:
                                    return ValueTypeValue.FromInt16((short)value);
                                case TypeFlags.Int32:
                                case TypeFlags.UInt32:
                                    return ValueTypeValue.FromInt32((int)value);
                                // case TypeFlags.ByRef: start GC tracking
                            }

                            ThrowHelper.ThrowInvalidProgramException();
                        }
                        return popped.Value;

                    case StackValueKind.Float:
                        if (locationType.IsWellKnownType(WellKnownType.Double))
                        {
                            return popped.Value;
                        }
                        else if (!locationType.IsWellKnownType(WellKnownType.Single))
                        {
                            ThrowHelper.ThrowInvalidProgramException();
                        }
                        return ValueTypeValue.FromSingle((float)popped.Value.AsDouble());

                    case StackValueKind.ByRef:
                        if (!locationType.IsByRef)
                        {
                            ThrowHelper.ThrowInvalidProgramException();
                        }
                        return popped.Value;

                    case StackValueKind.ObjRef:
                        if (!locationType.IsGCPointer)
                        {
                            ThrowHelper.ThrowInvalidProgramException();
                        }
                        return popped.Value;

                    case StackValueKind.ValueType:
                        if (!locationType.IsValueType
                            || ((BaseValueTypeValue)popped.Value).Size != ((DefType)locationType).InstanceFieldSize.AsInt)
                        {
                            ThrowHelper.ThrowInvalidProgramException();
                        }
                        return popped.Value;
                    
                    default:
                        throw new NotImplementedException();
                }
            }
        }

        private enum StackValueKind
        {
            Unknown,
            Int32,
            Int64,
            NativeInt,
            Float,
            ByRef,
            ObjRef,
            ValueType,
        }

        /// <summary>
        /// Represents a field value that can be serialized into a preinitialized blob.
        /// </summary>
        public interface ISerializableValue
        {
            void WriteFieldData(ref ObjectDataBuilder builder, FieldDesc field, NodeFactory factory);
        }

        /// <summary>
        /// Represents a frozen object whose contents can be serialized into the executable.
        /// </summary>
        public interface ISerializableReference : ISerializableValue
        {
            void WriteContent(ref ObjectDataBuilder builder, NodeFactory factory);
        }

        /// <summary>
        /// Represents a value with instance fields. This is either a reference type, or a byref to
        /// a valuetype.
        /// </summary>
        private interface IHasInstanceFields
        {
            void SetField(FieldDesc field, Value value);
            Value GetField(FieldDesc field);
            ByRefValue GetFieldAddress(FieldDesc field);
        }

        /// <summary>
        /// Represents a value that can be assigned into.
        /// </summary>
        private interface IAssignableValue
        {
            void Assign(Value value);
        }

        private abstract class Value : ISerializableValue
        {
            public abstract bool Equals(Value value);

            public static bool Equals(Value value1, Value value2)
            {
                if (value1 == value2)
                {
                    return true;
                }
                if (value1 == null || value2 == null)
                {
                    return false;
                }
                return value1.Equals(value2);
            }

            public abstract void WriteFieldData(ref ObjectDataBuilder builder, FieldDesc field, NodeFactory factory);

            private T ThrowInvalidProgram<T>()
            {
                ThrowHelper.ThrowInvalidProgramException();
                return default;
            }

            public virtual sbyte AsSByte() => ThrowInvalidProgram<sbyte>();
            public virtual short AsInt16() => ThrowInvalidProgram<short>();
            public virtual int AsInt32() => ThrowInvalidProgram<int>();
            public virtual long AsInt64() => ThrowInvalidProgram<long>();
            public virtual float AsSingle() => ThrowInvalidProgram<float>();
            public virtual double AsDouble() => ThrowInvalidProgram<double>();
        }

        private abstract class BaseValueTypeValue : Value
        {
            public abstract int Size { get; }
        }

        // Also represents pointers and function pointer.
        private class ValueTypeValue : BaseValueTypeValue, IAssignableValue
        {
            public readonly byte[] InstanceBytes;

            public override int Size => InstanceBytes.Length;

            public ValueTypeValue(TypeDesc type)
            {
                Debug.Assert(type.IsValueType || type.IsPointer || type.IsFunctionPointer);
                InstanceBytes = new byte[type.GetElementSize().AsInt];
            }

            private ValueTypeValue(byte[] bytes)
            {
                InstanceBytes = bytes;
            }

            public ByRefValue CreateByRef()
            {

                return new ByRefValue(InstanceBytes, 0);
            }

            void IAssignableValue.Assign(Value value)
            {
                if (!(value is ValueTypeValue other)
                    || other.Size != Size)
                {
                    ThrowHelper.ThrowInvalidProgramException();
                }

                Array.Copy(((ValueTypeValue)value).InstanceBytes, InstanceBytes, InstanceBytes.Length);
            }

            public override bool Equals(Value value)
            {
                if (!(value is ValueTypeValue vtvalue)
                    || vtvalue.InstanceBytes.Length != InstanceBytes.Length)
                {
                    ThrowHelper.ThrowInvalidProgramException();
                }

                for (int i = 0; i < InstanceBytes.Length; i++)
                {
                    if (InstanceBytes[i] != ((ValueTypeValue)value).InstanceBytes[i])
                        return false;
                }

                return true;
            }

            public override void WriteFieldData(ref ObjectDataBuilder builder, FieldDesc field, NodeFactory factory)
            {
                Debug.Assert(field.FieldType.GetElementSize().AsInt == InstanceBytes.Length);
                builder.EmitBytes(InstanceBytes);
            }

            private byte[] AsExactByteCount(int size)
            {
                if (InstanceBytes.Length != size)
                {
                    ThrowHelper.ThrowInvalidProgramException();
                }
                return InstanceBytes;
            }

            public override sbyte AsSByte() => (sbyte)AsExactByteCount(1)[0];
            public override short AsInt16() => BitConverter.ToInt16(AsExactByteCount(2), 0);
            public override int AsInt32() => BitConverter.ToInt32(AsExactByteCount(4), 0);
            public override long AsInt64() => BitConverter.ToInt64(AsExactByteCount(8), 0);
            public override float AsSingle() => BitConverter.ToSingle(AsExactByteCount(4), 0);
            public override double AsDouble() => BitConverter.ToDouble(AsExactByteCount(8), 0);
            public static ValueTypeValue FromSByte(sbyte value) => new ValueTypeValue(new byte[1] { (byte)value });
            public static ValueTypeValue FromInt16(short value) => new ValueTypeValue(BitConverter.GetBytes(value));
            public static ValueTypeValue FromInt32(int value) => new ValueTypeValue(BitConverter.GetBytes(value));
            public static ValueTypeValue FromInt64(long value) => new ValueTypeValue(BitConverter.GetBytes(value));
            public static ValueTypeValue FromSingle(float value) => new ValueTypeValue(BitConverter.GetBytes(value));
            public static ValueTypeValue FromDouble(double value) => new ValueTypeValue(BitConverter.GetBytes(value));
        }

        private class RuntimeFieldHandleValue : BaseValueTypeValue
        {
            public FieldDesc Field { get; private set; }

            public RuntimeFieldHandleValue(FieldDesc field)
            {
                Field = field;
            }

            public override int Size => Field.Context.Target.PointerSize;

            public override bool Equals(Value value)
            {
                if (!(value is RuntimeFieldHandleValue))
                {
                    ThrowHelper.ThrowInvalidProgramException();
                }

                return Field == ((RuntimeFieldHandleValue)value).Field;
            }

            public override void WriteFieldData(ref ObjectDataBuilder builder, FieldDesc field, NodeFactory factory)
            {
                Debug.Assert(field.FieldType.IsWellKnownType(WellKnownType.RuntimeFieldHandle));
                builder.EmitPointerReloc(factory.RuntimeFieldHandle(Field));
            }
        }

        private class ByRefValue : Value, IHasInstanceFields
        {
            public readonly byte[] PointedToBytes;
            public readonly int PointedToOffset;

            public ByRefValue(byte[] pointedToBytes, int pointedToOffset)
            {
                PointedToBytes = pointedToBytes;
                PointedToOffset = pointedToOffset;
            }

            public override bool Equals(Value value)
            {
                if (!(value is ByRefValue))
                {
                    ThrowHelper.ThrowInvalidProgramException();
                }

                return PointedToBytes == ((ByRefValue)value).PointedToBytes
                    && PointedToOffset == ((ByRefValue)value).PointedToOffset;
            }

            Value IHasInstanceFields.GetField(FieldDesc field) => new FieldAccessor(PointedToBytes, PointedToOffset).GetField(field);
            void IHasInstanceFields.SetField(FieldDesc field, Value value) => new FieldAccessor(PointedToBytes, PointedToOffset).SetField(field, value);
            ByRefValue IHasInstanceFields.GetFieldAddress(FieldDesc field) => new FieldAccessor(PointedToBytes, PointedToOffset).GetFieldAddress(field);

            public void Initialize(int size)
            {
                if ((uint)size > (uint)(PointedToBytes.Length - PointedToOffset))
                {
                    ThrowHelper.ThrowInvalidProgramException();
                }

                for (int i = PointedToOffset; i < PointedToOffset + size; i++)
                {
                    PointedToBytes[i] = 0;
                }
            }

            public override void WriteFieldData(ref ObjectDataBuilder builder, FieldDesc field, NodeFactory factory)
            {
                // This would imply we have a byref-typed static field. The layout algorithm should have blocked this.
                throw new NotImplementedException();
            }
        }

        private abstract class ReferenceTypeValue : Value, ISerializableReference
        {
            protected readonly TypeDesc _type;

            protected ReferenceTypeValue(TypeDesc type) { _type = type; }

            public override bool Equals(Value value)
            {
                return this == value;
            }

            public abstract void WriteContent(ref ObjectDataBuilder builder, NodeFactory factory);
        }

        private class ArrayInstance : ReferenceTypeValue
        {
            private readonly int _elementCount;
            private readonly int _elementSize;
            private readonly byte[] _data;

            public ArrayInstance(ArrayType type, int elementCount)
                : base(type)
            {
                _elementCount = elementCount;
                _elementSize = type.ElementType.GetElementSize().AsInt;
                _data = new byte[elementCount * _elementSize];
            }

            public bool TryInitialize(byte[] bytes)
            {
                if (bytes.Length != _data.Length)
                    return false;

                Array.Copy(bytes, _data, bytes.Length);
                return true;
            }

            public int Length
            {
                get
                {
                    return _elementCount;
                }
            }

            public bool TryStoreElement(int index, Value value)
            {
                Debug.Assert(value is ValueTypeValue);

                if ((uint)index > (uint)Length)
                    return false;

                var valueToStore = value as ValueTypeValue;
                Debug.Assert(valueToStore.InstanceBytes.Length == _elementSize);
                Array.Copy(valueToStore.InstanceBytes, 0, _data, index * _elementSize, valueToStore.InstanceBytes.Length);
                return true;
            }

            public override void WriteFieldData(ref ObjectDataBuilder builder, FieldDesc field, NodeFactory factory)
            {
                builder.EmitPointerReloc(factory.SerializedFrozenObject(field, this));
            }

            public override void WriteContent(ref ObjectDataBuilder builder, NodeFactory factory)
            {
                // EEType
                var node = factory.ConstructedTypeSymbol(_type);
                Debug.Assert(!node.RepresentsIndirectionCell);  // Arrays are always local
                builder.EmitPointerReloc(node);

                // numComponents
                builder.EmitInt(_elementCount);

                int pointerSize = _type.Context.Target.PointerSize;
                Debug.Assert(pointerSize == 8 || pointerSize == 4);

                if (pointerSize == 8)
                {
                    // padding numComponents in 64-bit
                    builder.EmitInt(0);
                }

                builder.EmitBytes(_data);
            }
        }

        private class StringInstance : ReferenceTypeValue
        {
            private readonly string _value;

            public StringInstance(TypeDesc stringType, string value)
                : base(stringType)
            {
                _value = value;
            }

            public override void WriteFieldData(ref ObjectDataBuilder builder, FieldDesc field, NodeFactory factory)
            {
                builder.EmitPointerReloc(factory.SerializedStringObject(_value));
            }

            public override void WriteContent(ref ObjectDataBuilder builder, NodeFactory factory)
            {
                // Not actually used by SerializedStringObject.
                throw new NotImplementedException();
            }
        }

        private class ObjectInstance : ReferenceTypeValue, IHasInstanceFields
        {
            private readonly byte[] _data;

            public ObjectInstance(DefType type)
                : base(type)
            {
                int size = type.InstanceByteCount.AsInt;
                if (type.IsValueType)
                    size += type.Context.Target.PointerSize;
                _data = new byte[size];
            }

            public static ObjectInstance Box(DefType type, byte[] data)
            {
                var inst = new ObjectInstance(type);
                Array.Copy(data, 0, inst._data, type.Context.Target.PointerSize, data.Length);
                return inst;
            }

            public bool TryUnboxAny(TypeDesc type, out Value value)
            {
                value = null;

                if (!type.IsValueType || type.IsNullable)
                    return false;

                if (_type.UnderlyingType != type.UnderlyingType)
                    return false;

                var result = new ValueTypeValue(type);
                Array.Copy(_data, type.Context.Target.PointerSize, result.InstanceBytes, 0, result.InstanceBytes.Length);
                value = result;
                return true;
            }

            Value IHasInstanceFields.GetField(FieldDesc field) => new FieldAccessor(_data).GetField(field);
            void IHasInstanceFields.SetField(FieldDesc field, Value value) => new FieldAccessor(_data).SetField(field, value);
            ByRefValue IHasInstanceFields.GetFieldAddress(FieldDesc field) => new FieldAccessor(_data).GetFieldAddress(field);

            public override void WriteFieldData(ref ObjectDataBuilder builder, FieldDesc field, NodeFactory factory)
            {
                builder.EmitPointerReloc(factory.SerializedFrozenObject(field, this));
            }

            public override void WriteContent(ref ObjectDataBuilder builder, NodeFactory factory)
            {
                // EEType
                var node = factory.ConstructedTypeSymbol(_type);
                Debug.Assert(!node.RepresentsIndirectionCell);  // Shouldn't have allowed preinitializing this
                builder.EmitPointerReloc(node);

                // We skip the first pointer because that's the EEType pointer
                // we just initialized above.
                int pointerSize = factory.Target.PointerSize;
                builder.EmitBytes(_data, pointerSize, _data.Length - pointerSize);
            }
        }

        private struct FieldAccessor
        {
            private readonly byte[] _instanceBytes;
            private readonly int _offset;

            public FieldAccessor(byte[] bytes, int offset = 0)
            {
                _instanceBytes = bytes;
                _offset = 0;
            }

            public Value GetField(FieldDesc field)
            {
                Debug.Assert(!field.IsStatic);
                Debug.Assert(!field.FieldType.IsGCPointer);
                int fieldOffset = field.Offset.AsInt;
                int fieldSize = field.FieldType.GetElementSize().AsInt;
                if (fieldOffset + fieldSize > _instanceBytes.Length - _offset)
                    ThrowHelper.ThrowInvalidProgramException();

                var result = new ValueTypeValue(field.FieldType);
                Array.Copy(_instanceBytes, _offset + fieldOffset, result.InstanceBytes, 0, fieldSize);
                return result;
            }

            public void SetField(FieldDesc field, Value value)
            {
                Debug.Assert(!field.IsStatic);
                Debug.Assert(!field.FieldType.IsGCPointer);
                int fieldOffset = field.Offset.AsInt;
                int fieldSize = field.FieldType.GetElementSize().AsInt;
                if (fieldOffset + fieldSize > _instanceBytes.Length - _offset)
                    ThrowHelper.ThrowInvalidProgramException();

                Array.Copy(((ValueTypeValue)value).InstanceBytes, 0, _instanceBytes, _offset + fieldOffset, fieldSize);
            }

            public ByRefValue GetFieldAddress(FieldDesc field)
            {
                Debug.Assert(!field.IsStatic);
                Debug.Assert(!field.FieldType.IsGCPointer);
                int fieldOffset = field.Offset.AsInt;
                int fieldSize = field.FieldType.GetElementSize().AsInt;
                if (fieldOffset + fieldSize > _instanceBytes.Length - _offset)
                    ThrowHelper.ThrowInvalidProgramException();

                return new ByRefValue(_instanceBytes, _offset + fieldOffset);
            }
        }

        private struct StackEntry
        {
            public readonly StackValueKind ValueKind;
            public readonly Value Value;

            public StackEntry(StackValueKind valueKind, Value value)
            {
                ValueKind = valueKind;
                Value = value;
            }
        }

        private struct Status
        {
            public string FailureReason { get; }

            public static Status Success => default;

            public bool IsSuccessful => FailureReason == null;

            private Status(string message)
            {
                FailureReason = message;
            }

            public static Status Fail(MethodDesc method, ILOpcode opcode, string detail = null)
            {
                return new Status($"Method '{method}', opcode '{opcode}' {detail ?? ""}");
            }

            public static Status Fail(MethodDesc method, string detail)
            {
                return new Status($"Method '{method}': {detail}");
            }
        }

        public class PreinitializationInfo
        {
            private readonly Dictionary<FieldDesc, ISerializableValue> _fieldValues;

            public MetadataType Type { get; }

            public string FailureReason { get; }

            public bool IsPreinitialized => _fieldValues != null;

            public PreinitializationInfo(MetadataType type, IEnumerable<KeyValuePair<FieldDesc, ISerializableValue>> fieldValues)
            {
                Type = type;
                _fieldValues = new Dictionary<FieldDesc, ISerializableValue>();
                foreach (var field in fieldValues)
                    _fieldValues.Add(field.Key, field.Value);
            }

            public PreinitializationInfo(MetadataType type, string failureReason)
            {
                Type = type;
                FailureReason = failureReason;
            }

            public ISerializableValue GetFieldValue(FieldDesc field)
            {
                Debug.Assert(IsPreinitialized);
                Debug.Assert(field.OwningType == Type);
                Debug.Assert(field.IsStatic && !field.HasRva && !field.IsThreadStatic && !field.IsLiteral);
                return _fieldValues[field];
            }
        }
    }
}
