// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

using Internal.TypeSystem;
using ILCompiler;
using LLVMSharp;
using ILCompiler.CodeGen;

namespace Internal.IL
{
    // Implements an IL scanner that scans method bodies to be compiled by the code generation
    // backend before the actual compilation happens to gain insights into the code.
    partial class ILImporter
    {
        public LLVMModuleRef Module { get; }
        private readonly MethodDesc _method;
        private readonly WebAssemblyCodegenCompilation _compilation;
        private LLVMValueRef _llvmFunction;
        private LLVMBasicBlockRef _curBasicBlock;
        private LLVMBuilderRef _builder;
        private readonly LocalVariableDefinition[] _locals;

        private readonly byte[] _ilBytes;

        /// <summary>
        /// Stack of values pushed onto the IL stack: locals, arguments, values, function pointer, ...
        /// </summary>
        private EvaluationStack<StackEntry> _stack = new EvaluationStack<StackEntry>(0);

        private class BasicBlock
        {
            // Common fields
            public BasicBlock Next;

            public int StartOffset;
            public int EndOffset;

            public EvaluationStack<StackEntry> EntryStack;

            public bool TryStart;
            public bool FilterStart;
            public bool HandlerStart;

            public LLVMBasicBlockRef Block;
        }

        private class ExceptionRegion
        {
            public ILExceptionRegion ILRegion;
        }
        private ExceptionRegion[] _exceptionRegions;

        public ILImporter(WebAssemblyCodegenCompilation compilation, MethodDesc method, MethodIL methodIL, string mangledName)
        {
            Module = compilation.Module;
            _compilation = compilation;
            _method = method;
            _ilBytes = methodIL.GetILBytes();
            _locals = methodIL.GetLocals();

            var ilExceptionRegions = methodIL.GetExceptionRegions();
            _exceptionRegions = new ExceptionRegion[ilExceptionRegions.Length];
            for (int i = 0; i < ilExceptionRegions.Length; i++)
            {
                _exceptionRegions[i] = new ExceptionRegion() { ILRegion = ilExceptionRegions[i] };
            }
            CreateLLVMFunction(mangledName);
        }

        public void Import()
        {
            FindBasicBlocks();
            ImportBasicBlocks();
        }

        private void GenerateProlog()
        {
            int totalLocalSize = 0;
            foreach(LocalVariableDefinition local in _locals)
            {
                int localSize = local.Type.GetElementSize().AsInt;
                totalLocalSize += localSize;
            }

            var sp = LLVM.GetFirstParam(_llvmFunction);
            
            for (int i = 0; i < totalLocalSize; i++)
            {
                var stackOffset = LLVM.BuildGEP(_builder, sp, new LLVMValueRef[] { LLVM.ConstInt(LLVM.Int32Type(), (ulong)i, LLVMMisc.False) }, String.Empty);
                LLVM.BuildStore(_builder, LLVM.ConstInt(LLVM.Int8Type(), 0, LLVMMisc.False), stackOffset);
            }
        }

        private void CreateLLVMFunction(string mangledName)
        {
            LLVMTypeRef universalSignature = LLVM.FunctionType(LLVM.VoidType(), new LLVMTypeRef[] { LLVM.PointerType(LLVM.Int8Type(), 0), LLVM.PointerType(LLVM.Int8Type(), 0) }, false);
            _llvmFunction = LLVM.AddFunction(Module, mangledName , universalSignature);
            _builder = LLVM.CreateBuilder();
        }

        /// <summary>
        /// Push an expression named <paramref name="name"/> of kind <paramref name="kind"/>.
        /// </summary>
        /// <param name="kind">Kind of entry in stack</param>
        /// <param name="name">Variable to be pushed</param>
        /// <param name="type">Type if any of <paramref name="name"/></param>
        private void PushExpression(StackValueKind kind, string name, LLVMValueRef llvmValue, TypeDesc type = null)
        {
            Debug.Assert(kind != StackValueKind.Unknown, "Unknown stack kind");

            _stack.Push(new ExpressionEntry(kind, name, llvmValue, type));
        }
        

