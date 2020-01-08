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
using ILCompiler.WebAssembly;
using Internal.IL.Stubs;
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
        public static LLVMContextRef Context { get; private set; }
        private static Dictionary<TypeDesc, LLVMTypeRef> LlvmStructs { get; } = new Dictionary<TypeDesc, LLVMTypeRef>();
        private static MetadataFieldLayoutAlgorithm LayoutAlgorithm { get; } = new MetadataFieldLayoutAlgorithm();
        private readonly MethodDesc _method;
        private readonly MethodIL _methodIL;
        private readonly MethodIL _canonMethodIL;
        private readonly MethodSignature _signature;
        private readonly TypeDesc _thisType;
        private readonly WebAssemblyCodegenCompilation _compilation;
        private readonly string _mangledName;
        private LLVMValueRef _llvmFunction;
        private LLVMValueRef _currentFunclet;
        private bool _isUnboxingThunk;
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

        List<LLVMValueRef> _exceptionFunclets;

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
            public LLVMBasicBlockRef LastInternalIf;
            public LLVMBasicBlockRef LastBlock => LastInternalIf.Pointer == IntPtr.Zero ? Block : LastInternalIf;
        }

        private class ExceptionRegion
        {
            public ILExceptionRegion ILRegion;
        }
        private ExceptionRegion[] _exceptionRegions;
        public ILImporter(WebAssemblyCodegenCompilation compilation, MethodDesc method, MethodIL methodIL, string mangledName, bool isUnboxingThunk)
        {
            Module = compilation.Module;
            _compilation = compilation;
            _method = method;
            _isUnboxingThunk = isUnboxingThunk;
            // stubs for Unix calls which are not available to this target yet
            if ((method.OwningType as EcmaType)?.Name == "Interop" && method.Name == "GetRandomBytes")
            {
                // this would normally fill the buffer parameter, but we'll just leave the buffer as is and that will be our "random" data for now
                methodIL = new ILStubMethodIL(method, new byte[] { (byte)ILOpcode.ret }, Array.Empty<LocalVariableDefinition>(), null);
            }
            else if ((method.OwningType as EcmaType)?.Name == "CalendarData" && method.Name == "EnumCalendarInfo")
            {
                // just return false 
                methodIL = new ILStubMethodIL(method, new byte[] { (byte)ILOpcode.ldc_i4_0, (byte)ILOpcode.ret }, Array.Empty<LocalVariableDefinition>(), null);
            }

            _canonMethodIL = methodIL;
            // Get the runtime determined method IL so that this works right in shared code
            // and tokens in shared code resolve to runtime determined types.
            MethodIL uninstantiatiedMethodIL = methodIL.GetMethodILDefinition();
            if (methodIL != uninstantiatiedMethodIL)
            {
                MethodDesc sharedMethod = method.GetSharedRuntimeFormMethodTarget();
                _methodIL = new InstantiatedMethodIL(sharedMethod, uninstantiatiedMethodIL);
            }
            else
            {
                _methodIL = methodIL;
            }

            _mangledName = mangledName;
            _ilBytes = methodIL.GetILBytes();
            _locals = methodIL.GetLocals();
            _localSlots = new LLVMValueRef[_locals.Length];
            _argSlots = new LLVMValueRef[method.Signature.Length];
            _signature = method.Signature;
            _thisType = method.OwningType;
            var ilExceptionRegions = methodIL.GetExceptionRegions();
            _exceptionRegions = new ExceptionRegion[ilExceptionRegions.Length];
            _exceptionFunclets = new List<LLVMValueRef>(_exceptionRegions.Length);
            int curRegion = 0;
            foreach (ILExceptionRegion region in ilExceptionRegions.OrderBy(region => region.TryOffset))
            {
                _exceptionRegions[curRegion++] = new ExceptionRegion() { ILRegion = region };
            }

            _llvmFunction = GetOrCreateLLVMFunction(mangledName, method.Signature, method.RequiresInstArg());
            _currentFunclet = _llvmFunction;
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

                foreach (LLVMValueRef funclet in _exceptionFunclets)
                {
                    LLVM.DeleteFunction(funclet);
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
            // shadow stack pointer and return address
            int signatureIndex = 1;
            if (NeedsReturnStackSlot(_signature))
            {
                signatureIndex++;
            }
            if (_method.RequiresInstArg()) // hidden param after shadow stack pointer and return slot if present
            {
                signatureIndex++;
            }

            IList<string> argNames = null;
            if (_debugInformation != null)
            {
                argNames = GetParameterNamesForMethod(_method);
            }

            for (int i = 0; i < _signature.Length; i++)
            {
                if (CanStoreTypeOnStack(_signature[i]))
                {
                    LLVMValueRef storageAddr;
                    LLVMValueRef argValue = LLVM.GetParam(_llvmFunction, (uint)signatureIndex);

                    // The caller will always pass the argument on the stack. If this function doesn't have 
                    // EH, we can put it in an alloca for efficiency and better debugging. Otherwise,
                    // copy it to the shadow stack so funclets can find it
                    int argOffset = i + thisOffset;
                    if (_exceptionRegions.Length == 0)
                    {
                        string argName = String.Empty;
                        if (argNames != null && argNames[argOffset] != null)
                        {
                            argName = argNames[argOffset] + "_";
                        }
                        argName += $"arg{argOffset}_";

                        storageAddr = LLVM.BuildAlloca(_builder, GetLLVMTypeForTypeDesc(_signature[i]), argName);
                        _argSlots[i] = storageAddr;
                    }
                    else
                    {
                        storageAddr = CastIfNecessary(LoadVarAddress(argOffset, LocalVarKind.Argument, out _), LLVM.PointerType(LLVM.TypeOf(argValue), 0));
                    }
                    LLVM.BuildStore(_builder, argValue, storageAddr);
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
                if (CanStoreVariableOnStack(_locals[i].Type))
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
                for (int i = 0; i < _locals.Length; i++)
                {
                    LLVMValueRef localAddr = LoadVarAddress(i, LocalVarKind.Local, out TypeDesc localType);
                    if (CanStoreVariableOnStack(localType))
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

            if (_thisType is MetadataType metadataType && !metadataType.IsBeforeFieldInit
                && (!_method.IsStaticConstructor && _method.Signature.IsStatic || _method.IsConstructor || (_thisType.IsValueType && !_method.Signature.IsStatic))
                && _compilation.TypeSystemContext.HasLazyStaticConstructor(metadataType))
            {
                TriggerCctor(metadataType);
            }

            LLVMBasicBlockRef block0 = GetLLVMBasicBlockForBlock(_basicBlocks[0]);
            LLVM.BuildBr(_builder, block0);
        }

        private LLVMValueRef CreateLLVMFunction(string mangledName, MethodSignature signature, bool hasHiddenParameter)
        {
            return LLVM.AddFunction(Module, mangledName, GetLLVMSignatureForMethod(signature, hasHiddenParameter));
        }

        private LLVMValueRef GetOrCreateLLVMFunction(string mangledName, MethodSignature signature, bool hasHiddenParam)
        {
            LLVMValueRef llvmFunction = LLVM.GetNamedFunction(Module, mangledName);

            if (llvmFunction.Pointer == IntPtr.Zero)
            {
                return CreateLLVMFunction(mangledName, signature, hasHiddenParam);
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

        /// <summary>
        /// Gets or creates an LLVM function for an exception handling funclet
        /// </summary>
        private LLVMValueRef GetOrCreateFunclet(ILExceptionRegionKind kind, int handlerOffset)
        {
            string funcletName = _mangledName + "$" + kind.ToString() + handlerOffset.ToString("X");
            LLVMValueRef funclet = LLVM.GetNamedFunction(Module, funcletName);
            if (funclet.Pointer == IntPtr.Zero)
            {
                // Funclets accept a shadow stack pointer and a generic ctx hidden param if the owning method has one
                var funcletArgs = new LLVMTypeRef[FuncletsRequireHiddenContext() ? 2 : 1];
                funcletArgs[0] = LLVM.PointerType(LLVM.Int8Type(), 0);
                if (FuncletsRequireHiddenContext())
                {
                    funcletArgs[1] = LLVM.PointerType(LLVM.Int8Type(), 0);
                }
                LLVMTypeRef universalFuncletSignature = LLVM.FunctionType(LLVM.VoidType(), funcletArgs, false);
                funclet = LLVM.AddFunction(Module, funcletName, universalFuncletSignature);
                _exceptionFunclets.Add(funclet);
            }

            return funclet;
        }

        private void ImportCallMemset(LLVMValueRef targetPointer, byte value, int length)
        {
            LLVMValueRef objectSizeValue = BuildConstInt32(length);
            ImportCallMemset(targetPointer, value, objectSizeValue);
        }

        private void ImportCallMemset(LLVMValueRef targetPointer, byte value, LLVMValueRef length)
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
                LLVMValueRef blockFunclet = GetFuncletForBlock(block);

                block.Block = LLVM.AppendBasicBlock(blockFunclet, "Block" + block.StartOffset.ToString("X"));
            }
            return block.Block;
        }

        /// <summary>
        /// Gets or creates the LLVM function or funclet the basic block is part of
        /// </summary>
        private LLVMValueRef GetFuncletForBlock(BasicBlock block)
        {
            LLVMValueRef blockFunclet;

            // Find the matching funclet for this block
            ExceptionRegion ehRegion = GetHandlerRegion(block.StartOffset);

            if (ehRegion != null)
            {
                blockFunclet = GetOrCreateFunclet(ehRegion.ILRegion.Kind, ehRegion.ILRegion.HandlerOffset);
            }
            else
            {
                blockFunclet = _llvmFunction;
            }

            return blockFunclet;
        }

        /// <summary>
        /// Returns the most nested exception handler region the offset is in
        /// </summary>
        /// <returns>An exception region or null if it is not in an exception region</returns>
        private ExceptionRegion GetHandlerRegion(int offset)
        {
            // Iterate backwards to find the most nested region
            for (int i = _exceptionRegions.Length - 1; i >= 0; i--)
            {
                ExceptionRegion region = _exceptionRegions[i];
                if (IsOffsetContained(offset, region.ILRegion.HandlerOffset, region.ILRegion.HandlerLength))
                {
                    return region;
                }
            }

            return null;
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
            _currentFunclet = GetFuncletForBlock(basicBlock);

            LLVM.PositionBuilderAtEnd(_builder, _curBasicBlock);
        }

        private void EndImportingBasicBlock(BasicBlock basicBlock)
        {
            var terminator = basicBlock.LastBlock.GetBasicBlockTerminator();
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

        private LLVMValueRef StoreTemp(int index, LLVMValueRef value, string name = null)
        {
            LLVMValueRef address = LoadVarAddress(index, LocalVarKind.Temp, out TypeDesc type);
            LLVM.BuildStore(_builder, CastToTypeDesc(value, type, name), CastToPointerToTypeDesc(address, type, $"Temp{index}_"));
            return address;
        }

        internal static LLVMValueRef LoadValue(LLVMBuilderRef builder, LLVMValueRef address, TypeDesc sourceType, LLVMTypeRef targetType, bool signExtend, string loadName = null)
        {
            var underlyingSourceType = sourceType.UnderlyingType;
            if (targetType.TypeKind == LLVMTypeKind.LLVMIntegerTypeKind && underlyingSourceType.IsPrimitive && !underlyingSourceType.IsPointer)
            {
                LLVMValueRef loadValueRef = CastIfNecessaryAndLoad(builder, address, underlyingSourceType, loadName);
                return CastIntValue(builder, loadValueRef, targetType, signExtend);
            }
            else if (targetType.TypeKind == LLVMTypeKind.LLVMDoubleTypeKind)
            {
                LLVMValueRef loadValueRef = CastIfNecessaryAndLoad(builder, address, underlyingSourceType, loadName);
                return CastDoubleValue(builder, loadValueRef, targetType);
            }
            else
            {
                var typedAddress = CastIfNecessary(builder, address, LLVM.PointerType(targetType, 0));
                return LLVM.BuildLoad(builder, typedAddress, loadName ?? "ldvalue");
            }
        }

        private static LLVMValueRef CastIfNecessaryAndLoad(LLVMBuilderRef builder, LLVMValueRef address, TypeDesc sourceTypeDesc, string loadName)
        {
            LLVMTypeRef sourceLLVMType = ILImporter.GetLLVMTypeForTypeDesc(sourceTypeDesc);
            LLVMValueRef typedAddress = CastIfNecessary(builder, address, LLVM.PointerType(sourceLLVMType, 0));
            return LLVM.BuildLoad(builder, typedAddress, loadName ?? "ldvalue");
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

        private static LLVMValueRef CastDoubleValue(LLVMBuilderRef builder, LLVMValueRef value, LLVMTypeRef type)
        {
            if (LLVM.TypeOf(value).Pointer == type.Pointer)
            {
                return value;
            }
            Debug.Assert(LLVM.TypeOf(value).TypeKind == LLVMTypeKind.LLVMFloatTypeKind);
            return LLVM.BuildFPExt(builder, value, type, "fpext");
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
                if (realArgIndex != -1)
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
                if (varOffset == -1)
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

            return LLVM.BuildGEP(_builder, LLVM.GetFirstParam(_currentFunclet),
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

        private void ImportStoreHelper(LLVMValueRef toStore, LLVMTypeRef valueType, LLVMValueRef basePtr, uint offset, string name = null, LLVMBuilderRef builder = default(LLVMBuilderRef))
        {
            if (builder.Pointer == IntPtr.Zero)
                builder = _builder;

            LLVMValueRef typedToStore = CastIfNecessary(builder, toStore, valueType, name);

            var storeLocation = LLVM.BuildGEP(builder, basePtr,
                new LLVMValueRef[] { LLVM.ConstInt(LLVM.Int32Type(), offset, LLVMMisc.False) },
                String.Empty);
            var typedStoreLocation = CastIfNecessary(builder, storeLocation, LLVM.PointerType(valueType, 0), "TypedStore" + (name ?? ""));
            LLVM.BuildStore(builder, typedToStore, typedStoreLocation);
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

        private LLVMValueRef CastIfNecessary(LLVMValueRef source, LLVMTypeRef valueType, string name = null, bool unsigned = false)
        {
            return CastIfNecessary(_builder, source, valueType, name, unsigned);
        }

        internal static LLVMValueRef CastIfNecessary(LLVMBuilderRef builder, LLVMValueRef source, LLVMTypeRef valueType, string name = null, bool unsigned = false)
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
                // when extending unsigned ints do fill left with 0s, zext
                typedToStore = unsigned && sourceType.GetIntTypeWidth() < valueType.GetIntTypeWidth()
                    ? LLVM.BuildZExt(builder, source, valueType, "CastZInt" + (name ?? ""))
                    : LLVM.BuildIntCast(builder, source, valueType, "CastInt" + (name ?? ""));
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
                        if (!LlvmStructs.TryGetValue(type, out LLVMTypeRef llvmStructType))
                        {
                            // LLVM thinks certain sizes of struct have a different calling convention than Clang does.
                            // Treating them as ints fixes that and is more efficient in general
                            int structSize = type.GetElementSize().AsInt;
                            int structAlignment = ((DefType)type).InstanceFieldAlignment.AsInt;
                            switch (structSize)
                            {
                                case 1:
                                    llvmStructType = LLVM.Int8Type();
                                    break;
                                case 2:
                                    if (structAlignment == 2)
                                    {
                                        llvmStructType = LLVM.Int16Type();
                                    }
                                    else
                                    {
                                        goto default;
                                    }
                                    break;
                                case 4:
                                    if (structAlignment == 4)
                                    {
                                        if (StructIsWrappedPrimitive(type, type.Context.GetWellKnownType(WellKnownType.Single)))
                                        {
                                            llvmStructType = LLVM.FloatType();
                                        }
                                        else
                                        {
                                            llvmStructType = LLVM.Int32Type();
                                        }
                                    }
                                    else
                                    {
                                        goto default;
                                    }
                                    break;
                                case 8:
                                    if (structAlignment == 8)
                                    {
                                        if (StructIsWrappedPrimitive(type, type.Context.GetWellKnownType(WellKnownType.Double)))
                                        {
                                            llvmStructType = LLVM.DoubleType();
                                        }
                                        else
                                        {
                                            llvmStructType = LLVM.Int64Type();
                                        }
                                    }
                                    else
                                    {
                                        goto default;
                                    }
                                    break;

                                default:
                                    // Forward-declare the struct in case there's a reference to it in the fields.
                                    // This must be a named struct or LLVM hits a stack overflow
                                    llvmStructType = LLVM.StructCreateNamed(Context, type.ToString());
                                    LlvmStructs[type] = llvmStructType;

                                    FieldDesc[] instanceFields = type.GetFields().Where(field => !field.IsStatic).ToArray();
                                    FieldAndOffset[] fieldLayout = new FieldAndOffset[instanceFields.Length];
                                    for (int i = 0; i < instanceFields.Length; i++)
                                    {
                                        fieldLayout[i] = new FieldAndOffset(instanceFields[i], instanceFields[i].Offset);
                                    }

                                    // Sort fields by offset and size in order to handle generating unions
                                    FieldAndOffset[] sortedFields = fieldLayout.OrderBy(fieldAndOffset => fieldAndOffset.Offset.AsInt).
                                        ThenByDescending(fieldAndOffset => fieldAndOffset.Field.FieldType.GetElementSize().AsInt).ToArray();

                                    List<LLVMTypeRef> llvmFields = new List<LLVMTypeRef>(sortedFields.Length);
                                    int lastOffset = -1;
                                    int nextNewOffset = -1;
                                    TypeDesc prevType = null;
                                    int totalSize = 0;

                                    foreach (FieldAndOffset fieldAndOffset in sortedFields)
                                    {
                                        int curOffset = fieldAndOffset.Offset.AsInt;

                                        if (prevType == null || (curOffset != lastOffset && curOffset >= nextNewOffset))
                                        {
                                            // The layout should be in order
                                            Debug.Assert(curOffset > lastOffset);

                                            int prevElementSize;
                                            if (prevType == null)
                                            {
                                                lastOffset = 0;
                                                prevElementSize = 0;
                                            }
                                            else
                                            {
                                                prevElementSize = prevType.GetElementSize().AsInt;
                                            }

                                            // Pad to this field if necessary
                                            int paddingSize = curOffset - lastOffset - prevElementSize;
                                            if (paddingSize > 0)
                                            {
                                                AddPaddingFields(paddingSize, llvmFields);
                                                totalSize += paddingSize;
                                            }

                                            TypeDesc fieldType = fieldAndOffset.Field.FieldType;
                                            int fieldSize = fieldType.GetElementSize().AsInt;

                                            llvmFields.Add(GetLLVMTypeForTypeDesc(fieldType));

                                            totalSize += fieldSize;
                                            lastOffset = curOffset;
                                            prevType = fieldType;
                                            nextNewOffset = curOffset + fieldSize;
                                        }
                                    }

                                    // If explicit layout is greater than the sum of fields, add padding
                                    if (totalSize < structSize)
                                    {
                                        AddPaddingFields(structSize - totalSize, llvmFields);
                                    }

                                    LLVM.StructSetBody(llvmStructType, llvmFields.ToArray(), true);
                                    break;
                            }

                            LlvmStructs[type] = llvmStructType;
                        }
                        return llvmStructType;
                    }

                case TypeFlags.Enum:
                    return GetLLVMTypeForTypeDesc(type.UnderlyingType);

                case TypeFlags.Void:
                    return LLVM.VoidType();

                default:
                    throw new NotImplementedException(type.Category.ToString());
            }
        }

        /// <summary>
        /// Returns true if a type is a struct that just wraps a given primitive
        /// or another struct that does so and can thus be treated as that primitive
        /// </summary>
        /// <param name="type">The struct to evaluate</param>
        /// <param name="primitiveType">The primitive to check for</param>
        /// <returns>True if the struct is a wrapper of the primitive</returns>
        private static bool StructIsWrappedPrimitive(TypeDesc type, TypeDesc primitiveType)
        {
            Debug.Assert(type.IsValueType);
            Debug.Assert(primitiveType.IsPrimitive);

            if (type.GetElementSize().AsInt != primitiveType.GetElementSize().AsInt)
            {
                return false;
            }

            FieldDesc[] fields = type.GetFields().ToArray();
            int instanceFieldCount = 0;
            bool foundPrimitive = false;

            foreach (FieldDesc field in fields)
            {
                if (field.IsStatic)
                {
                    continue;
                }

                instanceFieldCount++;

                // If there's more than one field, figuring out whether this is a primitive gets complicated, so assume it's not
                if (instanceFieldCount > 1)
                {
                    break;
                }

                TypeDesc fieldType = field.FieldType;
                if (fieldType == primitiveType)
                {
                    foundPrimitive = true;
                }
                else if (fieldType.IsValueType && !fieldType.IsPrimitive && StructIsWrappedPrimitive(fieldType, primitiveType))
                {
                    foundPrimitive = true;
                }
            }

            if (instanceFieldCount == 1 && foundPrimitive)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Pad out a struct at the current location
        /// </summary>
        /// <param name="paddingSize">Number of bytes of padding to add</param>
        /// <param name="llvmFields">The set of llvm fields in the struct so far</param>
        private static void AddPaddingFields(int paddingSize, List<LLVMTypeRef> llvmFields)
        {
            int numInts = paddingSize / 4;
            int numBytes = paddingSize - numInts * 4;
            for (int i = 0; i < numInts; i++)
            {
                llvmFields.Add(LLVM.Int32Type());
            }
            for (int i = 0; i < numBytes; i++)
            {
                llvmFields.Add(LLVM.Int8Type());
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
                if (!CanStoreVariableOnStack(localType))
                {
                    offset = PadNextOffset(localType, offset);
                }
            }
            return offset.AlignUp(_pointerSize);
        }

        private bool CanStoreVariableOnStack(TypeDesc variableType)
        {
            // Keep all variables on the shadow stack if there is exception
            // handling so funclets can access them
            if (_exceptionRegions.Length == 0)
            {
                return CanStoreTypeOnStack(variableType);
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
                if (!CanStoreVariableOnStack(_signature[i]))
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

            if (!CanStoreVariableOnStack(argType) && CanStoreTypeOnStack(argType))
            {
                // this is an arg that was passed on the stack and is now copied to the shadow stack: move past args that are passed on shadow stack
                for (int i = 0; i < _signature.Length; i++)
                {
                    if (!CanStoreTypeOnStack(_signature[i]))
                    {
                        offset = PadNextOffset(_signature[i], offset);
                    }
                }
            }

            for (int i = 0; i < index; i++)
            {
                // We could compact the set of argSlots to only those that we'd keep on the stack, but currently don't
                potentialRealArgIndex++;

                if (CanStoreTypeOnStack(_signature[index]))
                {
                    if (CanStoreTypeOnStack(_signature[i]) && !CanStoreVariableOnStack(_signature[index]) && !CanStoreVariableOnStack(_signature[i]))
                    {
                        offset = PadNextOffset(_signature[i], offset);
                    }
                }
                // if this is a shadow stack arg, then only count other shadow stack args as stack args come later
                else if (!CanStoreVariableOnStack(_signature[i]) && !CanStoreTypeOnStack(_signature[i]))
                {
                    offset = PadNextOffset(_signature[i], offset);
                }
            }

            if (CanStoreVariableOnStack(argType))
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

            if (CanStoreVariableOnStack(local.Type))
            {
                offset = -1;
            }
            else
            {
                offset = 0;
                for (int i = 0; i < index; i++)
                {
                    if (!CanStoreVariableOnStack(_locals[i].Type))
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
            TypeDesc type = (TypeDesc)_methodIL.GetObject(token);

            //TODO: call GetCastingHelperNameForType from JitHelper.cs (needs refactoring)
            string function;
            bool throwing = opcode == ILOpcode.castclass;
            if (type.IsArray)
                function = throwing ? "CheckCastArray" : "IsInstanceOfArray";
            else if (type.IsInterface)
                function = throwing ? "CheckCastInterface" : "IsInstanceOfInterface";
            else
                function = throwing ? "CheckCastClass" : "IsInstanceOfClass";

            StackEntry[] arguments;
            if (type.IsRuntimeDeterminedSubtype)
            {
                //TODO refactor argument creation with else below
                arguments = new StackEntry[]
                            {
                                new ExpressionEntry(StackValueKind.ValueType, "eeType", CallGenericHelper(ReadyToRunHelperId.TypeHandle, type),
                                    GetEETypePtrTypeDesc()),
                                _stack.Pop()
                            };
            }
            else
            {
                arguments = new StackEntry[]
                                {
                                    new LoadExpressionEntry(StackValueKind.ValueType, "eeType", GetEETypePointerForTypeDesc(type, true),
                                        GetEETypePtrTypeDesc()),
                                    _stack.Pop()
                                };
            }

            _stack.Push(CallRuntime(_compilation.TypeSystemContext, TypeCast, function, arguments, GetWellKnownType(WellKnownType.Object)));
        }

        LLVMValueRef CallGenericHelper(ReadyToRunHelperId helperId, object helperArg)
        {
            _dependencies.Add(GetGenericLookupHelperAndAddReference(helperId, helperArg, out LLVMValueRef helper));
            return LLVM.BuildCall(_builder, helper, new LLVMValueRef[]
            {
                GetShadowStack(),
                GetGenericContext()
            }, "getHelper");
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
                var retParam = LLVM.GetNextParam(LLVM.GetFirstParam(_llvmFunction));
                ImportStoreHelper(castValue, valueType, retParam, 0);
                LLVM.BuildRetVoid(_builder);
            }
            else
            {
                LLVM.BuildRet(_builder, castValue);
            }
        }

        private void ImportCall(ILOpcode opcode, int token)
        {
            MethodDesc runtimeDeterminedMethod = (MethodDesc)_methodIL.GetObject(token);
            MethodDesc callee = (MethodDesc)_canonMethodIL.GetObject(token);
            if (callee.IsIntrinsic)
            {
                if (ImportIntrinsicCall(callee, runtimeDeterminedMethod))
                {
                    return;
                }
            }

            if (callee.IsRawPInvoke() || (callee.IsInternalCall && callee.HasCustomAttribute("System.Runtime", "RuntimeImportAttribute")))
            {
                ImportRawPInvoke(callee);
                return;
            }

            TypeDesc localConstrainedType = _constrainedType;
            _constrainedType = null;

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
                        null,
                        new Int32ConstantEntry(paramCnt),
                        new AddressExpressionEntry(StackValueKind.ValueType, "newobj_array_pdims", dimensions)
                    };
                    if (!runtimeDeterminedMethod.OwningType.IsRuntimeDeterminedSubtype)
                    {
                        arguments[0] = new LoadExpressionEntry(StackValueKind.ValueType, "eeType", GetEETypePointerForTypeDesc(newType, true), eeTypeDesc);
                    }
                    else
                    {
                        var typeRef = CallGenericHelper(ReadyToRunHelperId.TypeHandle, runtimeDeterminedMethod.OwningType);
                        arguments[0] = new ExpressionEntry(StackValueKind.ValueType, "eeType", typeRef, eeTypeDesc);
                    }
                    MetadataType helperType = _compilation.TypeSystemContext.SystemModule.GetKnownType("Internal.Runtime.CompilerHelpers", "ArrayHelpers");
                    MethodDesc helperMethod = helperType.GetKnownMethod("NewObjArray", null);
                    PushNonNull(HandleCall(helperMethod, helperMethod.Signature, helperMethod, arguments, runtimeDeterminedMethod, forcedReturnType: newType));
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
                    if (callee.Signature.Length > _stack.Length)
                        throw new InvalidProgramException();

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
                        StackEntry newObjResult;
                        TypeDesc typeToAlloc;
                        var runtimeDeterminedRetType = runtimeDeterminedMethod.OwningType;

                        var eeTypePtrTypeDesc = GetEETypePtrTypeDesc();
                        if (runtimeDeterminedRetType.IsRuntimeDeterminedSubtype)
                        {
                            typeToAlloc = _compilation.ConvertToCanonFormIfNecessary(runtimeDeterminedRetType, CanonicalFormKind.Specific);
                            var typeRef = CallGenericHelper(ReadyToRunHelperId.TypeHandle, typeToAlloc);
                            newObjResult = AllocateObject(new ExpressionEntry(StackValueKind.ValueType, "eeType", typeRef, eeTypePtrTypeDesc));
                        }
                        else
                        {
                            typeToAlloc = callee.OwningType;
                            MetadataType metadataType = (MetadataType)typeToAlloc;
                            newObjResult = AllocateObject(new LoadExpressionEntry(StackValueKind.ValueType, "eeType", GetEETypePointerForTypeDesc(metadataType, true), eeTypePtrTypeDesc), typeToAlloc);
                        }

                        //one for the real result and one to be consumed by ctor
                        _stack.InsertAt(newObjResult, _stack.Top - callee.Signature.Length);
                        _stack.InsertAt(newObjResult, _stack.Top - callee.Signature.Length);
                    }
                }
            }

            if (opcode == ILOpcode.newobj && callee.OwningType.IsDelegate)
            {
                FunctionPointerEntry functionPointer = ((FunctionPointerEntry)_stack.Peek());
                TypeDesc canonDelegateType = callee.OwningType.ConvertToCanonForm(CanonicalFormKind.Specific);
                DelegateCreationInfo delegateInfo = _compilation.GetDelegateCtor(canonDelegateType, functionPointer.Method, followVirtualDispatch: false);
                MethodDesc delegateTargetMethod = delegateInfo.TargetMethod;
                callee = delegateInfo.Constructor.Method;
                if (delegateInfo.NeedsRuntimeLookup && !functionPointer.IsVirtual)
                {
                    LLVMValueRef helper;
                    List<LLVMTypeRef> additionalTypes = new List<LLVMTypeRef>();
                    var shadowStack = GetShadowStack();
                    if (delegateInfo.Thunk != null)
                    {
                        MethodDesc thunkMethod = delegateInfo.Thunk.Method;
                        AddMethodReference(thunkMethod);
                        PushExpression(StackValueKind.NativeInt, "invokeThunk",
                            GetOrCreateLLVMFunction(
                                _compilation.NameMangler.GetMangledMethodName(thunkMethod).ToString(),
                                thunkMethod.Signature,
                                false));
                    }
                    var sigLength = callee.Signature.Length;
                    var stackCopy = new StackEntry[sigLength];
                    for (var i = 0; i < sigLength; i++)
                    {
                        stackCopy[i] = _stack.Pop();
                    }
                    var thisEntry = _stack.Pop(); // the extra newObjResult which we dont want as we are not going through HandleCall
                    // by convention(?) the delegate initialize methods take this as the first parameter which is not in the ctor
                    // method sig, so add that here
                    int curOffset = 0;

                    // pass this (delegate obj) as first param
                    LLVMTypeRef llvmTypeRefForThis = GetLLVMTypeForTypeDesc(thisEntry.Type);
                    curOffset = PadOffset(thisEntry.Type, curOffset);
                    LLVMValueRef thisAddr = LLVM.BuildGEP(_builder, shadowStack, new LLVMValueRef[] { LLVM.ConstInt(LLVM.Int32Type(), (ulong)curOffset, LLVMMisc.False) }, "thisLoc");
                    LLVMValueRef llvmValueRefForThis = thisEntry.ValueAsType(LLVMTypeRef.PointerType(LLVMTypeRef.Int8Type(), 0), _builder);
                    LLVM.BuildStore(_builder, llvmValueRefForThis, CastIfNecessary(_builder, thisAddr, LLVM.PointerType(llvmTypeRefForThis, 0), "thisCast"));
                    curOffset = PadNextOffset(GetWellKnownType(WellKnownType.Object), curOffset);

                    List<LLVMValueRef> helperParams = new List<LLVMValueRef>
                    {
                        shadowStack,
                        GetGenericContext()
                    };

                    for (var i = 0; i < sigLength; i++)
                    {
                        TypeDesc argTypeDesc = callee.Signature[i];
                        LLVMTypeRef llvmTypeRefForArg = GetLLVMTypeForTypeDesc(argTypeDesc);
                        StackEntry argStackEntry = stackCopy[sigLength - i - 1];
                        if (CanStoreTypeOnStack(callee.Signature[i]))
                        {
                            LLVMValueRef llvmValueRefForArg = argStackEntry.ValueAsType(llvmTypeRefForArg, _builder);
                            additionalTypes.Add(llvmTypeRefForArg);
                            helperParams.Add(llvmValueRefForArg);
                        }
                        else
                        {
                            LLVMValueRef llvmValueRefForArg = argStackEntry.ValueAsType(LLVMTypeRef.PointerType(LLVMTypeRef.Int8Type(), 0), _builder);
                            curOffset = PadOffset(argTypeDesc, curOffset);
                            LLVMValueRef argAddr = LLVM.BuildGEP(_builder, shadowStack, new LLVMValueRef[] { LLVM.ConstInt(LLVM.Int32Type(), (ulong)curOffset, LLVMMisc.False) }, "arg" + i);
                            LLVM.BuildStore(_builder, llvmValueRefForArg, CastIfNecessary(_builder, argAddr, LLVM.PointerType(llvmTypeRefForArg, 0), $"parameter{i}_"));
                            curOffset = PadNextOffset(argTypeDesc, curOffset);
                        }
                    }

                    GetGenericLookupHelperAndAddReference(ReadyToRunHelperId.DelegateCtor, delegateInfo, out helper,
                        additionalTypes);
                    LLVM.BuildCall(_builder, helper, helperParams.ToArray(), string.Empty);
                    return;
                }
                if (!functionPointer.IsVirtual && delegateTargetMethod.OwningType.IsValueType &&
                         !delegateTargetMethod.Signature.IsStatic)
                {
                    _stack.Pop(); // remove the target

                    MethodDesc canonDelegateTargetMethod = delegateTargetMethod.GetCanonMethodTarget(CanonicalFormKind.Specific);
                    ISymbolNode targetNode = delegateInfo.GetTargetNode(_compilation.NodeFactory);
                    _dependencies.Add(targetNode);
                    if (delegateTargetMethod != canonDelegateTargetMethod)
                    {
                        var funcRef = LoadAddressOfSymbolNode(targetNode);
                        var toInt = LLVM.BuildPtrToInt(_builder, funcRef, LLVMTypeRef.Int32Type(), "toInt");
                        var withOffset = LLVM.BuildOr(_builder, toInt, BuildConstUInt32((uint)_compilation.TypeSystemContext.Target.FatFunctionPointerOffset), "withOffset");
                        PushExpression(StackValueKind.NativeInt, "fatthunk", withOffset);
                    }
                    else
                    {
                        PushExpression(StackValueKind.NativeInt, "thunk", GetOrCreateLLVMFunction(targetNode.GetMangledName(_compilation.NodeFactory.NameMangler), delegateTargetMethod.Signature, false));
                    }
                }
                else if (callee.Signature.Length == 3)
                {
                    // These are the invoke thunks e.g. {[S.P.CoreLib]System.Func`1<System.__Canon>.InvokeOpenStaticThunk()} that are passed to e.g. {[S.P.CoreLib]System.Delegate.InitializeOpenStaticThunk(object,native int,native int)}
                    // only push this if there is the third argument, i.e. not {[S.P.CoreLib]System.Delegate.InitializeClosedInstance(object,native int)}
                    PushExpression(StackValueKind.NativeInt, "thunk", GetOrCreateLLVMFunction(_compilation.NodeFactory.NameMangler.GetMangledMethodName(delegateInfo.Thunk.Method).ToString(), delegateInfo.Thunk.Method.Signature, false));
                }
            }

            HandleCall(callee, callee.Signature, runtimeDeterminedMethod, opcode, localConstrainedType);
        }

        private LLVMValueRef LLVMFunctionForMethod(MethodDesc callee, MethodDesc canonMethod, StackEntry thisPointer, bool isCallVirt,
            TypeDesc constrainedType, MethodDesc runtimeDeterminedMethod, out bool hasHiddenParam, 
            out LLVMValueRef dictPtrPtrStore,
            out LLVMValueRef fatFunctionPtr)
        {
            hasHiddenParam = false;
            dictPtrPtrStore = default(LLVMValueRef);
            fatFunctionPtr = default(LLVMValueRef);

            string canonMethodName = _compilation.NameMangler.GetMangledMethodName(canonMethod).ToString();
            TypeDesc owningType = callee.OwningType;
            bool delegateInvoke = owningType.IsDelegate && callee.Name == "Invoke";
            // Sealed methods must not be called virtually due to sealed vTables, so call them directly, but not delegate Invoke
            if ((canonMethod.IsFinal || canonMethod.OwningType.IsSealed()) && !delegateInvoke)
            {
                if (!_compilation.NodeFactory.TypeSystemContext.IsSpecialUnboxingThunkTargetMethod(canonMethod))
                {
                    hasHiddenParam = canonMethod.RequiresInstArg();
                }
                AddMethodReference(canonMethod);
                return GetOrCreateLLVMFunction(canonMethodName, canonMethod.Signature, hasHiddenParam);
            }

            if (canonMethod.IsVirtual && isCallVirt)
            {
                // TODO: Full resolution of virtual methods
                if (!canonMethod.IsNewSlot)
                    throw new NotImplementedException();

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

                if (constrainedType != null && constrainedType.IsValueType)
                {
                    isValueTypeCall = true;
                }

                if (isValueTypeCall)
                {
                    if (constrainedType != null)
                    {
                        if (constrainedType.IsRuntimeDeterminedType)
                        {
                            constrainedType = constrainedType.ConvertToCanonForm(CanonicalFormKind.Specific);
                        }
                        targetMethod = constrainedType.TryResolveConstraintMethodApprox(canonMethod.OwningType, canonMethod, out _);
                    }
                    else if (canonMethod.OwningType.IsInterface)
                    {
                        targetMethod = parameterType.ResolveInterfaceMethodTarget(canonMethod);
                    }
                    else
                    {
                        targetMethod = parameterType.FindVirtualFunctionTargetMethodOnObjectType(canonMethod);
                    }
                }

                hasHiddenParam = callee.RequiresInstArg();
                if (targetMethod != null)
                {
                    AddMethodReference(targetMethod);
                    return GetOrCreateLLVMFunction(_compilation.NameMangler.GetMangledMethodName(targetMethod).ToString(), canonMethod.Signature, hasHiddenParam);
                }
                if (canonMethod.HasInstantiation && !canonMethod.IsFinal && !canonMethod.OwningType.IsSealed())
                {
                    return GetCallableGenericVirtualMethod(thisPointer, canonMethod, callee, runtimeDeterminedMethod, out dictPtrPtrStore, out fatFunctionPtr);
                }
                return GetCallableVirtualMethod(thisPointer, callee, runtimeDeterminedMethod);
            }

            hasHiddenParam = canonMethod.RequiresInstArg();
            AddMethodReference(canonMethod);
            return GetOrCreateLLVMFunction(canonMethodName, canonMethod.Signature, hasHiddenParam);
        }

        private ISymbolNode GetMethodGenericDictionaryNode(MethodDesc method)
        {
            ISymbolNode node = _compilation.NodeFactory.MethodGenericDictionary(method);
            _dependencies.Add(node);

            return node;
        }

        private LLVMValueRef GetOrCreateMethodSlot(MethodDesc canonMethod, MethodDesc callee)
        {
            var vtableSlotSymbol = _compilation.NodeFactory.VTableSlot(callee);
            _dependencies.Add(vtableSlotSymbol);
            LLVMValueRef slot = LoadAddressOfSymbolNode(vtableSlotSymbol);
            return LLVM.BuildLoad(_builder, slot, $"{callee.Name}_slot");
        }

        private LLVMValueRef GetCallableVirtualMethod(StackEntry objectPtr, MethodDesc callee, MethodDesc runtimeDeterminedMethod)
        {
            Debug.Assert(runtimeDeterminedMethod.IsVirtual);

            LLVMValueRef slot = GetOrCreateMethodSlot(runtimeDeterminedMethod, callee);

            LLVMTypeRef llvmSignature = GetLLVMSignatureForMethod(runtimeDeterminedMethod.Signature, false);
            LLVMValueRef functionPtr;
            var thisPointer = objectPtr.ValueAsType(LLVM.PointerType(LLVM.Int8Type(), 0), _builder);
            ThrowIfNull(thisPointer);
            if (runtimeDeterminedMethod.OwningType.IsInterface)
            {
                ExpressionEntry interfaceEEType;
                ExpressionEntry eeTypeExpression;
                if (runtimeDeterminedMethod.OwningType.IsRuntimeDeterminedSubtype)
                {
                    var eeTypeDesc = GetEETypePtrTypeDesc();
                    //TODO interfaceEEType can be refactored out
                    eeTypeExpression = CallRuntime("System", _compilation.TypeSystemContext, "Object", "get_EEType",
                        new[] { new ExpressionEntry(StackValueKind.ObjRef, "thisPointer", thisPointer) });
                    interfaceEEType = new ExpressionEntry(StackValueKind.ValueType, "interfaceEEType", CallGenericHelper(ReadyToRunHelperId.TypeHandle, runtimeDeterminedMethod.OwningType), eeTypeDesc);
                }
                else
                {
                    var eeTypeDesc = GetEETypePtrTypeDesc();
                    interfaceEEType = new LoadExpressionEntry(StackValueKind.ValueType, "interfaceEEType", GetEETypePointerForTypeDesc(runtimeDeterminedMethod.OwningType, true), eeTypeDesc);
                    eeTypeExpression = new LoadExpressionEntry(StackValueKind.ValueType, "eeType", thisPointer, eeTypeDesc);
                }

                var targetEntry = CallRuntime(_compilation.TypeSystemContext, DispatchResolve, "FindInterfaceMethodImplementationTarget", new StackEntry[] { eeTypeExpression, interfaceEEType, new ExpressionEntry(StackValueKind.Int32, "slot", slot, GetWellKnownType(WellKnownType.UInt16)) });
                functionPtr = targetEntry.ValueAsType(LLVM.PointerType(llvmSignature, 0), _builder);
            }
            else
            {
                var rawObjectPtr = CastIfNecessary(thisPointer, LLVM.PointerType(LLVM.PointerType(LLVM.PointerType(llvmSignature, 0), 0), 0), objectPtr.Name());
                var eeType = LLVM.BuildLoad(_builder, rawObjectPtr, "ldEEType");
                var slotPtr = LLVM.BuildGEP(_builder, eeType, new LLVMValueRef[] { slot }, "__getslot__");
                functionPtr = LLVM.BuildLoad(_builder, slotPtr, "ld__getslot__");
            }

            return functionPtr;
        }

        private LLVMValueRef GetCallableGenericVirtualMethod(StackEntry objectPtr, MethodDesc canonMethod, MethodDesc callee, MethodDesc runtimeDeterminedMethod, out LLVMValueRef dictPtrPtrStore,
            out LLVMValueRef slotRef)
        {
            // this will only have a non-zero pointer the the GVM ptr is fat.
            dictPtrPtrStore = LLVM.BuildAlloca(_builder,
                LLVM.PointerType(LLVM.PointerType(LLVM.PointerType(LLVMTypeRef.Int8Type(), 0), 0), 0),
                "dictPtrPtrStore");

            _dependencies.Add(_compilation.NodeFactory.GVMDependencies(canonMethod));
            bool exactContextNeedsRuntimeLookup;
            if (canonMethod.HasInstantiation)
            {
                exactContextNeedsRuntimeLookup = callee.IsSharedByGenericInstantiations;
            }
            else
            {
                exactContextNeedsRuntimeLookup = canonMethod.OwningType.IsCanonicalSubtype(CanonicalFormKind.Any);
            }
            LLVMValueRef runtimeMethodHandle;
            if (exactContextNeedsRuntimeLookup)
            {
                LLVMValueRef helper;
                var node = GetGenericLookupHelperAndAddReference(ReadyToRunHelperId.MethodHandle, runtimeDeterminedMethod, out helper);
                _dependencies.Add(node);
                runtimeMethodHandle = LLVM.BuildCall(_builder, helper, new LLVMValueRef[]
                {
                    GetShadowStack(),
                    GetGenericContext()
                }, "getHelper");
            }
            else
            {
                var runtimeMethodHandleNode = _compilation.NodeFactory.RuntimeMethodHandle(runtimeDeterminedMethod);
                _dependencies.Add(runtimeMethodHandleNode);
                runtimeMethodHandle = LoadAddressOfSymbolNode(runtimeMethodHandleNode);
            }

            var lookupSlotArgs = new StackEntry[]
            {
                objectPtr, 
                new ExpressionEntry(StackValueKind.ObjRef, "rmh", runtimeMethodHandle, GetWellKnownType(WellKnownType.Object))
            };
            var gvmPtr = CallRuntime(_compilation.TypeSystemContext, "TypeLoaderExports", "GVMLookupForSlot", lookupSlotArgs);
            slotRef = gvmPtr.ValueAsType(LLVM.PointerType(LLVMTypeRef.Int8Type(), 0), _builder);

            var fatBranch = LLVM.AppendBasicBlock(_currentFunclet, "then");
            var notFatBranch = LLVM.AppendBasicBlock(_currentFunclet, "else");
            var endifBlock = LLVM.AppendBasicBlock(_currentFunclet, "endif");
            // if
            var andResRef = LLVM.BuildAnd(_builder, CastIfNecessary(_builder, slotRef, LLVMTypeRef.Int32Type()), LLVM.ConstInt(LLVM.Int32Type(), (ulong)_compilation.TypeSystemContext.Target.FatFunctionPointerOffset, LLVMMisc.False), "andPtrOffset");
            var eqz = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntEQ, andResRef, BuildConstInt32(0), "eqz");
            LLVM.BuildCondBr(_builder, eqz, notFatBranch, fatBranch);

            // fat
            LLVM.PositionBuilderAtEnd(_builder, fatBranch);
            var gep = RemoveFatOffset(_builder, slotRef);
            var loadFuncPtr = LLVM.BuildLoad(_builder,
                CastIfNecessary(_builder, gep, LLVM.PointerType(LLVM.PointerType(LLVMTypeRef.Int8Type(), 0), 0)),
                "loadFuncPtr");
            var dictPtrPtr = LLVM.BuildGEP(_builder,
                CastIfNecessary(_builder, gep,
                    LLVM.PointerType(LLVM.PointerType(LLVM.PointerType(LLVMTypeRef.Int8Type(), 0), 0), 0), "castDictPtrPtr"),
                new [] {BuildConstInt32(1)}, "dictPtrPtr");
            LLVM.BuildStore(_builder, dictPtrPtr, dictPtrPtrStore);
            LLVM.BuildBr(_builder, endifBlock);

            // not fat
            LLVM.PositionBuilderAtEnd(_builder, notFatBranch);
            // store null to indicate the GVM call needs no hidden param at run time
            LLVM.BuildStore(_builder, LLVM.ConstPointerNull(LLVM.PointerType(LLVM.PointerType(LLVM.PointerType(LLVMTypeRef.Int8Type(), 0), 0), 0)), dictPtrPtrStore);
            LLVM.BuildBr(_builder, endifBlock);

            // end if
            LLVM.PositionBuilderAtEnd(_builder, endifBlock);
            var loadPtr = LLVM.BuildPhi(_builder, LLVMTypeRef.PointerType(LLVMTypeRef.Int8Type(), 0), "fatNotFatPhi");
            LLVM.AddIncoming(loadPtr, new LLVMValueRef[] { loadFuncPtr, slotRef },
                new LLVMBasicBlockRef[] { fatBranch, notFatBranch }, 2);

            // dont know the type for sure, but will generate for no hidden dict param and change if necessary before calling.
            var asFunc = CastIfNecessary(_builder, loadPtr, LLVM.PointerType(GetLLVMSignatureForMethod(runtimeDeterminedMethod.Signature, false), 0) , "castToFunc");
            return asFunc;
        }

        private LLVMTypeRef GetLLVMSignatureForMethod(MethodSignature signature, bool hasHiddenParam)
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

            if (hasHiddenParam)
            {
                signatureTypes.Add(LLVM.PointerType(LLVM.Int8Type(), 0)); // *EEType
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

        private ExpressionEntry AllocateObject(StackEntry eeType, TypeDesc forcedReturnType = null)
        {
            //TODO: call GetNewObjectHelperForType from JitHelper.cs (needs refactoring)
            return CallRuntime(_compilation.TypeSystemContext, RuntimeExport, "RhNewObject", new StackEntry[] { eeType }, forcedReturnType);
        }

        MetadataType GetEETypePtrTypeDesc()
        {
            return _compilation.TypeSystemContext.SystemModule.GetKnownType("System", "EETypePtr");
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

        private static LLVMValueRef BuildConstUInt32(uint number)
        {
            return LLVM.ConstInt(LLVM.Int32Type(), number, LLVMMisc.False);
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
        private bool ImportIntrinsicCall(MethodDesc method, MethodDesc runtimeDeterminedMethod)
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
                        var fieldData = fieldNode.GetData(_compilation.NodeFactory, false).Data;
                        int srcLength = fieldData.Length;

                        if (arraySlot.Type.IsArray)
                        {
                            // Handle single dimensional arrays (vectors) and multidimensional.
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

                            LLVMValueRef offset;
                            if (arraySlot.Type.IsSzArray)
                            {
                                offset = ArrayBaseSizeRef();
                            }
                            else
                            {
                                ArrayType arrayType = (ArrayType)arraySlot.Type;
                                offset = BuildConstInt32(ArrayBaseSize() +
                                                         2 * sizeof(int) * arrayType.Rank);
                            }
                            var args = new LLVMValueRef[]
                            {
                                LLVM.BuildGEP(_builder, arrayObjPtr, new LLVMValueRef[] { offset }, string.Empty),
                                LLVM.BuildBitCast(_builder, src, LLVM.PointerType(LLVM.Int8Type(), 0), string.Empty),
                                BuildConstInt32(srcLength), // TODO: Handle destination array length to avoid runtime overflow.
                                BuildConstInt32(0), // Assume no alignment
                                BuildConstInt1(0)
                            };
                            LLVM.BuildCall(_builder, memcpyFunction, args, string.Empty);
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
                case "GetValueInternal":
                    if (metadataType.Namespace == "System" && metadataType.Name == "RuntimeTypeHandle")
                    {
                        var typeHandleSlot = (LdTokenEntry<TypeDesc>)_stack.Pop();
                        TypeDesc typeOfEEType = typeHandleSlot.LdToken;

                        if (typeOfEEType.IsRuntimeDeterminedSubtype)
                        {
                            var typeHandlerRef = CallGenericHelper(ReadyToRunHelperId.TypeHandle, typeOfEEType);
                            PushExpression(StackValueKind.Int32, "eeType", typeHandlerRef, GetWellKnownType(WellKnownType.IntPtr));
                        }
                        else
                        {
                            PushLoadExpression(StackValueKind.Int32, "eeType", GetEETypePointerForTypeDesc(typeOfEEType, true), GetWellKnownType(WellKnownType.IntPtr));
                        }
                        return true;
                    }
                    break;
            }

            return false;
        }

        private void HandleCall(MethodDesc callee, MethodSignature signature, MethodDesc runtimeDeterminedMethod, ILOpcode opcode = ILOpcode.call, TypeDesc constrainedType = null, LLVMValueRef calliTarget = default(LLVMValueRef), LLVMValueRef hiddenRef = default(LLVMValueRef))
        {
            bool resolvedConstraint = false; 

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
                else if (opcode == ILOpcode.callvirt)
                {
                    var canonConstrainedType = constrainedType;
                    if (constrainedType.IsRuntimeDeterminedSubtype)
                        canonConstrainedType = constrainedType.ConvertToCanonForm(CanonicalFormKind.Specific);
                    
                    bool forceUseRuntimeLookup;
                    var constrainedClosestDefType = canonConstrainedType.GetClosestDefType();
                    MethodDesc directMethod = constrainedClosestDefType.TryResolveConstraintMethodApprox(callee.OwningType, callee, out forceUseRuntimeLookup);

                    if (directMethod == null)
                    {
                        StackEntry eeTypeEntry;
                        var eeTypeDesc = GetEETypePtrTypeDesc();
                        if (constrainedType.IsRuntimeDeterminedSubtype)
                        {
                            eeTypeEntry = new ExpressionEntry(StackValueKind.ValueType, "eeType", CallGenericHelper(ReadyToRunHelperId.TypeHandle, constrainedType), eeTypeDesc.MakePointerType());
                        }
                        else
                        {
                            eeTypeEntry = new LoadExpressionEntry(StackValueKind.ValueType, "eeType", GetEETypePointerForTypeDesc(constrainedType, true), eeTypeDesc);
                        }

                        argumentValues[0] = CallRuntime(_compilation.TypeSystemContext, RuntimeExport, "RhBox",
                            new StackEntry[]
                            {
                                eeTypeEntry,
                                argumentValues[0],
                            });
                    }
                    else
                    {
                        callee = directMethod;
                        opcode = ILOpcode.call;
                        resolvedConstraint = true;
                    }
                }
            }
            MethodDesc canonMethod = callee?.GetCanonMethodTarget(CanonicalFormKind.Specific);
            PushNonNull(HandleCall(callee, signature, canonMethod, argumentValues, runtimeDeterminedMethod, opcode, constrainedType, calliTarget, hiddenRef, resolvedConstraint));
        }

        private ExpressionEntry HandleCall(MethodDesc callee, MethodSignature signature, MethodDesc canonMethod, StackEntry[] argumentValues, MethodDesc runtimeDeterminedMethod, ILOpcode opcode = ILOpcode.call, TypeDesc constrainedType = null, LLVMValueRef calliTarget = default(LLVMValueRef), LLVMValueRef hiddenParamRef = default(LLVMValueRef), bool resolvedConstraint = false, TypeDesc forcedReturnType = null)
        {
            LLVMValueRef fn;
            bool hasHiddenParam = false;
            LLVMValueRef hiddenParam = default;
            LLVMValueRef dictPtrPtrStore = default;
            LLVMValueRef fatFunctionPtr = default;
            if (opcode == ILOpcode.calli)
            {
                fn = calliTarget;
                hiddenParam = hiddenParamRef;
            }
            else
            {
                fn = LLVMFunctionForMethod(callee, canonMethod, signature.IsStatic ? null : argumentValues[0], opcode == ILOpcode.callvirt, constrainedType, runtimeDeterminedMethod, out hasHiddenParam, out dictPtrPtrStore, out fatFunctionPtr);
            }

            int offset = GetTotalParameterOffset() + GetTotalLocalOffset();
            LLVMValueRef shadowStack = LLVM.BuildGEP(_builder, LLVM.GetFirstParam(_currentFunclet),
                new LLVMValueRef[] { LLVM.ConstInt(LLVM.Int32Type(), (uint)offset, LLVMMisc.False) },
                String.Empty);
            var castShadowStack = LLVM.BuildPointerCast(_builder, shadowStack, LLVM.PointerType(LLVM.Int8Type(), 0), "castshadowstack");
            List<LLVMValueRef> llvmArgs = new List<LLVMValueRef>
            {
                castShadowStack
            };

            TypeDesc returnType = signature.ReturnType;

            bool needsReturnSlot = NeedsReturnStackSlot(signature);
            SpilledExpressionEntry returnSlot = null;
            var actualReturnType = forcedReturnType ?? returnType;
            if (needsReturnSlot)
            {
                int returnIndex = _spilledExpressions.Count;
                returnSlot = new SpilledExpressionEntry(GetStackValueKind(actualReturnType), callee?.Name + "_return", actualReturnType, returnIndex, this);
                _spilledExpressions.Add(returnSlot);
                LLVMValueRef returnAddress = LoadVarAddress(returnIndex, LocalVarKind.Temp, out TypeDesc unused);
                LLVMValueRef castReturnAddress = LLVM.BuildPointerCast(_builder, returnAddress, LLVM.PointerType(LLVM.Int8Type(), 0), callee?.Name + "_castreturn");
                llvmArgs.Add(castReturnAddress);
            }

            // for GVM, the hidden param is added conditionally at runtime.
            if (opcode != ILOpcode.calli && fatFunctionPtr.Pointer == IntPtr.Zero)
            {
                bool exactContextNeedsRuntimeLookup;
                if (callee.HasInstantiation)
                {
                    exactContextNeedsRuntimeLookup = callee.IsSharedByGenericInstantiations && !_isUnboxingThunk;
                }
                else
                {
                    exactContextNeedsRuntimeLookup = callee.OwningType.IsCanonicalSubtype(CanonicalFormKind.Any);
                }

                if (hasHiddenParam)
                {
                    if (exactContextNeedsRuntimeLookup)
                    {
                        if (!resolvedConstraint)
                        {
                            if (callee.RequiresInstMethodDescArg())
                            {
                                hiddenParam = CallGenericHelper(ReadyToRunHelperId.MethodDictionary, runtimeDeterminedMethod);
                            }
                            else
                            {
                                hiddenParam = CallGenericHelper(ReadyToRunHelperId.TypeHandle, runtimeDeterminedMethod.OwningType);
                            }
                        }
                        else
                        {
                            Debug.Assert(canonMethod.RequiresInstMethodTableArg() && constrainedType != null);
                            if (constrainedType.IsRuntimeDeterminedSubtype)
                            {
                                hiddenParam = CallGenericHelper(ReadyToRunHelperId.TypeHandle, constrainedType);
                            }
                            else
                            {
                                var constrainedTypeSymbol = _compilation.NodeFactory.ConstructedTypeSymbol(constrainedType);
                                _dependencies.Add(constrainedTypeSymbol);
                                hiddenParam = LoadAddressOfSymbolNode(constrainedTypeSymbol);
                            }
                        }
                    }
                    else
                    {
                        if (_isUnboxingThunk && _method.RequiresInstArg())
                        {
                            hiddenParam = LLVM.GetParam(_currentFunclet, (uint)(1 + (NeedsReturnStackSlot(_signature) ? 1 : 0)));
                        }
                        else if (canonMethod.RequiresInstMethodDescArg())
                        {
                            hiddenParam = LoadAddressOfSymbolNode(GetMethodGenericDictionaryNode(callee));
                        }
                        else
                        {
                            var owningTypeSymbol = _compilation.NodeFactory.ConstructedTypeSymbol(callee.OwningType);
                            _dependencies.Add(owningTypeSymbol);
                            hiddenParam = LoadAddressOfSymbolNode(owningTypeSymbol);
                        }
                    }
                }
            }

            if (hiddenParam.Pointer != IntPtr.Zero) 
            {
                llvmArgs.Add(CastIfNecessary(hiddenParam, LLVMTypeRef.PointerType(LLVMTypeRef.Int8Type(), 0)));
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
                    if (canonMethod != null && CanStoreTypeOnStack(argType))
                    {
                        argType = canonMethod.Signature[index - instanceAdjustment];
                    }
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
            LLVMValueRef llvmReturn = default;
            if (fatFunctionPtr.Pointer != IntPtr.Zero) // indicates GVM
            {
                // conditional call depending on if the function was fat/the dict hidden param is needed
                // TODO: not sure this is always conditional, maybe there is some optimisation that can be done to not inject this conditional logic depending on the caller/callee
                LLVMValueRef dict = LLVM.BuildLoad(_builder, dictPtrPtrStore, "dictPtrPtr");
                LLVMValueRef dictAsInt = LLVM.BuildPtrToInt(_builder, dict, LLVMTypeRef.Int32Type(), "toInt");
                LLVMValueRef eqZ = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntEQ, dictAsInt, BuildConstInt32(0), "eqz");
                var notFatBranch = LLVM.AppendBasicBlock(_currentFunclet, "notFat");
                var fatBranch = LLVM.AppendBasicBlock(_currentFunclet, "fat");
                var endifBlock = LLVM.AppendBasicBlock(_currentFunclet, "endif");
                LLVM.BuildCondBr(_builder, eqZ, notFatBranch, fatBranch); 
                // then
                LLVM.PositionBuilderAtEnd(_builder, notFatBranch);
                var notFatReturn = LLVM.BuildCall(_builder, fn, llvmArgs.ToArray(), string.Empty);
                LLVM.BuildBr(_builder, endifBlock);
                
                // else
                LLVM.PositionBuilderAtEnd(_builder, fatBranch);
                var fnWithDict = LLVM.BuildCast(_builder, LLVMOpcode.LLVMBitCast, fn, LLVM.PointerType(GetLLVMSignatureForMethod(runtimeDeterminedMethod.Signature, true), 0), "fnWithDict");
                var dictDereffed = LLVM.BuildLoad(_builder, LLVM.BuildLoad(_builder, dict, "l1"), "l2");
                llvmArgs.Insert(needsReturnSlot ? 2 : 1, dictDereffed);
                var fatReturn = LLVM.BuildCall(_builder, fnWithDict, llvmArgs.ToArray(), string.Empty);
                LLVM.BuildBr(_builder, endifBlock);
                
                // endif
                LLVM.PositionBuilderAtEnd(_builder, endifBlock);
                if (!returnType.IsVoid && !needsReturnSlot)
                {
                    llvmReturn = LLVM.BuildPhi(_builder, GetLLVMTypeForTypeDesc(returnType), "callReturnPhi");
                    LLVM.AddIncoming(llvmReturn, new LLVMValueRef[] { notFatReturn, fatReturn },
                        new LLVMBasicBlockRef[] { notFatBranch, fatBranch }, 2);
                }
                _currentBasicBlock.LastInternalIf = endifBlock;
            }
            else llvmReturn = LLVM.BuildCall(_builder, fn, llvmArgs.ToArray(), string.Empty);

            if (!returnType.IsVoid)
            {
                return needsReturnSlot ? returnSlot : 
                    (
                        canonMethod != null && canonMethod.Signature.ReturnType != actualReturnType
                        ? CreateGenericReturnExpression(GetStackValueKind(actualReturnType), callee?.Name + "_return", llvmReturn, actualReturnType)
                        : new ExpressionEntry(GetStackValueKind(actualReturnType), callee?.Name + "_return", llvmReturn, actualReturnType));
            }
            else
            {
                return null;
            }
        }

        // generic structs need to be cast to the actualReturnType
        private ExpressionEntry CreateGenericReturnExpression(StackValueKind stackValueKind, string calleeName, LLVMValueRef llvmReturn, TypeDesc actualReturnType)
        {
            Debug.Assert(llvmReturn.TypeOf().IsPackedStruct);
            var destStruct = GetLLVMTypeForTypeDesc(actualReturnType).GetUndef();
            for (uint elemNo = 0; elemNo < llvmReturn.TypeOf().CountStructElementTypes(); elemNo++)
            {
                var elemValRef = LLVM.BuildExtractValue(_builder, llvmReturn, 0, "ex" + elemNo);
                destStruct = LLVM.BuildInsertValue(_builder, destStruct, elemValRef, elemNo, "st" + elemNo);
            }
            return new ExpressionEntry(stackValueKind, calleeName, destStruct, actualReturnType);
        }

        // simple calling cases, not virtual, not calli
        private LLVMValueRef HandleDirectCall(MethodDesc callee, MethodSignature signature,
            StackEntry[] argumentValues,
            TypeDesc constrainedType, LLVMValueRef calliTarget, int offset, LLVMValueRef baseShadowStack,
            LLVMBuilderRef builder, bool needsReturnSlot,
            LLVMValueRef castReturnAddress, MethodDesc runtimeDeterminedMethod)
        {
            LLVMValueRef fn = LLVMFunctionForMethod(callee, callee, signature.IsStatic ? null : argumentValues[0], false, constrainedType, runtimeDeterminedMethod, out bool hasHiddenParam, out LLVMValueRef dictPtrPtrStore, out LLVMValueRef fatFunctionPtr);

            LLVMValueRef shadowStack = LLVM.BuildGEP(builder, baseShadowStack, new LLVMValueRef[] { LLVM.ConstInt(LLVM.Int32Type(), (uint)offset, LLVMMisc.False) }, String.Empty);
            var castShadowStack = LLVM.BuildPointerCast(builder, shadowStack, LLVM.PointerType(LLVM.Int8Type(), 0), "castshadowstack");

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
                    if (callee.OwningType.IsValueType)
                        argType = callee.OwningType.MakeByRefType();
                    else
                        argType = callee.OwningType;
                }
                else
                {
                    argType = signature[index - instanceAdjustment];
                }

                LLVMTypeRef valueType = GetLLVMTypeForTypeDesc(argType);
                LLVMValueRef argValue = toStore.ValueAsType(valueType, builder);

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

                    ImportStoreHelper(argValue, valueType, castShadowStack, (uint)argOffset, builder: builder);

                    argOffset += argType.GetElementSize().AsInt;
                }
            }

            LLVMValueRef llvmReturn = LLVM.BuildCall(builder, fn, llvmArgs.ToArray(), string.Empty);
            return llvmReturn;
        }

        private void AddMethodReference(MethodDesc method)
        {
            _dependencies.Add(_compilation.NodeFactory.MethodEntrypoint(method));
        }

        static Dictionary<string, MethodDesc> _pinvokeMap = new Dictionary<string, MethodDesc>();
        private void ImportRawPInvoke(MethodDesc method)
        {
            var arguments = new StackEntry[method.Signature.Length];
            for (int i = 0; i < arguments.Length; i++)
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
                if (!String.IsNullOrEmpty(entrypointName))
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
            LLVM.BuildStore(_builder, LLVM.BuildGEP(_builder, LLVM.GetFirstParam(_currentFunclet), new LLVMValueRef[] { stackFrameSize }, "shadowStackTop"),
                LLVM.GetNamedGlobal(Module, "t_pShadowStackTop"));

            LLVMValueRef pInvokeTransitionFrame = default;
            LLVMTypeRef pInvokeFunctionType = default;
            if (method.IsPInvoke)
            {
                // add call to go to preemptive mode
                LLVMTypeRef pInvokeTransitionFrameType =
                    LLVM.StructType(new LLVMTypeRef[] { LLVM.PointerType(LLVM.Int8Type(), 0), LLVM.PointerType(LLVM.Int8Type(), 0), LLVM.PointerType(LLVM.Int8Type(), 0) }, false);
                pInvokeFunctionType = LLVM.FunctionType(LLVM.VoidType(), new LLVMTypeRef[] { LLVM.PointerType(pInvokeTransitionFrameType, 0) }, false);
                pInvokeTransitionFrame = LLVM.BuildAlloca(_builder, pInvokeTransitionFrameType, "PInvokeTransitionFrame");
                LLVMValueRef RhpPInvoke2 = GetOrCreateLLVMFunction("RhpPInvoke2", pInvokeFunctionType);
                LLVM.BuildCall(_builder, RhpPInvoke2, new LLVMValueRef[] { pInvokeTransitionFrame }, "");
            }
            // Don't name the return value if the function returns void, it's invalid
            var returnValue = LLVM.BuildCall(_builder, nativeFunc, llvmArguments, !method.Signature.ReturnType.IsVoid ? "call" : string.Empty);

            if (method.IsPInvoke)
            {
                // add call to go to cooperative mode
                LLVMValueRef RhpPInvokeReturn2 = GetOrCreateLLVMFunction("RhpPInvokeReturn2", pInvokeFunctionType);
                LLVM.BuildCall(_builder, RhpPInvokeReturn2, new LLVMValueRef[] { pInvokeTransitionFrame }, "");
            }

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
            LLVMValueRef thunkFunc = GetOrCreateLLVMFunction(nativeName, thunkSig);

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
            MethodSignature methodSignature = (MethodSignature)_canonMethodIL.GetObject(token);

            var noHiddenParamSig = GetLLVMSignatureForMethod(methodSignature, false);
            var hddenParamSig = GetLLVMSignatureForMethod(methodSignature, true);
            var target = ((ExpressionEntry)_stack.Pop()).ValueAsType(LLVM.PointerType(noHiddenParamSig, 0), _builder);

            var functionPtrAsInt = LLVM.BuildPtrToInt(_builder, target, LLVMTypeRef.Int32Type(), "ptrToInt");
            var andResRef = LLVM.BuildBinOp(_builder, LLVMOpcode.LLVMAnd, functionPtrAsInt, LLVM.ConstInt(LLVM.Int32Type(), (ulong)_compilation.TypeSystemContext.Target.FatFunctionPointerOffset, LLVMMisc.False), "andFatCheck");
            var boolConv = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntEQ, andResRef, BuildConstInt32(0), "bitConv");
            var fatBranch = LLVM.AppendBasicBlock(_currentFunclet, "fat");
            var notFatBranch = LLVM.AppendBasicBlock(_currentFunclet, "notFat");
            var endif = LLVM.AppendBasicBlock(_currentFunclet, "endif");
            LLVM.BuildCondBr(_builder, boolConv, notFatBranch, fatBranch);
            LLVM.PositionBuilderAtEnd(_builder, notFatBranch);

            // non fat branch
            var parameterCount = methodSignature.Length + (methodSignature.IsStatic ? 0 : 1);
            StackEntry[] stackCopy = new StackEntry[parameterCount];
            for (int i = 0; i < stackCopy.Length; i++)
            {
                stackCopy[i] = _stack.Pop();
            }
            for (int i = 0; i < stackCopy.Length; i++)
            {
                _stack.Push(stackCopy[stackCopy.Length - i - 1]);
            }
            HandleCall(null, methodSignature, null, ILOpcode.calli, calliTarget: target);
            LLVMValueRef fatResRef = default;
            LLVMValueRef nonFatResRef = default;
            bool hasRes = !methodSignature.ReturnType.IsVoid;
            if (hasRes)
            {
                StackEntry nonFatRes = _stack.Pop();
                nonFatResRef = nonFatRes.ValueAsType(methodSignature.ReturnType, _builder);
            }
            LLVM.BuildBr(_builder, endif);
            LLVM.PositionBuilderAtEnd(_builder, fatBranch);

            // fat branch
            var minusOffset = RemoveFatOffset(_builder, target);
            var minusOffsetPtr = LLVM.BuildIntToPtr(_builder, minusOffset,
                LLVMTypeRef.PointerType(LLVMTypeRef.Int8Type(), 0), "ptr");
            var hiddenRefAddr = LLVM.BuildGEP(_builder, minusOffsetPtr, new[] { BuildConstInt32(_pointerSize) }, "fatArgPtr");
            var hiddenRefPtrPtr = LLVM.BuildPointerCast(_builder, hiddenRefAddr, LLVM.PointerType(LLVM.PointerType(LLVM.PointerType(LLVMTypeRef.Int8Type(), 0), 0), 0), "hiddenRefPtr");
            var hiddenRef = LLVM.BuildLoad(_builder, LLVM.BuildLoad(_builder, hiddenRefPtrPtr, "hiddenRefPtr"), "hiddenRef");

            for (int i = 0; i < stackCopy.Length; i++)
            {
                _stack.Push(stackCopy[stackCopy.Length - i - 1]);
            }
            var funcPtrPtrWithHidden = LLVM.BuildPointerCast(_builder, minusOffsetPtr, LLVM.PointerType(LLVM.PointerType(hddenParamSig, 0), 0), "hiddenFuncPtr");
            var funcWithHidden = LLVM.BuildLoad(_builder, funcPtrPtrWithHidden, "funcPtr");
            HandleCall(null, methodSignature, null, ILOpcode.calli, calliTarget: funcWithHidden, hiddenRef: hiddenRef);
            StackEntry fatRes = null;
            if (hasRes)
            {
                fatRes = _stack.Pop();
                fatResRef = fatRes.ValueAsType(methodSignature.ReturnType, _builder);
            }
            LLVM.BuildBr(_builder, endif);
            LLVM.PositionBuilderAtEnd(_builder, endif);

            // choose the right return value
            if (hasRes)
            {
                var phi = LLVM.BuildPhi(_builder, GetLLVMTypeForTypeDesc(methodSignature.ReturnType), "phi");
                LLVM.AddIncoming(phi, new LLVMValueRef[] { fatResRef, nonFatResRef },
                    new LLVMBasicBlockRef[] { fatBranch, notFatBranch }, 2);
                PushExpression(fatRes.Kind, "phi", phi, fatRes.Type);
            }
            _currentBasicBlock.LastInternalIf = endif;
        }

        private void ImportLdFtn(int token, ILOpcode opCode)
        {
            MethodDesc runtimeDeterminedMethod = (MethodDesc)_methodIL.GetObject(token);
            MethodDesc method = ((MethodDesc)_canonMethodIL.GetObject(token));
            MethodDesc canonMethod = method.GetCanonMethodTarget(CanonicalFormKind.Specific);
            LLVMValueRef targetLLVMFunction = default;
            bool hasHiddenParam = false;

            if (opCode == ILOpcode.ldvirtftn)
            {
                StackEntry thisPointer = _stack.Pop();
                if (runtimeDeterminedMethod.IsVirtual)
                {
                    // we want the fat function ptr here
                    LLVMValueRef fatFunctionPtr;
                    targetLLVMFunction = LLVMFunctionForMethod(method, canonMethod, thisPointer, true, null, runtimeDeterminedMethod, out hasHiddenParam, out LLVMValueRef dictPtrPtrStore, out fatFunctionPtr);
                    if (fatFunctionPtr.Pointer != IntPtr.Zero)
                    {
                        targetLLVMFunction = fatFunctionPtr;
                    }
                }
                else
                {
                    AddMethodReference(runtimeDeterminedMethod);
                }
            }
            else
            {
                if (canonMethod.IsSharedByGenericInstantiations && (canonMethod.HasInstantiation || canonMethod.Signature.IsStatic))
                {
                    var exactContextNeedsRuntimeLookup = method.HasInstantiation
                        ? method.IsSharedByGenericInstantiations
                        : method.OwningType.IsCanonicalSubtype(CanonicalFormKind.Any);
                    if (exactContextNeedsRuntimeLookup)
                    {
                        targetLLVMFunction = CallGenericHelper(ReadyToRunHelperId.MethodEntry, runtimeDeterminedMethod);
                        if (!(canonMethod.IsVirtual && !canonMethod.IsFinal && !canonMethod.OwningType.IsSealed()))
                        {
                            // fat function pointer
                            targetLLVMFunction = MakeFatPointer(_builder, targetLLVMFunction, _compilation);
                        }
                    }
                    else
                    {
                        var fatFunctionSymbol = GetAndAddFatFunctionPointer(runtimeDeterminedMethod);
                        targetLLVMFunction = MakeFatPointer(_builder, LoadAddressOfSymbolNode(fatFunctionSymbol), _compilation);
                    }
                }
                else AddMethodReference(canonMethod);
            }

            if (targetLLVMFunction.Pointer.Equals(IntPtr.Zero))
            {
                if (runtimeDeterminedMethod.IsNativeCallable)
                {
                    EcmaMethod ecmaMethod = ((EcmaMethod)runtimeDeterminedMethod);
                    string mangledName = ecmaMethod.GetNativeCallableExportName();
                    if (mangledName == null)
                    {
                        mangledName = ecmaMethod.Name;
                    }
                    LLVMTypeRef[] llvmParams = new LLVMTypeRef[runtimeDeterminedMethod.Signature.Length];
                    for (int i = 0; i < llvmParams.Length; i++)
                    {
                        llvmParams[i] = GetLLVMTypeForTypeDesc(runtimeDeterminedMethod.Signature[i]);
                    }
                    LLVMTypeRef thunkSig = LLVM.FunctionType(GetLLVMTypeForTypeDesc(runtimeDeterminedMethod.Signature.ReturnType), llvmParams, false);

                    targetLLVMFunction = GetOrCreateLLVMFunction(mangledName, thunkSig);
                }
                else
                {
                    hasHiddenParam = canonMethod.RequiresInstArg();
                    targetLLVMFunction = GetOrCreateLLVMFunction(_compilation.NameMangler.GetMangledMethodName(canonMethod).ToString(), runtimeDeterminedMethod.Signature, hasHiddenParam);
                }
            }

            var entry = new FunctionPointerEntry("ldftn", runtimeDeterminedMethod, targetLLVMFunction, GetWellKnownType(WellKnownType.IntPtr), opCode == ILOpcode.ldvirtftn);
            _stack.Push(entry);
        }

        ISymbolNode GetAndAddFatFunctionPointer(MethodDesc method, bool isUnboxingStub = false)
        {
            ISymbolNode node = _compilation.NodeFactory.FatFunctionPointer(method, isUnboxingStub);
            _dependencies.Add(node);
            return node;
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

                    LLVMValueRef right = op1.ValueForStackKind(kind, _builder, TypeNeedsSignExtension(op1.Type));
                    LLVMValueRef left = op2.ValueForStackKind(kind, _builder, TypeNeedsSignExtension(op2.Type));

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
                        if (op1.Type.IsWellKnownType(WellKnownType.Double) && op2.Type.IsWellKnownType(WellKnownType.Single))
                        {
                            left = LLVM.BuildFPExt(_builder, left, LLVM.DoubleType(), "fpextop2");
                        }
                        else if (op2.Type.IsWellKnownType(WellKnownType.Double) && op1.Type.IsWellKnownType(WellKnownType.Single))
                        {
                            right = LLVM.BuildFPExt(_builder, right, LLVM.DoubleType(), "fpextop1");
                        }
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
            if (type == null)
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
            LLVMValueRef left = op2.ValueForStackKind(kind, _builder, TypeNeedsSignExtension(op2.Type));
            LLVMValueRef right = op1.ValueForStackKind(kind, _builder, TypeNeedsSignExtension(op1.Type));
            if (kind == StackValueKind.Float)
            {
                if (op1.Type.IsWellKnownType(WellKnownType.Double) && op2.Type.IsWellKnownType(WellKnownType.Single))
                {
                    left = LLVM.BuildFPExt(_builder, left, LLVM.DoubleType(), "fpextop2");
                }
                else if (op2.Type.IsWellKnownType(WellKnownType.Double) && op1.Type.IsWellKnownType(WellKnownType.Single))
                {
                    right = LLVM.BuildFPExt(_builder, right, LLVM.DoubleType(), "fpextop1");
                }
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
                // these ops return an int32 for these.
                type = WidenBytesAndShorts(type);
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

        private TypeDesc WidenBytesAndShorts(TypeDesc type)
        {
            switch (type.Category)
            {
                case TypeFlags.Byte:
                case TypeFlags.SByte:
                case TypeFlags.Int16:
                case TypeFlags.UInt16:
                    return GetWellKnownType(WellKnownType.Int32);
                default:
                    return type;
            }
        }

        private void ImportShiftOperation(ILOpcode opcode)
        {
            LLVMValueRef result;
            StackEntry numBitsToShift = _stack.Pop();
            StackEntry valueToShift = _stack.Pop();

            LLVMValueRef valueToShiftValue = valueToShift.ValueForStackKind(valueToShift.Kind, _builder, TypeNeedsSignExtension(valueToShift.Type));

            // while it seems excessive that the bits to shift should need to be 64 bits, the LLVM docs say that both operands must be the same type and a compilation failure results if this is not the case.
            LLVMValueRef rhs;
            if (valueToShiftValue.TypeOf().Equals(LLVM.Int64Type()))
            {
                rhs = numBitsToShift.ValueAsInt64(_builder, false);
            }
            else
            {
                rhs = numBitsToShift.ValueAsInt32(_builder, false);
            }
            switch (opcode)
            {
                case ILOpcode.shl:
                    result = LLVM.BuildShl(_builder, valueToShiftValue, rhs, "shl");
                    break;
                case ILOpcode.shr:
                    result = LLVM.BuildAShr(_builder, valueToShiftValue, rhs, "shr");
                    break;
                case ILOpcode.shr_un:
                    result = LLVM.BuildLShr(_builder, valueToShiftValue, rhs, "shr");
                    break;
                default:
                    throw new InvalidOperationException(); // Should be unreachable
            }
            //TODO: do we need this if we sign extend above?
            PushExpression(valueToShift.Kind, "shiftop", result, WidenBytesAndShorts(valueToShift.Type));
        }

        bool TypeNeedsSignExtension(TypeDesc targetType)
        {
            var enumCleanTargetType = targetType?.UnderlyingType;
            if (enumCleanTargetType != null && targetType.IsPrimitive)
            {
                if (enumCleanTargetType.IsWellKnownType(WellKnownType.Byte) ||
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
                if (op1.Type.IsWellKnownType(WellKnownType.Double) && op2.Type.IsWellKnownType(WellKnownType.Single))
                {
                    typeSaneOp2 = LLVM.BuildFPExt(_builder, typeSaneOp2, LLVM.DoubleType(), "fpextop2");
                }
                else if (op2.Type.IsWellKnownType(WellKnownType.Double) && op1.Type.IsWellKnownType(WellKnownType.Single))
                {
                    typeSaneOp1 = LLVM.BuildFPExt(_builder, typeSaneOp1, LLVM.DoubleType(), "fpextop1");
                }
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
            LLVMValueRef converted = CastIfNecessary(loadedValue, GetLLVMTypeForTypeDesc(destType), value.Name(), wellKnownType == WellKnownType.UInt64 /* unsigned is always false, so check for the type explicitly */);
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
            TypeDesc methodType = (TypeDesc)_methodIL.GetObject(token);
            LLVMValueRef eeType;
            var eeTypeDesc = GetEETypePtrTypeDesc();
            ExpressionEntry eeTypeExp;
            if (methodType.IsRuntimeDeterminedSubtype)
            {
                eeType = CallGenericHelper(ReadyToRunHelperId.TypeHandle, methodType);
                eeTypeExp = new ExpressionEntry(StackValueKind.ByRef, "eeType", eeType, eeTypeDesc);
            }
            else
            {
                eeType = GetEETypePointerForTypeDesc(methodType, true);
                eeTypeExp = new LoadExpressionEntry(StackValueKind.ByRef, "eeType", eeType, eeTypeDesc);
            }
            StackEntry boxedObject = _stack.Pop();
            if (opCode == ILOpcode.unbox)
            {
                if (methodType.IsNullable)
                    throw new NotImplementedException();

                var arguments = new StackEntry[] { eeTypeExp, boxedObject };
                PushNonNull(CallRuntime(_compilation.TypeSystemContext, RuntimeExport, "RhUnbox2", arguments));
            }
            else //unbox_any
            {
                Debug.Assert(opCode == ILOpcode.unbox_any);
                LLVMValueRef untypedObjectValue = LLVM.BuildAlloca(_builder, GetLLVMTypeForTypeDesc(methodType), "objptr");
                var arguments = new StackEntry[]
                {
                    boxedObject,
                    new ExpressionEntry(StackValueKind.ByRef, "objPtr", untypedObjectValue, methodType.MakePointerType()),
                    eeTypeExp
                };
                CallRuntime(_compilation.TypeSystemContext, RuntimeExport, "RhUnboxAny", arguments);
                PushLoadExpression(GetStackValueKind(methodType), "unboxed", untypedObjectValue, methodType);
            }
        }

        LLVMValueRef GetShadowStack()
        {
            int offset = GetTotalParameterOffset() + GetTotalLocalOffset();
            return LLVM.BuildGEP(_builder, LLVM.GetFirstParam(_currentFunclet),
                new LLVMValueRef[] { LLVM.ConstInt(LLVM.Int32Type(), (uint)offset, LLVMMisc.False) },
                String.Empty);
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
            if (ldtokenValue is TypeDesc)
            {
                TypeDesc runtimeTypeHandleTypeDesc = GetWellKnownType(WellKnownType.RuntimeTypeHandle);
                var typeDesc = (TypeDesc)ldtokenValue;
                MethodDesc helper = _compilation.TypeSystemContext.GetHelperEntryPoint("LdTokenHelpers", "GetRuntimeTypeHandle");
                AddMethodReference(helper);
                var fn = LLVMFunctionForMethod(helper, helper, null/* static method */, false /* not virt */, _constrainedType, helper, out bool hasHiddenParam, out LLVMValueRef dictPtrPtrStore, out LLVMValueRef fatFunctionPtr);

                if (typeDesc.IsRuntimeDeterminedSubtype)
                {
                    var hiddenParam = CallGenericHelper(ReadyToRunHelperId.TypeHandle, typeDesc);
                    var handleRef = LLVM.BuildCall(_builder, fn, new LLVMValueRef[]
                    {
                        GetShadowStack(),
                        hiddenParam
                    }, "getHelper");
                    _stack.Push(new LdTokenEntry<TypeDesc>(StackValueKind.ValueType, "ldtoken", typeDesc, handleRef, runtimeTypeHandleTypeDesc));
                }
                else
                {
                    PushLoadExpression(StackValueKind.ByRef, "ldtoken", GetEETypePointerForTypeDesc(typeDesc, true), GetEETypePtrTypeDesc());
                    HandleCall(helper, helper.Signature, helper);
                    var callExp = _stack.Pop();
                    _stack.Push(new LdTokenEntry<TypeDesc>(StackValueKind.ValueType, "ldtoken", typeDesc, callExp.ValueAsInt32(_builder, false), runtimeTypeHandleTypeDesc));
                }
            }
            else if (ldtokenValue is FieldDesc)
            {
                LLVMValueRef fieldHandle = LLVM.ConstStruct(new LLVMValueRef[] { BuildConstInt32(0) }, true);
                StackEntry value = new LdTokenEntry<FieldDesc>(StackValueKind.ValueType, null, (FieldDesc)ldtokenValue, fieldHandle, GetWellKnownType(WellKnownType.RuntimeFieldHandle));
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

        private void ThrowIfNull(LLVMValueRef entry)
        {
            if (NullRefFunction.Pointer == IntPtr.Zero)
            {
                NullRefFunction = LLVM.AddFunction(Module, "corert.throwifnull", LLVM.FunctionType(LLVM.VoidType(), new LLVMTypeRef[] { LLVM.PointerType(LLVMTypeRef.Int8Type(), 0), LLVM.PointerType(LLVMTypeRef.Int8Type(), 0) }, false));
                var builder = LLVM.CreateBuilder();
                var block = LLVM.AppendBasicBlock(NullRefFunction, "Block");
                var throwBlock = LLVM.AppendBasicBlock(NullRefFunction, "ThrowBlock");
                var retBlock = LLVM.AppendBasicBlock(NullRefFunction, "RetBlock");
                LLVM.PositionBuilderAtEnd(builder, block);
                LLVM.BuildCondBr(builder, LLVM.BuildICmp(builder, LLVMIntPredicate.LLVMIntEQ, LLVM.GetParam(NullRefFunction, 1), LLVM.ConstPointerNull(LLVM.PointerType(LLVMTypeRef.Int8Type(), 0)), "nullCheck"),
                    throwBlock, retBlock);
                LLVM.PositionBuilderAtEnd(builder, throwBlock);
                MetadataType nullRefType = _compilation.NodeFactory.TypeSystemContext.SystemModule.GetType("System", "NullReferenceException");

                var arguments = new StackEntry[] { new LoadExpressionEntry(StackValueKind.ValueType, "eeType", GetEETypePointerForTypeDesc(nullRefType, true), GetEETypePtrTypeDesc()) };

                MetadataType helperType = _compilation.TypeSystemContext.SystemModule.GetKnownType("System.Runtime", RuntimeExport);
                MethodDesc helperMethod = helperType.GetKnownMethod("RhNewObject", null);
                var resultAddress = LLVM.BuildIntCast(builder, LLVM.BuildAlloca(builder, LLVM.Int32Type(), "resultAddress"), LLVM.PointerType(LLVMTypeRef.Int8Type(), 0), "castResultAddress");
                HandleDirectCall(helperMethod, helperMethod.Signature, arguments, null, default(LLVMValueRef), 0, LLVM.GetParam(NullRefFunction, 0), builder, true, resultAddress, helperMethod);

                var exceptionEntry = new ExpressionEntry(GetStackValueKind(nullRefType), "RhNewObject_return", resultAddress, nullRefType);

                var ctorDef = nullRefType.GetDefaultConstructor();

                var constructedExceptionObject = HandleDirectCall(ctorDef, ctorDef.Signature, new StackEntry[] { exceptionEntry }, null, default(LLVMValueRef), 0, LLVM.GetParam(NullRefFunction, 0), builder, false, default(LLVMValueRef), ctorDef);

                EmitTrapCall(builder);
                LLVM.PositionBuilderAtEnd(builder, retBlock);
                LLVM.BuildRetVoid(builder);
            }

            LLVMValueRef shadowStack = LLVM.BuildGEP(_builder, LLVM.GetFirstParam(_currentFunclet), new LLVMValueRef[] { LLVM.ConstInt(LLVM.Int32Type(), (uint)(GetTotalLocalOffset() + GetTotalParameterOffset()), LLVMMisc.False) }, String.Empty);

            LLVM.BuildCall(_builder, NullRefFunction, new LLVMValueRef[] { shadowStack, entry }, string.Empty);
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

        private LLVMValueRef GetFieldAddress(FieldDesc runtimeDeterminedField, FieldDesc field, bool isStatic)
        {
            if (field.IsStatic)
            {
                //pop unused value
                if (!isStatic)
                    _stack.Pop();

                ISymbolNode node = null;
                MetadataType owningType = (MetadataType)_compilation.ConvertToCanonFormIfNecessary(field.OwningType, CanonicalFormKind.Specific);
                LLVMValueRef staticBase;
                int fieldOffset;
                // If the type is non-BeforeFieldInit, this is handled before calling any methods on it
                //TODO : this seems to call into the cctor if the cctor itself accesses static fields. e.g. SR.  Try a test with an ++ in the cctor
                bool needsCctorCheck = (owningType.IsBeforeFieldInit || (!owningType.IsBeforeFieldInit && owningType != _thisType)) && _compilation.TypeSystemContext.HasLazyStaticConstructor(owningType);

                if (field.HasRva)
                {
                    node = _compilation.GetFieldRvaData(field);
                    staticBase = LoadAddressOfSymbolNode(node);
                    fieldOffset = 0;
                    // Run static constructor if necessary
                    if (needsCctorCheck)
                    {
                        TriggerCctor(owningType);
                    }
                }
                else
                {
                    fieldOffset = field.Offset.AsInt;
                    TypeDesc runtimeDeterminedOwningType = runtimeDeterminedField.OwningType;
                    if (field.IsThreadStatic)
                    {
                        if (runtimeDeterminedOwningType.IsRuntimeDeterminedSubtype)
                        {
                            staticBase = CallGenericHelper(ReadyToRunHelperId.GetThreadStaticBase, runtimeDeterminedOwningType);
                        }
                        else
                        {
                            ExpressionEntry returnExp;
                            node = TriggerCctorWithThreadStaticStorage((MetadataType)runtimeDeterminedOwningType, needsCctorCheck, out returnExp);
                            staticBase = returnExp.ValueAsType(returnExp.Type, _builder);
                        }
                    }
                    else
                    {
                        if (field.HasGCStaticBase)
                        {
                            if (runtimeDeterminedOwningType.IsRuntimeDeterminedSubtype)
                            {
                                needsCctorCheck = false; // no cctor for canonical types
                                staticBase = CallGenericHelper(ReadyToRunHelperId.GetGCStaticBase, runtimeDeterminedOwningType);
                            }
                            else
                            {
                                node = _compilation.NodeFactory.TypeGCStaticsSymbol(owningType);
                                LLVMValueRef basePtrPtr = LoadAddressOfSymbolNode(node);
                                staticBase = LLVM.BuildLoad(_builder, LLVM.BuildLoad(_builder, LLVM.BuildPointerCast(_builder, basePtrPtr, LLVM.PointerType(LLVM.PointerType(LLVM.PointerType(LLVM.Int8Type(), 0), 0), 0), "castBasePtrPtr"), "basePtr"), "base");
                            }
                        }
                        else
                        {
                            if (runtimeDeterminedOwningType.IsRuntimeDeterminedSubtype)
                            {
                                needsCctorCheck = false; // no cctor for canonical types
                                staticBase = CallGenericHelper(ReadyToRunHelperId.GetNonGCStaticBase, runtimeDeterminedOwningType);
                            }
                            else
                            {
                                node = _compilation.NodeFactory.TypeNonGCStaticsSymbol(owningType);
                                staticBase = LoadAddressOfSymbolNode(node);
                            }
                        }
                        // Run static constructor if necessary
                        if (needsCctorCheck)
                        {
                            TriggerCctor(owningType);
                        }
                    }
                }

                if (node != null) _dependencies.Add(node);

                LLVMValueRef castStaticBase = LLVM.BuildPointerCast(_builder, staticBase, LLVM.PointerType(LLVM.Int8Type(), 0), owningType.Name + "_statics");
                LLVMValueRef fieldAddr = LLVM.BuildGEP(_builder, castStaticBase, new LLVMValueRef[] { BuildConstInt32(fieldOffset) }, field.Name + "_addr");


                return fieldAddr;
            }
            else
            {
                return GetInstanceFieldAddress(_stack.Pop(), field);
            }
        }

        ISymbolNode GetGenericLookupHelperAndAddReference(ReadyToRunHelperId helperId, object helperArg, out LLVMValueRef helper, IEnumerable<LLVMTypeRef> additionalArgs = null)
        {
            ISymbolNode node;
            var retType = helperId == ReadyToRunHelperId.DelegateCtor
                ? LLVMTypeRef.VoidType()
                : LLVMTypeRef.PointerType(LLVMTypeRef.Int8Type(), 0);
            var helperArgs = new List<LLVMTypeRef>
            {
                LLVMTypeRef.PointerType(LLVMTypeRef.Int8Type(), 0),
                LLVMTypeRef.PointerType(LLVMTypeRef.Int8Type(), 0),
            };
            if (additionalArgs != null) helperArgs.AddRange(additionalArgs);
            if (_method.RequiresInstMethodDescArg())
            {
                node = _compilation.NodeFactory.ReadyToRunHelperFromDictionaryLookup(helperId, helperArg, _method);
                helper = GetOrCreateLLVMFunction(node.GetMangledName(_compilation.NameMangler),
                    LLVMTypeRef.FunctionType(retType, helperArgs.ToArray(), false));
            }
            else
            {
                 Debug.Assert(_method.RequiresInstMethodTableArg() || _method.AcquiresInstMethodTableFromThis());
                node = _compilation.NodeFactory.ReadyToRunHelperFromTypeLookup(helperId, helperArg, _method.OwningType);
                helper = GetOrCreateLLVMFunction(node.GetMangledName(_compilation.NameMangler),
                    LLVMTypeRef.FunctionType(retType, helperArgs.ToArray(), false));
            }
            // cpp backend relies on a lazy static constructor to get this node added during the dependency generation. 
            // If left to when the code is written that uses the helper then its too late.
            IMethodNode helperNode = (IMethodNode)_compilation.NodeFactory.HelperEntrypoint(HelperEntrypoint.EnsureClassConstructorRunAndReturnNonGCStaticBase);

            _dependencies.Add(node);
            _dependencies.Add(helperNode);
            return node;
        }

        /// <summary>
        /// Triggers a static constructor check and call for types that have them
        /// </summary>
        private void TriggerCctor(MetadataType type)
        {
            if (type.IsCanonicalSubtype(CanonicalFormKind.Specific)) return; // TODO - what to do here?
            ISymbolNode classConstructionContextSymbol = _compilation.NodeFactory.TypeNonGCStaticsSymbol(type);
            _dependencies.Add(classConstructionContextSymbol);
            LLVMValueRef firstNonGcStatic = LoadAddressOfSymbolNode(classConstructionContextSymbol);

            // TODO: Codegen could check whether it has already run rather than calling into EnsureClassConstructorRun
            // but we'd have to figure out how to manage the additional basic blocks
            LLVMValueRef classConstructionContextPtr = LLVM.BuildGEP(_builder, firstNonGcStatic, new LLVMValueRef[] { BuildConstInt32(-2) }, "classConstructionContext");
            StackEntry classConstructionContext = new AddressExpressionEntry(StackValueKind.NativeInt, "classConstructionContext", classConstructionContextPtr, GetWellKnownType(WellKnownType.IntPtr));
            CallRuntime("System.Runtime.CompilerServices", _compilation.TypeSystemContext, ClassConstructorRunner, "EnsureClassConstructorRun", new StackEntry[] { classConstructionContext });
        }

        /// <summary>
        /// Triggers creation of thread static storage and the static constructor if present
        /// </summary>
        private ISymbolNode TriggerCctorWithThreadStaticStorage(MetadataType type, bool needsCctorCheck, out ExpressionEntry returnExp)
        {
            ISymbolNode threadStaticIndexSymbol = _compilation.NodeFactory.TypeThreadStaticIndex(type);
            LLVMValueRef threadStaticIndex = LoadAddressOfSymbolNode(threadStaticIndexSymbol);

            StackEntry typeManagerSlotEntry = new LoadExpressionEntry(StackValueKind.ValueType, "typeManagerSlot", threadStaticIndex, GetWellKnownType(WellKnownType.Int32));
            LLVMValueRef typeTlsIndexPtr =
                LLVM.BuildGEP(_builder, threadStaticIndex, new LLVMValueRef[] { BuildConstInt32(1) }, "typeTlsIndexPtr"); // index is the second field after the ptr.
            StackEntry tlsIndexExpressionEntry = new LoadExpressionEntry(StackValueKind.ValueType, "typeTlsIndex", typeTlsIndexPtr, GetWellKnownType(WellKnownType.Int32));

            if (needsCctorCheck)
            {
                ISymbolNode classConstructionContextSymbol = _compilation.NodeFactory.TypeNonGCStaticsSymbol(type);
                _dependencies.Add(classConstructionContextSymbol);
                LLVMValueRef firstNonGcStatic = LoadAddressOfSymbolNode(classConstructionContextSymbol);

                // TODO: Codegen could check whether it has already run rather than calling into EnsureClassConstructorRun
                // but we'd have to figure out how to manage the additional basic blocks
                LLVMValueRef classConstructionContextPtr = LLVM.BuildGEP(_builder, firstNonGcStatic, new LLVMValueRef[] { BuildConstInt32(-2) }, "classConstructionContext");
                StackEntry classConstructionContext = new AddressExpressionEntry(StackValueKind.NativeInt, "classConstructionContext", classConstructionContextPtr,
                    GetWellKnownType(WellKnownType.IntPtr));

                returnExp = CallRuntime("System.Runtime.CompilerServices", _compilation.TypeSystemContext, ClassConstructorRunner, "CheckStaticClassConstructionReturnThreadStaticBase", new StackEntry[]
                                                                             {
                                                                                 typeManagerSlotEntry,
                                                                                 tlsIndexExpressionEntry,
                                                                                 classConstructionContext
                                                                             });
                return threadStaticIndexSymbol;
            }
            else
            {
                returnExp = CallRuntime("Internal.Runtime", _compilation.TypeSystemContext, ThreadStatics, "GetThreadStaticBaseForType", new StackEntry[]
                                                                                                                             {
                                                                                                                                 typeManagerSlotEntry,
                                                                                                                                 tlsIndexExpressionEntry
                                                                                                                             });
                return threadStaticIndexSymbol;
            }
        }

        private void TriggerCctor(MetadataType type, LLVMValueRef staticBaseValueRef, string runnerMethodName)
        {
            var classConstCtx = LLVM.BuildGEP(_builder,
                LLVM.BuildBitCast(_builder, staticBaseValueRef, LLVMTypeRef.PointerType(LLVMTypeRef.Int8Type(), 0),
                    "ptr8"), new LLVMValueRef[] { BuildConstInt32(-8) }, "backToClassCtx");
            StackEntry classConstructionContext = new AddressExpressionEntry(StackValueKind.NativeInt, "classConstructionContext", classConstCtx,
                GetWellKnownType(WellKnownType.IntPtr));
            StackEntry staticBaseEntry = new AddressExpressionEntry(StackValueKind.NativeInt, "staticBase", staticBaseValueRef,
                GetWellKnownType(WellKnownType.IntPtr));

            CallRuntime("System.Runtime.CompilerServices", _compilation.TypeSystemContext, ClassConstructorRunner, runnerMethodName, new StackEntry[]
                                                                         {
                                                                             classConstructionContext,
                                                                             staticBaseEntry
                                                                         });
        }

        private void ImportLoadField(int token, bool isStatic)
        {
            FieldDesc field = (FieldDesc)_methodIL.GetObject(token);
            FieldDesc canonFieldDesc = (FieldDesc)_canonMethodIL.GetObject(token);
            LLVMValueRef fieldAddress = GetFieldAddress(field, canonFieldDesc, isStatic);

            PushLoadExpression(GetStackValueKind(canonFieldDesc.FieldType), $"Field_{field.Name}", fieldAddress, canonFieldDesc.FieldType);
        }

        private void ImportAddressOfField(int token, bool isStatic)
        {
            FieldDesc field = (FieldDesc)_methodIL.GetObject(token);
            LLVMValueRef fieldAddress = GetFieldAddress(field, (FieldDesc)_canonMethodIL.GetObject(token), isStatic);
            _stack.Push(new AddressExpressionEntry(StackValueKind.ByRef, $"FieldAddress_{field.Name}", fieldAddress, field.FieldType.MakeByRefType()));
        }

        private void ImportStoreField(int token, bool isStatic)
        {
            FieldDesc runtimeDeterminedField = (FieldDesc)_methodIL.GetObject(token);
            FieldDesc field = (FieldDesc)_canonMethodIL.GetObject(token);
            StackEntry valueEntry = _stack.Pop();
            TypeDesc fieldType = _compilation.ConvertToCanonFormIfNecessary(field.FieldType, CanonicalFormKind.Specific);

            LLVMValueRef fieldAddress = GetFieldAddress(runtimeDeterminedField, field, isStatic);
            CastingStore(fieldAddress, valueEntry, fieldType);
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
            else if (llvmType.TypeKind == LLVMTypeKind.LLVMFloatTypeKind || llvmType.TypeKind == LLVMTypeKind.LLVMDoubleTypeKind)
                LLVM.BuildStore(_builder, LLVM.ConstReal(llvmType, 0.0), valueEntry.ValueAsType(LLVM.PointerType(llvmType, 0), _builder));
            else
                throw new NotImplementedException();
        }

        private void ImportBox(int token)
        {
            LLVMValueRef eeType;
            TypeDesc type = (TypeDesc)_methodIL.GetObject(token);

            StackEntry eeTypeEntry;
            var eeTypeDesc = GetEETypePtrTypeDesc();
            bool truncDouble = type.Equals(GetWellKnownType(WellKnownType.Single));
            if (type.IsRuntimeDeterminedSubtype)
            {
                eeType = CallGenericHelper(ReadyToRunHelperId.TypeHandle, type);
                eeTypeEntry = new ExpressionEntry(StackValueKind.ValueType, "eeType", eeType, eeTypeDesc.MakePointerType());
                type = type.ConvertToCanonForm(CanonicalFormKind.Specific);
            }
            else
            {
                eeType = GetEETypePointerForTypeDesc(type, true);
                eeTypeEntry = new LoadExpressionEntry(StackValueKind.ValueType, "eeType", eeType, eeTypeDesc.MakePointerType());
            }
            var toBoxValue = _stack.Pop();
            StackEntry valueAddress;
            if (truncDouble)
            {
                var doubleToBox = toBoxValue.ValueAsType(LLVMTypeRef.DoubleType(), _builder);
                var singleToBox = LLVM.BuildFPTrunc(_builder, doubleToBox, LLVMTypeRef.FloatType(), "trunc");
                toBoxValue = new ExpressionEntry(StackValueKind.Float, "singleToBox", singleToBox,
                    GetWellKnownType(WellKnownType.Single));
            }
            valueAddress = TakeAddressOf(toBoxValue);
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
            for (int i = _exceptionRegions.Length - 1; i >= 0; i--)
            {
                var r = _exceptionRegions[i];

                if (r.ILRegion.Kind == ILExceptionRegionKind.Finally &&
                    IsOffsetContained(_currentOffset - 1, r.ILRegion.TryOffset, r.ILRegion.TryLength) &&
                    !IsOffsetContained(target.StartOffset, r.ILRegion.TryOffset, r.ILRegion.TryLength))
                {
                    // Work backwards through containing finally blocks to call them in the right order
                    BasicBlock finallyBlock = _basicBlocks[r.ILRegion.HandlerOffset];
                    MarkBasicBlock(finallyBlock);
                    var funcletParams = new LLVMValueRef[FuncletsRequireHiddenContext() ? 2 : 1];
                    funcletParams[0] = LLVM.GetFirstParam(_currentFunclet);
                    if (FuncletsRequireHiddenContext())
                    {
                        funcletParams[1] = LLVM.GetParam(_currentFunclet, GetHiddenContextParamNo());
                    }
                    LLVM.BuildCall(_builder, GetFuncletForBlock(finallyBlock), funcletParams, String.Empty);
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
            TypeDesc runtimeDeterminedType = (TypeDesc)_methodIL.GetObject(token);
            TypeDesc runtimeDeterminedArrayType = runtimeDeterminedType.MakeArrayType();
            var sizeOfArray = _stack.Pop();
            StackEntry[] arguments;
            var eeTypeDesc = _compilation.TypeSystemContext.SystemModule.GetKnownType("Internal.Runtime", "EEType").MakePointerType();
            if (runtimeDeterminedArrayType.IsRuntimeDeterminedSubtype)
            {
                var lookedUpType = CallGenericHelper(ReadyToRunHelperId.TypeHandle, runtimeDeterminedArrayType);
                arguments = new StackEntry[] { new ExpressionEntry(StackValueKind.ValueType, "eeType", lookedUpType, eeTypeDesc), sizeOfArray };
            }
            else
            {
                arguments = new StackEntry[] { new LoadExpressionEntry(StackValueKind.ValueType, "eeType", GetEETypePointerForTypeDesc(runtimeDeterminedArrayType, true), eeTypeDesc), sizeOfArray };
                //TODO: call GetNewArrayHelperForType from JitHelper.cs (needs refactoring)
            }
            PushNonNull(CallRuntime(_compilation.TypeSystemContext, InternalCalls, "RhpNewArray", arguments, runtimeDeterminedArrayType));
        }

        LLVMValueRef GetGenericContext()
        {
            Debug.Assert(_method.IsSharedByGenericInstantiations);
            if (_method.AcquiresInstMethodTableFromThis())
            {
                LLVMValueRef typedAddress;
                LLVMValueRef thisPtr;
                
                typedAddress = CastIfNecessary(_builder, LLVM.GetFirstParam(_currentFunclet),
                    LLVM.PointerType(LLVM.PointerType(LLVM.PointerType(LLVMTypeRef.Int8Type(), 0), 0), 0));
                thisPtr = LLVM.BuildLoad(_builder, typedAddress, "loadThis");

                return LLVM.BuildLoad(_builder, thisPtr, "methodTablePtrRef");
            }
            return CastIfNecessary(_builder, LLVM.GetParam(_currentFunclet, GetHiddenContextParamNo() /* hidden param after shadow stack and return slot if present */), LLVMTypeRef.PointerType(LLVMTypeRef.Int8Type(), 0), "HiddenArg");
        }

        uint GetHiddenContextParamNo()
        {
            return 1 + (NeedsReturnStackSlot(_method.Signature) ? (uint)1 : 0);
        }

        bool FuncletsRequireHiddenContext()
        {
            return _method.IsSharedByGenericInstantiations && !_method.AcquiresInstMethodTableFromThis();
        }

        private LLVMValueRef ArrayBaseSizeRef()
        {
            return BuildConstInt32(ArrayBaseSize());
        }

        private int ArrayBaseSize()
        {
            return 2 * _compilation.NodeFactory.Target.PointerSize;
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
            var arrayReferenceValue = arrayReference.ValueAsType(LLVM.PointerType(LLVM.Int8Type(), 0), _builder);
            ThrowIfNull(arrayReferenceValue);
            LLVMValueRef lengthPtr = LLVM.BuildGEP(_builder, arrayReferenceValue, new LLVMValueRef[] { BuildConstInt32(_compilation.NodeFactory.Target.PointerSize) }, "arrayLength");
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
            ThrowIfNull(arrayReference);
            var elementSize = arrayElementType.GetElementSize();
            LLVMValueRef elementOffset = LLVM.BuildMul(_builder, elementPosition, BuildConstInt32(elementSize.AsInt), "elementOffset");
            LLVMValueRef arrayOffset = LLVM.BuildAdd(_builder, elementOffset, ArrayBaseSizeRef(), "arrayOffset");
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
            LLVM.BuildRetVoid(_builder);
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
        private const string InternalCalls = "InternalCalls";
        private const string TypeCast = "TypeCast";
        private const string DispatchResolve = "DispatchResolve";
        private const string ThreadStatics = "ThreadStatics";
        private const string ClassConstructorRunner = "ClassConstructorRunner";

        private ExpressionEntry CallRuntime(TypeSystemContext context, string className, string methodName, StackEntry[] arguments, TypeDesc forcedReturnType = null)
        {
            return CallRuntime("System.Runtime", context, className, methodName, arguments, forcedReturnType);
        }

        private ExpressionEntry CallRuntime(string @namespace, TypeSystemContext context, string className, string methodName, StackEntry[] arguments, TypeDesc forcedReturnType = null)
        {
            MetadataType helperType = context.SystemModule.GetKnownType(@namespace, className);
            MethodDesc helperMethod = helperType.GetKnownMethod(methodName, null);
            if ((helperMethod.IsInternalCall && helperMethod.HasCustomAttribute("System.Runtime", "RuntimeImportAttribute")))
                return ImportRawPInvoke(helperMethod, arguments, forcedReturnType: forcedReturnType);
            else
                return HandleCall(helperMethod, helperMethod.Signature, helperMethod, arguments, helperMethod);
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
            if (entry is LoadExpressionEntry)
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
                    addressValue = StoreTemp(entryIndex, ((ExpressionEntry)entry).RawLLVMValue);
                else
                    addressValue = StoreTemp(entryIndex, entry.ValueForStackKind(entry.Kind, _builder, false));
            }

            return new AddressExpressionEntry(StackValueKind.NativeInt, "address_of", addressValue, entry.Type.MakePointerType());
        }

        private TypeDesc ResolveTypeToken(int token)
        {
            return (TypeDesc)_canonMethodIL.GetObject(token);
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

        private void EmitTrapCall(LLVMBuilderRef builder = default(LLVMBuilderRef))
        {
            if (builder.Pointer == IntPtr.Zero)
                builder = _builder;

            if (TrapFunction.Pointer == IntPtr.Zero)
            {
                TrapFunction = LLVM.AddFunction(Module, "llvm.trap", LLVM.FunctionType(LLVM.VoidType(), Array.Empty<LLVMTypeRef>(), false));
            }
            LLVM.BuildCall(builder, TrapFunction, Array.Empty<LLVMValueRef>(), string.Empty);
            LLVM.BuildUnreachable(builder);
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

        //TOOD refactor with cctor
        public ExpressionEntry OutputCodeForGetThreadStaticBaseForType(LLVMValueRef threadStaticIndex)
        {
            var threadStaticIndexPtr = LLVM.BuildPointerCast(_builder, threadStaticIndex,
                LLVMTypeRef.PointerType(LLVMTypeRef.PointerType(LLVMTypeRef.Int32Type(), 0), 0), "tsiPtr");
            LLVMValueRef typeTlsIndexPtr =
                LLVM.BuildGEP(_builder, threadStaticIndexPtr, new LLVMValueRef[] { BuildConstInt32(1) }, "typeTlsIndexPtr"); // index is the second field after the ptr.

            StackEntry typeManagerSlotEntry = new LoadExpressionEntry(StackValueKind.ValueType, "typeManagerSlot", threadStaticIndexPtr, GetWellKnownType(WellKnownType.Int32));
            StackEntry tlsIndexExpressionEntry = new LoadExpressionEntry(StackValueKind.ValueType, "typeTlsIndex", typeTlsIndexPtr, GetWellKnownType(WellKnownType.Int32));

            var expressionEntry = CallRuntime("Internal.Runtime", _compilation.TypeSystemContext, ThreadStatics,
                "GetThreadStaticBaseForType", new StackEntry[]
                {
                    typeManagerSlotEntry, 
                    tlsIndexExpressionEntry
                });
            return expressionEntry;
        }

        private LLVMValueRef RemoveFatOffset(LLVMBuilderRef builder, LLVMValueRef fatFunctionRef)
        {
            return LLVM.BuildAnd(builder,
                CastIfNecessary(builder, fatFunctionRef, LLVMTypeRef.Int32Type()),
                BuildConstUInt32(~(uint)_compilation.TypeSystemContext.Target.FatFunctionPointerOffset), "minusFatOffset");
        }
    }
}
