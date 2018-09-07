// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Internal.TypeSystem;
using ILCompiler;
using LLVMSharp;
using ILCompiler.CodeGen;
using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;
using ILCompiler.WebAssembly;
using Internal.TypeSystem.Ecma;

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
        public LLVMContextRef Context { get; }
        private readonly MethodDesc _method;
        private readonly MethodIL _methodIL;
        private readonly MethodSignature _signature;
        private readonly TypeDesc _thisType;
        private readonly WebAssemblyCodegenCompilation _compilation;
        private LLVMValueRef _llvmFunction;
        private LLVMBasicBlockRef _curBasicBlock;
        private LLVMBuilderRef _builder;
        private readonly LocalVariableDefinition[] _locals;
        private readonly LLVMValueRef[] _localSlots;
        private readonly LLVMValueRef[] _argSlots;
        private List<SpilledExpressionEntry> _spilledExpressions = new List<SpilledExpressionEntry>();
        private int _pointerSize;
        private readonly byte[] _ilBytes;
        private MethodDebugInformation _debugInformation;
        private LLVMMetadataRef _debugFunction;
        private TypeDesc _constrainedType = null;

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
            _localSlots = new LLVMValueRef[_locals.Length];
            _argSlots = new LLVMValueRef[method.Signature.Length];
            _signature = method.Signature;
            _thisType = method.OwningType;

            var ilExceptionRegions = methodIL.GetExceptionRegions();
            _exceptionRegions = new ExceptionRegion[ilExceptionRegions.Length];
            for (int i = 0; i < ilExceptionRegions.Length; i++)
            {
                _exceptionRegions[i] = new ExceptionRegion() { ILRegion = ilExceptionRegions[i] };
            }
            _llvmFunction = GetOrCreateLLVMFunction(mangledName, method.Signature);
            _builder = LLVM.CreateBuilder();
            _pointerSize = compilation.NodeFactory.Target.PointerSize;

            _debugInformation = _compilation.GetDebugInfo(_methodIL);

            Context = LLVM.GetModuleContext(Module);
        }

        public void Import()
        {
            FindBasicBlocks();

            GenerateProlog();

            try
            {
                ImportBasicBlocks();
            }
            catch
            {
                LLVMBasicBlockRef trapBlock = LLVM.AppendBasicBlock(_llvmFunction, "Trap");
                
                // Change the function body to trap
                foreach (BasicBlock block in _basicBlocks)
                {
                    if (block != null && block.Block.Pointer != IntPtr.Zero)
                    {
                        LLVM.ReplaceAllUsesWith(block.Block, trapBlock);
                        LLVM.DeleteBasicBlock(block.Block);
                    }
                }

                LLVM.PositionBuilderAtEnd(_builder, trapBlock);
                EmitTrapCall();
                throw;
            }
            finally
            {
                // Generate thunk for runtime exports
                if (_method.IsRuntimeExport || _method.IsNativeCallable)
                {
                    EcmaMethod ecmaMethod = ((EcmaMethod)_method);
                    string exportName = ecmaMethod.IsRuntimeExport ? ecmaMethod.GetRuntimeExportName() : ecmaMethod.GetNativeCallableExportName();
                    if (exportName == null)
                    {
                        exportName = ecmaMethod.Name;
                    }

                    EmitNativeToManagedThunk(_compilation, _method, exportName, _llvmFunction);
                }
            }
        }

        private void GenerateProlog()
        {
            LLVMBasicBlockRef prologBlock = LLVM.AppendBasicBlock(_llvmFunction, "Prolog");
            LLVM.PositionBuilderAtEnd(_builder, prologBlock);

            // Copy arguments onto the stack to allow
            // them to be referenced by address
            int thisOffset = 0;
            if (!_signature.IsStatic)
            {
                thisOffset = 1;
            }

            // Keep track of where we are in the llvm signature, starting after the
            // shadow stack pointer and return adress
            int signatureIndex = 1;
            if (NeedsReturnStackSlot(_signature))
            {
                signatureIndex++;
            }

            string[] argNames = null;
            if (_debugInformation != null)
            {
                argNames = _debugInformation.GetParameterNames()?.ToArray();
            }

            for (int i = 0; i < _signature.Length; i++)
            {
                if (CanStoreTypeOnStack(_signature[i]))
                {
                    string argName = String.Empty;
                    if (argNames != null && argNames[i] != null)
                    {
                        argName = argNames[i] + "_";
                    }
                    argName += $"arg{i + thisOffset}_";

                    LLVMValueRef argStackSlot = LLVM.BuildAlloca(_builder, GetLLVMTypeForTypeDesc(_signature[i]), argName);
                    LLVM.BuildStore(_builder, LLVM.GetParam(_llvmFunction, (uint)signatureIndex), argStackSlot);
                    _argSlots[i] = argStackSlot;
                    signatureIndex++;
                }
            }

            string[] localNames = new string[_locals.Length];
            if (_debugInformation != null)
            {
                foreach (ILLocalVariable localDebugInfo in _debugInformation.GetLocalVariables() ?? Enumerable.Empty<ILLocalVariable>())
                {
                    // Check whether the slot still exists as the compiler may remove it for intrinsics
                    int slot = localDebugInfo.Slot;
                    if (slot < localNames.Length)
                    {
                        localNames[localDebugInfo.Slot] = localDebugInfo.Name;
                    }
                }
            }

            for (int i = 0; i < _locals.Length; i++)
            {
                if (CanStoreLocalOnStack(_locals[i].Type))
                {
                    string localName = String.Empty;
                    if (localNames[i] != null)
                    {
                        localName = localNames[i] + "_";
                    }

                    localName += $"local{i}_";

                    LLVMValueRef localStackSlot = LLVM.BuildAlloca(_builder, GetLLVMTypeForTypeDesc(_locals[i].Type), localName);
                    _localSlots[i] = localStackSlot;
                }
            }

            if (_methodIL.IsInitLocals)
            {
                for(int i = 0; i < _locals.Length; i++)
                {
                    LLVMValueRef localAddr = LoadVarAddress(i, LocalVarKind.Local, out TypeDesc localType);
                    if(CanStoreLocalOnStack(localType))
                    {
                        LLVMTypeRef llvmType = GetLLVMTypeForTypeDesc(localType);
                        LLVMTypeKind typeKind = LLVM.GetTypeKind(llvmType);
                        switch (typeKind)
                        {
                            case LLVMTypeKind.LLVMIntegerTypeKind:
                                if (llvmType.Equals(LLVM.Int1Type()))
                                {
                                    LLVM.BuildStore(_builder, BuildConstInt1(0), localAddr);
                                }
                                else if (llvmType.Equals(LLVM.Int8Type()))
                                {
                                    LLVM.BuildStore(_builder, BuildConstInt8(0), localAddr);
                                }
                                else if (llvmType.Equals(LLVM.Int16Type()))
                                {
                                    LLVM.BuildStore(_builder, BuildConstInt16(0), localAddr);
                                }
                                else if (llvmType.Equals(LLVM.Int32Type()))
                                {
                                    LLVM.BuildStore(_builder, BuildConstInt32(0), localAddr);
                                }
                                else if (llvmType.Equals(LLVM.Int64Type()))
                                {
                                    LLVM.BuildStore(_builder, BuildConstInt64(0), localAddr);
                                }
                                else
                                {
                                    throw new Exception("Unexpected LLVM int type");
                                }
                                break;

                            case LLVMTypeKind.LLVMPointerTypeKind:
                                LLVM.BuildStore(_builder, LLVM.ConstPointerNull(llvmType), localAddr);
                                break;

                            default:
                                LLVMValueRef castAddr = LLVM.BuildPointerCast(_builder, localAddr, LLVM.PointerType(LLVM.Int8Type(), 0), $"cast_local{i}_");
                                ImportCallMemset(castAddr, 0, localType.GetElementSize().AsInt);
                                break;
                        }
                    }
                    else
                    {
                        LLVMValueRef castAddr = LLVM.BuildPointerCast(_builder, localAddr, LLVM.PointerType(LLVM.Int8Type(), 0), $"cast_local{i}_");
                        ImportCallMemset(castAddr, 0, localType.GetElementSize().AsInt);
                    }
                }
            }

            MetadataType metadataType = (MetadataType)_thisType;
            if (!metadataType.IsBeforeFieldInit)
            {
                if (!_method.IsStaticConstructor && _method.Signature.IsStatic || _method.IsConstructor || (_thisType.IsValueType && !_method.Signature.IsStatic))
                {
                    TriggerCctor(metadataType);
                }
            }

            LLVMBasicBlockRef block0 = GetLLVMBasicBlockForBlock(_basicBlocks[0]);
            LLVM.BuildBr(_builder, block0);
        }

        private LLVMValueRef CreateLLVMFunction(string mangledName, MethodSignature signature)
        {
            return LLVM.AddFunction(Module, mangledName, GetLLVMSignatureForMethod(signature));
        }

        private LLVMValueRef GetOrCreateLLVMFunction(string mangledName, MethodSignature signature)
        {
            LLVMValueRef llvmFunction = LLVM.GetNamedFunction(Module, mangledName);

            if(llvmFunction.Pointer == IntPtr.Zero)
            {
                return CreateLLVMFunction(mangledName, signature);
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
            ImportCallMemset(targetPointer, value, objectSizeValue);
        }

        private void ImportCallMemset (LLVMValueRef targetPointer, byte value, LLVMValueRef length)
        {
            var memsetSignature = LLVM.FunctionType(LLVM.VoidType(), new LLVMTypeRef[] { LLVM.PointerType(LLVM.Int8Type(), 0), LLVM.Int8Type(), LLVM.Int32Type(), LLVM.Int32Type(), LLVM.Int1Type() }, false);
            LLVM.BuildCall(_builder, GetOrCreateLLVMFunction("llvm.memset.p0i8.i32", memsetSignature), new LLVMValueRef[] { targetPointer, BuildConstInt8(value), length, BuildConstInt32(1), BuildConstInt1(0) }, String.Empty);
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
                    _stack.Push(entryStack[i].Duplicate(_builder));
                }
            }

            _curBasicBlock = GetLLVMBasicBlockForBlock(basicBlock);

            LLVM.PositionBuilderAtEnd(_builder, _curBasicBlock);
        }

        private void EndImportingBasicBlock(BasicBlock basicBlock)
        {
            var terminator = basicBlock.Block.GetBasicBlockTerminator();
            if (terminator.Pointer == IntPtr.Zero)
            {
                if (_basicBlocks.Length > _currentOffset)
                {
                    if (_basicBlocks[_currentOffset].StartOffset == 0)
                        throw new InvalidProgramException();
                    MarkBasicBlock(_basicBlocks[_currentOffset]);
                    LLVM.BuildBr(_builder, GetLLVMBasicBlockForBlock(_basicBlocks[_currentOffset]));
                }
            }
        }

        private void StartImportingInstruction()
        {
            if (_debugInformation != null)
            {
                bool foundSequencePoint = false;
                ILSequencePoint curSequencePoint = default;
                foreach (var sequencePoint in _debugInformation.GetSequencePoints() ?? Enumerable.Empty<ILSequencePoint>())
                {
                    if (sequencePoint.Offset == _currentOffset)
                    {
                        curSequencePoint = sequencePoint;
                        foundSequencePoint = true;
                        break;
                    }
                    else if (sequencePoint.Offset < _currentOffset)
                    {
                        curSequencePoint = sequencePoint;
                        foundSequencePoint = true;
                    }
                }

                if (!foundSequencePoint)
                {
                    return;
                }

                // LLVM can't process empty string file names
                if (String.IsNullOrWhiteSpace(curSequencePoint.Document))
                {
                    return;
                }

                DebugMetadata debugMetadata;
                if (!_compilation.DebugMetadataMap.TryGetValue(curSequencePoint.Document, out debugMetadata))
                {
                    string fullPath = curSequencePoint.Document;
                    string fileName = Path.GetFileName(fullPath);
                    string directory = Path.GetDirectoryName(fullPath) ?? String.Empty;
                    LLVMMetadataRef fileMetadata = LLVMPInvokes.LLVMDIBuilderCreateFile(_compilation.DIBuilder, fullPath, fullPath.Length,
                        directory, directory.Length);

                    // todo: get the right value for isOptimized
                    LLVMMetadataRef compileUnitMetadata = LLVMPInvokes.LLVMDIBuilderCreateCompileUnit(_compilation.DIBuilder, LLVMDWARFSourceLanguage.LLVMDWARFSourceLanguageC,
                        fileMetadata, "ILC", 3, isOptimized: false, String.Empty, 0, 1, String.Empty, 0, LLVMDWARFEmissionKind.LLVMDWARFEmissionFull, 0, false, false);
                    LLVM.AddNamedMetadataOperand(Module, "llvm.dbg.cu", LLVM.MetadataAsValue(Context, compileUnitMetadata));

                    debugMetadata = new DebugMetadata(fileMetadata, compileUnitMetadata);
                    _compilation.DebugMetadataMap[fullPath] = debugMetadata;
                }

                if (_debugFunction.Pointer == IntPtr.Zero)
                {
                    _debugFunction = LLVM.DIBuilderCreateFunction(_compilation.DIBuilder, debugMetadata.CompileUnit, _method.Name, String.Empty, debugMetadata.File,
                        (uint)_debugInformation.GetSequencePoints().FirstOrDefault().LineNumber, default(LLVMMetadataRef), 1, 1, 1, 0, IsOptimized: 0, _llvmFunction);
                }

                LLVMMetadataRef currentLine = LLVMPInvokes.LLVMDIBuilderCreateDebugLocation(Context, (uint)curSequencePoint.LineNumber, 0, _debugFunction, default(LLVMMetadataRef));
                LLVM.SetCurrentDebugLocation(_builder, LLVM.MetadataAsValue(Context, currentLine));
            }
        }

        private void EndImportingInstruction()
        {
            // If this was constrained used in a call, it's already been cleared,
            // but if it was on some other instruction, it shoudln't carry forward
            _constrainedType = null;

            // Reset the debug position so it doesn't end up applying to the wrong instructions
            LLVM.SetCurrentDebugLocation(_builder, default(LLVMValueRef));
        }

        private void ImportNop()
        {
            EmitDoNothingCall();
        }

        private void ImportBreak()
        {
            if (DebugtrapFunction.Pointer == IntPtr.Zero)
            {
                DebugtrapFunction = LLVM.AddFunction(Module, "llvm.debugtrap", LLVM.FunctionType(LLVM.VoidType(), Array.Empty<LLVMTypeRef>(), false));
            }
            LLVM.BuildCall(_builder, DebugtrapFunction, Array.Empty<LLVMValueRef>(), string.Empty);
        }

        private void ImportLoadVar(int index, bool argument)
        {
            LLVMValueRef typedLoadLocation = LoadVarAddress(index, argument ? LocalVarKind.Argument : LocalVarKind.Local, out TypeDesc type);
            PushLoadExpression(GetStackValueKind(type), (argument ? "arg" : "loc") + index + "_", typedLoadLocation, type);
        }

        private LLVMValueRef LoadTemp(int index)
        {
            LLVMValueRef address = LoadVarAddress(index, LocalVarKind.Temp, out TypeDesc type);
            return LLVM.BuildLoad(_builder, CastToPointerToTypeDesc(address, type, $"Temp{index}_"), $"LdTemp{index}_"); 
        }

        internal LLVMValueRef LoadTemp(int index, LLVMTypeRef asType)
        {
            LLVMValueRef address = LoadVarAddress(index, LocalVarKind.Temp, out TypeDesc type);
            return LLVM.BuildLoad(_builder, CastIfNecessary(address, LLVM.PointerType(asType, 0), $"Temp{index}_"), $"LdTemp{index}_");
        }

        private void StoreTemp(int index, LLVMValueRef value, string name = null)
        {
            LLVMValueRef address = LoadVarAddress(index, LocalVarKind.Temp, out TypeDesc type);
            LLVM.BuildStore(_builder, CastToTypeDesc(value, type, name), CastToPointerToTypeDesc(address, type, $"Temp{index}_"));
        }

        internal static LLVMValueRef LoadValue(LLVMBuilderRef builder, LLVMValueRef address, TypeDesc sourceType, LLVMTypeRef targetType, bool signExtend, string loadName = null)
        {
            var underlyingSourceType = sourceType.UnderlyingType;
            if (targetType.TypeKind == LLVMTypeKind.LLVMIntegerTypeKind && underlyingSourceType.IsPrimitive && !underlyingSourceType.IsPointer)
            {
                var sourceLLVMType = ILImporter.GetLLVMTypeForTypeDesc(underlyingSourceType);
                var typedAddress = CastIfNecessary(builder, address, LLVM.PointerType(sourceLLVMType, 0));
                return CastIntValue(builder, LLVM.BuildLoad(builder, typedAddress, loadName ?? "ldvalue"), targetType, signExtend);
            }
            else
            {
                var typedAddress = CastIfNecessary(builder, address, LLVM.PointerType(targetType, 0));
                return LLVM.BuildLoad(builder, typedAddress, loadName ?? "ldvalue");
            }
        }

        private static LLVMValueRef CastIntValue(LLVMBuilderRef builder, LLVMValueRef value, LLVMTypeRef type, bool signExtend)
        {
            LLVMTypeKind typeKind = LLVM.TypeOf(value).TypeKind;
            if (LLVM.TypeOf(value).Pointer == type.Pointer)
            {
                return value;
            }
            else if (typeKind == LLVMTypeKind.LLVMPointerTypeKind)
            {
                return LLVM.BuildPtrToInt(builder, value, type, "intcast");
            }
            else if (typeKind == LLVMTypeKind.LLVMFloatTypeKind || typeKind == LLVMTypeKind.LLVMDoubleTypeKind)
            {
                if (signExtend)
                {
                    return LLVM.BuildFPToSI(builder, value, type, "fptosi");
                }
                else
                {
                    return LLVM.BuildFPToUI(builder, value, type, "fptoui");
                }
            }
            else if (signExtend && type.GetIntTypeWidth() > LLVM.TypeOf(value).GetIntTypeWidth())
            {
                return LLVM.BuildSExtOrBitCast(builder, value, type, "SExtOrBitCast");
            }
            else if (type.GetIntTypeWidth() > LLVM.TypeOf(value).GetIntTypeWidth())
            {
                return LLVM.BuildZExtOrBitCast(builder, value, type, "ZExtOrBitCast");
            }
            else
            {
                Debug.Assert(typeKind == LLVMTypeKind.LLVMIntegerTypeKind);
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

                GetArgSizeAndOffsetAtIndex(index, out int argSize, out varOffset, out int realArgIndex);

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

                // If the argument can be passed as a real argument rather than on the shadow stack,
                // get its address here
                if(realArgIndex != -1)
                {
                    return _argSlots[realArgIndex];
                }
            }
            else if (kind == LocalVarKind.Local)
            {
                varBase = GetTotalParameterOffset();
                GetLocalSizeAndOffsetAtIndex(index, out int localSize, out varOffset);
                valueType = GetLLVMTypeForTypeDesc(_locals[index].Type);
                type = _locals[index].Type;
                if(varOffset == -1)
                {
                    Debug.Assert(_localSlots[index].Pointer != IntPtr.Zero);
                    return _localSlots[index];
                }
            }
            else
            {
                varBase = GetTotalRealLocalOffset() + GetTotalParameterOffset();
                GetSpillSizeAndOffsetAtIndex(index, out int localSize, out varOffset);
                valueType = GetLLVMTypeForTypeDesc(_spilledExpressions[index].Type);
                type = _spilledExpressions[index].Type;
            }

            return LLVM.BuildGEP(_builder, LLVM.GetFirstParam(_llvmFunction),
                new LLVMValueRef[] { LLVM.ConstInt(LLVM.Int32Type(), (uint)(varBase + varOffset), LLVMMisc.False) },
                $"{kind}{index}_");

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
            CastingStore(varAddress, toStore, varType, $"Variable{index}_");
        }

        private void ImportStoreHelper(LLVMValueRef toStore, LLVMTypeRef valueType, LLVMValueRef basePtr, uint offset, string name = null)
        {
            LLVMValueRef typedToStore = CastIfNecessary(toStore, valueType, name);
            
            var storeLocation = LLVM.BuildGEP(_builder, basePtr,
                new LLVMValueRef[] { LLVM.ConstInt(LLVM.Int32Type(), offset, LLVMMisc.False) },
                String.Empty);
            var typedStoreLocation = CastIfNecessary(storeLocation, LLVM.PointerType(valueType, 0), "TypedStore" + (name ?? ""));
            LLVM.BuildStore(_builder, typedToStore, typedStoreLocation);
        }

        private LLVMValueRef CastToRawPointer(LLVMValueRef source, string name = null)
        {
            return CastIfNecessary(source, LLVM.PointerType(LLVM.Int8Type(), 0), name);
        }

        private LLVMValueRef CastToTypeDesc(LLVMValueRef source, TypeDesc type, string name = null)
        {
            return CastIfNecessary(source, GetLLVMTypeForTypeDesc(type), (name ?? "") + type.ToString());
        }

        private LLVMValueRef CastToPointerToTypeDesc(LLVMValueRef source, TypeDesc type, string name = null)
        {
            return CastIfNecessary(source, LLVM.PointerType(GetLLVMTypeForTypeDesc(type), 0), (name ?? "") + type.ToString());
        }

        private void CastingStore(LLVMValueRef address, StackEntry value, TypeDesc targetType, string targetName = null)
        {
            var typedStoreLocation = CastToPointerToTypeDesc(address, targetType, targetName);
            LLVM.BuildStore(_builder, value.ValueAsType(targetType, _builder), typedStoreLocation);
        }

        private LLVMValueRef CastIfNecessary(LLVMValueRef source, LLVMTypeRef valueType, string name = null)
        {
            return CastIfNecessary(_builder, source, valueType, name);
        }

        internal static LLVMValueRef CastIfNecessary(LLVMBuilderRef builder, LLVMValueRef source, LLVMTypeRef valueType, string name = null)
        {
            LLVMTypeRef sourceType = LLVM.TypeOf(source);
            if (sourceType.Pointer == valueType.Pointer)
                return source;

            LLVMTypeKind toStoreKind = LLVM.GetTypeKind(sourceType);
            LLVMTypeKind valueTypeKind = LLVM.GetTypeKind(valueType);

            LLVMValueRef typedToStore = source;
            if (toStoreKind == LLVMTypeKind.LLVMPointerTypeKind && valueTypeKind == LLVMTypeKind.LLVMPointerTypeKind)
            {
                typedToStore = LLVM.BuildPointerCast(builder, source, valueType, "CastPtr" + (name ?? ""));
            }
            else if (toStoreKind == LLVMTypeKind.LLVMPointerTypeKind && valueTypeKind == LLVMTypeKind.LLVMIntegerTypeKind)
            {
                typedToStore = LLVM.BuildPtrToInt(builder, source, valueType, "CastInt" + (name ?? ""));
            }
            else if (toStoreKind == LLVMTypeKind.LLVMIntegerTypeKind && valueTypeKind == LLVMTypeKind.LLVMArrayTypeKind)
            {
                typedToStore = LLVM.BuildLoad(builder, CastIfNecessary(builder, source, LLVM.PointerType(valueType, 0), name), "CastArrayLoad" + (name ?? ""));
            }
            else if (toStoreKind == LLVMTypeKind.LLVMPointerTypeKind && valueTypeKind == LLVMTypeKind.LLVMArrayTypeKind)
            {
                typedToStore = LLVM.BuildLoad(builder, CastIfNecessary(builder, source, LLVM.PointerType(valueType, 0), name), "CastArrayLoad" + (name ?? ""));
            }
            else if (toStoreKind == LLVMTypeKind.LLVMPointerTypeKind && valueTypeKind != LLVMTypeKind.LLVMIntegerTypeKind)
            {
                throw new NotImplementedException($"trying to cast {toStoreKind} to {valueTypeKind}");
            }
            else if (toStoreKind == LLVMTypeKind.LLVMIntegerTypeKind && valueTypeKind == LLVMTypeKind.LLVMPointerTypeKind)
            {
                typedToStore = LLVM.BuildIntToPtr(builder, source, valueType, "CastPtr" + (name ?? ""));
            }
            else if (toStoreKind != LLVMTypeKind.LLVMIntegerTypeKind && valueTypeKind == LLVMTypeKind.LLVMPointerTypeKind)
            {
                throw new NotImplementedException($"trying to cast {toStoreKind} to {valueTypeKind}");
            }
            else if (toStoreKind == LLVMTypeKind.LLVMFloatTypeKind && valueTypeKind == LLVMTypeKind.LLVMDoubleTypeKind)
            {
                typedToStore = LLVM.BuildFPExt(builder, source, valueType, "CastFloatToDouble" + (name ?? ""));
            }

            else if (toStoreKind == LLVMTypeKind.LLVMDoubleTypeKind && valueTypeKind == LLVMTypeKind.LLVMFloatTypeKind)
            {
                typedToStore = LLVM.BuildFPTrunc(builder, source, valueType, "CastDoubleToFloat" + (name ?? ""));
            }
            else if (toStoreKind != valueTypeKind && toStoreKind != LLVMTypeKind.LLVMIntegerTypeKind && valueTypeKind != LLVMTypeKind.LLVMIntegerTypeKind)
            {
                throw new NotImplementedException($"trying to cast {toStoreKind} to {valueTypeKind}");
            }
            else if (toStoreKind == valueTypeKind && toStoreKind == LLVMTypeKind.LLVMIntegerTypeKind)
            {
                Debug.Assert(toStoreKind != LLVMTypeKind.LLVMPointerTypeKind && valueTypeKind != LLVMTypeKind.LLVMPointerTypeKind);
                typedToStore = LLVM.BuildIntCast(builder, source, valueType, "CastInt" + (name ?? ""));
            }
            else if (toStoreKind == LLVMTypeKind.LLVMIntegerTypeKind && (valueTypeKind == LLVMTypeKind.LLVMDoubleTypeKind || valueTypeKind == LLVMTypeKind.LLVMFloatTypeKind))
            {
                //TODO: keep track of the TypeDesc so we can call BuildUIToFP when the integer is unsigned
                typedToStore = LLVM.BuildSIToFP(builder, source, valueType, "CastSIToFloat" + (name ?? ""));
            }
            else if ((toStoreKind == LLVMTypeKind.LLVMDoubleTypeKind || toStoreKind == LLVMTypeKind.LLVMFloatTypeKind) && 
                valueTypeKind == LLVMTypeKind.LLVMIntegerTypeKind)
            {
                //TODO: keep track of the TypeDesc so we can call BuildFPToUI when the integer is unsigned
                typedToStore = LLVM.BuildFPToSI(builder, source, valueType, "CastFloatSI" + (name ?? ""));
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
                    {
                        int structSize = type.GetElementSize().AsInt;

                        // LLVM thinks certain sizes of struct have a different calling convention than Clang does.
                        // Treating them as ints fixes that and is more efficient in general
                        switch (structSize)
                        {
                            case 1:
                                return LLVM.Int8Type();
                            case 2:
                                return LLVM.Int16Type();
                            case 4:
                                return LLVM.Int32Type();
                            case 8:
                                return LLVM.Int64Type();
                        }

                        int numInts = structSize / 4;
                        int numBytes = structSize - numInts * 4;
                        LLVMTypeRef[] structMembers = new LLVMTypeRef[numInts + numBytes];
                        for (int i = 0; i < numInts; i++)
                        {
                            structMembers[i] = LLVM.Int32Type();
                        }
                        for (int i = 0; i < numBytes; i++)
                        {
                            structMembers[i + numInts] = LLVM.Int8Type();
                        }

                        return LLVM.StructType(structMembers, true);
                    }

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
                offset = PadNextOffset(_spilledExpressions[i].Type, offset);
            }
            return offset.AlignUp(_pointerSize);
        }

        private int GetTotalRealLocalOffset()
        {
            int offset = 0;
            for (int i = 0; i < _locals.Length; i++)
            {
                TypeDesc localType = _locals[i].Type;
                if (!CanStoreLocalOnStack(localType))
                {
                    offset = PadNextOffset(localType, offset);
                }
            }
            return offset.AlignUp(_pointerSize);
        }

        private bool CanStoreLocalOnStack(TypeDesc localType)
        {
            // Keep all locals on the shadow stack if there is exception
            // handling so funclets can access them
            if (_exceptionRegions.Length == 0)
            {
                return CanStoreTypeOnStack(localType);
            }
            return false;
        }

        /// <summary>
        /// Returns true if the type can be stored on the local stack
        /// instead of the shadow stack in this method.
        /// </summary>
        private static bool CanStoreTypeOnStack(TypeDesc type)
        {
            if (type is DefType defType)
            {
                if (!defType.IsGCPointer && !defType.ContainsGCPointers)
                {
                    return true;
                }
            }
            else if (type is PointerType)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true if the method returns a type that must be kept
        /// on the shadow stack
        /// </summary>
        private static bool NeedsReturnStackSlot(MethodSignature signature)
        {
            return !signature.ReturnType.IsVoid && !CanStoreTypeOnStack(signature.ReturnType);
        }

        private int GetTotalParameterOffset()
        {
            int offset = 0;
            for (int i = 0; i < _signature.Length; i++)
            {
                if (!CanStoreTypeOnStack(_signature[i]))
                {
                    offset = PadNextOffset(_signature[i], offset);
                }
            }
            if (!_signature.IsStatic)
            {
                // If this is a struct, then it's a pointer on the stack
                if (_thisType.IsValueType)
                {
                    offset = PadNextOffset(_thisType.MakeByRefType(), offset);
                }
                else
                {
                    offset = PadNextOffset(_thisType, offset);
                }
            }

            return offset.AlignUp(_pointerSize);
        }

        private void GetArgSizeAndOffsetAtIndex(int index, out int size, out int offset, out int realArgIndex)
        {
            realArgIndex = -1;

            int thisSize = 0;
            if (!_signature.IsStatic)
            {
                thisSize = _thisType.IsValueType ? _thisType.Context.Target.PointerSize : _thisType.GetElementSize().AsInt.AlignUp(_pointerSize);
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

            int potentialRealArgIndex = 0;

            offset = thisSize;
            for (int i = 0; i < index; i++)
            {
                // We could compact the set of argSlots to only those that we'd keep on the stack, but currently don't
                potentialRealArgIndex++;

                if (!CanStoreTypeOnStack(_signature[i]))
                {
                    offset = PadNextOffset(_signature[i], offset);
                }
            }

            if (CanStoreTypeOnStack(argType))
            {
                realArgIndex = potentialRealArgIndex;
                offset = -1;
            }
            else
            {
                offset = PadOffset(argType, offset);
            }
        }

        private void GetLocalSizeAndOffsetAtIndex(int index, out int size, out int offset)
        {
            LocalVariableDefinition local = _locals[index];
            size = local.Type.GetElementSize().AsInt;

            if (CanStoreLocalOnStack(local.Type))
            {
                offset = -1;
            }
            else
            {
                offset = 0;
                for (int i = 0; i < index; i++)
                {
                    if (!CanStoreLocalOnStack(_locals[i].Type))
                    {
                        offset = PadNextOffset(_locals[i].Type, offset);
                    }
                }
                offset = PadOffset(local.Type, offset);
            }
        }

        private void GetSpillSizeAndOffsetAtIndex(int index, out int size, out int offset)
        {
            SpilledExpressionEntry spill = _spilledExpressions[index];
            size = spill.Type.GetElementSize().AsInt;

            offset = 0;
            for (int i = 0; i < index; i++)
            {
                offset = PadNextOffset(_spilledExpressions[i].Type, offset);
            }
            offset = PadOffset(spill.Type, offset);
        }

        public int PadNextOffset(TypeDesc type, int atOffset)
        {
            var size = type is DefType && type.IsValueType ? ((DefType)type).InstanceFieldSize : type.Context.Target.LayoutPointerSize;
            return PadOffset(type, atOffset) + size.AsInt;
        }

        public int PadOffset(TypeDesc type, int atOffset)
        {
            var fieldAlignment = type is DefType && type.IsValueType ? ((DefType)type).InstanceFieldAlignment : type.Context.Target.LayoutPointerSize;
            var alignment = LayoutInt.Min(fieldAlignment, new LayoutInt(ComputePackingSize(type))).AsInt;
            var padding = (atOffset + (alignment - 1)) & ~(alignment - 1);
            return padding;
        }

        private static int ComputePackingSize(TypeDesc type)
        {
            if (type is MetadataType)
            {
                var metaType = type as MetadataType;
                var layoutMetadata = metaType.GetClassLayout();

                // If a type contains pointers then the metadata specified packing size is ignored (On desktop this is disqualification from ManagedSequential)
                if (layoutMetadata.PackingSize == 0 || metaType.ContainsGCPointers)
                    return type.Context.Target.DefaultPackingSize;
                else
                    return layoutMetadata.PackingSize;
            }
            else
                return type.Context.Target.DefaultPackingSize;
        }

        private void ImportAddressOfVar(int index, bool argument)
        {
            TypeDesc type;
            LLVMValueRef typedLoadLocation = LoadVarAddress(index, argument ? LocalVarKind.Argument : LocalVarKind.Local, out type);
            _stack.Push(new AddressExpressionEntry(StackValueKind.ByRef, "ldloca", typedLoadLocation, type.MakeByRefType()));
        }

        private void ImportDup()
        {
            var entry = _stack.Pop();
            _stack.Push(entry.Duplicate(_builder));
            _stack.Push(entry.Duplicate(_builder));
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
            TypeDesc type = ResolveTypeToken(token);
            
            //TODO: call GetCastingHelperNameForType from JitHelper.cs (needs refactoring)
            string function;
            bool throwing = opcode == ILOpcode.castclass;
            if (type.IsArray)
                function = throwing ? "CheckCastArray" : "IsInstanceOfArray";
            else if (type.IsInterface)
                function = throwing ? "CheckCastInterface" : "IsInstanceOfInterface";
            else
                function = throwing ? "CheckCastClass" : "IsInstanceOfClass";

            var arguments = new StackEntry[]
            {
                _stack.Pop(),
                new LoadExpressionEntry(StackValueKind.ValueType, "eeType", GetEETypePointerForTypeDesc(type, true), _compilation.TypeSystemContext.SystemModule.GetKnownType("System", "EETypePtr"))
            };

            _stack.Push(CallRuntime(_compilation.TypeSystemContext, TypeCast, function, arguments, type));
        }

        private void ImportLoadNull()
        {
            _stack.Push(new ExpressionEntry(StackValueKind.ObjRef, "null", LLVM.ConstInt(LLVM.Int32Type(), 0, LLVMMisc.False)));
        }

        private void ImportReturn()
        {
            if (_signature.ReturnType.IsVoid)
            {
                LLVM.BuildRetVoid(_builder);
                return;
            }

            StackEntry retVal = _stack.Pop();
            LLVMTypeRef valueType = GetLLVMTypeForTypeDesc(_signature.ReturnType);
            LLVMValueRef castValue = retVal.ValueAsType(valueType, _builder);

            if (NeedsReturnStackSlot(_signature))
            {
                ImportStoreHelper(castValue, valueType, LLVM.GetNextParam(LLVM.GetFirstParam(_llvmFunction)), 0);
                LLVM.BuildRetVoid(_builder);
            }
            else
            {
                LLVM.BuildRet(_builder, castValue);
            }
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

            if (callee.IsRawPInvoke() || (callee.IsInternalCall && callee.HasCustomAttribute("System.Runtime", "RuntimeImportAttribute")))
            {
                ImportRawPInvoke(callee);
                return;
            }

            if (opcode == ILOpcode.newobj)
            {
                TypeDesc newType = callee.OwningType;
                if (newType.IsArray)
                {
                    var paramCnt = callee.Signature.Length;
                    var eeTypeDesc = _compilation.TypeSystemContext.SystemModule.GetKnownType("Internal.Runtime", "EEType").MakePointerType();
                    LLVMValueRef dimensions = LLVM.BuildArrayAlloca(_builder, LLVMTypeRef.Int32Type(), BuildConstInt32(paramCnt), "newobj_array_pdims_" + _currentOffset);
                    for (int i = paramCnt - 1; i >= 0; --i)
                    {
                        LLVM.BuildStore(_builder, _stack.Pop().ValueAsInt32(_builder, true),
                            LLVM.BuildGEP(_builder, dimensions, new LLVMValueRef[] { BuildConstInt32(i) }, "pdims_ptr"));
                    }
                    var arguments = new StackEntry[]
                    {
                        new LoadExpressionEntry(StackValueKind.ValueType, "eeType", GetEETypePointerForTypeDesc(newType, true), eeTypeDesc),
                        new Int32ConstantEntry(paramCnt),
                        new AddressExpressionEntry(StackValueKind.ValueType, "newobj_array_pdims", dimensions)
                    };
                    MetadataType helperType = _compilation.TypeSystemContext.SystemModule.GetKnownType("Internal.Runtime.CompilerHelpers", "ArrayHelpers");
                    MethodDesc helperMethod = helperType.GetKnownMethod("NewObjArray", null);
                    PushNonNull(HandleCall(helperMethod, helperMethod.Signature, arguments, forcedReturnType: newType));
                    return;
                }
                else if (newType.IsString)
                {
                    // String constructors actually look like regular method calls
                    IMethodNode node = _compilation.NodeFactory.StringAllocator(callee);
                    _dependencies.Add(node);
                    callee = node.Method;
                    opcode = ILOpcode.call;
                }
                else
                {
                    if (callee.Signature.Length > _stack.Length) //System.Reflection.MemberFilter.ctor
                        throw new InvalidProgramException();

                    StackEntry newObjResult;
                    if (newType.IsValueType)
                    {
                        // Allocate a slot on the shadow stack for the value type
                        int spillIndex = _spilledExpressions.Count;
                        SpilledExpressionEntry spillEntry = new SpilledExpressionEntry(GetStackValueKind(newType), "newobj" + _currentOffset, newType, spillIndex, this);
                        _spilledExpressions.Add(spillEntry);
                        LLVMValueRef addrOfValueType = LoadVarAddress(spillIndex, LocalVarKind.Temp, out TypeDesc unused);
                        AddressExpressionEntry valueTypeByRef = new AddressExpressionEntry(StackValueKind.ByRef, "newobj_slot" + _currentOffset, addrOfValueType, newType.MakeByRefType());

                        // The ctor needs a reference to the spill slot, but the 
                        // actual value ends up on the stack after the ctor is done
                        _stack.InsertAt(spillEntry, _stack.Top - callee.Signature.Length);
                        _stack.InsertAt(valueTypeByRef, _stack.Top - callee.Signature.Length);
                    }
                    else
                    {
                        newObjResult = AllocateObject(callee.OwningType);

                        //one for the real result and one to be consumed by ctor
                        _stack.InsertAt(newObjResult, _stack.Top - callee.Signature.Length);
                        _stack.InsertAt(newObjResult, _stack.Top - callee.Signature.Length);
                    }
                }
            }

            if (opcode == ILOpcode.newobj && callee.OwningType.IsDelegate)
            {
                FunctionPointerEntry functionPointer = ((FunctionPointerEntry)_stack.Peek());
                DelegateCreationInfo delegateInfo = _compilation.GetDelegateCtor(callee.OwningType, functionPointer.Method, functionPointer.IsVirtual);
                callee = delegateInfo.Constructor.Method;
                if (callee.Signature.Length == 3)
                {
                    PushExpression(StackValueKind.NativeInt, "thunk", GetOrCreateLLVMFunction(_compilation.NodeFactory.NameMangler.GetMangledMethodName(delegateInfo.Thunk.Method).ToString(), delegateInfo.Thunk.Method.Signature));
                }
            }

            TypeDesc localConstrainedType = _constrainedType;
            _constrainedType = null;
            HandleCall(callee, callee.Signature, opcode, localConstrainedType);
        }

        private LLVMValueRef LLVMFunctionForMethod(MethodDesc callee, StackEntry thisPointer, bool isCallVirt, TypeDesc constrainedType)
        {
            string calleeName = _compilation.NameMangler.GetMangledMethodName(callee).ToString();

            // Sealed methods must not be called virtually due to sealed vTables, so call them directly
            if(callee.IsFinal || callee.OwningType.IsSealed())
            {
                AddMethodReference(callee);
                return GetOrCreateLLVMFunction(calleeName, callee.Signature);
            }

            if (thisPointer != null && callee.IsVirtual && isCallVirt)
            {
                // TODO: Full resolution of virtual methods
                if (!callee.IsNewSlot)
                    throw new NotImplementedException();

                if (!_compilation.HasFixedSlotVTable(callee.OwningType))
                    AddVirtualMethodReference(callee);

                bool isValueTypeCall = false;
                TypeDesc thisType = thisPointer.Type;
                TypeFlags category = thisType.Category;
                MethodDesc targetMethod = null;
                TypeDesc parameterType = null;

                if (category == TypeFlags.ByRef)
                {
                    parameterType = ((ByRefType)thisType).ParameterType;
                    if (parameterType.IsValueType)
                    {
                        isValueTypeCall = true;
                    }
                }

                if(constrainedType != null && constrainedType.IsValueType)
                {
                    isValueTypeCall = true;
                }

                if (isValueTypeCall)
                {
                    if (constrainedType != null)
                    {
                        targetMethod = constrainedType.TryResolveConstraintMethodApprox(callee.OwningType, callee, out _);
                    }
                    else if (callee.OwningType.IsInterface)
                    {
                        targetMethod = parameterType.ResolveInterfaceMethodTarget(callee);
                    }
                    else
                    {
                        targetMethod = parameterType.FindVirtualFunctionTargetMethodOnObjectType(callee);
                    }
                }

                if (targetMethod != null)
                {
                    AddMethodReference(targetMethod);
                    return GetOrCreateLLVMFunction(_compilation.NameMangler.GetMangledMethodName(targetMethod).ToString(), callee.Signature);
                }

                return GetCallableVirtualMethod(thisPointer, callee);

            }
            else
            {
                return GetOrCreateLLVMFunction(calleeName, callee.Signature);
            }
        }

        private LLVMValueRef GetOrCreateMethodSlot(MethodDesc method)
        {
            var vtableSlotSymbol = _compilation.NodeFactory.VTableSlot(method);
            _dependencies.Add(vtableSlotSymbol);
            LLVMValueRef slot = LoadAddressOfSymbolNode(vtableSlotSymbol);
            return LLVM.BuildLoad(_builder, slot, $"{method.Name}_slot");
        }

        private LLVMValueRef GetCallableVirtualMethod(StackEntry objectPtr, MethodDesc method)
        {
            Debug.Assert(method.IsVirtual);
            LLVMValueRef slot = GetOrCreateMethodSlot(method);
            var pointerSize = method.Context.Target.PointerSize;
            LLVMTypeRef llvmSignature = GetLLVMSignatureForMethod(method.Signature);
            LLVMValueRef functionPtr;
            if (method.OwningType.IsInterface)
            {
                var eeTypeDesc = _compilation.TypeSystemContext.SystemModule.GetKnownType("System", "EETypePtr");
                var interfaceEEType = new LoadExpressionEntry(StackValueKind.ValueType, "interfaceEEType", GetEETypePointerForTypeDesc(method.OwningType, true), eeTypeDesc);
                var eeTypeExpression = new LoadExpressionEntry(StackValueKind.ValueType, "eeType", objectPtr.ValueAsType(LLVM.PointerType(LLVM.Int8Type(), 0), _builder), eeTypeDesc);
                var targetEntry = CallRuntime(_compilation.TypeSystemContext, DispatchResolve, "FindInterfaceMethodImplementationTarget", new StackEntry[] { eeTypeExpression, interfaceEEType, new ExpressionEntry(StackValueKind.Int32, "slot", slot, GetWellKnownType(WellKnownType.UInt16)) });
                functionPtr = targetEntry.ValueAsType(LLVM.PointerType(llvmSignature, 0), _builder);
            }
            else
            {
                var rawObjectPtr = CastIfNecessary(objectPtr.ValueAsType(LLVM.PointerType(LLVM.Int8Type(), 0), _builder), LLVM.PointerType(LLVM.PointerType(LLVM.PointerType(llvmSignature, 0), 0), 0), objectPtr.Name());
                var eeType = LLVM.BuildLoad(_builder, rawObjectPtr, "ldEEType");
                var slotPtr = LLVM.BuildGEP(_builder, eeType, new LLVMValueRef[] { slot }, "__getslot__");
                functionPtr = LLVM.BuildLoad(_builder, slotPtr, "ld__getslot__");
            }

            return functionPtr;
        }

        private LLVMTypeRef GetLLVMSignatureForMethod(MethodSignature signature)
        {
            TypeDesc returnType = signature.ReturnType;
            LLVMTypeRef llvmReturnType;
            bool returnOnStack = false;
            if (!NeedsReturnStackSlot(signature))
            {
                returnOnStack = true;
                llvmReturnType = GetLLVMTypeForTypeDesc(returnType);
            }
            else
            {
                llvmReturnType = LLVM.VoidType();
            }

            List<LLVMTypeRef> signatureTypes = new List<LLVMTypeRef>();
            signatureTypes.Add(LLVM.PointerType(LLVM.Int8Type(), 0)); // Shadow stack pointer

            if (!returnOnStack && returnType != GetWellKnownType(WellKnownType.Void))
            {
                signatureTypes.Add(LLVM.PointerType(LLVM.Int8Type(), 0));
            }

            // Intentionally skipping the 'this' pointer since it could always be a GC reference
            // and thus must be on the shadow stack
            foreach (TypeDesc type in signature)
            {
                if (CanStoreTypeOnStack(type))
                {
                    signatureTypes.Add(GetLLVMTypeForTypeDesc(type));
                }
            }

            return LLVM.FunctionType(llvmReturnType, signatureTypes.ToArray(), false);
        }

        private ExpressionEntry AllocateObject(TypeDesc type)
        {
            MetadataType metadataType = (MetadataType)type;
            var eeTypeDesc = _compilation.TypeSystemContext.SystemModule.GetKnownType("System", "EETypePtr");
            var arguments = new StackEntry[] { new LoadExpressionEntry(StackValueKind.ValueType, "eeType", GetEETypePointerForTypeDesc(metadataType, true), eeTypeDesc) };
            //TODO: call GetNewObjectHelperForType from JitHelper.cs (needs refactoring)
            return CallRuntime(_compilation.TypeSystemContext, RuntimeExport, "RhNewObject", arguments, type);
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

        private static LLVMValueRef BuildConstInt16(byte number)
        {
            return LLVM.ConstInt(LLVM.Int16Type(), number, LLVMMisc.False);
        }

        private static LLVMValueRef BuildConstInt32(int number)
        {
            return LLVM.ConstInt(LLVM.Int32Type(), (ulong)number, LLVMMisc.False);
        }

        private static LLVMValueRef BuildConstInt64(long number)
        {
            return LLVM.ConstInt(LLVM.Int64Type(), (ulong)number, LLVMMisc.False);
        }

        private LLVMValueRef GetEETypeForTypeDesc(TypeDesc target, bool constructed)
        {
            var eeTypePointer = GetEETypePointerForTypeDesc(target, constructed);
            return LLVM.BuildLoad(_builder, eeTypePointer, "eeTypePtrLoad");
        }

        private LLVMValueRef GetEETypePointerForTypeDesc(TypeDesc target, bool constructed)
        {
            ISymbolNode node;
            if (constructed)
            {
                node = _compilation.NodeFactory.MaximallyConstructableType(target);
            }
            else
            {
                node = _compilation.NodeFactory.NecessaryTypeSymbol(target);
            }
            LLVMValueRef eeTypePointer = WebAssemblyObjectWriter.GetSymbolValuePointer(Module, node, _compilation.NameMangler, false);
            _dependencies.Add(node);

            return eeTypePointer;
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
                case "InitializeArray":
                    if (metadataType.Namespace == "System.Runtime.CompilerServices" && metadataType.Name == "RuntimeHelpers")
                    {
                        StackEntry fieldSlot = _stack.Pop();
                        StackEntry arraySlot = _stack.Pop();

                        // TODO: Does fldHandle always come from ldtoken? If not, what to do with other cases?
                        if (!(fieldSlot is LdTokenEntry<FieldDesc> checkedFieldSlot) ||
                            !(_compilation.GetFieldRvaData(checkedFieldSlot.LdToken) is BlobNode fieldNode))
                            throw new InvalidProgramException("Provided field handle is invalid.");

                        LLVMValueRef src = LoadAddressOfSymbolNode(fieldNode);
                        _dependencies.Add(fieldNode);
                        int srcLength = fieldNode.GetData(_compilation.NodeFactory, false).Data.Length;

                        if (arraySlot.Type.IsSzArray)
                        {
                            // Handle single dimensional arrays (vectors).
                            LLVMValueRef arrayObjPtr = arraySlot.ValueAsType(LLVM.PointerType(LLVM.Int8Type(), 0), _builder);

                            var argsType = new LLVMTypeRef[]
                            {
                            LLVM.PointerType(LLVM.Int8Type(), 0),
                            LLVM.PointerType(LLVM.Int8Type(), 0),
                            LLVM.Int32Type(),
                            LLVM.Int32Type(),
                            LLVM.Int1Type()
                            };
                            LLVMValueRef memcpyFunction = GetOrCreateLLVMFunction("llvm.memcpy.p0i8.p0i8.i32", LLVM.FunctionType(LLVM.VoidType(), argsType, false));

                            var args = new LLVMValueRef[]
                            {
                            LLVM.BuildGEP(_builder, arrayObjPtr, new LLVMValueRef[] { ArrayBaseSize() }, string.Empty),
                            LLVM.BuildBitCast(_builder, src, LLVM.PointerType(LLVM.Int8Type(), 0), string.Empty),
                            BuildConstInt32(srcLength), // TODO: Handle destination array length to avoid runtime overflow.
                            BuildConstInt32(0), // Assume no alignment
                            BuildConstInt1(0)
                            };
                            LLVM.BuildCall(_builder, memcpyFunction, args, string.Empty);
                        }
                        else if (arraySlot.Type.IsMdArray)
                        {
                            // Handle multidimensional arrays.
                            // TODO: Add support for multidimensional array.
                            throw new NotImplementedException();
                        }
                        else
                        {
                            // Handle object-typed first argument. This include System.Array typed array, and any ill-typed argument.
                            // TODO: Emit runtime type check code on array argument and further memcpy.
                            // TODO: Maybe a new runtime interface for this is better than hand-written code emission?
                            throw new NotImplementedException();
                        }
                        
                        return true;
                    }
                    break;
                case "get_Value":
                    if (metadataType.IsByReferenceOfT)
                    {
                        StackEntry byRefHolder = _stack.Pop();

                        TypeDesc byRefType = metadataType.Instantiation[0].MakeByRefType();
                        PushLoadExpression(StackValueKind.ByRef, "byref", byRefHolder.ValueForStackKind(StackValueKind.ByRef, _builder, false), byRefType);
                        return true;
                    }
                    break;
                case ".ctor":
                    if (metadataType.IsByReferenceOfT)
                    {
                        StackEntry byRefValueParamHolder = _stack.Pop();

                        // Allocate a slot on the shadow stack for the ByReference type
                        int spillIndex = _spilledExpressions.Count;
                        SpilledExpressionEntry spillEntry = new SpilledExpressionEntry(StackValueKind.ByRef, "byref" + _currentOffset, metadataType, spillIndex, this);
                        _spilledExpressions.Add(spillEntry);
                        LLVMValueRef addrOfValueType = LoadVarAddress(spillIndex, LocalVarKind.Temp, out TypeDesc unused);
                        var typedAddress = CastIfNecessary(_builder, addrOfValueType, LLVM.PointerType(LLVM.Int32Type(), 0));
                        LLVM.BuildStore(_builder, byRefValueParamHolder.ValueForStackKind(StackValueKind.ByRef, _builder, false), typedAddress);

                        _stack.Push(spillEntry);
                        return true;
                    }
                    break;
            }

            return false;
        }

        private void HandleCall(MethodDesc callee, MethodSignature signature, ILOpcode opcode = ILOpcode.call, TypeDesc constrainedType = null, LLVMValueRef calliTarget = default(LLVMValueRef))
        {
            var parameterCount = signature.Length + (signature.IsStatic ? 0 : 1);
            // The last argument is the top of the stack. We need to reverse them and store starting at the first argument
            StackEntry[] argumentValues = new StackEntry[parameterCount];
            for (int i = 0; i < argumentValues.Length; i++)
            {
                argumentValues[argumentValues.Length - i - 1] = _stack.Pop();
            }

            if (constrainedType != null)
            {
                if (signature.IsStatic)
                {
                    // Constrained call on static method
                    ThrowHelper.ThrowInvalidProgramException(ExceptionStringID.InvalidProgramSpecific, _method);
                }
                StackEntry thisByRef = argumentValues[0];
                if (thisByRef.Kind != StackValueKind.ByRef)
                {
                    // Constrained call without byref
                    ThrowHelper.ThrowInvalidProgramException(ExceptionStringID.InvalidProgramSpecific, _method);
                }

                // If this is a constrained call and the 'this' pointer is a reference type, it's a byref,
                // dereference it before calling.
                if (!constrainedType.IsValueType)
                {
                    TypeDesc objectType = thisByRef.Type.GetParameterType();
                    argumentValues[0] = new LoadExpressionEntry(StackValueKind.ObjRef, "thisPtr", thisByRef.ValueAsType(objectType, _builder), objectType);
                }
            }

            PushNonNull(HandleCall(callee, signature, argumentValues, opcode, constrainedType, calliTarget));
        }

        private ExpressionEntry HandleCall(MethodDesc callee, MethodSignature signature, StackEntry[] argumentValues, ILOpcode opcode = ILOpcode.call, TypeDesc constrainedType = null, LLVMValueRef calliTarget = default(LLVMValueRef), TypeDesc forcedReturnType = null)
        {
            if (opcode == ILOpcode.callvirt && callee.IsVirtual)
            {
                AddVirtualMethodReference(callee);
            }
            else if (callee != null)
            {
                AddMethodReference(callee);
            }
            var pointerSize = _compilation.NodeFactory.Target.PointerSize;

            LLVMValueRef returnAddress;
            LLVMValueRef castReturnAddress = default;
            TypeDesc returnType = signature.ReturnType;

            bool needsReturnSlot = NeedsReturnStackSlot(signature);
            SpilledExpressionEntry returnSlot = null;
            if (needsReturnSlot)
            {
                int returnIndex = _spilledExpressions.Count;
                returnSlot = new SpilledExpressionEntry(GetStackValueKind(returnType), callee?.Name + "_return", returnType, returnIndex, this);
                _spilledExpressions.Add(returnSlot);
                returnAddress = LoadVarAddress(returnIndex, LocalVarKind.Temp, out TypeDesc unused);
                castReturnAddress = LLVM.BuildPointerCast(_builder, returnAddress, LLVM.PointerType(LLVM.Int8Type(), 0), callee?.Name + "_castreturn");
            }

            int offset = GetTotalParameterOffset() + GetTotalLocalOffset();
            LLVMValueRef shadowStack = LLVM.BuildGEP(_builder, LLVM.GetFirstParam(_llvmFunction),
                new LLVMValueRef[] { LLVM.ConstInt(LLVM.Int32Type(), (uint)offset, LLVMMisc.False) },
                String.Empty);
            var castShadowStack = LLVM.BuildPointerCast(_builder, shadowStack, LLVM.PointerType(LLVM.Int8Type(), 0), "castshadowstack");

            List<LLVMValueRef> llvmArgs = new List<LLVMValueRef>();
            llvmArgs.Add(castShadowStack);
            if (needsReturnSlot)
            {
                llvmArgs.Add(castReturnAddress);
            }

            // argument offset on the shadow stack
            int argOffset = 0;
            var instanceAdjustment = signature.IsStatic ? 0 : 1;
            for (int index = 0; index < argumentValues.Length; index++)
            {
                StackEntry toStore = argumentValues[index];

                bool isThisParameter = false;
                TypeDesc argType;
                if (index == 0 && !signature.IsStatic)
                {
                    isThisParameter = true;
                    if (opcode == ILOpcode.calli)
                        argType = toStore.Type;
                    else if (callee.OwningType.IsValueType)
                        argType = callee.OwningType.MakeByRefType();
                    else
                        argType = callee.OwningType;
                }
                else
                {
                    argType = signature[index - instanceAdjustment];
                }

                LLVMTypeRef valueType = GetLLVMTypeForTypeDesc(argType);
                LLVMValueRef argValue = toStore.ValueAsType(valueType, _builder);

                // Pass arguments as parameters if possible
                if (!isThisParameter && CanStoreTypeOnStack(argType))
                {
                    llvmArgs.Add(argValue);
                }
                // Otherwise store them on the shadow stack
                else
                {
                    // The previous argument might have left this type unaligned, so pad if necessary
                    argOffset = PadOffset(argType, argOffset);

                    ImportStoreHelper(argValue, valueType, castShadowStack, (uint)argOffset);

                    argOffset += argType.GetElementSize().AsInt;
                }
            }

            LLVMValueRef fn;
            if (opcode == ILOpcode.calli)
            {
                fn = calliTarget;
            }
            else
            {
                fn = LLVMFunctionForMethod(callee, signature.IsStatic ? null : argumentValues[0], opcode == ILOpcode.callvirt, constrainedType);
            }

            LLVMValueRef llvmReturn = LLVM.BuildCall(_builder, fn, llvmArgs.ToArray(), string.Empty);
            
            if (!returnType.IsVoid)
            {
                if (needsReturnSlot)
                {
                    return returnSlot;
                }
                else
                {
                    return new ExpressionEntry(GetStackValueKind(returnType), callee?.Name + "_return", llvmReturn, returnType);
                }
            }
            else
            {
                return null;
            }
        }

        private void AddMethodReference(MethodDesc method)
        {
            _dependencies.Add(_compilation.NodeFactory.MethodEntrypoint(method));
        }

        private void AddVirtualMethodReference(MethodDesc method)
        {
            _dependencies.Add(_compilation.NodeFactory.VirtualMethodUse(method));
        }
        static Dictionary<string, MethodDesc> _pinvokeMap = new Dictionary<string, MethodDesc>();
        private void ImportRawPInvoke(MethodDesc method)
        {
            var arguments = new StackEntry[method.Signature.Length];
            for(int i = 0; i < arguments.Length; i++)
            {
                // Arguments are reversed on the stack
                // Coerce pointers to the native type
                arguments[arguments.Length - i - 1] = _stack.Pop();
            }


            PushNonNull(ImportRawPInvoke(method, arguments));
        }

        private ExpressionEntry ImportRawPInvoke(MethodDesc method, StackEntry[] arguments, TypeDesc forcedReturnType = null)
        {
            //emscripten dies if this is output because its expected to have i32, i32, i64. But the runtime has defined it as i8*, i8*, i64
            if (method.Name == "memmove")
                throw new NotImplementedException();

            string realMethodName = method.Name;

            if (method.IsPInvoke)
            {
                string entrypointName = method.GetPInvokeMethodMetadata().Name;
                if(!String.IsNullOrEmpty(entrypointName))
                {
                    realMethodName = entrypointName;
                }
            }
            else if (!method.IsPInvoke && method is TypeSystem.Ecma.EcmaMethod)
            {
                realMethodName = ((TypeSystem.Ecma.EcmaMethod)method).GetRuntimeImportName() ?? method.Name;
            }
            MethodDesc existantDesc;
            LLVMValueRef nativeFunc;
            LLVMValueRef realNativeFunc = LLVM.GetNamedFunction(Module, realMethodName);
            if (_pinvokeMap.TryGetValue(realMethodName, out existantDesc))
            {
                if (existantDesc != method)
                {
                    // Set up native parameter types
                    nativeFunc = MakeExternFunction(method, realMethodName, realNativeFunc);
                }
                else
                {
                    nativeFunc = realNativeFunc;
                }
            }
            else
            {
                _pinvokeMap.Add(realMethodName, method);
                nativeFunc = realNativeFunc;
            }

            // Create an import if we haven't already
            if (nativeFunc.Pointer == IntPtr.Zero)
            {
                // Set up native parameter types
                nativeFunc = MakeExternFunction(method, realMethodName);
            }

            LLVMValueRef[] llvmArguments = new LLVMValueRef[method.Signature.Length];
            for (int i = 0; i < arguments.Length; i++)
            {
                TypeDesc signatureType = method.Signature[i];
                llvmArguments[i] = arguments[i].ValueAsType(GetLLVMTypeForTypeDesc(signatureType), _builder);
            }

            // Save the top of the shadow stack in case the callee reverse P/Invokes
            LLVMValueRef stackFrameSize = BuildConstInt32(GetTotalParameterOffset() + GetTotalLocalOffset());
            LLVM.BuildStore(_builder, LLVM.BuildGEP(_builder, LLVM.GetFirstParam(_llvmFunction), new LLVMValueRef[] { stackFrameSize }, "shadowStackTop"),
                LLVM.GetNamedGlobal(Module, "t_pShadowStackTop"));

            // Don't name the return value if the function returns void, it's invalid
            var returnValue = LLVM.BuildCall(_builder, nativeFunc, llvmArguments, !method.Signature.ReturnType.IsVoid ? "call" : string.Empty);

            if (!method.Signature.ReturnType.IsVoid)
                return new ExpressionEntry(GetStackValueKind(method.Signature.ReturnType), "retval", returnValue, forcedReturnType ?? method.Signature.ReturnType);
            else
                return null;
        }

        private LLVMValueRef MakeExternFunction(MethodDesc method, string realMethodName, LLVMValueRef realFunction = default(LLVMValueRef))
        {
            LLVMValueRef nativeFunc;
            LLVMTypeRef[] paramTypes = new LLVMTypeRef[method.Signature.Length];
            for (int i = 0; i < paramTypes.Length; i++)
            {
                paramTypes[i] = GetLLVMTypeForTypeDesc(method.Signature[i]);
            }

            // Define the full signature
            LLVMTypeRef nativeFuncType = LLVM.FunctionType(GetLLVMTypeForTypeDesc(method.Signature.ReturnType), paramTypes, LLVMMisc.False);

            if (realFunction.Pointer == IntPtr.Zero)
            {
                nativeFunc = LLVM.AddFunction(Module, realMethodName, nativeFuncType);
                LLVM.SetLinkage(nativeFunc, LLVMLinkage.LLVMDLLImportLinkage);
            }
            else
            {
                nativeFunc = LLVM.BuildPointerCast(_builder, realFunction, LLVM.PointerType(nativeFuncType, 0), realMethodName + "__slot__");
            }
            return nativeFunc;
        }

        static LLVMValueRef s_shadowStackTop = default(LLVMValueRef);
        LLVMValueRef ShadowStackTop
        {
            get
            {
                if (s_shadowStackTop.Pointer.Equals(IntPtr.Zero))
                {
                    s_shadowStackTop = LLVM.AddGlobal(Module, LLVM.PointerType(LLVM.Int8Type(), 0), "t_pShadowStackTop");
                    LLVM.SetLinkage(s_shadowStackTop, LLVMLinkage.LLVMInternalLinkage);
                    LLVM.SetInitializer(s_shadowStackTop, LLVM.ConstPointerNull(LLVM.PointerType(LLVM.Int8Type(), 0)));
                    LLVM.SetThreadLocal(s_shadowStackTop, LLVMMisc.True);                    
                }
                return s_shadowStackTop;
            }
        }

        private void EmitNativeToManagedThunk(WebAssemblyCodegenCompilation compilation, MethodDesc method, string nativeName, LLVMValueRef managedFunction)
        {
            if (_pinvokeMap.TryGetValue(nativeName, out MethodDesc existing))
            {
                if (existing != method)
                    throw new InvalidProgramException("export and import function were mismatched");
            }
            else
            {
                _pinvokeMap.Add(nativeName, method);
            }

            LLVMTypeRef[] llvmParams = new LLVMTypeRef[method.Signature.Length];
            for (int i = 0; i < llvmParams.Length; i++)
            {
                llvmParams[i] = GetLLVMTypeForTypeDesc(method.Signature[i]);
            }

            LLVMTypeRef thunkSig = LLVM.FunctionType(GetLLVMTypeForTypeDesc(method.Signature.ReturnType), llvmParams, false);
            LLVMValueRef thunkFunc = LLVM.AddFunction(compilation.Module, nativeName, thunkSig);

            LLVMBasicBlockRef shadowStackSetupBlock = LLVM.AppendBasicBlock(thunkFunc, "ShadowStackSetupBlock");
            LLVMBasicBlockRef allocateShadowStackBlock = LLVM.AppendBasicBlock(thunkFunc, "allocateShadowStackBlock");
            LLVMBasicBlockRef managedCallBlock = LLVM.AppendBasicBlock(thunkFunc, "ManagedCallBlock");

            LLVMBuilderRef builder = LLVM.CreateBuilder();
            LLVM.PositionBuilderAtEnd(builder, shadowStackSetupBlock);

            // Allocate shadow stack if it's null
            LLVMValueRef shadowStackPtr = LLVM.BuildAlloca(builder, LLVM.PointerType(LLVM.Int8Type(), 0), "ShadowStackPtr");
            LLVMValueRef savedShadowStack = LLVM.BuildLoad(builder, ShadowStackTop, "SavedShadowStack");
            LLVM.BuildStore(builder, savedShadowStack, shadowStackPtr);
            LLVMValueRef shadowStackNull = LLVM.BuildICmp(builder, LLVMIntPredicate.LLVMIntEQ, savedShadowStack, LLVM.ConstPointerNull(LLVM.PointerType(LLVM.Int8Type(), 0)), "ShadowStackNull");
            LLVM.BuildCondBr(builder, shadowStackNull, allocateShadowStackBlock, managedCallBlock);

            LLVM.PositionBuilderAtEnd(builder, allocateShadowStackBlock);

            LLVMValueRef newShadowStack = LLVM.BuildArrayMalloc(builder, LLVM.Int8Type(), BuildConstInt32(1000000), "NewShadowStack");
            LLVM.BuildStore(builder, newShadowStack, shadowStackPtr);
            LLVM.BuildBr(builder, managedCallBlock);

            LLVM.PositionBuilderAtEnd(builder, managedCallBlock);
            LLVMTypeRef reversePInvokeFrameType = LLVM.StructType(new LLVMTypeRef[] { LLVM.PointerType(LLVM.Int8Type(), 0), LLVM.PointerType(LLVM.Int8Type(), 0) }, false);
            LLVMValueRef reversePInvokeFrame = default(LLVMValueRef);
            LLVMTypeRef reversePInvokeFunctionType = LLVM.FunctionType(LLVM.VoidType(), new LLVMTypeRef[] { LLVM.PointerType(reversePInvokeFrameType, 0) }, false);
            if (method.IsNativeCallable)
            {
                reversePInvokeFrame = LLVM.BuildAlloca(builder, reversePInvokeFrameType, "ReversePInvokeFrame");
                LLVMValueRef RhpReversePInvoke2 = GetOrCreateLLVMFunction("RhpReversePInvoke2", reversePInvokeFunctionType);
                LLVM.BuildCall(builder, RhpReversePInvoke2, new LLVMValueRef[] { reversePInvokeFrame }, "");
            }

            LLVMValueRef shadowStack = LLVM.BuildLoad(builder, shadowStackPtr, "ShadowStack");
            int curOffset = 0;
            curOffset = PadNextOffset(method.Signature.ReturnType, curOffset);
            LLVMValueRef calleeFrame = LLVM.BuildGEP(builder, shadowStack, new LLVMValueRef[] { BuildConstInt32(curOffset) }, "calleeFrame");

            List<LLVMValueRef> llvmArgs = new List<LLVMValueRef>();
            llvmArgs.Add(calleeFrame);

            bool needsReturnSlot = NeedsReturnStackSlot(method.Signature);

            if (needsReturnSlot)
            {
                // Slot for return value if necessary
                llvmArgs.Add(shadowStack);
            }

            for (int i = 0; i < llvmParams.Length; i++)
            {
                LLVMValueRef argValue = LLVM.GetParam(thunkFunc, (uint)i);

                if (CanStoreTypeOnStack(method.Signature[i]))
                {
                    llvmArgs.Add(argValue);
                }
                else
                {
                    curOffset = PadOffset(method.Signature[i], curOffset);
                    LLVMValueRef argAddr = LLVM.BuildGEP(builder, shadowStack, new LLVMValueRef[] { LLVM.ConstInt(LLVM.Int32Type(), (ulong)curOffset, LLVMMisc.False) }, "arg" + i);
                    LLVM.BuildStore(builder, argValue, CastIfNecessary(builder, argAddr, LLVM.PointerType(llvmParams[i], 0), $"parameter{i}_"));
                    curOffset = PadNextOffset(method.Signature[i], curOffset);
                }
            }

            LLVMValueRef llvmReturnValue = LLVM.BuildCall(builder, managedFunction, llvmArgs.ToArray(), "");

            if (method.IsNativeCallable)
            {
                LLVMValueRef RhpReversePInvokeReturn2 = GetOrCreateLLVMFunction("RhpReversePInvokeReturn2", reversePInvokeFunctionType);
                LLVM.BuildCall(builder, RhpReversePInvokeReturn2, new LLVMValueRef[] { reversePInvokeFrame }, "");
            }

            if (!method.Signature.ReturnType.IsVoid)
            {
                if (needsReturnSlot)
                {
                    LLVM.BuildRet(builder, LLVM.BuildLoad(builder, CastIfNecessary(builder, shadowStack, LLVM.PointerType(GetLLVMTypeForTypeDesc(method.Signature.ReturnType), 0)), "returnValue"));
                }
                else
                {
                    LLVM.BuildRet(builder, llvmReturnValue);
                }
            }
            else
            {
                LLVM.BuildRetVoid(builder);
            }
        }

        private void ImportCalli(int token)
        {
            MethodSignature methodSignature = (MethodSignature)_methodIL.GetObject(token);
            HandleCall(null, methodSignature, ILOpcode.calli, calliTarget: ((ExpressionEntry)_stack.Pop()).ValueAsType(LLVM.PointerType(GetLLVMSignatureForMethod(methodSignature), 0), _builder));
        }

        private void ImportLdFtn(int token, ILOpcode opCode)
        {
            MethodDesc method = (MethodDesc)_methodIL.GetObject(token);
            LLVMValueRef targetLLVMFunction = default(LLVMValueRef);
            if (opCode == ILOpcode.ldvirtftn)
            {
                StackEntry thisPointer = _stack.Pop();
                if (method.IsVirtual)
                {
                    targetLLVMFunction = LLVMFunctionForMethod(method, thisPointer, true, null);
                    AddVirtualMethodReference(method);
                }
            }
            else
            {
                AddMethodReference(method);
            }

            if (targetLLVMFunction.Pointer.Equals(IntPtr.Zero))
            {
                targetLLVMFunction = GetOrCreateLLVMFunction(_compilation.NameMangler.GetMangledMethodName(method).ToString(), method.Signature);
            }

            var entry = new FunctionPointerEntry("ldftn", method, targetLLVMFunction, GetWellKnownType(WellKnownType.IntPtr), opCode == ILOpcode.ldvirtftn);
            _stack.Push(entry);
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

                    LLVMValueRef right = op1.ValueForStackKind(kind, _builder, false);
                    LLVMValueRef left = op2.ValueForStackKind(kind, _builder, false);

                    if (kind != StackValueKind.Float)
                    {
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
                    else
                    {
                        switch (opcode)
                        {
                            case ILOpcode.beq:
                                condition = LLVM.BuildFCmp(_builder, LLVMRealPredicate.LLVMRealOEQ, left, right, "beq");
                                break;
                            case ILOpcode.bge:
                                condition = LLVM.BuildFCmp(_builder, LLVMRealPredicate.LLVMRealOGE, left, right, "bge");
                                break;
                            case ILOpcode.bgt:
                                condition = LLVM.BuildFCmp(_builder, LLVMRealPredicate.LLVMRealOGT, left, right, "bgt");
                                break;
                            case ILOpcode.ble:
                                condition = LLVM.BuildFCmp(_builder, LLVMRealPredicate.LLVMRealOLE, left, right, "ble");
                                break;
                            case ILOpcode.blt:
                                condition = LLVM.BuildFCmp(_builder, LLVMRealPredicate.LLVMRealOLT, left, right, "blt");
                                break;
                            case ILOpcode.bne_un:
                                condition = LLVM.BuildFCmp(_builder, LLVMRealPredicate.LLVMRealONE, left, right, "bne_un");
                                break;
                            case ILOpcode.bge_un:
                                condition = LLVM.BuildFCmp(_builder, LLVMRealPredicate.LLVMRealUGE, left, right, "bge_un");
                                break;
                            case ILOpcode.bgt_un:
                                condition = LLVM.BuildFCmp(_builder, LLVMRealPredicate.LLVMRealUGT, left, right, "bgt_un");
                                break;
                            case ILOpcode.ble_un:
                                condition = LLVM.BuildFCmp(_builder, LLVMRealPredicate.LLVMRealULE, left, right, "ble_un");
                                break;
                            case ILOpcode.blt_un:
                                condition = LLVM.BuildFCmp(_builder, LLVMRealPredicate.LLVMRealULT, left, right, "blt_un");
                                break;
                            default:
                                throw new NotSupportedException(); // unreachable
                        }
                    }
                }

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
            if(type == null)
            {
                type = GetWellKnownType(WellKnownType.Object);
            }

            LLVMValueRef pointerElementType = pointer.ValueAsType(type.MakePointerType(), _builder);
            _stack.Push(new LoadExpressionEntry(type != null ? GetStackValueKind(type) : StackValueKind.ByRef, $"Indirect{pointer.Name()}",
                pointerElementType, type));
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
            LLVMValueRef left = op2.ValueForStackKind(kind, _builder, false);
            LLVMValueRef right = op1.ValueForStackKind(kind, _builder, false);
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

        bool TypeNeedsSignExtension(TypeDesc targetType)
        {
            var enumCleanTargetType = targetType?.UnderlyingType;
            if(enumCleanTargetType != null && targetType.IsPrimitive)
            {
                if(enumCleanTargetType.IsWellKnownType(WellKnownType.Byte) ||
                    enumCleanTargetType.IsWellKnownType(WellKnownType.Char) ||
                    enumCleanTargetType.IsWellKnownType(WellKnownType.UInt16) ||
                    enumCleanTargetType.IsWellKnownType(WellKnownType.UInt32) ||
                    enumCleanTargetType.IsWellKnownType(WellKnownType.UInt64) ||
                    enumCleanTargetType.IsWellKnownType(WellKnownType.UIntPtr))
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            return false;
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
            LLVMValueRef typeSaneOp1 = op1.ValueForStackKind(kind, _builder, TypeNeedsSignExtension(op1.Type));
            LLVMValueRef typeSaneOp2 = op2.ValueForStackKind(kind, _builder, TypeNeedsSignExtension(op2.Type));

            if (kind != StackValueKind.Float)
            {
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
            }
            else
            {
                switch (opcode)
                {
                    case ILOpcode.ceq:
                        result = LLVM.BuildFCmp(_builder, LLVMRealPredicate.LLVMRealOEQ, typeSaneOp2, typeSaneOp1, "ceq");
                        break;
                    case ILOpcode.cgt:
                        result = LLVM.BuildFCmp(_builder, LLVMRealPredicate.LLVMRealOGT, typeSaneOp2, typeSaneOp1, "cgt");
                        break;
                    case ILOpcode.clt:
                        result = LLVM.BuildFCmp(_builder, LLVMRealPredicate.LLVMRealOLT, typeSaneOp2, typeSaneOp1, "clt");
                        break;
                    case ILOpcode.cgt_un:
                        result = LLVM.BuildFCmp(_builder, LLVMRealPredicate.LLVMRealUGT, typeSaneOp2, typeSaneOp1, "cgt_un");
                        break;
                    case ILOpcode.clt_un:
                        result = LLVM.BuildFCmp(_builder, LLVMRealPredicate.LLVMRealULT, typeSaneOp2, typeSaneOp1, "clt_un");
                        break;
                    default:
                        throw new NotSupportedException(); // unreachable
                }
            }

            PushExpression(StackValueKind.Int32, "cmpop", result, GetWellKnownType(WellKnownType.SByte));
        }

        private void ImportConvert(WellKnownType wellKnownType, bool checkOverflow, bool unsigned)
        {
            StackEntry value = _stack.Pop();
            TypeDesc destType = GetWellKnownType(wellKnownType);

            // Load the value and then convert it instead of using ValueAsType to avoid loading the incorrect size
            LLVMValueRef loadedValue = value.ValueAsType(value.Type, _builder);
            LLVMValueRef converted = CastIfNecessary(loadedValue, GetLLVMTypeForTypeDesc(destType), value.Name());
            PushExpression(GetStackValueKind(destType), "conv", converted, destType);
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
            var type = ResolveTypeToken(token);

            if (!type.IsValueType)
            {
                throw new InvalidOperationException();
            }

            var src = _stack.Pop();

            if (src.Kind != StackValueKind.NativeInt && src.Kind != StackValueKind.ByRef && src.Kind != StackValueKind.ObjRef)
            {
                throw new InvalidOperationException();
            }

            var dest = _stack.Pop();

            if (dest.Kind != StackValueKind.NativeInt && dest.Kind != StackValueKind.ByRef && dest.Kind != StackValueKind.ObjRef)
            {
                throw new InvalidOperationException();
            }

            var pointerType = GetLLVMTypeForTypeDesc(type.MakePointerType());

            var value = LLVM.BuildLoad(_builder, src.ValueAsType(pointerType, _builder), "cpobj.load");

            LLVM.BuildStore(_builder, value, dest.ValueAsType(pointerType, _builder));
        }

        private void ImportUnbox(int token, ILOpcode opCode)
        {
            TypeDesc type = ResolveTypeToken(token);
            LLVMValueRef eeType = GetEETypePointerForTypeDesc(type, true);
            var eeTypeDesc = _compilation.TypeSystemContext.SystemModule.GetKnownType("System", "EETypePtr");
            StackEntry boxedObject = _stack.Pop();
            if (opCode == ILOpcode.unbox)
            {
                if (type.IsNullable)
                    throw new NotImplementedException();

                var arguments = new StackEntry[] { new LoadExpressionEntry(StackValueKind.ByRef, "eeType", eeType, eeTypeDesc), boxedObject };
                PushNonNull(CallRuntime(_compilation.TypeSystemContext, RuntimeExport, "RhUnbox2", arguments));
            }
            else //unbox_any
            {
                Debug.Assert(opCode == ILOpcode.unbox_any);
                LLVMValueRef untypedObjectValue = LLVM.BuildAlloca(_builder, GetLLVMTypeForTypeDesc(type), "objptr");
                var arguments = new StackEntry[]
                {
                    boxedObject,
                    new ExpressionEntry(StackValueKind.ByRef, "objPtr", untypedObjectValue, type.MakePointerType()),
                    new LoadExpressionEntry(StackValueKind.ByRef, "eeType", eeType, eeTypeDesc)
                };
                CallRuntime(_compilation.TypeSystemContext, RuntimeExport, "RhUnboxAny", arguments);
                PushLoadExpression(GetStackValueKind(type), "unboxed", untypedObjectValue, type);
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
                PushLoadExpression(StackValueKind.ByRef, "ldtoken", GetEETypePointerForTypeDesc(ldtokenValue as TypeDesc, false), _compilation.TypeSystemContext.SystemModule.GetKnownType("System", "EETypePtr"));
                MethodDesc helper = _compilation.TypeSystemContext.GetHelperEntryPoint("LdTokenHelpers", "GetRuntimeTypeHandle");
                AddMethodReference(helper);
                HandleCall(helper, helper.Signature);
                name = ldtokenValue.ToString();
            }
            else if (ldtokenValue is FieldDesc)
            {
                ldtokenKind = WellKnownType.RuntimeFieldHandle;
                LLVMValueRef fieldHandle = LLVM.ConstStruct(new LLVMValueRef[] { BuildConstInt32(0) }, true);
                value = new LdTokenEntry<FieldDesc>(StackValueKind.ValueType, null, (FieldDesc)ldtokenValue, fieldHandle, GetWellKnownType(ldtokenKind));
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
            StackEntry allocSizeEntry = _stack.Pop();
            LLVMValueRef allocSize = allocSizeEntry.ValueAsInt32(_builder, false);
            LLVMValueRef allocatedMemory = LLVM.BuildArrayAlloca(_builder, LLVMTypeRef.Int8Type(), allocSize, "localloc" + _currentOffset);
            LLVM.SetAlignment(allocatedMemory, (uint)_pointerSize);
            if (_methodIL.IsInitLocals)
            {
                ImportCallMemset(allocatedMemory, 0, allocSize);
            }

            PushExpression(StackValueKind.NativeInt, "localloc" + _currentOffset, allocatedMemory, _compilation.TypeSystemContext.GetPointerType(GetWellKnownType(WellKnownType.Void)));
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
            TypeDesc type = (TypeDesc)_methodIL.GetObject(token);
            int size = type.GetElementSize().AsInt;
            PushExpression(StackValueKind.Int32, "sizeof", LLVM.ConstInt(LLVM.Int32Type(), (ulong)size, LLVMMisc.False), GetWellKnownType(WellKnownType.Int32));
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
            _constrainedType = (TypeDesc)_methodIL.GetObject(token);
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

                ISymbolNode node;
                MetadataType owningType = (MetadataType)field.OwningType;
                LLVMValueRef staticBase;
                int fieldOffset = field.Offset.AsInt;

                // TODO: We need the right thread static per thread
                if (field.IsThreadStatic)
                {
                    node = _compilation.NodeFactory.TypeThreadStaticsSymbol(owningType);
                    staticBase = LoadAddressOfSymbolNode(node);
                }

                else if (field.HasGCStaticBase)
                {
                    node = _compilation.NodeFactory.TypeGCStaticsSymbol(owningType);

                    // We can't use GCStatics in the data section until we can successfully call
                    // InitializeModules on startup, so stick with globals for now
                    //LLVMValueRef basePtrPtr = LoadAddressOfSymbolNode(node);
                    //staticBase = LLVM.BuildLoad(_builder, LLVM.BuildLoad(_builder, LLVM.BuildPointerCast(_builder, basePtrPtr, LLVM.PointerType(LLVM.PointerType(LLVM.PointerType(LLVM.Int8Type(), 0), 0), 0), "castBasePtrPtr"), "basePtr"), "base");
                    staticBase = WebAssemblyObjectWriter.EmitGlobal(Module, field, _compilation.NameMangler);
                    fieldOffset = 0;
                }
                else
                {
                    node = _compilation.NodeFactory.TypeNonGCStaticsSymbol(owningType);
                    staticBase = LoadAddressOfSymbolNode(node);
                }

                _dependencies.Add(node);

                // Run static constructor if necessary
                // If the type is non-BeforeFieldInit, this is handled before calling any methods on it
                if (owningType.IsBeforeFieldInit || (!owningType.IsBeforeFieldInit && owningType != _thisType))
                {
                    TriggerCctor(owningType);
                }

                LLVMValueRef castStaticBase = LLVM.BuildPointerCast(_builder, staticBase, LLVM.PointerType(LLVM.Int8Type(), 0), owningType.Name + "_statics");
                LLVMValueRef fieldAddr = LLVM.BuildGEP(_builder, castStaticBase, new LLVMValueRef[] { BuildConstInt32(fieldOffset) }, field.Name + "_addr");


                return fieldAddr;
            }
            else
            {
                return GetInstanceFieldAddress(_stack.Pop(), field);
            }
        }

        /// <summary>
        /// Triggers a static constructor check and call for types that have them
        /// </summary>
        private void TriggerCctor(MetadataType type)
        {
            if (_compilation.TypeSystemContext.HasLazyStaticConstructor(type))
            {
                ISymbolNode classConstructionContextSymbol = _compilation.NodeFactory.TypeNonGCStaticsSymbol(type);
                _dependencies.Add(classConstructionContextSymbol);
                LLVMValueRef firstNonGcStatic = LoadAddressOfSymbolNode(classConstructionContextSymbol);

                // TODO: Codegen could check whether it has already run rather than calling into EnsureClassConstructorRun
                // but we'd have to figure out how to manage the additional basic blocks
                LLVMValueRef classConstructionContextPtr = LLVM.BuildGEP(_builder, firstNonGcStatic, new LLVMValueRef[] { BuildConstInt32(-2) }, "classConstructionContext");
                StackEntry classConstructionContext = new AddressExpressionEntry(StackValueKind.NativeInt, "classConstructionContext", classConstructionContextPtr, GetWellKnownType(WellKnownType.IntPtr));
                MetadataType helperType = _compilation.TypeSystemContext.SystemModule.GetKnownType("System.Runtime.CompilerServices", "ClassConstructorRunner");
                MethodDesc helperMethod = helperType.GetKnownMethod("EnsureClassConstructorRun", null);
                HandleCall(helperMethod, helperMethod.Signature, new StackEntry[] { classConstructionContext });
            }
        }

        private void ImportLoadField(int token, bool isStatic)
        {
            FieldDesc field = (FieldDesc)_methodIL.GetObject(token);
            LLVMValueRef fieldAddress = GetFieldAddress(field, isStatic);
            PushLoadExpression(GetStackValueKind(field.FieldType), $"Field_{field.Name}", fieldAddress, field.FieldType);
        }

        private void ImportAddressOfField(int token, bool isStatic)
        {
            FieldDesc field = (FieldDesc)_methodIL.GetObject(token);
            LLVMValueRef fieldAddress = GetFieldAddress(field, isStatic);
            _stack.Push(new AddressExpressionEntry(StackValueKind.ByRef, $"FieldAddress_{field.Name}", fieldAddress, field.FieldType.MakeByRefType()));
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
            if (llvmType.TypeKind == LLVMTypeKind.LLVMStructTypeKind)
            {
                ImportCallMemset(valueEntry.ValueAsType(LLVM.PointerType(LLVM.Int8Type(), 0), _builder), 0, type.GetElementSize().AsInt);
            }
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
            LLVMValueRef eeType = GetEETypePointerForTypeDesc(type, true);
            var eeTypeDesc = _compilation.TypeSystemContext.SystemModule.GetKnownType("System", "EETypePtr");
            var valueAddress = TakeAddressOf(_stack.Pop());
            var eeTypeEntry = new LoadExpressionEntry(StackValueKind.ValueType, "eeType", eeType, eeTypeDesc.MakePointerType());
            if (type.IsValueType)
            {
                var arguments = new StackEntry[] { eeTypeEntry, valueAddress };
                PushNonNull(CallRuntime(_compilation.TypeSystemContext, RuntimeExport, "RhBox", arguments));
            }
            else
            {
                var arguments = new StackEntry[] { valueAddress, eeTypeEntry };
                PushNonNull(CallRuntime(_compilation.TypeSystemContext, RuntimeExport, "RhBoxAny", arguments));
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
            TypeDesc arrayType = ResolveTypeToken(token).MakeArrayType();
            var sizeOfArray = _stack.Pop();
            var eeTypeDesc = _compilation.TypeSystemContext.SystemModule.GetKnownType("Internal.Runtime", "EEType").MakePointerType();
            var arguments = new StackEntry[] { new LoadExpressionEntry(StackValueKind.ValueType, "eeType", GetEETypePointerForTypeDesc(arrayType, true), eeTypeDesc), sizeOfArray };
            //TODO: call GetNewArrayHelperForType from JitHelper.cs (needs refactoring)
            PushNonNull(CallRuntime(_compilation.TypeSystemContext, InternalCalls, "RhpNewArray", arguments, arrayType));
        }

        private LLVMValueRef ArrayBaseSize()
        {
            return BuildConstInt32(2 * _compilation.NodeFactory.Target.PointerSize);
        }

        private void ImportLoadElement(int token)
        {
            ImportLoadElement(ResolveTypeToken(token));
        }

        private void ImportLoadElement(TypeDesc elementType)
        {
            StackEntry index = _stack.Pop();
            StackEntry arrayReference = _stack.Pop();
            var nullSafeElementType = elementType ?? GetWellKnownType(WellKnownType.Object);
            PushLoadExpression(GetStackValueKind(nullSafeElementType), $"{arrayReference.Name()}Element", GetElementAddress(index.ValueAsInt32(_builder, true), arrayReference.ValueAsType(LLVM.PointerType(LLVM.Int8Type(), 0), _builder), nullSafeElementType), nullSafeElementType);
        }

        private void ImportStoreElement(int token)
        {
            ImportStoreElement(ResolveTypeToken(token));
        }

        private void ImportStoreElement(TypeDesc elementType)
        {
            StackEntry value = _stack.Pop();
            StackEntry index = _stack.Pop();
            StackEntry arrayReference = _stack.Pop();
            var nullSafeElementType = elementType ?? GetWellKnownType(WellKnownType.Object);
            LLVMValueRef elementAddress = GetElementAddress(index.ValueAsInt32(_builder, true), arrayReference.ValueAsType(LLVM.PointerType(LLVM.Int8Type(), 0), _builder), nullSafeElementType);
            CastingStore(elementAddress, value, nullSafeElementType);
        }

        private void ImportLoadLength()
        {
            StackEntry arrayReference = _stack.Pop();
            LLVMValueRef lengthPtr = LLVM.BuildGEP(_builder, arrayReference.ValueAsType(LLVM.PointerType(LLVM.Int8Type(), 0), _builder), new LLVMValueRef[] { BuildConstInt32(_compilation.NodeFactory.Target.PointerSize) }, "arrayLength");
            LLVMValueRef castLengthPtr = LLVM.BuildPointerCast(_builder, lengthPtr, LLVM.PointerType(LLVM.Int32Type(), 0), "castArrayLength");
            PushLoadExpression(StackValueKind.Int32, "arrayLength", castLengthPtr, GetWellKnownType(WellKnownType.Int32));
        }

        private void ImportAddressOfElement(int token)
        {
            TypeDesc elementType = ResolveTypeToken(token);
            var byRefElement = elementType.MakeByRefType();
            StackEntry index = _stack.Pop();
            StackEntry arrayReference = _stack.Pop();

            PushExpression(GetStackValueKind(byRefElement), $"{arrayReference.Name()}ElementAddress", GetElementAddress(index.ValueAsInt32(_builder, true), arrayReference.ValueAsType(LLVM.PointerType(LLVM.Int8Type(), 0), _builder), elementType), byRefElement);
        }

        private LLVMValueRef GetElementAddress(LLVMValueRef elementPosition, LLVMValueRef arrayReference, TypeDesc arrayElementType)
        {
            var elementSize = arrayElementType.IsValueType ? ((DefType)arrayElementType).InstanceByteCount : arrayElementType.GetElementSize();
            LLVMValueRef elementOffset = LLVM.BuildMul(_builder, elementPosition, BuildConstInt32(elementSize.AsInt), "elementOffset");
            LLVMValueRef arrayOffset = LLVM.BuildAdd(_builder, elementOffset, ArrayBaseSize(), "arrayOffset");
            return LLVM.BuildGEP(_builder, arrayReference, new LLVMValueRef[] { arrayOffset }, "elementPointer");
        }

        LLVMValueRef EmitRuntimeHelperCall(string name, TypeDesc returnType, LLVMValueRef[] parameters)
        {
            var runtimeHelperSig = LLVM.FunctionType(GetLLVMTypeForTypeDesc(returnType), parameters.Select(valRef => LLVM.TypeOf(valRef)).ToArray(), false);
            var runtimeHelper = GetOrCreateLLVMFunction(name, runtimeHelperSig);
            return LLVM.BuildCall(_builder, runtimeHelper, parameters, "call_" + name);
        }

        private void ImportEndFinally()
        {
            // These are currently unreachable since we can't get into finally blocks.
            // We'll need to change this once we have other finally block handling.
            LLVM.BuildUnreachable(_builder);
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

                    StoreTemp(entry.LocalIndex, currentEntry.ValueAsType(entry.Type, _builder));
                }
            }

            MarkBasicBlock(next);

        }

        private const string RuntimeExport = "RuntimeExports";
        private const string RuntimeImport = "RuntimeImports";
        private const string InternalCalls = "InternalCalls";
        private const string TypeCast = "TypeCast";
        private const string DispatchResolve = "DispatchResolve";
        private ExpressionEntry CallRuntime(TypeSystemContext context, string className, string methodName, StackEntry[] arguments, TypeDesc forcedReturnType = null)
        {
            MetadataType helperType = context.SystemModule.GetKnownType("System.Runtime", className);
            MethodDesc helperMethod = helperType.GetKnownMethod(methodName, null);
            if((helperMethod.IsInternalCall && helperMethod.HasCustomAttribute("System.Runtime", "RuntimeImportAttribute")))
                return ImportRawPInvoke(helperMethod, arguments, forcedReturnType: forcedReturnType);
            else
                return HandleCall(helperMethod, helperMethod.Signature, arguments, forcedReturnType: forcedReturnType);
        }

        private void PushNonNull(StackEntry entry)
        {
            if (entry != null)
            {
                _stack.Push(entry);
            }
        }

        private StackEntry NewSpillSlot(StackEntry entry)
        {
            var entryType = entry.Type ?? GetWellKnownType(WellKnownType.Object); //type is required here, currently the only time entry.Type is null is if someone has pushed a null literal
            var entryIndex = _spilledExpressions.Count;
            var newEntry = new SpilledExpressionEntry(entry.Kind, entry is ExpressionEntry ? ((ExpressionEntry)entry).Name : "spilled" + entryIndex, entryType, entryIndex, this);
            _spilledExpressions.Add(newEntry);
            return newEntry;
        }

        private StackEntry TakeAddressOf(StackEntry entry)
        {
            var entryType = entry.Type ?? GetWellKnownType(WellKnownType.Object); //type is required here, currently the only time entry.Type is null is if someone has pushed a null literal

            LLVMValueRef addressValue;
            if(entry is LoadExpressionEntry)
            {
                addressValue = ((LoadExpressionEntry)entry).RawLLVMValue;
            }
            else if (entry is SpilledExpressionEntry)
            {
                int spillIndex = ((SpilledExpressionEntry)entry).LocalIndex;
                addressValue = LoadVarAddress(spillIndex, LocalVarKind.Temp, out TypeDesc unused);
            }
            else
            {
                //This path should only ever be taken for constants and the results of a primitive cast (not writable)
                //all other cases should be operating on a LoadExpressionEntry
                var entryIndex = _spilledExpressions.Count;
                var newEntry = new SpilledExpressionEntry(entry.Kind, entry is ExpressionEntry ? ((ExpressionEntry)entry).Name : "address_of_temp" + entryIndex, entryType, entryIndex, this);
                _spilledExpressions.Add(newEntry);

                if (entry is ExpressionEntry)
                    StoreTemp(entryIndex, ((ExpressionEntry)entry).RawLLVMValue);
                else
                    StoreTemp(entryIndex, entry.ValueForStackKind(entry.Kind, _builder, false));

                addressValue = LoadVarAddress(entryIndex, LocalVarKind.Temp, out TypeDesc type);
            }

            return new AddressExpressionEntry(StackValueKind.NativeInt, "address_of", addressValue, entry.Type.MakePointerType());
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

        private void ReportMethodEndInsideInstruction()
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
            LLVM.BuildUnreachable(_builder);
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