        /// <summary>
        /// Generate a cast in case the stack type of source is not identical or compatible with destination type.
        /// </summary>
        /// <param name="destType">Type of destination</param>
        /// <param name="srcEntry">Source entry from stack</param>
        private void AppendCastIfNecessary(TypeDesc destType, StackEntry srcEntry)
        {
            ConstantEntry constant = srcEntry as ConstantEntry;
            if ((constant != null) && (constant.IsCastNecessary(destType)) || !destType.IsValueType || destType != srcEntry.Type)
            {
                throw new NotImplementedException();
                /*
                Append("(");
                Append(GetSignatureTypeNameAndAddReference(destType));
                Append(")");*/
            }
        }

        private void AppendCastIfNecessary(StackValueKind dstType, TypeDesc srcType)
        {
            if (dstType == StackValueKind.ByRef)
            {

                throw new NotImplementedException();
                /*
                Append("(");
                Append(GetSignatureTypeNameAndAddReference(srcType));
                Append(")");*/
            }
            else
            if (srcType.IsPointer)
            {
                throw new NotImplementedException();
                //Append("(intptr_t)");
            }
        }


        private void MarkInstructionBoundary()
        {
        }

        private LLVMBasicBlockRef GetLLVMBasicBlockForBlock(BasicBlock block)
        {
            if (block.Block.Pointer == IntPtr.Zero)
            {
                block.Block = LLVM.AppendBasicBlock(_llvmFunction, "Block" + block.StartOffset);
            }
            return block.Block;
        }

        private void StartImportingBasicBlock(BasicBlock basicBlock)
        {
            _stack.Clear();

            EvaluationStack<StackEntry> entryStack = basicBlock.EntryStack;
            if (entryStack != null)
            {
                int n = entryStack.Length;
                for (int i = 0; i < n; i++)
                {
                    _stack.Push(entryStack[i].Duplicate());
                }
            }

            bool isFirstBlock = false;
            if(_curBasicBlock.Equals(default(LLVMBasicBlockRef)))
            {
                isFirstBlock = true;
            }
            _curBasicBlock = GetLLVMBasicBlockForBlock(basicBlock);
            
            LLVM.PositionBuilderAtEnd(_builder, _curBasicBlock);
            
            if(isFirstBlock)
            {
                GenerateProlog();
            }
        }

        private void EndImportingBasicBlock(BasicBlock basicBlock)
        {
            var terminator = basicBlock.Block.GetBasicBlockTerminator();
            if (terminator.Pointer == IntPtr.Zero)
            {
                LLVM.BuildBr(_builder, GetLLVMBasicBlockForBlock(_basicBlocks[_currentOffset]));
            }
        }

        private void StartImportingInstruction()
        {
        }

        private void EndImportingInstruction()
        {
        }

        private void ImportNop()
        {
        }

        private void ImportBreak()
        {
        }

        private void ImportLoadVar(int index, bool argument)
        {
            if (argument)
            {
                throw new NotImplementedException("loading from argument");
            }

            GetLocalSizeAndOffsetAtIndex(index, out int localSize, out int localOffset);

            LLVMTypeRef valueType = GetLLVMTypeForTypeDesc(_locals[index].Type);
            var loadLocation = LLVM.BuildGEP(_builder, LLVM.GetFirstParam(_llvmFunction),
                new LLVMValueRef[] { LLVM.ConstInt(LLVM.Int32Type(), (uint)localOffset, LLVMMisc.False) },
                String.Empty);
            var typedLoadLocation = LLVM.BuildPointerCast(_builder, loadLocation, LLVM.PointerType(valueType, 0), String.Empty);
            var loadResult = LLVM.BuildLoad(_builder, typedLoadLocation, String.Empty);

            _stack.Push(new ExpressionEntry(GetStackValueKind(_locals[index].Type), String.Empty, loadResult, _locals[index].Type));
        }

