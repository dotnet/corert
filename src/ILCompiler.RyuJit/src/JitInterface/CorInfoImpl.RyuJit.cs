// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

using Internal.IL;
using Internal.TypeSystem;
using Internal.ReadyToRunConstants;

using ILCompiler;
using ILCompiler.DependencyAnalysis;

#if SUPPORT_JIT
using MethodCodeNode = Internal.Runtime.JitSupport.JitMethodCodeNode;
using RyuJitCompilation = ILCompiler.Compilation;
#endif

namespace Internal.JitInterface
{
    unsafe partial class CorInfoImpl
    {
        private struct SequencePoint
        {
            public string Document;
            public int LineNumber;
        }

        private const CORINFO_RUNTIME_ABI TargetABI = CORINFO_RUNTIME_ABI.CORINFO_CORERT_ABI;

        private uint OffsetOfDelegateFirstTarget => (uint)(4 * PointerSize); // Delegate::m_functionPointer
        private int SizeOfReversePInvokeTransitionFrame => 2 * PointerSize;

        private RyuJitCompilation _compilation;
        private MethodCodeNode _methodCodeNode;
        private DebugLocInfo[] _debugLocInfos;
        private DebugVarInfo[] _debugVarInfos;
        private Dictionary<int, SequencePoint> _sequencePoints;
        private Dictionary<uint, ILLocalVariable> _localSlotToInfoMap;
        private Dictionary<uint, string> _parameterIndexToNameMap;
        private TypeDesc[] _variableToTypeDesc;
        private readonly UnboxingMethodDescFactory _unboxingThunkFactory = new UnboxingMethodDescFactory();

        public CorInfoImpl(RyuJitCompilation compilation)
            : this()
        {
            _compilation = compilation;
        }

        private MethodDesc getUnboxingThunk(MethodDesc method)
        {
            return _unboxingThunkFactory.GetUnboxingMethod(method);
        }

        public void CompileMethod(MethodCodeNode methodCodeNodeNeedingCode, MethodIL methodIL = null)
        {
            _methodCodeNode = methodCodeNodeNeedingCode;

            try
            {
                CompileMethodInternal(methodCodeNodeNeedingCode, methodIL);
            }
            finally
            {
                CompileMethodCleanup();
            }
        }

        private CORINFO_RUNTIME_LOOKUP_KIND GetLookupKindFromContextSource(GenericContextSource contextSource)
        {
            switch (contextSource)
            {
                case GenericContextSource.MethodParameter:
                    return CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_METHODPARAM;
                case GenericContextSource.TypeParameter:
                    return CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_CLASSPARAM;
                default:
                    Debug.Assert(contextSource == GenericContextSource.ThisObject);
                    return CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_THISOBJ;
            }
        }

        private void ComputeLookup(ref CORINFO_RESOLVED_TOKEN pResolvedToken, object entity, ReadyToRunHelperId helperId, ref CORINFO_LOOKUP lookup)
        {
            if (_compilation.NeedsRuntimeLookup(helperId, entity))
            {
                lookup.lookupKind.needsRuntimeLookup = true;
                lookup.runtimeLookup.signature = null;

                // Do not bother computing the runtime lookup if we are inlining. The JIT is going
                // to abort the inlining attempt anyway.
                if (pResolvedToken.tokenContext != contextFromMethodBeingCompiled())
                {
                    lookup.lookupKind.runtimeLookupKind = CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_NOT_SUPPORTED;
                    return;
                }

                MethodDesc contextMethod = methodFromContext(pResolvedToken.tokenContext);

                GenericDictionaryLookup genericLookup = _compilation.ComputeGenericLookup(contextMethod, helperId, entity);

                if (genericLookup.UseHelper)
                {
                    lookup.runtimeLookup.indirections = CORINFO.USEHELPER;
                    lookup.lookupKind.runtimeLookupFlags = (ushort)genericLookup.HelperId;
                    lookup.lookupKind.runtimeLookupArgs = (void*)ObjectToHandle(genericLookup.HelperObject);
                }
                else
                {
                    if (genericLookup.ContextSource == GenericContextSource.MethodParameter)
                    {
                        lookup.runtimeLookup.helper = CorInfoHelpFunc.CORINFO_HELP_RUNTIMEHANDLE_METHOD;
                    }
                    else
                    {
                        lookup.runtimeLookup.helper = CorInfoHelpFunc.CORINFO_HELP_RUNTIMEHANDLE_CLASS;
                    }

                    lookup.runtimeLookup.indirections = (ushort)(genericLookup.NumberOfIndirections + (genericLookup.IndirectLastOffset ? 1 : 0));
                    lookup.runtimeLookup.offset0 = (IntPtr)genericLookup[0];
                    if (genericLookup.NumberOfIndirections > 1)
                    {
                        lookup.runtimeLookup.offset1 = (IntPtr)genericLookup[1];
                        if (genericLookup.IndirectLastOffset)
                            lookup.runtimeLookup.offset2 = IntPtr.Zero;
                    }
                    else if (genericLookup.IndirectLastOffset)
                    {
                        lookup.runtimeLookup.offset1 = IntPtr.Zero;
                    }
                    lookup.runtimeLookup.sizeOffset = CORINFO.CORINFO_NO_SIZE_CHECK;
                    lookup.runtimeLookup.testForFixup = false; // TODO: this will be needed in true multifile
                    lookup.runtimeLookup.testForNull = false;
                    lookup.runtimeLookup.indirectFirstOffset = false;
                    lookup.runtimeLookup.indirectSecondOffset = false;
                    lookup.lookupKind.runtimeLookupFlags = 0;
                    lookup.lookupKind.runtimeLookupArgs = null;
                }

                lookup.lookupKind.runtimeLookupKind = GetLookupKindFromContextSource(genericLookup.ContextSource);
            }
            else
            {
                lookup.lookupKind.needsRuntimeLookup = false;
                ISymbolNode constLookup = _compilation.ComputeConstantLookup(helperId, entity);
                lookup.constLookup = CreateConstLookupToSymbol(constLookup);
            }
        }

        private bool getReadyToRunHelper(ref CORINFO_RESOLVED_TOKEN pResolvedToken, ref CORINFO_LOOKUP_KIND pGenericLookupKind, CorInfoHelpFunc id, ref CORINFO_CONST_LOOKUP pLookup)
        {
            switch (id)
            {
                case CorInfoHelpFunc.CORINFO_HELP_READYTORUN_NEW:
                case CorInfoHelpFunc.CORINFO_HELP_READYTORUN_NEWARR_1:
                case CorInfoHelpFunc.CORINFO_HELP_READYTORUN_ISINSTANCEOF:
                case CorInfoHelpFunc.CORINFO_HELP_READYTORUN_CHKCAST:
                    return false;
                case CorInfoHelpFunc.CORINFO_HELP_READYTORUN_STATIC_BASE:
                    {
                        var type = HandleToObject(pResolvedToken.hClass);
                        if (type.IsCanonicalSubtype(CanonicalFormKind.Any))
                            return false;

                        pLookup = CreateConstLookupToSymbol(_compilation.NodeFactory.ReadyToRunHelper(ReadyToRunHelperId.GetNonGCStaticBase, type));
                    }
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_READYTORUN_GENERIC_STATIC_BASE:
                    {
                        // Token == 0 means "initialize this class". We only expect RyuJIT to call it for this case.
                        Debug.Assert(pResolvedToken.token == 0 && pResolvedToken.tokenScope == null);
                        Debug.Assert(pGenericLookupKind.needsRuntimeLookup);

                        DefType typeToInitialize = (DefType)MethodBeingCompiled.OwningType;
                        Debug.Assert(typeToInitialize.IsCanonicalSubtype(CanonicalFormKind.Any));

                        DefType helperArg = typeToInitialize.ConvertToSharedRuntimeDeterminedForm();
                        ISymbolNode helper = GetGenericLookupHelper(pGenericLookupKind.runtimeLookupKind, ReadyToRunHelperId.GetNonGCStaticBase, helperArg);
                        pLookup = CreateConstLookupToSymbol(helper);
                    }
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_READYTORUN_GENERIC_HANDLE:
                    {
                        Debug.Assert(pGenericLookupKind.needsRuntimeLookup);

                        ReadyToRunHelperId helperId = (ReadyToRunHelperId)pGenericLookupKind.runtimeLookupFlags;
                        object helperArg = HandleToObject((IntPtr)pGenericLookupKind.runtimeLookupArgs);
                        ISymbolNode helper = GetGenericLookupHelper(pGenericLookupKind.runtimeLookupKind, helperId, helperArg);
                        pLookup = CreateConstLookupToSymbol(helper);
                    }
                    break;
                default:
                    throw new NotImplementedException("ReadyToRun: " + id.ToString());
            }
            return true;
        }

        private void getReadyToRunDelegateCtorHelper(ref CORINFO_RESOLVED_TOKEN pTargetMethod, CORINFO_CLASS_STRUCT_* delegateType, ref CORINFO_LOOKUP pLookup)
        {
#if DEBUG
            // In debug, write some bogus data to the struct to ensure we have filled everything
            // properly.
            fixed (CORINFO_LOOKUP* tmp = &pLookup)
                MemoryHelper.FillMemory((byte*)tmp, 0xcc, sizeof(CORINFO_LOOKUP));
#endif

            MethodDesc targetMethod = HandleToObject(pTargetMethod.hMethod);
            TypeDesc delegateTypeDesc = HandleToObject(delegateType);

            if (targetMethod.IsSharedByGenericInstantiations)
            {
                // If the method is not exact, fetch it as a runtime determined method.
                targetMethod = (MethodDesc)GetRuntimeDeterminedObjectForToken(ref pTargetMethod);
            }

            bool isLdvirtftn = pTargetMethod.tokenType == CorInfoTokenKind.CORINFO_TOKENKIND_Ldvirtftn;
            DelegateCreationInfo delegateInfo = _compilation.GetDelegateCtor(delegateTypeDesc, targetMethod, isLdvirtftn);

            if (delegateInfo.NeedsRuntimeLookup)
            {
                pLookup.lookupKind.needsRuntimeLookup = true;

                MethodDesc contextMethod = methodFromContext(pTargetMethod.tokenContext);

                // We should not be inlining these. RyuJIT should have aborted inlining already.
                Debug.Assert(contextMethod == MethodBeingCompiled);

                pLookup.lookupKind.runtimeLookupKind = GetGenericRuntimeLookupKind(contextMethod);
                pLookup.lookupKind.runtimeLookupFlags = (ushort)ReadyToRunHelperId.DelegateCtor;
                pLookup.lookupKind.runtimeLookupArgs = (void*)ObjectToHandle(delegateInfo);
            }
            else
            {
                pLookup.lookupKind.needsRuntimeLookup = false;
                pLookup.constLookup = CreateConstLookupToSymbol(_compilation.NodeFactory.ReadyToRunHelper(ReadyToRunHelperId.DelegateCtor, delegateInfo));
            }
        }

