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

        public ILImporter(ILScanner compilation, MethodIL methodIL)
        {
            _compilation = compilation;
            _factory = (ILScanNodeFactory)compilation.NodeFactory;
            
            _ilBytes = methodIL.GetILBytes();

            // Get the runtime determined method IL so that this works right in shared code
            // and tokens in shared code resolve to runtime determined types.
            MethodDesc method = methodIL.OwningMethod;
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
                Debug.Assert(_canonMethod.RequiresInstArg());
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

            if (type.IsRuntimeDeterminedSubtype)
            {
                _dependencies.Add(GetGenericLookupHelper(helperId, type), "IsInst/CastClass");
            }
            else
            {
                _dependencies.Add(_factory.ReadyToRunHelper(helperId, type), "IsInst/CastClass");
            }
        }
        
        private void ImportCall(ILOpcode opcode, int token)
        {
            var method = (MethodDesc)_methodIL.GetObject(token);

            if (opcode == ILOpcode.newobj)
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

            /*if (_constrained != null)
            {
                bool forceUseRuntimeLookup;
                MethodDesc directMethod = _constrained.GetClosestDefType().TryResolveConstraintMethodApprox(method.OwningType, method, out forceUseRuntimeLookup);
                if (directMethod == null && _constrained.IsEnum)
                {
                    // Constrained calls to methods on enum methods resolve to System.Enum's methods. System.Enum is a reference
                    // type though, so we would fail to resolve and box. We have a special path for those to avoid boxing.
                    directMethod = _compilation.TypeSystemContext.TryResolveConstrainedEnumMethod(_constrained, method);
                }
                _constrained = null;
            }*/
        }

        private void ImportLdFtn(int token, ILOpcode opCode)
        {
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
        }

        private void ImportLeave(BasicBlock target)
        {
            ImportFallthrough(target);
        }

        private void ImportNewArray(int token)
        {
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
