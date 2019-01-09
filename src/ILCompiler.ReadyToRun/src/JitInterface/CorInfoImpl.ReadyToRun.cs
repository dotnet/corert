// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using ILCompiler;
using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysis.ReadyToRun;

using ReadyToRunHelper = ILCompiler.ReadyToRunHelper;
using System.Collections.Generic;

namespace Internal.JitInterface
{
    public class MethodWithToken
    {
        public readonly MethodDesc Method;
        public readonly ModuleToken Token;

        public MethodWithToken(MethodDesc method, ModuleToken token)
        {
            Method = method;
            Token = token;
        }
    }

    public class MethodContext
    {
        public readonly MethodDesc Method;
        public readonly IntPtr Context;

        public MethodContext(MethodDesc method, IntPtr context)
        {
            Method = method;
            Context = context;
        }

        public override bool Equals(object obj)
        {
            return obj is MethodContext other && Method == other.Method && Context == other.Context;
        }

        public override int GetHashCode()
        {
            int hashCode = -117376276;
            hashCode = hashCode * -1521134295 + Method.GetHashCode();
            hashCode = hashCode * -1521134295 + Context.GetHashCode();
            return hashCode;
        }
    }

    public class RequiresRuntimeJitException : Exception
    {
        public RequiresRuntimeJitException(object reason)
            : base(reason.ToString())
        {
        }
    }

    unsafe partial class CorInfoImpl
    {
        private const CORINFO_RUNTIME_ABI TargetABI = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI;

        private uint OffsetOfDelegateFirstTarget => (uint)(3 * PointerSize); // Delegate::m_functionPointer

        private readonly ReadyToRunCodegenCompilation _compilation;
        private IReadyToRunMethodCodeNode _methodCodeNode;
        private OffsetMapping[] _debugLocInfos;
        private NativeVarInfo[] _debugVarInfos;

        /// <summary>
        /// Input ECMA module. In multi-file compilation, multiple CorInfoImpl objects are created
        /// for the individual modules to propagate module information through JIT back to managed
        /// code to provide base module for reference token resolution.
        /// </summary>
        private readonly EcmaModule _tokenContext;

        private readonly SignatureContext _signatureContext;

        public CorInfoImpl(ReadyToRunCodegenCompilation compilation, EcmaModule tokenContext, JitConfigProvider jitConfig)
            : this(jitConfig)
        {
            _compilation = compilation;
            _tokenContext = tokenContext;
            _signatureContext = new SignatureContext(_compilation.NodeFactory.Resolver);
        }

        public void CompileMethod(IReadyToRunMethodCodeNode methodCodeNodeNeedingCode)
        {
            _methodCodeNode = methodCodeNodeNeedingCode;

            CompileMethodInternal(methodCodeNodeNeedingCode);
        }

        private void ComputeLookup(ref CORINFO_RESOLVED_TOKEN pResolvedToken, object entity, ReadyToRunHelperId helperId, ref CORINFO_LOOKUP lookup)
        {
            if (_compilation.NeedsRuntimeLookup(helperId, entity))
            {
                lookup.lookupKind.needsRuntimeLookup = true;
                lookup.runtimeLookup.signature = null;

                MethodDesc contextMethod = methodFromContext(pResolvedToken.tokenContext);

                // Do not bother computing the runtime lookup if we are inlining. The JIT is going
                // to abort the inlining attempt anyway.
                if (contextMethod != MethodBeingCompiled)
                    return;

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

                    lookup.runtimeLookup.indirections = (ushort)genericLookup.NumberOfIndirections;
                    lookup.runtimeLookup.offset0 = (IntPtr)genericLookup[0];
                    if (genericLookup.NumberOfIndirections > 1)
                        lookup.runtimeLookup.offset1 = (IntPtr)genericLookup[1];
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
                ISymbolNode constLookup = _compilation.SymbolNodeFactory.ComputeConstantLookup(helperId, entity, _signatureContext);
                lookup.constLookup = CreateConstLookupToSymbol(constLookup);
            }
        }

