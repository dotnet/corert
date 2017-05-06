// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;

using ILCompiler;
using ILCompiler.DependencyAnalysis;

using Debug = System.Diagnostics.Debug;
using DependencyList = ILCompiler.DependencyAnalysisFramework.DependencyNodeCore<ILCompiler.DependencyAnalysis.NodeFactory>.DependencyList;

namespace Internal.IL
{
    // Implements an IL scanner that scans method bodies to be compiled by the code generation
    // backend before the actual compilation happens to gain insights into the code.
    partial class ILImporter
    {
        private readonly MethodIL _methodIL;
        private readonly ILScanner _compilation;
        private readonly ILScanNodeFactory _factory;

        private readonly MethodDesc _canonMethod;

        private readonly DependencyList _dependencies = new DependencyList();

        private readonly byte[] _ilBytes;
        
        private class BasicBlock
        {
            // Common fields
            public BasicBlock Next;

            public int StartOffset;
            public int EndOffset;

            public bool TryStart;
            public bool FilterStart;
            public bool HandlerStart;
        }

        private TypeDesc _constrained;

        private class ExceptionRegion
        {
            public ILExceptionRegion ILRegion;
        }
        private ExceptionRegion[] _exceptionRegions;

        public ILImporter(ILScanner compilation, MethodDesc method, MethodIL methodIL)
        {
            // This is e.g. an "extern" method in C# without a DllImport or InternalCall.
            if (methodIL == null)
            {
                throw new TypeSystemException.InvalidProgramException(ExceptionStringID.InvalidProgramSpecific, method);
            }

            _compilation = compilation;
            _factory = (ILScanNodeFactory)compilation.NodeFactory;
            
            _ilBytes = methodIL.GetILBytes();

            // Get the runtime determined method IL so that this works right in shared code
            // and tokens in shared code resolve to runtime determined types.
            if (method.IsSharedByGenericInstantiations)
            {
                MethodDesc sharedMethod = method.GetSharedRuntimeFormMethodTarget();
                _methodIL = new InstantiatedMethodIL(sharedMethod, methodIL.GetMethodILDefinition());
            }
            else
            {
                _methodIL = methodIL;
            }

            _canonMethod = method;

            var ilExceptionRegions = methodIL.GetExceptionRegions();
            _exceptionRegions = new ExceptionRegion[ilExceptionRegions.Length];
            for (int i = 0; i < ilExceptionRegions.Length; i++)
            {
                _exceptionRegions[i] = new ExceptionRegion() { ILRegion = ilExceptionRegions[i] };
            }
        }

        public DependencyList Import()
        {
            FindBasicBlocks();
            ImportBasicBlocks();

            return _dependencies;
        }

        private ISymbolNode GetGenericLookupHelper(ReadyToRunHelperId helperId, object helperArgument)
        {
            if (_canonMethod.RequiresInstMethodDescArg())
            {
                return _compilation.NodeFactory.ReadyToRunHelperFromDictionaryLookup(helperId, helperArgument, _canonMethod);
            }
            else
            {
                Debug.Assert(_canonMethod.RequiresInstArg() || _canonMethod.AcquiresInstMethodTableFromThis());
                return _compilation.NodeFactory.ReadyToRunHelperFromTypeLookup(helperId, helperArgument, _canonMethod.OwningType);
            }
        }

        private ISymbolNode GetHelperEntrypoint(ReadyToRunHelper helper)
        {
            string mangledName;
            MethodDesc methodDesc;
            JitHelper.GetEntryPoint(_compilation.TypeSystemContext, helper, out mangledName, out methodDesc);
            Debug.Assert(mangledName != null || methodDesc != null);

            ISymbolNode entryPoint;
            if (mangledName != null)
                entryPoint = _compilation.NodeFactory.ExternSymbol(mangledName);
            else
                entryPoint = _compilation.NodeFactory.MethodEntrypoint(methodDesc);

            return entryPoint;
        }

        private void MarkInstructionBoundary() { }
        private void StartImportingBasicBlock(BasicBlock basicBlock) { }
        private void EndImportingBasicBlock(BasicBlock basicBlock) { }
        private void StartImportingInstruction() { }
        private void EndImportingInstruction() { }

        private void ImportJmp(int token)
        {
            throw new NotImplementedException();
        }

