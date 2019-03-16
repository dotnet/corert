// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

using Internal.Text;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using Internal.TypeSystem.Interop;

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

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            if (Context is MethodDesc contextAsMethod)
            {
                sb.Append(nameMangler.GetMangledMethodName(contextAsMethod));
            }
            else
            {
                sb.Append(nameMangler.GetMangledTypeName(ContextType));
            }
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
                object targetEntity = entity;
                if (entity is MethodDesc methodDesc)
                {
                    targetEntity = new MethodWithToken(methodDesc, new ModuleToken(_tokenContext, pResolvedToken.token));
                }
                lookup.lookupKind.needsRuntimeLookup = false;
                ISymbolNode constLookup = _compilation.SymbolNodeFactory.ComputeConstantLookup(helperId, targetEntity, _signatureContext);
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
                        object helperArg = GetRuntimeDeterminedObjectForToken(ref pResolvedToken);
                        if (helperArg is MethodDesc methodDesc)
                        {
                            helperArg = new MethodWithToken(methodDesc, new ModuleToken(_tokenContext, pResolvedToken.token));
                        }
                        TypeDesc constrainedType = null;
                        if (helperId == ReadyToRunHelperId.MethodEntry && pGenericLookupKind.runtimeLookupArgs != null)
                        {
                            constrainedType = (TypeDesc)GetRuntimeDeterminedObjectForToken(ref *(CORINFO_RESOLVED_TOKEN*)pGenericLookupKind.runtimeLookupArgs);
                        }

                        GenericContext methodContext = new GenericContext(entityFromContext(pResolvedToken.tokenContext));
                        ISymbolNode helper = _compilation.SymbolNodeFactory.GenericLookupHelper(
                            pGenericLookupKind.runtimeLookupKind,
                            helperId,
                            helperArg,
                            constrainedType,
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

                case CorInfoHelpFunc.CORINFO_HELP_INITCLASS:
                case CorInfoHelpFunc.CORINFO_HELP_INITINSTCLASS:
                    throw new RequiresRuntimeJitException(ftnNum.ToString());

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

        private void ceeInfoGetCallInfo(
            ref CORINFO_RESOLVED_TOKEN pResolvedToken,
            CORINFO_RESOLVED_TOKEN* pConstrainedResolvedToken,
            CORINFO_METHOD_STRUCT_* callerHandle,
            CORINFO_CALLINFO_FLAGS flags,
            CORINFO_CALL_INFO* pResult,
            out MethodDesc methodToCall,
            out MethodDesc targetMethod,
            out TypeDesc constrainedType,
            out MethodDesc originalMethod,
            out TypeDesc exactType,
            out MethodDesc callerMethod,
            out EcmaModule callerModule,
            out bool useInstantiatingStub)
        {
#if DEBUG
            // In debug, write some bogus data to the struct to ensure we have filled everything
            // properly.
            MemoryHelper.FillMemory((byte*)pResult, 0xcc, Marshal.SizeOf<CORINFO_CALL_INFO>());
#endif
            pResult->codePointerOrStubLookup.lookupKind.needsRuntimeLookup = false;
            useInstantiatingStub = false;

            originalMethod = HandleToObject(pResolvedToken.hMethod);
            TypeDesc type = HandleToObject(pResolvedToken.hClass);

            callerMethod = HandleToObject(callerHandle);
            if (!_compilation.NodeFactory.CompilationModuleGroup.ContainsMethodBody(callerMethod, unboxingStub: false))
            {
                // We must abort inline attempts calling from outside of the version bubble being compiled
                // because we have no way to remap the token relative to the external module to the current version bubble.
                throw new RequiresRuntimeJitException(callerMethod.ToString() + " -> " + originalMethod.ToString());
            }

            callerModule = ((EcmaMethod)callerMethod.GetTypicalMethodDefinition()).Module;

            // Spec says that a callvirt lookup ignores static methods. Since static methods
            // can't have the exact same signature as instance methods, a lookup that found
            // a static method would have never found an instance method.
            if (originalMethod.Signature.IsStatic && (flags & CORINFO_CALLINFO_FLAGS.CORINFO_CALLINFO_CALLVIRT) != 0)
            {
                throw new BadImageFormatException();
            }

            exactType = type;

            constrainedType = null;
            if ((flags & CORINFO_CALLINFO_FLAGS.CORINFO_CALLINFO_CALLVIRT) != 0 && pConstrainedResolvedToken != null)
            {
                constrainedType = HandleToObject(pConstrainedResolvedToken->hClass);
            }

            bool resolvedConstraint = false;
            bool forceUseRuntimeLookup = false;

            MethodDesc methodAfterConstraintResolution = originalMethod;
            if (constrainedType == null)
            {
                pResult->thisTransform = CORINFO_THIS_TRANSFORM.CORINFO_NO_THIS_TRANSFORM;
            }
            else if (constrainedType.IsRuntimeDeterminedSubtype || exactType.IsRuntimeDeterminedSubtype)
            {
                // <NICE> It shouldn't really matter what we do here - but the x86 JIT is annoyingly sensitive
                // about what we do, since it pretend generic variables are reference types and generates
                // an internal JIT tree even when just verifying generic code. </NICE>
                if (constrainedType.IsRuntimeDeterminedType)
                {
                    pResult->thisTransform = CORINFO_THIS_TRANSFORM.CORINFO_DEREF_THIS; // convert 'this' of type &T --> T
                }
                else if (constrainedType.IsValueType)
                {
                    pResult->thisTransform = CORINFO_THIS_TRANSFORM.CORINFO_BOX_THIS; // convert 'this' of type &VC<T> --> boxed(VC<T>)
                }
                else
                {
                    pResult->thisTransform = CORINFO_THIS_TRANSFORM.CORINFO_DEREF_THIS; // convert 'this' of type &C<T> --> C<T>
                }
            }
            else
            {
                // We have a "constrained." call.  Try a partial resolve of the constraint call.  Note that this
                // will not necessarily resolve the call exactly, since we might be compiling
                // shared generic code - it may just resolve it to a candidate suitable for
                // JIT compilation, and require a runtime lookup for the actual code pointer
                // to call.

                if (constrainedType.IsEnum && originalMethod.Name == "GetHashCode")
                {
                    MethodDesc methodOnUnderlyingType = constrainedType.UnderlyingType.FindVirtualFunctionTargetMethodOnObjectType(originalMethod);
                    Debug.Assert(methodOnUnderlyingType != null);

                    constrainedType = constrainedType.UnderlyingType;
                    originalMethod = methodOnUnderlyingType;
                }

                MethodDesc directMethod = constrainedType.TryResolveConstraintMethodApprox(exactType, originalMethod, out forceUseRuntimeLookup);
                if (directMethod != null)
                {
                    // Either
                    //    1. no constraint resolution at compile time (!directMethod)
                    // OR 2. no code sharing lookup in call
                    // OR 3. we have have resolved to an instantiating stub

                    methodAfterConstraintResolution = directMethod;
                    if (methodAfterConstraintResolution.GetCanonMethodTarget(CanonicalFormKind.Specific).RequiresInstArg())
                    {
                        useInstantiatingStub = true;
                    }

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

            //
            // Initialize callee context used for inlining and instantiation arguments
            //

            targetMethod = methodAfterConstraintResolution;

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
            bool callVirtCrossingVersionBubble = false;

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
            else if (targetMethod.OwningType.IsInterface && targetMethod.IsAbstract)
            {
                // Backwards compat: calls to abstract interface methods are treated as callvirt
                directCall = false;
            }
            else if ((flags & CORINFO_CALLINFO_FLAGS.CORINFO_CALLINFO_CALLVIRT) == 0 || resolvedConstraint)
            {
                directCall = true;
            }
            else
            {
                bool devirt;

                // if we are generating version resilient code
                // AND
                //    caller/callee are in different version bubbles
                // we have to apply more restrictive rules
                // These rules are related to the "inlining rules" as far as the
                // boundaries of a version bubble are concerned.
                if (!_compilation.NodeFactory.CompilationModuleGroup.ContainsMethodBody(callerMethod, unboxingStub: false) ||
                    !_compilation.NodeFactory.CompilationModuleGroup.ContainsMethodBody(targetMethod, unboxingStub: false))
                {
                    // For version resiliency we won't de-virtualize all final/sealed method calls.  Because during a 
                    // servicing event it is legal to unseal a method or type.
                    //
                    // Note that it is safe to devirtualize in the following cases, since a servicing event cannot later modify it
                    //  1) Callvirt on a virtual final method of a value type - since value types are sealed types as per ECMA spec
                    devirt = (constrainedType ?? targetMethod.OwningType).IsValueType || targetMethod.IsInternalCall;

                    callVirtCrossingVersionBubble = true;
                }
                else if (targetMethod.OwningType.IsInterface)
                {
                    // Handle interface methods specially because the Sealed bit has no meaning on interfaces.
                    devirt = !targetMethod.IsVirtual;
                }
                else
                {
                    devirt = !targetMethod.IsVirtual || targetMethod.IsFinal || targetMethod.OwningType.IsSealed();
                }

                if (devirt)
                {
                    resolvedCallVirt = true;
                    directCall = true;
                }
            }

            methodToCall = targetMethod;
            MethodDesc canonMethod = targetMethod.GetCanonMethodTarget(CanonicalFormKind.Specific);

            if (directCall)
            {
                bool allowInstParam = (flags & CORINFO_CALLINFO_FLAGS.CORINFO_CALLINFO_ALLOWINSTPARAM) != 0;

                if (!allowInstParam && canonMethod.RequiresInstArg())
                {
                    useInstantiatingStub = true;
                }

                // We don't allow a JIT to call the code directly if a runtime lookup is
                // needed. This is the case if
                //     1. the scan of the call token indicated that it involves code sharing
                // AND 2. the method is an instantiating stub
                //
                // In these cases the correct instantiating stub is only found via a runtime lookup.
                //
                // Note that most JITs don't call instantiating stubs directly if they can help it -
                // they call the underlying shared code and provide the type context parameter
                // explicitly. However
                //    (a) some JITs may call instantiating stubs (it makes the JIT simpler) and
                //    (b) if the method is a remote stub then the EE will force the
                //        call through an instantiating stub and
                //    (c) constraint calls that require runtime context lookup are never resolved 
                //        to underlying shared generic code

                if (((pResult->exactContextNeedsRuntimeLookup && useInstantiatingStub && (!allowInstParam || resolvedConstraint)) || forceUseRuntimeLookup)
                    && entityFromContext(pResolvedToken.tokenContext) is MethodDesc methodDesc && methodDesc.IsSharedByGenericInstantiations)
                {
                    // Handle invalid IL - see comment in code:CEEInfo::ComputeRuntimeLookupForSharedGenericToken
                    pResult->kind = CORINFO_CALL_KIND.CORINFO_CALL_CODE_POINTER;

                    // For reference types, the constrained type does not affect method resolution
                    DictionaryEntryKind entryKind = (constrainedType != null && constrainedType.IsValueType
                        ? DictionaryEntryKind.ConstrainedMethodEntrySlot
                        : DictionaryEntryKind.MethodEntrySlot);

                    ComputeRuntimeLookupForSharedGenericToken(entryKind, ref pResolvedToken, pConstrainedResolvedToken, targetMethod, ref pResult->codePointerOrStubLookup);
                }
                else
                {
                    if (allowInstParam)
                    {
                        methodToCall = canonMethod;
                    }

                    pResult->kind = CORINFO_CALL_KIND.CORINFO_CALL;

                    // Compensate for always treating delegates as direct calls above
                    if ((flags & CORINFO_CALLINFO_FLAGS.CORINFO_CALLINFO_LDFTN) != 0 &&
                        (flags & CORINFO_CALLINFO_FLAGS.CORINFO_CALLINFO_CALLVIRT) != 0 &&
                        !resolvedCallVirt)
                    {
                        pResult->kind = CORINFO_CALL_KIND.CORINFO_VIRTUALCALL_LDVIRTFTN;
                    }
                }
                pResult->nullInstanceCheck = resolvedCallVirt;
            }
            // All virtual calls which take method instantiations must
            // currently be implemented by an indirect call via a runtime-lookup
            // function pointer
            else if (targetMethod.HasInstantiation)
            {
                pResult->kind = CORINFO_CALL_KIND.CORINFO_VIRTUALCALL_LDVIRTFTN;  // stub dispatch can't handle generic method calls yet
                pResult->nullInstanceCheck = true;
            }
            // Non-interface dispatches go through the vtable.
            else if (!targetMethod.OwningType.IsInterface)
            {
                pResult->kind = CORINFO_CALL_KIND.CORINFO_VIRTUALCALL_STUB;
                pResult->nullInstanceCheck = true;

                // We'll special virtual calls to target methods in the corelib assembly when compiling in R2R mode, and generate fragile-NI-like callsites for improved performance. We
                // can do that because today we'll always service the corelib assembly and the runtime in one bundle. Any caller in the corelib version bubble can benefit from this
                // performance optimization.
                /* TODO-PERF, GitHub issue# 7168: uncommenting the conditional statement below enables
                ** VTABLE-based calls for Corelib (and maybe a larger framework version bubble in the
                ** future). Making it work requires construction of the method table in managed code
                ** matching the CoreCLR algorithm (MethodTableBuilder).
                if (MethodInSystemVersionBubble(callerMethod) && MethodInSystemVersionBubble(targetMethod))
                {
                    pResult->kind = CORINFO_CALL_KIND.CORINFO_VIRTUALCALL_VTABLE;
                }
                */
            }
            else
            {
                // Insert explicit null checks for cross-version bubble non-interface calls.
                // It is required to handle null checks properly for non-virtual <-> virtual change between versions
                pResult->nullInstanceCheck = callVirtCrossingVersionBubble && !targetMethod.OwningType.IsInterface;
                pResult->kind = CORINFO_CALL_KIND.CORINFO_VIRTUALCALL_LDVIRTFTN;

                // We can't make stub calls when we need exact information
                // for interface calls from shared code.

                if (// If the token is not shared then we don't need a runtime lookup
                    pResult->exactContextNeedsRuntimeLookup
                    // Handle invalid IL - see comment in code:CEEInfo::ComputeRuntimeLookupForSharedGenericToken
                    && entityFromContext(pResolvedToken.tokenContext) is MethodDesc methodDesc && methodDesc.IsSharedByGenericInstantiations)
                {
                    ComputeRuntimeLookupForSharedGenericToken(DictionaryEntryKind.DispatchStubAddrSlot, ref pResolvedToken, null, targetMethod, ref pResult->codePointerOrStubLookup);
                }
                else
                {
                    // We use an indirect call
                    pResult->codePointerOrStubLookup.constLookup.accessType = InfoAccessType.IAT_PVALUE;
                    pResult->codePointerOrStubLookup.constLookup.addr = null;
                }
            }

            pResult->hMethod = ObjectToHandle(methodToCall);

            // TODO: access checks
            pResult->accessAllowed = CorInfoIsAccessAllowedResult.CORINFO_ACCESS_ALLOWED;

            // We're pretty much done at this point.  Let's grab the rest of the information that the jit is going to
            // need.
            pResult->classFlags = getClassAttribsInternal(methodToCall.OwningType);

            pResult->methodFlags = getMethodAttribsInternal(methodToCall);
            Get_CORINFO_SIG_INFO(methodToCall, &pResult->sig, useInstantiatingStub);
        }

        private bool MethodInSystemVersionBubble(MethodDesc method)
        {
            return _compilation.NodeFactory.CompilationModuleGroup.ContainsMethodBody(method, unboxingStub: false) &&
                method.OwningType.GetTypeDefinition().GetClosestDefType() is MetadataType metadataType &&
                metadataType.Module == _compilation.TypeSystemContext.SystemModule;
        }

        private void getCallInfo(ref CORINFO_RESOLVED_TOKEN pResolvedToken, CORINFO_RESOLVED_TOKEN* pConstrainedResolvedToken, CORINFO_METHOD_STRUCT_* callerHandle, CORINFO_CALLINFO_FLAGS flags, CORINFO_CALL_INFO* pResult)
        {
            MethodDesc methodToCall;
            MethodDesc targetMethod;
            TypeDesc constrainedType;
            MethodDesc originalMethod;
            TypeDesc exactType;
            MethodDesc callerMethod;
            EcmaModule callerModule;
            bool useInstantiatingStub;
            ceeInfoGetCallInfo(
                ref pResolvedToken, 
                pConstrainedResolvedToken, 
                callerHandle, 
                flags, 
                pResult, 
                out methodToCall,
                out targetMethod, 
                out constrainedType, 
                out originalMethod, 
                out exactType,
                out callerMethod,
                out callerModule,
                out useInstantiatingStub);

            if (pResult->thisTransform == CORINFO_THIS_TRANSFORM.CORINFO_BOX_THIS)
            {
                // READYTORUN: FUTURE: Optionally create boxing stub at runtime
                throw new RequiresRuntimeJitException(pResult->thisTransform.ToString());
            }

            // OK, if the EE said we're not doing a stub dispatch then just return the kind to
            // the caller.  No other kinds of virtual calls have extra information attached.
            switch (pResult->kind)
            {
                case CORINFO_CALL_KIND.CORINFO_VIRTUALCALL_STUB:
                    {
                        if (pResult->codePointerOrStubLookup.lookupKind.needsRuntimeLookup)
                        {
                            return;
                        }

                        pResult->codePointerOrStubLookup.constLookup = CreateConstLookupToSymbol(
                            _compilation.SymbolNodeFactory.InterfaceDispatchCell(targetMethod,
                            new ModuleToken(callerModule, (mdToken)pResolvedToken.token),
                            _signatureContext,
                            isUnboxingStub: false,
                            _compilation.NameMangler.GetMangledMethodName(MethodBeingCompiled).ToString()));
                    }
                    break;


                case CORINFO_CALL_KIND.CORINFO_CALL_CODE_POINTER:
                    Debug.Assert(pResult->codePointerOrStubLookup.lookupKind.needsRuntimeLookup);

                    // There is no easy way to detect method referenced via generic lookups in generated code.
                    // Report this method reference unconditionally.
                    // TODO: m_pImage->m_pPreloader->MethodReferencedByCompiledCode(pResult->hMethod);
                    return;

                case CORINFO_CALL_KIND.CORINFO_CALL:
                    {
                        // Constrained token is not interesting with this transforms
                        if (pResult->thisTransform != CORINFO_THIS_TRANSFORM.CORINFO_NO_THIS_TRANSFORM)
                            pConstrainedResolvedToken = null;

                        // READYTORUN: FUTURE: Direct calls if possible
                        pResult->codePointerOrStubLookup.constLookup = CreateConstLookupToSymbol(
                            _compilation.NodeFactory.ImportedMethodNode(methodToCall, constrainedType, originalMethod,
                            new ModuleToken(callerModule, pResolvedToken.token),
                            isUnboxingStub: false,
                            isInstantiatingStub: useInstantiatingStub,
                            _signatureContext));
                    }
                    break;

                case CORINFO_CALL_KIND.CORINFO_VIRTUALCALL_VTABLE:
                    // Only calls within the CoreLib version bubble support fragile NI codegen with vtable based calls, for better performance (because 
                    // CoreLib and the runtime will always be updated together anyways - this is a special case)
                    break;

                case CORINFO_CALL_KIND.CORINFO_VIRTUALCALL_LDVIRTFTN:
                    if (!pResult->exactContextNeedsRuntimeLookup)
                    {
                        bool atypicalCallsite = (flags & CORINFO_CALLINFO_FLAGS.CORINFO_CALLINFO_ATYPICAL_CALLSITE) != 0;
                        pResult->codePointerOrStubLookup.constLookup = CreateConstLookupToSymbol(
                            _compilation.NodeFactory.DynamicHelperCell(
                                new MethodWithToken(targetMethod, new ModuleToken(callerModule, pResolvedToken.token)),
                                _signatureContext));

                        Debug.Assert(!pResult->sig.hasTypeArg());
                    }
                    break;

                default:
                    throw new NotImplementedException(pResult->kind.ToString());
            }

            if (pResult->sig.hasTypeArg())
            {
                if (pResult->exactContextNeedsRuntimeLookup)
                {
                    // Nothing to do... The generic handle lookup gets embedded in to the codegen
                    // during the jitting of the call.
                    // (Note: The generic lookup in R2R is performed by a call to a helper at runtime, not by
                    // codegen emitted at crossgen time)
                }
                else
                {
                    GenericContext methodContext = new GenericContext(entityFromContext(pResolvedToken.tokenContext));
                    MethodDesc canonMethod = targetMethod.GetCanonMethodTarget(CanonicalFormKind.Specific);
                    if (canonMethod.RequiresInstMethodDescArg())
                    {
                        pResult->instParamLookup = CreateConstLookupToSymbol(_compilation.SymbolNodeFactory.ReadyToRunHelper(
                            ReadyToRunHelperId.MethodDictionary,
                            new MethodWithToken(targetMethod, new ModuleToken(callerModule, pResolvedToken.token)),
                            signatureContext: _signatureContext));
                    }
                    else
                    {
                        pResult->instParamLookup = CreateConstLookupToSymbol(_compilation.SymbolNodeFactory.ReadyToRunHelper(
                            ReadyToRunHelperId.TypeDictionary,
                            exactType,
                            signatureContext: _signatureContext));
                    }
                }
            }
        }

        private void ComputeRuntimeLookupForSharedGenericToken(
            DictionaryEntryKind entryKind,
            ref CORINFO_RESOLVED_TOKEN pResolvedToken,
            CORINFO_RESOLVED_TOKEN* pConstrainedResolvedToken,
            MethodDesc templateMethod,
            ref CORINFO_LOOKUP pResultLookup)
        {
            pResultLookup.lookupKind.needsRuntimeLookup = true;
            pResultLookup.lookupKind.runtimeLookupFlags = 0;

            ref CORINFO_RUNTIME_LOOKUP pResult = ref pResultLookup.runtimeLookup;
            pResult.signature = null;

            pResult.indirectFirstOffset = false;
            pResult.indirectSecondOffset = false;

            // Unless we decide otherwise, just do the lookup via a helper function
            pResult.indirections = CORINFO.USEHELPER;

            MethodDesc contextMethod = methodFromContext(pResolvedToken.tokenContext);
            TypeDesc contextType = contextMethod.OwningType;

            // Do not bother computing the runtime lookup if we are inlining. The JIT is going
            // to abort the inlining attempt anyway.
            if (contextMethod != MethodBeingCompiled)
            {
                return;
            }

            // There is a pathological case where invalid IL refereces __Canon type directly, but there is no dictionary availabled to store the lookup. 
            // All callers of ComputeRuntimeLookupForSharedGenericToken have to filter out this case. We can't do much about it here.
            Debug.Assert(contextMethod.IsSharedByGenericInstantiations);

            if (contextMethod.RequiresInstMethodDescArg())
            {
                pResultLookup.lookupKind.runtimeLookupKind = CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_METHODPARAM;
            }
            else
            {
                if (contextMethod.RequiresInstMethodTableArg())
                    pResultLookup.lookupKind.runtimeLookupKind = CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_CLASSPARAM;
                else
                    pResultLookup.lookupKind.runtimeLookupKind = CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_THISOBJ;
            }

            pResultLookup.lookupKind.runtimeLookupArgs = null;

            switch (entryKind)
            {
                case DictionaryEntryKind.DeclaringTypeHandleSlot:
                    Debug.Assert(templateMethod != null);
                    pResultLookup.lookupKind.runtimeLookupArgs = ObjectToHandle(templateMethod.OwningType);
                    pResultLookup.lookupKind.runtimeLookupFlags = (ushort)ReadyToRunHelperId.DeclaringTypeHandle;
                    break;

                case DictionaryEntryKind.TypeHandleSlot:
                    pResultLookup.lookupKind.runtimeLookupFlags = (ushort)ReadyToRunHelperId.TypeHandle;
                    break;

                case DictionaryEntryKind.MethodDescSlot:
                case DictionaryEntryKind.MethodEntrySlot:
                case DictionaryEntryKind.ConstrainedMethodEntrySlot:
                case DictionaryEntryKind.DispatchStubAddrSlot:
                    {
                        if (entryKind == DictionaryEntryKind.MethodDescSlot)
                            pResultLookup.lookupKind.runtimeLookupFlags = (ushort)ReadyToRunHelperId.MethodHandle;
                        else if (entryKind == DictionaryEntryKind.MethodEntrySlot || entryKind == DictionaryEntryKind.ConstrainedMethodEntrySlot)
                            pResultLookup.lookupKind.runtimeLookupFlags = (ushort)ReadyToRunHelperId.MethodEntry;
                        else
                            pResultLookup.lookupKind.runtimeLookupFlags = (ushort)ReadyToRunHelperId.VirtualEntry;

                        pResultLookup.lookupKind.runtimeLookupArgs = pConstrainedResolvedToken;
                        break;
                    }

                case DictionaryEntryKind.FieldDescSlot:
                    pResultLookup.lookupKind.runtimeLookupFlags = (ushort)ReadyToRunHelperId.FieldHandle;
                    break;

                default:
                    throw new NotImplementedException(entryKind.ToString());
            }

            // For R2R compilations, we don't generate the dictionary lookup signatures (dictionary lookups are done in a 
            // different way that is more version resilient... plus we can't have pointers to existing MTs/MDs in the sigs)
        }

        private void ceeInfoEmbedGenericHandle(ref CORINFO_RESOLVED_TOKEN pResolvedToken, bool fEmbedParent, ref CORINFO_GENERICHANDLE_RESULT pResult)
        {
#if DEBUG
            // In debug, write some bogus data to the struct to ensure we have filled everything
            // properly.
            fixed (CORINFO_GENERICHANDLE_RESULT* tmp = &pResult)
                MemoryHelper.FillMemory((byte*)tmp, 0xcc, Marshal.SizeOf<CORINFO_GENERICHANDLE_RESULT>());
#endif

            bool runtimeLookup = false;
            MethodDesc templateMethod = null;

            if (!fEmbedParent && pResolvedToken.hMethod != null)
            {
                MethodDesc md = HandleToObject(pResolvedToken.hMethod);
                TypeDesc td = HandleToObject(pResolvedToken.hClass);

                pResult.handleType = CorInfoGenericHandleType.CORINFO_HANDLETYPE_METHOD;

                Debug.Assert(md.OwningType == td);

                pResult.compileTimeHandle = (CORINFO_GENERIC_STRUCT_*)ObjectToHandle(md);
                templateMethod = md;

                // Runtime lookup is only required for stubs. Regular entrypoints are always the same shared MethodDescs.
                runtimeLookup = md.IsSharedByGenericInstantiations;
            }
            else if (!fEmbedParent && pResolvedToken.hField != null)
            {
                FieldDesc fd = HandleToObject(pResolvedToken.hField);
                TypeDesc td = HandleToObject(pResolvedToken.hClass);

                pResult.handleType = CorInfoGenericHandleType.CORINFO_HANDLETYPE_FIELD;
                pResult.compileTimeHandle = (CORINFO_GENERIC_STRUCT_*)pResolvedToken.hField;

                runtimeLookup = fd.IsStatic && td.IsCanonicalSubtype(CanonicalFormKind.Specific);
            }
            else
            {
                TypeDesc td = HandleToObject(pResolvedToken.hClass);

                pResult.handleType = CorInfoGenericHandleType.CORINFO_HANDLETYPE_CLASS;
                pResult.compileTimeHandle = (CORINFO_GENERIC_STRUCT_*)pResolvedToken.hClass;

                if (fEmbedParent && pResolvedToken.hMethod != null)
                {
                    MethodDesc declaringMethod = HandleToObject(pResolvedToken.hMethod);
                    if (declaringMethod.OwningType.GetClosestDefType() != td.GetClosestDefType())
                    {
                        //
                        // The method type may point to a sub-class of the actual class that declares the method.
                        // It is important to embed the declaring type in this case.
                        //

                        templateMethod = declaringMethod;
                        pResult.compileTimeHandle = (CORINFO_GENERIC_STRUCT_*)ObjectToHandle(declaringMethod.OwningType);
                    }
                }

                // IsSharedByGenericInstantiations would not work here. The runtime lookup is required
                // even for standalone generic variables that show up as __Canon here.
                runtimeLookup = td.IsCanonicalSubtype(CanonicalFormKind.Specific);
            }

            Debug.Assert(pResult.compileTimeHandle != null);

            if (runtimeLookup
                /* TODO: this Crossgen check doesn't pass for ThisObjGenericLookupTest when inlining
                 * GenericLookup<object>.CheckInstanceTypeArg -> GetTypeName<object> because 
                 * tokenContext is GenericLookup<object> which is an exact class. Crossgen doesn't hit
                 * this code path as it properly propagates the object generic type argument to GetTypeName
                 * and inlines the method too.
                    // Handle invalid IL - see comment in code:CEEInfo::ComputeRuntimeLookupForSharedGenericToken
                    && ContextIsShared(pResolvedToken.tokenContext)
                */)
            {
                DictionaryEntryKind entryKind = DictionaryEntryKind.EmptySlot;
                switch (pResult.handleType)
                {
                    case CorInfoGenericHandleType.CORINFO_HANDLETYPE_CLASS:
                        entryKind = (templateMethod != null ? DictionaryEntryKind.DeclaringTypeHandleSlot : DictionaryEntryKind.TypeHandleSlot);
                        break;
                    case CorInfoGenericHandleType.CORINFO_HANDLETYPE_METHOD:
                        entryKind = DictionaryEntryKind.MethodDescSlot;
                        break;
                    case CorInfoGenericHandleType.CORINFO_HANDLETYPE_FIELD:
                        entryKind = DictionaryEntryKind.FieldDescSlot;
                        break;
                    default:
                        throw new NotImplementedException(pResult.handleType.ToString());
                }

                ComputeRuntimeLookupForSharedGenericToken(entryKind, ref pResolvedToken, pConstrainedResolvedToken: null, templateMethod, ref pResult.lookup);
            }
            else
            {
                // If the target is not shared then we've already got our result and
                // can simply do a static look up
                pResult.lookup.lookupKind.needsRuntimeLookup = false;

                pResult.lookup.constLookup.handle = pResult.compileTimeHandle;
                pResult.lookup.constLookup.accessType = InfoAccessType.IAT_VALUE;
            }
        }

        private bool ContextIsShared(CORINFO_CONTEXT_STRUCT *context)
        {
            MethodDesc contextMethod = methodFromContext(context);
            return contextMethod != null && contextMethod.IsSharedByGenericInstantiations;
        }

        private void embedGenericHandle(ref CORINFO_RESOLVED_TOKEN pResolvedToken, bool fEmbedParent, ref CORINFO_GENERICHANDLE_RESULT pResult)
        {
            ceeInfoEmbedGenericHandle(ref pResolvedToken, fEmbedParent, ref pResult);

            Debug.Assert(pResult.compileTimeHandle != null);

            if (pResult.lookup.lookupKind.needsRuntimeLookup)
            {
                if (pResult.handleType == CorInfoGenericHandleType.CORINFO_HANDLETYPE_METHOD)
                {
                    // There is no easy way to detect method referenced via generic lookups in generated code.
                    // Report this method reference unconditionally.
                    // TODO: m_pImage->m_pPreloader->MethodReferencedByCompiledCode((CORINFO_METHOD_HANDLE)pResult->compileTimeHandle);
                }
            }
            else
            {
                ISymbolNode symbolNode;

                switch (pResult.handleType)
                {
                    case CorInfoGenericHandleType.CORINFO_HANDLETYPE_CLASS:
                        symbolNode = _compilation.SymbolNodeFactory.ReadyToRunHelper(
                            ReadyToRunHelperId.TypeHandle,
                            HandleToObject(pResolvedToken.hClass),
                            _signatureContext);
                        break;

                    case CorInfoGenericHandleType.CORINFO_HANDLETYPE_METHOD:
                        symbolNode = _compilation.SymbolNodeFactory.ReadyToRunHelper(
                            ReadyToRunHelperId.MethodHandle,
                            new MethodWithToken(HandleToObject(pResolvedToken.hMethod), new ModuleToken(_tokenContext, pResolvedToken.token)),
                            _signatureContext);
                        break;

                    case CorInfoGenericHandleType.CORINFO_HANDLETYPE_FIELD:
                        symbolNode = _compilation.SymbolNodeFactory.ReadyToRunHelper(
                            ReadyToRunHelperId.FieldHandle,
                            HandleToObject(pResolvedToken.hField),
                            _signatureContext);
                        break;

                    default:
                        throw new NotImplementedException(pResult.handleType.ToString());
                }

                pResult.lookup.constLookup = CreateConstLookupToSymbol(symbolNode);
            }
        }

        private CORINFO_METHOD_STRUCT_* embedMethodHandle(CORINFO_METHOD_STRUCT_* handle, ref void* ppIndirection)
        {
            // TODO: READYTORUN FUTURE: Handle this case correctly
            MethodDesc methodDesc = HandleToObject(handle);
            throw new RequiresRuntimeJitException("embedMethodHandle: " + methodDesc.ToString());
        }

        private bool IsLayoutFixedInCurrentVersionBubble(TypeDesc type)
        {
            // Primitive types and enums have fixed layout
            if (type.IsPrimitive || type.IsEnum)
            {
                return true;
            }

            if (!_compilation.NodeFactory.CompilationModuleGroup.ContainsType(type))
            {
                if (!type.IsValueType)
                {
                    // Eventually, we may respect the non-versionable attribute for reference types too. For now, we are going
                    // to play it safe and ignore it.
                    return false;
                }

                // Valuetypes with non-versionable attribute are candidates for fixed layout. Reject the rest.
                return type is MetadataType metadataType && metadataType.HasCustomAttribute("System.Runtime.Versioning", "NonVersionableAttribute");
            }

            return true;
        }

        /// <summary>
        /// Is field layout of the inheritance chain fixed within the current version bubble?
        /// </summary>
        private bool IsInheritanceChainLayoutFixedInCurrentVersionBubble(TypeDesc type)
        {
            // This method is not expected to be called for value types
            Debug.Assert(!type.IsValueType);

            while (!type.IsObject && type != null)
            {
                if (!IsLayoutFixedInCurrentVersionBubble(type))
                {
                    return false;
                }
                type = type.BaseType;
            }

            return true;
        }

        private bool HasLayoutMetadata(TypeDesc type)
        {
            if (type.IsValueType && (MarshalUtils.IsBlittableType(type) || MarshalUtils.IsManagedSequentialType(type)))
            {
                // Sequential layout
                return true;
            }
            else
            {
                // <BUGNUM>workaround for B#104780 - VC fails to set SequentialLayout on some classes
                // with ClassSize. Too late to fix compiler for V1.
                //
                // To compensate, we treat AutoLayout classes as Sequential if they
                // meet all of the following criteria:
                //
                //    - ClassSize present and nonzero.
                //    - No instance fields declared
                //    - Base class is System.ValueType.
                //</BUGNUM>
                return type.BaseType.IsValueType && !type.GetFields().GetEnumerator().MoveNext();
            }
        }

        private void EncodeFieldBaseOffset(FieldDesc field, CORINFO_FIELD_INFO* pResult)
        {
            TypeDesc pMT = field.OwningType;

            if (pResult->fieldAccessor != CORINFO_FIELD_ACCESSOR.CORINFO_FIELD_INSTANCE)
            {
                // No-op except for instance fields
            }
            else if (!IsLayoutFixedInCurrentVersionBubble(pMT))
            {
                if (pMT.IsValueType)
                {
                    // ENCODE_CHECK_FIELD_OFFSET
                    // TODO: root field check import
                }
                else
                {
                    // ENCODE_FIELD_OFFSET
                    pResult->offset = 0;
                    pResult->fieldAccessor = CORINFO_FIELD_ACCESSOR.CORINFO_FIELD_INSTANCE_WITH_BASE;
                    pResult->fieldLookup = CreateConstLookupToSymbol(_compilation.SymbolNodeFactory.FieldOffset(field, _signatureContext));
                }
            }
            else if (pMT.IsValueType)
            {
                // ENCODE_NONE
            }
            else if (IsInheritanceChainLayoutFixedInCurrentVersionBubble(pMT.BaseType))
            {
                // ENCODE_NONE
            }
            else if (HasLayoutMetadata(pMT))
            {
                // We won't try to be smart for classes with layout.
                // They are complex to get right, and very rare anyway.
                // ENCODE_FIELD_OFFSET
                pResult->offset = 0;
                pResult->fieldAccessor = CORINFO_FIELD_ACCESSOR.CORINFO_FIELD_INSTANCE_WITH_BASE;
                pResult->fieldLookup = CreateConstLookupToSymbol(_compilation.SymbolNodeFactory.FieldOffset(field, _signatureContext));
            }
            else
            {
                // ENCODE_FIELD_BASE_OFFSET
                pResult->offset -= (uint)pMT.BaseType.InstanceByteCount.AsInt;
                pResult->fieldAccessor = CORINFO_FIELD_ACCESSOR.CORINFO_FIELD_INSTANCE_WITH_BASE;
                pResult->fieldLookup = CreateConstLookupToSymbol(_compilation.SymbolNodeFactory.FieldBaseOffset(field.OwningType, _signatureContext));
            }
        }
    }
}