        private ISymbolNode GetHelperFtnUncached(CorInfoHelpFunc ftnNum)
        {
            ReadyToRunHelper id;

            switch (ftnNum)
            {
                case CorInfoHelpFunc.CORINFO_HELP_THROW:
                    id = ReadyToRunHelper.Throw;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_RETHROW:
                    id = ReadyToRunHelper.Rethrow;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_USER_BREAKPOINT:
                    id = ReadyToRunHelper.DebugBreak;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_OVERFLOW:
                    id = ReadyToRunHelper.Overflow;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_RNGCHKFAIL:
                    id = ReadyToRunHelper.RngChkFail;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_FAIL_FAST:
                    id = ReadyToRunHelper.FailFast;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_THROWNULLREF:
                    id = ReadyToRunHelper.ThrowNullRef;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_THROWDIVZERO:
                    id = ReadyToRunHelper.ThrowDivZero;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_THROW_ARGUMENTOUTOFRANGEEXCEPTION:
                    id = ReadyToRunHelper.ThrowArgumentOutOfRange;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_THROW_ARGUMENTEXCEPTION:
                    id = ReadyToRunHelper.ThrowArgument;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_THROW_NOT_IMPLEMENTED:
                    id = ReadyToRunHelper.ThrowNotImplemented;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_THROW_PLATFORM_NOT_SUPPORTED:
                    id = ReadyToRunHelper.ThrowPlatformNotSupported;
                    break;

                case CorInfoHelpFunc.CORINFO_HELP_ASSIGN_REF:
                    id = ReadyToRunHelper.WriteBarrier;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_CHECKED_ASSIGN_REF:
                    id = ReadyToRunHelper.CheckedWriteBarrier;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_ASSIGN_BYREF:
                    id = ReadyToRunHelper.ByRefWriteBarrier;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_ASSIGN_REF_EAX:
                    id = ReadyToRunHelper.WriteBarrier_EAX;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_ASSIGN_REF_ECX:
                    id = ReadyToRunHelper.WriteBarrier_ECX;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_CHECKED_ASSIGN_REF_EAX:
                    id = ReadyToRunHelper.CheckedWriteBarrier_EAX;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_CHECKED_ASSIGN_REF_ECX:
                    id = ReadyToRunHelper.CheckedWriteBarrier_ECX;
                    break;

                case CorInfoHelpFunc.CORINFO_HELP_ARRADDR_ST:
                    id = ReadyToRunHelper.Stelem_Ref;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_LDELEMA_REF:
                    id = ReadyToRunHelper.Ldelema_Ref;
                    break;

                case CorInfoHelpFunc.CORINFO_HELP_MEMSET:
                    id = ReadyToRunHelper.MemSet;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_MEMCPY:
                    id = ReadyToRunHelper.MemCpy;
                    break;

                case CorInfoHelpFunc.CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE:
                    id = ReadyToRunHelper.GetRuntimeType;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_METHODDESC_TO_STUBRUNTIMEMETHOD:
                    id = ReadyToRunHelper.GetRuntimeMethodHandle;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_FIELDDESC_TO_STUBRUNTIMEFIELD:
                    id = ReadyToRunHelper.GetRuntimeFieldHandle;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPEHANDLE:
                    id = ReadyToRunHelper.GetRuntimeTypeHandle;
                    break;

                case CorInfoHelpFunc.CORINFO_HELP_ARE_TYPES_EQUIVALENT:
                    id = ReadyToRunHelper.AreTypesEquivalent;
                    break;

                case CorInfoHelpFunc.CORINFO_HELP_BOX:
                    id = ReadyToRunHelper.Box;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_BOX_NULLABLE:
                    id = ReadyToRunHelper.Box_Nullable;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_UNBOX:
                    id = ReadyToRunHelper.Unbox;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_UNBOX_NULLABLE:
                    id = ReadyToRunHelper.Unbox_Nullable;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_NEW_MDARR_NONVARARG:
                    id = ReadyToRunHelper.NewMultiDimArr_NonVarArg;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_NEWFAST:
                    id = ReadyToRunHelper.NewObject;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_NEWSFAST:
                    return _compilation.NodeFactory.ExternSymbol("RhpNewFast");
                case CorInfoHelpFunc.CORINFO_HELP_NEWSFAST_FINALIZE:
                    return _compilation.NodeFactory.ExternSymbol("RhpNewFinalizable");
                case CorInfoHelpFunc.CORINFO_HELP_NEWSFAST_ALIGN8:
                    return _compilation.NodeFactory.ExternSymbol("RhpNewFastAlign8");
                case CorInfoHelpFunc.CORINFO_HELP_NEWSFAST_ALIGN8_FINALIZE:
                    return _compilation.NodeFactory.ExternSymbol("RhpNewFinalizableAlign8");
                case CorInfoHelpFunc.CORINFO_HELP_NEWSFAST_ALIGN8_VC:
                    return _compilation.NodeFactory.ExternSymbol("RhpNewFastMisalign");
                case CorInfoHelpFunc.CORINFO_HELP_NEWARR_1_DIRECT:
                    id = ReadyToRunHelper.NewArray;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_NEWARR_1_ALIGN8:
                    return _compilation.NodeFactory.ExternSymbol("RhpNewArrayAlign8");
                case CorInfoHelpFunc.CORINFO_HELP_NEWARR_1_VC:
                    return _compilation.NodeFactory.ExternSymbol("RhpNewArray");

                case CorInfoHelpFunc.CORINFO_HELP_STACK_PROBE:
                    return _compilation.NodeFactory.ExternSymbol("RhpStackProbe");

                case CorInfoHelpFunc.CORINFO_HELP_POLL_GC:
                    return _compilation.NodeFactory.ExternSymbol("RhpGcPoll");

                case CorInfoHelpFunc.CORINFO_HELP_LMUL:
                    id = ReadyToRunHelper.LMul;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_LMUL_OVF:
                    id = ReadyToRunHelper.LMulOfv;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_ULMUL_OVF:
                    id = ReadyToRunHelper.ULMulOvf;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_LDIV:
                    id = ReadyToRunHelper.LDiv;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_LMOD:
                    id = ReadyToRunHelper.LMod;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_ULDIV:
                    id = ReadyToRunHelper.ULDiv;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_ULMOD:
                    id = ReadyToRunHelper.ULMod;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_LLSH:
                    id = ReadyToRunHelper.LLsh;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_LRSH:
                    id = ReadyToRunHelper.LRsh;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_LRSZ:
                    id = ReadyToRunHelper.LRsz;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_LNG2DBL:
                    id = ReadyToRunHelper.Lng2Dbl;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_ULNG2DBL:
                    id = ReadyToRunHelper.ULng2Dbl;
                    break;

                case CorInfoHelpFunc.CORINFO_HELP_DIV:
                    id = ReadyToRunHelper.Div;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_MOD:
                    id = ReadyToRunHelper.Mod;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_UDIV:
                    id = ReadyToRunHelper.UDiv;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_UMOD:
                    id = ReadyToRunHelper.UMod;
                    break;

                case CorInfoHelpFunc.CORINFO_HELP_DBL2INT:
                    id = ReadyToRunHelper.Dbl2Int;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_DBL2INT_OVF:
                    id = ReadyToRunHelper.Dbl2IntOvf;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_DBL2LNG:
                    id = ReadyToRunHelper.Dbl2Lng;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_DBL2LNG_OVF:
                    id = ReadyToRunHelper.Dbl2LngOvf;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_DBL2UINT:
                    id = ReadyToRunHelper.Dbl2UInt;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_DBL2UINT_OVF:
                    id = ReadyToRunHelper.Dbl2UIntOvf;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_DBL2ULNG:
                    id = ReadyToRunHelper.Dbl2ULng;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_DBL2ULNG_OVF:
                    id = ReadyToRunHelper.Dbl2ULngOvf;
                    break;

                case CorInfoHelpFunc.CORINFO_HELP_FLTREM:
                    id = ReadyToRunHelper.FltRem;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_DBLREM:
                    id = ReadyToRunHelper.DblRem;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_FLTROUND:
                    id = ReadyToRunHelper.FltRound;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_DBLROUND:
                    id = ReadyToRunHelper.DblRound;
                    break;

                case CorInfoHelpFunc.CORINFO_HELP_JIT_PINVOKE_BEGIN:
                    id = ReadyToRunHelper.PInvokeBegin;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_JIT_PINVOKE_END:
                    id = ReadyToRunHelper.PInvokeEnd;
                    break;

                case CorInfoHelpFunc.CORINFO_HELP_JIT_REVERSE_PINVOKE_ENTER:
                    id = ReadyToRunHelper.ReversePInvokeEnter;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_JIT_REVERSE_PINVOKE_EXIT:
                    id = ReadyToRunHelper.ReversePInvokeExit;
                    break;

                case CorInfoHelpFunc.CORINFO_HELP_CHKCASTANY:
                    id = ReadyToRunHelper.CheckCastAny;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_ISINSTANCEOFANY:
                    id = ReadyToRunHelper.CheckInstanceAny;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_CHKCASTCLASS:
                case CorInfoHelpFunc.CORINFO_HELP_CHKCASTCLASS_SPECIAL:
                    // TODO: separate helper for the _SPECIAL case
                    id = ReadyToRunHelper.CheckCastClass;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_ISINSTANCEOFCLASS:
                    id = ReadyToRunHelper.CheckInstanceClass;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_CHKCASTARRAY:
                    id = ReadyToRunHelper.CheckCastArray;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_ISINSTANCEOFARRAY:
                    id = ReadyToRunHelper.CheckInstanceArray;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_CHKCASTINTERFACE:
                    id = ReadyToRunHelper.CheckCastInterface;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_ISINSTANCEOFINTERFACE:
                    id = ReadyToRunHelper.CheckInstanceInterface;
                    break;

                case CorInfoHelpFunc.CORINFO_HELP_MON_ENTER:
                    id = ReadyToRunHelper.MonitorEnter;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_MON_EXIT:
                    id = ReadyToRunHelper.MonitorExit;
                    break;

                case CorInfoHelpFunc.CORINFO_HELP_MON_ENTER_STATIC:
                    id = ReadyToRunHelper.MonitorEnterStatic;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_MON_EXIT_STATIC:
                    id = ReadyToRunHelper.MonitorExitStatic;
                    break;

                case CorInfoHelpFunc.CORINFO_HELP_GVMLOOKUP_FOR_SLOT:
                    id = ReadyToRunHelper.GVMLookupForSlot;
                    break;

                case CorInfoHelpFunc.CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE_MAYBENULL:
                    id = ReadyToRunHelper.TypeHandleToRuntimeType;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_GETREFANY:
                    id = ReadyToRunHelper.GetRefAny;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPEHANDLE_MAYBENULL:
                    id = ReadyToRunHelper.TypeHandleToRuntimeTypeHandle;
                    break;

                default:
                    throw new NotImplementedException(ftnNum.ToString());
            }

            string mangledName;
            MethodDesc methodDesc;
            JitHelper.GetEntryPoint(_compilation.TypeSystemContext, id, out mangledName, out methodDesc);
            Debug.Assert(mangledName != null || methodDesc != null);

            ISymbolNode entryPoint;
            if (mangledName != null)
                entryPoint = _compilation.NodeFactory.ExternSymbol(mangledName);
            else
                entryPoint = _compilation.NodeFactory.MethodEntrypoint(methodDesc);

            return entryPoint;
        }