        private void ImportCasting(ILOpcode opcode, int token)
        {
            TypeDesc type = (TypeDesc)_methodIL.GetObject(token);

            // Nullable needs to be unwrapped
            if (type.IsNullable)
                type = type.Instantiation[0];

            if (type.IsRuntimeDeterminedSubtype)
            {
                _dependencies.Add(GetGenericLookupHelper(ReadyToRunHelperId.TypeHandle, type), "IsInst/CastClass");
            }
            else
            {
                ReadyToRunHelperId helperId;
                if (opcode == ILOpcode.isinst)
                {
                    helperId = ReadyToRunHelperId.IsInstanceOf;
                }
                else
                {
                    Debug.Assert(opcode == ILOpcode.castclass);
                    helperId = ReadyToRunHelperId.CastClass;
                }

                _dependencies.Add(_factory.ReadyToRunHelper(helperId, type), "IsInst/CastClass");
            }
        }
        
        private void ImportCall(ILOpcode opcode, int token)
        {
            // Strip runtime determined characteristics off of the method (because that's how RyuJIT operates)
            var runtimeDeterminedMethod = (MethodDesc)_methodIL.GetObject(token);
            MethodDesc method = runtimeDeterminedMethod;
            if (runtimeDeterminedMethod.IsRuntimeDeterminedExactMethod)
                method = runtimeDeterminedMethod.GetCanonMethodTarget(CanonicalFormKind.Specific);

            if (method.IsRawPInvoke())
            {
                // Raw P/invokes don't have any dependencies.
                return;
            }

            string reason = null;
            switch (opcode)
            {
                case ILOpcode.newobj:
                    reason = "newobj"; break;
                case ILOpcode.call:
                    reason = "call"; break;
                case ILOpcode.callvirt:
                    reason = "callvirt"; break;
                case ILOpcode.ldftn:
                    reason = "ldftn"; break;
                case ILOpcode.ldvirtftn:
                    reason = "ldvirtftn"; break;
                default:
                    Debug.Assert(false); break;
            }

            if (opcode == ILOpcode.newobj && !method.OwningType.IsString /* String .ctor handled below */)
            {
                TypeDesc owningType = method.OwningType;
                if (owningType.IsRuntimeDeterminedSubtype)
                {
                    _dependencies.Add(GetGenericLookupHelper(ReadyToRunHelperId.TypeHandle, owningType), "newobj");
                }
                else
                {
                    _dependencies.Add(_factory.ConstructedTypeSymbol(owningType), "newobj");
                }

                _dependencies.Add(GetHelperEntrypoint(ReadyToRunHelper.NewObject), "newobj");
            }

            TypeDesc exactType = method.OwningType;

            bool resolvedConstraint = false;
            bool forceUseRuntimeLookup = false;

            MethodDesc methodAfterConstraintResolution = method;
            if (_constrained != null)
            {
                // We have a "constrained." call.  Try a partial resolve of the constraint call.  Note that this
                // will not necessarily resolve the call exactly, since we might be compiling
                // shared generic code - it may just resolve it to a candidate suitable for
                // JIT compilation, and require a runtime lookup for the actual code pointer
                // to call.

                MethodDesc directMethod = _constrained.GetClosestDefType().TryResolveConstraintMethodApprox(method.OwningType, method, out forceUseRuntimeLookup);
                if (directMethod == null && _constrained.IsEnum)
                {
                    // Constrained calls to methods on enum methods resolve to System.Enum's methods. System.Enum is a reference
                    // type though, so we would fail to resolve and box. We have a special path for those to avoid boxing.
                    directMethod = _compilation.TypeSystemContext.TryResolveConstrainedEnumMethod(_constrained, method);
                }
                
                if (directMethod != null)
                {
                    // Either
                    //    1. no constraint resolution at compile time (!directMethod)
                    // OR 2. no code sharing lookup in call
                    // OR 3. we have have resolved to an instantiating stub

                    methodAfterConstraintResolution = directMethod;

                    Debug.Assert(!methodAfterConstraintResolution.OwningType.IsInterface);
                    resolvedConstraint = true;

                    exactType = _constrained;
                }
                else if (_constrained.IsValueType)
                {
                    AddBoxingDependencies(_constrained, reason);
                }

                _constrained = null;
            }

            MethodDesc targetMethod = methodAfterConstraintResolution;

            //
            // Determine whether to perform direct call
            //

            bool directCall = false;

            if (targetMethod.Signature.IsStatic)
            {
                // Static methods are always direct calls
                directCall = true;
            }
            else if (targetMethod.OwningType.IsInterface)
            {
                // Force all interface calls to be interpreted as if they are virtual.
                directCall = false;
            }
            else if ((opcode != ILOpcode.callvirt && opcode != ILOpcode.ldvirtftn) || resolvedConstraint)
            {
                directCall = true;
            }
            else
            {
                if (!targetMethod.IsVirtual || targetMethod.IsFinal || targetMethod.OwningType.IsSealed())
                {
                    directCall = true;
                }
            }

            bool allowInstParam = opcode != ILOpcode.ldvirtftn && opcode != ILOpcode.ldftn;

            if (directCall && !allowInstParam && targetMethod.GetCanonMethodTarget(CanonicalFormKind.Specific).RequiresInstArg())
            {
                // Needs a single address to call this method but the method needs a hidden argument.
                // We need a fat function pointer for this that captures both things.

                if (runtimeDeterminedMethod.IsRuntimeDeterminedExactMethod)
                {
                    _dependencies.Add(GetGenericLookupHelper(ReadyToRunHelperId.MethodEntry, runtimeDeterminedMethod), reason);
                }
                else
                {
                    _dependencies.Add(_factory.FatFunctionPointer(runtimeDeterminedMethod), reason);
                }
            }
            else if (directCall)
            {
                if (targetMethod.IsIntrinsic)
                {
                    // If this is an intrinsic method with a callsite-specific expansion, this will replace
                    // the method with a method the intrinsic expands into. If it's not the special intrinsic,
                    // method stays unchanged.
                    targetMethod = _compilation.ExpandIntrinsicForCallsite(targetMethod, _canonMethod);
                }

                MethodDesc concreteMethod = targetMethod;
                targetMethod = targetMethod.GetCanonMethodTarget(CanonicalFormKind.Specific);

                if (targetMethod.IsConstructor && targetMethod.OwningType.IsString)
                {
                    _dependencies.Add(_factory.StringAllocator(targetMethod), reason);
                }
                else if (runtimeDeterminedMethod.IsRuntimeDeterminedExactMethod)
                {
                    if (targetMethod.IsSharedByGenericInstantiations && !resolvedConstraint)
                    {
                        _dependencies.Add(_factory.RuntimeDeterminedMethod(runtimeDeterminedMethod), reason);
                    }
                    else
                    {
                        Debug.Assert(!forceUseRuntimeLookup);
                        _dependencies.Add(_factory.MethodEntrypoint(targetMethod), reason);
                    }
                }
                else
                {
                    ISymbolNode instParam = null;

                    if (targetMethod.RequiresInstMethodDescArg())
                    {
                        instParam = _compilation.NodeFactory.MethodGenericDictionary(concreteMethod);
                    }
                    else if (targetMethod.RequiresInstMethodTableArg())
                    {
                        // Ask for a constructed type symbol because we need the vtable to get to the dictionary
                        instParam = _compilation.NodeFactory.ConstructedTypeSymbol(concreteMethod.OwningType);
                    }

                    if (instParam != null)
                    {
                        _dependencies.Add(instParam, reason);
                        _dependencies.Add(_compilation.NodeFactory.ShadowConcreteMethod(concreteMethod), reason);
                    }
                    else if (targetMethod.AcquiresInstMethodTableFromThis())
                    {
                        _dependencies.Add(_compilation.NodeFactory.ShadowConcreteMethod(concreteMethod), reason);
                    }
                    else
                    {
                        _dependencies.Add(_compilation.NodeFactory.MethodEntrypoint(targetMethod), reason);
                    }
                }
            }
            else if (method.HasInstantiation)
            {
                // Generic virtual method call

                if (runtimeDeterminedMethod.IsRuntimeDeterminedExactMethod)
                {
                    _dependencies.Add(GetGenericLookupHelper(ReadyToRunHelperId.MethodHandle, runtimeDeterminedMethod), reason);
                }
                else
                {
                    _dependencies.Add(_factory.RuntimeMethodHandle(runtimeDeterminedMethod), reason);
                }
            }
            else
            {
                ReadyToRunHelperId helper;
                if (opcode == ILOpcode.ldvirtftn)
                {
                    helper = ReadyToRunHelperId.ResolveVirtualFunction;
                }
                else
                {
                    Debug.Assert(opcode == ILOpcode.callvirt);
                    helper = ReadyToRunHelperId.VirtualCall;
                }

                if (runtimeDeterminedMethod.IsRuntimeDeterminedExactMethod && targetMethod.OwningType.IsInterface)
                {
                    _dependencies.Add(GetGenericLookupHelper(helper, runtimeDeterminedMethod), reason);
                }
                else
                {
                    // Get the slot defining method to make sure our virtual method use tracking gets this right.
                    // For normal C# code the targetMethod will always be newslot.
                    MethodDesc slotDefiningMethod = targetMethod.IsNewSlot ?
                        targetMethod : MetadataVirtualMethodAlgorithm.FindSlotDefiningMethodForVirtualMethod(targetMethod);

                    _dependencies.Add(_factory.ReadyToRunHelper(helper, slotDefiningMethod), reason);
                }
            }
        }

