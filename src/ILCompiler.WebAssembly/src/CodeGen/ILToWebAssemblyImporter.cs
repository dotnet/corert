// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Internal.TypeSystem;
using ILCompiler;
using LLVMSharp.Interop;
using ILCompiler.Compiler.DependencyAnalysis;
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

        private class ExceptionRegion
        {
            public ILExceptionRegion ILRegion;
        }

        ArrayBuilder<object> _dependencies = new ArrayBuilder<object>();
        public IEnumerable<object> GetDependencies()
        {
            return _dependencies.ToArray();
        }

        public LLVMModuleRef Module { get; }
        public static LLVMContextRef Context { get; private set; }
        private static Dictionary<TypeDesc, LLVMTypeRef> LlvmStructs { get; } = new Dictionary<TypeDesc, LLVMTypeRef>();
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
        private readonly int _pointerSize;
        private readonly byte[] _ilBytes;
        private MethodDebugInformation _debugInformation;
        private LLVMMetadataRef _debugFunction;
        private TypeDesc _constrainedType = null;
        private List<LLVMValueRef> _exceptionFunclets;
        private List<int> _leaveTargets;
        private readonly Dictionary<Tuple<int, IntPtr>, LLVMBasicBlockRef> _landingPads;
        private readonly Dictionary<IntPtr, LLVMBasicBlockRef> _funcletUnreachableBlocks = new Dictionary<IntPtr, LLVMBasicBlockRef>();
        private readonly Dictionary<IntPtr, LLVMBasicBlockRef> _funcletResumeBlocks = new Dictionary<IntPtr, LLVMBasicBlockRef>();
        private readonly EHInfoNode _ehInfoNode;
        private AddressCacheContext _funcletAddrCacheCtx;

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
            public LLVMBasicBlockRef LastInternalBlock;
            public readonly List<LLVMBasicBlockRef> LLVMBlocks = new List<LLVMBasicBlockRef>(1);
            public LLVMBasicBlockRef LastBlock => LastInternalBlock.Handle == IntPtr.Zero ? Block : LastInternalBlock;
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
            if (ilExceptionRegions.Length != 0)
            {
                _exceptionFunclets = new List<LLVMValueRef>(_exceptionRegions.Length);
                _landingPads = new Dictionary<Tuple<int, IntPtr>, LLVMBasicBlockRef>();
                _ehInfoNode = new EHInfoNode(_mangledName);
            }
            int curRegion = 0;
            foreach (ILExceptionRegion region in ilExceptionRegions.OrderBy(region => region.TryOffset)
                .ThenByDescending(region => region.TryLength)  // outer regions with the same try offset as inner region first - they will have longer lengths, // WASMTODO, except maybe an inner of try {} catch {} which could still be a problem
                .ThenBy(region => region.HandlerOffset))
            {
                _exceptionRegions[curRegion++] = new ExceptionRegion
                                                 {
                                                     ILRegion = region
                                                 };
            }

            _llvmFunction = GetOrCreateLLVMFunction(mangledName, method.Signature, method.RequiresInstArg());
            _currentFunclet = _llvmFunction;
            _pointerSize = compilation.NodeFactory.Target.PointerSize;

            _debugInformation = _compilation.GetDebugInfo(_methodIL);

            Context = Module.Context;
            _builder = Context.CreateBuilder();
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
                LLVMBasicBlockRef trapBlock = _llvmFunction.AppendBasicBlock("Trap");

                // Change the function body to trap
                foreach (BasicBlock block in _basicBlocks)
                {
                    if (block != null)
                    {
                        foreach (LLVMBasicBlockRef llvmBlock in block.LLVMBlocks)
                        {
                            if (llvmBlock.Handle != IntPtr.Zero)
                            {
                                llvmBlock.AsValue().ReplaceAllUsesWith(trapBlock.AsValue());
                                llvmBlock.RemoveFromParent();
                            }
                        }
                    }
                }

                if (_exceptionFunclets != null)
                {
                    foreach (LLVMValueRef funclet in _exceptionFunclets)
                    {
                        funclet.DeleteFunction();
                    }
                }

                _builder.PositionAtEnd(trapBlock);
                EmitTrapCall();
                throw;
            }
            finally
            {
                // Generate thunk for runtime exports
                if ((_method.IsRuntimeExport || _method.IsUnmanagedCallersOnly) && _method is EcmaMethod)  // TODO: Reverse delegate invokes probably need something here, but what would be the export name?
                {
                    EcmaMethod ecmaMethod = ((EcmaMethod)_method);
                    string exportName = ecmaMethod.IsRuntimeExport ? ecmaMethod.GetRuntimeExportName() : ecmaMethod.GetUnmanagedCallersOnlyExportName();
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
            // Avoid appearing to be in any exception regions
            _currentOffset = -1;

            LLVMBuilderRef prologBuilder = Context.CreateBuilder();
            LLVMBasicBlockRef prologBlock = _llvmFunction.AppendBasicBlock("Prolog");
            prologBuilder.PositionAtEnd(prologBlock);
            // Copy arguments onto the stack to allow
            // them to be referenced by address
            int thisOffset = 0;
            if (!_signature.IsStatic)
            {
                thisOffset = 1;
            }
            _funcletAddrCacheCtx = new AddressCacheContext
            {
                // sparsely populated, args on LLVM stack not in here
                ArgAddresses = new LLVMValueRef[thisOffset + _signature.Length],
                LocalAddresses = new LLVMValueRef[_locals.Length],
                TempAddresses = new List<LLVMValueRef>(),
                PrologBuilder = prologBuilder
            };
            // Allocate slots to store exception being dispatched and generic context if present
            if (_exceptionRegions.Length > 0)
            {
                _spilledExpressions.Add(new SpilledExpressionEntry(StackValueKind.ObjRef, "ExceptionSlot", GetWellKnownType(WellKnownType.Object), 0, this));
                // clear any uncovered object references for GC.Collect
                ImportCallMemset(LoadVarAddress(0, LocalVarKind.Temp, out TypeDesc _, prologBuilder), 0, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)_pointerSize), prologBuilder);
                // and a slot for the generic context if present
                if (FuncletsRequireHiddenContext())
                {
                    var genCtx = new SpilledExpressionEntry(StackValueKind.ObjRef, "GenericCtxSlot", GetWellKnownType(WellKnownType.IntPtr), 1, this);
                    _spilledExpressions.Add(genCtx);
                    // put the generic context in the slot for reference by funclets
                    var addressValue = CastIfNecessary(prologBuilder, LoadVarAddress(genCtx.LocalIndex, LocalVarKind.Temp, _funcletAddrCacheCtx, out TypeDesc unused, prologBuilder), 
                        LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), 0));
                    prologBuilder.BuildStore(_llvmFunction.GetParam(GetHiddenContextParamNo()), addressValue);
                }
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
                    Debug.Assert(signatureIndex < _llvmFunction.ParamsCount);
                    LLVMValueRef argValue = _llvmFunction.GetParam((uint)signatureIndex);

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

                        storageAddr = prologBuilder.BuildAlloca(GetLLVMTypeForTypeDesc(_signature[i]), argName);
                        _argSlots[i] = storageAddr;
                    }
                    else
                    {
                        storageAddr = CastIfNecessary(prologBuilder, LoadVarAddress(argOffset, LocalVarKind.Argument, _funcletAddrCacheCtx, out _, prologBuilder), LLVMTypeRef.CreatePointer(argValue.TypeOf, 0));
                    }

                    prologBuilder.BuildStore(argValue, storageAddr);
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

                    LLVMValueRef localStackSlot = prologBuilder.BuildAlloca(GetLLVMTypeForTypeDesc(_locals[i].Type), localName);
                    _localSlots[i] = localStackSlot;
                }
            }

            if (_methodIL.IsInitLocals)
            {
                for (int i = 0; i < _locals.Length; i++)
                {
                    LLVMValueRef localAddr = LoadVarAddress(i, LocalVarKind.Local, _funcletAddrCacheCtx, out TypeDesc localType, prologBuilder);
                    if (CanStoreVariableOnStack(localType))
                    {
                        LLVMTypeRef llvmType = GetLLVMTypeForTypeDesc(localType);
                        LLVMTypeKind typeKind = llvmType.Kind;
                        switch (typeKind)
                        {
                            case LLVMTypeKind.LLVMIntegerTypeKind:
                                if (llvmType.Equals(LLVMTypeRef.Int1))
                                {
                                    prologBuilder.BuildStore(BuildConstInt1(0), localAddr);
                                }
                                else if (llvmType.Equals(LLVMTypeRef.Int8))
                                {
                                    prologBuilder.BuildStore(BuildConstInt8(0), localAddr);
                                }
                                else if (llvmType.Equals(LLVMTypeRef.Int16))
                                {
                                    prologBuilder.BuildStore(BuildConstInt16(0), localAddr);
                                }
                                else if (llvmType.Equals(LLVMTypeRef.Int32))
                                {
                                    prologBuilder.BuildStore(BuildConstInt32(0), localAddr);
                                }
                                else if (llvmType.Equals(LLVMTypeRef.Int64))
                                {
                                    prologBuilder.BuildStore(BuildConstInt64(0), localAddr);
                                }
                                else
                                {
                                    throw new Exception("Unexpected LLVM int type");
                                }
                                break;

                            case LLVMTypeKind.LLVMPointerTypeKind:
                                prologBuilder.BuildStore(LLVMValueRef.CreateConstPointerNull(llvmType), localAddr);
                                break;

                            default:
                                LLVMValueRef castAddr = prologBuilder.BuildPointerCast(localAddr, LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), $"cast_local{i}_");
                                ImportCallMemset(castAddr, 0, localType.GetElementSize().AsInt, prologBuilder);
                                break;
                        }
                    }
                    else
                    {
                        LLVMValueRef castAddr = prologBuilder.BuildPointerCast(localAddr, LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), $"cast_local{i}_");
                        ImportCallMemset(castAddr, 0, localType.GetElementSize().AsInt, prologBuilder);
                    }
                }
            }

            if (_thisType is MetadataType metadataType && !metadataType.IsBeforeFieldInit
                && (!_method.IsStaticConstructor && _method.Signature.IsStatic || _method.IsConstructor || (_thisType.IsValueType && !_method.Signature.IsStatic))
                && _compilation.HasLazyStaticConstructor(metadataType))
            {
                if(_debugInformation != null)
                {
                    // set the location for the call to EnsureClassConstructorRun
                    // LLVM can't process empty string file names
                    var curSequencePoint = GetSequencePoint(0 /* offset for the prolog? */);
                    if (!string.IsNullOrWhiteSpace(curSequencePoint.Document))
                    {
                        DebugMetadata debugMetadata = GetOrCreateDebugMetadata(curSequencePoint);
                        LLVMMetadataRef currentLine = CreateDebugFunctionAndDiLocation(debugMetadata, curSequencePoint);
                        prologBuilder.CurrentDebugLocation = Context.MetadataAsValue(currentLine);
                    }
                }
                TriggerCctor(metadataType, prologBuilder);
            }

            LLVMBasicBlockRef block0 = GetLLVMBasicBlockForBlock(_basicBlocks[0]);
            prologBuilder.PositionBefore(prologBuilder.BuildBr(block0));
            _builder.PositionAtEnd(block0);
        }

        private LLVMValueRef CreateLLVMFunction(string mangledName, MethodSignature signature, bool hasHiddenParameter)
        {
            return Module.AddFunction(mangledName, GetLLVMSignatureForMethod(signature, hasHiddenParameter));
        }

        private LLVMValueRef GetOrCreateLLVMFunction(string mangledName, MethodSignature signature, bool hasHiddenParam)
        {
            LLVMValueRef llvmFunction = Module.GetNamedFunction(mangledName);

            if(llvmFunction.Handle == IntPtr.Zero)
            {
                return CreateLLVMFunction(mangledName, signature, hasHiddenParam);
            }
            return llvmFunction;
        }

        private LLVMValueRef GetOrCreateLLVMFunction(string mangledName, LLVMTypeRef functionType)
        {
            LLVMValueRef llvmFunction = Module.GetNamedFunction(mangledName);

            if (llvmFunction.Handle == IntPtr.Zero)
            {
                return Module.AddFunction(mangledName, functionType);
            }
            return llvmFunction;
        }

        /// <summary>
        /// Gets or creates an LLVM function for an exception handling funclet
        /// </summary>
        private LLVMValueRef GetOrCreateFunclet(ILExceptionRegionKind kind, int handlerOffset)
        {
            string funcletName = _mangledName + "$" + kind.ToString() + handlerOffset.ToString("X");
            LLVMValueRef funclet = Module.GetNamedFunction(funcletName);
            if (funclet.Handle == IntPtr.Zero)
            {
                LLVMTypeRef returnType;
                if (kind == ILExceptionRegionKind.Filter || kind == ILExceptionRegionKind.Catch)
                {
                    returnType = LLVMTypeRef.Int32;
                }
                else
                {
                    returnType = LLVMTypeRef.Void;
                }
                var funcletArgs = new LLVMTypeRef[] { LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0) }; 
                LLVMTypeRef universalFuncletSignature = LLVMTypeRef.CreateFunction(returnType, funcletArgs, false);
                funclet = Module.AddFunction(funcletName, universalFuncletSignature);

                _exceptionFunclets.Add(funclet);
            }

            return funclet;
        }

        private void ImportCallMemset(LLVMValueRef targetPointer, byte value, int length, LLVMBuilderRef builder)
        {
            LLVMValueRef objectSizeValue = BuildConstInt32(length);
            ImportCallMemset(targetPointer, value, objectSizeValue, builder);
        }

        private void ImportCallMemset(LLVMValueRef targetPointer, byte value, LLVMValueRef length, LLVMBuilderRef builder = default(LLVMBuilderRef))
        {
            if (builder.Handle == IntPtr.Zero) builder = _builder;
            var memsetSignature = LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, new LLVMTypeRef[] { LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), LLVMTypeRef.Int8, LLVMTypeRef.Int32, LLVMTypeRef.Int1 }, false);
            builder.BuildCall(GetOrCreateLLVMFunction("llvm.memset.p0i8.i32", memsetSignature), new LLVMValueRef[] { targetPointer, BuildConstInt8(value), length, BuildConstInt1(0) }, String.Empty);
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
                            llvmValue = _builder.BuildIntCast(llvmValue, LLVMTypeRef.Int32, "");
                        }
                    }
                    break;

                case StackValueKind.Int64:
                    {
                        if (!type.IsWellKnownType(WellKnownType.Int64)
                            && !(type.IsWellKnownType(WellKnownType.UInt64)))
                        {
                            llvmValue = _builder.BuildIntCast(llvmValue, LLVMTypeRef.Int64, "");
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
            if (block.Block.Handle == IntPtr.Zero)
            {
                LLVMValueRef blockFunclet = GetFuncletForBlock(block);

                block.Block = blockFunclet.AppendBasicBlock("Block" + block.StartOffset.ToString("X"));
                block.LLVMBlocks.Add(block.Block);
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
                ILExceptionRegionKind kind;
                int funcletOffset;
                if (ehRegion.ILRegion.Kind == ILExceptionRegionKind.Filter && 
                    IsOffsetContained(block.StartOffset, ehRegion.ILRegion.FilterOffset, ehRegion.ILRegion.HandlerOffset - ehRegion.ILRegion.FilterOffset))
                {
                    kind = ILExceptionRegionKind.Filter;
                    funcletOffset = ehRegion.ILRegion.FilterOffset;
                }
                else
                {
                    kind = ehRegion.ILRegion.Kind;
                    if (kind == ILExceptionRegionKind.Filter)
                    {
                        kind = ILExceptionRegionKind.Catch;
                    }
                    funcletOffset = ehRegion.ILRegion.HandlerOffset;
                }

                blockFunclet = GetOrCreateFunclet(kind, funcletOffset);
            }
            else
            {
                blockFunclet = _llvmFunction;
            }

            return blockFunclet;
        }

        /// <summary>
        /// Returns the most nested try region the current offset is in
        /// </summary>
        private ExceptionRegion GetCurrentTryRegion()
        {
            return GetTryRegion(_currentOffset);
        }

        private ExceptionRegion GetTryRegion(int offset)
        {
            // Iterate backwards to find the most nested region
            for (int i = _exceptionRegions.Length - 1; i >= 0; i--)
            {
                ILExceptionRegion region = _exceptionRegions[i].ILRegion;
                if (IsOffsetContained(offset - 1, region.TryOffset, region.TryLength))
                {
                    return _exceptionRegions[i];
                }
            }

            return null;
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
                ExceptionRegion exceptionRegion = _exceptionRegions[i];
                if (IsOffsetContained(offset, exceptionRegion.ILRegion.HandlerOffset, exceptionRegion.ILRegion.HandlerLength) ||
                    (exceptionRegion.ILRegion.Kind == ILExceptionRegionKind.Filter && IsOffsetContained(offset, exceptionRegion.ILRegion.FilterOffset, exceptionRegion.ILRegion.HandlerOffset - exceptionRegion.ILRegion.FilterOffset)))
                {
                    return _exceptionRegions[i];
                }
            }

            return null;
        }

        private ILExceptionRegionKind? GetHandlerRegionKind(BasicBlock block)
        {
            ExceptionRegion ehRegion = GetHandlerRegion(block.StartOffset);
            return ehRegion?.ILRegion.Kind;
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
            ILExceptionRegionKind? handlerKind = GetHandlerRegionKind(basicBlock);
            if (basicBlock.FilterStart || handlerKind == ILExceptionRegionKind.Finally)
            {
                // need to pad out the spilled slots in case the filter or finally tries to allocate some temp variables on the shadow stack.  As some spaces are used in the call to FindFirstPassHandlerWasm/InvokeSecondPassWasm
                // and inside FindFirstPassHandlerWasm/InvokeSecondPassWasm we need to leave space for them.
                // An alternative approach could be to calculate this space in RhpCallFilterFunclet/RhpCallFinallyFunclet and pass it through
                NewSpillSlot(new ExpressionEntry(StackValueKind.Int32, "infoIteratorEntry", LLVMValueRef.CreateConstNull(LLVMTypeRef.Int32))); //3 ref and out params
                NewSpillSlot(new ExpressionEntry(StackValueKind.Int32, "tryRegionIdxEntry", LLVMValueRef.CreateConstNull(LLVMTypeRef.Int32)));
                NewSpillSlot(new ExpressionEntry(StackValueKind.Int32, "handlerFuncPtrEntry", LLVMValueRef.CreateConstNull(LLVMTypeRef.Int32)));
                NewSpillSlot(new ExpressionEntry(StackValueKind.Int32, "padding", LLVMValueRef.CreateConstNull(LLVMTypeRef.Int32))); // 8 bytes enough to cover the temps used in FindFirstPassHandlerWasm or InvokeSecondPassWasm
                NewSpillSlot(new ExpressionEntry(StackValueKind.Int32, "padding", LLVMValueRef.CreateConstNull(LLVMTypeRef.Int32)));
            }

            _curBasicBlock = GetLLVMBasicBlockForBlock(basicBlock);
            _currentFunclet = GetFuncletForBlock(basicBlock);

            // Push an exception object for catch and filter
            if (basicBlock.HandlerStart || basicBlock.FilterStart)
            {
                _funcletAddrCacheCtx = null;
                foreach (ExceptionRegion ehRegion in _exceptionRegions)
                {
                    if (ehRegion.ILRegion.HandlerOffset == basicBlock.StartOffset ||
                        ehRegion.ILRegion.FilterOffset == basicBlock.StartOffset)
                    {
                        if (ehRegion.ILRegion.Kind != ILExceptionRegionKind.Finally)
                        {
                            // todo: should this be converted to the exception's type?
                            _stack.Push(_spilledExpressions[0]);
                        }
                        break;
                    }
                }
            }

            if (basicBlock.TryStart)
            {
                foreach (ExceptionRegion ehRegion in _exceptionRegions)
                {
                    if(ehRegion.ILRegion.TryOffset == basicBlock.StartOffset)
                    {
                        MarkBasicBlock(_basicBlocks[ehRegion.ILRegion.HandlerOffset]);
                        if (ehRegion.ILRegion.Kind == ILExceptionRegionKind.Filter)
                        {
                            MarkBasicBlock(_basicBlocks[ehRegion.ILRegion.FilterOffset]);
                        }
                    }
                }
            }

           _builder.PositionAtEnd(_curBasicBlock);
        }

        private void EndImportingBasicBlock(BasicBlock basicBlock)
        {
            var terminator = basicBlock.LastBlock.Terminator;
            if (terminator.Handle == IntPtr.Zero)
            {
                if (_basicBlocks.Length > _currentOffset)
                {
                    if (_basicBlocks[_currentOffset].StartOffset == 0)
                        throw new InvalidProgramException();
                    MarkBasicBlock(_basicBlocks[_currentOffset]);

                    _builder.BuildBr(GetLLVMBasicBlockForBlock(_basicBlocks[_currentOffset]));
                }
            }
        }

        private void StartImportingInstruction()
        {
            if (_debugInformation != null)
            {
                ILSequencePoint curSequencePoint = GetSequencePoint(_currentOffset);

                // LLVM can't process empty string file names
                if (string.IsNullOrWhiteSpace(curSequencePoint.Document))
                {
                    return;
                }

                DebugMetadata debugMetadata = GetOrCreateDebugMetadata(curSequencePoint);

                LLVMMetadataRef currentLine = CreateDebugFunctionAndDiLocation(debugMetadata, curSequencePoint);
                _builder.CurrentDebugLocation = Context.MetadataAsValue(currentLine);
            }
        }

        LLVMMetadataRef CreateDebugFunctionAndDiLocation(DebugMetadata debugMetadata, ILSequencePoint sequencePoint)
        {
            if (_debugFunction.Handle == IntPtr.Zero)
            {
                LLVMMetadataRef functionMetaType = _compilation.DIBuilder.CreateSubroutineType(debugMetadata.File,
                    ReadOnlySpan<LLVMMetadataRef>.Empty, LLVMDIFlags.LLVMDIFlagZero);

                uint lineNumber = (uint) _debugInformation.GetSequencePoints().FirstOrDefault().LineNumber;
                _debugFunction = _compilation.DIBuilder.CreateFunction(debugMetadata.File, _method.Name, _method.Name,
                    debugMetadata.File,
                    lineNumber, functionMetaType, 1, 1, lineNumber, 0, 0);
                LLVMSharpInterop.DISetSubProgram(_llvmFunction, _debugFunction);
            }
            return Context.CreateDebugLocation((uint)sequencePoint.LineNumber, 0, _debugFunction, default(LLVMMetadataRef));
        }

        ILSequencePoint GetSequencePoint(int offset)
        {
            ILSequencePoint curSequencePoint = default;
            foreach (var sequencePoint in _debugInformation.GetSequencePoints() ?? Enumerable.Empty<ILSequencePoint>())
            {
                if (offset <= sequencePoint.Offset) // take the first sequence point in case we need to make a call to RhNewObject before the first matching sequence point
                {
                    curSequencePoint = sequencePoint;
                    break;
                }
                if (sequencePoint.Offset < offset)
                {
                    curSequencePoint = sequencePoint;
                }
            }
            return curSequencePoint;
        }

        DebugMetadata GetOrCreateDebugMetadata(ILSequencePoint curSequencePoint)
        {
            DebugMetadata debugMetadata;
            if (!_compilation.DebugMetadataMap.TryGetValue(curSequencePoint.Document, out debugMetadata))
            {
                string fullPath = curSequencePoint.Document;
                string fileName = Path.GetFileName(fullPath);
                string directory = Path.GetDirectoryName(fullPath) ?? String.Empty;
                LLVMMetadataRef fileMetadata = _compilation.DIBuilder.CreateFile(fileName, directory);

                // todo: get the right value for isOptimized
                LLVMMetadataRef compileUnitMetadata = _compilation.DIBuilder.CreateCompileUnit(
                    LLVMDWARFSourceLanguage.LLVMDWARFSourceLanguageC,
                    fileMetadata, "ILC", 0 /* Optimized */, String.Empty, 1, String.Empty,
                    LLVMDWARFEmissionKind.LLVMDWARFEmissionFull, 0, 0, 0);
                Module.AddNamedMetadataOperand("llvm.dbg.cu", compileUnitMetadata);

                debugMetadata = new DebugMetadata(fileMetadata, compileUnitMetadata);
                _compilation.DebugMetadataMap[fileName] = debugMetadata;
            }
            return debugMetadata;
        }

        private void EndImportingInstruction()
        {
            // If this was constrained used in a call, it's already been cleared,
            // but if it was on some other instruction, it shoudln't carry forward
            _constrainedType = null;

            // Reset the debug position so it doesn't end up applying to the wrong instructions
            _builder.CurrentDebugLocation = default(LLVMValueRef);
        }

        private void ImportNop()
        {
            EmitDoNothingCall();
        }

        private void ImportBreak()
        {
            if (DebugtrapFunction.Handle == IntPtr.Zero)
            {
                DebugtrapFunction = Module.AddFunction("llvm.debugtrap", LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, Array.Empty<LLVMTypeRef>(), false));
            }
            _builder.BuildCall(DebugtrapFunction, Array.Empty<LLVMValueRef>(), string.Empty);
        }

        private void ImportLoadVar(int index, bool argument)
        {
            LLVMValueRef typedLoadLocation = LoadVarAddress(index, argument ? LocalVarKind.Argument : LocalVarKind.Local, out TypeDesc type);
            PushLoadExpression(GetStackValueKind(type), (argument ? "arg" : "loc") + index + "_", typedLoadLocation, type);
        }

        internal LLVMValueRef LoadTemp(int index, LLVMTypeRef asType)
        {
            LLVMValueRef address = LoadVarAddress(index, LocalVarKind.Temp, out TypeDesc type);
            return _builder.BuildLoad(CastIfNecessary(address, LLVMTypeRef.CreatePointer(asType, 0), $"Temp{index}_"), $"LdTemp{index}_");
        }

        private LLVMValueRef StoreTemp(int index, LLVMValueRef value, string name = null)
        {
            LLVMValueRef address = LoadVarAddress(index, LocalVarKind.Temp, out TypeDesc type);
            _builder.BuildStore(CastToTypeDesc(value, type, name), CastToPointerToTypeDesc(address, type, $"Temp{index}_"));
            return address;
        }

        internal static LLVMValueRef LoadValue(LLVMBuilderRef builder, LLVMValueRef address, TypeDesc sourceType, LLVMTypeRef targetType, bool signExtend, string loadName = null)
        {
            var underlyingSourceType = sourceType.UnderlyingType;
            if (targetType.Kind == LLVMTypeKind.LLVMIntegerTypeKind && underlyingSourceType.IsPrimitive && !underlyingSourceType.IsPointer)
            {
                LLVMValueRef loadValueRef = CastIfNecessaryAndLoad(builder, address, underlyingSourceType, loadName);
                return CastIntValue(builder, loadValueRef, targetType, signExtend);
            }
            else if (targetType.Kind == LLVMTypeKind.LLVMDoubleTypeKind)
            {
                LLVMValueRef loadValueRef = CastIfNecessaryAndLoad(builder, address, underlyingSourceType, loadName);
                return CastDoubleValue(builder, loadValueRef, targetType);
            }
            else
            {
                var typedAddress = CastIfNecessary(builder, address, LLVMTypeRef.CreatePointer(targetType, 0));
                return builder.BuildLoad(typedAddress, loadName ?? "ldvalue");
            }
        }

        private static LLVMValueRef CastIfNecessaryAndLoad(LLVMBuilderRef builder, LLVMValueRef address, TypeDesc sourceTypeDesc, string loadName)
        {
            LLVMTypeRef sourceLLVMType = ILImporter.GetLLVMTypeForTypeDesc(sourceTypeDesc);
            LLVMValueRef typedAddress = CastIfNecessary(builder, address, LLVMTypeRef.CreatePointer(sourceLLVMType, 0));
            return builder.BuildLoad(typedAddress, loadName ?? "ldvalue");
        }

        private static LLVMValueRef CastIntValue(LLVMBuilderRef builder, LLVMValueRef value, LLVMTypeRef type, bool signExtend)
        {
            LLVMTypeKind typeKind = value.TypeOf.Kind;
            if (value.TypeOf.Handle == type.Handle)
            {
                return value;
            }
            else if (typeKind == LLVMTypeKind.LLVMPointerTypeKind)
            {
                return builder.BuildPtrToInt(value, type, "intcast");
            }
            else if (typeKind == LLVMTypeKind.LLVMFloatTypeKind || typeKind == LLVMTypeKind.LLVMDoubleTypeKind)
            {
                if (signExtend)
                {
                    return builder.BuildFPToSI(value, type, "fptosi");
                }
                else
                {
                    return builder.BuildFPToUI(value, type, "fptoui");
                }
            }
            else if (signExtend && type.IntWidth > value.TypeOf.IntWidth)
            {
                return builder.BuildSExtOrBitCast(value, type, "SExtOrBitCast");
            }
            else if (type.IntWidth > value.TypeOf.IntWidth)
            {
                return builder.BuildZExtOrBitCast(value, type, "ZExtOrBitCast");
            }
            else
            {
                Debug.Assert(typeKind == LLVMTypeKind.LLVMIntegerTypeKind);
                return builder.BuildIntCast(value, type, "intcast");
            }
        }

        private static LLVMValueRef CastDoubleValue(LLVMBuilderRef builder, LLVMValueRef value, LLVMTypeRef type)
        {
            if (value.TypeOf.Handle == type.Handle)
            {
                return value;
            }
            Debug.Assert(value.TypeOf.Kind == LLVMTypeKind.LLVMFloatTypeKind);
            return builder.BuildFPExt(value, type, "fpext");
        }

        private LLVMValueRef LoadVarAddress(int index, LocalVarKind kind, out TypeDesc type, LLVMBuilderRef builder = default(LLVMBuilderRef))
        {
            return LoadVarAddress(index, kind, _funcletAddrCacheCtx, out type, builder);
        }

        private LLVMValueRef LoadVarAddress(int index, LocalVarKind kind, AddressCacheContext addressCacheContext, out TypeDesc type, LLVMBuilderRef builder = default(LLVMBuilderRef))
        {
            if (builder.Handle == IntPtr.Zero) builder = _builder;

            int varOffset;
            if (kind == LocalVarKind.Argument)
            {
                int varCountBase = 0;
                if (!_signature.IsStatic)
                {
                    varCountBase = 1;
                }
                if (addressCacheContext != null && addressCacheContext.ArgAddresses[index] != null)
                {
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
                    return addressCacheContext.ArgAddresses[index];
                }
                varOffset = GetArgOffsetAtIndex(index, out int realArgIndex);

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

                // If the argument can be passed as a real argument rather than on the shadow stack,
                // get its address here
                if (realArgIndex != -1)
                {
                    return _argSlots[realArgIndex];
                }
            }
            else if (kind == LocalVarKind.Local)
            {
                type = _locals[index].Type;
                if (addressCacheContext != null && addressCacheContext.LocalAddresses[index] != null)
                {
                    return addressCacheContext.LocalAddresses[index];
                }
                varOffset = GetLocalOffsetAtIndex(index);
                if (varOffset == -1)
                {
                    Debug.Assert(_localSlots[index].Handle != IntPtr.Zero);
                    return _localSlots[index];
                }
                varOffset = varOffset + GetTotalParameterOffset();
            }
            else
            {
                type = _spilledExpressions[index].Type;
                if (addressCacheContext != null && addressCacheContext.TempAddresses.Count > index && addressCacheContext.TempAddresses[index] != null)
                {
                    return addressCacheContext.TempAddresses[index];
                }
                varOffset = GetSpillOffsetAtIndex(index, GetTotalRealLocalOffset()) + GetTotalParameterOffset();
            }

            LLVMValueRef addr;
            if (addressCacheContext != null)
            {
                addr = addressCacheContext.PrologBuilder.BuildGEP(_currentFunclet.GetParam(0),
                    new LLVMValueRef[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (uint)(varOffset), false) },
                    $"{kind}{index}_");
                if (kind == LocalVarKind.Argument)
                {
                    addressCacheContext.ArgAddresses[index] = addr;
                }
                else if (kind == LocalVarKind.Local)
                {
                    addressCacheContext.LocalAddresses[index] = addr;
                }
                else if (kind == LocalVarKind.Temp)
                {
                    if (addressCacheContext.TempAddresses.Count <= index)
                    {
                        var toAdd = index - addressCacheContext.TempAddresses.Count + 1;
                        for (var i = 0; i < toAdd; i++) addressCacheContext.TempAddresses.Add(null);
                    }
                    addressCacheContext.TempAddresses[index] = addr;
                }
            }
            else
            {
                addr = builder.BuildGEP(_currentFunclet.GetParam(0),
                    new LLVMValueRef[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (uint)(varOffset), false) },
                    $"{kind}{index}_");
            }
            return addr;
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
                case TypeFlags.FunctionPointer:
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
            CastingStore(varAddress, toStore, varType, false, $"Variable{index}_");
        }

        private void ImportStoreHelper(LLVMValueRef toStore, LLVMTypeRef valueType, LLVMValueRef basePtr, uint offset, string name = null, LLVMBuilderRef builder = default(LLVMBuilderRef))
        {
            if (builder.Handle == IntPtr.Zero)
                builder = _builder;

            LLVMValueRef typedToStore = CastIfNecessary(builder, toStore, valueType, name);

            var storeLocation = builder.BuildGEP(basePtr,
                new LLVMValueRef[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, offset, false) },
                String.Empty);
            var typedStoreLocation = CastIfNecessary(builder, storeLocation, LLVMTypeRef.CreatePointer(valueType, 0), "TypedStore" + (name ?? ""));
            builder.BuildStore(typedToStore, typedStoreLocation);
        }

        private LLVMValueRef CastToRawPointer(LLVMValueRef source, string name = null)
        {
            return CastIfNecessary(source, LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), name);
        }

        private LLVMValueRef CastToTypeDesc(LLVMValueRef source, TypeDesc type, string name = null)
        {
            return CastIfNecessary(source, GetLLVMTypeForTypeDesc(type), (name ?? "") + type.ToString());
        }

        private LLVMValueRef CastToPointerToTypeDesc(LLVMValueRef source, TypeDesc type, string name = null)
        {
            return CastIfNecessary(source, LLVMTypeRef.CreatePointer(GetLLVMTypeForTypeDesc(type), 0), (name ?? "") + type.ToString());
        }

        private void CastingStore(LLVMValueRef address, StackEntry value, TypeDesc targetType, bool withGCBarrier, string targetName = null)
        {
            if (withGCBarrier && targetType.IsGCPointer)
            {
                CallRuntime(_method.Context, "InternalCalls", "RhpAssignRef", new StackEntry[]
                {
                    new ExpressionEntry(StackValueKind.Int32, "address", address), value
                });
            }
            else
            {
                var typedStoreLocation = CastToPointerToTypeDesc(address, targetType, targetName);
                var llvmValue = value.ValueAsType(targetType, _builder);
                if (withGCBarrier && IsStruct(targetType))
                {
                    StoreStruct(address, llvmValue, targetType, typedStoreLocation);
                }
                else
                {
                    _builder.BuildStore(llvmValue, typedStoreLocation);
                }
            }
        }

        private static bool IsStruct(TypeDesc typeDesc)
        {
            return typeDesc.IsValueType && !typeDesc.IsPrimitive && !typeDesc.IsEnum;
        }

        private void StoreStruct(LLVMValueRef address, LLVMValueRef llvmValue, TypeDesc targetType, LLVMValueRef typedStoreLocation, bool childStruct = false)
        {
            // TODO: if this is used for anything multithreaded, this foreach and the subsequent BuildStore are susceptible to a race condition 
            foreach (FieldDesc f in targetType.GetFields())
            {
                if (f.IsStatic) continue;
                if (IsStruct(f.FieldType) && llvmValue.TypeOf.IsPackedStruct)
                {
                    LLVMValueRef targetAddress = _builder.BuildGEP(address, new[] { BuildConstInt32(f.Offset.AsInt) });
                    uint index = LLVMSharpInterop.ElementAtOffset(_compilation.TargetData, llvmValue.TypeOf, (ulong)f.Offset.AsInt);
                    LLVMValueRef fieldValue = _builder.BuildExtractValue(llvmValue, index);
                    //recurse into struct
                    StoreStruct(targetAddress, fieldValue, f.FieldType, CastToPointerToTypeDesc(targetAddress, f.FieldType), true);
                }
                else if (f.FieldType.IsGCPointer)
                {
                    LLVMValueRef targetAddress = _builder.BuildGEP(address, new[] {BuildConstInt32(f.Offset.AsInt)});
                    LLVMValueRef fieldValue;
                    if (llvmValue.TypeOf.IsPackedStruct)
                    {
                        uint index = LLVMSharpInterop.ElementAtOffset(_compilation.TargetData, llvmValue.TypeOf, (ulong) f.Offset.AsInt);
                        fieldValue = _builder.BuildExtractValue(llvmValue, index);
                        Debug.Assert(fieldValue.TypeOf.Kind == LLVMTypeKind.LLVMPointerTypeKind, "expected an LLVM pointer type");
                    }
                    else
                    {
                        // single field IL structs are not LLVM structs
                        fieldValue = llvmValue;
                    }
                    CallRuntime(_method.Context, "InternalCalls", "RhpAssignRef",
                        new StackEntry[]
                        {
                            new ExpressionEntry(StackValueKind.Int32, "targetAddress", targetAddress),
                            new ExpressionEntry(StackValueKind.ObjRef, "sourceAddress", fieldValue)
                        });
                }
            }
            if (!childStruct)
            {
                _builder.BuildStore(llvmValue, typedStoreLocation); // just copy all the fields again for simplicity, if all the fields were set using RhpAssignRef then a possible optimisation would be to skip this line
            }
        }

        private LLVMValueRef CastIfNecessary(LLVMValueRef source, LLVMTypeRef valueType, string name = null, bool unsigned = false)
        {
            return CastIfNecessary(_builder, source, valueType, name, unsigned);
        }

        internal static LLVMValueRef CastIfNecessary(LLVMBuilderRef builder, LLVMValueRef source, LLVMTypeRef valueType, string name = null, bool unsigned = false)
        {
            LLVMTypeRef sourceType = source.TypeOf;
            if (sourceType.Handle == valueType.Handle)
                return source;

            LLVMTypeKind toStoreKind = sourceType.Kind;
            LLVMTypeKind valueTypeKind = valueType.Kind;

            LLVMValueRef typedToStore = source;
            if (toStoreKind == LLVMTypeKind.LLVMPointerTypeKind && valueTypeKind == LLVMTypeKind.LLVMPointerTypeKind)
            {
                typedToStore = builder.BuildPointerCast(source, valueType, "CastPtr" + (name ?? ""));
            }
            else if (toStoreKind == LLVMTypeKind.LLVMPointerTypeKind && valueTypeKind == LLVMTypeKind.LLVMIntegerTypeKind)
            {
                typedToStore = builder.BuildPtrToInt(source, valueType, "CastInt" + (name ?? ""));
            }
            else if (toStoreKind == LLVMTypeKind.LLVMIntegerTypeKind && valueTypeKind == LLVMTypeKind.LLVMArrayTypeKind)
            {
                typedToStore = builder.BuildLoad(CastIfNecessary(builder, source, LLVMTypeRef.CreatePointer(valueType, 0), name), "CastArrayLoad" + (name ?? ""));
            }
            else if (toStoreKind == LLVMTypeKind.LLVMPointerTypeKind && valueTypeKind == LLVMTypeKind.LLVMArrayTypeKind)
            {
                typedToStore = builder.BuildLoad(CastIfNecessary(builder, source, LLVMTypeRef.CreatePointer(valueType, 0), name), "CastArrayLoad" + (name ?? ""));
            }
            else if (toStoreKind == LLVMTypeKind.LLVMPointerTypeKind && valueTypeKind != LLVMTypeKind.LLVMIntegerTypeKind)
            {
                throw new NotImplementedException($"trying to cast {toStoreKind} to {valueTypeKind}");
            }
            else if (toStoreKind == LLVMTypeKind.LLVMIntegerTypeKind && valueTypeKind == LLVMTypeKind.LLVMPointerTypeKind)
            {
                typedToStore = builder.BuildIntToPtr(source, valueType, "CastPtr" + (name ?? ""));
            }
            else if (toStoreKind != LLVMTypeKind.LLVMIntegerTypeKind && valueTypeKind == LLVMTypeKind.LLVMPointerTypeKind)
            {
                throw new NotImplementedException($"trying to cast {toStoreKind} to {valueTypeKind}");
            }
            else if (toStoreKind == LLVMTypeKind.LLVMFloatTypeKind && valueTypeKind == LLVMTypeKind.LLVMDoubleTypeKind)
            {
                typedToStore = builder.BuildFPExt(source, valueType, "CastFloatToDouble" + (name ?? ""));
            }

            else if (toStoreKind == LLVMTypeKind.LLVMDoubleTypeKind && valueTypeKind == LLVMTypeKind.LLVMFloatTypeKind)
            {
                typedToStore = builder.BuildFPTrunc(source, valueType, "CastDoubleToFloat" + (name ?? ""));
            }
            else if (toStoreKind != valueTypeKind && toStoreKind != LLVMTypeKind.LLVMIntegerTypeKind && valueTypeKind != LLVMTypeKind.LLVMIntegerTypeKind)
            {
                throw new NotImplementedException($"trying to cast {toStoreKind} to {valueTypeKind}");
            }
            else if (toStoreKind == valueTypeKind && toStoreKind == LLVMTypeKind.LLVMIntegerTypeKind)
            {
                Debug.Assert(toStoreKind != LLVMTypeKind.LLVMPointerTypeKind && valueTypeKind != LLVMTypeKind.LLVMPointerTypeKind);
                // when extending unsigned ints do fill left with 0s, zext
                typedToStore = unsigned && sourceType.IntWidth < valueType.IntWidth
                    ? builder.BuildZExt(source, valueType, "CastZInt" + (name ?? ""))
                    : builder.BuildIntCast(source, valueType, "CastInt" + (name ?? ""));
            }
            else if (toStoreKind == LLVMTypeKind.LLVMIntegerTypeKind && (valueTypeKind == LLVMTypeKind.LLVMDoubleTypeKind || valueTypeKind == LLVMTypeKind.LLVMFloatTypeKind))
            {
                //TODO: keep track of the TypeDesc so we can call BuildUIToFP when the integer is unsigned
                typedToStore = builder.BuildSIToFP(source, valueType, "CastSIToFloat" + (name ?? ""));
            }
            else if ((toStoreKind == LLVMTypeKind.LLVMDoubleTypeKind || toStoreKind == LLVMTypeKind.LLVMFloatTypeKind) &&
                valueTypeKind == LLVMTypeKind.LLVMIntegerTypeKind)
            {
                //TODO: keep track of the TypeDesc so we can call BuildFPToUI when the integer is unsigned
                typedToStore = builder.BuildFPToSI(source, valueType, "CastFloatSI" + (name ?? ""));
            }

            return typedToStore;
        }

        internal static LLVMTypeRef GetLLVMTypeForTypeDesc(TypeDesc type)
        {
            switch (type.Category)
            {
                case TypeFlags.Boolean:
                    return LLVMTypeRef.Int1;

                case TypeFlags.SByte:
                case TypeFlags.Byte:
                    return LLVMTypeRef.Int8;

                case TypeFlags.Int16:
                case TypeFlags.UInt16:
                case TypeFlags.Char:
                    return LLVMTypeRef.Int16;

                case TypeFlags.Int32:
                case TypeFlags.UInt32:
                    return LLVMTypeRef.Int32;
                case TypeFlags.IntPtr:
                case TypeFlags.UIntPtr:
                case TypeFlags.Array:
                case TypeFlags.SzArray:
                case TypeFlags.ByRef:
                case TypeFlags.Class:
                case TypeFlags.Interface:
                    return LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);

                case TypeFlags.Pointer:
                    return LLVMTypeRef.CreatePointer(type.GetParameterType().IsVoid ? LLVMTypeRef.Int8 : GetLLVMTypeForTypeDesc(type.GetParameterType()), 0);
                case TypeFlags.FunctionPointer:
                    return LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);

                case TypeFlags.Int64:
                case TypeFlags.UInt64:
                    return LLVMTypeRef.Int64;

                case TypeFlags.Single:
                    return LLVMTypeRef.Float;

                case TypeFlags.Double:
                    return LLVMTypeRef.Double;

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
                                    llvmStructType = LLVMTypeRef.Int8;
                                    break;
                                case 2:
                                    if (structAlignment == 2)
                                    {
                                        llvmStructType = LLVMTypeRef.Int16;
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
                                            llvmStructType = LLVMTypeRef.Float;
                                        }
                                        else
                                        {
                                            llvmStructType = LLVMTypeRef.Int32;
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
                                            llvmStructType = LLVMTypeRef.Double;
                                        }
                                        else
                                        {
                                            llvmStructType = LLVMTypeRef.Int64;
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
                                    llvmStructType = Context.CreateNamedStruct(type.ToString());
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

                                    llvmStructType.StructSetBody(llvmFields.ToArray(), true);
                                    break;
                            }

                            LlvmStructs[type] = llvmStructType;
                        }
                        return llvmStructType;
                    }

                case TypeFlags.Enum:
                    return GetLLVMTypeForTypeDesc(type.UnderlyingType);

                case TypeFlags.Void:
                    return LLVMTypeRef.Void;

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
                llvmFields.Add(LLVMTypeRef.Int32);
            }
            for (int i = 0; i < numBytes; i++)
            {
                llvmFields.Add(LLVMTypeRef.Int8);
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

        private int GetArgOffsetAtIndex(int index, out int realArgIndex)
        {
            realArgIndex = -1;
            int offset;
            int thisSize = 0;
            if (!_signature.IsStatic)
            {
                thisSize = _thisType.IsValueType ? _thisType.Context.Target.PointerSize : _thisType.GetElementSize().AsInt.AlignUp(_pointerSize);
                if (index == 0)
                {
                    return 0;
                }
                else
                {
                    index--;
                }
            }

            var argType = _signature[index];
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
            return offset;
        }

        private int GetLocalOffsetAtIndex(int index)
        {
            LocalVariableDefinition local = _locals[index];
            int offset;
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
            return offset;
        }

        private int GetSpillOffsetAtIndex(int index, int offset)
        {
            SpilledExpressionEntry spill = _spilledExpressions[index];

            for (int i = 0; i < index; i++)
            {
                offset = PadNextOffset(_spilledExpressions[i].Type, offset);
            }
            offset = PadOffset(spill.Type, offset);
            return offset;
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

            LLVMValueRef typeRef;
            if (type.IsRuntimeDeterminedSubtype)
            {
                typeRef = CallGenericHelper(ReadyToRunHelperId.TypeHandleForCasting, type);
            }
            else
            {
                ISymbolNode lookup = _compilation.ComputeConstantLookup(ReadyToRunHelperId.TypeHandleForCasting, type);
                _dependencies.Add(lookup);
                typeRef = LoadAddressOfSymbolNode(lookup);
            }

            _stack.Push(CallRuntime(_compilation.TypeSystemContext, TypeCast, function,
                new StackEntry[]
                {
                    new ExpressionEntry(StackValueKind.ValueType, "eeType", typeRef, GetWellKnownType(WellKnownType.IntPtr)),
                    _stack.Pop()
                }, GetWellKnownType(WellKnownType.Object)));
        }

        LLVMValueRef CallGenericHelper(ReadyToRunHelperId helperId, object helperArg)
        {
            _dependencies.Add(GetGenericLookupHelperAndAddReference(helperId, helperArg, out LLVMValueRef helper));
            return _builder.BuildCall(helper, new LLVMValueRef[]
            {
                GetShadowStack(),
                GetGenericContext()
            }, "getHelper");
        }

        private void ImportLoadNull()
        {
            _stack.Push(new ExpressionEntry(StackValueKind.ObjRef, "null", LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0, false)));
        }

        private void ImportReturn()
        {
            if (_signature.ReturnType.IsVoid)
            {
                _builder.BuildRetVoid();
                return;
            }

            StackEntry retVal = _stack.Pop();
            LLVMTypeRef valueType = GetLLVMTypeForTypeDesc(_signature.ReturnType);
            LLVMValueRef castValue = retVal.ValueAsType(valueType, _builder);

            if (NeedsReturnStackSlot(_signature))
            {
                var retParam = _llvmFunction.GetParam(1);
                ImportStoreHelper(castValue, valueType, retParam, 0);
                _builder.BuildRetVoid();
            }
            else
            {
                _builder.BuildRet(castValue);
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

            // Hard coded InternalCall mappings for mono interoperability
            if (callee.IsInternalCall)
            {
                var metadataType = callee.OwningType as MetadataType;
                // See https://github.com/dotnet/runtime/blob/9ba9a300a08170c8170ea52981810f41fad68cf0/src/mono/wasm/runtime/driver.c#L400-L407
                // Mono have these InternalCall methods in different namespaces but just mapping them to System.Private.WebAssembly.
                if (metadataType != null && (metadataType.Namespace == "WebAssembly.JSInterop" && metadataType.Name == "InternalCalls" || metadataType.Namespace == "WebAssembly" && metadataType.Name == "Runtime"))
                {
                    var coreRtJsInternalCallsType = _compilation.TypeSystemContext
                        .GetModuleForSimpleName("System.Private.WebAssembly")
                        .GetKnownType("System.Private.WebAssembly", "InternalCalls");
                    callee = coreRtJsInternalCallsType.GetMethod(callee.Name, callee.Signature);
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
                    LLVMValueRef dimensions = _builder.BuildArrayAlloca(LLVMTypeRef.Int32, BuildConstInt32(paramCnt), "newobj_array_pdims_" + _currentOffset);
                    for (int i = paramCnt - 1; i >= 0; --i)
                    {
                        _builder.BuildStore(_stack.Pop().ValueAsInt32(_builder, true),
                            _builder.BuildGEP(dimensions, new LLVMValueRef[] { BuildConstInt32(i) }, "pdims_ptr"));
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
                    PushNonNull(HandleCall(helperMethod, helperMethod.Signature, helperMethod, arguments, runtimeDeterminedMethod, forcedReturnType: newType).Item1);
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

                        if (runtimeDeterminedRetType.IsRuntimeDeterminedSubtype)
                        {
                            typeToAlloc = _compilation.ConvertToCanonFormIfNecessary(runtimeDeterminedRetType, CanonicalFormKind.Specific);
                            var typeRef = CallGenericHelper(ReadyToRunHelperId.TypeHandle, typeToAlloc);
                            newObjResult = AllocateObject(new ExpressionEntry(StackValueKind.ValueType, "eeType", typeRef, GetWellKnownType(WellKnownType.IntPtr)));
                        }
                        else
                        {
                            typeToAlloc = callee.OwningType;
                            MetadataType metadataType = (MetadataType)typeToAlloc;
                            newObjResult = AllocateObject(new LoadExpressionEntry(StackValueKind.ValueType, "eeType", GetEETypePointerForTypeDesc(metadataType, true), GetWellKnownType(WellKnownType.IntPtr)), typeToAlloc);
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
                    LLVMValueRef thisAddr = _builder.BuildGEP(shadowStack, new LLVMValueRef[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)curOffset, false) }, "thisLoc");
                    LLVMValueRef llvmValueRefForThis = thisEntry.ValueAsType(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), _builder);
                    _builder.BuildStore(llvmValueRefForThis, CastIfNecessary(_builder, thisAddr, LLVMTypeRef.CreatePointer(llvmTypeRefForThis, 0), "thisCast"));
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
                            LLVMValueRef llvmValueRefForArg = argStackEntry.ValueAsType(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), _builder);
                            curOffset = PadOffset(argTypeDesc, curOffset);
                            LLVMValueRef argAddr = _builder.BuildGEP(shadowStack, new LLVMValueRef[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)curOffset, false) }, "arg" + i);
                            _builder.BuildStore(llvmValueRefForArg, CastIfNecessary(_builder, argAddr, LLVMTypeRef.CreatePointer(llvmTypeRefForArg, 0), $"parameter{i}_"));
                            curOffset = PadNextOffset(argTypeDesc, curOffset);
                        }
                    }

                    GetGenericLookupHelperAndAddReference(ReadyToRunHelperId.DelegateCtor, delegateInfo, out helper,
                        additionalTypes);
                    _builder.BuildCall(helper, helperParams.ToArray(), string.Empty);
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
                        var toInt = _builder.BuildPtrToInt(funcRef, LLVMTypeRef.Int32, "toInt");
                        var withOffset = _builder.BuildOr(toInt, BuildConstUInt32((uint)_compilation.TypeSystemContext.Target.FatFunctionPointerOffset), "withOffset");
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

            TypeDesc owningType = callee.OwningType;
            bool delegateInvoke = owningType.IsDelegate && callee.Name == "Invoke";
            // Sealed methods must not be called virtually due to sealed vTables, so call them directly, but not delegate Invoke
            if ((canonMethod.IsFinal || canonMethod.OwningType.IsSealed()) && !delegateInvoke)
            {
                if (!_compilation.NodeFactory.TypeSystemContext.IsSpecialUnboxingThunkTargetMethod(canonMethod))
                {
                    hasHiddenParam = canonMethod.RequiresInstArg() || canonMethod.IsArrayAddressMethod();
                }
                AddMethodReference(canonMethod);
                string physicalName = _compilation.NodeFactory.MethodEntrypoint(canonMethod).GetMangledName(_compilation.NameMangler);
                return GetOrCreateLLVMFunction(physicalName, canonMethod.Signature, hasHiddenParam);
            }

            LLVMValueRef thisRef = default;
            if (thisPointer != null)
            {
                thisRef = thisPointer.ValueAsType(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), _builder);
                ThrowIfNull(thisRef);
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
                return GetCallableVirtualMethod(thisRef, callee, runtimeDeterminedMethod);
            }

            hasHiddenParam = canonMethod.RequiresInstArg();
            AddMethodReference(canonMethod);
            string canonMethodName = _compilation.NodeFactory.MethodEntrypoint(canonMethod).GetMangledName(_compilation.NameMangler);
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
            return _builder.BuildLoad(slot, $"{callee.Name}_slot");
        }

        private LLVMValueRef GetCallableVirtualMethod(LLVMValueRef thisPointer, MethodDesc callee, MethodDesc runtimeDeterminedMethod)
        {
            Debug.Assert(runtimeDeterminedMethod.IsVirtual);

            LLVMValueRef slot = GetOrCreateMethodSlot(runtimeDeterminedMethod, callee);

            LLVMTypeRef llvmSignature = GetLLVMSignatureForMethod(runtimeDeterminedMethod.Signature, false);
            LLVMValueRef functionPtr;
            ThrowIfNull(thisPointer);
            if (runtimeDeterminedMethod.OwningType.IsInterface)
            {
                ExpressionEntry interfaceEEType;
                ExpressionEntry eeTypeExpression;
                if (runtimeDeterminedMethod.OwningType.IsRuntimeDeterminedSubtype)
                {
                    //TODO interfaceEEType can be refactored out
                    eeTypeExpression = CallRuntime("System", _compilation.TypeSystemContext, "Object", "get_EEType",
                        new[] { new ExpressionEntry(StackValueKind.ObjRef, "thisPointer", thisPointer) });
                    interfaceEEType = new ExpressionEntry(StackValueKind.ValueType, "interfaceEEType", CallGenericHelper(ReadyToRunHelperId.TypeHandle, runtimeDeterminedMethod.OwningType), GetWellKnownType(WellKnownType.IntPtr));
                }
                else
                {
                    interfaceEEType = new LoadExpressionEntry(StackValueKind.ValueType, "interfaceEEType", GetEETypePointerForTypeDesc(runtimeDeterminedMethod.OwningType, true), GetWellKnownType(WellKnownType.IntPtr));
                    eeTypeExpression = new LoadExpressionEntry(StackValueKind.ValueType, "eeType", thisPointer, GetWellKnownType(WellKnownType.IntPtr));
                }

                var targetEntry = CallRuntime(_compilation.TypeSystemContext, DispatchResolve, "FindInterfaceMethodImplementationTarget", new StackEntry[] { eeTypeExpression, interfaceEEType, new ExpressionEntry(StackValueKind.Int32, "slot", slot, GetWellKnownType(WellKnownType.UInt16)) });
                functionPtr = targetEntry.ValueAsType(LLVMTypeRef.CreatePointer(llvmSignature, 0), _builder);
            }
            else
            {
                var rawObjectPtr = CastIfNecessary(thisPointer, LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(llvmSignature, 0), 0), 0), "this");
                var eeType = _builder.BuildLoad(rawObjectPtr, "ldEEType");
                var slotPtr = _builder.BuildGEP(eeType, new LLVMValueRef[] { slot }, "__getslot__");
                functionPtr = _builder.BuildLoad(slotPtr, "ld__getslot__");
            }

            return functionPtr;
        }

        private LLVMValueRef GetCallableGenericVirtualMethod(StackEntry objectPtr, MethodDesc canonMethod, MethodDesc callee, MethodDesc runtimeDeterminedMethod, out LLVMValueRef dictPtrPtrStore,
            out LLVMValueRef slotRef)
        {
            // this will only have a non-zero pointer the the GVM ptr is fat.
            dictPtrPtrStore = _builder.BuildAlloca(LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), 0), 0),
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
                runtimeMethodHandle = _builder.BuildCall(helper, new LLVMValueRef[]
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
            slotRef = gvmPtr.ValueAsType(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), _builder);

            var fatBranch = _currentFunclet.AppendBasicBlock("then");
            var notFatBranch = _currentFunclet.AppendBasicBlock("else");
            var endifBlock = _currentFunclet.AppendBasicBlock("endif");
            // if
            var andResRef = _builder.BuildAnd(CastIfNecessary(_builder, slotRef, LLVMTypeRef.Int32), LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)_compilation.TypeSystemContext.Target.FatFunctionPointerOffset, false), "andPtrOffset");
            var eqz = _builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, andResRef, BuildConstInt32(0), "eqz");
            _builder.BuildCondBr(eqz, notFatBranch, fatBranch);

            // fat
            _builder.PositionAtEnd(fatBranch);
            var gep = RemoveFatOffset(_builder, slotRef);
            var loadFuncPtr = _builder.BuildLoad(CastIfNecessary(_builder, gep, LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), 0)),
                "loadFuncPtr");
            var dictPtrPtr = _builder.BuildGEP(CastIfNecessary(_builder, gep,
                    LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), 0), 0), "castDictPtrPtr"),
                new [] {BuildConstInt32(1)}, "dictPtrPtr");
            _builder.BuildStore(dictPtrPtr, dictPtrPtrStore);
            _builder.BuildBr(endifBlock);

            // not fat
            _builder.PositionAtEnd(notFatBranch);
            // store null to indicate the GVM call needs no hidden param at run time
            _builder.BuildStore(LLVMValueRef.CreateConstPointerNull(LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), 0), 0)), dictPtrPtrStore);
            _builder.BuildBr(endifBlock);

            // end if
            _builder.PositionAtEnd(endifBlock);
            var loadPtr = _builder.BuildPhi(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), "fatNotFatPhi");
            loadPtr.AddIncoming(new LLVMValueRef[] { loadFuncPtr, slotRef },
                new LLVMBasicBlockRef[] { fatBranch, notFatBranch }, 2);

            // dont know the type for sure, but will generate for no hidden dict param and change if necessary before calling.
            var asFunc = CastIfNecessary(_builder, loadPtr, LLVMTypeRef.CreatePointer(GetLLVMSignatureForMethod(runtimeDeterminedMethod.Signature, false), 0) , "castToFunc");
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
                llvmReturnType = LLVMTypeRef.Void;
            }

            List<LLVMTypeRef> signatureTypes = new List<LLVMTypeRef>();
            signatureTypes.Add(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0)); // Shadow stack pointer

            if (!returnOnStack && returnType != GetWellKnownType(WellKnownType.Void))
            {
                signatureTypes.Add(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0));
            }

            if (hasHiddenParam)
            {
                signatureTypes.Add(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0)); // *EEType
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

            return LLVMTypeRef.CreateFunction(llvmReturnType, signatureTypes.ToArray(), false);
        }

        private ExpressionEntry AllocateObject(StackEntry eeType, TypeDesc forcedReturnType = null)
        {
            //TODO: call GetNewObjectHelperForType from JitHelper.cs (needs refactoring)
            return CallRuntime(_compilation.TypeSystemContext, RuntimeExport, "RhNewObject", new StackEntry[] { eeType }, forcedReturnType);
        }

        private static LLVMValueRef BuildConstInt1(int number)
        {
            Debug.Assert(number == 0 || number == 1, "Non-boolean int1");
            return LLVMValueRef.CreateConstInt(LLVMTypeRef.Int1, (ulong)number, false);
        }

        private static LLVMValueRef BuildConstInt8(byte number)
        {
            return LLVMValueRef.CreateConstInt(LLVMTypeRef.Int8, number, false);
        }

        private static LLVMValueRef BuildConstInt16(byte number)
        {
            return LLVMValueRef.CreateConstInt(LLVMTypeRef.Int16, number, false);
        }

        private static LLVMValueRef BuildConstInt32(int number)
        {
            return LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)number, false);
        }

        private static LLVMValueRef BuildConstUInt32(uint number)
        {
            return LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, number, false);
        }

        private static LLVMValueRef BuildConstInt64(long number)
        {
            return LLVMValueRef.CreateConstInt(LLVMTypeRef.Int64, (ulong)number, false);
        }

        private static LLVMValueRef BuildConstUInt64(ulong number)
        {
            return LLVMValueRef.CreateConstInt(LLVMTypeRef.Int64, number, false);
        }

        private LLVMValueRef GetEETypeForTypeDesc(TypeDesc target, bool constructed)
        {
            var eeTypePointer = GetEETypePointerForTypeDesc(target, constructed);
            return _builder.BuildLoad(eeTypePointer, "eeTypePtrLoad");
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
                            LLVMValueRef arrayObjPtr = arraySlot.ValueAsType(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), _builder);

                            var argsType = new LLVMTypeRef[]
                            {
                            LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0),
                            LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0),
                            LLVMTypeRef.Int32,
                            LLVMTypeRef.Int1
                            };
                            LLVMValueRef memcpyFunction = GetOrCreateLLVMFunction("llvm.memcpy.p0i8.p0i8.i32", LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, argsType, false));

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
                                _builder.BuildGEP(arrayObjPtr, new LLVMValueRef[] { offset }, string.Empty),
                                _builder.BuildBitCast(src, LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), string.Empty),
                                BuildConstInt32(srcLength), // TODO: Handle destination array length to avoid runtime overflow.
                                BuildConstInt1(0)
                            };
                            _builder.BuildCall(memcpyFunction, args, string.Empty);
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
                        var typedAddress = CastIfNecessary(_builder, addrOfValueType, LLVMTypeRef.CreatePointer(LLVMTypeRef.Int32, 0));
                        _builder.BuildStore(byRefValueParamHolder.ValueForStackKind(StackValueKind.ByRef, _builder, false), typedAddress);

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
                case "DefaultConstructorOf":
                    if (metadataType.Namespace == "System" && metadataType.Name == "Activator" && method.Instantiation.Length == 1)
                    {
                        if (runtimeDeterminedMethod.IsRuntimeDeterminedExactMethod)
                        {
                            var ctorRef = CallGenericHelper(ReadyToRunHelperId.DefaultConstructor, runtimeDeterminedMethod.Instantiation[0]);
                            PushExpression(StackValueKind.Int32, "ctor", ctorRef, GetWellKnownType(WellKnownType.IntPtr));
                        }
                        else
                        {
                            IMethodNode methodNode = (IMethodNode)_compilation.ComputeConstantLookup(ReadyToRunHelperId.DefaultConstructor, method.Instantiation[0]);
                            _dependencies.Add(methodNode);

                            MethodDesc ctor = methodNode.Method;
                            PushExpression(StackValueKind.Int32, "ctor", LLVMFunctionForMethod(ctor, ctor, null, false, null, ctor, out bool _, out LLVMValueRef _, out LLVMValueRef _), GetWellKnownType(WellKnownType.IntPtr));
                        }

                        return true;
                    }
                    break;
            }

            return false;
        }

        // if the call is done via `invoke` then we need the try/then block passed back in case the calling code takes the result from a phi.
        private LLVMBasicBlockRef HandleCall(MethodDesc callee, MethodSignature signature, MethodDesc runtimeDeterminedMethod, ILOpcode opcode = ILOpcode.call, TypeDesc constrainedType = null, LLVMValueRef calliTarget = default(LLVMValueRef), LLVMValueRef hiddenRef = default(LLVMValueRef))
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
                        if (constrainedType.IsRuntimeDeterminedSubtype)
                        {
                            eeTypeEntry = new ExpressionEntry(StackValueKind.ValueType, "eeType", CallGenericHelper(ReadyToRunHelperId.TypeHandle, constrainedType), GetWellKnownType(WellKnownType.IntPtr));
                        }
                        else
                        {
                            eeTypeEntry = new LoadExpressionEntry(StackValueKind.ValueType, "eeType", GetEETypePointerForTypeDesc(constrainedType, true), GetWellKnownType(WellKnownType.IntPtr));
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
            (ExpressionEntry, LLVMBasicBlockRef) returnDetails = HandleCall(callee, signature, canonMethod, argumentValues, runtimeDeterminedMethod, opcode, constrainedType, calliTarget, hiddenRef, resolvedConstraint);
            PushNonNull(returnDetails.Item1);
            return returnDetails.Item2;
        }

        private (ExpressionEntry, LLVMBasicBlockRef) HandleCall(MethodDesc callee, MethodSignature signature, MethodDesc canonMethod, StackEntry[] argumentValues, MethodDesc runtimeDeterminedMethod, ILOpcode opcode = ILOpcode.call, 
            TypeDesc constrainedType = null, LLVMValueRef calliTarget = default(LLVMValueRef), LLVMValueRef hiddenParamRef = default(LLVMValueRef), bool resolvedConstraint = false, TypeDesc forcedReturnType = null, bool fromLandingPad = false, LLVMBuilderRef builder = default(LLVMBuilderRef))
        {
            if (builder.Handle == IntPtr.Zero)
                builder = _builder;

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
            LLVMValueRef shadowStack = builder.BuildGEP(_currentFunclet.GetParam(0),
                new LLVMValueRef[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (uint)offset, false) },
                String.Empty);
            var castShadowStack = builder.BuildPointerCast(shadowStack, LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), "castshadowstack");
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
                LLVMValueRef castReturnAddress = builder.BuildPointerCast(returnAddress, LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), callee?.Name + "_castreturn");
                llvmArgs.Add(castReturnAddress);
            }

            // for GVM, the hidden param is added conditionally at runtime.
            if (opcode != ILOpcode.calli && fatFunctionPtr.Handle == IntPtr.Zero)
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
                            hiddenParam = _currentFunclet.GetParam((uint)(1 + (NeedsReturnStackSlot(_signature) ? 1 : 0)));
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

            if (hiddenParam.Handle != IntPtr.Zero) 
            {
                llvmArgs.Add(CastIfNecessary(hiddenParam, LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0)));
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
            LLVMValueRef llvmReturn = default;
            LLVMBasicBlockRef nextInstrBlock = default(LLVMBasicBlockRef);
            if (fatFunctionPtr.Handle != IntPtr.Zero) // indicates GVM
            {
                // conditional call depending on if the function was fat/the dict hidden param is needed
                // TODO: not sure this is always conditional, maybe there is some optimisation that can be done to not inject this conditional logic depending on the caller/callee
                LLVMValueRef dict = builder.BuildLoad( dictPtrPtrStore, "dictPtrPtr");
                LLVMValueRef dictAsInt = builder.BuildPtrToInt(dict, LLVMTypeRef.Int32, "toInt");
                LLVMValueRef eqZ = builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, dictAsInt, BuildConstInt32(0), "eqz");
                var notFatBranch = _currentFunclet.AppendBasicBlock("notFat");
                var fatBranch = _currentFunclet.AppendBasicBlock("fat");
                var endifBlock = _currentFunclet.AppendBasicBlock("endif");
                builder.BuildCondBr(eqZ, notFatBranch, fatBranch);
               
                // then
                builder.PositionAtEnd(notFatBranch);
                ExceptionRegion currentTryRegion = GetCurrentTryRegion();
                LLVMValueRef notFatReturn = CallOrInvoke(fromLandingPad, builder, currentTryRegion, fn, llvmArgs.ToArray(), ref nextInstrBlock);
                builder.BuildBr(endifBlock);

                // else
                builder.PositionAtEnd(fatBranch);
                var fnWithDict = builder.BuildCast(LLVMOpcode.LLVMBitCast, fn, LLVMTypeRef.CreatePointer(GetLLVMSignatureForMethod(runtimeDeterminedMethod.Signature, true), 0), "fnWithDict");
                var dictDereffed = builder.BuildLoad(builder.BuildLoad( dict, "l1"), "l2");
                llvmArgs.Insert(needsReturnSlot ? 2 : 1, dictDereffed);
                LLVMValueRef fatReturn = CallOrInvoke(fromLandingPad, builder, currentTryRegion, fnWithDict, llvmArgs.ToArray(), ref nextInstrBlock);
                builder.BuildBr(endifBlock);

                // endif
                builder.PositionAtEnd(endifBlock);
                if (!returnType.IsVoid && !needsReturnSlot)

                {
                    llvmReturn = builder.BuildPhi(GetLLVMTypeForTypeDesc(returnType), "callReturnPhi");
                    llvmReturn.AddIncoming(new LLVMValueRef[] { notFatReturn, fatReturn },
                        new LLVMBasicBlockRef[] { notFatBranch, fatBranch }, 2);
                }
                _currentBasicBlock.LastInternalBlock = endifBlock;
            }
            else
            {
                llvmReturn = CallOrInvoke(fromLandingPad, builder, GetCurrentTryRegion(), fn, llvmArgs.ToArray(), ref nextInstrBlock);
            }

            if (!returnType.IsVoid)
            {
                return (
                    needsReturnSlot
                        ? returnSlot
                        : (
                            canonMethod != null && canonMethod.Signature.ReturnType != actualReturnType
                                ? CreateGenericReturnExpression(GetStackValueKind(actualReturnType),
                                    callee?.Name + "_return", llvmReturn, actualReturnType)
                                : new ExpressionEntry(GetStackValueKind(actualReturnType), callee?.Name + "_return",
                                    llvmReturn, actualReturnType)),
                    nextInstrBlock);
            }
            else
            {
                return (null, default(LLVMBasicBlockRef));
            }
        }

        LLVMValueRef CallOrInvoke(bool fromLandingPad, LLVMBuilderRef builder, ExceptionRegion currentTryRegion,
            LLVMValueRef fn, LLVMValueRef[] llvmArgs, ref LLVMBasicBlockRef nextInstrBlock)
        {
            LLVMValueRef retVal;
            if (currentTryRegion == null || fromLandingPad) // not handling exceptions that occur in the LLVM landing pad determining the EH handler 
            {
                retVal = builder.BuildCall(fn, llvmArgs, string.Empty);
            }
            else
            {
                nextInstrBlock = _currentFunclet.AppendBasicBlock(String.Format("Try{0:X}", _currentOffset));

                retVal = builder.BuildInvoke(fn, llvmArgs,
                    nextInstrBlock, GetOrCreateLandingPad(currentTryRegion), string.Empty);

                AddInternalBasicBlock(nextInstrBlock);
                builder.PositionAtEnd(_curBasicBlock);
            }
            return retVal;
        }

        // generic structs need to be cast to the actualReturnType
        private ExpressionEntry CreateGenericReturnExpression(StackValueKind stackValueKind, string calleeName, LLVMValueRef llvmReturn, TypeDesc actualReturnType)
        {
            Debug.Assert(llvmReturn.TypeOf.IsPackedStruct);
            var destStruct = GetLLVMTypeForTypeDesc(actualReturnType).Undef;
            for (uint elemNo = 0; elemNo < llvmReturn.TypeOf.StructElementTypesCount; elemNo++)
            {
                var elemValRef = _builder.BuildExtractValue(llvmReturn, elemNo, "ex" + elemNo);
                destStruct = _builder.BuildInsertValue(destStruct, elemValRef, elemNo, "st" + elemNo);
            }
            return new ExpressionEntry(stackValueKind, calleeName, destStruct, actualReturnType);
        }

        private LLVMBasicBlockRef GetOrCreateLandingPad(ExceptionRegion tryRegion)
        {
            Tuple<int, IntPtr> landingPadKey = Tuple.Create(tryRegion.ILRegion.TryOffset, _currentFunclet.Handle);
            if (_landingPads.TryGetValue(landingPadKey, out LLVMBasicBlockRef landingPad))
            {
                return landingPad;
            }

            if (GxxPersonality.Handle.Equals(IntPtr.Zero))
            {
                GxxPersonalityType = LLVMTypeRef.CreateStruct(new[] {LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), LLVMTypeRef.Int32 }, false);
                GxxPersonality = Module.AddFunction("__gxx_personality_v0", LLVMTypeRef.CreateFunction(GxxPersonalityType, new []
                {
                    LLVMTypeRef.Int32,
                    LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0),
                    LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0),
                }, true));
            }

            landingPad = _currentFunclet.AppendBasicBlock("LandingPad" + tryRegion.ILRegion.TryOffset.ToString("X"));
            _landingPads[landingPadKey] = landingPad;

            LLVMBuilderRef landingPadBuilder = Context.CreateBuilder();
            if (_debugFunction.Handle != IntPtr.Zero)
            {
                // we need a location if going to call something, e.g. InitFromEhInfo and the call could be inlined, this is an LLVM requirement
                landingPadBuilder.CurrentDebugLocation = _builder.CurrentDebugLocation;
            }
            landingPadBuilder.PositionAtEnd(landingPad);
            LLVMValueRef pad = landingPadBuilder.BuildLandingPad(GxxPersonalityType, GxxPersonality, 1, "");
            pad.AddClause(LLVMValueRef.CreateConstPointerNull(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0)));
            pad.IsCleanup = true; // always enter this clause regardless of exception type - do our own exception type matching
            if (RhpCallCatchFunclet.Handle.Equals(IntPtr.Zero))
            {
                RhpCallCatchFunclet = GetOrCreateLLVMFunction("RhpCallCatchFunclet", LLVMTypeRef.CreateFunction(LLVMTypeRef.Int32, new []
                                                                                                                                {
                                                                                                                                    LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0),
                                                                                                                                    LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0),
                                                                                                                                    LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0),
                                                                                                                                    LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0),
                                                                                                                                }, false));
                BuildCatchFunclet(Module, 
                    new LLVMTypeRef[]
                    {
                        LLVMTypeRef.CreatePointer(LLVMTypeRef.CreateFunction(LLVMTypeRef.Int32, new LLVMTypeRef[]
                        {
                            LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0)
                        }, false), 0), // pHandlerIP - catch funcletAddress
                        LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), // shadow stack
                    });
                BuildFilterFunclet(Module, 
                    new LLVMTypeRef[]
                    {
                        LLVMTypeRef.CreatePointer(LLVMTypeRef.CreateFunction(LLVMTypeRef.Int32, new LLVMTypeRef[]
                        {
                            LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), // shadow stack
                        }, false), 0), // pHandlerIP - catch funcletAddress
                        LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), // shadow stack
                    });
                BuildFinallyFunclet(Module);
            }

            // __cxa_begin catch to get the c++ exception object, must be paired with __cxa_end_catch (http://libcxxabi.llvm.org/spec.html)
            var exPtr = landingPadBuilder.BuildCall(GetCxaBeginCatchFunction(), new LLVMValueRef[] { landingPadBuilder.BuildExtractValue(pad, 0, "ex") });

            // unwrap managed, cast to 32bit pointer from 8bit personality signature pointer
            var ex32Ptr = landingPadBuilder.BuildPointerCast(exPtr, LLVMTypeRef.CreatePointer(LLVMTypeRef.Int32, 0));
            var plus4 = landingPadBuilder.BuildGEP(ex32Ptr, new LLVMValueRef[] {BuildConstInt32(1)}, "offset");

            var managedPtr = landingPadBuilder.BuildLoad(plus4, "managedEx");

            // WASMTODO: should this really be a newobj call?
            LLVMTypeRef ehInfoIteratorType = LLVMTypeRef.CreateStruct(new LLVMTypeRef[] { LLVMTypeRef.Int32, LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0) }, false);
            var ehInfoIterator = landingPadBuilder.BuildAlloca(ehInfoIteratorType, "ehInfoIterPtr");

            var iteratorInitArgs = new StackEntry[] {
                                                        new ExpressionEntry(StackValueKind.ObjRef, "ehInfoIter", ehInfoIterator), 
                                                        new ExpressionEntry(StackValueKind.ByRef, "ehInfoStart", LoadAddressOfSymbolNode(_ehInfoNode, landingPadBuilder)),
                                                        new ExpressionEntry(StackValueKind.ByRef, "ehInfoEnd", LoadAddressOfSymbolNode(_ehInfoNode.EndSymbol, landingPadBuilder)),
                                                 new ExpressionEntry(StackValueKind.Int32, "idxStart", LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0, false)) };
            var res = CallRuntime(_compilation.TypeSystemContext, "EHClauseIterator", "InitFromEhInfo", iteratorInitArgs, null, fromLandingPad: true, builder: landingPadBuilder);

            // params are:
            // object exception, uint idxStart,
            // ref StackFrameIterator frameIter, out uint tryRegionIdx, out byte* pHandler
            var tryRegionIdx = landingPadBuilder.BuildAlloca(LLVMTypeRef.Int32, "tryRegionIdx");
            var handlerFuncPtr = landingPadBuilder.BuildAlloca(LLVMTypeRef.Int32, "handlerFuncPtr");

            // put the exception in the spilled slot, exception slot is at 0
            var addressValue = CastIfNecessary(landingPadBuilder, LoadVarAddress(_spilledExpressions[0].LocalIndex,
                    LocalVarKind.Temp, out TypeDesc unused, builder:landingPadBuilder),
                LLVMTypeRef.CreatePointer(LLVMTypeRef.Int32, 0));
            landingPadBuilder.BuildStore(managedPtr, addressValue);

            var arguments = new StackEntry[] { new ExpressionEntry(StackValueKind.ObjRef, "managedPtr", managedPtr),
                                                 new ExpressionEntry(StackValueKind.Int32, "idxStart", LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0xFFFFFFFFu, false)), 
                                                 new ExpressionEntry(StackValueKind.Int32, "idxCurrentBlockStart", LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)_currentBasicBlock.StartOffset, false)),
                                                 new ExpressionEntry(StackValueKind.NativeInt, "shadowStack", _currentFunclet.GetParam(0)),
                                                 new ExpressionEntry(StackValueKind.ByRef, "refFrameIter", ehInfoIterator),
                                                 new ExpressionEntry(StackValueKind.ByRef, "tryRegionIdx", tryRegionIdx),
                                                 new ExpressionEntry(StackValueKind.ByRef, "pHandler", handlerFuncPtr)
                                                 };
            var handler = CallRuntime(_compilation.TypeSystemContext, "EH", "FindFirstPassHandlerWasm", arguments, null, fromLandingPad: true, builder: landingPadBuilder);
            var handlerFunc = landingPadBuilder.BuildLoad(handlerFuncPtr, "handlerFunc");

            var leaveDestination = landingPadBuilder.BuildAlloca(LLVMTypeRef.Int32, "leaveDest"); // create a variable to store the operand of the leave as we can't use the result of the call directly due to domination/branches
            landingPadBuilder.BuildStore(LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0, false), leaveDestination);
            var foundCatchBlock = _currentFunclet.AppendBasicBlock("LPFoundCatch");
            // If it didn't find a catch block, we can rethrow (resume in LLVM) the C++ exception to continue the stack walk.
            var noCatch = landingPadBuilder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0, false),
                handler.ValueAsInt32(landingPadBuilder, false), "testCatch");
            var secondPassBlock = _currentFunclet.AppendBasicBlock("SecondPass");
            landingPadBuilder.BuildCondBr(noCatch, secondPassBlock, foundCatchBlock);

            landingPadBuilder.PositionAtEnd(foundCatchBlock);
            // finished with the c++ exception
            landingPadBuilder.BuildCall(GetCxaEndCatchFunction(), new LLVMValueRef[] { });

            LLVMValueRef[] callCatchArgs = new LLVMValueRef[]
                                  {
                                      LLVMValueRef.CreateConstPointerNull(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0)),
                                      CastIfNecessary(landingPadBuilder, handlerFunc, LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0)), /* catch funclet address */
                                      _currentFunclet.GetParam(0),
                                      LLVMValueRef.CreateConstPointerNull(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0))
                                  };
            LLVMValueRef leaveReturnValue = landingPadBuilder.BuildCall(RhpCallCatchFunclet, callCatchArgs, "");

            landingPadBuilder.BuildStore(leaveReturnValue, leaveDestination);
            landingPadBuilder.BuildBr(secondPassBlock);

            landingPadBuilder.PositionAtEnd(secondPassBlock);

            // reinitialise the iterator
            CallRuntime(_compilation.TypeSystemContext, "EHClauseIterator", "InitFromEhInfo", iteratorInitArgs, null, fromLandingPad: true, builder: landingPadBuilder);

            var secondPassArgs = new StackEntry[] { new ExpressionEntry(StackValueKind.Int32, "idxStart", LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0xFFFFFFFFu, false)),
                                                      new ExpressionEntry(StackValueKind.Int32, "idxTryLandingStart", LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)tryRegion.ILRegion.TryOffset, false)),
                                                      new ExpressionEntry(StackValueKind.ByRef, "refFrameIter", ehInfoIterator),
                                                      new ExpressionEntry(StackValueKind.Int32, "idxLimit", LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0xFFFFFFFFu, false)),
                                                      new ExpressionEntry(StackValueKind.NativeInt, "shadowStack", _currentFunclet.GetParam(0))
                                                  };
            CallRuntime(_compilation.TypeSystemContext, "EH", "InvokeSecondPassWasm", secondPassArgs, null, true, builder: landingPadBuilder);

            var catchLeaveBlock = _currentFunclet.AppendBasicBlock("CatchLeave");
            landingPadBuilder.BuildCondBr(noCatch, GetOrCreateResumeBlock(pad, tryRegion.ILRegion.TryOffset.ToString()), catchLeaveBlock);
            landingPadBuilder.PositionAtEnd(catchLeaveBlock);

            // Use the else as the path for no exception handler found for this exception
            LLVMValueRef @switch = landingPadBuilder.BuildSwitch(landingPadBuilder.BuildLoad(leaveDestination, "loadLeaveDest"), GetOrCreateUnreachableBlock(), 1 /* number of cases, but fortunately this doesn't seem to make much difference */);

            if (_leaveTargets != null)
            {
                LLVMBasicBlockRef switchReturnBlock = default;
                foreach (var leaveTarget in _leaveTargets)
                {
                    var targetBlock = _basicBlocks[leaveTarget];
                    var funcletForBlock = GetFuncletForBlock(targetBlock);
                    if (funcletForBlock.Handle.Equals(_currentFunclet.Handle))
                    {
                        @switch.AddCase(BuildConstInt32(targetBlock.StartOffset), GetLLVMBasicBlockForBlock(targetBlock));
                    }
                    else
                    {

                        // leave destination is in a different funclet, this happens when an exception is thrown/rethrown from inside a catch handler and the throw is not directly in a try handler
                        // In this case we need to return out of this funclet to get back to the containing funclet.  Logic checks we are actually in a catch funclet as opposed to a finally or the main function funclet
                        ExceptionRegion currentRegion = GetTryRegion(_currentBasicBlock.StartOffset);
                        if (currentRegion != null && _currentBasicBlock.StartOffset >= currentRegion.ILRegion.HandlerOffset && _currentBasicBlock.StartOffset < currentRegion.ILRegion.HandlerOffset + currentRegion.ILRegion.HandlerLength
                            && currentRegion.ILRegion.Kind == ILExceptionRegionKind.Catch)
                        {
                            if (switchReturnBlock == default)
                            {
                                switchReturnBlock = _currentFunclet.AppendBasicBlock("SwitchReturn");
                            }
                            @switch.AddCase(BuildConstInt32(targetBlock.StartOffset), switchReturnBlock);
                        }
                    }
                }
                if (switchReturnBlock != default)
                {
                    landingPadBuilder.PositionAtEnd(switchReturnBlock);
                    landingPadBuilder.BuildRet(landingPadBuilder.BuildLoad(leaveDestination, "loadLeaveDest"));
                }
            }

            landingPadBuilder.Dispose();

            return landingPad;
        }

        LLVMValueRef GetCxaBeginCatchFunction()
        {
            if (CxaBeginCatchFunction == default)
            {
                // takes the exception structure and returns the c++ exception, defined by emscripten
                CxaBeginCatchFunction = Module.AddFunction("__cxa_begin_catch", LLVMTypeRef.CreateFunction(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), 
                    new LLVMTypeRef[] { LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0)}, false));
            }
            return CxaBeginCatchFunction;
        }

        LLVMValueRef GetCxaEndCatchFunction()
        {
            if (CxaEndCatchFunction == default)
            {
                CxaEndCatchFunction = Module.AddFunction("__cxa_end_catch", LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, new LLVMTypeRef[] { }, false));
            }
            return CxaEndCatchFunction;
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


            PushNonNull(ImportRawPInvoke(method, arguments, _builder));
        }

        private ExpressionEntry ImportRawPInvoke(MethodDesc method, StackEntry[] arguments, LLVMBuilderRef builder, TypeDesc forcedReturnType = null)
        {
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
            LLVMValueRef realNativeFunc = Module.GetNamedFunction(realMethodName);
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
            if (nativeFunc.Handle == IntPtr.Zero)
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
            _builder.BuildStore(_builder.BuildGEP(_currentFunclet.GetParam(0), new LLVMValueRef[] {stackFrameSize}, "shadowStackTop"), ShadowStackTop);

            LLVMValueRef pInvokeTransitionFrame = default;
            LLVMTypeRef pInvokeFunctionType = default;
            if (method.IsPInvoke)
            {
                // add call to go to preemptive mode
                LLVMTypeRef pInvokeTransitionFrameType =
                    LLVMTypeRef.CreateStruct(new LLVMTypeRef[] { LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0) }, false);
                pInvokeFunctionType = LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, new LLVMTypeRef[] { LLVMTypeRef.CreatePointer(pInvokeTransitionFrameType, 0) }, false);
                pInvokeTransitionFrame = _builder.BuildAlloca(pInvokeTransitionFrameType, "PInvokeTransitionFrame");
                LLVMValueRef RhpPInvoke2 = GetOrCreateLLVMFunction("RhpPInvoke2", pInvokeFunctionType);
                _builder.BuildCall(RhpPInvoke2, new LLVMValueRef[] { pInvokeTransitionFrame }, "");
            }
            // Don't name the return value if the function returns void, it's invalid
            var returnValue = _builder.BuildCall(nativeFunc, llvmArguments, !method.Signature.ReturnType.IsVoid ? "call" : string.Empty);

            if (method.IsPInvoke)
            {
                // add call to go to cooperative mode
                LLVMValueRef RhpPInvokeReturn2 = GetOrCreateLLVMFunction("RhpPInvokeReturn2", pInvokeFunctionType);
                _builder.BuildCall(RhpPInvokeReturn2, new LLVMValueRef[] { pInvokeTransitionFrame }, "");
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
            LLVMTypeRef nativeFuncType = LLVMTypeRef.CreateFunction(GetLLVMTypeForTypeDesc(method.Signature.ReturnType), paramTypes, false);

            if (realFunction.Handle == IntPtr.Zero)
            {
                nativeFunc = Module.AddFunction(realMethodName, nativeFuncType);
                nativeFunc.Linkage = LLVMLinkage.LLVMDLLImportLinkage;
            }
            else
            {
                nativeFunc = _builder.BuildPointerCast(realFunction, LLVMTypeRef.CreatePointer(nativeFuncType, 0), realMethodName + "__slot__");
            }
            return nativeFunc;
        }

        static LLVMValueRef s_shadowStackTop = default(LLVMValueRef);

        LLVMValueRef ShadowStackTop
        {
            get
            {
                if (s_shadowStackTop.Handle.Equals(IntPtr.Zero))
                {
                    s_shadowStackTop = Module.AddGlobal(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), "t_pShadowStackTop");
                    s_shadowStackTop.Linkage = LLVMLinkage.LLVMExternalLinkage;
                    s_shadowStackTop.Initializer = LLVMValueRef.CreateConstPointerNull(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0));
                    s_shadowStackTop.ThreadLocalMode = LLVMThreadLocalMode.LLVMLocalDynamicTLSModel;
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

            LLVMTypeRef thunkSig = LLVMTypeRef.CreateFunction(GetLLVMTypeForTypeDesc(method.Signature.ReturnType), llvmParams, false);
            LLVMValueRef thunkFunc = GetOrCreateLLVMFunction(nativeName, thunkSig);

            LLVMBasicBlockRef shadowStackSetupBlock = thunkFunc.AppendBasicBlock("ShadowStackSetupBlock");
            LLVMBasicBlockRef allocateShadowStackBlock = thunkFunc.AppendBasicBlock("allocateShadowStackBlock");
            LLVMBasicBlockRef managedCallBlock = thunkFunc.AppendBasicBlock("ManagedCallBlock");

            LLVMBuilderRef builder = Context.CreateBuilder();
            builder.PositionAtEnd(shadowStackSetupBlock);

            // Allocate shadow stack if it's null
            LLVMValueRef shadowStackPtr = builder.BuildAlloca(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), "ShadowStackPtr");
            LLVMValueRef savedShadowStack = builder.BuildLoad(ShadowStackTop, "SavedShadowStack");
            builder.BuildStore(savedShadowStack, shadowStackPtr);
            LLVMValueRef shadowStackNull = builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, savedShadowStack, LLVMValueRef.CreateConstPointerNull(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0)), "ShadowStackNull");
            builder.BuildCondBr(shadowStackNull, allocateShadowStackBlock, managedCallBlock);

            builder.PositionAtEnd(allocateShadowStackBlock);

            LLVMValueRef newShadowStack = builder.BuildArrayMalloc(LLVMTypeRef.Int8, BuildConstInt32(1000000), "NewShadowStack");
            builder.BuildStore(newShadowStack, shadowStackPtr);
            builder.BuildBr(managedCallBlock);

            builder.PositionAtEnd(managedCallBlock);
            LLVMTypeRef reversePInvokeFrameType = LLVMTypeRef.CreateStruct(new LLVMTypeRef[] { LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0) }, false);
            LLVMValueRef reversePInvokeFrame = default(LLVMValueRef);
            LLVMTypeRef reversePInvokeFunctionType = LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, new LLVMTypeRef[] { LLVMTypeRef.CreatePointer(reversePInvokeFrameType, 0) }, false);
            if (method.IsUnmanagedCallersOnly)
            {
                reversePInvokeFrame = builder.BuildAlloca(reversePInvokeFrameType, "ReversePInvokeFrame");
                LLVMValueRef RhpReversePInvoke2 = GetOrCreateLLVMFunction("RhpReversePInvoke2", reversePInvokeFunctionType);
                builder.BuildCall(RhpReversePInvoke2, new LLVMValueRef[] { reversePInvokeFrame }, "");
            }

            LLVMValueRef shadowStack = builder.BuildLoad(shadowStackPtr, "ShadowStack");
            int curOffset = 0;
            curOffset = PadNextOffset(method.Signature.ReturnType, curOffset);
            ImportCallMemset(shadowStack, 0, curOffset, builder); // clear any uncovered object references for GC.Collect
            LLVMValueRef calleeFrame = builder.BuildGEP(shadowStack, new LLVMValueRef[] { BuildConstInt32(curOffset) }, "calleeFrame");

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
                LLVMValueRef argValue = thunkFunc.GetParam((uint)i);

                if (CanStoreTypeOnStack(method.Signature[i]))
                {
                    llvmArgs.Add(argValue);
                }
                else
                {
                    curOffset = PadOffset(method.Signature[i], curOffset);
                    LLVMValueRef argAddr = builder.BuildGEP(shadowStack, new LLVMValueRef[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)curOffset, false) }, "arg" + i);
                    builder.BuildStore(argValue, CastIfNecessary(builder, argAddr, LLVMTypeRef.CreatePointer(llvmParams[i], 0), $"parameter{i}_"));
                    curOffset = PadNextOffset(method.Signature[i], curOffset);
                }
            }

            LLVMValueRef llvmReturnValue = builder.BuildCall(managedFunction, llvmArgs.ToArray(), "");

            if (method.IsUnmanagedCallersOnly)
            {
                LLVMValueRef RhpReversePInvokeReturn2 = GetOrCreateLLVMFunction("RhpReversePInvokeReturn2", reversePInvokeFunctionType);
                builder.BuildCall(RhpReversePInvokeReturn2, new LLVMValueRef[] { reversePInvokeFrame }, "");
            }

            if (!method.Signature.ReturnType.IsVoid)
            {
                if (needsReturnSlot)
                {
                    builder.BuildRet(builder.BuildLoad(CastIfNecessary(builder, shadowStack, LLVMTypeRef.CreatePointer(GetLLVMTypeForTypeDesc(method.Signature.ReturnType), 0)), "returnValue"));
                }
                else
                {
                    builder.BuildRet(llvmReturnValue);
                }
            }
            else
            {
                builder.BuildRetVoid();
            }
        }

        private void ImportCalli(int token)
        {
            MethodSignature methodSignature = (MethodSignature)_canonMethodIL.GetObject(token);

            var noHiddenParamSig = GetLLVMSignatureForMethod(methodSignature, false);
            var hddenParamSig = GetLLVMSignatureForMethod(methodSignature, true);
            var target = ((ExpressionEntry)_stack.Pop()).ValueAsType(LLVMTypeRef.CreatePointer(noHiddenParamSig, 0), _builder);

            var functionPtrAsInt = _builder.BuildPtrToInt(target, LLVMTypeRef.Int32, "ptrToInt");
            var andResRef = _builder.BuildBinOp(LLVMOpcode.LLVMAnd, functionPtrAsInt, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)_compilation.TypeSystemContext.Target.FatFunctionPointerOffset, false), "andFatCheck");
            var boolConv = _builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, andResRef, BuildConstInt32(0), "bitConv");
            var fatBranch = _currentFunclet.AppendBasicBlock("fat");
            var notFatBranch = _currentFunclet.AppendBasicBlock("notFat");
            var endif = _currentFunclet.AppendBasicBlock("endif");
            _builder.BuildCondBr(boolConv, notFatBranch, fatBranch);
            _builder.PositionAtEnd(notFatBranch);

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
            var nonFatCallThenBlock = HandleCall(null, methodSignature, null, ILOpcode.calli, calliTarget: target);
            LLVMValueRef fatResRef = default;
            LLVMValueRef nonFatResRef = default;
            bool hasRes = !methodSignature.ReturnType.IsVoid;
            if (hasRes)
            {
                StackEntry nonFatRes = _stack.Pop();
                nonFatResRef = nonFatRes.ValueAsType(methodSignature.ReturnType, _builder);
            }
            _builder.BuildBr(endif);
            _builder.PositionAtEnd(fatBranch);

            // fat branch
            var minusOffset = RemoveFatOffset(_builder, target);
            var minusOffsetPtr = _builder.BuildIntToPtr(minusOffset,
                LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), "ptr");
            var hiddenRefAddr = _builder.BuildGEP(minusOffsetPtr, new[] { BuildConstInt32(_pointerSize) }, "fatArgPtr");
            var hiddenRefPtrPtr = _builder.BuildPointerCast(hiddenRefAddr, LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), 0), 0), "hiddenRefPtr");
            var hiddenRef = _builder.BuildLoad(_builder.BuildLoad(hiddenRefPtrPtr, "hiddenRefPtr"), "hiddenRef");

            for (int i = 0; i < stackCopy.Length; i++)
            {
                _stack.Push(stackCopy[stackCopy.Length - i - 1]);
            }
            var funcPtrPtrWithHidden = _builder.BuildPointerCast(minusOffsetPtr, LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(hddenParamSig, 0), 0), "hiddenFuncPtr");
            var funcWithHidden = _builder.BuildLoad(funcPtrPtrWithHidden, "funcPtr");
            var fatCallThenBlock = HandleCall(null, methodSignature, null, ILOpcode.calli, calliTarget: funcWithHidden, hiddenRef: hiddenRef);
            StackEntry fatRes = null;
            if (hasRes)
            {
                fatRes = _stack.Pop();
                fatResRef = fatRes.ValueAsType(methodSignature.ReturnType, _builder);
            }
            _builder.BuildBr(endif);
            _builder.PositionAtEnd(endif);

            // choose the right return value
            if (hasRes)
            {
                var phi = _builder.BuildPhi(GetLLVMTypeForTypeDesc(methodSignature.ReturnType), "phi");
                phi.AddIncoming(new LLVMValueRef[] {fatResRef, nonFatResRef},
                    new LLVMBasicBlockRef[]
                    {
                        fatCallThenBlock.Handle == IntPtr.Zero
                            ? fatBranch
                            : fatCallThenBlock, // phi requires the preceding blocks, which in the case of an `invoke` (when in a try block) will be the `then` block of the `invoke`
                        nonFatCallThenBlock.Handle == IntPtr.Zero
                            ? notFatBranch
                            : nonFatCallThenBlock // phi requires the preceding blocks, which in the case of an `invoke` (when in a try block) will be the `then` block of the `invoke`
                    }, 2);
                PushExpression(fatRes.Kind, "phi", phi, fatRes.Type);
            }
            _currentBasicBlock.LastInternalBlock = endif;
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
                    if (fatFunctionPtr.Handle != IntPtr.Zero)
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

            if (targetLLVMFunction.Handle.Equals(IntPtr.Zero))
            {
                if (runtimeDeterminedMethod.IsUnmanagedCallersOnly)
                {
                    EcmaMethod ecmaMethod = ((EcmaMethod)runtimeDeterminedMethod);
                    string mangledName = ecmaMethod.GetUnmanagedCallersOnlyExportName();
                    if (mangledName == null)
                    {
                        mangledName = ecmaMethod.Name;
                    }
                    LLVMTypeRef[] llvmParams = new LLVMTypeRef[runtimeDeterminedMethod.Signature.Length];
                    for (int i = 0; i < llvmParams.Length; i++)
                    {
                        llvmParams[i] = GetLLVMTypeForTypeDesc(runtimeDeterminedMethod.Signature[i]);
                    }
                    LLVMTypeRef thunkSig = LLVMTypeRef.CreateFunction(GetLLVMTypeForTypeDesc(runtimeDeterminedMethod.Signature.ReturnType), llvmParams, false);

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
                _builder.BuildBr(GetLLVMBasicBlockForBlock(target));
            }
            else
            {
                LLVMValueRef condition;

                if (opcode == ILOpcode.brfalse || opcode == ILOpcode.brtrue)
                {
                    var op = _stack.Pop();
                    LLVMValueRef value = op.ValueAsInt32(_builder, false);

                    if (value.TypeOf.Kind != LLVMTypeKind.LLVMIntegerTypeKind)
                        throw new InvalidProgramException("branch on non integer");

                    if (opcode == ILOpcode.brfalse)
                    {
                        condition = _builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, value, LLVMValueRef.CreateConstInt(value.TypeOf, 0, false), "brfalse");
                    }
                    else
                    {
                        condition = _builder.BuildICmp(LLVMIntPredicate.LLVMIntNE, value, LLVMValueRef.CreateConstInt(value.TypeOf, 0, false), "brtrue");
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
                                condition = _builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, left, right, "beq");
                                break;
                            case ILOpcode.bge:
                                condition = _builder.BuildICmp(LLVMIntPredicate.LLVMIntSGE, left, right, "bge");
                                break;
                            case ILOpcode.bgt:
                                condition = _builder.BuildICmp(LLVMIntPredicate.LLVMIntSGT, left, right, "bgt");
                                break;
                            case ILOpcode.ble:
                                condition = _builder.BuildICmp(LLVMIntPredicate.LLVMIntSLE, left, right, "ble");
                                break;
                            case ILOpcode.blt:
                                condition = _builder.BuildICmp(LLVMIntPredicate.LLVMIntSLT, left, right, "blt");
                                break;
                            case ILOpcode.bne_un:
                                condition = _builder.BuildICmp(LLVMIntPredicate.LLVMIntNE, left, right, "bne_un");
                                break;
                            case ILOpcode.bge_un:
                                condition = _builder.BuildICmp(LLVMIntPredicate.LLVMIntUGE, left, right, "bge_un");
                                break;
                            case ILOpcode.bgt_un:
                                condition = _builder.BuildICmp(LLVMIntPredicate.LLVMIntUGT, left, right, "bgt_un");
                                break;
                            case ILOpcode.ble_un:
                                condition = _builder.BuildICmp(LLVMIntPredicate.LLVMIntULE, left, right, "ble_un");
                                break;
                            case ILOpcode.blt_un:
                                condition = _builder.BuildICmp(LLVMIntPredicate.LLVMIntULT, left, right, "blt_un");
                                break;
                            default:
                                throw new NotSupportedException(); // unreachable
                        }
                    }
                    else
                    {
                        if (op1.Type.IsWellKnownType(WellKnownType.Double) && op2.Type.IsWellKnownType(WellKnownType.Single))
                        {
                            left = _builder.BuildFPExt(left, LLVMTypeRef.Double, "fpextop2");
                        }
                        else if (op2.Type.IsWellKnownType(WellKnownType.Double) && op1.Type.IsWellKnownType(WellKnownType.Single))
                        {
                            right = _builder.BuildFPExt(right, LLVMTypeRef.Double, "fpextop1");
                        }
                        switch (opcode)
                        {
                            case ILOpcode.beq:
                                condition = _builder.BuildFCmp(LLVMRealPredicate.LLVMRealOEQ, left, right, "beq");
                                break;
                            case ILOpcode.bge:
                                condition = _builder.BuildFCmp(LLVMRealPredicate.LLVMRealOGE, left, right, "bge");
                                break;
                            case ILOpcode.bgt:
                                condition = _builder.BuildFCmp(LLVMRealPredicate.LLVMRealOGT, left, right, "bgt");
                                break;
                            case ILOpcode.ble:
                                condition = _builder.BuildFCmp(LLVMRealPredicate.LLVMRealOLE, left, right, "ble");
                                break;
                            case ILOpcode.blt:
                                condition = _builder.BuildFCmp(LLVMRealPredicate.LLVMRealOLT, left, right, "blt");
                                break;
                            case ILOpcode.bne_un:
                                condition = _builder.BuildFCmp(LLVMRealPredicate.LLVMRealONE, left, right, "bne_un");
                                break;
                            case ILOpcode.bge_un:
                                condition = _builder.BuildFCmp(LLVMRealPredicate.LLVMRealUGE, left, right, "bge_un");
                                break;
                            case ILOpcode.bgt_un:
                                condition = _builder.BuildFCmp(LLVMRealPredicate.LLVMRealUGT, left, right, "bgt_un");
                                break;
                            case ILOpcode.ble_un:
                                condition = _builder.BuildFCmp(LLVMRealPredicate.LLVMRealULE, left, right, "ble_un");
                                break;
                            case ILOpcode.blt_un:
                                condition = _builder.BuildFCmp(LLVMRealPredicate.LLVMRealULT, left, right, "blt_un");
                                break;
                            default:
                                throw new NotSupportedException(); // unreachable
                        }
                    }
                }

                ImportFallthrough(target);
                ImportFallthrough(fallthrough);
                _builder.BuildCondBr(condition, GetLLVMBasicBlockForBlock(target), GetLLVMBasicBlockForBlock(fallthrough));
            }
        }

        private void ImportSwitchJump(int jmpBase, int[] jmpDelta, BasicBlock fallthrough)
        {
            var operand = _stack.Pop();

            var @switch = _builder.BuildSwitch(operand.ValueAsInt32(_builder, false), GetLLVMBasicBlockForBlock(fallthrough), (uint)jmpDelta.Length);
            for (var i = 0; i < jmpDelta.Length; i++)
            {
                var target = _basicBlocks[_currentOffset + jmpDelta[i]];
                @switch.AddCase(LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)i, false), GetLLVMBasicBlockForBlock(target));
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
            bool requireWriteBarrier;

            if (type != null)
            {
                typedPointer = destinationPointer.ValueAsType(type.MakePointerType(), _builder);
                typedValue = value.ValueAsType(type, _builder);
                if (IsStruct(type))
                {
                    StoreStruct(typedPointer, typedValue, type, typedPointer);
                    return;
                }
                requireWriteBarrier = type.IsGCPointer;
            }
            else
            {
                typedPointer = destinationPointer.ValueAsType(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int32, 0), _builder);
                typedValue = value.ValueAsInt32(_builder, false);
                requireWriteBarrier = (value is ExpressionEntry) && !((ExpressionEntry)value).RawLLVMValue.IsNull && value.Type.IsGCPointer;
            }
            if (requireWriteBarrier)
            {
                CallRuntime(_method.Context, "InternalCalls", "RhpAssignRef", new StackEntry[]
                {
                    new ExpressionEntry(StackValueKind.Int32, "typedPointer", typedPointer), value
                });
            }
            else
            {
                _builder.BuildStore(typedValue, typedPointer);
            }
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
                    left = _builder.BuildFPExt(left, LLVMTypeRef.Double, "fpextop2");
                }
                else if (op2.Type.IsWellKnownType(WellKnownType.Double) && op1.Type.IsWellKnownType(WellKnownType.Single))
                {
                    right = _builder.BuildFPExt(right, LLVMTypeRef.Double, "fpextop1");
                }
                switch (opcode)
                {
                    case ILOpcode.add:
                        result = _builder.BuildFAdd(left, right, "fadd");
                        break;
                    case ILOpcode.sub:
                        result = _builder.BuildFSub(left, right, "fsub");
                        break;
                    case ILOpcode.mul:
                        result = _builder.BuildFMul(left, right, "fmul");
                        break;
                    case ILOpcode.div:
                        result = _builder.BuildFDiv(left, right, "fdiv");
                        break;
                    case ILOpcode.rem:
                        result = _builder.BuildFRem(left, right, "frem");
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
                        result = _builder.BuildAdd(left, right, "add");
                        break;
                    case ILOpcode.sub:
                        result = _builder.BuildSub(left, right, "sub");
                        break;
                    case ILOpcode.mul:
                        result = _builder.BuildMul(left, right, "mul");
                        break;
                    case ILOpcode.div:
                        result = _builder.BuildSDiv(left, right, "sdiv");
                        break;
                    case ILOpcode.div_un:
                        result = _builder.BuildUDiv(left, right, "udiv");
                        break;
                    case ILOpcode.rem:
                        result = _builder.BuildSRem(left, right, "srem");
                        break;
                    case ILOpcode.rem_un:
                        result = _builder.BuildURem(left, right, "urem");
                        break;
                    case ILOpcode.and:
                        result = _builder.BuildAnd(left, right, "and");
                        break;
                    case ILOpcode.or:
                        result = _builder.BuildOr(left, right, "or");
                        break;
                    case ILOpcode.xor:
                        result = _builder.BuildXor(left, right, "xor");
                        break;

                    case ILOpcode.add_ovf:
                        Debug.Assert(CanPerformSignedOverflowOperations(op1.Kind));
                        if (Is32BitStackValue(op1.Kind))
                        {
                            result = BuildArithmeticOperationWithOverflowCheck(left, right, "sadd", LLVMTypeRef.Int32);
                        }
                        else
                        {
                            result = BuildArithmeticOperationWithOverflowCheck(left, right, "sadd", LLVMTypeRef.Int64);
                        }
                        break;
                    case ILOpcode.add_ovf_un:
                        Debug.Assert(CanPerformUnsignedOverflowOperations(op1.Kind));
                        if (Is32BitStackValue(op1.Kind))
                        {
                            result = BuildArithmeticOperationWithOverflowCheck(left, right, "uadd", LLVMTypeRef.Int32);
                        }
                        else
                        {
                            result = BuildArithmeticOperationWithOverflowCheck(left, right, "uadd", LLVMTypeRef.Int64);
                        }
                        break;
                    case ILOpcode.sub_ovf:
                        Debug.Assert(CanPerformSignedOverflowOperations(op1.Kind));
                        if (Is32BitStackValue(op1.Kind))
                        {
                            result = BuildArithmeticOperationWithOverflowCheck(left, right, "ssub", LLVMTypeRef.Int32);
                        }
                        else
                        {
                            result = BuildArithmeticOperationWithOverflowCheck(left, right, "ssub", LLVMTypeRef.Int64);
                        }
                        break;
                    case ILOpcode.sub_ovf_un:
                        Debug.Assert(CanPerformUnsignedOverflowOperations(op1.Kind));
                        if (Is32BitStackValue(op1.Kind))
                        {
                            result = BuildArithmeticOperationWithOverflowCheck(left, right, "usub", LLVMTypeRef.Int32);
                        }
                        else
                        {
                            result = BuildArithmeticOperationWithOverflowCheck(left, right, "usub", LLVMTypeRef.Int64);
                        }
                        break;
                    case ILOpcode.mul_ovf_un:
                        Debug.Assert(CanPerformUnsignedOverflowOperations(op1.Kind));
                        if (Is32BitStackValue(op1.Kind))
                        {
                            result = BuildArithmeticOperationWithOverflowCheck(left, right, "umul", LLVMTypeRef.Int32);
                        }
                        else
                        {
                            result = BuildArithmeticOperationWithOverflowCheck(left, right, "umul", LLVMTypeRef.Int64);
                        }
                        break;
                    case ILOpcode.mul_ovf:
                        if (Is32BitStackValue(op1.Kind))
                        {
                            result = BuildArithmeticOperationWithOverflowCheck(left, right, "smul", LLVMTypeRef.Int32);
                        }
                        else
                        {
                            result = BuildArithmeticOperationWithOverflowCheck(left, right, "smul", LLVMTypeRef.Int64);
                        }
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

        LLVMValueRef BuildArithmeticOperationWithOverflowCheck(LLVMValueRef left, LLVMValueRef right, string arithmeticOp, LLVMTypeRef intType)
        {
            LLVMValueRef mulFunction = GetOrCreateLLVMFunction("llvm." + arithmeticOp + ".with.overflow." + (intType == LLVMTypeRef.Int32 ? "i32" : "i64"), LLVMTypeRef.CreateFunction(
                LLVMTypeRef.CreateStruct(new[] { intType, LLVMTypeRef.Int1}, false), new[] { intType, intType }));
            LLVMValueRef mulRes = _builder.BuildCall(mulFunction, new[] {left, right});
            var overflow = _builder.BuildExtractValue(mulRes, 1);
            LLVMBasicBlockRef overflowBlock = _currentFunclet.AppendBasicBlock("ovf");
            LLVMBasicBlockRef noOverflowBlock = _currentFunclet.AppendBasicBlock("no_ovf");
            _builder.BuildCondBr(overflow, overflowBlock, noOverflowBlock);
            
            _builder.PositionAtEnd(overflowBlock);
            CallOrInvokeThrowException(_builder, "ThrowHelpers", "ThrowOverflowException");
            
            _builder.PositionAtEnd(noOverflowBlock);
            LLVMValueRef result = _builder.BuildExtractValue(mulRes, 0);
            AddInternalBasicBlock(noOverflowBlock);
            return result;
        }

        void AddInternalBasicBlock(LLVMBasicBlockRef basicBlock)
        {
            _curBasicBlock = basicBlock;
            _currentBasicBlock.LLVMBlocks.Add(_curBasicBlock);
            _currentBasicBlock.LastInternalBlock = _curBasicBlock;
        }

        bool CanPerformSignedOverflowOperations(StackValueKind kind)
        {
            return kind == StackValueKind.Int32 || kind == StackValueKind.Int64;
        }

        bool CanPerformUnsignedOverflowOperations(StackValueKind kind)
        {
            return CanPerformSignedOverflowOperations(kind) || kind == StackValueKind.ByRef ||
                   kind == StackValueKind.ObjRef || kind == StackValueKind.NativeInt;
        }

        bool Is32BitStackValue(StackValueKind kind)
        {
            return kind == StackValueKind.Int32 || kind == StackValueKind.ByRef ||  kind == StackValueKind.ObjRef || kind == StackValueKind.NativeInt;
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
            if (valueToShiftValue.TypeOf.Equals(LLVMTypeRef.Int64))
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
                    result = _builder.BuildShl(valueToShiftValue, rhs, "shl");
                    break;
                case ILOpcode.shr:
                    result = _builder.BuildAShr(valueToShiftValue, rhs, "shr");
                    break;
                case ILOpcode.shr_un:
                    result = _builder.BuildLShr(valueToShiftValue, rhs, "shr");
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
                    enumCleanTargetType.IsWellKnownType(WellKnownType.Boolean) ||
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
                        result = _builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, typeSaneOp2, typeSaneOp1, "ceq");
                        break;
                    case ILOpcode.cgt:
                        result = _builder.BuildICmp(LLVMIntPredicate.LLVMIntSGT, typeSaneOp2, typeSaneOp1, "cgt");
                        break;
                    case ILOpcode.clt:
                        result = _builder.BuildICmp(LLVMIntPredicate.LLVMIntSLT, typeSaneOp2, typeSaneOp1, "clt");
                        break;
                    case ILOpcode.cgt_un:
                        result = _builder.BuildICmp(LLVMIntPredicate.LLVMIntUGT, typeSaneOp2, typeSaneOp1, "cgt_un");
                        break;
                    case ILOpcode.clt_un:
                        result = _builder.BuildICmp(LLVMIntPredicate.LLVMIntULT, typeSaneOp2, typeSaneOp1, "clt_un");
                        break;
                    default:
                        throw new NotSupportedException(); // unreachable
                }
            }
            else
            {
                if (op1.Type.IsWellKnownType(WellKnownType.Double) && op2.Type.IsWellKnownType(WellKnownType.Single))
                {
                    typeSaneOp2 = _builder.BuildFPExt(typeSaneOp2, LLVMTypeRef.Double, "fpextop2");
                }
                else if (op2.Type.IsWellKnownType(WellKnownType.Double) && op1.Type.IsWellKnownType(WellKnownType.Single))
                {
                    typeSaneOp1 = _builder.BuildFPExt(typeSaneOp1, LLVMTypeRef.Double, "fpextop1");
                }
                switch (opcode)
                {
                    case ILOpcode.ceq:
                        result = _builder.BuildFCmp(LLVMRealPredicate.LLVMRealOEQ, typeSaneOp2, typeSaneOp1, "ceq");
                        break;
                    case ILOpcode.cgt:
                        result = _builder.BuildFCmp(LLVMRealPredicate.LLVMRealOGT, typeSaneOp2, typeSaneOp1, "cgt");
                        break;
                    case ILOpcode.clt:
                        result = _builder.BuildFCmp(LLVMRealPredicate.LLVMRealOLT, typeSaneOp2, typeSaneOp1, "clt");
                        break;
                    case ILOpcode.cgt_un:
                        result = _builder.BuildFCmp(LLVMRealPredicate.LLVMRealUGT, typeSaneOp2, typeSaneOp1, "cgt_un");
                        break;
                    case ILOpcode.clt_un:
                        result = _builder.BuildFCmp(LLVMRealPredicate.LLVMRealULT, typeSaneOp2, typeSaneOp1, "clt_un");
                        break;
                    default:
                        throw new NotSupportedException(); // unreachable
                }
            }

            PushExpression(StackValueKind.Int32, "cmpop", result, GetWellKnownType(WellKnownType.UInt32));
        }

        private void ImportConvert(WellKnownType wellKnownType, bool checkOverflow, bool unsigned)
        {
            //TODO checkOverflow - r_un & r_4, i & i_un
            StackEntry value = _stack.Pop();
            TypeDesc destType = GetWellKnownType(wellKnownType);

            // Load the value and then convert it instead of using ValueAsType to avoid loading the incorrect size
            LLVMValueRef loadedValue = value.ValueAsType(value.Type, _builder);

            ExpressionEntry expressionEntry;
            if (checkOverflow)
            {
                Debug.Assert(destType is EcmaType);
                if (IsLlvmReal(loadedValue.TypeOf))
                {
                    expressionEntry = BuildConvOverflowFromReal(value, loadedValue, (EcmaType)destType, wellKnownType, unsigned, value.Type);
                }
                else
                {
                    expressionEntry = BuildConvOverflow(value.Name(), loadedValue, (EcmaType)destType, wellKnownType, unsigned, value.Type);
                }
            }
            else
            {
                LLVMValueRef converted = CastIfNecessary(loadedValue, GetLLVMTypeForTypeDesc(destType), value.Name(), wellKnownType == WellKnownType.UInt64 /* unsigned is always false, so check for the type explicitly */);
                expressionEntry = new ExpressionEntry(GetStackValueKind(destType), "conv", converted, destType);
            }
            _stack.Push(expressionEntry);
        }

        private bool IsLlvmReal(LLVMTypeRef llvmTypeRef)
        {
            return llvmTypeRef == LLVMTypeRef.Float || llvmTypeRef == LLVMTypeRef.Double;
        }

        ExpressionEntry BuildConvOverflowFromReal(StackEntry value, LLVMValueRef loadedValue, EcmaType destType, WellKnownType destWellKnownType, bool unsigned, TypeDesc sourceType)
        {
            //TODO: single overflow checks extend to doubles - this could be more efficient
            if (value.Type == GetWellKnownType(WellKnownType.Single))
            {
                value = new ExpressionEntry(StackValueKind.Float, "dbl", _builder.BuildFPExt(loadedValue, LLVMTypeRef.Double), GetWellKnownType(WellKnownType.Double));
            }
            switch (destWellKnownType)
            {
                case WellKnownType.Byte:
                case WellKnownType.SByte:
                case WellKnownType.Int16:
                case WellKnownType.UInt16:
                    var intExpression = CallRuntime("Internal.Runtime.CompilerHelpers", _method.Context, "MathHelpers", "Dbl2IntOvf", new[] {value});
                    return BuildConvOverflow(value.Name(), intExpression.ValueForStackKind(StackValueKind.Int32, _builder, false), destType, destWellKnownType, unsigned, sourceType);
                case WellKnownType.Int32:
                    return CallRuntime("Internal.Runtime.CompilerHelpers", _method.Context, "MathHelpers", "Dbl2IntOvf", new[] { value });
                case WellKnownType.UInt32:
                case WellKnownType.UIntPtr: // TODO : 64bit.
                    return CallRuntime("Internal.Runtime.CompilerHelpers", _method.Context, "MathHelpers", "Dbl2UIntOvf", new[] { value });
                case WellKnownType.Int64:
                    return CallRuntime("Internal.Runtime.CompilerHelpers", _method.Context, "MathHelpers", "Dbl2LngOvf", new[] { value });
                case WellKnownType.UInt64:
                    return CallRuntime("Internal.Runtime.CompilerHelpers", _method.Context, "MathHelpers", "Dbl2ULngOvf", new[] { value });
                default:
                    throw new InvalidProgramException("Unsupported destination for singled/double overflow check");
            }
        }

        ExpressionEntry BuildConvOverflow(string name, LLVMValueRef loadedValue, EcmaType destType, WellKnownType destWellKnownType, bool unsigned, TypeDesc sourceType)
        {
            ulong maxValue = 0;
            long minValue = 0;
            switch (destWellKnownType)
            {
                case WellKnownType.Byte:
                    maxValue = byte.MaxValue;
                    break;
                case WellKnownType.SByte:
                    maxValue = (ulong)sbyte.MaxValue;
                    minValue = sbyte.MinValue;
                    break;
                case WellKnownType.UInt16:
                    maxValue = ushort.MaxValue;
                    break;
                case WellKnownType.Int16:
                    maxValue = (ulong)short.MaxValue;
                    minValue = short.MinValue;
                    break;
                case WellKnownType.UInt32:
                case WellKnownType.UIntPtr: // TODO : 64bit.
                    maxValue = uint.MaxValue;
                    break;
                case WellKnownType.Int32:
                    maxValue = int.MaxValue;
                    minValue = int.MinValue;
                    break;
                case WellKnownType.UInt64:
                    maxValue = ulong.MaxValue;
                    break;
                case WellKnownType.Int64:
                    maxValue = long.MaxValue;
                    minValue = long.MinValue;
                    break;
            }
            BuildConvOverflowCheck(loadedValue, unsigned, maxValue, minValue, sourceType, destType);
            LLVMValueRef converted = CastIfNecessary(loadedValue, GetLLVMTypeForTypeDesc(destType), name, unsigned);
            return new ExpressionEntry(GetStackValueKind(destType), "conv", converted, destType);
        }

        private void BuildConvOverflowCheck(LLVMValueRef loadedValue, bool unsigned, ulong maxValue, long minValue, TypeDesc sourceType, EcmaType destType)
        {
            var maxDiff = LLVMValueRef.CreateConstInt(loadedValue.TypeOf, maxValue - (ulong)minValue);

            LLVMBasicBlockRef overflowBlock = _currentFunclet.AppendBasicBlock("ovf");
            LLVMBasicBlockRef noOverflowBlock = _currentFunclet.AppendBasicBlock("no_ovf");
            LLVMValueRef cmp;
            //special case same width signed -> unsigned, can just check for negative values
            if (unsigned && (loadedValue.TypeOf.IntWidth >> 3) == destType.InstanceFieldSize.AsInt && 
                (sourceType == GetWellKnownType(WellKnownType.Int16)
                || sourceType == GetWellKnownType(WellKnownType.Int32)
                || sourceType == GetWellKnownType(WellKnownType.Int64)))
            {
                cmp = _builder.BuildICmp(LLVMIntPredicate.LLVMIntSLT, loadedValue, LLVMValueRef.CreateConstInt(loadedValue.TypeOf, 0));
            }
            else
            {
                var valueDiff = _builder.BuildSub(loadedValue, LLVMValueRef.CreateConstInt(loadedValue.TypeOf, (ulong)minValue));
                cmp = _builder.BuildICmp(LLVMIntPredicate.LLVMIntUGT, valueDiff, maxDiff);
            }
            _builder.BuildCondBr(cmp, overflowBlock, noOverflowBlock);

            _builder.PositionAtEnd(overflowBlock);
            CallOrInvokeThrowException(_builder, "ThrowHelpers", "ThrowOverflowException");
            _builder.PositionAtEnd(noOverflowBlock);
            AddInternalBasicBlock(noOverflowBlock);
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
                        result = _builder.BuildFNeg(argument.ValueForStackKind(argument.Kind, _builder, false), "neg");
                    }
                    else
                    {
                        result = _builder.BuildNeg(argument.ValueForStackKind(argument.Kind, _builder, true), "neg");
                    }
                    break;
                case ILOpcode.not:
                    result = _builder.BuildNot(argument.ValueForStackKind(argument.Kind, _builder, true), "not");
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

            var value = _builder.BuildLoad(src.ValueAsType(pointerType, _builder), "cpobj.load");

            _builder.BuildStore(value, dest.ValueAsType(pointerType, _builder));
        }

        private void ImportUnbox(int token, ILOpcode opCode)
        {
            TypeDesc type = ResolveTypeToken(token);
            TypeDesc methodType = (TypeDesc)_methodIL.GetObject(token);
            LLVMValueRef eeType;
            ExpressionEntry eeTypeExp;
            if (methodType.IsRuntimeDeterminedSubtype)
            {
                eeType = CallGenericHelper(ReadyToRunHelperId.TypeHandle, methodType);
                eeTypeExp = new ExpressionEntry(StackValueKind.ByRef, "eeType", eeType, GetWellKnownType(WellKnownType.IntPtr));
            }
            else
            {
                eeType = GetEETypePointerForTypeDesc(methodType, true);
                eeTypeExp = new LoadExpressionEntry(StackValueKind.ByRef, "eeType", eeType, GetWellKnownType(WellKnownType.IntPtr));
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
                LLVMValueRef untypedObjectValue = _builder.BuildAlloca(GetLLVMTypeForTypeDesc(methodType), "objptr");
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
            return _builder.BuildGEP(_currentFunclet.GetParam(0),
                new LLVMValueRef[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (uint)offset, false) },
                String.Empty);
        }

        private void ImportRefAnyVal(int token)
        {
        }

        private void ImportCkFinite()
        {
            StackEntry value = _stack.Pop();
            if (value.Type == GetWellKnownType(WellKnownType.Single))
            {
                ThrowCkFinite(value.ValueForStackKind(value.Kind, _builder, false), 32, ref CkFinite32Function);
            }
            else
            {
                ThrowCkFinite(value.ValueForStackKind(value.Kind, _builder, false), 64, ref CkFinite64Function);
            }

            _stack.Push(value);
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
                    var handleRef = _builder.BuildCall( fn, new LLVMValueRef[]
                    {
                        GetShadowStack(),
                        hiddenParam
                    }, "getHelper");
                    _stack.Push(new LdTokenEntry<TypeDesc>(StackValueKind.ValueType, "ldtoken", typeDesc, handleRef, runtimeTypeHandleTypeDesc));
                }
                else
                {
                    PushLoadExpression(StackValueKind.ByRef, "ldtoken", GetEETypePointerForTypeDesc(typeDesc, true), GetWellKnownType(WellKnownType.IntPtr));
                    HandleCall(helper, helper.Signature, helper);
                    var callExp = _stack.Pop();
                    _stack.Push(new LdTokenEntry<TypeDesc>(StackValueKind.ValueType, "ldtoken", typeDesc, callExp.ValueAsInt32(_builder, false), runtimeTypeHandleTypeDesc));
                }
            }
            else if (ldtokenValue is FieldDesc)
            {
                LLVMValueRef fieldHandle = LLVMValueRef.CreateConstStruct(new LLVMValueRef[] { BuildConstInt32(0) }, true);
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
            LLVMValueRef allocatedMemory = _builder.BuildArrayAlloca(LLVMTypeRef.Int8, allocSize, "localloc" + _currentOffset);
            allocatedMemory.Alignment = (uint)_pointerSize;
            if (_methodIL.IsInitLocals)
            {
                ImportCallMemset(allocatedMemory, 0, allocSize);
            }

            PushExpression(StackValueKind.NativeInt, "localloc" + _currentOffset, allocatedMemory, _compilation.TypeSystemContext.GetPointerType(GetWellKnownType(WellKnownType.Void)));
        }

        private void ImportEndFilter()
        {
            _builder.BuildRet(_stack.Pop().ValueAsInt32(_builder, false));
        }

        private void ImportCpBlk()
        {
        }

        private void ImportInitBlk()
        {
        }

        private void ImportRethrow()
        {
            // rethrows can only occur from a catch handler in which case there should be an exception slot
            Debug.Assert(_spilledExpressions.Count > 0 && _spilledExpressions[0].Name == "ExceptionSlot");

            ThrowOrRethrow(_spilledExpressions[0]);
        }

        private void ImportSizeOf(int token)
        {
            TypeDesc type = (TypeDesc)_methodIL.GetObject(token);
            int size = type.GetElementSize().AsInt;
            PushExpression(StackValueKind.Int32, "sizeof", LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)size, false), GetWellKnownType(WellKnownType.Int32));
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

            ThrowOrRethrow(exceptionObject);
        }

        void ThrowOrRethrow(StackEntry exceptionObject)
        {
            int offset = GetTotalParameterOffset() + GetTotalLocalOffset();
            LLVMValueRef shadowStack = _builder.BuildGEP(_currentFunclet.GetParam(0),
                new LLVMValueRef[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (uint)offset, false) },
                String.Empty);
            LLVMValueRef exSlot = _builder.BuildBitCast(shadowStack, LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), 0));
            _builder.BuildStore(exceptionObject.ValueAsType(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), _builder), exSlot);
            LLVMValueRef[] llvmArgs = new LLVMValueRef[] { shadowStack };
            MetadataType helperType = _compilation.TypeSystemContext.SystemModule.GetKnownType("System", "Exception");
            MethodDesc helperMethod = helperType.GetKnownMethod("DispatchExWasm", null);
            LLVMValueRef fn = LLVMFunctionForMethod(helperMethod, helperMethod, null, false, null, null, out bool hasHiddenParam, out LLVMValueRef dictPtrPtrStore, out LLVMValueRef fatFunctionPtr);
            ExceptionRegion currentExceptionRegion = GetCurrentTryRegion();
            _builder.BuildCall(fn, llvmArgs, string.Empty);

            if (RhpThrowEx.Handle.Equals(IntPtr.Zero))
            {
                RhpThrowEx = Module.AddFunction("RhpThrowEx", LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, new LLVMTypeRef[] { LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0) }, false));
            }

            LLVMValueRef[] args = new LLVMValueRef[] { exceptionObject.ValueAsType(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), _builder) };
            if (currentExceptionRegion == null)
            {
                _builder.BuildCall(RhpThrowEx, args, "");
                _builder.BuildUnreachable();
            }
            else
            {
                _builder.BuildInvoke(RhpThrowEx, args, GetOrCreateUnreachableBlock(), GetOrCreateLandingPad(currentExceptionRegion), "");
            }

            for (int i = 0; i < _exceptionRegions.Length; i++)
            {
                var r = _exceptionRegions[i];

                if (IsOffsetContained(_currentOffset - 1, r.ILRegion.TryOffset, r.ILRegion.TryLength))
                {
                    MarkBasicBlock(_basicBlocks[r.ILRegion.HandlerOffset]);
                }
            }
        }

        private LLVMBasicBlockRef GetOrCreateUnreachableBlock()
        {
            if (_funcletUnreachableBlocks.TryGetValue(_currentFunclet.Handle, out LLVMBasicBlockRef unreachableBlock))
            {
                return unreachableBlock;
            }

            unreachableBlock = _currentFunclet.AppendBasicBlock("Unreachable");
            LLVMBuilderRef unreachableBuilder = Context.CreateBuilder();
            unreachableBuilder.PositionAtEnd(unreachableBlock);
            unreachableBuilder.BuildUnreachable();
            unreachableBuilder.Dispose();
            _funcletUnreachableBlocks[_currentFunclet.Handle] = unreachableBlock;

            return unreachableBlock;
        }

        private LLVMBasicBlockRef GetOrCreateResumeBlock(LLVMValueRef exceptionValueRef, string offset)
        {
            if (_funcletResumeBlocks.TryGetValue(exceptionValueRef.Handle, out LLVMBasicBlockRef resumeBlock))
            {
                return resumeBlock;
            }

            resumeBlock = _currentFunclet.AppendBasicBlock("Resume" + offset);
            LLVMBuilderRef resumeBuilder = Context.CreateBuilder();
            resumeBuilder.PositionAtEnd(resumeBlock);
            resumeBuilder.BuildResume(exceptionValueRef);
            resumeBuilder.Dispose();
            _funcletResumeBlocks[_currentFunclet.Handle] = resumeBlock;

            return resumeBlock;
        }

        private void ThrowIfNull(LLVMValueRef entry)
        {
            if (NullRefFunction.Handle == IntPtr.Zero)
            {
                NullRefFunction = Module.AddFunction("corert.throwifnull", LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, new LLVMTypeRef[] { LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0) }, false));
                var builder = Context.CreateBuilder();
                var block = NullRefFunction.AppendBasicBlock("Block");
                var throwBlock = NullRefFunction.AppendBasicBlock("ThrowBlock");
                var retBlock = NullRefFunction.AppendBasicBlock("RetBlock");
                builder.PositionAtEnd(block);
                builder.BuildCondBr(builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, NullRefFunction.GetParam(1), LLVMValueRef.CreateConstPointerNull(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0)), "nullCheck"),
                    throwBlock, retBlock);
                builder.PositionAtEnd(throwBlock);
                
                ThrowException(builder, "ThrowHelpers", "ThrowNullReferenceException", NullRefFunction);

                builder.PositionAtEnd(retBlock);
                builder.BuildRetVoid();
            }

            LLVMBasicBlockRef nextInstrBlock = default;
            CallOrInvoke(false, _builder, GetCurrentTryRegion(), NullRefFunction, new LLVMValueRef[] { GetShadowStack(), entry }, ref nextInstrBlock);
        }

        private void ThrowCkFinite(LLVMValueRef value, int size, ref LLVMValueRef llvmCheckFunction)
        {
            if (llvmCheckFunction.Handle == IntPtr.Zero)
            {
                llvmCheckFunction = Module.AddFunction("corert.throwckfinite" + size, LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, new LLVMTypeRef[] { LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), size == 32 ? LLVMTypeRef.Float : LLVMTypeRef.Double }, false));
                LLVMValueRef exponentMask;
                LLVMTypeRef intTypeRef;
                var builder = Context.CreateBuilder();
                var block = llvmCheckFunction.AppendBasicBlock("Block");
                builder.PositionAtEnd(block);

                if (size == 32) 
                {
                    intTypeRef = LLVMTypeRef.Int32;
                    exponentMask = LLVMValueRef.CreateConstInt(intTypeRef, 0x7F800000, false);
                }
                else
                {
                    intTypeRef = LLVMTypeRef.Int64;
                    exponentMask = LLVMValueRef.CreateConstInt(intTypeRef, 0x7FF0000000000000, false);
                }

                var valRef = builder.BuildBitCast(llvmCheckFunction.GetParam(1), intTypeRef);
                LLVMValueRef exponentBits = builder.BuildAnd(valRef, exponentMask, "and");
                LLVMValueRef isFinite = builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, exponentBits, exponentMask, "isfinite");

                LLVMBasicBlockRef throwBlock = llvmCheckFunction.AppendBasicBlock("Throw");
                LLVMBasicBlockRef afterIf = llvmCheckFunction.AppendBasicBlock("AfterIf");
                builder.BuildCondBr(isFinite, throwBlock, afterIf);

                builder.PositionAtEnd(throwBlock);

                ThrowException(builder, "ThrowHelpers", "ThrowOverflowException", llvmCheckFunction);

                afterIf.MoveAfter(llvmCheckFunction.LastBasicBlock);
                builder.PositionAtEnd(afterIf);
                builder.BuildRetVoid();
            }

            LLVMBasicBlockRef nextInstrBlock = default;
            CallOrInvoke(false, _builder, GetCurrentTryRegion(), llvmCheckFunction, new LLVMValueRef[] { GetShadowStack(), value }, ref nextInstrBlock);
        }

        private void ThrowException(LLVMBuilderRef builder, string helperClass, string helperMethodName, LLVMValueRef throwingFunction)
        {
            LLVMValueRef fn = GetHelperLlvmMethod(helperClass, helperMethodName);
            builder.BuildCall(fn, new LLVMValueRef[] {throwingFunction.GetParam(0) }, string.Empty);
            builder.BuildUnreachable();
        }

        /// <summary>
        /// Calls or invokes the call to throwing the exception so it can be caught in the caller
        /// </summary>
        private void CallOrInvokeThrowException(LLVMBuilderRef builder, string helperClass, string helperMethodName)
        {
            LLVMValueRef fn = GetHelperLlvmMethod(helperClass, helperMethodName);
            LLVMBasicBlockRef nextInstrBlock = default;
            CallOrInvoke(false, builder, GetCurrentTryRegion(), fn, new LLVMValueRef[] {GetShadowStack()},  ref nextInstrBlock);
            builder.BuildUnreachable();
        }

        LLVMValueRef GetHelperLlvmMethod(string helperClass, string helperMethodName)
        {
            MetadataType helperType = _compilation.TypeSystemContext.SystemModule.GetKnownType("Internal.Runtime.CompilerHelpers", helperClass);
            MethodDesc helperMethod = helperType.GetKnownMethod(helperMethodName, null);
            LLVMValueRef fn = LLVMFunctionForMethod(helperMethod, helperMethod, null, false, null, null, out bool hasHiddenParam, out LLVMValueRef dictPtrPtrStore, out LLVMValueRef fatFunctionPtr);
            return fn;
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
                    untypedObjectValue = _builder.BuildAlloca(llvmObjectType, "objptr");
                    _builder.BuildStore(objectEntry.ValueAsType(llvmObjectType, _builder), untypedObjectValue);
                    untypedObjectValue = _builder.BuildPointerCast(untypedObjectValue, LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), "objptrcast");
                }
            }
            else
            {
                untypedObjectValue = objectEntry.ValueAsType(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), _builder);
            }
            ThrowIfNull(untypedObjectValue);
            if (field.Offset.AsInt == 0)
            {
                return untypedObjectValue;
            }
            else
            {
                var loadLocation = _builder.BuildGEP(untypedObjectValue,
                    new LLVMValueRef[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)field.Offset.AsInt, false) }, String.Empty);
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
                bool needsCctorCheck = (owningType.IsBeforeFieldInit || (!owningType.IsBeforeFieldInit && owningType != _thisType)) && _compilation.HasLazyStaticConstructor(owningType);

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
                                staticBase = _builder.BuildLoad(_builder.BuildLoad(_builder.BuildPointerCast(basePtrPtr, LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), 0), 0), "castBasePtrPtr"), "basePtr"), "base");
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

                LLVMValueRef castStaticBase = _builder.BuildPointerCast(staticBase, LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), owningType.Name + "_statics");
                LLVMValueRef fieldAddr = _builder.BuildGEP(castStaticBase, new LLVMValueRef[] { BuildConstInt32(fieldOffset) }, field.Name + "_addr");


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
            GenericDictionaryLookup lookup = _compilation.ComputeGenericLookup(_method, helperId, helperArg);

            var retType = helperId == ReadyToRunHelperId.DelegateCtor
                ? LLVMTypeRef.Void
                : LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);
            var helperArgs = new List<LLVMTypeRef>
            {
                LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0),
                LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0),
            };
            if (additionalArgs != null) helperArgs.AddRange(additionalArgs);
            if (_method.RequiresInstMethodDescArg())
            {
                node = _compilation.NodeFactory.ReadyToRunHelperFromDictionaryLookup(lookup.HelperId, lookup.HelperObject, _method);
                helper = GetOrCreateLLVMFunction(node.GetMangledName(_compilation.NameMangler),
                    LLVMTypeRef.CreateFunction(retType, helperArgs.ToArray(), false));
            }
            else
            {
                Debug.Assert(_method.RequiresInstMethodTableArg() || _method.AcquiresInstMethodTableFromThis());
                node = _compilation.NodeFactory.ReadyToRunHelperFromTypeLookup(lookup.HelperId, lookup.HelperObject, _method.OwningType);
                helper = GetOrCreateLLVMFunction(node.GetMangledName(_compilation.NameMangler),
                    LLVMTypeRef.CreateFunction(retType, helperArgs.ToArray(), false));
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
        private void TriggerCctor(MetadataType type, LLVMBuilderRef builder = default)
        {
            if (builder.Handle == IntPtr.Zero) builder = _builder;
            if (type.IsCanonicalSubtype(CanonicalFormKind.Specific)) return; // TODO - what to do here?
            ISymbolNode classConstructionContextSymbol = _compilation.NodeFactory.TypeNonGCStaticsSymbol(type);
            _dependencies.Add(classConstructionContextSymbol);
            LLVMValueRef firstNonGcStatic = LoadAddressOfSymbolNode(classConstructionContextSymbol, builder);

            // TODO: Codegen could check whether it has already run rather than calling into EnsureClassConstructorRun
            // but we'd have to figure out how to manage the additional basic blocks
            LLVMValueRef classConstructionContextPtr = builder.BuildGEP(firstNonGcStatic, new LLVMValueRef[] { BuildConstInt32(-2) }, "classConstructionContext");
            StackEntry classConstructionContext = new AddressExpressionEntry(StackValueKind.NativeInt, "classConstructionContext", classConstructionContextPtr, GetWellKnownType(WellKnownType.IntPtr));
            CallRuntime("System.Runtime.CompilerServices", _compilation.TypeSystemContext, ClassConstructorRunner, "EnsureClassConstructorRun", new StackEntry[] { classConstructionContext }, builder: builder);
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
                _builder.BuildGEP(threadStaticIndex, new LLVMValueRef[] { BuildConstInt32(1) }, "typeTlsIndexPtr"); // index is the second field after the ptr.
            StackEntry tlsIndexExpressionEntry = new LoadExpressionEntry(StackValueKind.ValueType, "typeTlsIndex", typeTlsIndexPtr, GetWellKnownType(WellKnownType.Int32));

            if (needsCctorCheck)
            {
                ISymbolNode classConstructionContextSymbol = _compilation.NodeFactory.TypeNonGCStaticsSymbol(type);
                _dependencies.Add(classConstructionContextSymbol);
                LLVMValueRef firstNonGcStatic = LoadAddressOfSymbolNode(classConstructionContextSymbol);

                // TODO: Codegen could check whether it has already run rather than calling into EnsureClassConstructorRun
                // but we'd have to figure out how to manage the additional basic blocks
                LLVMValueRef classConstructionContextPtr = _builder.BuildGEP(firstNonGcStatic, new LLVMValueRef[] { BuildConstInt32(-2) }, "classConstructionContext");
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
            var classConstCtx = _builder.BuildGEP(
                _builder.BuildBitCast(staticBaseValueRef, LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0),
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

            LLVMValueRef fieldAddress = GetFieldAddress(runtimeDeterminedField, field, isStatic);
            CastingStore(fieldAddress, valueEntry, field.FieldType, true);
        }

        // Loads symbol address. Address is represented as a i32*
        private LLVMValueRef LoadAddressOfSymbolNode(ISymbolNode node, LLVMBuilderRef builder = default(LLVMBuilderRef))
        {
            if (builder.Handle == IntPtr.Zero)
                builder = _builder;

            LLVMValueRef addressOfAddress = WebAssemblyObjectWriter.GetSymbolValuePointer(Module, node, _compilation.NameMangler, false);
            //return addressOfAddress;
            return builder.BuildLoad(addressOfAddress, "LoadAddressOfSymbolNode");
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
            if (llvmType.Kind == LLVMTypeKind.LLVMStructTypeKind)
            {
                ImportCallMemset(valueEntry.ValueAsType(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), _builder), 0, type.GetElementSize().AsInt, _builder);
            }
            else if (llvmType.Kind == LLVMTypeKind.LLVMIntegerTypeKind)
                _builder.BuildStore(LLVMValueRef.CreateConstInt(llvmType, 0, false), valueEntry.ValueAsType(LLVMTypeRef.CreatePointer(llvmType, 0), _builder));
            else if (llvmType.Kind == LLVMTypeKind.LLVMPointerTypeKind)
                _builder.BuildStore(LLVMValueRef.CreateConstNull(llvmType), valueEntry.ValueAsType(LLVMTypeRef.CreatePointer(llvmType, 0), _builder));
            else if (llvmType.Kind == LLVMTypeKind.LLVMFloatTypeKind || llvmType.Kind == LLVMTypeKind.LLVMDoubleTypeKind)
                _builder.BuildStore(LLVMValueRef.CreateConstReal(llvmType, 0.0), valueEntry.ValueAsType(LLVMTypeRef.CreatePointer(llvmType, 0), _builder));
            else
                throw new NotImplementedException();
        }

        private void ImportBox(int token)
        {
            LLVMValueRef eeType;
            TypeDesc type = (TypeDesc)_methodIL.GetObject(token);

            StackEntry eeTypeEntry;
            bool truncDouble = type.Equals(GetWellKnownType(WellKnownType.Single));
            if (type.IsRuntimeDeterminedSubtype)
            {
                eeType = CallGenericHelper(ReadyToRunHelperId.TypeHandle, type);
                eeTypeEntry = new ExpressionEntry(StackValueKind.ValueType, "eeType", eeType, GetWellKnownType(WellKnownType.IntPtr).MakePointerType());
                type = type.ConvertToCanonForm(CanonicalFormKind.Specific);
            }
            else
            {
                eeType = GetEETypePointerForTypeDesc(type, true);
                eeTypeEntry = new LoadExpressionEntry(StackValueKind.ValueType, "eeType", eeType, GetWellKnownType(WellKnownType.IntPtr).MakePointerType());
            }
            var toBoxValue = _stack.Pop();
            StackEntry valueAddress;
            if (truncDouble)
            {
                var doubleToBox = toBoxValue.ValueAsType(LLVMTypeRef.Double, _builder);
                var singleToBox = _builder.BuildFPTrunc(doubleToBox, LLVMTypeRef.Float, "trunc");
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
                    var funcletParams = new LLVMValueRef[] {_currentFunclet.GetParam(0)};

                    // todo morganb: this should use invoke if the finally is inside of an outer try block
                    _builder.BuildCall(GetFuncletForBlock(finallyBlock), funcletParams, String.Empty);
                }
            }

            MarkBasicBlock(target);

            // If the target is in the current funclet, jump to it. If it's not, we're in a catch
            // block and need to return the offset to the calling funclet to jump to the block
            LLVMValueRef targetFunclet = GetFuncletForBlock(target);
            if (_currentFunclet.Handle.Equals(targetFunclet.Handle))
            {
                _builder.BuildBr(GetLLVMBasicBlockForBlock(target));
            }
            else
            {
                _builder.BuildRet(BuildConstInt32(target.StartOffset));
            }
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
            }
            var helper = GetNewArrayHelperForType(runtimeDeterminedArrayType);
            var res = CallRuntime(_compilation.TypeSystemContext, InternalCalls, helper, arguments, runtimeDeterminedArrayType);
            int spillIndex = _spilledExpressions.Count;
            SpilledExpressionEntry spillEntry = new SpilledExpressionEntry(StackValueKind.ObjRef, "newarray" + _currentOffset, runtimeDeterminedArrayType, spillIndex, this);
            _spilledExpressions.Add(spillEntry);
            LLVMValueRef addrOfValueType = LoadVarAddress(spillIndex, LocalVarKind.Temp, out TypeDesc unused);
            var typedAddress = CastIfNecessary(_builder, addrOfValueType, LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), 0));
            _builder.BuildStore(res.ValueAsType(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), _builder), typedAddress);

            PushNonNull(spillEntry);
        }

        //TODO: copy of the same method in JitHelper.cs but that is internal
        public static string GetNewArrayHelperForType(TypeDesc type)
        {
            if (type.RequiresAlign8())
                return "RhpNewArrayAlign8";
        
            return "RhpNewArray";
        }

        LLVMValueRef GetGenericContext()
        {
            Debug.Assert(_method.IsSharedByGenericInstantiations);
            if (_method.AcquiresInstMethodTableFromThis())
            {
                LLVMValueRef typedAddress;
                LLVMValueRef thisPtr;

                typedAddress = CastIfNecessary(_builder, _currentFunclet.GetParam(0),
                    LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), 0), 0));
                thisPtr = _builder.BuildLoad( typedAddress, "loadThis");

                return _builder.BuildLoad( thisPtr, "methodTablePtrRef");
            }
            // if the function has exception regions, the generic context is stored in a local, otherwise get it from the parameters
            return _exceptionRegions.Length > 0
                ? _builder.BuildLoad(CastIfNecessary(_builder, LoadVarAddress(1, LocalVarKind.Temp, out TypeDesc unused),  LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), 0), "ctx"))
                : CastIfNecessary(_builder, _currentFunclet.GetParam(GetHiddenContextParamNo() /* hidden param after shadow stack and return slot if present */), LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), "HiddenArg");
        }

        uint GetHiddenContextParamNo()
        {
            return 1 + (NeedsReturnStackSlot(_method.Signature) ? (uint)1 : 0);
        }

        bool FuncletsRequireHiddenContext()
        {
            return _method.RequiresInstArg();
        }

        LLVMValueRef GetGenericContextParamForFunclet()
        {
            return FuncletsRequireHiddenContext()
                ? GetGenericContext()
                : LLVMValueRef.CreateConstPointerNull(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0));
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
            PushLoadExpression(GetStackValueKind(nullSafeElementType), $"{arrayReference.Name()}Element", GetElementAddress(index.ValueAsInt32(_builder, true), arrayReference.ValueAsType(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), _builder), nullSafeElementType), nullSafeElementType);
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
            LLVMValueRef elementAddress = GetElementAddress(index.ValueAsInt32(_builder, true), arrayReference.ValueAsType(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), _builder), nullSafeElementType);
            CastingStore(elementAddress, value, nullSafeElementType, true);
        }

        private void ImportLoadLength()
        {
            StackEntry arrayReference = _stack.Pop();
            var arrayReferenceValue = arrayReference.ValueAsType(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), _builder);
            ThrowIfNull(arrayReferenceValue);
            LLVMValueRef lengthPtr = _builder.BuildGEP(arrayReferenceValue, new LLVMValueRef[] { BuildConstInt32(_compilation.NodeFactory.Target.PointerSize) }, "arrayLength");
            LLVMValueRef castLengthPtr = _builder.BuildPointerCast(lengthPtr, LLVMTypeRef.CreatePointer(LLVMTypeRef.Int32, 0), "castArrayLength");
            PushLoadExpression(StackValueKind.Int32, "arrayLength", castLengthPtr, GetWellKnownType(WellKnownType.Int32));
        }

        private void ImportAddressOfElement(int token)
        {
            TypeDesc elementType = ResolveTypeToken(token);
            var byRefElement = elementType.MakeByRefType();
            StackEntry index = _stack.Pop();
            StackEntry arrayReference = _stack.Pop();

            PushExpression(GetStackValueKind(byRefElement), $"{arrayReference.Name()}ElementAddress", GetElementAddress(index.ValueAsInt32(_builder, true), arrayReference.ValueAsType(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), _builder), elementType), byRefElement);
        }

        private LLVMValueRef GetElementAddress(LLVMValueRef elementPosition, LLVMValueRef arrayReference, TypeDesc arrayElementType)
        {
            ThrowIfNull(arrayReference);
            var elementSize = arrayElementType.GetElementSize();
            LLVMValueRef elementOffset = _builder.BuildMul(elementPosition, BuildConstInt32(elementSize.AsInt), "elementOffset");
            LLVMValueRef arrayOffset = _builder.BuildAdd(elementOffset, ArrayBaseSizeRef(), "arrayOffset");
            return _builder.BuildGEP(arrayReference, new LLVMValueRef[] { arrayOffset }, "elementPointer");
        }

        LLVMValueRef EmitRuntimeHelperCall(string name, TypeDesc returnType, LLVMValueRef[] parameters)
        {
            var runtimeHelperSig = LLVMTypeRef.CreateFunction(GetLLVMTypeForTypeDesc(returnType), parameters.Select(valRef => valRef.TypeOf).ToArray(), false);
            var runtimeHelper = GetOrCreateLLVMFunction(name, runtimeHelperSig);
            return _builder.BuildCall(runtimeHelper, parameters, "call_" + name);
        }

        private void ImportEndFinally()
        {
            _builder.BuildRetVoid();
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

        private ExpressionEntry CallRuntime(TypeSystemContext context, string className, string methodName, StackEntry[] arguments, TypeDesc forcedReturnType = null, bool fromLandingPad = false, LLVMBuilderRef builder = default(LLVMBuilderRef))
        {
            return CallRuntime("System.Runtime", context, className, methodName, arguments, forcedReturnType, fromLandingPad, builder);
        }

        private ExpressionEntry CallRuntime(string @namespace, TypeSystemContext context, string className, string methodName, StackEntry[] arguments, TypeDesc forcedReturnType = null, bool fromLandingPad = false, LLVMBuilderRef builder = default(LLVMBuilderRef))
        {
            if (builder.Handle == IntPtr.Zero)
                builder = _builder;

            MetadataType helperType = context.SystemModule.GetKnownType(@namespace, className);
            MethodDesc helperMethod = helperType.GetKnownMethod(methodName, null);
            if ((helperMethod.IsInternalCall && helperMethod.HasCustomAttribute("System.Runtime", "RuntimeImportAttribute")))
                return ImportRawPInvoke(helperMethod, arguments, forcedReturnType: forcedReturnType, builder: builder);
            else
                return HandleCall(helperMethod, helperMethod.Signature, helperMethod, arguments, helperMethod, fromLandingPad: fromLandingPad, builder: builder).Item1;
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
            if (builder.Handle == IntPtr.Zero)
                builder = _builder;

            if (TrapFunction.Handle == IntPtr.Zero)
            {
                TrapFunction = Module.AddFunction("llvm.trap", LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, Array.Empty<LLVMTypeRef>(), false));
            }
            builder.BuildCall(TrapFunction, Array.Empty<LLVMValueRef>(), string.Empty);
            builder.BuildUnreachable();
        }

        private void EmitDoNothingCall()
        {
            if (DoNothingFunction.Handle == IntPtr.Zero)
            {
                DoNothingFunction = Module.AddFunction("llvm.donothing", LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, Array.Empty<LLVMTypeRef>(), false));
            }
            _builder.BuildCall(DoNothingFunction, Array.Empty<LLVMValueRef>(), string.Empty);
        }

        public override string ToString()
        {
            return _method.ToString();
        }
        
        //TOOD refactor with cctor
        public ExpressionEntry OutputCodeForGetThreadStaticBaseForType(LLVMValueRef threadStaticIndex)
        {
            var threadStaticIndexPtr = _builder.BuildPointerCast(threadStaticIndex,
                LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int32, 0), 0), "tsiPtr");
            LLVMValueRef typeTlsIndexPtr =
                _builder.BuildGEP(threadStaticIndexPtr, new LLVMValueRef[] { BuildConstInt32(1) }, "typeTlsIndexPtr"); // index is the second field after the ptr.

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
            return builder.BuildAnd(CastIfNecessary(builder, fatFunctionRef, LLVMTypeRef.Int32),
                BuildConstUInt32(~(uint)_compilation.TypeSystemContext.Target.FatFunctionPointerOffset), "minusFatOffset");
        }

        private void CreateEHData(WebAssemblyMethodCodeNode methodCodeNodeNeedingCode)
        {
            ObjectNode.ObjectData ehInfo = _exceptionRegions.Length > 0 ? EncodeEHInfo() : null;
            //WASMTODO: is this just for debugging, what happens if we dont have it
            //            DebugEHClauseInfo[] debugEHClauseInfos = null;
            //            if (_ehClauses != null)
            //            {
            //                debugEHClauseInfos = new DebugEHClauseInfo[_ehClauses.Length];
            //                for (int i = 0; i < _ehClauses.Length; i++)
            //                {
            //                    var clause = _ehClauses[i];
            //                    debugEHClauseInfos[i] = new DebugEHClauseInfo(clause.TryOffset, clause.TryLength,
            //                        clause.HandlerOffset, clause.HandlerLength);
            //                }
            //            }

            // WASMTODO: guessing we dont need this 
            //            _methodCodeNode.SetCode(objectData);

            // WASMTODO: guessing we dont need these yet 
            //            _methodCodeNode.InitializeFrameInfos(_frameInfos);
            //            _methodCodeNode.InitializeDebugEHClauseInfos(debugEHClauseInfos);
            //            _methodCodeNode.InitializeGCInfo(_gcInfo);
            //
            //            _methodCodeNode.InitializeDebugLocInfos(_debugLocInfos);
            //            _methodCodeNode.InitializeDebugVarInfos(_debugVarInfos);

            if (ehInfo != null)
            {
                _ehInfoNode.AddEHInfo(ehInfo);
                _dependencies.Add(_ehInfoNode);
            }
        }

        private ObjectNode.ObjectData EncodeEHInfo()
        {
            var builder = new ObjectDataBuilder();
            builder.RequireInitialAlignment(1);
            int totalClauses = _exceptionRegions.Length;

            // Count the number of special markers that will be needed
//            for (int i = 1; i < _exceptionRegions.Length; i++)
//            {
//                ExceptionRegion clause = _exceptionRegions[i];
//                ExceptionRegion previousClause = _exceptionRegions[i - 1];

                // WASMTODO : do we need these special markers and if so how do we detect and set CORINFO_EH_CLAUSE_SAMETRY?
//                if ((previousClause.ILRegion.TryOffset == clause.ILRegion.TryOffset) &&
//                    (previousClause.ILRegion.TryLength == clause.ILRegion.TryLength) &&
//                    ((clause.Flags & CORINFO_EH_CLAUSE_FLAGS.CORINFO_EH_CLAUSE_SAMETRY) == 0))
//                {
//                    totalClauses++;
//                }
//            }

            builder.EmitCompressedUInt((uint)totalClauses);
            // Iterate backwards to emit the innermost first, but within a try region go forwards to get the first matching catch type
            int i = _exceptionRegions.Length - 1;
            while (i >= 0)
            {
                int tryStart = _exceptionRegions[i].ILRegion.TryOffset;
                int tryLength = _exceptionRegions[i].ILRegion.TryLength;
                for (var j = 0; j < _exceptionRegions.Length; j++)
                {
                    ExceptionRegion exceptionRegion = _exceptionRegions[j];
                    if (exceptionRegion.ILRegion.TryOffset != tryStart || exceptionRegion.ILRegion.TryLength != tryLength) continue;
                    //                if (i > 0)
                    //                {
                    //                    ExceptionRegion previousClause = _exceptionRegions[i - 1];

                    // If the previous clause has same try offset and length as the current clause,
                    // but belongs to a different try block (CORINFO_EH_CLAUSE_SAMETRY is not set),
                    // emit a special marker to allow runtime distinguish this case.
                    //WASMTODO: see above - do we need these
                    //                    if ((previousClause.TryOffset == clause.TryOffset) &&
                    //                        (previousClause.TryLength == clause.TryLength) &&
                    //                        ((clause.Flags & CORINFO_EH_CLAUSE_FLAGS.CORINFO_EH_CLAUSE_SAMETRY) == 0))
                    //                    {
                    //                        builder.EmitCompressedUInt(0);
                    //                        builder.EmitCompressedUInt((uint)RhEHClauseKind.RH_EH_CLAUSE_FAULT);
                    //                        builder.EmitCompressedUInt(0);
                    //                    }
                    //                }

                    RhEHClauseKind clauseKind;

                    if (exceptionRegion.ILRegion.Kind == ILExceptionRegionKind.Fault ||
                        exceptionRegion.ILRegion.Kind == ILExceptionRegionKind.Finally)
                    {
                        clauseKind = RhEHClauseKind.RH_EH_CLAUSE_FAULT;
                    }
                    else if (exceptionRegion.ILRegion.Kind == ILExceptionRegionKind.Filter)
                    {
                        clauseKind = RhEHClauseKind.RH_EH_CLAUSE_FILTER;
                    }
                    else
                    {
                        clauseKind = RhEHClauseKind.RH_EH_CLAUSE_TYPED;
                    }

                    builder.EmitCompressedUInt((uint)exceptionRegion.ILRegion.TryOffset);

                    builder.EmitCompressedUInt(((uint)tryLength << 2) | (uint)clauseKind);

                    RelocType rel = (_compilation.NodeFactory.Target.IsWindows)
                        ? RelocType.IMAGE_REL_BASED_ABSOLUTE
                        : RelocType.IMAGE_REL_BASED_REL32;

                    if (_compilation.NodeFactory.Target.Abi == TargetAbi.Jit)
                        rel = RelocType.IMAGE_REL_BASED_REL32;

                    switch (clauseKind)
                    {
                        case RhEHClauseKind.RH_EH_CLAUSE_TYPED:
                            var type = (TypeDesc)_methodIL.GetObject((int)exceptionRegion.ILRegion.ClassToken);
                            Debug.Assert(!type.IsCanonicalSubtype(CanonicalFormKind.Any));
                            AlignForSymbol(ref builder);
                            var typeSymbol = _compilation.NodeFactory.NecessaryTypeSymbol(type);
                            builder.EmitReloc(typeSymbol, rel);
                            string catchFuncletName = GetFuncletName(exceptionRegion,
                                exceptionRegion.ILRegion.HandlerOffset, exceptionRegion.ILRegion.Kind);
                            builder.EmitReloc(new WebAssemblyBlockRefNode(catchFuncletName), rel);
                            break;
                        case RhEHClauseKind.RH_EH_CLAUSE_FAULT:
                            AlignForSymbol(ref builder);
                            string finallyFuncletName = GetFuncletName(exceptionRegion,
                                exceptionRegion.ILRegion.HandlerOffset, exceptionRegion.ILRegion.Kind);
                            builder.EmitReloc(new WebAssemblyBlockRefNode(finallyFuncletName), rel);
                            break;
                        case RhEHClauseKind.RH_EH_CLAUSE_FILTER:
                            AlignForSymbol(ref builder);
                            string clauseFuncletName = GetFuncletName(exceptionRegion,
                                exceptionRegion.ILRegion.HandlerOffset, ILExceptionRegionKind.Catch);
                            builder.EmitReloc(new WebAssemblyBlockRefNode(clauseFuncletName), rel);
                            string filterFuncletName = GetFuncletName(exceptionRegion,
                                exceptionRegion.ILRegion.FilterOffset, exceptionRegion.ILRegion.Kind);
                            builder.EmitReloc(new WebAssemblyBlockRefNode(filterFuncletName), rel);
                            break;
                    }
                    i--;
                }
            }

            return builder.ToObjectData();
        }

        private string GetFuncletName(ExceptionRegion exceptionRegion, int regionOffset, ILExceptionRegionKind ilExceptionRegionKind)
        {
            return _mangledName + "$" + ilExceptionRegionKind.ToString() + regionOffset.ToString("X");
        }

        void AlignForSymbol(ref ObjectDataBuilder builder)
        {
            if ((builder.CountBytes & 3) == 0) return;
            var padding = (4 - (builder.CountBytes & 3));

            for (var pad = 0; pad < padding; pad++)
            {
                builder.EmitByte(0);
            }
        }

        enum RhEHClauseKind
        {
            RH_EH_CLAUSE_TYPED = 0, // catch
            RH_EH_CLAUSE_FAULT = 1, // fault and finally
            RH_EH_CLAUSE_FILTER = 2 
        }

        partial void OnLeaveTargetCreated(int target)
        {
            if (_leaveTargets == null)
            {
                _leaveTargets = new List<int>();
            }
            if(!_leaveTargets.Contains(target)) _leaveTargets.Add(target);
        }

        class AddressCacheContext
        {
            internal LLVMBuilderRef PrologBuilder;
            internal LLVMValueRef[] ArgAddresses;
            internal LLVMValueRef[] LocalAddresses;
            internal List<LLVMValueRef> TempAddresses;
        }
    }
}