        private StackValueKind GetStackValueKind(TypeDesc type)
        {
            switch (type.Category)
            {
                case TypeFlags.Boolean:
                case TypeFlags.Char:
                case TypeFlags.SByte:
                case TypeFlags.Byte:
                case TypeFlags.Int16:
                case TypeFlags.UInt16:
                case TypeFlags.Int32:
                case TypeFlags.UInt32:
                    return StackValueKind.Int32;
                case TypeFlags.Int64:
                case TypeFlags.UInt64:
                    return StackValueKind.Int64;
                case TypeFlags.Single:
                case TypeFlags.Double:
                    return StackValueKind.Float;
                case TypeFlags.IntPtr:
                case TypeFlags.UIntPtr:
                    return StackValueKind.NativeInt;
                case TypeFlags.ValueType:
                case TypeFlags.Nullable:
                    return StackValueKind.ValueType;
                case TypeFlags.Enum:
                    return GetStackValueKind(type.UnderlyingType);
                case TypeFlags.Class:
                case TypeFlags.Interface:
                case TypeFlags.Array:
                case TypeFlags.SzArray:
                    return StackValueKind.ObjRef;
                case TypeFlags.ByRef:
                    return StackValueKind.ByRef;
                case TypeFlags.Pointer:
                    return StackValueKind.NativeInt;
                default:
                    return StackValueKind.Unknown;
            }
        }

        private void ImportStoreVar(int index, bool argument)
        {
            if(argument)
            {
                throw new NotImplementedException("storing to argument");
            }
            
            GetLocalSizeAndOffsetAtIndex(index, out int localSize, out int localOffset);

            LLVMValueRef toStore = _stack.Pop().LLVMValue;

            LLVMTypeRef valueType = GetLLVMTypeForTypeDesc(_locals[index].Type);
            var storeLocation = LLVM.BuildGEP(_builder, LLVM.GetFirstParam(_llvmFunction),
                new LLVMValueRef[] { LLVM.ConstInt(LLVM.Int32Type(), (uint)localOffset, LLVMMisc.False) },
                String.Empty);
            var typedStoreLocation = LLVM.BuildPointerCast(_builder, storeLocation, LLVM.PointerType(valueType, 0), String.Empty);
            LLVM.BuildStore(_builder, toStore, typedStoreLocation);
        }

        private LLVMTypeRef GetLLVMTypeForTypeDesc(TypeDesc type)
        {
            switch (type.Category)
            {
                case TypeFlags.Boolean:
                    return LLVM.Int1Type();

                case TypeFlags.SByte:
                case TypeFlags.Byte:
                    return LLVM.Int8Type();

                case TypeFlags.Int16:
                case TypeFlags.UInt16:
                case TypeFlags.Char:
                    return LLVM.Int16Type();

                case TypeFlags.Int32:
                case TypeFlags.UInt32:
                case TypeFlags.IntPtr:
                case TypeFlags.UIntPtr:
                case TypeFlags.Class:
                case TypeFlags.Interface:
                case TypeFlags.Array:
                case TypeFlags.SzArray:
                case TypeFlags.ByRef:
                case TypeFlags.Pointer:
                    return LLVM.Int32Type();

                case TypeFlags.Int64:
                case TypeFlags.UInt64:
                    return LLVM.Int64Type();

                case TypeFlags.Single:
                    return LLVM.FloatType();

                case TypeFlags.Double:
                    return LLVM.DoubleType();

                case TypeFlags.ValueType:
                case TypeFlags.Nullable:
                    return LLVM.ArrayType(LLVM.Int8Type(), (uint)type.GetElementSize().AsInt);

                case TypeFlags.Enum:
                    return GetLLVMTypeForTypeDesc(type.UnderlyingType);                    
                default:
                    throw new NotImplementedException(type.Category.ToString());
            }
        }

        private void GetLocalSizeAndOffsetAtIndex(int index, out int size, out int offset)
        {
            LocalVariableDefinition local = _locals[index];
            size = local.Type.GetElementSize().AsInt;

            offset = 0;
            for (int i = 0; i < index; i++)
            {
                offset += _locals[i].Type.GetElementSize().AsInt;
            }
        }

        private void ImportAddressOfVar(int index, bool argument)
        {
        }

        private void ImportDup()
        {
        }

        private void ImportPop()
        {
        }

        private void ImportJmp(int token)
        {
        }

        private void ImportCasting(ILOpcode opcode, int token)
        {
        }

        private void ImportLoadNull()
        {
        }

        private void ImportReturn()
        {
            StackEntry retVal = _stack.Pop();
            //LLVM.BuildRet(_builder, retVal.LLVMValue);
            LLVM.BuildRetVoid(_builder);
        }

        private void ImportCall(ILOpcode opcode, int token)
        {
        }

        private void ImportCalli(int token)
        {
        }

        private void ImportLdFtn(int token, ILOpcode opCode)
        {
        }