        private void ImportLdFtn(int token, ILOpcode opCode)
        {
            ImportCall(opCode, token);
        }
        
        private void ImportBranch(ILOpcode opcode, BasicBlock target, BasicBlock fallthrough)
        {
            ImportFallthrough(target);

            if (fallthrough != null)
                ImportFallthrough(fallthrough);
        }

        private void ImportSwitchJump(int jmpBase, int[] jmpDelta, BasicBlock fallthrough)
        {
            for (int i = 0; i < jmpDelta.Length; i++)
            {
                BasicBlock target = _basicBlocks[jmpBase + jmpDelta[i]];
                ImportFallthrough(target);
            }

            if (fallthrough != null)
                ImportFallthrough(fallthrough);
        }

        private void ImportUnbox(int token, ILOpcode opCode)
        {
            TypeDesc type = (TypeDesc)_methodIL.GetObject(token);

            if (!type.IsValueType)
                return;

            if (type.IsRuntimeDeterminedSubtype)
            {
                _dependencies.Add(GetGenericLookupHelper(ReadyToRunHelperId.TypeHandle, type), "Unbox");
            }
            else
            {
                _dependencies.Add(_factory.NecessaryTypeSymbol(type), "Unbox");
            }

            ReadyToRunHelper helper;
            if (opCode == ILOpcode.unbox)
            {
                helper = ReadyToRunHelper.Unbox;
            }
            else
            {
                Debug.Assert(opCode == ILOpcode.unbox_any);
                helper = ReadyToRunHelper.Unbox_Nullable;
            }

            _dependencies.Add(GetHelperEntrypoint(helper), "Unbox");
        }

