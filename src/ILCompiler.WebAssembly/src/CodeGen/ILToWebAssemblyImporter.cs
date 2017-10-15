// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Internal.TypeSystem;
using ILCompiler;
using LLVMSharp;
using ILCompiler.CodeGen;
using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;

namespace Internal.IL
{
    // Implements an IL scanner that scans method bodies to be compiled by the code generation
    // backend before the actual compilation happens to gain insights into the code.
    partial class ILImporter
    {
        ArrayBuilder<object> _dependencies = new ArrayBuilder<object>();
        public IEnumerable<object> GetDependencies()
        {
            return _dependencies.ToArray();
        }

        public LLVMModuleRef Module { get; }
        private readonly MethodDesc _method;
        private readonly MethodIL _methodIL;
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
            public enum ImportState : byte
            {
                Unmarked,
                IsPending
            }

            public BasicBlock Next;

            public int StartOffset;
            public ImportState State = ImportState.Unmarked;

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
            _methodIL = methodIL;
            _ilBytes = methodIL.GetILBytes();
            _locals = methodIL.GetLocals();

            var ilExceptionRegions = methodIL.GetExceptionRegions();
            _exceptionRegions = new ExceptionRegion[ilExceptionRegions.Length];
            for (int i = 0; i < ilExceptionRegions.Length; i++)
            {
                _exceptionRegions[i] = new ExceptionRegion() { ILRegion = ilExceptionRegions[i] };
            }
            _llvmFunction = GetOrCreateLLVMFunction(mangledName);
            _builder = LLVM.CreateBuilder();
        }

        public void Import()
        {
            FindBasicBlocks();

            try
            {
                ImportBasicBlocks();
            }
            catch
            {
                // Change the function body to trap
                foreach (BasicBlock block in _basicBlocks)
                {
                    if (block != null && block.Block.Pointer != IntPtr.Zero)
                    {
                        LLVM.DeleteBasicBlock(block.Block);
                    }
                }
                LLVMBasicBlockRef trapBlock = LLVM.AppendBasicBlock(_llvmFunction, "Trap");
                LLVM.PositionBuilderAtEnd(_builder, trapBlock);
                if (TrapFunction.Pointer == IntPtr.Zero)
                {
                    TrapFunction = LLVM.AddFunction(Module, "llvm.trap", LLVM.FunctionType(LLVM.VoidType(), Array.Empty<LLVMTypeRef>(), false));
                }
                LLVM.BuildCall(_builder, TrapFunction, Array.Empty<LLVMValueRef>(), String.Empty);
                LLVM.BuildRetVoid(_builder);
                throw;
            }
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
            int paramOffset = GetTotalParameterOffset();
            for (int i = 0; i < totalLocalSize; i++)
            {
                var stackOffset = LLVM.BuildGEP(_builder, sp, new LLVMValueRef[] { LLVM.ConstInt(LLVM.Int32Type(), (ulong)(paramOffset + i), LLVMMisc.False) }, String.Empty);
                LLVM.BuildStore(_builder, LLVM.ConstInt(LLVM.Int8Type(), 0, LLVMMisc.False), stackOffset);
            }
        }

        private LLVMValueRef CreateLLVMFunction(string mangledName)
        {
            LLVMTypeRef universalSignature = LLVM.FunctionType(LLVM.VoidType(), new LLVMTypeRef[] { LLVM.PointerType(LLVM.Int8Type(), 0), LLVM.PointerType(LLVM.Int8Type(), 0) }, false);
            return LLVM.AddFunction(Module, mangledName , universalSignature);            
        }