        private void getFunctionEntryPoint(CORINFO_METHOD_STRUCT_* ftn, ref CORINFO_CONST_LOOKUP pResult, CORINFO_ACCESS_FLAGS accessFlags)
        {
            MethodDesc method = HandleToObject(ftn);

            // TODO: Implement MapMethodDeclToMethodImpl from CoreCLR
            if (method.IsVirtual)
                throw new NotImplementedException("getFunctionEntryPoint");

            pResult = CreateConstLookupToSymbol(_compilation.NodeFactory.MethodEntrypoint(method));
        }

        private bool canTailCall(CORINFO_METHOD_STRUCT_* callerHnd, CORINFO_METHOD_STRUCT_* declaredCalleeHnd, CORINFO_METHOD_STRUCT_* exactCalleeHnd, bool fIsTailPrefix)
        {
            // No restrictions on tailcalls
            return true;
        }

        private InfoAccessType constructStringLiteral(CORINFO_MODULE_STRUCT_* module, mdToken metaTok, ref void* ppValue)
        {
            MethodIL methodIL = (MethodIL)HandleToObject((IntPtr)module);
            object literal = methodIL.GetObject((int)metaTok);
            ISymbolNode stringObject = _compilation.NodeFactory.SerializedStringObject((string)literal);
            ppValue = (void*)ObjectToHandle(stringObject);
            return stringObject.RepresentsIndirectionCell ? InfoAccessType.IAT_PVALUE : InfoAccessType.IAT_VALUE;
        }

        enum RhEHClauseKind
        {
            RH_EH_CLAUSE_TYPED = 0,
            RH_EH_CLAUSE_FAULT = 1,
            RH_EH_CLAUSE_FILTER = 2
        }

        private ObjectNode.ObjectData EncodeEHInfo()
        {
            var builder = new ObjectDataBuilder();
            builder.RequireInitialAlignment(1);

            int totalClauses = _ehClauses.Length;

            // Count the number of special markers that will be needed
            for (int i = 1; i < _ehClauses.Length; i++)
            {
                ref CORINFO_EH_CLAUSE clause = ref _ehClauses[i];
                ref CORINFO_EH_CLAUSE previousClause = ref _ehClauses[i - 1];

                if ((previousClause.TryOffset == clause.TryOffset) &&
                    (previousClause.TryLength == clause.TryLength) &&
                    ((clause.Flags & CORINFO_EH_CLAUSE_FLAGS.CORINFO_EH_CLAUSE_SAMETRY) == 0))
                {
                    totalClauses++;
                }
            }

            builder.EmitCompressedUInt((uint)totalClauses);

            for (int i = 0; i < _ehClauses.Length; i++)
            {
                ref CORINFO_EH_CLAUSE clause = ref _ehClauses[i];

                if (i > 0)
                {
                    ref CORINFO_EH_CLAUSE previousClause = ref _ehClauses[i - 1];

                    // If the previous clause has same try offset and length as the current clause,
                    // but belongs to a different try block (CORINFO_EH_CLAUSE_SAMETRY is not set),
                    // emit a special marker to allow runtime distinguish this case.
                    if ((previousClause.TryOffset == clause.TryOffset) &&
                        (previousClause.TryLength == clause.TryLength) &&
                        ((clause.Flags & CORINFO_EH_CLAUSE_FLAGS.CORINFO_EH_CLAUSE_SAMETRY) == 0))
                    {
                        builder.EmitCompressedUInt(0);
                        builder.EmitCompressedUInt((uint)RhEHClauseKind.RH_EH_CLAUSE_FAULT);
                        builder.EmitCompressedUInt(0);
                    }
                }

                RhEHClauseKind clauseKind;

                if (((clause.Flags & CORINFO_EH_CLAUSE_FLAGS.CORINFO_EH_CLAUSE_FAULT) != 0) ||
                    ((clause.Flags & CORINFO_EH_CLAUSE_FLAGS.CORINFO_EH_CLAUSE_FINALLY) != 0))
                {
                    clauseKind = RhEHClauseKind.RH_EH_CLAUSE_FAULT;
                }
                else
                if ((clause.Flags & CORINFO_EH_CLAUSE_FLAGS.CORINFO_EH_CLAUSE_FILTER) != 0)
                {
                    clauseKind = RhEHClauseKind.RH_EH_CLAUSE_FILTER;
                }
                else
                {
                    clauseKind = RhEHClauseKind.RH_EH_CLAUSE_TYPED;
                }

                builder.EmitCompressedUInt((uint)clause.TryOffset);

                // clause.TryLength returned by the JIT is actually end offset...
                // https://github.com/dotnet/runtime/issues/5282
                int tryLength = (int)clause.TryLength - (int)clause.TryOffset;
                builder.EmitCompressedUInt((uint)((tryLength << 2) | (int)clauseKind));

                switch (clauseKind)
                {
                    case RhEHClauseKind.RH_EH_CLAUSE_TYPED:
                        {
                            builder.EmitCompressedUInt(clause.HandlerOffset);

                            var methodIL = (MethodIL)HandleToObject((IntPtr)_methodScope);
                            var type = (TypeDesc)methodIL.GetObject((int)clause.ClassTokenOrOffset);

                            // Once https://github.com/dotnet/corert/issues/3460 is done, this should be an assert.
                            // Throwing InvalidProgram is not great, but we want to do *something* if this happens
                            // because doing nothing means problems at runtime. This is not worth piping a
                            // a new exception with a fancy message for.
                            if (type.IsCanonicalSubtype(CanonicalFormKind.Any))
                                ThrowHelper.ThrowInvalidProgramException();

                            var typeSymbol = _compilation.NodeFactory.NecessaryTypeSymbol(type);

                            RelocType rel = (_compilation.NodeFactory.Target.IsWindows) ?
                                RelocType.IMAGE_REL_BASED_ABSOLUTE :
                                RelocType.IMAGE_REL_BASED_RELPTR32;

                            if (_compilation.NodeFactory.Target.Abi == TargetAbi.Jit)
                                rel = RelocType.IMAGE_REL_BASED_REL32;

                            builder.EmitReloc(typeSymbol, rel);
                        }
                        break;
                    case RhEHClauseKind.RH_EH_CLAUSE_FAULT:
                        builder.EmitCompressedUInt(clause.HandlerOffset);
                        break;
                    case RhEHClauseKind.RH_EH_CLAUSE_FILTER:
                        builder.EmitCompressedUInt(clause.HandlerOffset);
                        builder.EmitCompressedUInt(clause.ClassTokenOrOffset);
                        break;
                }
            }

            return builder.ToObjectData();
        }

        private void setVars(CORINFO_METHOD_STRUCT_* ftn, uint cVars, NativeVarInfo* vars)
        {
            if (_localSlotToInfoMap == null && _parameterIndexToNameMap == null)
            {
                return;
            }

            uint paramCount = (_parameterIndexToNameMap == null) ? 0 : (uint)_parameterIndexToNameMap.Count;
            Dictionary<uint, DebugVarInfo> debugVars = new Dictionary<uint, DebugVarInfo>();

            for (int i = 0; i < cVars; i++)
            {
                NativeVarInfo nativeVarInfo = vars[i];

                if (nativeVarInfo.varNumber < paramCount)
                {
                    string name = _parameterIndexToNameMap[nativeVarInfo.varNumber];
                    updateDebugVarInfo(debugVars, name, true, nativeVarInfo);
                }
                else if (_localSlotToInfoMap != null)
                {
                    ILLocalVariable ilVar;
                    uint slotNumber = nativeVarInfo.varNumber - paramCount;
                    if (_localSlotToInfoMap.TryGetValue(slotNumber, out ilVar))
                    {
                        updateDebugVarInfo(debugVars, ilVar.Name, false, nativeVarInfo);
                    }
                }
            }

            _debugVarInfos = new DebugVarInfo[debugVars.Count];
            debugVars.Values.CopyTo(_debugVarInfos, 0);
        }