        private void ImportLoadInt(long value, StackValueKind kind)
        {
            switch (kind)
            {
                case StackValueKind.Int32:
                case StackValueKind.NativeInt:
                    _stack.Push(new Int32ConstantEntry((int)value));
                    break;

                case StackValueKind.Int64:
                    _stack.Push(new Int64ConstantEntry(value));
                    break;

                default:
                    throw new InvalidOperationException(kind.ToString());
            }           

        }

        private void ImportLoadFloat(double value)
        {
        }

        private void ImportBranch(ILOpcode opcode, BasicBlock target, BasicBlock fallthrough)
        {
            if (opcode == ILOpcode.br)
            {
                LLVM.BuildBr(_builder, GetLLVMBasicBlockForBlock(target));
            }
            else
            {
                LLVMValueRef condition;

                if (opcode == ILOpcode.brfalse)
                {
                    var op = _stack.Pop();
                    condition = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntEQ, op.LLVMValue, LLVM.ConstInt(LLVM.Int32Type(), 0, LLVMMisc.False), "brfalse");
                }
                else if (opcode == ILOpcode.brtrue)
                {
                    var op = _stack.Pop();
                    condition = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntNE, op.LLVMValue, LLVM.ConstInt(LLVM.Int32Type(), 0, LLVMMisc.False), "brfalse");
                }
                else
                {
                    var op1 = _stack.Pop();
                    var op2 = _stack.Pop();

                    // StackValueKind is carefully ordered to make this work (assuming the IL is valid)
                    StackValueKind kind;

                    if (op1.Kind > op2.Kind)
                    {
                        kind = op1.Kind;
                    }
                    else
                    {
                        kind = op2.Kind;
                    }


                    switch (opcode)
                    {
                        case ILOpcode.beq:
                            condition = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntEQ, op1.LLVMValue, op2.LLVMValue, "beq");
                            break;
                        case ILOpcode.bge:
                            condition = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntSGE, op1.LLVMValue, op2.LLVMValue, "bge");
                            break;
                        case ILOpcode.bgt:
                            condition = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntSGT, op1.LLVMValue, op2.LLVMValue, "bgt");
                            break;
                        case ILOpcode.ble:
                            condition = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntSLE, op1.LLVMValue, op2.LLVMValue, "ble");
                            break;
                        case ILOpcode.blt:
                            condition = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntSLT, op1.LLVMValue, op2.LLVMValue, "blt");
                            break;
                        case ILOpcode.bne_un:
                            condition = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntNE, op1.LLVMValue, op2.LLVMValue, "bne_un");
                            break;
                        case ILOpcode.bge_un:
                            condition = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntUGE, op1.LLVMValue, op2.LLVMValue, "bge_un");
                            break;
                        case ILOpcode.bgt_un:
                            condition = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntUGT, op1.LLVMValue, op2.LLVMValue, "bgt_un");
                            break;
                        case ILOpcode.ble_un:
                            condition = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntULE, op1.LLVMValue, op2.LLVMValue, "ble_un");
                            break;
                        case ILOpcode.blt_un:
                            condition = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntULT, op1.LLVMValue, op2.LLVMValue, "blt_un");
                            break;
                        default:
                            throw new NotSupportedException(); // unreachable
                    }
                }

                LLVM.BuildCondBr(_builder, condition, GetLLVMBasicBlockForBlock(target), GetLLVMBasicBlockForBlock(fallthrough));

                ImportFallthrough(fallthrough);
            }

            ImportFallthrough(target);
        }

        private void ImportSwitchJump(int jmpBase, int[] jmpDelta, BasicBlock fallthrough)
        {
        }

        private void ImportLoadIndirect(int token)
        {
        }

        private void ImportLoadIndirect(TypeDesc type)
        {
        }

        private void ImportStoreIndirect(int token)
        {
        }

        private void ImportStoreIndirect(TypeDesc type)
        {
        }

        private void ImportBinaryOperation(ILOpcode opcode)
        {
            StackEntry op1 = _stack.Pop();
            StackEntry op2 = _stack.Pop();

            // StackValueKind is carefully ordered to make this work (assuming the IL is valid)
            StackValueKind kind;
            TypeDesc type;

            if (op1.Kind > op2.Kind)
            {
                kind = op1.Kind;
                type = op1.Type;
            }
            else
            {
                kind = op2.Kind;
                type = op2.Type;
            }

            // The one exception from the above rule
            if ((kind == StackValueKind.ByRef) &&
                    (opcode == ILOpcode.sub || opcode == ILOpcode.sub_ovf || opcode == ILOpcode.sub_ovf_un))
            {
                kind = StackValueKind.NativeInt;
                type = null;
            }

            LLVMValueRef result;
            switch (opcode)
            {
                case ILOpcode.add:
                    result = LLVM.BuildAdd(_builder, op1.LLVMValue, op2.LLVMValue, "add");
                    break;
                case ILOpcode.sub:
                    result = LLVM.BuildSub(_builder, op1.LLVMValue, op2.LLVMValue, "sub");
                    break;
                case ILOpcode.mul:
                    result = LLVM.BuildMul(_builder, op1.LLVMValue, op2.LLVMValue, "mul");
                    break;
                case ILOpcode.div:
                    result = LLVM.BuildSDiv(_builder, op1.LLVMValue, op2.LLVMValue, "sdiv");
                    break;
                case ILOpcode.div_un:
                    result = LLVM.BuildUDiv(_builder, op1.LLVMValue, op2.LLVMValue, "udiv");
                    break;
                case ILOpcode.rem:
                    result = LLVM.BuildSRem(_builder, op1.LLVMValue, op2.LLVMValue, "srem");
                    break;
                case ILOpcode.rem_un:
                    result = LLVM.BuildURem(_builder, op1.LLVMValue, op2.LLVMValue, "urem");
                    break;
                case ILOpcode.and:
                    result = LLVM.BuildAnd(_builder, op1.LLVMValue, op2.LLVMValue, "and");
                    break;
                case ILOpcode.or:
                    result = LLVM.BuildOr(_builder, op1.LLVMValue, op2.LLVMValue, "or");
                    break;
                case ILOpcode.xor:
                    result = LLVM.BuildXor(_builder, op1.LLVMValue, op2.LLVMValue, "xor");
                    break;

                // TODO: Overflow checks
                case ILOpcode.add_ovf:
                case ILOpcode.add_ovf_un:
                    result = LLVM.BuildAdd(_builder, op1.LLVMValue, op2.LLVMValue, "add");
                    break;
                case ILOpcode.sub_ovf:
                case ILOpcode.sub_ovf_un:
                    result = LLVM.BuildSub(_builder, op1.LLVMValue, op2.LLVMValue, "sub");
                    break;
                case ILOpcode.mul_ovf:
                case ILOpcode.mul_ovf_un:
                    result = LLVM.BuildMul(_builder, op1.LLVMValue, op2.LLVMValue, "mul");
                    break;

                default:
                    throw new InvalidOperationException(); // Should be unreachable
            }

            PushExpression(kind, "", result, type);
        }

        private void ImportShiftOperation(ILOpcode opcode)
        {
        }

        private void ImportCompareOperation(ILOpcode opcode)
        {
            var op1 = _stack.Pop();
            var op2 = _stack.Pop();

            // StackValueKind is carefully ordered to make this work (assuming the IL is valid)
            StackValueKind kind;

            if (op1.Kind > op2.Kind)
            {
                kind = op1.Kind;
            }
            else
            {
                kind = op2.Kind;
            }

            LLVMValueRef result;
            switch (opcode)
            {
                case ILOpcode.ceq:
                    result = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntEQ, op1.LLVMValue, op2.LLVMValue, "ceq");
                    break;
                case ILOpcode.cgt:
                    result = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntSGT, op1.LLVMValue, op2.LLVMValue, "cgt");
                    break;
                case ILOpcode.clt:
                    result = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntSLT, op1.LLVMValue, op2.LLVMValue, "clt");
                    break;
                case ILOpcode.cgt_un:
                    result = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntUGT, op1.LLVMValue, op2.LLVMValue, "cgt_un");
                    break;
                case ILOpcode.clt_un:
                    result = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntULT, op1.LLVMValue, op2.LLVMValue, "clt_un");
                    break;
                default:
                    throw new NotSupportedException(); // unreachable
            }

            PushExpression(kind, "", result);
        }

        private void ImportConvert(WellKnownType wellKnownType, bool checkOverflow, bool unsigned)
        {
        }

        private void ImportUnaryOperation(ILOpcode opCode)
        {
        }

        private void ImportCpOpj(int token)
        {
        }

        private void ImportUnbox(int token, ILOpcode opCode)
        {
        }

        private void ImportRefAnyVal(int token)
        {
        }

        private void ImportCkFinite()
        {
        }

        private void ImportMkRefAny(int token)
        {
        }

        private void ImportLdToken(int token)
        {
        }

        private void ImportLocalAlloc()
        {
        }

        private void ImportEndFilter()
        {
        }

        private void ImportCpBlk()
        {
        }

        private void ImportInitBlk()
        {
        }

        private void ImportRethrow()
        {
        }

        private void ImportSizeOf(int token)
        {
        }

        private void ImportRefAnyType()
        {
        }

        private void ImportArgList()
        {
        }

        private void ImportUnalignedPrefix(byte alignment)
        {
        }

        private void ImportVolatilePrefix()
        {
        }

        private void ImportTailPrefix()
        {
        }

        private void ImportConstrainedPrefix(int token)
        {
        }

        private void ImportNoPrefix(byte mask)
        {
        }

        private void ImportReadOnlyPrefix()
        {
        }

        private void ImportThrow()
        {
        }

        private void ImportLoadField(int token, bool isStatic)
        {
        }

        private void ImportAddressOfField(int token, bool isStatic)
        {
        }

        private void ImportStoreField(int token, bool isStatic)
        {
        }

        private void ImportLoadString(int token)
        {
        }

        private void ImportInitObj(int token)
        {
        }

        private void ImportBox(int token)
        {
        }

        private void ImportLeave(BasicBlock target)
        {
        }

        private void ImportNewArray(int token)
        {
        }

        private void ImportLoadElement(int token)
        {
        }

        private void ImportLoadElement(TypeDesc elementType)
        {
        }

        private void ImportStoreElement(int token)
        {
        }

        private void ImportStoreElement(TypeDesc elementType)
        {
        }

        private void ImportLoadLength()
        {
        }

        private void ImportAddressOfElement(int token)
        {
        }

        private void ImportEndFinally()
        {
        }

        private void ImportFallthrough(BasicBlock next)
        {
            EvaluationStack<StackEntry> entryStack = next.EntryStack;

            if (entryStack != null)
            {
                if (entryStack.Length != _stack.Length)
                    throw new InvalidProgramException();

                for (int i = 0; i < entryStack.Length; i++)
                {
                    // TODO: Do we need to allow conversions?
                    if (entryStack[i].Kind != _stack[i].Kind)
                        throw new InvalidProgramException();

                    if (entryStack[i].Kind == StackValueKind.ValueType)
                    {
                        if (entryStack[i].Type != _stack[i].Type)
                            throw new InvalidProgramException();
                    }
                }
            }
            else
            {
                if (_stack.Length > 0)
                {
                    entryStack = new EvaluationStack<StackEntry>(_stack.Length);

#pragma warning disable 162 // Due to not implement3ed exception incrementer in for needs pragma warning disable
                    for (int i = 0; i < _stack.Length; i++)
                    {
                        throw new NotImplementedException();
                        //entryStack.Push(NewSpillSlot(_stack[i]));
                    }
#pragma warning restore 162
                }
                next.EntryStack = entryStack;
            }

            if (entryStack != null)
            {
#pragma warning disable 162// Due to not implement3ed exception incrementer in for needs pragma warning disable
                for (int i = 0; i < entryStack.Length; i++)
                {
                    throw new NotImplementedException();
                    /*AppendLine();
                    Append(entryStack[i]);
                    Append(" = ");
                    Append(_stack[i]);
                    AppendSemicolon();*/
                }
#pragma warning restore 162
            }

            MarkBasicBlock(next);

        }

        private TypeDesc GetWellKnownType(WellKnownType wellKnownType)
        {
            return _compilation.TypeSystemContext.GetWellKnownType(wellKnownType);
        }

        private void ReportInvalidBranchTarget(int targetOffset)
        {
            ThrowHelper.ThrowInvalidProgramException();
        }

        private void ReportFallthroughAtEndOfMethod()
        {
            ThrowHelper.ThrowInvalidProgramException();
        }

        private void ReportInvalidInstruction(ILOpcode opcode)
        {
            ThrowHelper.ThrowInvalidProgramException();
        }

    }
}