        private LLVMValueRef GetOrCreateLLVMFunction(string mangledName)
        {
            LLVMValueRef llvmFunction = LLVM.GetNamedFunction(Module, mangledName);

            if(llvmFunction.Pointer == IntPtr.Zero)
            {
                return CreateLLVMFunction(mangledName);
            }
            return llvmFunction;
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

            switch (kind)
            {
                case StackValueKind.Int32:
                    {
                        if (!type.IsWellKnownType(WellKnownType.Int32)
                            && !type.IsWellKnownType(WellKnownType.IntPtr)
                            && !type.IsWellKnownType(WellKnownType.UInt32)
                            && !type.IsWellKnownType(WellKnownType.UIntPtr))
                        {
                            llvmValue = LLVM.BuildIntCast(_builder, llvmValue, LLVM.Int32Type(), "");
                        }
                    }
                    break;

                case StackValueKind.Int64:
                    {
                        if (!type.IsWellKnownType(WellKnownType.Int64)
                            && !(type.IsWellKnownType(WellKnownType.UInt64)))
                        {
                            llvmValue = LLVM.BuildIntCast(_builder, llvmValue, LLVM.Int64Type(), "");
                        }
                    }
                    break;

                case StackValueKind.NativeInt:
                    break;
            }

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
            int varBase;
            int varOffset;
            LLVMTypeRef valueType;
            TypeDesc type;

            if (argument)
            {
                varBase = 0;
                // todo: this is off by one for instance methods
                GetArgSizeAndOffsetAtIndex(index, out int argSize, out varOffset);
                valueType = GetLLVMTypeForTypeDesc(_method.Signature[index]);
                type = _method.Signature[index];
            }
            else
            {
                varBase = GetTotalParameterOffset();
                GetLocalSizeAndOffsetAtIndex(index, out int localSize, out varOffset);
                valueType = GetLLVMTypeForTypeDesc(_locals[index].Type);
                type = _locals[index].Type;
            }

            var loadLocation = LLVM.BuildGEP(_builder, LLVM.GetFirstParam(_llvmFunction),
                new LLVMValueRef[] { LLVM.ConstInt(LLVM.Int32Type(), (uint)(varBase + varOffset), LLVMMisc.False) },
                String.Empty);
            var typedLoadLocation = LLVM.BuildPointerCast(_builder, loadLocation, LLVM.PointerType(valueType, 0), String.Empty);
            var loadResult = LLVM.BuildLoad(_builder, typedLoadLocation, "ld" + (argument ? "arg" : "loc") + index + "_");

            PushExpression(GetStackValueKind(type), String.Empty, loadResult, type);
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

            ImportStoreHelper(toStore, valueType, LLVM.GetFirstParam(_llvmFunction), (uint)(GetTotalParameterOffset() + localOffset));
        }