        private void updateDebugVarInfo(Dictionary<uint, DebugVarInfo> debugVars, string name,
                                        bool isParam, NativeVarInfo nativeVarInfo)
        {
            DebugVarInfo debugVar;

            if (!debugVars.TryGetValue(nativeVarInfo.varNumber, out debugVar))
            {
                debugVar = new DebugVarInfo(name, isParam, _variableToTypeDesc[(int)nativeVarInfo.varNumber]);
                debugVars[nativeVarInfo.varNumber] = debugVar;
            }

            debugVar.Ranges.Add(nativeVarInfo);
        }

        /// <summary>
        /// Create a DebugLocInfo which is a table from native offset to source line.
        /// using native to il offset (pMap) and il to source line (_sequencePoints).
        /// </summary>
        private void setBoundaries(CORINFO_METHOD_STRUCT_* ftn, uint cMap, OffsetMapping* pMap)
        {
            Debug.Assert(_debugLocInfos == null);
            // No interest if sequencePoints is not populated before.
            if (_sequencePoints == null)
            {
                return;
            }

            int largestILOffset = 0; // All epiloges point to the largest IL offset.
            for (int i = 0; i < cMap; i++)
            {
                OffsetMapping nativeToILInfo = pMap[i];
                int currectILOffset = (int)nativeToILInfo.ilOffset;
                if (currectILOffset > largestILOffset) // Special offsets are negative.
                {
                    largestILOffset = currectILOffset;
                }
            }

            int previousNativeOffset = -1;
            List<DebugLocInfo> debugLocInfos = new List<DebugLocInfo>();
            for (int i = 0; i < cMap; i++)
            {
                OffsetMapping nativeToILInfo = pMap[i];
                int ilOffset = (int)nativeToILInfo.ilOffset;
                int nativeOffset = (int)pMap[i].nativeOffset;
                if (nativeOffset == previousNativeOffset)
                {
                    // Save the first one, skip others.
                    continue;
                }
                switch (ilOffset)
                {
                    case (int)MappingTypes.PROLOG:
                        ilOffset = 0;
                        break;
                    case (int)MappingTypes.EPILOG:
                        ilOffset = largestILOffset;
                        break;
                    case (int)MappingTypes.NO_MAPPING:
                        continue;
                }

                SequencePoint s;
                if (_sequencePoints.TryGetValue((int)ilOffset, out s))
                {
                    Debug.Assert(s.Document != null);
                    DebugLocInfo loc = new DebugLocInfo(nativeOffset, s.Document, s.LineNumber);
                    debugLocInfos.Add(loc);
                    previousNativeOffset = nativeOffset;
                }
            }

            if (debugLocInfos.Count > 0)
            {
                _debugLocInfos = debugLocInfos.ToArray();
            }
        }

        private void SetDebugInformation(IMethodNode methodCodeNodeNeedingCode, MethodIL methodIL)
        {
            try
            {
                MethodDebugInformation debugInfo = _compilation.GetDebugInfo(methodIL);

                // TODO: NoLineNumbers
                //if (!_compilation.Options.NoLineNumbers)
                {
                    IEnumerable<ILSequencePoint> ilSequencePoints = debugInfo.GetSequencePoints();
                    if (ilSequencePoints != null)
                    {
                        try
                        {
                            SetSequencePoints(ilSequencePoints);
                        }
                        catch (BadImageFormatException)
                        {
                            // Roslyn had a bug where it was generating bad sequence points:
                            // https://github.com/dotnet/roslyn/issues/20118
                            // Do not crash the compiler.
                            _compilation.Logger.Writer.WriteLine($"Warning: ignoring debug info for {methodCodeNodeNeedingCode.Method.ToString()}");
                        }
                    }
                }

                IEnumerable<ILLocalVariable> localVariables = debugInfo.GetLocalVariables();
                if (localVariables != null)
                {
                    SetLocalVariables(localVariables);
                }

                IEnumerable<string> parameters = debugInfo.GetParameterNames();
                if (parameters != null)
                {
                    SetParameterNames(parameters);
                }

                ArrayBuilder<TypeDesc> variableToTypeDesc = new ArrayBuilder<TypeDesc>();

                var signature = MethodBeingCompiled.Signature;
                if (!signature.IsStatic)
                {
                    TypeDesc type = MethodBeingCompiled.OwningType;

                    // This pointer for value types is a byref
                    if (MethodBeingCompiled.OwningType.IsValueType)
                        type = type.MakeByRefType();

                    variableToTypeDesc.Add(type);
                }

                for (int i = 0; i < signature.Length; ++i)
                {
                    TypeDesc type = signature[i];
                    variableToTypeDesc.Add(type);
                }
                var locals = methodIL.GetLocals();
                for (int i = 0; i < locals.Length; ++i)
                {
                    TypeDesc type = locals[i].Type;
                    variableToTypeDesc.Add(type);
                }
                _variableToTypeDesc = variableToTypeDesc.ToArray();
            }
            catch (Exception e)
            {
                // Debug info not successfully loaded.
                Log.WriteLine(e.Message + " (" + methodCodeNodeNeedingCode.ToString() + ")");
            }
        }

        public void SetSequencePoints(IEnumerable<ILSequencePoint> ilSequencePoints)
        {
            Debug.Assert(ilSequencePoints != null);
            Dictionary<int, SequencePoint> sequencePoints = new Dictionary<int, SequencePoint>();

            foreach (var point in ilSequencePoints)
            {
                sequencePoints.Add(point.Offset, new SequencePoint() { Document = point.Document, LineNumber = point.LineNumber });
            }

            _sequencePoints = sequencePoints;
        }

        public void SetLocalVariables(IEnumerable<ILLocalVariable> localVariables)
        {
            Debug.Assert(localVariables != null);
            var localSlotToInfoMap = new Dictionary<uint, ILLocalVariable>();

            foreach (var v in localVariables)
            {
                localSlotToInfoMap[(uint)v.Slot] = v;
            }

            _localSlotToInfoMap = localSlotToInfoMap;
        }

        public void SetParameterNames(IEnumerable<string> parameters)
        {
            Debug.Assert(parameters != null);
            var parameterIndexToNameMap = new Dictionary<uint, string>();
            uint index = 0;

            foreach (var p in parameters)
            {
                parameterIndexToNameMap[index] = p;
                ++index;
            }

            _parameterIndexToNameMap = parameterIndexToNameMap;
        }

        private ISymbolNode GetGenericLookupHelper(CORINFO_RUNTIME_LOOKUP_KIND runtimeLookupKind, ReadyToRunHelperId helperId, object helperArgument)
        {
            if (runtimeLookupKind == CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_THISOBJ
                || runtimeLookupKind == CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_CLASSPARAM)
            {
                return _compilation.NodeFactory.ReadyToRunHelperFromTypeLookup(helperId, helperArgument, MethodBeingCompiled.OwningType);
            }

            Debug.Assert(runtimeLookupKind == CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_METHODPARAM);
            return _compilation.NodeFactory.ReadyToRunHelperFromDictionaryLookup(helperId, helperArgument, MethodBeingCompiled);
        }

        private CorInfoHelpFunc getCastingHelper(ref CORINFO_RESOLVED_TOKEN pResolvedToken, bool fThrowing)
        {
            TypeDesc type = HandleToObject(pResolvedToken.hClass);

            CorInfoHelpFunc helper;

            if (type.IsCanonicalDefinitionType(CanonicalFormKind.Any))
            {
                // In shared code just use the catch-all helper for type variables, as the same
                // code may be used for interface/array/class instantiations
                //
                // We may be able to take advantage of constraints to select a specialized helper.
                // This optimizations does not seem to be warranted at the moment.
                helper = CorInfoHelpFunc.CORINFO_HELP_ISINSTANCEOFANY;
            }
            else if (type.IsInterface)
            {
                // If it is an interface, use the fast interface helper
                helper = CorInfoHelpFunc.CORINFO_HELP_ISINSTANCEOFINTERFACE;
            }
            else if (type.IsArray)
            {
                // If it is an array, use the fast array helper
                helper = CorInfoHelpFunc.CORINFO_HELP_ISINSTANCEOFARRAY;
            }
            else if (type.IsDefType)
            {
                helper = CorInfoHelpFunc.CORINFO_HELP_ISINSTANCEOFCLASS;
#if !SUPPORT_JIT
                // When this assert is hit, we'll have to do something with the class checks in RyuJIT
                // Frozen strings might end up failing inlined checks generated by RyuJIT for sealed classes.
                Debug.Assert(!_compilation.NodeFactory.CompilationModuleGroup.CanHaveReferenceThroughImportTable);
#endif
            }
            else
            {
                // Otherwise, use the slow helper
                helper = CorInfoHelpFunc.CORINFO_HELP_ISINSTANCEOFANY;
            }

            if (fThrowing)
            {
                int delta = CorInfoHelpFunc.CORINFO_HELP_CHKCASTANY - CorInfoHelpFunc.CORINFO_HELP_ISINSTANCEOFANY;

                Debug.Assert(CorInfoHelpFunc.CORINFO_HELP_ISINSTANCEOFINTERFACE + delta
                    == CorInfoHelpFunc.CORINFO_HELP_CHKCASTINTERFACE);
                Debug.Assert(CorInfoHelpFunc.CORINFO_HELP_ISINSTANCEOFARRAY + delta
                    == CorInfoHelpFunc.CORINFO_HELP_CHKCASTARRAY);
                Debug.Assert(CorInfoHelpFunc.CORINFO_HELP_ISINSTANCEOFCLASS + delta
                    == CorInfoHelpFunc.CORINFO_HELP_CHKCASTCLASS);

                helper += delta;
            }

            return helper;
        }

