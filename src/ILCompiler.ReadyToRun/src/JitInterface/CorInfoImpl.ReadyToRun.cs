// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

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

        public override bool Equals(object obj)
        {
            return obj is MethodWithToken methodWithToken &&
                Equals(methodWithToken);
        }

        public override int GetHashCode()
        {
            return Method.GetHashCode() ^ unchecked(17 * Token.GetHashCode());
        }

        public bool Equals(MethodWithToken methodWithToken)
        {
            return Method == methodWithToken.Method && Token.Equals(methodWithToken.Token);
        }
    }

    public struct GenericContext : IEquatable<GenericContext>
    {
        public readonly TypeSystemEntity Context;

        public TypeDesc ContextType { get { return (Context is MethodDesc contextAsMethod ? contextAsMethod.OwningType : (TypeDesc)Context); } }

        public MethodDesc ContextMethod { get { return (MethodDesc)Context; } }

        public GenericContext(TypeSystemEntity context)
        {
            Context = context;
        }

        public bool Equals(GenericContext other) => Context == other.Context;

        public override bool Equals(object obj) => obj is GenericContext other && Context == other.Context;

        public override int GetHashCode() => Context.GetHashCode();

        public override string ToString() => Context.ToString();
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
                if (contextMethod != _methodCodeNode.Method)
                {
                    return;
                }

                // There is a pathological case where invalid IL refereces __Canon type directly, but there is no dictionary available to store the lookup. 
                // All callers of ComputeRuntimeLookupForSharedGenericToken have to filter out this case. We can't do much about it here.
                Debug.Assert(contextMethod.IsSharedByGenericInstantiations);

                if (contextMethod.RequiresInstMethodDescArg())
                {
                    lookup.lookupKind.runtimeLookupKind = CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_METHODPARAM;
                }
                else
                {
                    if (contextMethod.RequiresInstMethodTableArg())
                    {
                        lookup.lookupKind.runtimeLookupKind = CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_CLASSPARAM;
                    }
                    else
                    {
                        lookup.lookupKind.runtimeLookupKind = CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_THISOBJ;
                    }
                }

                // Unless we decide otherwise, just do the lookup via a helper function
                lookup.runtimeLookup.indirections = CORINFO.USEHELPER;

                lookup.lookupKind.runtimeLookupFlags = (ushort)helperId;
                lookup.lookupKind.runtimeLookupArgs = (void*)ObjectToHandle(entity);

                lookup.runtimeLookup.indirectFirstOffset = false;
                lookup.runtimeLookup.indirectSecondOffset = false;

                // For R2R compilations, we don't generate the dictionary lookup signatures (dictionary lookups are done in a 
                // different way that is more version resilient... plus we can't have pointers to existing MTs/MDs in the sigs)
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

                        pLookup = CreateConstLookupToSymbol(_compilation.SymbolNodeFactory.ReadyToRunHelper(ReadyToRunHelperId.CctorTrigger, type, _signatureContext));
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
                        GenericContext methodContext = new GenericContext(entityFromContext(pResolvedToken.tokenContext));
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

            pLookup.lookupKind.needsRuntimeLookup = false;
            pLookup.constLookup = CreateConstLookupToSymbol(_compilation.SymbolNodeFactory.DelegateCtor(
                    delegateTypeDesc, targetMethod, new ModuleToken(_tokenContext, (mdToken)pTargetMethod.token), _signatureContext));
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


                case CorInfoHelpFunc.CORINFO_HELP_GETGENERICS_GCSTATIC_BASE:
                    id = ReadyToRunHelper.GenericGcStaticBase;
                    break;

                case CorInfoHelpFunc.CORINFO_HELP_GETGENERICS_NONGCSTATIC_BASE:
                    id = ReadyToRunHelper.GenericNonGcStaticBase;
                    break;

                case CorInfoHelpFunc.CORINFO_HELP_GETGENERICS_GCTHREADSTATIC_BASE:
                    id = ReadyToRunHelper.GenericGcTlsBase;
                    break;

                case CorInfoHelpFunc.CORINFO_HELP_GETGENERICS_NONGCTHREADSTATIC_BASE:
                    id = ReadyToRunHelper.GenericNonGcTlsBase;
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
                case CorInfoHelpFunc.CORINFO_HELP_VIRTUAL_FUNC_PTR:
                    id = ReadyToRunHelper.VirtualFuncPtr;
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_READYTORUN_VIRTUAL_FUNC_PTR:
                    id = ReadyToRunHelper.VirtualFuncPtr;
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

        private CorInfoHelpFunc getNewHelper(ref CORINFO_RESOLVED_TOKEN pResolvedToken, CORINFO_METHOD_STRUCT_* callerHandle, byte* pHasSideEffects = null)
        {
            TypeDesc type = HandleToObject(pResolvedToken.hClass);

            if (pHasSideEffects != null)
            {
                *pHasSideEffects = (byte)(type.HasFinalizer ? 1 : 0);
            }

            return CorInfoHelpFunc.CORINFO_HELP_NEWFAST;
        }

        private CorInfoHelpFunc getNewArrHelper(CORINFO_CLASS_STRUCT_* arrayCls)
        {
            return CorInfoHelpFunc.CORINFO_HELP_NEWARR_1_DIRECT;
        }

        private static bool IsClassPreInited(TypeDesc type)
        {
            if (type.IsGenericDefinition)
            {
                return true;
            }
            if (type.HasStaticConstructor)
            {
                return false;
            }
            if (HasBoxedRegularStatics(type))
            {
                return false;
            }
            if (IsDynamicStatics(type))
            {
                return false;
            }
            return true;
        }

        private static bool HasBoxedRegularStatics(TypeDesc type)
        {
            foreach (FieldDesc field in type.GetFields())
            {
                if (field.IsStatic && 
                    !field.IsLiteral && 
                    field.FieldType.IsValueType &&
                    !field.FieldType.UnderlyingType.IsPrimitive)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsDynamicStatics(TypeDesc type)
        {
            if (type.HasInstantiation)
            {
                foreach (FieldDesc field in type.GetFields())
                {
                    if (field.IsStatic && !field.IsLiteral)
                    {
                        return true;
                    }
                }
            }
            return false;
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

            // This block enforces the rule that methods with [NativeCallable] attribute
            // can only be called from unmanaged code. The call from managed code is replaced
            // with a stub that throws an InvalidProgramException
            if (method.IsNativeCallable && (flags & CORINFO_CALLINFO_FLAGS.CORINFO_CALLINFO_LDFTN) == 0)
            {
                ThrowHelper.ThrowInvalidProgramException(ExceptionStringID.InvalidProgramNativeCallable, method);
            }

            TypeDesc exactType = HandleToObject(pResolvedToken.hClass);

            TypeDesc constrainedType = null;
            if ((flags & CORINFO_CALLINFO_FLAGS.CORINFO_CALLINFO_CALLVIRT) != 0 && pConstrainedResolvedToken != null)
            {
                constrainedType = HandleToObject(pConstrainedResolvedToken->hClass);
            }

            bool resolvedConstraint = false;
            bool forceUseRuntimeLookup = false;

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
                    if (method.Name == "GetHashCode")
                    {
                        directMethod = constrainedType.UnderlyingType.FindVirtualFunctionTargetMethodOnObjectType(method);
                        Debug.Assert(directMethod != null);

                        constrainedType = constrainedType.UnderlyingType;
                        method = directMethod;
                    }
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
            }

            //
            // Determine whether to perform direct call
            //

            bool directCall = false;
            bool resolvedCallVirt = false;
            bool useInstantiatingStub = false;

            if ((flags & CORINFO_CALLINFO_FLAGS.CORINFO_CALLINFO_LDFTN) != 0)
            {
                directCall = true;
            }
            else
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

            if (directCall)
            {
                bool allowInstParam = (flags & CORINFO_CALLINFO_FLAGS.CORINFO_CALLINFO_ALLOWINSTPARAM) != 0;
                MethodDesc canonMethod = targetMethod.GetCanonMethodTarget(CanonicalFormKind.Specific);

                if (pResult->exactContextNeedsRuntimeLookup && !allowInstParam && targetMethod.GetCanonMethodTarget(CanonicalFormKind.Specific).RequiresInstArg())
                {
                    CORINFO_RUNTIME_LOOKUP_KIND lookupKind = GetGenericRuntimeLookupKind(canonMethod);
                    ReadyToRunHelperId helperId = ReadyToRunHelperId.MethodHandle;
                    object helperArg = new MethodWithToken(canonMethod, new ModuleToken(_tokenContext, pResolvedToken.token));
                    GenericContext methodContext = new GenericContext(entityFromContext(pResolvedToken.tokenContext));
                    ISymbolNode helper = _compilation.SymbolNodeFactory.GenericLookupHelper(lookupKind, helperId, helperArg, methodContext, _signatureContext);
                    pResult->codePointerOrStubLookup.constLookup = CreateConstLookupToSymbol(helper);
                    pResult->codePointerOrStubLookup.lookupKind.needsRuntimeLookup = true;
                    pResult->codePointerOrStubLookup.lookupKind.runtimeLookupFlags = 0;
                    pResult->codePointerOrStubLookup.runtimeLookup.indirections = CORINFO.USEHELPER;
                    pResult->kind = CORINFO_CALL_KIND.CORINFO_CALL_CODE_POINTER;
                }
                else
                {
                    bool referencingArrayAddressMethod = false;

                    if (targetMethod.IsIntrinsic)
                    {
                        // For multidim array Address method, we pretend the method requires a hidden instantiation argument
                        // (even though it doesn't need one). We'll actually swap the method out for a differnt one with
                        // a matching calling convention later. See ArrayMethod for a description.
                        referencingArrayAddressMethod = targetMethod.IsArrayAddressMethod();
                    }

                    MethodDesc concreteMethod = targetMethod;
                    targetMethod = targetMethod.GetCanonMethodTarget(CanonicalFormKind.Specific);

                    pResult->kind = CORINFO_CALL_KIND.CORINFO_CALL;

                    // Compensate for always treating delegates as direct calls above
                    if ((flags & CORINFO_CALLINFO_FLAGS.CORINFO_CALLINFO_LDFTN) != 0 && (flags & CORINFO_CALLINFO_FLAGS.CORINFO_CALLINFO_CALLVIRT) != 0 && !resolvedCallVirt)
                    {
                        pResult->kind = CORINFO_CALL_KIND.CORINFO_VIRTUALCALL_LDVIRTFTN;
                    }

                    if (pResult->exactContextNeedsRuntimeLookup)
                    {
                        // Nothing to do... The generic handle lookup gets embedded in to the codegen
                        // during the jitting of the call.
                        // (Note: The generic lookup in R2R is performed by a call to a helper at runtime, not by
                        // codegen emitted at crossgen time)

                        Debug.Assert(!forceUseRuntimeLookup);
                        pResult->codePointerOrStubLookup.constLookup = CreateConstLookupToSymbol(
                            _compilation.NodeFactory.MethodEntrypoint(targetMethod, constrainedType, method,
                                new ModuleToken(_tokenContext, pResolvedToken.token), _signatureContext)
                            );
                    }
                    else
                    {
                        ISymbolNode instParam = null;

                        if (targetMethod.RequiresInstMethodDescArg())
                        {
                            instParam = _compilation.SymbolNodeFactory.MethodGenericDictionary(concreteMethod,
                                new ModuleToken(_tokenContext, pResolvedToken.token), _signatureContext);
                        }
                        else if (targetMethod.RequiresInstMethodTableArg() || referencingArrayAddressMethod)
                        {
                            // Ask for a constructed type symbol because we need the vtable to get to the dictionary
                            instParam = _compilation.SymbolNodeFactory.ConstructedTypeSymbol(concreteMethod.OwningType, _signatureContext);
                        }

                        if (instParam != null)
                        {
                            pResult->instParamLookup = CreateConstLookupToSymbol(instParam);
                        }

                        if (pResult->kind == CORINFO_CALL_KIND.CORINFO_VIRTUALCALL_LDVIRTFTN)
                        {
                            MethodDesc exactMethod = (MethodDesc)GetRuntimeDeterminedObjectForToken(ref pResolvedToken);
                            useInstantiatingStub = true;
                            pResult->codePointerOrStubLookup.constLookup = CreateConstLookupToSymbol(_compilation.NodeFactory.DynamicHelperCell(
                                new MethodWithToken(exactMethod, new ModuleToken(_tokenContext, pResolvedToken.token)), _signatureContext));
                        }
                        else
                        {
                            pResult->codePointerOrStubLookup.constLookup = CreateConstLookupToSymbol(
                            _compilation.NodeFactory.MethodEntrypoint(targetMethod, constrainedType, method,
                                new ModuleToken(_tokenContext, pResolvedToken.token), _signatureContext)
                            );
                        }
                    }
                }

                pResult->nullInstanceCheck = resolvedCallVirt;
            }
            else if (method.HasInstantiation)
            {
                // GVM Call Support
                MethodDesc exactMethod = (MethodDesc)GetRuntimeDeterminedObjectForToken(ref pResolvedToken);

                pResult->kind = CORINFO_CALL_KIND.CORINFO_VIRTUALCALL_LDVIRTFTN;
                pResult->nullInstanceCheck = true;
                pResult->codePointerOrStubLookup.constLookup = CreateConstLookupToSymbol(
                    _compilation.NodeFactory.DynamicHelperCell(
                        new MethodWithToken(exactMethod, new ModuleToken(_tokenContext, pResolvedToken.token)), _signatureContext));
                useInstantiatingStub = true;         
            }
            else
            // In ReadyToRun, we always use the dispatch stub to call virtual methods
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
                        _compilation.SymbolNodeFactory.InterfaceDispatchCell(targetMethod,
                            new ModuleToken(_tokenContext, (mdToken)pResolvedToken.token), _signatureContext, isUnboxingStub: false
                        , _compilation.NameMangler.GetMangledMethodName(MethodBeingCompiled).ToString()));
                }
            }

            pResult->hMethod = ObjectToHandle(targetMethod);

            pResult->accessAllowed = CorInfoIsAccessAllowedResult.CORINFO_ACCESS_ALLOWED;

            // We're pretty much done at this point.  Let's grab the rest of the information that the jit is going to
            // need.
            pResult->classFlags = getClassAttribsInternal(targetMethod.OwningType);

            pResult->methodFlags = getMethodAttribsInternal(targetMethod);
            Get_CORINFO_SIG_INFO(targetMethod, &pResult->sig, useInstantiatingStub);

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

            pResult->_secureDelegateInvoke = 0;
        }
    }
}