        private void ImportStoreHelper(LLVMValueRef toStore, LLVMTypeRef valueType, LLVMValueRef basePtr, uint offset)
        {
            LLVMTypeKind toStoreKind = LLVM.GetTypeKind(LLVM.TypeOf(toStore));
            LLVMTypeKind valueTypeKind = LLVM.GetTypeKind(valueType);

            LLVMValueRef typedToStore = toStore;
            if(toStoreKind == LLVMTypeKind.LLVMPointerTypeKind && valueTypeKind == LLVMTypeKind.LLVMPointerTypeKind)
            {
                typedToStore = LLVM.BuildPointerCast(_builder, toStore, valueType, "storePtrCast");
            }
            else if(toStoreKind == LLVMTypeKind.LLVMPointerTypeKind && valueTypeKind != LLVMTypeKind.LLVMPointerTypeKind)
            {
                typedToStore = LLVM.BuildPtrToInt(_builder, toStore, valueType, "storeIntCast");
            }
            else if (toStoreKind != LLVMTypeKind.LLVMPointerTypeKind && valueTypeKind == LLVMTypeKind.LLVMPointerTypeKind)
            {
                typedToStore = LLVM.BuildIntToPtr(_builder, toStore, valueType, "storePtrCast");
            }
            else
            {
                Debug.Assert(toStoreKind != LLVMTypeKind.LLVMPointerTypeKind && valueTypeKind != LLVMTypeKind.LLVMPointerTypeKind);
                typedToStore = LLVM.BuildIntCast(_builder, toStore, valueType, "storeIntCast");
            }
            
            var storeLocation = LLVM.BuildGEP(_builder, basePtr,
                new LLVMValueRef[] { LLVM.ConstInt(LLVM.Int32Type(), offset, LLVMMisc.False) },
                String.Empty);
            var typedStoreLocation = LLVM.BuildPointerCast(_builder, storeLocation, LLVM.PointerType(valueType, 0), String.Empty);
            LLVM.BuildStore(_builder, typedToStore, typedStoreLocation);
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
                    return LLVM.Int32Type();

                case TypeFlags.Class:
                case TypeFlags.Interface:
                    return LLVM.PointerType(LLVM.Int8Type(), 0);

                case TypeFlags.Array:
                case TypeFlags.SzArray:
                case TypeFlags.ByRef:
                    return LLVM.Int32Type();

                case TypeFlags.Pointer:
                    return LLVM.PointerType(GetLLVMTypeForTypeDesc(type.GetParameterType()), 0);

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

                case TypeFlags.Void:
                    return LLVM.VoidType();

                default:
                    throw new NotImplementedException(type.Category.ToString());
            }
        }

        private int GetTotalLocalOffset()
        {
            int offset = 0;
            for (int i = 0; i < _locals.Length; i++)
            {
                offset += _locals[i].Type.GetElementSize().AsInt;
            }
            return offset;
        }

        private int GetTotalParameterOffset()
        {
            int offset = 0;
            for (int i = 0; i < _method.Signature.Length; i++)
            {
                offset += _method.Signature[i].GetElementSize().AsInt;
            }
            return offset;
        }

        private void GetArgSizeAndOffsetAtIndex(int index, out int size, out int offset)
        {
            var argType = _method.Signature[index];
            size = argType.GetElementSize().AsInt;

            offset = 0;
            for (int i = 0; i < index; i++)
            {
                offset += _method.Signature[i].GetElementSize().AsInt;
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
            if (argument)
            {
                throw new NotImplementedException("ldarga");
            }

            int localOffset = GetTotalParameterOffset();
            GetLocalSizeAndOffsetAtIndex(index, out int size, out int offset);
            localOffset += offset;

            var localPtr = LLVM.BuildGEP(_builder, LLVM.GetFirstParam(_llvmFunction),
                new LLVMValueRef[] { LLVM.ConstInt(LLVM.Int32Type(), (uint)localOffset, LLVMMisc.False) }, "ldloca");
            //var typedLocalPtr = LLVM.BuildPointerCast(_builder, localPtr, GetLLVMTypeForTypeDesc(_locals[index].Type.MakePointerType()), "ldloca");

            _stack.Push(new ExpressionEntry(StackValueKind.NativeInt, "ldloca", localPtr, _locals[index].Type.MakePointerType()));
        }

        private void ImportDup()
        {
            _stack.Push(_stack.Peek().Duplicate());
        }

        private void ImportPop()
        {
            _stack.Pop();
        }

        private void ImportJmp(int token)
        {
        }

        private void ImportCasting(ILOpcode opcode, int token)
        {
        }

        private void ImportLoadNull()
        {
            _stack.Push(new ExpressionEntry(StackValueKind.ObjRef, "null", LLVM.ConstInt(LLVM.Int32Type(), 0, LLVMMisc.False)));
        }

        private void ImportReturn()
        {
            if(_method.Signature.ReturnType != GetWellKnownType(WellKnownType.Void))
            {
                StackEntry retVal = _stack.Pop();
                LLVMTypeRef valueType = GetLLVMTypeForTypeDesc(_method.Signature.ReturnType);

                ImportStoreHelper(retVal.LLVMValue, valueType, LLVM.GetNextParam(LLVM.GetFirstParam(_llvmFunction)), 0);
            }

            LLVM.BuildRetVoid(_builder);
        }

        private void ImportCall(ILOpcode opcode, int token)
        {
            MethodDesc callee = (MethodDesc)_methodIL.GetObject(token);

            if (callee.IsIntrinsic)
            {
                if (ImportIntrinsicCall(callee))
                {
                    return;
                }
            }

            if (callee.IsPInvoke)
            {
                ImportRawPInvoke(callee);
                return;
            }

            // we don't really have virtual call support, but we'll treat it as direct for now
            if (opcode != ILOpcode.call && opcode !=  ILOpcode.callvirt)
            {
                throw new NotImplementedException();
            }
            HandleCall(callee);
        }

        /// <summary>
        /// Implements intrinsic methods instread of calling them
        /// </summary>
        /// <returns>True if the method was implemented</returns>
        private bool ImportIntrinsicCall(MethodDesc method)
        {
            Debug.Assert(method.IsIntrinsic);

            if (!(method.OwningType is MetadataType metadataType))
            {
                return false;
            }

            switch (method.Name)
            {
                // Workaround for not being able to build a WASM version of CoreLib. This method
                // would return the x64 size, which is too large for WASM
                case "get_OffsetToStringData":
                    if (metadataType.Name == "RuntimeHelpers" && metadataType.Namespace == "System.Runtime.CompilerServices")
                    {
                        _stack.Push(new Int32ConstantEntry(8, _method.Context.GetWellKnownType(WellKnownType.Int32)));
                        return true;
                    }
                    break;
            }

            return false;
        }

        private void HandleCall(MethodDesc callee)
        { 
            AddMethodReference(callee);
            string calleeName = _compilation.NameMangler.GetMangledMethodName(callee).ToString();
            LLVMValueRef fn = GetOrCreateLLVMFunction(calleeName);

            int offset = GetTotalParameterOffset() + GetTotalLocalOffset() + callee.Signature.ReturnType.GetElementSize().AsInt;

            LLVMValueRef shadowStack = LLVM.BuildGEP(_builder, LLVM.GetFirstParam(_llvmFunction),
                new LLVMValueRef[] { LLVM.ConstInt(LLVM.Int32Type(), (uint)offset, LLVMMisc.False) },
                String.Empty);
            var castShadowStack = LLVM.BuildPointerCast(_builder, shadowStack, LLVM.PointerType(LLVM.Int8Type(), 0), String.Empty);

            int returnOffset = GetTotalParameterOffset() + GetTotalLocalOffset();
            var returnAddress = LLVM.BuildGEP(_builder, LLVM.GetFirstParam(_llvmFunction),
                new LLVMValueRef[] { LLVM.ConstInt(LLVM.Int32Type(), (uint)returnOffset, LLVMMisc.False) },
                String.Empty);
            var castReturnAddress = LLVM.BuildPointerCast(_builder, returnAddress, LLVM.PointerType(LLVM.Int8Type(), 0), String.Empty);

            // argument offset
            uint argOffset = 0;

            // The last argument is the top of the stack. We need to reverse them and store starting at the first argument
            LLVMValueRef[] argumentValues = new LLVMValueRef[callee.Signature.Length];
            for(int i = 0; i < argumentValues.Length; i++)
            {
                argumentValues[argumentValues.Length - i - 1] = _stack.Pop().LLVMValue;
            }

            for (int index = 0; index < argumentValues.Length; index++)
            {
                LLVMValueRef toStore = argumentValues[index];

                LLVMTypeRef valueType = GetLLVMTypeForTypeDesc(callee.Signature[index]);

                ImportStoreHelper(toStore, valueType, castShadowStack, argOffset);

                argOffset += (uint) callee.Signature[index].GetElementSize().AsInt;
            }

            LLVM.BuildCall(_builder, fn, new LLVMValueRef[] {
                castShadowStack,
                castReturnAddress}, string.Empty);

            
            if (!callee.Signature.ReturnType.IsVoid)
            {
                LLVMTypeRef returnLLVMType = GetLLVMTypeForTypeDesc(callee.Signature.ReturnType);
                LLVMValueRef returnLLVMPointer = LLVM.BuildPointerCast(_builder, returnAddress, LLVM.PointerType(returnLLVMType, 0), String.Empty);
                LLVMValueRef loadResult = LLVM.BuildLoad(_builder, returnLLVMPointer, String.Empty);
                PushExpression(GetStackValueKind(callee.Signature.ReturnType), String.Empty, loadResult, callee.Signature.ReturnType);
            }
        }

        private void AddMethodReference(MethodDesc method)
        {
            _dependencies.Add(_compilation.NodeFactory.MethodEntrypoint(method));
        }

        private void ImportRawPInvoke(MethodDesc method)
        {
            LLVMValueRef nativeFunc = LLVM.GetNamedFunction(Module, method.Name);

            // Create an import if we haven't already
            if (nativeFunc.Pointer == IntPtr.Zero)
            {
                // Set up native parameter types
                LLVMTypeRef[] paramTypes = new LLVMTypeRef[method.Signature.Length];
                for (int i = 0; i < paramTypes.Length; i++)
                {
                    paramTypes[i] = GetLLVMTypeForTypeDesc(method.Signature[i]);
                }

                // Define the full signature
                LLVMTypeRef nativeFuncType = LLVM.FunctionType(GetLLVMTypeForTypeDesc(method.Signature.ReturnType), paramTypes, LLVMMisc.False);

                nativeFunc = LLVM.AddFunction(Module, method.Name, nativeFuncType);
                LLVM.SetLinkage(nativeFunc, LLVMLinkage.LLVMDLLImportLinkage);
            }

            LLVMValueRef[] arguments = new LLVMValueRef[method.Signature.Length];
            for(int i = 0; i < arguments.Length; i++)
            {
                LLVMValueRef argValue = _stack.Pop().LLVMValue;

                // Arguments are reversed on the stack
                // Coerce pointers to the native type
                TypeDesc signatureType = method.Signature[arguments.Length - i - 1];
                LLVMValueRef typedValue = argValue;
                if (signatureType.IsPointer)
                {
                    LLVMTypeRef signatureLlvmType = GetLLVMTypeForTypeDesc(signatureType);
                    typedValue = LLVM.BuildPointerCast(_builder, argValue, signatureLlvmType, String.Empty);
                }
                arguments[arguments.Length - i - 1] = typedValue;
            }

            var returnValue = LLVM.BuildCall(_builder, nativeFunc, arguments, "call");

            // TODO: void returns
            PushExpression(GetStackValueKind(method.Signature.ReturnType), String.Empty, returnValue, method.Signature.ReturnType);
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
                    _stack.Push(new Int32ConstantEntry((int)value, _method.Context.GetWellKnownType(WellKnownType.Int32)));
                    break;

                case StackValueKind.Int64:
                    _stack.Push(new Int64ConstantEntry(value, _method.Context.GetWellKnownType(WellKnownType.Int64)));
                    break;

                default:
                    throw new InvalidOperationException(kind.ToString());
            }           

        }

        private void ImportLoadFloat(double value)
        {
            _stack.Push(new FloatConstantEntry(value, _method.Context.GetWellKnownType(WellKnownType.Double)));
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

                if (opcode == ILOpcode.brfalse || opcode == ILOpcode.brtrue)
                {
                    var op = _stack.Pop();
                    LLVMValueRef value = op.LLVMValue;
                    if (LLVM.GetTypeKind(LLVM.TypeOf(value)) == LLVMTypeKind.LLVMPointerTypeKind)
                    {
                        value = LLVM.BuildPtrToInt(_builder, value, LLVM.Int32Type(), String.Empty);
                    }

                    if (opcode == ILOpcode.brfalse)
                    {
                        condition = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntEQ, value, LLVM.ConstInt(LLVM.TypeOf(value), 0, LLVMMisc.False), "brfalse");
                    }
                    else
                    {
                        condition = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntNE, value, LLVM.ConstInt(LLVM.TypeOf(value), 0, LLVMMisc.False), "brtrue");
                    }
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
            ImportLoadIndirect(ResolveTypeToken(token));
        }

        private void ImportLoadIndirect(TypeDesc type)
        {
            StackEntry pointer = _stack.Pop();
            LLVMTypeRef loadType = GetLLVMTypeForTypeDesc(type);
            LLVMTypeRef pointerType = LLVM.PointerType(loadType, 0);

            LLVMValueRef typedPointer;
            if (LLVM.GetTypeKind(LLVM.TypeOf(pointer.LLVMValue)) != LLVMTypeKind.LLVMPointerTypeKind)
            {
                typedPointer = LLVM.BuildIntToPtr(_builder, pointer.LLVMValue, pointerType, "ldindintptrcast");
            }
            else
            {
                typedPointer = LLVM.BuildPointerCast(_builder, pointer.LLVMValue, pointerType, "ldindptrcast");
            }

            LLVMValueRef load = LLVM.BuildLoad(_builder, typedPointer, "ldind");
            PushExpression(GetStackValueKind(type), "ldlind", load, type);
        }

        private void ImportStoreIndirect(int token)
        {
            ImportStoreIndirect(ResolveTypeToken(token));
        }

        private void ImportStoreIndirect(TypeDesc type)
        {
            StackEntry value = _stack.Pop();
            StackEntry destinationPointer = _stack.Pop();
            LLVMTypeRef requestedPointerType = LLVM.PointerType(GetLLVMTypeForTypeDesc(type), 0);
            LLVMValueRef typedValue = value.LLVMValue;
            LLVMValueRef typedPointer = destinationPointer.LLVMValue;

            if (LLVM.GetTypeKind(LLVM.TypeOf(destinationPointer.LLVMValue)) != LLVMTypeKind.LLVMPointerTypeKind)
            {
                typedPointer = LLVM.BuildIntToPtr(_builder, destinationPointer.LLVMValue, requestedPointerType, "stindintptrcast");
            }
            else
            {
                typedPointer = LLVM.BuildPointerCast(_builder, destinationPointer.LLVMValue, requestedPointerType, "stindptrcast");
            }

            if (value.Type != type)
            {
                if (LLVM.GetTypeKind(GetLLVMTypeForTypeDesc(value.Type)) != LLVMTypeKind.LLVMPointerTypeKind)
                {
                    typedValue = LLVM.BuildIntCast(_builder, typedValue, GetLLVMTypeForTypeDesc(type), "stindvalcast");
                }
                else
                {
                    typedValue = LLVM.BuildPointerCast(_builder, typedValue, GetLLVMTypeForTypeDesc(type), "stindvalptrcast");
                }
            }

            LLVM.BuildStore(_builder, typedValue, typedPointer);
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
                    // TODO: converting these to ints should also happen for sub and some other operations
                    LLVMValueRef left = op1.LLVMValue;
                    LLVMValueRef right = op2.LLVMValue;

                    if (kind == StackValueKind.NativeInt || kind == StackValueKind.ObjRef || kind == StackValueKind.ByRef)
                    {
                        if(LLVM.GetTypeKind(LLVM.TypeOf(left)) == LLVMTypeKind.LLVMPointerTypeKind)
                        {
                            left = LLVM.BuildPtrToInt(_builder, left, LLVM.Int32Type(), "lptrasint");
                        }
                        if (LLVM.GetTypeKind(LLVM.TypeOf(right)) == LLVMTypeKind.LLVMPointerTypeKind)
                        {
                            right = LLVM.BuildPtrToInt(_builder, right, LLVM.Int32Type(), "rptrasint");
                        }
                    }
                    result = LLVM.BuildAdd(_builder, left, right, "add");
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
            LLVMValueRef result;

            StackEntry numBitsToShift = _stack.Pop();
            StackEntry valueToShift = _stack.Pop();

            switch (opcode)
            {
                case ILOpcode.shl:
                    result = LLVM.BuildShl(_builder, valueToShift.LLVMValue, numBitsToShift.LLVMValue, "shl");
                    break;
                case ILOpcode.shr:
                    result = LLVM.BuildAShr(_builder, valueToShift.LLVMValue, numBitsToShift.LLVMValue, "shr");
                    break;
                case ILOpcode.shr_un:
                    result = LLVM.BuildLShr(_builder, valueToShift.LLVMValue, numBitsToShift.LLVMValue, "shr");
                    break;
                default:
                    throw new InvalidOperationException(); // Should be unreachable
            }

            PushExpression(valueToShift.Kind, "", result, valueToShift.Type);
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
                    result = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntEQ, op2.LLVMValue, op1.LLVMValue, "ceq");
                    break;
                case ILOpcode.cgt:
                    result = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntSGT, op2.LLVMValue, op1.LLVMValue, "cgt");
                    break;
                case ILOpcode.clt:
                    result = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntSLT, op2.LLVMValue, op1.LLVMValue, "clt");
                    break;
                case ILOpcode.cgt_un:
                    result = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntUGT, op2.LLVMValue, op1.LLVMValue, "cgt_un");
                    break;
                case ILOpcode.clt_un:
                    result = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntULT, op2.LLVMValue, op1.LLVMValue, "clt_un");
                    break;
                default:
                    throw new NotSupportedException(); // unreachable
            }

            PushExpression(kind, "", result, GetWellKnownType(WellKnownType.SByte));
        }

        private void ImportConvert(WellKnownType wellKnownType, bool checkOverflow, bool unsigned)
        {
            StackEntry value = _stack.Pop();
            StackEntry convertedValue = value.Duplicate();
            //conv.u for a pointer should change to a int8*
            if(wellKnownType == WellKnownType.UIntPtr)
            {
                if (value.Kind == StackValueKind.Int32)
                {
                    convertedValue.LLVMValue = LLVM.BuildIntToPtr(_builder, value.LLVMValue, LLVM.PointerType(LLVM.Int8Type(), 0), "conv.u");
                }
            }

            _stack.Push(convertedValue);
        }

        private void ImportUnaryOperation(ILOpcode opCode)
        {
            var argument = _stack.Pop();
             
            LLVMValueRef result;
            switch (opCode)
            {
                case ILOpcode.neg:
                    if (argument.Kind == StackValueKind.Float)
                    {
                        result = LLVM.BuildFNeg(_builder, argument.LLVMValue, "neg");
                    }   
                    else
                    {
                        result = LLVM.BuildNeg(_builder, argument.LLVMValue, "neg");
                    }
                    break;
                case ILOpcode.not:
                    result = LLVM.BuildNot(_builder, argument.LLVMValue, "not");
                    break;
                default:
                    throw new NotSupportedException(); // unreachable
            }

            PushExpression(argument.Kind, "", result, argument.Type);
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
            var ldtokenValue = _methodIL.GetObject(token);
            WellKnownType ldtokenKind;
            string name;
            StackEntry value;
            if (ldtokenValue is TypeDesc)
            {
                ldtokenKind = WellKnownType.RuntimeTypeHandle;
                //AddTypeReference((TypeDesc)ldtokenValue, false);

                // todo: this doesn't work because we don't have the eetypeptr pushed. How do we get the eetypeptr?
                MethodDesc helper = _compilation.TypeSystemContext.GetHelperEntryPoint("LdTokenHelpers", "GetRuntimeTypeHandle");
                //AddMethodReference(helper);
                HandleCall(helper);
                name = ldtokenValue.ToString();

                //value = new LdTokenEntry<TypeDesc>(StackValueKind.ValueType, name, (TypeDesc)ldtokenValue, GetWellKnownType(ldtokenKind));
            }
            else if (ldtokenValue is FieldDesc)
            {
                ldtokenKind = WellKnownType.RuntimeFieldHandle;
                // todo: this is probably the wrong llvm value for the field
                value = new LdTokenEntry<FieldDesc>(StackValueKind.ValueType, null, (FieldDesc)ldtokenValue, LLVM.ConstInt(LLVM.Int32Type(), (uint)token, LLVMMisc.False), GetWellKnownType(ldtokenKind));
                _stack.Push(value);
            }
            else if (ldtokenValue is MethodDesc)
            {
                throw new NotImplementedException();
            }
            else
            {
                throw new InvalidOperationException();
            }
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
            if(isStatic)
            {
                throw new NotImplementedException("static stfld");
            }

            FieldDesc field = (FieldDesc)_methodIL.GetObject(token);

            StackEntry valueEntry = _stack.Pop();
            StackEntry objectEntry = _stack.Pop();

            LLVMValueRef value = valueEntry.LLVMValue;

            // All integers are int32 on the stack, but need to be resized to fit fields
            if(valueEntry.Kind == StackValueKind.Int32)
            {
                value = LLVM.BuildIntCast(_builder, value, GetLLVMTypeForTypeDesc(field.FieldType), "intfieldcast");
            }

            var untypedObjectPointer = LLVM.BuildPointerCast(_builder, objectEntry.LLVMValue, LLVM.PointerType(LLVMTypeRef.Int8Type(), 0), "stfld");
            var storeLocation = LLVM.BuildGEP(_builder, untypedObjectPointer,
                new LLVMValueRef[] { LLVM.ConstInt(LLVM.Int32Type(), (ulong)field.Offset.AsInt, LLVMMisc.False) }, "stfld");
            var typedStoreLocation = LLVM.BuildPointerCast(_builder, storeLocation, LLVM.PointerType(GetLLVMTypeForTypeDesc(field.FieldType), 0), "stfld");
            LLVM.BuildStore(_builder, value, typedStoreLocation);
        }

        // Loads symbol address. Address is represented as a i32*
        private LLVMValueRef LoadAddressOfSymbolNode(ISymbolNode node)
        {
            LLVMValueRef addressOfAddress = WebAssemblyObjectWriter.GetSymbolValuePointer(Module, node, _compilation.NameMangler, false);
            //return addressOfAddress;
            return LLVM.BuildLoad(_builder, addressOfAddress, "LoadAddressOfSymbolNode");
        }

        private void ImportLoadString(int token)
        {
            TypeDesc stringType = this._compilation.TypeSystemContext.GetWellKnownType(WellKnownType.String);

            string str = (string)_methodIL.GetObject(token);
            ISymbolNode node = _compilation.NodeFactory.SerializedStringObject(str);
            LLVMValueRef stringDataPointer = LoadAddressOfSymbolNode(node);
            _dependencies.Add(node);
            _stack.Push(new ExpressionEntry(GetStackValueKind(stringType), String.Empty, stringDataPointer, stringType));
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
                        // todo: do we need anything special for spilled stacks like cpp codegen does?
                        entryStack.Push(_stack[i]);
                        //entryStack.Push(NewSpillSlot(_stack[i]));
                    }
#pragma warning restore 162
                }
                next.EntryStack = entryStack;
            }

            if (entryStack != null)
            {
                // todo: do we have to do anything here?
#pragma warning disable 162// Due to not implement3ed exception incrementer in for needs pragma warning disable
                for (int i = 0; i < entryStack.Length; i++)
                {
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

        private TypeDesc ResolveTypeToken(int token)
        {
            return (TypeDesc)_methodIL.GetObject(token);
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