        private CorInfoHelpFunc getNewHelper(ref CORINFO_RESOLVED_TOKEN pResolvedToken, CORINFO_METHOD_STRUCT_* callerHandle, byte* pHasSideEffects = null)
        {
            TypeDesc type = HandleToObject(pResolvedToken.hClass);

            Debug.Assert(!type.IsString && !type.IsArray && !type.IsCanonicalDefinitionType(CanonicalFormKind.Any));
            
            if (pHasSideEffects != null)
            {
                *pHasSideEffects = (byte)(type.HasFinalizer ? 1 : 0);
            }

            if (type.RequiresAlign8())
            {
                if (type.HasFinalizer)
                    return CorInfoHelpFunc.CORINFO_HELP_NEWSFAST_ALIGN8_FINALIZE;

                if (type.IsValueType)
                    return CorInfoHelpFunc.CORINFO_HELP_NEWSFAST_ALIGN8_VC;

                return CorInfoHelpFunc.CORINFO_HELP_NEWSFAST_ALIGN8;
            }

            if (type.HasFinalizer)
                return CorInfoHelpFunc.CORINFO_HELP_NEWSFAST_FINALIZE;

            return CorInfoHelpFunc.CORINFO_HELP_NEWSFAST;
        }

        private CorInfoHelpFunc getNewArrHelper(CORINFO_CLASS_STRUCT_* arrayCls)
        {
            TypeDesc type = HandleToObject(arrayCls);

            Debug.Assert(type.IsArray);

            if (type.RequiresAlign8())
                return CorInfoHelpFunc.CORINFO_HELP_NEWARR_1_ALIGN8;

            return CorInfoHelpFunc.CORINFO_HELP_NEWARR_1_VC;
        }

        private IMethodNode GetMethodEntrypoint(MethodDesc method)
        {
            bool isUnboxingThunk = method.IsUnboxingThunk();
            if (isUnboxingThunk)
            {
                method = method.GetUnboxedMethod();
            }
            return _compilation.NodeFactory.MethodEntrypoint(method, isUnboxingThunk);
        }

        private static bool IsTypeSpecForTypicalInstantiation(TypeDesc t)
        {
            Instantiation inst = t.Instantiation;
            for (int i = 0; i < inst.Length; i++)
            {
                var arg = inst[i] as SignatureTypeVariable;
                if (arg == null || arg.Index != i)
                    return false;
            }
            return true;
        }

