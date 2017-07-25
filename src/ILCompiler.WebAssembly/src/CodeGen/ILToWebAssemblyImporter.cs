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

            _curBasicBlock = LLVM.AppendBasicBlock(_llvmFunction, "Block" + basicBlock.StartOffset);
            LLVM.PositionBuilderAtEnd(_builder, _curBasicBlock);
        }

        private void EndImportingBasicBlock(BasicBlock basicBlock)
        {
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
        }

        private void ImportStoreVar(int index, bool argument)
        {
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
