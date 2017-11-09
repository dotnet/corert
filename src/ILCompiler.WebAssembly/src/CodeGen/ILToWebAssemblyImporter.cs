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
        public enum LocalVarKind
        {
            Argument,
            Local,
            Temp
        }

        ArrayBuilder<object> _dependencies = new ArrayBuilder<object>();
        public IEnumerable<object> GetDependencies()
        {
            return _dependencies.ToArray();
        }

        public LLVMModuleRef Module { get; }
        private readonly MethodDesc _method;
        private readonly MethodIL _methodIL;
        private readonly MethodSignature _signature;
        private readonly TypeDesc _thisType;
        private readonly WebAssemblyCodegenCompilation _compilation;
        private LLVMValueRef _llvmFunction;
        private LLVMBasicBlockRef _curBasicBlock;
        private LLVMBuilderRef _builder;
        private readonly LocalVariableDefinition[] _locals;
        private List<SpilledExpressionEntry> _spilledExpressions = new List<SpilledExpressionEntry>();

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
            _signature = method.Signature;
            _thisType = method.OwningType;

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
                EmitTrapCall();
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

        private LLVMValueRef GetOrCreateLLVMFunction(string mangledName, LLVMTypeRef functionType)
        {
            LLVMValueRef llvmFunction = LLVM.GetNamedFunction(Module, mangledName);

            if (llvmFunction.Pointer == IntPtr.Zero)
            {
                return LLVM.AddFunction(Module, mangledName, functionType);
            }
            return llvmFunction;
        }

        private void ImportCallMemset(LLVMValueRef targetPointer, byte value, int length)
        {
            LLVMValueRef objectSizeValue = BuildConstInt32(length);
            var memsetSignature = LLVM.FunctionType(LLVM.VoidType(), new LLVMTypeRef[] { LLVM.PointerType(LLVM.Int8Type(), 0), LLVM.Int8Type(), LLVM.Int32Type(), LLVM.Int32Type(), LLVM.Int1Type() }, false);
            LLVM.BuildCall(_builder, GetOrCreateLLVMFunction("llvm.memset.p0i8.i32", memsetSignature), new LLVMValueRef[] { targetPointer, BuildConstInt8(value), objectSizeValue, BuildConstInt32(1), BuildConstInt1(0) }, String.Empty);
        }

        private void PushLoadExpression(StackValueKind kind, string name, LLVMValueRef rawLLVMValue, TypeDesc type)
        {
            Debug.Assert(kind != StackValueKind.Unknown, "Unknown stack kind");
            _stack.Push(new LoadExpressionEntry(kind, name, rawLLVMValue, type));
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
                if (_basicBlocks[_currentOffset].StartOffset == 0)
                    throw new InvalidProgramException();
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
            EmitDoNothingCall();
        }

        private void ImportBreak()
        {
        }

        private void ImportLoadVar(int index, bool argument)
        {
            LLVMValueRef typedLoadLocation = LoadVarAddress(index, argument ? LocalVarKind.Argument : LocalVarKind.Local, out TypeDesc type);
            PushLoadExpression(GetStackValueKind(type), "ld" + (argument ? "arg" : "loc") + index + "_", typedLoadLocation, type);
        }

        private LLVMValueRef LoadTemp(int index)
        {
            LLVMValueRef address = LoadVarAddress(index, LocalVarKind.Temp, out TypeDesc type);
            return LLVM.BuildLoad(_builder, CastToPointerToTypeDesc(address, type), "ldtemp"); 
        }

        internal LLVMValueRef LoadTemp(int index, LLVMTypeRef asType)
        {
            LLVMValueRef address = LoadVarAddress(index, LocalVarKind.Temp, out TypeDesc type);
            return LLVM.BuildLoad(_builder, CastIfNecessary(address, LLVM.PointerType(asType, 0)), "ldtemp");
        }

        private void StoreTemp(int index, LLVMValueRef value)
        {
            LLVMValueRef address = LoadVarAddress(index, LocalVarKind.Temp, out TypeDesc type);
            LLVM.BuildStore(_builder, CastToTypeDesc(value, type), CastToPointerToTypeDesc(address, type));
        }

        internal static LLVMValueRef LoadValue(LLVMBuilderRef builder, LLVMValueRef address, TypeDesc sourceType, LLVMTypeRef targetType, bool signExtend)
        {
            if (targetType.TypeKind == LLVMTypeKind.LLVMIntegerTypeKind && sourceType.IsPrimitive && !sourceType.IsPointer)
            {
                var sourceLLVMType = ILImporter.GetLLVMTypeForTypeDesc(sourceType);
                var typedAddress = CastIfNecessary(builder, address, LLVM.PointerType(sourceLLVMType, 0));
                return CastIntValue(builder, LLVM.BuildLoad(builder, typedAddress, "ldvalue"), targetType, signExtend);
            }
            else
            {
                var typedAddress = CastIfNecessary(builder, address, LLVM.PointerType(targetType, 0));
                return LLVM.BuildLoad(builder, typedAddress, "ldvalue");
            }
        }

        private static LLVMValueRef CastIntValue(LLVMBuilderRef builder, LLVMValueRef value, LLVMTypeRef type, bool signExtend)
        {
            if (LLVM.TypeOf(value).Pointer == type.Pointer)
            {
                return value;
            }
            else if (LLVM.TypeOf(value).TypeKind == LLVMTypeKind.LLVMPointerTypeKind)
            {
                return LLVM.BuildPtrToInt(builder, value, type, "intcast");
            }
            else if (signExtend && type.GetIntTypeWidth() > LLVM.TypeOf(value).GetIntTypeWidth())
            {
                return LLVM.BuildSExtOrBitCast(builder, value, type, "SExtOrBitCast");
            }
            else
            {
                Debug.Assert(LLVM.TypeOf(value).TypeKind == LLVMTypeKind.LLVMIntegerTypeKind);
                return LLVM.BuildIntCast(builder, value, type, "intcast");
            }
        }

        private LLVMValueRef LoadVarAddress(int index, LocalVarKind kind, out TypeDesc type)
        {
            int varBase;
            int varCountBase;
            int varOffset;
            LLVMTypeRef valueType;

            if (kind == LocalVarKind.Argument)
            {
                varCountBase = 0;
                varBase = 0;
                if (!_signature.IsStatic)
                {
                    varCountBase = 1;
                }

                GetArgSizeAndOffsetAtIndex(index, out int argSize, out varOffset);

                if (!_signature.IsStatic && index == 0)
                {
                    type = _thisType;
                    if (type.IsValueType)
                    {
                        type = type.MakeByRefType();
                    }
                }
                else
                {
                    type = _signature[index - varCountBase];
                }
                valueType = GetLLVMTypeForTypeDesc(type);
            }
            else if (kind == LocalVarKind.Local)
            {
                varBase = GetTotalParameterOffset();
                GetLocalSizeAndOffsetAtIndex(index, out int localSize, out varOffset);
                valueType = GetLLVMTypeForTypeDesc(_locals[index].Type);
                type = _locals[index].Type;
            }
            else
            {
                varBase = GetTotalRealLocalOffset();
                GetSpillSizeAndOffsetAtIndex(index, out int localSize, out varOffset);
                valueType = GetLLVMTypeForTypeDesc(_spilledExpressions[index].Type);
                type = _spilledExpressions[index].Type;
            }

            return LLVM.BuildGEP(_builder, LLVM.GetFirstParam(_llvmFunction),
                new LLVMValueRef[] { LLVM.ConstInt(LLVM.Int32Type(), (uint)(varBase + varOffset), LLVMMisc.False) },
                String.Empty);

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
            TypeDesc varType;
            StackEntry toStore = _stack.Pop();
            LLVMValueRef varAddress = LoadVarAddress(index, argument ? LocalVarKind.Argument : LocalVarKind.Local, out varType);
            CastingStore(varAddress, toStore, varType);
        }

        private void ImportStoreHelper(LLVMValueRef toStore, LLVMTypeRef valueType, LLVMValueRef basePtr, uint offset)
        {
            LLVMValueRef typedToStore = CastIfNecessary(toStore, valueType);
            
            var storeLocation = LLVM.BuildGEP(_builder, basePtr,
                new LLVMValueRef[] { LLVM.ConstInt(LLVM.Int32Type(), offset, LLVMMisc.False) },
                String.Empty);
            var typedStoreLocation = CastIfNecessary(storeLocation, LLVM.PointerType(valueType, 0));
            LLVM.BuildStore(_builder, typedToStore, typedStoreLocation);
        }

        private LLVMValueRef CastToRawPointer(LLVMValueRef source)
        {
            return CastIfNecessary(source, LLVM.PointerType(LLVM.Int8Type(), 0));
        }

        private LLVMValueRef CastToTypeDesc(LLVMValueRef source, TypeDesc type)
        {
            return CastIfNecessary(source, GetLLVMTypeForTypeDesc(type));
        }

        private LLVMValueRef CastToPointerToTypeDesc(LLVMValueRef source, TypeDesc type)
        {
            return CastIfNecessary(source, LLVM.PointerType(GetLLVMTypeForTypeDesc(type), 0));
        }

        private void CastingStore(LLVMValueRef address, StackEntry value, TypeDesc targetType)
        {
            var typedStoreLocation = CastToPointerToTypeDesc(address, targetType);
            LLVM.BuildStore(_builder, value.ValueAsType(targetType, _builder), typedStoreLocation);
        }

        private LLVMValueRef CastIfNecessary(LLVMValueRef source, LLVMTypeRef valueType)
        {
            return CastIfNecessary(_builder, source, valueType);
        }

        internal static LLVMValueRef CastIfNecessary(LLVMBuilderRef builder, LLVMValueRef source, LLVMTypeRef valueType)
        {
            LLVMTypeRef sourceType = LLVM.TypeOf(source);
            if (sourceType.Pointer == valueType.Pointer)
                return source;

            LLVMTypeKind toStoreKind = LLVM.GetTypeKind(LLVM.TypeOf(source));
            LLVMTypeKind valueTypeKind = LLVM.GetTypeKind(valueType);

            LLVMValueRef typedToStore = source;
            if (toStoreKind == LLVMTypeKind.LLVMPointerTypeKind && valueTypeKind == LLVMTypeKind.LLVMPointerTypeKind)
            {
                typedToStore = LLVM.BuildPointerCast(builder, source, valueType, "CastIfNecessaryPtr");
            }
            else if (toStoreKind == LLVMTypeKind.LLVMPointerTypeKind && valueTypeKind == LLVMTypeKind.LLVMIntegerTypeKind)
            {
                typedToStore = LLVM.BuildPtrToInt(builder, source, valueType, "CastIfNecessaryInt");
            }
            else if (toStoreKind == LLVMTypeKind.LLVMIntegerTypeKind && valueTypeKind == LLVMTypeKind.LLVMArrayTypeKind)
            {
                typedToStore = LLVM.BuildLoad(builder, CastIfNecessary(builder, source, LLVM.PointerType(valueType, 0)), "CastIfNecessaryArrayLoad");
            }
            else if (toStoreKind == LLVMTypeKind.LLVMPointerTypeKind && valueTypeKind == LLVMTypeKind.LLVMArrayTypeKind)
            {
                typedToStore = LLVM.BuildLoad(builder, CastIfNecessary(builder, source, LLVM.PointerType(valueType, 0)), "CastIfNecessaryArrayLoad");
            }
            else if (toStoreKind == LLVMTypeKind.LLVMPointerTypeKind && valueTypeKind != LLVMTypeKind.LLVMIntegerTypeKind)
            {
                throw new NotImplementedException($"trying to cast {toStoreKind} to {valueTypeKind}");
            }
            else if (toStoreKind == LLVMTypeKind.LLVMIntegerTypeKind && valueTypeKind == LLVMTypeKind.LLVMPointerTypeKind)
            {
                typedToStore = LLVM.BuildIntToPtr(builder, source, valueType, "CastIfNecessaryPtr");
            }
            else if (toStoreKind != LLVMTypeKind.LLVMIntegerTypeKind && valueTypeKind == LLVMTypeKind.LLVMPointerTypeKind)
            {
                throw new NotImplementedException($"trying to cast {toStoreKind} to {valueTypeKind}");
            }
            else if (toStoreKind != valueTypeKind && toStoreKind != LLVMTypeKind.LLVMIntegerTypeKind && valueTypeKind != LLVMTypeKind.LLVMIntegerTypeKind)
            {
                throw new NotImplementedException($"trying to cast {toStoreKind} to {valueTypeKind}");
            }
            else if (toStoreKind == valueTypeKind && toStoreKind == LLVMTypeKind.LLVMIntegerTypeKind)
            {
                Debug.Assert(toStoreKind != LLVMTypeKind.LLVMPointerTypeKind && valueTypeKind != LLVMTypeKind.LLVMPointerTypeKind);
                typedToStore = LLVM.BuildIntCast(builder, source, valueType, "CastIfNecessaryInt");
            }
            else if (toStoreKind == LLVMTypeKind.LLVMFloatTypeKind && valueTypeKind != LLVMTypeKind.LLVMFloatTypeKind)
            {
                typedToStore = LLVM.BuildIntCast(builder, source, valueType, "CastIfNecessaryFloat");
            }
            else if (toStoreKind != LLVMTypeKind.LLVMFloatTypeKind && valueTypeKind == LLVMTypeKind.LLVMFloatTypeKind)
            {
                typedToStore = LLVM.BuildFPCast(builder, source, valueType, "CastIfNecessaryFloat");
            }

            return typedToStore;
        }

        internal static LLVMTypeRef GetLLVMTypeForTypeDesc(TypeDesc type)
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
                    return LLVM.Int32Type();
                case TypeFlags.IntPtr:
                case TypeFlags.UIntPtr:
                case TypeFlags.Array:
                case TypeFlags.SzArray:
                case TypeFlags.ByRef:
                case TypeFlags.Class:
                case TypeFlags.Interface:
                    return LLVM.PointerType(LLVM.Int8Type(), 0);

                case TypeFlags.Pointer:
                    return LLVM.PointerType(type.GetParameterType().IsVoid ? LLVM.Int8Type() : GetLLVMTypeForTypeDesc(type.GetParameterType()), 0);

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
            int offset = GetTotalRealLocalOffset();
            for (int i = 0; i < _spilledExpressions.Count; i++)
            {
                offset += _spilledExpressions[i].Type.GetElementSize().AsInt;
            }
            return offset;
        }

        private int GetTotalRealLocalOffset()
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
            for (int i = 0; i < _signature.Length; i++)
            {
                offset += _signature[i].GetElementSize().AsInt;
            }
            if (!_signature.IsStatic)
            {
                // If this is a struct, then it's a pointer on the stack
                if (_thisType.IsValueType)
                {
                    offset += _thisType.Context.Target.PointerSize;
                }
                else
                {
                    offset += _thisType.GetElementSize().AsInt;
                }
            }

            return offset;
        }

        private void GetArgSizeAndOffsetAtIndex(int index, out int size, out int offset)
        {
            int thisSize = 0;
            if (!_signature.IsStatic)
            {
                thisSize = _thisType.IsValueType ? _thisType.Context.Target.PointerSize : _thisType.GetElementSize().AsInt;
                if (index == 0)
                {
                    size = thisSize;
                    offset = 0;
                    return;
                }
                else
                {
                    index--;
                }
            }

            var argType = _signature[index];
            size = argType.GetElementSize().AsInt;

            offset = thisSize;
            for (int i = 0; i < index; i++)
            {
                offset += _signature[i].GetElementSize().AsInt;
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

        private void GetSpillSizeAndOffsetAtIndex(int index, out int size, out int offset)
        {
            SpilledExpressionEntry spill = _spilledExpressions[index];
            size = spill.Type.GetElementSize().AsInt;

            offset = 0;
            for (int i = 0; i < index; i++)
            {
                offset += _spilledExpressions[i].Type.GetElementSize().AsInt;
            }
        }

        private void ImportAddressOfVar(int index, bool argument)
        {
            TypeDesc type;
            LLVMValueRef typedLoadLocation = LoadVarAddress(index, argument ? LocalVarKind.Argument : LocalVarKind.Local, out type);
            _stack.Push(new AddressExpressionEntry(StackValueKind.ByRef, "ldloca", typedLoadLocation, type.MakePointerType()));
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
            throw new NotImplementedException("jmp");
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
            if (_signature.ReturnType != GetWellKnownType(WellKnownType.Void))
            {
                StackEntry retVal = _stack.Pop();
                LLVMTypeRef valueType = GetLLVMTypeForTypeDesc(_signature.ReturnType);
                ImportStoreHelper(retVal.ValueAsType(valueType, _builder), valueType, LLVM.GetNextParam(LLVM.GetFirstParam(_llvmFunction)), 0);
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

            if (opcode == ILOpcode.newobj)
            {
                if (callee.OwningType.IsString)
                {
                    // String constructors actually look like regular method calls
                    IMethodNode node = _compilation.NodeFactory.StringAllocator(callee);
                    _dependencies.Add(node);
                    callee = node.Method;
                    opcode = ILOpcode.call;
                }
                else
                {
                    StackEntry newObjResult = AllocateObject(callee.OwningType);
                    //one for the real result and one to be consumed by ctor
                    if (callee.Signature.Length > _stack.Length) //System.Reflection.MemberFilter.ctor
                        throw new InvalidProgramException();
                    _stack.InsertAt(newObjResult, _stack.Top - callee.Signature.Length);
                    _stack.InsertAt(newObjResult, _stack.Top - callee.Signature.Length);
                }
            }

            // we don't really have virtual call support, but we'll treat it as direct for now
            if (opcode != ILOpcode.call && opcode !=  ILOpcode.callvirt && opcode != ILOpcode.newobj)
            {
                throw new NotImplementedException();
            }
            if (opcode == ILOpcode.callvirt && callee.IsAbstract)
            {
                throw new NotImplementedException();
            }

            HandleCall(callee);
        }

        private ExpressionEntry AllocateObject(TypeDesc type)
        {
            MetadataType metadataType = (MetadataType)type;
            int objectSize = metadataType.InstanceByteCount.AsInt;
            if (metadataType.IsValueType)
            {
                objectSize += type.Context.Target.PointerSize;
            }

            LLVMValueRef allocatedMemory = LLVM.BuildMalloc(_builder, LLVM.ArrayType(LLVM.Int8Type(), (uint)objectSize), "newobj");
            LLVMValueRef castMemory = LLVM.BuildPointerCast(_builder, allocatedMemory, LLVM.PointerType(LLVM.Int8Type(), 0), "castnewobj");
            ImportCallMemset(castMemory, 0, objectSize);
            LLVMValueRef eeTypePointer = GetEETypeForTypeDesc(type);
            LLVMValueRef objectHeaderPtr = LLVM.BuildPointerCast(_builder, allocatedMemory, LLVM.PointerType(LLVM.TypeOf(eeTypePointer), 0), "objectHeaderPtr");
            LLVM.BuildStore(_builder, eeTypePointer, objectHeaderPtr);
            return new ExpressionEntry(StackValueKind.ObjRef, "newobj", castMemory, type);
        }

        private static LLVMValueRef BuildConstInt1(int number)
        {
            Debug.Assert(number == 0 || number == 1, "Non-boolean int1");
            return LLVM.ConstInt(LLVM.Int1Type(), (ulong)number, LLVMMisc.False);
        }

        private static LLVMValueRef BuildConstInt8(byte number)
        {
            return LLVM.ConstInt(LLVM.Int8Type(), number, LLVMMisc.False);
        }

        private static LLVMValueRef BuildConstInt32(int number)
        {
            return LLVM.ConstInt(LLVM.Int32Type(), (ulong)number, LLVMMisc.False);
        }

        private LLVMValueRef GetEETypeForTypeDesc(TypeDesc target)
        {
            ISymbolNode node = _compilation.NodeFactory.ConstructedTypeSymbol(target);
            LLVMValueRef eeTypePointer = LoadAddressOfSymbolNode(node);
            _dependencies.Add(node);
            var eeTypePtrType = _compilation.TypeSystemContext.SystemModule.GetKnownType("System", "EETypePtr");
            var ptrPtrType = LLVM.PointerType(GetLLVMTypeForTypeDesc(eeTypePtrType), 0);
            return LLVM.BuildPointerCast(_builder, eeTypePointer, ptrPtrType, "castEETypePtr");
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
            var castShadowStack = LLVM.BuildPointerCast(_builder, shadowStack, LLVM.PointerType(LLVM.Int8Type(), 0), "castshadowstack");

            int returnOffset = GetTotalParameterOffset() + GetTotalLocalOffset();
            var returnAddress = LLVM.BuildGEP(_builder, LLVM.GetFirstParam(_llvmFunction),
                new LLVMValueRef[] { LLVM.ConstInt(LLVM.Int32Type(), (uint)returnOffset, LLVMMisc.False) },
                String.Empty);
            var castReturnAddress = LLVM.BuildPointerCast(_builder, returnAddress, LLVM.PointerType(LLVM.Int8Type(), 0), "castreturnaddress");

            // argument offset
            uint argOffset = 0;
            int instanceAdjustment = 0;
            if (!callee.Signature.IsStatic)
            {
                instanceAdjustment = 1;
            }

            // The last argument is the top of the stack. We need to reverse them and store starting at the first argument
            StackEntry[] argumentValues = new StackEntry[callee.Signature.Length + instanceAdjustment];

            for(int i = 0; i < argumentValues.Length; i++)
            {
                argumentValues[argumentValues.Length - i - 1] = _stack.Pop();
            }

            for (int index = 0; index < argumentValues.Length; index++)
            {
                StackEntry toStore = argumentValues[index];

                TypeDesc argType;
                if (index == 0 && !callee.Signature.IsStatic)
                {
                    argType = callee.OwningType;
                }
                else
                {
                    argType = callee.Signature[index - instanceAdjustment];
                }

                LLVMTypeRef valueType = GetLLVMTypeForTypeDesc(argType);

                ImportStoreHelper(toStore.ValueAsType(valueType, _builder), valueType, castShadowStack, argOffset);

                argOffset += (uint)argType.GetElementSize().AsInt;
            }

            LLVM.BuildCall(_builder, fn, new LLVMValueRef[] {
                castShadowStack,
                castReturnAddress}, string.Empty);

            
            if (!callee.Signature.ReturnType.IsVoid)
            {
                LLVMTypeRef returnLLVMType = GetLLVMTypeForTypeDesc(callee.Signature.ReturnType);
                LLVMValueRef returnLLVMPointer = LLVM.BuildPointerCast(_builder, returnAddress, LLVM.PointerType(returnLLVMType, 0), "castreturnpointer");
                PushLoadExpression(GetStackValueKind(callee.Signature.ReturnType), String.Empty, returnLLVMPointer, callee.Signature.ReturnType);
            }
        }

        private void AddMethodReference(MethodDesc method)
        {
            _dependencies.Add(_compilation.NodeFactory.MethodEntrypoint(method));
        }

        private void ImportRawPInvoke(MethodDesc method)
        {
            LLVMValueRef nativeFunc = LLVM.GetNamedFunction(Module, method.Name);

            //emscripten dies if this is output because its expected to have i32, i32, i64. But the runtime has defined it as i8*, i8*, i64
            if (method.Name == "memmove")
                throw new NotImplementedException();


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
                // Arguments are reversed on the stack
                // Coerce pointers to the native type
                TypeDesc signatureType = method.Signature[arguments.Length - i - 1];
                arguments[arguments.Length - i - 1] = _stack.Pop().ValueAsType(GetLLVMTypeForTypeDesc(signatureType), _builder);
            }

            //dont name the return value if the function returns void, its invalid
            var returnValue = LLVM.BuildCall(_builder, nativeFunc, arguments, !method.Signature.ReturnType.IsVoid ? "call" : string.Empty);

            if(!method.Signature.ReturnType.IsVoid)
                PushExpression(GetStackValueKind(method.Signature.ReturnType), "retval", returnValue, method.Signature.ReturnType);
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
                ImportFallthrough(target);
                //TODO: why does this illegal branch happen in System.Reflection.MemberFilter.ctor
                if (target.StartOffset == 0)
                    throw new InvalidProgramException();
                LLVM.BuildBr(_builder, GetLLVMBasicBlockForBlock(target));
            }
            else
            {
                LLVMValueRef condition;

                if (opcode == ILOpcode.brfalse || opcode == ILOpcode.brtrue)
                {
                    var op = _stack.Pop();
                    LLVMValueRef value = op.ValueAsInt32(_builder, false);

                    if (LLVM.TypeOf(value).TypeKind != LLVMTypeKind.LLVMIntegerTypeKind)
                        throw new InvalidProgramException("branch on non integer");

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

                    LLVMValueRef left = op1.ValueForStackKind(kind, _builder, false);
                    LLVMValueRef right = op2.ValueForStackKind(kind, _builder, false);

                    switch (opcode)
                    {
                        case ILOpcode.beq:
                            condition = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntEQ, left, right, "beq");
                            break;
                        case ILOpcode.bge:
                            condition = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntSGE, left, right, "bge");
                            break;
                        case ILOpcode.bgt:
                            condition = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntSGT, left, right, "bgt");
                            break;
                        case ILOpcode.ble:
                            condition = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntSLE, left, right, "ble");
                            break;
                        case ILOpcode.blt:
                            condition = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntSLT, left, right, "blt");
                            break;
                        case ILOpcode.bne_un:
                            condition = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntNE, left, right, "bne_un");
                            break;
                        case ILOpcode.bge_un:
                            condition = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntUGE, left, right, "bge_un");
                            break;
                        case ILOpcode.bgt_un:
                            condition = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntUGT, left, right, "bgt_un");
                            break;
                        case ILOpcode.ble_un:
                            condition = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntULE, left, right, "ble_un");
                            break;
                        case ILOpcode.blt_un:
                            condition = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntULT, left, right, "blt_un");
                            break;
                        default:
                            throw new NotSupportedException(); // unreachable
                    }
                }
                //TODO: why did this happen only during an optimized build of [System.Private.CoreLib]System.Threading.Lock.ReleaseContended
                if (target.StartOffset == 0)
                    throw new NotImplementedException("cant branch to entry basic block");

                ImportFallthrough(target);
                ImportFallthrough(fallthrough);
                LLVM.BuildCondBr(_builder, condition, GetLLVMBasicBlockForBlock(target), GetLLVMBasicBlockForBlock(fallthrough));
            }
        }

        private void ImportSwitchJump(int jmpBase, int[] jmpDelta, BasicBlock fallthrough)
        {
            var operand = _stack.Pop();

            var @switch = LLVM.BuildSwitch(_builder, operand.ValueAsInt32(_builder, false), GetLLVMBasicBlockForBlock(fallthrough), (uint)jmpDelta.Length);
            for (var i = 0; i < jmpDelta.Length; i++)
            {
                var target = _basicBlocks[_currentOffset + jmpDelta[i]];
                LLVM.AddCase(@switch, LLVM.ConstInt(LLVM.Int32Type(), (ulong)i, false), GetLLVMBasicBlockForBlock(target));
                ImportFallthrough(target);
            }

            ImportFallthrough(fallthrough);
        }

        private void ImportLoadIndirect(int token)
        {
            ImportLoadIndirect(ResolveTypeToken(token));
        }

        private void ImportLoadIndirect(TypeDesc type)
        {
            var pointer = _stack.Pop();
            Debug.Assert(pointer is ExpressionEntry || pointer is ConstantEntry);
            var expressionPointer = pointer as ExpressionEntry;
            TypeDesc pointerElementType = pointer.Type.GetParameterType();
            LLVMValueRef rawValue = expressionPointer?.RawLLVMValue ?? LLVM.ConstNull(GetLLVMTypeForTypeDesc(pointerElementType));
            _stack.Push(new LoadExpressionEntry(type != null ? GetStackValueKind(type) : StackValueKind.ByRef, "ldind",
                rawValue, pointer.Type.GetParameterType()));
        }

        private void ImportStoreIndirect(int token)
        {
            ImportStoreIndirect(ResolveTypeToken(token));
        }

        private void ImportStoreIndirect(TypeDesc type)
        {
            StackEntry value = _stack.Pop();
            StackEntry destinationPointer = _stack.Pop();
            LLVMValueRef typedValue;
            LLVMValueRef typedPointer;

            if (type != null)
            {
                typedValue = value.ValueAsType(type, _builder);
                typedPointer = destinationPointer.ValueAsType(type.MakePointerType(), _builder);
            }
            else
            {
                typedPointer = destinationPointer.ValueAsType(LLVM.PointerType(LLVM.Int32Type(), 0), _builder);
                typedValue = value.ValueAsInt32(_builder, false);
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
            if (kind == StackValueKind.ByRef)
            {
                kind = StackValueKind.NativeInt;
                type = type.MakePointerType();
            }

            LLVMValueRef result;
            LLVMValueRef left = op1.ValueForStackKind(kind, _builder, false);
            LLVMValueRef right = op2.ValueForStackKind(kind, _builder, false);
            if (kind == StackValueKind.Float)
            {
                switch (opcode)
                {
                    case ILOpcode.add:
                        result = LLVM.BuildFAdd(_builder, left, right, "fadd");
                        break;
                    case ILOpcode.sub:
                        result = LLVM.BuildFSub(_builder, left, right, "fsub");
                        break;
                    case ILOpcode.mul:
                        result = LLVM.BuildFMul(_builder, left, right, "fmul");
                        break;
                    case ILOpcode.div:
                        result = LLVM.BuildFDiv(_builder, left, right, "fdiv");
                        break;
                    case ILOpcode.rem:
                        result = LLVM.BuildFRem(_builder, left, right, "frem");
                        break;

                    // TODO: Overflow checks
                    case ILOpcode.add_ovf:
                    case ILOpcode.add_ovf_un:
                        result = LLVM.BuildFAdd(_builder, left, right, "fadd");
                        break;
                    case ILOpcode.sub_ovf:
                    case ILOpcode.sub_ovf_un:
                        result = LLVM.BuildFSub(_builder, left, right, "fsub");
                        break;
                    case ILOpcode.mul_ovf:
                    case ILOpcode.mul_ovf_un:
                        result = LLVM.BuildFMul(_builder, left, right, "fmul");
                        break;

                    default:
                        throw new InvalidOperationException(); // Should be unreachable
                }
            }
            else
            {
                switch (opcode)
                {
                    case ILOpcode.add:
                        result = LLVM.BuildAdd(_builder, left, right, "add");
                        break;
                    case ILOpcode.sub:
                        result = LLVM.BuildSub(_builder, left, right, "sub");
                        break;
                    case ILOpcode.mul:
                        result = LLVM.BuildMul(_builder, left, right, "mul");
                        break;
                    case ILOpcode.div:
                        result = LLVM.BuildSDiv(_builder, left, right, "sdiv");
                        break;
                    case ILOpcode.div_un:
                        result = LLVM.BuildUDiv(_builder, left, right, "udiv");
                        break;
                    case ILOpcode.rem:
                        result = LLVM.BuildSRem(_builder, left, right, "srem");
                        break;
                    case ILOpcode.rem_un:
                        result = LLVM.BuildURem(_builder, left, right, "urem");
                        break;
                    case ILOpcode.and:
                        result = LLVM.BuildAnd(_builder, left, right, "and");
                        break;
                    case ILOpcode.or:
                        result = LLVM.BuildOr(_builder, left, right, "or");
                        break;
                    case ILOpcode.xor:
                        result = LLVM.BuildXor(_builder, left, right, "xor");
                        break;

                    // TODO: Overflow checks
                    case ILOpcode.add_ovf:
                    case ILOpcode.add_ovf_un:
                        result = LLVM.BuildAdd(_builder, left, right, "add");
                        break;
                    case ILOpcode.sub_ovf:
                    case ILOpcode.sub_ovf_un:
                        result = LLVM.BuildSub(_builder, left, right, "sub");
                        break;
                    case ILOpcode.mul_ovf:
                    case ILOpcode.mul_ovf_un:
                        result = LLVM.BuildMul(_builder, left, right, "mul");
                        break;

                    default:
                        throw new InvalidOperationException(); // Should be unreachable
                }
            }


            if (kind == StackValueKind.NativeInt || kind == StackValueKind.ByRef || kind == StackValueKind.ObjRef)
            {
                //we need to put the type back if we changed it because it started out a pointer
                result = CastToTypeDesc(result, type);
            }
            PushExpression(kind, "binop", result, type);
        }

        private void ImportShiftOperation(ILOpcode opcode)
        {
            LLVMValueRef result;
            StackEntry numBitsToShift = _stack.Pop();
            StackEntry valueToShift = _stack.Pop();

            LLVMValueRef valueToShiftValue = valueToShift.ValueForStackKind(valueToShift.Kind, _builder, false);

            switch (opcode)
            {
                case ILOpcode.shl:
                    result = LLVM.BuildShl(_builder, valueToShiftValue, numBitsToShift.ValueAsInt32(_builder, false), "shl");
                    break;
                case ILOpcode.shr:
                    result = LLVM.BuildAShr(_builder, valueToShiftValue, numBitsToShift.ValueAsInt32(_builder, false), "shr");
                    break;
                case ILOpcode.shr_un:
                    result = LLVM.BuildLShr(_builder, valueToShiftValue, numBitsToShift.ValueAsInt32(_builder, false), "shr");
                    break;
                default:
                    throw new InvalidOperationException(); // Should be unreachable
            }

            PushExpression(valueToShift.Kind, "shiftop", result, valueToShift.Type);
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
            LLVMValueRef typeSaneOp1 = op1.ValueForStackKind(kind, _builder, true);
            LLVMValueRef typeSaneOp2 = op2.ValueForStackKind(kind, _builder, true);

            switch (opcode)
            {
                case ILOpcode.ceq:
                    result = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntEQ, typeSaneOp2, typeSaneOp1, "ceq");
                    break;
                case ILOpcode.cgt:
                    result = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntSGT, typeSaneOp2, typeSaneOp1, "cgt");
                    break;
                case ILOpcode.clt:
                    result = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntSLT, typeSaneOp2, typeSaneOp1, "clt");
                    break;
                case ILOpcode.cgt_un:
                    result = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntUGT, typeSaneOp2, typeSaneOp1, "cgt_un");
                    break;
                case ILOpcode.clt_un:
                    result = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntULT, typeSaneOp2, typeSaneOp1, "clt_un");
                    break;
                default:
                    throw new NotSupportedException(); // unreachable
            }

            PushExpression(StackValueKind.Int32, "cmpop", result, GetWellKnownType(WellKnownType.SByte));
        }

        private void ImportConvert(WellKnownType wellKnownType, bool checkOverflow, bool unsigned)
        {
            StackEntry value = _stack.Pop();
            LLVMValueRef convertedValue;
            //conv.u for a pointer should change to a int8*
            if (wellKnownType == WellKnownType.UIntPtr)
            {
                if (value.Kind == StackValueKind.Int32)
                {
                    convertedValue = LLVM.BuildIntToPtr(_builder, value.ValueAsInt32(_builder, false), LLVM.PointerType(LLVM.Int8Type(), 0), "conv.u");
                }
                else
                {
                    convertedValue = value.ValueAsType(GetWellKnownType(wellKnownType), _builder);
                }
            }
            else
            {
                convertedValue = value.ValueAsType(GetWellKnownType(wellKnownType), _builder);
            }
            PushExpression(value.Kind, "conv", convertedValue, value.Type);
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
                        result = LLVM.BuildFNeg(_builder, argument.ValueForStackKind(argument.Kind, _builder, false), "neg");
                    }   
                    else
                    {
                        result = LLVM.BuildNeg(_builder, argument.ValueForStackKind(argument.Kind, _builder, true), "neg");
                    }
                    break;
                case ILOpcode.not:
                    result = LLVM.BuildNot(_builder, argument.ValueForStackKind(argument.Kind, _builder, true), "not");
                    break;
                default:
                    throw new NotSupportedException(); // unreachable
            }

            PushExpression(argument.Kind, "unaryop", result, argument.Type);
        }

        private void ImportCpOpj(int token)
        {
        }

        private void ImportUnbox(int token, ILOpcode opCode)
        {
            TypeDesc type = ResolveTypeToken(token);
            if (type.IsNullable)
                throw new NotImplementedException();

            if (opCode == ILOpcode.unbox)
            {
                var unboxResult = _stack.Pop().ValueAsType(LLVM.PointerType(LLVM.Int8Type(), 0), _builder);
                LLVMValueRef unboxData = LLVM.BuildGEP(_builder, unboxResult, new LLVMValueRef[] { BuildConstInt32(type.Context.Target.PointerSize) }, "unboxData");
                //push the pointer to the data, but it shouldnt be implicitly dereferenced
                PushExpression(GetStackValueKind(type), "unboxed", unboxData, type);
            }
            else //unbox_any
            {
                Debug.Assert(opCode == ILOpcode.unbox_any);

                //TODO: when the runtime is ready switch this to calling the real RhUnboxAny
                //LLVMValueRef eeType = GetEETypeForTypeDesc(type);
                //var eeTypeDesc = _compilation.TypeSystemContext.SystemModule.GetKnownType("System", "EETypePtr");
                //LLVMValueRef untypedObjectValue = LLVM.BuildAlloca(_builder, GetLLVMTypeForTypeDesc(type), "objptr");
                //PushExpression(StackValueKind.ByRef, "objPtr", untypedObjectValue, type.MakePointerType());
                //PushExpression(StackValueKind.ByRef, "eeType", eeType, eeTypeDesc);
                //CallRuntimeExport(_compilation.TypeSystemContext, "RhUnboxAny");
                //PushLoadExpression(GetStackValueKind(type), "unboxed", untypedObjectValue, type);
                //this can be removed once we can call RhUnboxAny
                if (!type.IsValueType)
                    throw new NotImplementedException(); 

                var unboxResult = _stack.Pop().ValueAsType(LLVM.PointerType(LLVM.Int8Type(), 0), _builder);
                LLVMValueRef unboxData = LLVM.BuildGEP(_builder, unboxResult, new LLVMValueRef[] {  BuildConstInt32(type.Context.Target.PointerSize) }, "unboxData");
                PushLoadExpression(GetStackValueKind(type), "unboxed", unboxData, type);
            }
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
                PushExpression(StackValueKind.ByRef, "ldtoken", GetEETypeForTypeDesc(ldtokenValue as TypeDesc), _compilation.TypeSystemContext.SystemModule.GetKnownType("System", "EETypePtr"));
                MethodDesc helper = _compilation.TypeSystemContext.GetHelperEntryPoint("LdTokenHelpers", "GetRuntimeTypeHandle");
                AddMethodReference(helper);
                HandleCall(helper);
                name = ldtokenValue.ToString();
            }
            else if (ldtokenValue is FieldDesc)
            {
                ldtokenKind = WellKnownType.RuntimeFieldHandle;
                value = new LdTokenEntry<FieldDesc>(StackValueKind.ValueType, null, (FieldDesc)ldtokenValue, GetWellKnownType(ldtokenKind));
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
            EmitTrapCall();
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
            var exceptionObject = _stack.Pop();

            EmitTrapCall();
        }

        private LLVMValueRef GetInstanceFieldAddress(StackEntry objectEntry, FieldDesc field)
        {
            var objectType = objectEntry.Type ?? field.OwningType;
            LLVMValueRef untypedObjectValue;
            LLVMTypeRef llvmObjectType = GetLLVMTypeForTypeDesc(objectType);
            if (objectType.IsValueType && !objectType.IsPointer && objectEntry.Kind != StackValueKind.NativeInt && objectEntry.Kind != StackValueKind.ByRef)
            {
                if (objectEntry is LoadExpressionEntry)
                {
                    untypedObjectValue = CastToRawPointer(((LoadExpressionEntry)objectEntry).RawLLVMValue);
                }
                else
                {
                    untypedObjectValue = LLVM.BuildAlloca(_builder, llvmObjectType, "objptr");
                    LLVM.BuildStore(_builder, objectEntry.ValueAsType(llvmObjectType, _builder), untypedObjectValue);
                    untypedObjectValue = LLVM.BuildPointerCast(_builder, untypedObjectValue, LLVM.PointerType(LLVMTypeRef.Int8Type(), 0), "objptrcast");
                }
            }
            else
            {
                untypedObjectValue = objectEntry.ValueAsType(LLVM.PointerType(LLVMTypeRef.Int8Type(), 0), _builder);
            }
            if (field.Offset.AsInt == 0)
            {
                return untypedObjectValue;
            }
            else
            {
                var loadLocation = LLVM.BuildGEP(_builder, untypedObjectValue,
                    new LLVMValueRef[] { LLVM.ConstInt(LLVM.Int32Type(), (ulong)field.Offset.AsInt, LLVMMisc.False) }, String.Empty);
                return loadLocation;
            }
        }

        private LLVMValueRef GetFieldAddress(FieldDesc field, bool isStatic)
        {
            if (field.IsStatic)
            {
                //pop unused value
                if (!isStatic)
                    _stack.Pop();

                return WebAssemblyObjectWriter.EmitGlobal(Module, field, _compilation.NameMangler);
            }
            else
            {
                return GetInstanceFieldAddress(_stack.Pop(), field);
            }
        }

        private void ImportLoadField(int token, bool isStatic)
        {
            FieldDesc field = (FieldDesc)_methodIL.GetObject(token);
            LLVMValueRef fieldAddress = GetFieldAddress(field, isStatic);
            PushLoadExpression(GetStackValueKind(field.FieldType), "ldfld_" + field.Name, fieldAddress, field.FieldType);
        }

        private void ImportAddressOfField(int token, bool isStatic)
        {
            FieldDesc field = (FieldDesc)_methodIL.GetObject(token);
            LLVMValueRef fieldAddress = GetFieldAddress(field, isStatic);
            _stack.Push(new AddressExpressionEntry(StackValueKind.ByRef, "ldflda", fieldAddress, field.FieldType.MakePointerType()));
        }

        private void ImportStoreField(int token, bool isStatic)
        {
            FieldDesc field = (FieldDesc)_methodIL.GetObject(token);
            StackEntry valueEntry = _stack.Pop();
            LLVMValueRef fieldAddress = GetFieldAddress(field, isStatic);
            CastingStore(fieldAddress, valueEntry, field.FieldType);
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
            TypeDesc type = ResolveTypeToken(token);
            var valueEntry = _stack.Pop();
            var llvmType = GetLLVMTypeForTypeDesc(type);
            if (llvmType.TypeKind == LLVMTypeKind.LLVMArrayTypeKind)
                ImportCallMemset(valueEntry.ValueAsType(LLVM.PointerType(LLVM.Int8Type(), 0), _builder), 0, type.GetElementSize().AsInt);
            else if (llvmType.TypeKind == LLVMTypeKind.LLVMIntegerTypeKind)
                LLVM.BuildStore(_builder, LLVM.ConstInt(llvmType, 0, LLVMMisc.False), valueEntry.ValueAsType(LLVM.PointerType(llvmType, 0), _builder));
            else if (llvmType.TypeKind == LLVMTypeKind.LLVMPointerTypeKind)
                LLVM.BuildStore(_builder, LLVM.ConstNull(llvmType), valueEntry.ValueAsType(LLVM.PointerType(llvmType, 0), _builder));
            else if (llvmType.TypeKind == LLVMTypeKind.LLVMFloatTypeKind)
                LLVM.BuildStore(_builder, LLVM.ConstReal(llvmType, 0.0), valueEntry.ValueAsType(LLVM.PointerType(llvmType, 0), _builder));
            else
                throw new NotImplementedException();
        }

        private void ImportBox(int token)
        {
            TypeDesc type = ResolveTypeToken(token);
            if (type.IsValueType)
            {
                if (type.IsNullable)
                    throw new NotImplementedException();

                var value = _stack.Pop();
                ExpressionEntry boxTarget = AllocateObject(type);
                LLVMValueRef boxData = LLVM.BuildGEP(_builder, boxTarget.RawLLVMValue, new LLVMValueRef[] { BuildConstInt32(type.Context.Target.PointerSize) }, "boxData");
                LLVMValueRef typedBoxData = LLVM.BuildPointerCast(_builder, boxData, LLVM.PointerType(GetLLVMTypeForTypeDesc(type), 0), "typedBoxData");
                LLVM.BuildStore(_builder, value.ValueAsType(type, _builder), typedBoxData);
                _stack.Push(boxTarget);
            }
        }

        private void ImportLeave(BasicBlock target)
        {
            for (int i = 0; i < _exceptionRegions.Length; i++)
            {
                var r = _exceptionRegions[i];

                if (r.ILRegion.Kind == ILExceptionRegionKind.Finally &&
                    IsOffsetContained(_currentOffset - 1, r.ILRegion.TryOffset, r.ILRegion.TryLength) &&
                    !IsOffsetContained(target.StartOffset, r.ILRegion.TryOffset, r.ILRegion.TryLength))
                {
                    MarkBasicBlock(_basicBlocks[r.ILRegion.HandlerOffset]);
                }
            }

            MarkBasicBlock(target);
            LLVM.BuildBr(_builder, GetLLVMBasicBlockForBlock(target));
        }

        private static bool IsOffsetContained(int offset, int start, int length)
        {
            return start <= offset && offset < start + length;
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
                    for (int i = 0; i < _stack.Length; i++)
                    {
                        entryStack.Push(NewSpillSlot(_stack[i]));
                    }
                }
                next.EntryStack = entryStack;
            }

            if (entryStack != null)
            {
                for (int i = 0; i < entryStack.Length; i++)
                {
                    var currentEntry = _stack[i];
                    var entry = entryStack[i] as SpilledExpressionEntry;
                    if (entry == null)
                        throw new InvalidProgramException();

                    if (currentEntry is SpilledExpressionEntry)
                        continue; //this is already a sharable value

                    StoreTemp(entry.LocalIndex, currentEntry.ValueAsType(entry.Type, _builder));
                }
            }

            MarkBasicBlock(next);

        }

        private void CallRuntimeExport(TypeSystemContext context, string methodName)
        {
            MetadataType helperType = context.SystemModule.GetKnownType("System.Runtime", "RuntimeExports");
            MethodDesc helperMethod = helperType.GetKnownMethod(methodName, null);
            HandleCall(helperMethod);
        }

        private StackEntry NewSpillSlot(StackEntry entry)
        {
            if (entry is SpilledExpressionEntry)
                return entry;
            else
            {
                var entryType = entry.Type ?? GetWellKnownType(WellKnownType.Object); //type is required here, currently the only time entry.Type is null is if someone has pushed a null literal
                var entryIndex = _spilledExpressions.Count;
                var newEntry = new SpilledExpressionEntry(entry.Kind, entry is ExpressionEntry ? ((ExpressionEntry)entry).Name : "spilled" + entryIndex, entryType, entryIndex, this);
                _spilledExpressions.Add(newEntry);
                return newEntry;
            }
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

        private void EmitTrapCall()
        {
            if (TrapFunction.Pointer == IntPtr.Zero)
            {
                TrapFunction = LLVM.AddFunction(Module, "llvm.trap", LLVM.FunctionType(LLVM.VoidType(), Array.Empty<LLVMTypeRef>(), false));
            }
            LLVM.BuildCall(_builder, TrapFunction, Array.Empty<LLVMValueRef>(), string.Empty);
        }

        private void EmitDoNothingCall()
        {
            if (DoNothingFunction.Pointer == IntPtr.Zero)
            {
                DoNothingFunction = LLVM.AddFunction(Module, "llvm.donothing", LLVM.FunctionType(LLVM.VoidType(), Array.Empty<LLVMTypeRef>(), false));
            }
            LLVM.BuildCall(_builder, DoNothingFunction, Array.Empty<LLVMValueRef>(), string.Empty);
        }

        public override string ToString()
        {
            return _method.ToString();
        }
    }
}