        private void getCallInfo(ref CORINFO_RESOLVED_TOKEN pResolvedToken, CORINFO_RESOLVED_TOKEN* pConstrainedResolvedToken, CORINFO_METHOD_STRUCT_* callerHandle, CORINFO_CALLINFO_FLAGS flags, CORINFO_CALL_INFO* pResult)
        {
#if DEBUG
            // In debug, write some bogus data to the struct to ensure we have filled everything
            // properly.
            MemoryHelper.FillMemory((byte*)pResult, 0xcc, Marshal.SizeOf<CORINFO_CALL_INFO>());
#endif
            MethodDesc method = HandleToObject(pResolvedToken.hMethod);

            // Spec says that a callvirt lookup ignores static methods. Since static methods
            // can't have the exact same signature as instance methods, a lookup that found
            // a static method would have never found an instance method.
            if (method.Signature.IsStatic && (flags & CORINFO_CALLINFO_FLAGS.CORINFO_CALLINFO_CALLVIRT) != 0)
            {
                throw new BadImageFormatException();
            }

            // This block enforces the rule that methods with [UnmanagedCallersOnly] attribute
            // can only be called from unmanaged code. The call from managed code is replaced
            // with a stub that throws an InvalidProgramException
            if (method.IsUnmanagedCallersOnly && (flags & CORINFO_CALLINFO_FLAGS.CORINFO_CALLINFO_LDFTN) == 0)
            {
                ThrowHelper.ThrowInvalidProgramException(ExceptionStringID.InvalidProgramUnmanagedCallersOnly, method);
            }

            TypeDesc exactType = HandleToObject(pResolvedToken.hClass);

            TypeDesc constrainedType = null;
            if ((flags & CORINFO_CALLINFO_FLAGS.CORINFO_CALLINFO_CALLVIRT) != 0 && pConstrainedResolvedToken != null)
            {
                constrainedType = HandleToObject(pConstrainedResolvedToken->hClass);
            }

            bool resolvedConstraint = false;
            bool forceUseRuntimeLookup = false;
            bool targetIsFatFunctionPointer = false;

            MethodDesc methodAfterConstraintResolution = method;
            if (constrainedType == null)
            {
                pResult->thisTransform = CORINFO_THIS_TRANSFORM.CORINFO_NO_THIS_TRANSFORM;
            }
            else
            {
                // We have a "constrained." call.  Try a partial resolve of the constraint call.  Note that this
                // will not necessarily resolve the call exactly, since we might be compiling
                // shared generic code - it may just resolve it to a candidate suitable for
                // JIT compilation, and require a runtime lookup for the actual code pointer
                // to call.

                MethodDesc directMethod = constrainedType.GetClosestDefType().TryResolveConstraintMethodApprox(exactType, method, out forceUseRuntimeLookup);
                if (directMethod == null && constrainedType.IsEnum)
                {
                    // Constrained calls to methods on enum methods resolve to System.Enum's methods. System.Enum is a reference
                    // type though, so we would fail to resolve and box. We have a special path for those to avoid boxing.
                    directMethod = _compilation.TypeSystemContext.TryResolveConstrainedEnumMethod(constrainedType, method);
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
                    pResult->thisTransform = CORINFO_THIS_TRANSFORM.CORINFO_NO_THIS_TRANSFORM;

                    exactType = constrainedType;
                }
                else if (constrainedType.IsValueType)
                {
                    pResult->thisTransform = CORINFO_THIS_TRANSFORM.CORINFO_BOX_THIS;
                }
                else
                {
                    pResult->thisTransform = CORINFO_THIS_TRANSFORM.CORINFO_DEREF_THIS;
                }
            }

            MethodDesc targetMethod = methodAfterConstraintResolution;

            //
            // Initialize callee context used for inlining and instantiation arguments
            //


            if (targetMethod.HasInstantiation)
            {
                pResult->contextHandle = contextFromMethod(targetMethod);
                pResult->exactContextNeedsRuntimeLookup = targetMethod.IsSharedByGenericInstantiations;
            }
            else
            {
                pResult->contextHandle = contextFromType(exactType);
                pResult->exactContextNeedsRuntimeLookup = exactType.IsCanonicalSubtype(CanonicalFormKind.Any);

                // Use main method as the context as long as the methods are called on the same type
                if (pResult->exactContextNeedsRuntimeLookup &&
                    pResolvedToken.tokenContext == contextFromMethodBeingCompiled() &&
                    constrainedType == null &&
                    exactType == MethodBeingCompiled.OwningType)
                {
                    var methodIL = (MethodIL)HandleToObject((IntPtr)pResolvedToken.tokenScope);
                    var rawMethod = (MethodDesc)methodIL.GetMethodILDefinition().GetObject((int)pResolvedToken.token);
                    if (IsTypeSpecForTypicalInstantiation(rawMethod.OwningType))
                    {
                        pResult->contextHandle = contextFromMethodBeingCompiled();
                    }
                }
            }

            //
            // Determine whether to perform direct call
            //

            bool directCall = false;
            bool resolvedCallVirt = false;

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
            else if ((flags & CORINFO_CALLINFO_FLAGS.CORINFO_CALLINFO_CALLVIRT) == 0 || resolvedConstraint)
            {
                directCall = true;
            }
            else
            {
                if (!targetMethod.IsVirtual || targetMethod.IsFinal || targetMethod.OwningType.IsSealed())
                {
                    resolvedCallVirt = true;
                    directCall = true;
                }
            }

            pResult->codePointerOrStubLookup.lookupKind.needsRuntimeLookup = false;

            bool allowInstParam = (flags & CORINFO_CALLINFO_FLAGS.CORINFO_CALLINFO_ALLOWINSTPARAM) != 0;

            if (directCall && !allowInstParam && targetMethod.GetCanonMethodTarget(CanonicalFormKind.Specific).RequiresInstArg())
            {
                // JIT needs a single address to call this method but the method needs a hidden argument.
                // We need a fat function pointer for this that captures both things.
                targetIsFatFunctionPointer = true;

                // JIT won't expect fat function pointers unless this is e.g. delegate creation
                Debug.Assert((flags & CORINFO_CALLINFO_FLAGS.CORINFO_CALLINFO_LDFTN) != 0);

                pResult->kind = CORINFO_CALL_KIND.CORINFO_CALL_CODE_POINTER;

                if (pResult->exactContextNeedsRuntimeLookup)
                {
                    pResult->codePointerOrStubLookup.lookupKind.needsRuntimeLookup = true;
                    pResult->codePointerOrStubLookup.lookupKind.runtimeLookupFlags = 0;
                    pResult->codePointerOrStubLookup.runtimeLookup.indirections = CORINFO.USEHELPER;

                    // Do not bother computing the runtime lookup if we are inlining. The JIT is going
                    // to abort the inlining attempt anyway.
                    if (pResolvedToken.tokenContext == contextFromMethodBeingCompiled())
                    {
                        MethodDesc contextMethod = methodFromContext(pResolvedToken.tokenContext);
                        pResult->codePointerOrStubLookup.lookupKind.runtimeLookupKind = GetGenericRuntimeLookupKind(contextMethod);
                        pResult->codePointerOrStubLookup.lookupKind.runtimeLookupFlags = (ushort)ReadyToRunHelperId.MethodEntry;
                        pResult->codePointerOrStubLookup.lookupKind.runtimeLookupArgs = (void*)ObjectToHandle(GetRuntimeDeterminedObjectForToken(ref pResolvedToken));
                    }
                    else
                    {
                        pResult->codePointerOrStubLookup.lookupKind.runtimeLookupKind = CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_NOT_SUPPORTED;
                    }
                }
                else
                {
                    pResult->codePointerOrStubLookup.constLookup =
                        CreateConstLookupToSymbol(_compilation.NodeFactory.FatFunctionPointer(targetMethod));
                }
            }
            else if (directCall)
            {
                bool referencingArrayAddressMethod = false;

                if (targetMethod.IsIntrinsic)
                {
                    // If this is an intrinsic method with a callsite-specific expansion, this will replace
                    // the method with a method the intrinsic expands into. If it's not the special intrinsic,
                    // method stays unchanged.
                    var methodIL = (MethodIL)HandleToObject((IntPtr)pResolvedToken.tokenScope);
                    targetMethod = _compilation.ExpandIntrinsicForCallsite(targetMethod, methodIL.OwningMethod);

                    // For multidim array Address method, we pretend the method requires a hidden instantiation argument
                    // (even though it doesn't need one). We'll actually swap the method out for a differnt one with
                    // a matching calling convention later. See ArrayMethod for a description.
                    referencingArrayAddressMethod = targetMethod.IsArrayAddressMethod();
                }

                MethodDesc concreteMethod = targetMethod;
                targetMethod = targetMethod.GetCanonMethodTarget(CanonicalFormKind.Specific);

                pResult->kind = CORINFO_CALL_KIND.CORINFO_CALL;

                if (targetMethod.IsConstructor && targetMethod.OwningType.IsString)
                {
                    // Calling a string constructor doesn't call the actual constructor.
                    pResult->codePointerOrStubLookup.constLookup = CreateConstLookupToSymbol(
                        _compilation.NodeFactory.StringAllocator(targetMethod)
                        );
                }
                else if (pResult->exactContextNeedsRuntimeLookup)
                {
                    // Nothing to do... The generic handle lookup gets embedded in to the codegen
                    // during the jitting of the call.
                    // (Note: The generic lookup in R2R is performed by a call to a helper at runtime, not by
                    // codegen emitted at crossgen time)

                    Debug.Assert(!forceUseRuntimeLookup);
                    pResult->codePointerOrStubLookup.constLookup = CreateConstLookupToSymbol(
                        GetMethodEntrypoint(targetMethod)
                        );
                }
                else
                {
                    ISymbolNode instParam = null;

                    if (targetMethod.RequiresInstMethodDescArg())
                    {
                        instParam = _compilation.NodeFactory.MethodGenericDictionary(concreteMethod);
                    }
                    else if (targetMethod.RequiresInstMethodTableArg() || referencingArrayAddressMethod)
                    {
                        // Ask for a constructed type symbol because we need the vtable to get to the dictionary
                        instParam = _compilation.NodeFactory.ConstructedTypeSymbol(concreteMethod.OwningType);
                    }

                    if (instParam != null)
                    {
                        pResult->instParamLookup = CreateConstLookupToSymbol(instParam);
                    }

                    pResult->codePointerOrStubLookup.constLookup = CreateConstLookupToSymbol(
                        GetMethodEntrypoint(targetMethod)
                        );
                }

                pResult->nullInstanceCheck = resolvedCallVirt;
            }
            else if (method.HasInstantiation)
            {
                // GVM Call Support
                pResult->kind = CORINFO_CALL_KIND.CORINFO_VIRTUALCALL_LDVIRTFTN;
                pResult->codePointerOrStubLookup.constLookup.accessType = InfoAccessType.IAT_VALUE;
                pResult->nullInstanceCheck = true;

                if (pResult->exactContextNeedsRuntimeLookup)
                {
                    ComputeLookup(ref pResolvedToken,
                        GetRuntimeDeterminedObjectForToken(ref pResolvedToken),
                        ReadyToRunHelperId.MethodHandle,
                        ref pResult->codePointerOrStubLookup);
                    Debug.Assert(pResult->codePointerOrStubLookup.lookupKind.needsRuntimeLookup);
                }

                // RyuJIT will assert if we report CORINFO_CALLCONV_PARAMTYPE for a result of a ldvirtftn
                // We don't need an instantiation parameter, so let's just not report it. Might be nice to
                // move that assert to some place later though.
                targetIsFatFunctionPointer = true;
            }
            else if ((flags & CORINFO_CALLINFO_FLAGS.CORINFO_CALLINFO_LDFTN) == 0
                && targetMethod.OwningType.IsInterface)
            {
                pResult->kind = CORINFO_CALL_KIND.CORINFO_VIRTUALCALL_STUB;

                if (pResult->exactContextNeedsRuntimeLookup)
                {
                    ComputeLookup(ref pResolvedToken,
                        GetRuntimeDeterminedObjectForToken(ref pResolvedToken),
                        ReadyToRunHelperId.VirtualDispatchCell,
                        ref pResult->codePointerOrStubLookup);
                    Debug.Assert(pResult->codePointerOrStubLookup.lookupKind.needsRuntimeLookup);
                }
                else
                {
                    pResult->codePointerOrStubLookup.lookupKind.needsRuntimeLookup = false;
                    pResult->codePointerOrStubLookup.constLookup.accessType = InfoAccessType.IAT_PVALUE;
                    pResult->codePointerOrStubLookup.constLookup.addr = (void*)ObjectToHandle(
                        _compilation.NodeFactory.InterfaceDispatchCell(targetMethod
#if !SUPPORT_JIT
                        , _compilation.NameMangler.GetMangledMethodName(MethodBeingCompiled).ToString()
#endif
                        ));
                }
            }
            else if ((flags & CORINFO_CALLINFO_FLAGS.CORINFO_CALLINFO_LDFTN) == 0
                && _compilation.HasFixedSlotVTable(targetMethod.OwningType))
            {
                pResult->kind = CORINFO_CALL_KIND.CORINFO_VIRTUALCALL_VTABLE;
                pResult->nullInstanceCheck = true;
            }
            else
            {
                ReadyToRunHelperId helperId;
                if ((flags & CORINFO_CALLINFO_FLAGS.CORINFO_CALLINFO_LDFTN) != 0)
                {
                    pResult->kind = CORINFO_CALL_KIND.CORINFO_VIRTUALCALL_LDVIRTFTN;
                    helperId = ReadyToRunHelperId.ResolveVirtualFunction;
                }
                else
                {
                    // CORINFO_CALL_CODE_POINTER tells the JIT that this is indirect
                    // call that should not be inlined.
                    pResult->kind = CORINFO_CALL_KIND.CORINFO_CALL_CODE_POINTER;
                    helperId = ReadyToRunHelperId.VirtualCall;
                }

                // If this is a non-interface call, we actually don't need a runtime lookup to find the target.
                // We don't even need to keep track of the runtime-determined method being called because the system ensures
                // that if e.g. Foo<__Canon>.GetHashCode is needed and we're generating a dictionary for Foo<string>,
                // Foo<string>.GetHashCode is needed too.
                if (pResult->exactContextNeedsRuntimeLookup && targetMethod.OwningType.IsInterface)
                {
                    // We need JitInterface changes to fully support this.
                    // If this is LDVIRTFTN of an interface method that is part of a verifiable delegate creation sequence,
                    // RyuJIT is not going to use this value.
                    Debug.Assert(helperId == ReadyToRunHelperId.ResolveVirtualFunction);
                    pResult->exactContextNeedsRuntimeLookup = false;
                    pResult->codePointerOrStubLookup.constLookup = CreateConstLookupToSymbol(_compilation.NodeFactory.ExternSymbol("NYI_LDVIRTFTN"));
                }
                else
                {
                    pResult->exactContextNeedsRuntimeLookup = false;
                    targetMethod = targetMethod.GetCanonMethodTarget(CanonicalFormKind.Specific);

                    // Get the slot defining method to make sure our virtual method use tracking gets this right.
                    // For normal C# code the targetMethod will always be newslot.
                    MethodDesc slotDefiningMethod = targetMethod.IsNewSlot ?
                        targetMethod : MetadataVirtualMethodAlgorithm.FindSlotDefiningMethodForVirtualMethod(targetMethod);

                    pResult->codePointerOrStubLookup.constLookup =
                        CreateConstLookupToSymbol(
                            _compilation.NodeFactory.ReadyToRunHelper(helperId, slotDefiningMethod));
                }

                // The current CoreRT ReadyToRun helpers do not handle null thisptr - ask the JIT to emit explicit null checks
                // TODO: Optimize this
                pResult->nullInstanceCheck = true;
            }

            pResult->hMethod = ObjectToHandle(targetMethod);

            pResult->accessAllowed = CorInfoIsAccessAllowedResult.CORINFO_ACCESS_ALLOWED;

            // We're pretty much done at this point.  Let's grab the rest of the information that the jit is going to
            // need.
            pResult->classFlags = getClassAttribsInternal(targetMethod.OwningType);

            pResult->methodFlags = getMethodAttribsInternal(targetMethod);
            Get_CORINFO_SIG_INFO(targetMethod, &pResult->sig, targetIsFatFunctionPointer);

            if ((flags & CORINFO_CALLINFO_FLAGS.CORINFO_CALLINFO_VERIFICATION) != 0)
            {
                if (pResult->hMethod != pResolvedToken.hMethod)
                {
                    pResult->verMethodFlags = getMethodAttribsInternal(targetMethod);
                    Get_CORINFO_SIG_INFO(targetMethod, &pResult->verSig);
                }
                else
                {
                    pResult->verMethodFlags = pResult->methodFlags;
                    pResult->verSig = pResult->sig;
                }
            }

            pResult->_wrapperDelegateInvoke = 0;
        }