        private void ImportRefAnyVal(int token)
        {
            throw new NotImplementedException();
        }

        private void ImportMkRefAny(int token)
        {
            throw new NotImplementedException();
        }

        private void ImportLdToken(int token)
        {
            object obj = _methodIL.GetObject(token);

            if (obj is TypeDesc)
            {
                var type = (TypeDesc)obj;
                if (type.IsRuntimeDeterminedSubtype)
                {
                    _dependencies.Add(GetGenericLookupHelper(ReadyToRunHelperId.TypeHandle, type), "ldtoken");
                }
                else
                {
                    if (ConstructedEETypeNode.CreationAllowed(type))
                        _dependencies.Add(_factory.ConstructedTypeSymbol(type), "ldtoken");
                    else
                        _dependencies.Add(_factory.NecessaryTypeSymbol(type), "ldtoken");
                }
            }
            else if (obj is MethodDesc)
            {
                var method = (MethodDesc)obj;
                if (method.IsRuntimeDeterminedExactMethod)
                {
                    _dependencies.Add(GetGenericLookupHelper(ReadyToRunHelperId.MethodHandle, method), "ldtoken");
                }
                else
                {
                    _dependencies.Add(_factory.RuntimeMethodHandle(method), "ldtoken");
                }
            }
            else
            {
                Debug.Assert(obj is FieldDesc);

                var field = (FieldDesc)obj;
                if (field.OwningType.IsRuntimeDeterminedSubtype)
                {
                    _dependencies.Add(GetGenericLookupHelper(ReadyToRunHelperId.FieldHandle, field), "ldtoken");
                }
                else
                {
                    _dependencies.Add(_factory.RuntimeFieldHandle(field), "ldtoken");
                }
            }
        }

