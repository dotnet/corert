﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using ILCompiler.DependencyAnalysis.ReadyToRun;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    using ReadyToRunHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper;

    public sealed partial class ReadyToRunSymbolNodeFactory
    {

        private readonly Dictionary<ModuleToken, ISymbolNode> _importStrings;

        private readonly ReadyToRunCodegenNodeFactory _codegenNodeFactory;
  

        public ReadyToRunSymbolNodeFactory(ReadyToRunCodegenNodeFactory codegenNodeFactory)
        {
            _codegenNodeFactory = codegenNodeFactory;
            _importStrings = new Dictionary<ModuleToken, ISymbolNode>();
        }

        public ISymbolNode StringLiteral(ModuleToken token)
        {
            if (!_importStrings.TryGetValue(token, out ISymbolNode stringNode))
            {
                stringNode = new StringImport(_codegenNodeFactory.StringImports, token);
                _importStrings.Add(token, stringNode);
            }
            return stringNode;
        }

        private readonly Dictionary<ReadyToRunHelperId, Dictionary<object, ISymbolNode>> _r2rHelpers = new Dictionary<ReadyToRunHelperId, Dictionary<object, ISymbolNode>>();

        public ISymbolNode ReadyToRunHelper(ReadyToRunHelperId id, object target, SignatureContext signatureContext)
        {
            if (id == ReadyToRunHelperId.NecessaryTypeHandle)
            {
                // We treat TypeHandle and NecessaryTypeHandle the same - don't emit two copies of the same import
                id = ReadyToRunHelperId.TypeHandle;
            }

            if (!_r2rHelpers.TryGetValue(id, out Dictionary<object, ISymbolNode> helperNodeMap))
            {
                helperNodeMap = new Dictionary<object, ISymbolNode>();
                _r2rHelpers.Add(id, helperNodeMap);
            }

            if (helperNodeMap.TryGetValue(target, out ISymbolNode helperNode))
            {
                return helperNode;
            }

            switch (id)
            {
                case ReadyToRunHelperId.NewHelper:
                    helperNode = CreateNewHelper((TypeDesc)target, signatureContext);
                    break;

                case ReadyToRunHelperId.NewArr1:
                    helperNode = CreateNewArrayHelper((ArrayType)target, signatureContext);
                    break;

                case ReadyToRunHelperId.GetGCStaticBase:
                    helperNode = CreateGCStaticBaseHelper((TypeDesc)target, signatureContext);
                    break;

                case ReadyToRunHelperId.GetNonGCStaticBase:
                    helperNode = CreateNonGCStaticBaseHelper((TypeDesc)target, signatureContext);
                    break;

                case ReadyToRunHelperId.GetThreadStaticBase:
                    helperNode = CreateThreadGcStaticBaseHelper((TypeDesc)target, signatureContext);
                    break;

                case ReadyToRunHelperId.GetThreadNonGcStaticBase:
                    helperNode = CreateThreadNonGcStaticBaseHelper((TypeDesc)target, signatureContext);
                    break;

                case ReadyToRunHelperId.IsInstanceOf:
                    helperNode = CreateIsInstanceOfHelper((TypeDesc)target, signatureContext);
                    break;

                case ReadyToRunHelperId.CastClass:
                    helperNode = CreateCastClassHelper((TypeDesc)target, signatureContext);
                    break;

                case ReadyToRunHelperId.TypeHandle:
                    helperNode = CreateTypeHandleHelper((TypeDesc)target, signatureContext);
                    break;

                case ReadyToRunHelperId.FieldHandle:
                    helperNode = CreateFieldHandleHelper((FieldDesc)target, signatureContext);
                    break;

                case ReadyToRunHelperId.VirtualCall:
                    helperNode = CreateVirtualCallHelper((MethodDesc)target, signatureContext);
                    break;

                case ReadyToRunHelperId.DelegateCtor:
                    helperNode = CreateDelegateCtorHelper((DelegateCreationInfo)target, signatureContext);
                    break;

                default:
                    throw new NotImplementedException();
            }

            helperNodeMap.Add(target, helperNode);
            return helperNode;
        }

        private ISymbolNode CreateNewHelper(TypeDesc type, SignatureContext signatureContext)
        {
            return new DelayLoadHelperImport(
                _codegenNodeFactory,
                _codegenNodeFactory.HelperImports,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper,
                new NewObjectFixupSignature(type, signatureContext));
        }

        private ISymbolNode CreateNewArrayHelper(ArrayType type, SignatureContext signatureContext)
        {
            return new DelayLoadHelperImport(
                _codegenNodeFactory,
                _codegenNodeFactory.HelperImports,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper,
                new NewArrayFixupSignature(type, signatureContext));
        }

        private ISymbolNode CreateGCStaticBaseHelper(TypeDesc type, SignatureContext signatureContext)
        {
            return new DelayLoadHelperImport(
                _codegenNodeFactory,
                _codegenNodeFactory.HelperImports,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper,
                new TypeFixupSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_StaticBaseGC, type, signatureContext));
        }

        private ISymbolNode CreateNonGCStaticBaseHelper(TypeDesc type, SignatureContext signatureContext)
        {
            return new DelayLoadHelperImport(
                _codegenNodeFactory,
                _codegenNodeFactory.HelperImports,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper,
                new TypeFixupSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_StaticBaseNonGC, type, signatureContext));
        }

        private ISymbolNode CreateThreadGcStaticBaseHelper(TypeDesc type, SignatureContext signatureContext)
        {
            return new DelayLoadHelperImport(
                _codegenNodeFactory,
                _codegenNodeFactory.HelperImports,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper,
                new TypeFixupSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_ThreadStaticBaseGC, type, signatureContext));
        }

        private ISymbolNode CreateThreadNonGcStaticBaseHelper(TypeDesc type, SignatureContext signatureContext)
        {
            return new DelayLoadHelperImport(
                _codegenNodeFactory,
                _codegenNodeFactory.HelperImports,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper,
                new TypeFixupSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_ThreadStaticBaseNonGC, type, signatureContext));
        }

        private ISymbolNode CreateIsInstanceOfHelper(TypeDesc type, SignatureContext signatureContext)
        {
            return new DelayLoadHelperImport(
                _codegenNodeFactory,
                _codegenNodeFactory.HelperImports,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper,
                new TypeFixupSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_IsInstanceOf, type, signatureContext));
        }

        private ISymbolNode CreateCastClassHelper(TypeDesc type, SignatureContext signatureContext)
        {
            return new DelayLoadHelperImport(
                _codegenNodeFactory,
                _codegenNodeFactory.HelperImports,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper_Obj,
                new TypeFixupSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_ChkCast, type, signatureContext));
        }

        private ISymbolNode CreateTypeHandleHelper(TypeDesc type, SignatureContext signatureContext)
        {
            return new PrecodeHelperImport(
                _codegenNodeFactory,
                new TypeFixupSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_TypeHandle, type, signatureContext));
        }

        private ISymbolNode CreateFieldHandleHelper(FieldDesc field, SignatureContext signatureContext)
        {
            return new PrecodeHelperImport(
                _codegenNodeFactory,
                new FieldFixupSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_FieldHandle, field, signatureContext));
        }

        private ISymbolNode CreateVirtualCallHelper(MethodDesc method, SignatureContext signatureContext)
        {
            return new DelayLoadHelperImport(
                _codegenNodeFactory,
                _codegenNodeFactory.DispatchImports,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper_Obj,
                _codegenNodeFactory.MethodSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_VirtualEntry, method,
                    constrainedType: null, signatureContext: signatureContext, isUnboxingStub: false, isInstantiatingStub: false));
        }

        private ISymbolNode CreateDelegateCtorHelper(DelegateCreationInfo info, SignatureContext signatureContext)
        {
            return info.Constructor;
        }

        Dictionary<ILCompiler.ReadyToRunHelper, ISymbolNode> _helperCache = new Dictionary<ILCompiler.ReadyToRunHelper, ISymbolNode>();

        public ISymbolNode ExternSymbol(ILCompiler.ReadyToRunHelper helper)
        {
            ISymbolNode result;
            if (_helperCache.TryGetValue(helper, out result))
            {
                return result;
            }

            ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper r2rHelper;
            switch (helper)
            {
                // Exception handling helpers
                case ILCompiler.ReadyToRunHelper.Throw:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Throw;
                    break;

                case ILCompiler.ReadyToRunHelper.Rethrow:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Rethrow;
                    break;

                case ILCompiler.ReadyToRunHelper.Overflow:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Overflow;
                    break;

                case ILCompiler.ReadyToRunHelper.RngChkFail:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_RngChkFail;
                    break;

                case ILCompiler.ReadyToRunHelper.FailFast:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_FailFast;
                    break;

                case ILCompiler.ReadyToRunHelper.ThrowNullRef:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_ThrowNullRef;
                    break;

                case ILCompiler.ReadyToRunHelper.ThrowDivZero:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_ThrowDivZero;
                    break;

                // Write barriers
                case ILCompiler.ReadyToRunHelper.WriteBarrier:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_WriteBarrier;
                    break;

                case ILCompiler.ReadyToRunHelper.CheckedWriteBarrier:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_CheckedWriteBarrier;
                    break;

                case ILCompiler.ReadyToRunHelper.ByRefWriteBarrier:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_ByRefWriteBarrier;
                    break;

                // Array helpers
                case ILCompiler.ReadyToRunHelper.Stelem_Ref:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Stelem_Ref;
                    break;

                case ILCompiler.ReadyToRunHelper.Ldelema_Ref:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Ldelema_Ref;
                    break;

                case ILCompiler.ReadyToRunHelper.MemSet:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_MemSet;
                    break;

                case ILCompiler.ReadyToRunHelper.MemCpy:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_MemCpy;
                    break;

                // Get string handle lazily
                case ILCompiler.ReadyToRunHelper.GetString:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_GetString;
                    break;

                // Reflection helpers
                case ILCompiler.ReadyToRunHelper.GetRuntimeTypeHandle:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_GetRuntimeTypeHandle;
                    break;

                case ILCompiler.ReadyToRunHelper.GetRuntimeMethodHandle:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_GetRuntimeMethodHandle;
                    break;

                case ILCompiler.ReadyToRunHelper.GetRuntimeFieldHandle:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_GetRuntimeFieldHandle;
                    break;

                case ILCompiler.ReadyToRunHelper.Box:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Box;
                    break;

                case ILCompiler.ReadyToRunHelper.Box_Nullable:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Box_Nullable;
                    break;

                case ILCompiler.ReadyToRunHelper.Unbox:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Unbox;
                    break;

                case ILCompiler.ReadyToRunHelper.Unbox_Nullable:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Unbox_Nullable;
                    break;

                case ILCompiler.ReadyToRunHelper.NewMultiDimArr:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_NewMultiDimArr;
                    break;

                case ILCompiler.ReadyToRunHelper.NewMultiDimArr_NonVarArg:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_NewMultiDimArr_NonVarArg;
                    break;

                // Helpers used with generic handle lookup cases
                case ILCompiler.ReadyToRunHelper.NewObject:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_NewObject;
                    break;

                case ILCompiler.ReadyToRunHelper.NewArray:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_NewArray;
                    break;

                case ILCompiler.ReadyToRunHelper.CheckCastAny:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_CheckCastAny;
                    break;

                case ILCompiler.ReadyToRunHelper.CheckInstanceAny:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_CheckInstanceAny;
                    break;

                case ILCompiler.ReadyToRunHelper.GenericGcStaticBase:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_GenericGcStaticBase;
                    break;

                case ILCompiler.ReadyToRunHelper.GenericNonGcStaticBase:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_GenericNonGcStaticBase;
                    break;

                case ILCompiler.ReadyToRunHelper.GenericGcTlsBase:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_GenericGcTlsBase;
                    break;

                case ILCompiler.ReadyToRunHelper.GenericNonGcTlsBase:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_GenericNonGcTlsBase;
                    break;

                case ILCompiler.ReadyToRunHelper.VirtualFuncPtr:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_VirtualFuncPtr;
                    break;

                // Long mul/div/shift ops
                case ILCompiler.ReadyToRunHelper.LMul:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_LMul;
                    break;

                case ILCompiler.ReadyToRunHelper.LMulOfv:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_LMulOfv;
                    break;

                case ILCompiler.ReadyToRunHelper.ULMulOvf:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_ULMulOvf;
                    break;

                case ILCompiler.ReadyToRunHelper.LDiv:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_LDiv;
                    break;

                case ILCompiler.ReadyToRunHelper.LMod:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_LMod;
                    break;

                case ILCompiler.ReadyToRunHelper.ULDiv:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_ULDiv;
                    break;

                case ILCompiler.ReadyToRunHelper.ULMod:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_ULMod;
                    break;

                case ILCompiler.ReadyToRunHelper.LLsh:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_LLsh;
                    break;

                case ILCompiler.ReadyToRunHelper.LRsh:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_LRsh;
                    break;

                case ILCompiler.ReadyToRunHelper.LRsz:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_LRsz;
                    break;

                case ILCompiler.ReadyToRunHelper.Lng2Dbl:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Lng2Dbl;
                    break;

                case ILCompiler.ReadyToRunHelper.ULng2Dbl:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_ULng2Dbl;
                    break;

                // 32-bit division helpers
                case ILCompiler.ReadyToRunHelper.Div:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Div;
                    break;

                case ILCompiler.ReadyToRunHelper.Mod:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Mod;
                    break;

                case ILCompiler.ReadyToRunHelper.UDiv:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_UDiv;
                    break;

                case ILCompiler.ReadyToRunHelper.UMod:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_UMod;
                    break;

                // Floating point conversions
                case ILCompiler.ReadyToRunHelper.Dbl2Int:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Dbl2Int;
                    break;

                case ILCompiler.ReadyToRunHelper.Dbl2IntOvf:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Dbl2IntOvf;
                    break;

                case ILCompiler.ReadyToRunHelper.Dbl2Lng:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Dbl2Lng;
                    break;

                case ILCompiler.ReadyToRunHelper.Dbl2LngOvf:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Dbl2LngOvf;
                    break;

                case ILCompiler.ReadyToRunHelper.Dbl2UInt:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Dbl2UInt;
                    break;

                case ILCompiler.ReadyToRunHelper.Dbl2UIntOvf:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Dbl2UIntOvf;
                    break;

                case ILCompiler.ReadyToRunHelper.Dbl2ULng:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Dbl2ULng;
                    break;

                case ILCompiler.ReadyToRunHelper.Dbl2ULngOvf:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Dbl2ULngOvf;
                    break;

                // Floating point ops
                case ILCompiler.ReadyToRunHelper.DblRem:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DblRem;
                    break;

                case ILCompiler.ReadyToRunHelper.FltRem:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_FltRem;
                    break;

                case ILCompiler.ReadyToRunHelper.DblRound:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DblRound;
                    break;

                case ILCompiler.ReadyToRunHelper.FltRound:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_FltRound;
                    break;

                case ILCompiler.ReadyToRunHelper.GetRefAny:
                    // TODO-PERF: currently not implemented in Crossgen
                    ThrowHelper.ThrowInvalidProgramException();
                    // ThrowInvalidProgramException should never return
                    throw new NotImplementedException();

                default:
                    throw new NotImplementedException(helper.ToString());
            }

            result = _codegenNodeFactory.GetReadyToRunHelperCell(r2rHelper);
            _helperCache.Add(helper, result);
            return result;
        }

        public ISymbolNode HelperMethodEntrypoint(ILCompiler.ReadyToRunHelper helperId, MethodDesc method)
        {
            return ExternSymbol(helperId);
        }

        readonly Dictionary<MethodAndCallSite, ISymbolNode> _interfaceDispatchCells = new Dictionary<MethodAndCallSite, ISymbolNode>();

        public ISymbolNode InterfaceDispatchCell(MethodDesc method, SignatureContext signatureContext, bool isUnboxingStub, string callSite)
        {
            MethodAndCallSite cellKey = new MethodAndCallSite(method, callSite);
            if (!_interfaceDispatchCells.TryGetValue(cellKey, out ISymbolNode dispatchCell))
            {
                dispatchCell = new DelayLoadHelperImport(
                    _codegenNodeFactory,
                    _codegenNodeFactory.DispatchImports,
                    ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_MethodCall |
                    ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_FLAG_VSD,
                    _codegenNodeFactory.MethodSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_VirtualEntry, method,
                        null, signatureContext, isUnboxingStub, isInstantiatingStub: false),
                    callSite);

                _interfaceDispatchCells.Add(cellKey, dispatchCell);
            }
            return dispatchCell;
        }

        public ISymbolNode ComputeConstantLookup(ReadyToRunHelperId helperId, object entity, SignatureContext signatureContext)
        {
            return ReadyToRunHelper(helperId, entity, signatureContext);
        }

        readonly Dictionary<MethodDesc, ISortableSymbolNode> _genericDictionaryCache = new Dictionary<MethodDesc, ISortableSymbolNode>();

        public ISortableSymbolNode MethodGenericDictionary(MethodDesc method, SignatureContext signatureContext)
        {
            if (!_genericDictionaryCache.TryGetValue(method, out ISortableSymbolNode genericDictionary))
            {
                genericDictionary = new PrecodeHelperImport(
                    _codegenNodeFactory,
                    _codegenNodeFactory.MethodSignature(
                        ReadyToRunFixupKind.READYTORUN_FIXUP_MethodDictionary,
                        method,
                        constrainedType: null,
                        signatureContext: signatureContext,
                        isUnboxingStub: false,
                        isInstantiatingStub: true));
                _genericDictionaryCache.Add(method, genericDictionary);
            }
            return genericDictionary;
        }

        readonly Dictionary<TypeDesc, ISymbolNode> _constructedTypeSymbols = new Dictionary<TypeDesc, ISymbolNode>();

        public ISymbolNode ConstructedTypeSymbol(TypeDesc type, SignatureContext signatureContext)
        {
            if (!_constructedTypeSymbols.TryGetValue(type, out ISymbolNode symbol))
            {
                symbol = new PrecodeHelperImport(
                    _codegenNodeFactory,
                    new TypeFixupSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_TypeDictionary, type, signatureContext));
                _constructedTypeSymbols.Add(type, symbol);
            }
            return symbol;
        }

        private readonly Dictionary<TypeAndMethod, ISymbolNode> _delegateCtors = new Dictionary<TypeAndMethod, ISymbolNode>();

        public ISymbolNode DelegateCtor(TypeDesc delegateType, MethodDesc targetMethod, SignatureContext signatureContext)
        {
            TypeAndMethod ctorKey = new TypeAndMethod(delegateType, targetMethod, isUnboxingStub: false, isInstantiatingStub: false);
            if (!_delegateCtors.TryGetValue(ctorKey, out ISymbolNode ctorNode))
            {
                IMethodNode targetMethodNode = _codegenNodeFactory.MethodEntrypoint(targetMethod, constrainedType: null, originalMethod: null, signatureContext: signatureContext, isUnboxingStub: false);

                ctorNode = new DelayLoadHelperImport(
                    _codegenNodeFactory,
                    _codegenNodeFactory.HelperImports,
                    ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper,
                    new DelegateCtorSignature(delegateType, targetMethodNode, signatureContext));
                _delegateCtors.Add(ctorKey, ctorNode);
            }
            return ctorNode;
        }

        struct MethodAndCallSite : IEquatable<MethodAndCallSite>
        {
            public readonly MethodDesc Method;
            public readonly string CallSite;

            public MethodAndCallSite(MethodDesc method, string callSite)
            {
                CallSite = callSite;
                Method = method;
            }

            public bool Equals(MethodAndCallSite other)
            {
                return CallSite == other.CallSite && Method == other.Method;
            }

            public override bool Equals(object obj)
            {
                return obj is MethodAndCallSite other && Equals(other);
            }

            public override int GetHashCode()
            {
                return (CallSite != null ? CallSite.GetHashCode() : 0) + unchecked(31 * Method.GetHashCode());
            }
        }
    }
}