        private void embedGenericHandle(ref CORINFO_RESOLVED_TOKEN pResolvedToken, bool fEmbedParent, ref CORINFO_GENERICHANDLE_RESULT pResult)
        {
#if DEBUG
            // In debug, write some bogus data to the struct to ensure we have filled everything
            // properly.
            fixed (CORINFO_GENERICHANDLE_RESULT* tmp = &pResult)
                MemoryHelper.FillMemory((byte*)tmp, 0xcc, Marshal.SizeOf<CORINFO_GENERICHANDLE_RESULT>());
#endif
            ReadyToRunHelperId helperId = ReadyToRunHelperId.Invalid;
            object target = null;

            if (!fEmbedParent && pResolvedToken.hMethod != null)
            {
                MethodDesc md = HandleToObject(pResolvedToken.hMethod);
                TypeDesc td = HandleToObject(pResolvedToken.hClass);

                pResult.handleType = CorInfoGenericHandleType.CORINFO_HANDLETYPE_METHOD;

                Debug.Assert(md.OwningType == td);

                pResult.compileTimeHandle = (CORINFO_GENERIC_STRUCT_*)ObjectToHandle(md);

                if (pResolvedToken.tokenType == CorInfoTokenKind.CORINFO_TOKENKIND_Ldtoken)
                    helperId = ReadyToRunHelperId.MethodHandle;
                else
                {
                    Debug.Assert(pResolvedToken.tokenType == CorInfoTokenKind.CORINFO_TOKENKIND_Method);
                    helperId = ReadyToRunHelperId.MethodDictionary;
                }

                target = GetRuntimeDeterminedObjectForToken(ref pResolvedToken);
            }
            else if (!fEmbedParent && pResolvedToken.hField != null)
            {
                FieldDesc fd = HandleToObject(pResolvedToken.hField);
                TypeDesc td = HandleToObject(pResolvedToken.hClass);

                pResult.handleType = CorInfoGenericHandleType.CORINFO_HANDLETYPE_FIELD;
                pResult.compileTimeHandle = (CORINFO_GENERIC_STRUCT_*)pResolvedToken.hField;

                Debug.Assert(pResolvedToken.tokenType == CorInfoTokenKind.CORINFO_TOKENKIND_Ldtoken);
                helperId = ReadyToRunHelperId.FieldHandle;
                target = GetRuntimeDeterminedObjectForToken(ref pResolvedToken);
            }
            else
            {
                TypeDesc td = HandleToObject(pResolvedToken.hClass);

                pResult.handleType = CorInfoGenericHandleType.CORINFO_HANDLETYPE_CLASS;
                pResult.compileTimeHandle = (CORINFO_GENERIC_STRUCT_*)pResolvedToken.hClass;

                object obj = GetRuntimeDeterminedObjectForToken(ref pResolvedToken);
                target = obj as TypeDesc;
                if (target == null)
                {
                    Debug.Assert(fEmbedParent);

                    if (obj is MethodDesc objAsMethod)
                    {
                        target = objAsMethod.OwningType;
                    }
                    else
                    {
                        Debug.Assert(obj is FieldDesc);
                        target = ((FieldDesc)obj).OwningType;
                    }
                }

                if (pResolvedToken.tokenType == CorInfoTokenKind.CORINFO_TOKENKIND_NewObj
                        || pResolvedToken.tokenType == CorInfoTokenKind.CORINFO_TOKENKIND_Newarr
                        || pResolvedToken.tokenType == CorInfoTokenKind.CORINFO_TOKENKIND_Box
                        || pResolvedToken.tokenType == CorInfoTokenKind.CORINFO_TOKENKIND_Constrained)
                {
                    helperId = ReadyToRunHelperId.TypeHandle;
                }
                else if (pResolvedToken.tokenType == CorInfoTokenKind.CORINFO_TOKENKIND_Casting)
                {
                    helperId = ReadyToRunHelperId.TypeHandleForCasting;
                }
                else if (pResolvedToken.tokenType == CorInfoTokenKind.CORINFO_TOKENKIND_Ldtoken)
                {
                    helperId = _compilation.GetLdTokenHelperForType(td);
                }
                else
                {
                    helperId = ReadyToRunHelperId.NecessaryTypeHandle;
                }
            }

            Debug.Assert(pResult.compileTimeHandle != null);

            ComputeLookup(ref pResolvedToken, target, helperId, ref pResult.lookup);
        }

        private CORINFO_METHOD_STRUCT_* embedMethodHandle(CORINFO_METHOD_STRUCT_* handle, ref void* ppIndirection)
        {
            MethodDesc method = HandleToObject(handle);
            ISymbolNode methodHandleSymbol = _compilation.NodeFactory.RuntimeMethodHandle(method);
            CORINFO_METHOD_STRUCT_* result = (CORINFO_METHOD_STRUCT_*)ObjectToHandle(methodHandleSymbol);

            if (methodHandleSymbol.RepresentsIndirectionCell)
            {
                ppIndirection = result;
                return null;
            }
            else
            {
                ppIndirection = null;
                return result;
            }
        }

        private void getMethodVTableOffset(CORINFO_METHOD_STRUCT_* method, ref uint offsetOfIndirection, ref uint offsetAfterIndirection, ref bool isRelative)
        {
            MethodDesc methodDesc = HandleToObject(method);
            int pointerSize = _compilation.TypeSystemContext.Target.PointerSize;
            offsetOfIndirection = (uint)CORINFO_VIRTUALCALL_NO_CHUNK.Value;
            isRelative = false;

            // Normalize to the slot defining method. We don't have slot information for the overrides.
            methodDesc = MetadataVirtualMethodAlgorithm.FindSlotDefiningMethodForVirtualMethod(methodDesc);
            Debug.Assert(!methodDesc.CanMethodBeInSealedVTable());

            int slot = VirtualMethodSlotHelper.GetVirtualMethodSlot(_compilation.NodeFactory, methodDesc, methodDesc.OwningType);
            Debug.Assert(slot != -1);

            offsetAfterIndirection = (uint)(EETypeNode.GetVTableOffset(pointerSize) + slot * pointerSize);
        }

        private void expandRawHandleIntrinsic(ref CORINFO_RESOLVED_TOKEN pResolvedToken, ref CORINFO_GENERICHANDLE_RESULT pResult)
        {
            // Resolved token as a potentially RuntimeDetermined object.
            MethodDesc method = (MethodDesc)GetRuntimeDeterminedObjectForToken(ref pResolvedToken);

            switch (method.Name)
            {
                case "EETypePtrOf":
                    ComputeLookup(ref pResolvedToken, method.Instantiation[0], ReadyToRunHelperId.TypeHandle, ref pResult.lookup);
                    break;
                case "DefaultConstructorOf":
                    ComputeLookup(ref pResolvedToken, method.Instantiation[0], ReadyToRunHelperId.DefaultConstructor, ref pResult.lookup);
                    break;
                case "AllocatorOf":
                    ComputeLookup(ref pResolvedToken, method.Instantiation[0], ReadyToRunHelperId.ObjectAllocator, ref pResult.lookup);
                    break;
            }
        }

        private uint getMethodAttribs(CORINFO_METHOD_STRUCT_* ftn)
        {
            return getMethodAttribsInternal(HandleToObject(ftn));
        }

        private void* getMethodSync(CORINFO_METHOD_STRUCT_* ftn, ref void* ppIndirection)
        {
            MethodDesc method = HandleToObject(ftn);
            TypeDesc type = method.OwningType;
            ISymbolNode methodSync = _compilation.NodeFactory.NecessaryTypeSymbol(type);

            void* result = (void*)ObjectToHandle(methodSync);

            if (methodSync.RepresentsIndirectionCell)
            {
                ppIndirection = result;
                return null;
            }
            else
            {
                ppIndirection = null;
                return result;
            }
        }

        private HRESULT allocMethodBlockCounts(uint count, ref BlockCounts* pBlockCounts)
        {
            throw new NotImplementedException("allocMethodBlockCounts");
        }

        private HRESULT getMethodBlockCounts(CORINFO_METHOD_STRUCT_* ftnHnd, ref uint pCount, ref BlockCounts* pBlockCounts, ref uint pNumRuns)
        { throw new NotImplementedException("getBBProfileData"); }

        private void getAddressOfPInvokeTarget(CORINFO_METHOD_STRUCT_* method, ref CORINFO_CONST_LOOKUP pLookup)
        {
            MethodDesc md = HandleToObject(method);

            string externName = md.GetPInvokeMethodMetadata().Name ?? md.Name;
            Debug.Assert(externName != null);

            pLookup = CreateConstLookupToSymbol(_compilation.NodeFactory.ExternSymbol(externName));
        }

        private void getGSCookie(IntPtr* pCookieVal, IntPtr** ppCookieVal)
        {
            // TODO: fully implement GS cookies

            if (pCookieVal != null)
            {
                if (PointerSize == 4)
                {
                    *pCookieVal = (IntPtr)0x3F796857;
                }
                else
                {
                    *pCookieVal = (IntPtr)0x216D6F6D202C6948;
                }
                *ppCookieVal = null;
            }
            else
            {
                throw new NotImplementedException("getGSCookie");
            }
        }

        private bool pInvokeMarshalingRequired(CORINFO_METHOD_STRUCT_* handle, CORINFO_SIG_INFO* callSiteSig)
        {
            // calli is covered by convertPInvokeCalliToCall
            if (handle == null)
            {
#if DEBUG
                MethodSignature methodSignature = (MethodSignature)HandleToObject((IntPtr)callSiteSig->pSig);

                MethodDesc stub = _compilation.PInvokeILProvider.GetCalliStub(methodSignature);
                Debug.Assert(!IsPInvokeStubRequired(stub));
#endif

                return false;
            }

            MethodDesc method = HandleToObject(handle);

            if (method.IsRawPInvoke())
                return false;

            // We could have given back the PInvoke stub IL to the JIT and let it inline it, without
            // checking whether there is any stub required. Save the JIT from doing the inlining by checking upfront.
            return IsPInvokeStubRequired(method);
        }