        private void ImportRefAnyType()
        {
            throw new NotImplementedException();
        }

        private void ImportArgList()
        {
            throw new NotImplementedException();
        }

        private void ImportConstrainedPrefix(int token)
        {
            _constrained = (TypeDesc)_methodIL.GetObject(token);
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
            // If we care, this can include allocating the frozen string node.
        }

        private void ImportBox(int token)
        {
            AddBoxingDependencies((TypeDesc)_methodIL.GetObject(token), "Box");
        }

        private void AddBoxingDependencies(TypeDesc type, string reason)
        {
            if (type.IsRuntimeDeterminedSubtype)
            {
                _dependencies.Add(GetGenericLookupHelper(ReadyToRunHelperId.TypeHandle, type), reason);
            }
            else
            {
                _dependencies.Add(_factory.ConstructedTypeSymbol(type), reason);
            }

            if (type.IsNullable)
            {
                _dependencies.Add(GetHelperEntrypoint(ReadyToRunHelper.Box), reason);
            }
            else
            {
                _dependencies.Add(GetHelperEntrypoint(ReadyToRunHelper.Box_Nullable), reason);
            }
        }

        private void ImportLeave(BasicBlock target)
        {
            ImportFallthrough(target);
        }

        private void ImportNewArray(int token)
        {
            var type = ((TypeDesc)_methodIL.GetObject(token)).MakeArrayType();
            if (type.IsRuntimeDeterminedSubtype)
            {
                _dependencies.Add(GetGenericLookupHelper(ReadyToRunHelperId.TypeHandle, type), "newarr");
                _dependencies.Add(GetHelperEntrypoint(ReadyToRunHelper.NewArray), "newarr");
            }
            else
            {
                _dependencies.Add(_factory.ReadyToRunHelper(ReadyToRunHelperId.NewArr1, type), "newarr");
            }
        }

        private void ImportFallthrough(BasicBlock next)
        {
            MarkBasicBlock(next);
        }

        private TypeDesc GetWellKnownType(WellKnownType wellKnownType)
        {
            return _compilation.TypeSystemContext.GetWellKnownType(wellKnownType);
        }

        private void ImportNop() { }
        private void ImportBreak() { }
        private void ImportLoadVar(int index, bool argument) { }
        private void ImportStoreVar(int index, bool argument) { }
        private void ImportAddressOfVar(int index, bool argument) { }
        private void ImportDup() { }
        private void ImportPop() { }
        private void ImportCalli(int token) { }
        private void ImportLoadNull() { }
        private void ImportReturn() { }
        private void ImportLoadInt(long value, StackValueKind kind) { }
        private void ImportLoadFloat(double value) { }
        private void ImportLoadIndirect(int token) { }
        private void ImportLoadIndirect(TypeDesc type) { }
        private void ImportStoreIndirect(int token) { }
        private void ImportStoreIndirect(TypeDesc type) { }
        private void ImportBinaryOperation(ILOpcode opcode) { }
        private void ImportShiftOperation(ILOpcode opcode) { }
        private void ImportCompareOperation(ILOpcode opcode) { }
        private void ImportConvert(WellKnownType wellKnownType, bool checkOverflow, bool unsigned) { }
        private void ImportUnaryOperation(ILOpcode opCode) { }
        private void ImportCpOpj(int token) { }
        private void ImportCkFinite() { }
        private void ImportLocalAlloc() { }
        private void ImportEndFilter() { }
        private void ImportCpBlk() { }
        private void ImportInitBlk() { }
        private void ImportRethrow() { }
        private void ImportSizeOf(int token) { }
        private void ImportUnalignedPrefix(byte alignment) { }
        private void ImportVolatilePrefix() { }
        private void ImportTailPrefix() { }
        private void ImportNoPrefix(byte mask) { }
        private void ImportReadOnlyPrefix() { }
        private void ImportThrow() { }
        private void ImportInitObj(int token) { }
        private void ImportLoadElement(int token) { }
        private void ImportLoadElement(TypeDesc elementType) { }
        private void ImportStoreElement(int token) { }
        private void ImportStoreElement(TypeDesc elementType) { }
        private void ImportLoadLength() { }
        private void ImportAddressOfElement(int token) { }
        private void ImportEndFinally() { }
    }
}