        private bool getReadyToRunHelper(ref CORINFO_RESOLVED_TOKEN pResolvedToken, ref CORINFO_LOOKUP_KIND pGenericLookupKind, CorInfoHelpFunc id, ref CORINFO_CONST_LOOKUP pLookup)
        {
            switch (id)
            {
                case CorInfoHelpFunc.CORINFO_HELP_READYTORUN_NEW:
                    {
                        var type = HandleToObject(pResolvedToken.hClass);
                        Debug.Assert(type.IsDefType);
                        if (type.IsCanonicalSubtype(CanonicalFormKind.Any))
                            return false;

                        pLookup = CreateConstLookupToSymbol(_compilation.SymbolNodeFactory.ReadyToRunHelper(ReadyToRunHelperId.NewHelper, type, _signatureContext));
                    }
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_READYTORUN_NEWARR_1:
                    {
                        var type = HandleToObject(pResolvedToken.hClass);
                        Debug.Assert(type.IsSzArray);
                        if (type.IsCanonicalSubtype(CanonicalFormKind.Any))
                            return false;

                        pLookup = CreateConstLookupToSymbol(_compilation.SymbolNodeFactory.ReadyToRunHelper(ReadyToRunHelperId.NewArr1, type, _signatureContext));
                    }
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_READYTORUN_ISINSTANCEOF:
                    {
                        var type = HandleToObject(pResolvedToken.hClass);
                        if (type.IsCanonicalSubtype(CanonicalFormKind.Any))
                            return false;

                        // ECMA-335 III.4.3:  If typeTok is a nullable type, Nullable<T>, it is interpreted as "boxed" T
                        if (type.IsNullable)
                            type = type.Instantiation[0];

                        pLookup = CreateConstLookupToSymbol(_compilation.SymbolNodeFactory.ReadyToRunHelper(ReadyToRunHelperId.IsInstanceOf, type, _signatureContext));
                    }
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_READYTORUN_CHKCAST:
                    {
                        var type = HandleToObject(pResolvedToken.hClass);
                        if (type.IsCanonicalSubtype(CanonicalFormKind.Any))
                            return false;

                        // ECMA-335 III.4.3:  If typeTok is a nullable type, Nullable<T>, it is interpreted as "boxed" T
                        if (type.IsNullable)
                            type = type.Instantiation[0];

                        pLookup = CreateConstLookupToSymbol(_compilation.SymbolNodeFactory.ReadyToRunHelper(ReadyToRunHelperId.CastClass, type, _signatureContext));
                    }
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_READYTORUN_STATIC_BASE:
                    {
                        var type = HandleToObject(pResolvedToken.hClass);
                        if (type.IsCanonicalSubtype(CanonicalFormKind.Any))
                            return false;

                        pLookup = CreateConstLookupToSymbol(_compilation.SymbolNodeFactory.ReadyToRunHelper(ReadyToRunHelperId.GetNonGCStaticBase, type, _signatureContext));
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
                        MethodContext methodContext = new MethodContext(methodFromContext(pResolvedToken.tokenContext), new IntPtr(pResolvedToken.tokenContext));
                        ISymbolNode helper = _compilation.SymbolNodeFactory.GenericLookupHelper(
                            pGenericLookupKind.runtimeLookupKind,
                            ReadyToRunHelperId.GetNonGCStaticBase, 
                            helperArg, 
                            methodContext, 
                            _signatureContext);
                        pLookup = CreateConstLookupToSymbol(helper);
                    }
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_READYTORUN_GENERIC_HANDLE:
                    {
                        Debug.Assert(pGenericLookupKind.needsRuntimeLookup);

                        ReadyToRunHelperId helperId = (ReadyToRunHelperId)pGenericLookupKind.runtimeLookupFlags;
                        object helperArg = HandleToObject((IntPtr)pGenericLookupKind.runtimeLookupArgs);
                        if (helperArg is MethodDesc methodArg)
                        {
                            helperArg = new MethodWithToken(methodArg, new ModuleToken(_tokenContext, pResolvedToken.token));
                        }
                        MethodContext methodContext = new MethodContext(methodFromContext(pResolvedToken.tokenContext), new IntPtr(pResolvedToken.tokenContext));
                        ISymbolNode helper = _compilation.SymbolNodeFactory.GenericLookupHelper(
                            pGenericLookupKind.runtimeLookupKind,
                            helperId,
                            helperArg, 
                            methodContext, 
                            _signatureContext);
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

            /* TODO
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
            */
            {
                pLookup.lookupKind.needsRuntimeLookup = false;
                pLookup.constLookup = CreateConstLookupToSymbol(_compilation.SymbolNodeFactory.DelegateCtor(
                    delegateTypeDesc, targetMethod, new ModuleToken(_tokenContext, (mdToken)pTargetMethod.token), _signatureContext));
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
                case CorInfoHelpFunc.CORINFO_HELP_NEW_MDARR:
                    id = ReadyToRunHelper.NewMultiDimArr;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_NEW_MDARR_NONVARARG:
                    id = ReadyToRunHelper.NewMultiDimArr_NonVarArg;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_NEWFAST:
                    id = ReadyToRunHelper.NewObject;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_NEWARR_1_DIRECT:
                    id = ReadyToRunHelper.NewArray;
                    break;

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

                case CorInfoHelpFunc.CORINFO_HELP_ASSIGN_REF_EAX:
                    id = ReadyToRunHelper.WriteBarrier_EAX;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_ASSIGN_REF_EBX:
                    id = ReadyToRunHelper.WriteBarrier_EBX;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_ASSIGN_REF_ECX:
                    id = ReadyToRunHelper.WriteBarrier_ECX;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_ASSIGN_REF_ESI:
                    id = ReadyToRunHelper.WriteBarrier_ESI;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_ASSIGN_REF_EDI:
                    id = ReadyToRunHelper.WriteBarrier_EDI;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_ASSIGN_REF_EBP:
                    id = ReadyToRunHelper.WriteBarrier_EBP;
                    break;

                case CorInfoHelpFunc.CORINFO_HELP_CHECKED_ASSIGN_REF_EAX:
                    id = ReadyToRunHelper.CheckedWriteBarrier_EAX;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_CHECKED_ASSIGN_REF_EBX:
                    id = ReadyToRunHelper.CheckedWriteBarrier_EBX;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_CHECKED_ASSIGN_REF_ECX:
                    id = ReadyToRunHelper.CheckedWriteBarrier_ECX;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_CHECKED_ASSIGN_REF_ESI:
                    id = ReadyToRunHelper.CheckedWriteBarrier_ESI;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_CHECKED_ASSIGN_REF_EDI:
                    id = ReadyToRunHelper.CheckedWriteBarrier_EDI;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_CHECKED_ASSIGN_REF_EBP:
                    id = ReadyToRunHelper.CheckedWriteBarrier_EBP;
                    break;

                case CorInfoHelpFunc.CORINFO_HELP_ENDCATCH:
                    id = ReadyToRunHelper.EndCatch;
                    break;

                default:
                    throw new NotImplementedException(ftnNum.ToString());
            }

            return _compilation.SymbolNodeFactory.ExternSymbol(id);
        }

        private void getFunctionEntryPoint(CORINFO_METHOD_STRUCT_* ftn, ref CORINFO_CONST_LOOKUP pResult, CORINFO_ACCESS_FLAGS accessFlags)
        {
            throw new RequiresRuntimeJitException(HandleToObject(ftn).ToString());
        }

        private bool canTailCall(CORINFO_METHOD_STRUCT_* callerHnd, CORINFO_METHOD_STRUCT_* declaredCalleeHnd, CORINFO_METHOD_STRUCT_* exactCalleeHnd, bool fIsTailPrefix)
        {
            if (fIsTailPrefix)
            {
                throw new RequiresRuntimeJitException(nameof(fIsTailPrefix));
            }

            return false;
        }

        private InfoAccessType constructStringLiteral(CORINFO_MODULE_STRUCT_* module, mdToken metaTok, ref void* ppValue)
        {
            ISymbolNode stringObject = _compilation.SymbolNodeFactory.StringLiteral(new ModuleToken(_tokenContext, metaTok));
            ppValue = (void*)ObjectToHandle(stringObject);
            return InfoAccessType.IAT_PPVALUE;
        }

        public ISymbolNode ComputeConstantLookup(ReadyToRunHelperId helperId, object entity, SignatureContext signatureContext)
        {
            return _compilation.SymbolNodeFactory.ReadyToRunHelper(helperId, entity, signatureContext);
        }

        enum EHInfoFields
        {
            Flags = 0,
            TryOffset = 1,
            TryEnd = 2,
            HandlerOffset = 3,
            HandlerEnd = 4,
            ClassTokenOrOffset = 5,

            Length
        }

        private ObjectNode.ObjectData EncodeEHInfo()
        {
            int totalClauses = _ehClauses.Length;
            byte[] ehInfoData = new byte[(int)EHInfoFields.Length * sizeof(uint) * totalClauses];

            for (int i = 0; i < totalClauses; i++)
            {
                ref CORINFO_EH_CLAUSE clause = ref _ehClauses[i];
                int clauseOffset = (int)EHInfoFields.Length * sizeof(uint) * i;
                Array.Copy(BitConverter.GetBytes((uint)clause.Flags), 0, ehInfoData, clauseOffset + (int)EHInfoFields.Flags * sizeof(uint), sizeof(uint));
                Array.Copy(BitConverter.GetBytes((uint)clause.TryOffset), 0, ehInfoData, clauseOffset + (int)EHInfoFields.TryOffset * sizeof(uint), sizeof(uint));
                // JIT in fact returns the end offset in the length field
                Array.Copy(BitConverter.GetBytes((uint)(clause.TryLength)), 0, ehInfoData, clauseOffset + (int)EHInfoFields.TryEnd * sizeof(uint), sizeof(uint));
                Array.Copy(BitConverter.GetBytes((uint)clause.HandlerOffset), 0, ehInfoData, clauseOffset + (int)EHInfoFields.HandlerOffset * sizeof(uint), sizeof(uint));
                Array.Copy(BitConverter.GetBytes((uint)(clause.HandlerLength)), 0, ehInfoData, clauseOffset + (int)EHInfoFields.HandlerEnd * sizeof(uint), sizeof(uint));
                Array.Copy(BitConverter.GetBytes((uint)clause.ClassTokenOrOffset), 0, ehInfoData, clauseOffset + (int)EHInfoFields.ClassTokenOrOffset * sizeof(uint), sizeof(uint));
            }
            return new ObjectNode.ObjectData(ehInfoData, Array.Empty<Relocation>(), alignment: 1, definedSymbols: Array.Empty<ISymbolDefinitionNode>());
        }

        /// <summary>
        /// Create a NativeVarInfo array; a table from native code range to variable location
        /// on the stack / in a specific register.
        /// </summary>
        private void setVars(CORINFO_METHOD_STRUCT_* ftn, uint cVars, NativeVarInfo* vars)
        {
            Debug.Assert(_debugVarInfos == null);

            if (cVars == 0)
                return;

            _debugVarInfos = new NativeVarInfo[cVars];
            for (int i = 0; i < cVars; i++)
            {
                _debugVarInfos[i] = vars[i];
            }
        }

        /// <summary>
        /// Create a DebugLocInfo array; a table from native offset to IL offset.
        /// </summary>
        private void setBoundaries(CORINFO_METHOD_STRUCT_* ftn, uint cMap, OffsetMapping* pMap)
        {
            Debug.Assert(_debugLocInfos == null);

            _debugLocInfos = new OffsetMapping[cMap];
            for (int i = 0; i < cMap; i++)
            {
                _debugLocInfos[i] = pMap[i];
            }
        }

        private void PublishEmptyCode()
        {
            _methodCodeNode.SetCode(new ObjectNode.ObjectData(Array.Empty<byte>(), null, 1, Array.Empty<ISymbolDefinitionNode>()));
            _methodCodeNode.InitializeFrameInfos(Array.Empty<FrameInfo>());
        }

        private CorInfoHelpFunc getCastingHelper(ref CORINFO_RESOLVED_TOKEN pResolvedToken, bool fThrowing)
        {
            return fThrowing ? CorInfoHelpFunc.CORINFO_HELP_CHKCASTANY : CorInfoHelpFunc.CORINFO_HELP_ISINSTANCEOFANY;
        }

        private CorInfoHelpFunc getNewHelper(ref CORINFO_RESOLVED_TOKEN pResolvedToken, CORINFO_METHOD_STRUCT_* callerHandle)
        {
            return CorInfoHelpFunc.CORINFO_HELP_NEWFAST;
        }

        private CorInfoHelpFunc getNewArrHelper(CORINFO_CLASS_STRUCT_* arrayCls)
        {
            return CorInfoHelpFunc.CORINFO_HELP_NEWARR_1_DIRECT;
        }
    }
}