        private bool convertPInvokeCalliToCall(ref CORINFO_RESOLVED_TOKEN pResolvedToken, bool mustConvert)
        {
            var methodIL = (MethodIL)HandleToObject((IntPtr)pResolvedToken.tokenScope);
            if (methodIL.OwningMethod.IsPInvoke)
            {
                return false;
            }

            MethodSignature signature = (MethodSignature)methodIL.GetObject((int)pResolvedToken.token);

            CorInfoCallConv callConv = (CorInfoCallConv)(signature.Flags & MethodSignatureFlags.UnmanagedCallingConventionMask);
            if (callConv != CorInfoCallConv.CORINFO_CALLCONV_C &&
                callConv != CorInfoCallConv.CORINFO_CALLCONV_STDCALL &&
                callConv != CorInfoCallConv.CORINFO_CALLCONV_THISCALL &&
                callConv != CorInfoCallConv.CORINFO_CALLCONV_FASTCALL)
            {
                return false;
            }

            MethodDesc stub = _compilation.PInvokeILProvider.GetCalliStub(signature);
            if (!mustConvert && !IsPInvokeStubRequired(stub))
                return false;

            pResolvedToken.hMethod = ObjectToHandle(stub);
            pResolvedToken.hClass = ObjectToHandle(stub.OwningType);
            return true;
        }

        private bool IsPInvokeStubRequired(MethodDesc method)
        {
            if (_compilation.GetMethodIL(method) is Internal.IL.Stubs.PInvokeILStubMethodIL stub)
                return stub.IsStubRequired;

            // This path is taken for PInvokes replaced by RemovingILProvider
            return true;
        }

        private int SizeOfPInvokeTransitionFrame
        {
            get
            {
                // struct PInvokeTransitionFrame:
                // #ifdef _TARGET_ARM_
                //  m_ChainPointer
                // #endif
                //  m_RIP
                //  m_FramePointer
                //  m_pThread
                //  m_Flags + align (no align for ARM64 that has 64 bit m_Flags)
                //  m_PreserverRegs - RSP
                //      No need to save other preserved regs because of the JIT ensures that there are
                //      no live GC references in callee saved registers around the PInvoke callsite.
                int size = 5 * this.PointerSize;

                if (_compilation.TypeSystemContext.Target.Architecture == TargetArchitecture.ARM)
                    size += this.PointerSize; // m_ChainPointer

                return size;
            }
        }

        private bool canGetCookieForPInvokeCalliSig(CORINFO_SIG_INFO* szMetaSig)
        { throw new NotImplementedException("canGetCookieForPInvokeCalliSig"); }

        private void classMustBeLoadedBeforeCodeIsRun(CORINFO_CLASS_STRUCT_* cls)
        {
        }

        private void setEHcount(uint cEH)
        {
            _ehClauses = new CORINFO_EH_CLAUSE[cEH];
        }

        private void setEHinfo(uint EHnumber, ref CORINFO_EH_CLAUSE clause)
        {
            _ehClauses[EHnumber] = clause;
        }

        private void reportInliningDecision(CORINFO_METHOD_STRUCT_* inlinerHnd, CORINFO_METHOD_STRUCT_* inlineeHnd, CorInfoInline inlineResult, byte* reason)
        {
        }

        private void getFieldInfo(ref CORINFO_RESOLVED_TOKEN pResolvedToken, CORINFO_METHOD_STRUCT_* callerHandle, CORINFO_ACCESS_FLAGS flags, CORINFO_FIELD_INFO* pResult)
        {
#if DEBUG
            // In debug, write some bogus data to the struct to ensure we have filled everything
            // properly.
            MemoryHelper.FillMemory((byte*)pResult, 0xcc, Marshal.SizeOf<CORINFO_FIELD_INFO>());
#endif

            Debug.Assert(((int)flags & ((int)CORINFO_ACCESS_FLAGS.CORINFO_ACCESS_GET |
                                        (int)CORINFO_ACCESS_FLAGS.CORINFO_ACCESS_SET |
                                        (int)CORINFO_ACCESS_FLAGS.CORINFO_ACCESS_ADDRESS |
                                        (int)CORINFO_ACCESS_FLAGS.CORINFO_ACCESS_INIT_ARRAY)) != 0);

            var field = HandleToObject(pResolvedToken.hField);

            CORINFO_FIELD_ACCESSOR fieldAccessor;
            CORINFO_FIELD_FLAGS fieldFlags = (CORINFO_FIELD_FLAGS)0;
            uint fieldOffset = (field.IsStatic && field.HasRva ? 0xBAADF00D : (uint)field.Offset.AsInt);

            if (field.IsStatic)
            {
                fieldFlags |= CORINFO_FIELD_FLAGS.CORINFO_FLG_FIELD_STATIC;

                if (field.HasRva)
                {
                    fieldFlags |= CORINFO_FIELD_FLAGS.CORINFO_FLG_FIELD_UNMANAGED;

                    // TODO: Handle the case when the RVA is in the TLS range
                    fieldAccessor = CORINFO_FIELD_ACCESSOR.CORINFO_FIELD_STATIC_RVA_ADDRESS;

                    // We are not going through a helper. The constructor has to be triggered explicitly.
                    if (_compilation.HasLazyStaticConstructor(field.OwningType))
                    {
                        fieldFlags |= CORINFO_FIELD_FLAGS.CORINFO_FLG_FIELD_INITCLASS;
                    }
                }
                else if (field.OwningType.IsCanonicalSubtype(CanonicalFormKind.Any))
                {
                    // The JIT wants to know how to access a static field on a generic type. We need a runtime lookup.
                    fieldAccessor = CORINFO_FIELD_ACCESSOR.CORINFO_FIELD_STATIC_READYTORUN_HELPER;
                    pResult->helper = CorInfoHelpFunc.CORINFO_HELP_READYTORUN_GENERIC_STATIC_BASE;

                    // Don't try to compute the runtime lookup if we're inlining. The JIT is going to abort the inlining
                    // attempt anyway.
                    if (pResolvedToken.tokenContext == contextFromMethodBeingCompiled())
                    {
                        MethodDesc contextMethod = methodFromContext(pResolvedToken.tokenContext);

                        FieldDesc runtimeDeterminedField = (FieldDesc)GetRuntimeDeterminedObjectForToken(ref pResolvedToken);

                        ReadyToRunHelperId helperId;

                        // Find out what kind of base do we need to look up.
                        if (field.IsThreadStatic)
                        {
                            helperId = ReadyToRunHelperId.GetThreadStaticBase;
                        }
                        else if (field.HasGCStaticBase)
                        {
                            helperId = ReadyToRunHelperId.GetGCStaticBase;
                        }
                        else
                        {
                            helperId = ReadyToRunHelperId.GetNonGCStaticBase;
                        }

                        // What generic context do we look up the base from.
                        ISymbolNode helper;
                        if (contextMethod.AcquiresInstMethodTableFromThis() || contextMethod.RequiresInstMethodTableArg())
                        {
                            helper = _compilation.NodeFactory.ReadyToRunHelperFromTypeLookup(
                                helperId, runtimeDeterminedField.OwningType, contextMethod.OwningType);
                        }
                        else
                        {
                            Debug.Assert(contextMethod.RequiresInstMethodDescArg());
                            helper = _compilation.NodeFactory.ReadyToRunHelperFromDictionaryLookup(
                                helperId, runtimeDeterminedField.OwningType, contextMethod);
                        }

                        pResult->fieldLookup = CreateConstLookupToSymbol(helper);
                    }
                }
                else
                {
                    fieldAccessor = CORINFO_FIELD_ACCESSOR.CORINFO_FIELD_STATIC_SHARED_STATIC_HELPER;
                    pResult->helper = CorInfoHelpFunc.CORINFO_HELP_READYTORUN_STATIC_BASE;

                    ReadyToRunHelperId helperId = ReadyToRunHelperId.Invalid;
                    CORINFO_FIELD_ACCESSOR intrinsicAccessor;
                    if (field.IsIntrinsic &&
                        (flags & CORINFO_ACCESS_FLAGS.CORINFO_ACCESS_GET) != 0 &&
                        (intrinsicAccessor = getFieldIntrinsic(field)) != (CORINFO_FIELD_ACCESSOR)(-1))
                    {
                        fieldAccessor = intrinsicAccessor;
                    }
                    else if (field.IsThreadStatic)
                    {
                        helperId = ReadyToRunHelperId.GetThreadStaticBase;
                    }
                    else
                    {
                        helperId = field.HasGCStaticBase ?
                            ReadyToRunHelperId.GetGCStaticBase :
                            ReadyToRunHelperId.GetNonGCStaticBase;

                        //
                        // Currently, we only do this optimization for regular statics, but it
                        // looks like it may be permissible to do this optimization for
                        // thread statics as well.
                        //
                        if ((flags & CORINFO_ACCESS_FLAGS.CORINFO_ACCESS_ADDRESS) != 0 &&
                            (fieldAccessor != CORINFO_FIELD_ACCESSOR.CORINFO_FIELD_STATIC_TLS))
                        {
                            fieldFlags |= CORINFO_FIELD_FLAGS.CORINFO_FLG_FIELD_SAFESTATIC_BYREF_RETURN;
                        }
                    }

                    if (helperId != ReadyToRunHelperId.Invalid)
                    {
                        pResult->fieldLookup = CreateConstLookupToSymbol(
                            _compilation.NodeFactory.ReadyToRunHelper(helperId, field.OwningType));
                    }
                }
            }
            else
            {
                fieldAccessor = CORINFO_FIELD_ACCESSOR.CORINFO_FIELD_INSTANCE;
            }

            if (field.IsInitOnly)
                fieldFlags |= CORINFO_FIELD_FLAGS.CORINFO_FLG_FIELD_FINAL;

            pResult->fieldAccessor = fieldAccessor;
            pResult->fieldFlags = fieldFlags;
            pResult->fieldType = getFieldType(pResolvedToken.hField, &pResult->structType, pResolvedToken.hClass);
            pResult->accessAllowed = CorInfoIsAccessAllowedResult.CORINFO_ACCESS_ALLOWED;
            pResult->offset = fieldOffset;

            // TODO: We need to implement access checks for fields and methods.  See JitInterface.cpp in mrtjit
            //       and STS::AccessCheck::CanAccess.
        }
    }
}
