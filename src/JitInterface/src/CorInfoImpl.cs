// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;

using Internal.TypeSystem;

using Internal.IL;

using ILCompiler;
using ILCompiler.SymbolReader;
using ILCompiler.DependencyAnalysis;

namespace Internal.JitInterface
{
    internal unsafe partial class CorInfoImpl
    {
        private IntPtr _comp;

        [DllImport("ryujit")]
        private extern static IntPtr jitStartup(IntPtr host);

        [DllImport("ryujit")]
        private extern static IntPtr getJit();

        [DllImport("jitinterface")]
        private extern static IntPtr GetJitHost();

        [DllImport("jitinterface")]
        private extern static IntPtr GetJitInterfaceWrapper(IntPtr unwrapped);

        private IntPtr _jit;

        [DllImport("jitinterface")]
        private extern static CorJitResult JitWrapper(out IntPtr exception, IntPtr _this, IntPtr comp, ref CORINFO_METHOD_INFO info, uint flags,
            out IntPtr nativeEntry, out uint codeSize);

        [DllImport("jitinterface")]
        private extern static IntPtr AllocException([MarshalAs(UnmanagedType.LPWStr)]string message, int messageLength);

        private IntPtr AllocException(Exception ex)
        {
            string exString = ex.ToString();
            IntPtr nativeException = AllocException(exString, exString.Length);
            if (_nativeExceptions == null)
            {
                _nativeExceptions = new List<IntPtr>();
            }
            _nativeExceptions.Add(nativeException);
            return nativeException;
        }

        [DllImport("jitinterface")]
        private extern static void FreeException(IntPtr obj);

        [DllImport("jitinterface")]
        private extern static char* GetExceptionMessage(IntPtr obj);

        private Compilation _compilation;

        public CorInfoImpl(Compilation compilation)
        {
            _compilation = compilation;

            jitStartup(GetJitHost());

            _comp = GetJitInterfaceWrapper(CreateUnmanagedInstance());

            _jit = getJit();
        }

        public TextWriter Log
        {
            get
            {
                return _compilation.Log;
            }
        }

        private struct SequencePoint
        {
            public string Document;
            public int LineNumber;
        }

        private MethodCodeNode _methodCodeNode;

        private CORINFO_MODULE_STRUCT_* _methodScope; // Needed to resolve CORINFO_EH_CLAUSE tokens

        public void CompileMethod(MethodCodeNode methodCodeNodeNeedingCode)
        {
            try
            {
                _methodCodeNode = methodCodeNodeNeedingCode;

                CORINFO_METHOD_INFO methodInfo;
                Get_CORINFO_METHOD_INFO(MethodBeingCompiled, out methodInfo);

                _methodScope = methodInfo.scope;

                try
                {
                    CompilerTypeSystemContext typeSystemContext = _compilation.TypeSystemContext;

                    if (!_compilation.Options.NoLineNumbers)
                    {
                        IEnumerable<ILSequencePoint> ilSequencePoints = typeSystemContext.GetSequencePointsForMethod(MethodBeingCompiled);
                        if (ilSequencePoints != null)
                        {
                            SetSequencePoints(ilSequencePoints);
                        }
                    }

                    IEnumerable<ILLocalVariable> localVariables = typeSystemContext.GetLocalVariableNamesForMethod(MethodBeingCompiled);
                    if (localVariables != null)
                    {
                        SetLocalVariables(localVariables);
                    }

                    IEnumerable<string> parameters = typeSystemContext.GetParameterNamesForMethod(MethodBeingCompiled);
                    if (parameters != null)
                    {
                        SetParameterNames(parameters);
                    }
                }
                catch (Exception e)
                {
                    // Debug info not successfully loaded.
                    _compilation.Log.WriteLine(e.Message + " (" + methodCodeNodeNeedingCode.GetName() + ")");
                }

                IntPtr exception;
                IntPtr nativeEntry;
                uint codeSize;
                JitWrapper(out exception, _jit, _comp, ref methodInfo, (uint)CorJitFlag.CORJIT_FLG_CALL_GETJITFLAGS, out nativeEntry, out codeSize);
                if (exception != IntPtr.Zero)
                {
                    char* szMessage = GetExceptionMessage(exception);
                    string message = szMessage != null ? new string(szMessage) : "JIT Exception";
                    throw new Exception(message);
                }

                PublishCode();
            }
            finally
            {
                CompileMethodCleanup();
            }
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
            builder.Alignment = 1;

            // TODO: Filter out duplicate clauses

            builder.EmitCompressedUInt((uint)_ehClauses.Length);

            for (int i = 0; i < _ehClauses.Length; i++)
            {
                var clause = _ehClauses[i];
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
                // https://github.com/dotnet/coreclr/issues/3585
                int tryLength = (int)clause.TryLength - (int)clause.TryOffset;
                builder.EmitCompressedUInt((uint)((tryLength << 2) | (int)clauseKind));

                switch (clauseKind)
                {
                    case RhEHClauseKind.RH_EH_CLAUSE_TYPED:
                        {
                            builder.EmitCompressedUInt(clause.HandlerOffset);

                            var methodIL = (MethodIL)HandleToObject((IntPtr)_methodScope);
                            var type = (TypeDesc)methodIL.GetObject((int)clause.ClassTokenOrOffset);
                            var typeSymbol = _compilation.NodeFactory.NecessaryTypeSymbol(type);

                            builder.EmitReloc(typeSymbol, RelocType.IMAGE_REL_BASED_ABSOLUTE);
                        }
                        break;
                    case RhEHClauseKind.RH_EH_CLAUSE_FAULT:
                        builder.EmitCompressedUInt(clause.HandlerOffset);
                        break;
                    case RhEHClauseKind.RH_EH_CLAUSE_FILTER:
                        builder.EmitCompressedUInt(clause.ClassTokenOrOffset);
                        break;
                }
            }

            return builder.ToObjectData();
        }

        private void PublishCode()
        {
            var relocs = _relocs.ToArray();
            Array.Sort(relocs, (x, y) => (x.Offset - y.Offset));

            var objectData = new ObjectNode.ObjectData(_code,
                                                       relocs,
                                                       _compilation.NodeFactory.Target.MinimumFunctionAlignment,
                                                       new ISymbolNode[] { _methodCodeNode });

            _methodCodeNode.SetCode(objectData);

            _methodCodeNode.InitializeFrameInfos(_frameInfos);
            if (_ehClauses != null)
                _methodCodeNode.InitializeEHInfo(EncodeEHInfo());

            _methodCodeNode.InitializeDebugLocInfos(_debugLocInfos);
            _methodCodeNode.InitializeDebugVarInfos(_debugVarInfos);
        }

        private MethodDesc MethodBeingCompiled
        {
            get
            {
                return _methodCodeNode.Method;
            }
        }

        private int PointerSize
        {
            get
            {
                return _compilation.TypeSystemContext.Target.PointerSize;
            }
        }

        private Dictionary<Object, GCHandle> _pins = new Dictionary<object, GCHandle>();

        private IntPtr GetPin(Object obj)
        {
            GCHandle handle;
            if (!_pins.TryGetValue(obj, out handle))
            {
                handle = GCHandle.Alloc(obj, GCHandleType.Pinned);
                _pins.Add(obj, handle);
            }
            return handle.AddrOfPinnedObject();
        }

        private List<IntPtr> _nativeExceptions;

        private void CompileMethodCleanup()
        {
            foreach (var pin in _pins)
                pin.Value.Free();
            _pins.Clear();

            if (_nativeExceptions != null)
            {
                foreach (IntPtr ex in _nativeExceptions)
                    FreeException(ex);
                _nativeExceptions = null;
            }

            _methodCodeNode = null;

            _code = null;
            _coldCode = null;

            _roData = null;
            _roDataBlob = null;

            _relocs = new ArrayBuilder<Relocation>();

            _numFrameInfos = 0;
            _usedFrameInfos = 0;
            _frameInfos = null;

            _ehClauses = null;

            _sequencePoints = null;
            _debugLocInfos = null;
            _debugVarInfos = null;
        }

        private Dictionary<Object, IntPtr> _objectToHandle = new Dictionary<Object, IntPtr>();
        private List<Object> _handleToObject = new List<Object>();

        private const int handleMultipler = 8;
        private const int handleBase = 0x420000;

        private IntPtr ObjectToHandle(Object obj)
        {
            IntPtr handle;
            if (!_objectToHandle.TryGetValue(obj, out handle))
            {
                handle = (IntPtr)(8 * _handleToObject.Count + handleBase);
                _handleToObject.Add(obj);
                _objectToHandle.Add(obj, handle);
            }
            return handle;
        }

        private Object HandleToObject(IntPtr handle)
        {
            int index = ((int)handle - handleBase) / handleMultipler;
            return _handleToObject[index];
        }

        private MethodDesc HandleToObject(CORINFO_METHOD_STRUCT_* method) { return (MethodDesc)HandleToObject((IntPtr)method); }
        private CORINFO_METHOD_STRUCT_* ObjectToHandle(MethodDesc method) { return (CORINFO_METHOD_STRUCT_*)ObjectToHandle((Object)method); }

        private TypeDesc HandleToObject(CORINFO_CLASS_STRUCT_* type) { return (TypeDesc)HandleToObject((IntPtr)type); }
        private CORINFO_CLASS_STRUCT_* ObjectToHandle(TypeDesc type) { return (CORINFO_CLASS_STRUCT_*)ObjectToHandle((Object)type); }

        private FieldDesc HandleToObject(CORINFO_FIELD_STRUCT_* field) { return (FieldDesc)HandleToObject((IntPtr)field); }
        private CORINFO_FIELD_STRUCT_* ObjectToHandle(FieldDesc field) { return (CORINFO_FIELD_STRUCT_*)ObjectToHandle((Object)field); }

        private bool Get_CORINFO_METHOD_INFO(MethodDesc method, out CORINFO_METHOD_INFO methodInfo)
        {
            var methodIL = _compilation.GetMethodIL(method);
            if (methodIL == null)
            {
                methodInfo = default(CORINFO_METHOD_INFO);
                return false;
            }

            methodInfo.ftn = ObjectToHandle(method);
            methodInfo.scope = (CORINFO_MODULE_STRUCT_*)ObjectToHandle(methodIL);
            var ilCode = methodIL.GetILBytes();
            methodInfo.ILCode = (byte*)GetPin(ilCode);
            methodInfo.ILCodeSize = (uint)ilCode.Length;
            methodInfo.maxStack = (uint)methodIL.GetMaxStack();
            methodInfo.EHcount = (uint)methodIL.GetExceptionRegions().Length;
            methodInfo.options = methodIL.GetInitLocals() ? CorInfoOptions.CORINFO_OPT_INIT_LOCALS : (CorInfoOptions)0;
            methodInfo.regionKind = CorInfoRegionKind.CORINFO_REGION_NONE;

            Get_CORINFO_SIG_INFO(method.Signature, out methodInfo.args);
            Get_CORINFO_SIG_INFO(methodIL.GetLocals(), out methodInfo.locals);

            return true;
        }

        private void Get_CORINFO_SIG_INFO(MethodSignature signature, out CORINFO_SIG_INFO sig)
        {
            sig.callConv = (CorInfoCallConv)0;
            if (!signature.IsStatic) sig.callConv |= CorInfoCallConv.CORINFO_CALLCONV_HASTHIS;

            TypeDesc returnType = signature.ReturnType;

            CorInfoType corInfoRetType = asCorInfoType(signature.ReturnType, out sig.retTypeClass);
            sig._retType = (byte)corInfoRetType;
            sig.retTypeSigClass = sig.retTypeClass; // The difference between the two is not relevant for ILCompiler

            sig.flags = 0;    // used by IL stubs code

            sig.numArgs = (ushort)signature.Length;

            sig.args = (CORINFO_ARG_LIST_STRUCT_*)0; // CORINFO_ARG_LIST_STRUCT_ is argument index

            // TODO: Shared generic
            sig.sigInst.classInst = null;
            sig.sigInst.classInstCount = 0;
            sig.sigInst.methInst = null;
            sig.sigInst.methInstCount = 0;

            sig.pSig = (byte*)ObjectToHandle(signature);
            sig.cbSig = 0; // Not used by the JIT
            sig.scope = null; // Not used by the JIT
            sig.token = 0; // Not used by the JIT

            // TODO: Shared generic
            // if (ftn->RequiresInstArg())
            // {
            //     sig.callConv = (CorInfoCallConv)(sig.callConv | CORINFO_CALLCONV_PARAMTYPE);
            // }
        }

        private void Get_CORINFO_SIG_INFO(LocalVariableDefinition[] locals, out CORINFO_SIG_INFO sig)
        {
            sig.callConv = CorInfoCallConv.CORINFO_CALLCONV_DEFAULT;
            sig._retType = (byte)CorInfoType.CORINFO_TYPE_VOID;
            sig.retTypeClass = null;
            sig.retTypeSigClass = null;
            sig.flags = (byte)CorInfoSigInfoFlags.CORINFO_SIGFLAG_IS_LOCAL_SIG;

            sig.numArgs = (ushort)locals.Length;

            sig.sigInst.classInst = null;
            sig.sigInst.classInstCount = 0;
            sig.sigInst.methInst = null;
            sig.sigInst.methInstCount = 0;

            sig.args = (CORINFO_ARG_LIST_STRUCT_*)0; // CORINFO_ARG_LIST_STRUCT_ is argument index

            sig.pSig = (byte*)ObjectToHandle(locals);
            sig.cbSig = 0; // Not used by the JIT
            sig.scope = null; // Not used by the JIT
            sig.token = 0; // Not used by the JIT
        }

        private CorInfoType asCorInfoType(TypeDesc type)
        {
            if (type.IsEnum)
            {
                type = type.UnderlyingType;
            }

            if (type.IsPrimitive)
            {
                Debug.Assert((CorInfoType)TypeFlags.Void == CorInfoType.CORINFO_TYPE_VOID);
                Debug.Assert((CorInfoType)TypeFlags.Double == CorInfoType.CORINFO_TYPE_DOUBLE);

                return (CorInfoType)type.Category;
            }

            if (type.IsPointer)
            {
                return CorInfoType.CORINFO_TYPE_PTR;
            }

            if (type.IsByRef)
            {
                return CorInfoType.CORINFO_TYPE_BYREF;
            }

            if (type.IsValueType)
            {
                return CorInfoType.CORINFO_TYPE_VALUECLASS;
            }

            return CorInfoType.CORINFO_TYPE_CLASS;
        }

        private CorInfoType asCorInfoType(TypeDesc type, out CORINFO_CLASS_STRUCT_* structType)
        {
            var corInfoType = asCorInfoType(type);
            structType = ((corInfoType == CorInfoType.CORINFO_TYPE_CLASS) ||
                (corInfoType == CorInfoType.CORINFO_TYPE_VALUECLASS) ||
                (corInfoType == CorInfoType.CORINFO_TYPE_BYREF)) ? ObjectToHandle(type) : null;
            return corInfoType;
        }

        private CORINFO_CONTEXT_STRUCT* contextFromMethod(MethodDesc method)
        {
            return (CORINFO_CONTEXT_STRUCT*)(((ulong)ObjectToHandle(method)) | (ulong)CorInfoContextFlags.CORINFO_CONTEXTFLAGS_METHOD);
        }

        private CORINFO_CONTEXT_STRUCT* contextFromType(TypeDesc type)
        {
            return (CORINFO_CONTEXT_STRUCT*)(((ulong)ObjectToHandle(type)) | (ulong)CorInfoContextFlags.CORINFO_CONTEXTFLAGS_CLASS);
        }

        private MethodDesc methodFromContext(CORINFO_CONTEXT_STRUCT* contextStruct)
        {
            if (((ulong)contextStruct & (ulong)CorInfoContextFlags.CORINFO_CONTEXTFLAGS_MASK) == (ulong)CorInfoContextFlags.CORINFO_CONTEXTFLAGS_CLASS)
            {
                return null;
            }
            else
            {
                return HandleToObject((CORINFO_METHOD_STRUCT_*)((ulong)contextStruct & ~(ulong)CorInfoContextFlags.CORINFO_CONTEXTFLAGS_MASK));
            }
        }

        private TypeDesc typeFromContext(CORINFO_CONTEXT_STRUCT* contextStruct)
        {
            if (((ulong)contextStruct & (ulong)CorInfoContextFlags.CORINFO_CONTEXTFLAGS_MASK) == (ulong)CorInfoContextFlags.CORINFO_CONTEXTFLAGS_CLASS)
            {
                return HandleToObject((CORINFO_CLASS_STRUCT_*)((ulong)contextStruct & ~(ulong)CorInfoContextFlags.CORINFO_CONTEXTFLAGS_MASK));
            }
            else
            {
                return methodFromContext(contextStruct).OwningType;
            }
        }

        private uint getMethodAttribsInternal(MethodDesc method)
        {
            CorInfoFlag result = 0;

            // CORINFO_FLG_PROTECTED - verification only

            if (method.Signature.IsStatic)
                result |= CorInfoFlag.CORINFO_FLG_STATIC;

            // TODO: if (pMD->IsSynchronized())
            //    result |= CORINFO_FLG_SYNCH;

            if (method.IsIntrinsic)
                result |= CorInfoFlag.CORINFO_FLG_INTRINSIC;
            if (method.IsVirtual)
                result |= CorInfoFlag.CORINFO_FLG_VIRTUAL;
            if (method.IsAbstract)
                result |= CorInfoFlag.CORINFO_FLG_ABSTRACT;
            if (method.IsConstructor || method.IsStaticConstructor)
                result |= CorInfoFlag.CORINFO_FLG_CONSTRUCTOR;

            //
            // See if we need to embed a .cctor call at the head of the
            // method body.
            //

            var owningType = method.OwningType;
            var owningMetadataType = owningType as MetadataType;

            // method or class might have the final bit
            if (method.IsFinal || (owningMetadataType != null && owningMetadataType.IsSealed))
                result |= CorInfoFlag.CORINFO_FLG_FINAL;

            // TODO: Generics
            // if (pMD->IsSharedByGenericInstantiations())
            //     result |= CORINFO_FLG_SHAREDINST;

            // TODO: PInvoke
            // if ((attribs & MethodAttributes.PinvokeImpl) != 0)
            //    result |= CorInfoFlag.CORINFO_FLG_PINVOKE;

            // TODO: Cache inlining hits
            // Check for an inlining directive.

            if (method.IsNoInlining)
            {
                /* Function marked as not inlineable */
                result |= CorInfoFlag.CORINFO_FLG_DONT_INLINE;
            }
            else if (method.IsAggressiveInlining)
            {
                result |= CorInfoFlag.CORINFO_FLG_FORCEINLINE;
            }

            if (owningType.IsDelegate)
            {
                if (method.Name == "Invoke")
                    // This is now used to emit efficient invoke code for any delegate invoke,
                    // including multicast.
                    result |= CorInfoFlag.CORINFO_FLG_DELEGATE_INVOKE;
            }

            result |= CorInfoFlag.CORINFO_FLG_NOSECURITYWRAP;

            return (uint)result;
        }

        private uint getMethodAttribs(CORINFO_METHOD_STRUCT_* ftn)
        {
            return getMethodAttribsInternal(HandleToObject(ftn));
        }

        private void setMethodAttribs(CORINFO_METHOD_STRUCT_* ftn, CorInfoMethodRuntimeFlags attribs)
        {
            // TODO: Inlining
        }

        private void getMethodSig(CORINFO_METHOD_STRUCT_* ftn, CORINFO_SIG_INFO* sig, CORINFO_CLASS_STRUCT_* memberParent)
        {
            MethodDesc method = HandleToObject(ftn);

            Get_CORINFO_SIG_INFO(method.Signature, out *sig);
        }

        [return: MarshalAs(UnmanagedType.I1)]
        private bool getMethodInfo(CORINFO_METHOD_STRUCT_* ftn, ref CORINFO_METHOD_INFO info)
        {
            return Get_CORINFO_METHOD_INFO(HandleToObject(ftn), out info);
        }

        private CorInfoInline canInline(CORINFO_METHOD_STRUCT_* callerHnd, CORINFO_METHOD_STRUCT_* calleeHnd, ref uint pRestrictions)
        {
            // No restrictions on inlining
            return CorInfoInline.INLINE_PASS;
        }

        private void reportInliningDecision(CORINFO_METHOD_STRUCT_* inlinerHnd, CORINFO_METHOD_STRUCT_* inlineeHnd, CorInfoInline inlineResult, byte* reason)
        {
        }

        [return: MarshalAs(UnmanagedType.I1)]
        private bool canTailCall(CORINFO_METHOD_STRUCT_* callerHnd, CORINFO_METHOD_STRUCT_* declaredCalleeHnd, CORINFO_METHOD_STRUCT_* exactCalleeHnd, [MarshalAs(UnmanagedType.I1)]bool fIsTailPrefix)
        {
            // No restrictions on tailcalls
            return true;
        }

        private void reportTailCallDecision(CORINFO_METHOD_STRUCT_* callerHnd, CORINFO_METHOD_STRUCT_* calleeHnd, [MarshalAs(UnmanagedType.I1)]bool fIsTailPrefix, CorInfoTailCall tailCallResult, byte* reason)
        {
        }

        private void getEHinfo(CORINFO_METHOD_STRUCT_* ftn, uint EHnumber, ref CORINFO_EH_CLAUSE clause)
        {
            var methodIL = _compilation.GetMethodIL(HandleToObject(ftn));

            var ehRegion = methodIL.GetExceptionRegions()[EHnumber];

            clause.Flags = (CORINFO_EH_CLAUSE_FLAGS)ehRegion.Kind;
            clause.TryOffset = (uint)ehRegion.TryOffset;
            clause.TryLength = (uint)ehRegion.TryLength;
            clause.HandlerOffset = (uint)ehRegion.HandlerOffset;
            clause.HandlerLength = (uint)ehRegion.HandlerLength;
            clause.ClassTokenOrOffset = (uint)((ehRegion.Kind == ILExceptionRegionKind.Filter) ? ehRegion.FilterOffset : ehRegion.ClassToken);
        }

        private CORINFO_CLASS_STRUCT_* getMethodClass(CORINFO_METHOD_STRUCT_* method)
        {
            var m = HandleToObject(method);
            return ObjectToHandle(m.OwningType);
        }

        private CORINFO_MODULE_STRUCT_* getMethodModule(CORINFO_METHOD_STRUCT_* method)
        { throw new NotImplementedException("getMethodModule"); }
        private void getMethodVTableOffset(CORINFO_METHOD_STRUCT_* method, ref uint offsetOfIndirection, ref uint offsetAfterIndirection)
        { throw new NotImplementedException("getMethodVTableOffset"); }

        [return: MarshalAs(UnmanagedType.I1)]
        private bool isInSIMDModule(CORINFO_CLASS_STRUCT_* classHnd)
        {
            // TODO: SIMD
            return false;
        }

        private CorInfoUnmanagedCallConv getUnmanagedCallConv(CORINFO_METHOD_STRUCT_* method)
        { throw new NotImplementedException("getUnmanagedCallConv"); }
        [return: MarshalAs(UnmanagedType.Bool)]
        private bool pInvokeMarshalingRequired(CORINFO_METHOD_STRUCT_* method, CORINFO_SIG_INFO* callSiteSig)
        { throw new NotImplementedException("pInvokeMarshalingRequired"); }
        [return: MarshalAs(UnmanagedType.Bool)]
        private bool satisfiesMethodConstraints(CORINFO_CLASS_STRUCT_* parent, CORINFO_METHOD_STRUCT_* method)
        { throw new NotImplementedException("satisfiesMethodConstraints"); }
        [return: MarshalAs(UnmanagedType.Bool)]
        private bool isCompatibleDelegate(CORINFO_CLASS_STRUCT_* objCls, CORINFO_CLASS_STRUCT_* methodParentCls, CORINFO_METHOD_STRUCT_* method, CORINFO_CLASS_STRUCT_* delegateCls, [MarshalAs(UnmanagedType.Bool)] ref bool pfIsOpenDelegate)
        { throw new NotImplementedException("isCompatibleDelegate"); }
        [return: MarshalAs(UnmanagedType.Bool)]
        private bool isDelegateCreationAllowed(CORINFO_CLASS_STRUCT_* delegateHnd, CORINFO_METHOD_STRUCT_* calleeHnd)
        {
            return true;
        }
        private CorInfoInstantiationVerification isInstantiationOfVerifiedGeneric(CORINFO_METHOD_STRUCT_* method)
        { throw new NotImplementedException("isInstantiationOfVerifiedGeneric"); }
        private void initConstraintsForVerification(CORINFO_METHOD_STRUCT_* method, [MarshalAs(UnmanagedType.Bool)] ref bool pfHasCircularClassConstraints, [MarshalAs(UnmanagedType.Bool)] ref bool pfHasCircularMethodConstraint)
        { throw new NotImplementedException("isInstantiationOfVerifiedGeneric"); }
        private CorInfoCanSkipVerificationResult canSkipMethodVerification(CORINFO_METHOD_STRUCT_* ftnHandle)
        { throw new NotImplementedException("canSkipMethodVerification"); }

        private void methodMustBeLoadedBeforeCodeIsRun(CORINFO_METHOD_STRUCT_* method)
        {
        }

        private CORINFO_METHOD_STRUCT_* mapMethodDeclToMethodImpl(CORINFO_METHOD_STRUCT_* method)
        { throw new NotImplementedException("mapMethodDeclToMethodImpl"); }

        private void getGSCookie(GSCookie* pCookieVal, GSCookie** ppCookieVal)
        {
            // TODO: fully implement GS cookies

            if (pCookieVal != null)
            {
                *pCookieVal = (GSCookie)0x216D6F6D202C6948;
                *ppCookieVal = null;
            }
            else
            {
                throw new NotImplementedException("getGSCookie");
            }
        }

        private void resolveToken(ref CORINFO_RESOLVED_TOKEN pResolvedToken)
        {
            var methodIL = (MethodIL)HandleToObject((IntPtr)pResolvedToken.tokenScope);

            var result = methodIL.GetObject((int)pResolvedToken.token);

            pResolvedToken.hClass = null;
            pResolvedToken.hMethod = null;
            pResolvedToken.hField = null;

            if (result is MethodDesc)
            {
                MethodDesc method = result as MethodDesc;
                pResolvedToken.hMethod = ObjectToHandle(method);
                pResolvedToken.hClass = ObjectToHandle(method.OwningType);
            }
            else
            if (result is FieldDesc)
            {
                FieldDesc field = result as FieldDesc;
                pResolvedToken.hField = ObjectToHandle(field);
                pResolvedToken.hClass = ObjectToHandle(field.OwningType);
            }
            else
            {
                TypeDesc type = (TypeDesc)result;
                if (pResolvedToken.tokenType == CorInfoTokenKind.CORINFO_TOKENKIND_Newarr)
                    type = type.MakeArrayType();
                pResolvedToken.hClass = ObjectToHandle(type);
            }

            pResolvedToken.pTypeSpec = null;
            pResolvedToken.cbTypeSpec = 0;
            pResolvedToken.pMethodSpec = null;
            pResolvedToken.cbMethodSpec = 0;
        }

        private void findSig(CORINFO_MODULE_STRUCT_* module, uint sigTOK, CORINFO_CONTEXT_STRUCT* context, CORINFO_SIG_INFO* sig)
        {
            var methodIL = (MethodIL)HandleToObject((IntPtr)module);
            Get_CORINFO_SIG_INFO((MethodSignature)methodIL.GetObject((int)sigTOK), out *sig);
        }

        private void findCallSiteSig(CORINFO_MODULE_STRUCT_* module, uint methTOK, CORINFO_CONTEXT_STRUCT* context, CORINFO_SIG_INFO* sig)
        {
            // TODO: dynamic scopes
            // TODO: verification
            var methodIL = (MethodIL)HandleToObject((IntPtr)module);
            Get_CORINFO_SIG_INFO(((MethodDesc)methodIL.GetObject((int)methTOK)).Signature, out *sig);
        }

        private CORINFO_CLASS_STRUCT_* getTokenTypeAsHandle(ref CORINFO_RESOLVED_TOKEN pResolvedToken)
        {
            WellKnownType result = WellKnownType.RuntimeTypeHandle;

            if (pResolvedToken.hMethod != null)
            {
                result = WellKnownType.RuntimeMethodHandle;
            }
            else
            if (pResolvedToken.hField != null)
            {
                result = WellKnownType.RuntimeFieldHandle;
            }

            return ObjectToHandle(_compilation.TypeSystemContext.GetWellKnownType(result));
        }

        private CorInfoCanSkipVerificationResult canSkipVerification(CORINFO_MODULE_STRUCT_* module)
        { throw new NotImplementedException("canSkipVerification"); }
        [return: MarshalAs(UnmanagedType.Bool)]
        private bool isValidToken(CORINFO_MODULE_STRUCT_* module, uint metaTOK)
        { throw new NotImplementedException("isValidToken"); }
        [return: MarshalAs(UnmanagedType.Bool)]
        private bool isValidStringRef(CORINFO_MODULE_STRUCT_* module, uint metaTOK)
        { throw new NotImplementedException("isValidStringRef"); }
        [return: MarshalAs(UnmanagedType.Bool)]
        private bool shouldEnforceCallvirtRestriction(CORINFO_MODULE_STRUCT_* scope)
        { throw new NotImplementedException("shouldEnforceCallvirtRestriction"); }

        private CorInfoType asCorInfoType(CORINFO_CLASS_STRUCT_* cls)
        {
            var type = HandleToObject(cls);
            return asCorInfoType(type);
        }

        private byte* getClassName(CORINFO_CLASS_STRUCT_* cls)
        {
            var type = HandleToObject(cls);
            return (byte*)GetPin(StringToUTF8(type.ToString()));
        }

        private int appendClassName(short** ppBuf, ref int pnBufLen, CORINFO_CLASS_STRUCT_* cls, [MarshalAs(UnmanagedType.Bool)]bool fNamespace, [MarshalAs(UnmanagedType.Bool)]bool fFullInst, [MarshalAs(UnmanagedType.Bool)]bool fAssembly)
        { throw new NotImplementedException("appendClassName"); }

        [return: MarshalAs(UnmanagedType.Bool)]
        private bool isValueClass(CORINFO_CLASS_STRUCT_* cls)
        {
            return HandleToObject(cls).IsValueType;
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        private bool canInlineTypeCheckWithObjectVTable(CORINFO_CLASS_STRUCT_* cls)
        {
            return true;
        }

        private uint getClassAttribs(CORINFO_CLASS_STRUCT_* cls)
        {
            TypeDesc type = HandleToObject(cls);
            return getClassAttribsInternal(type);
        }

        private uint getClassAttribsInternal(TypeDesc type)
        {
            // TODO: This method needs to implement:
            //       1. GenericParameterType: CORINFO_FLG_GENERIC_TYPE_VARIABLE
            //       2. Shared instantiation: IsCanonicalSubtype, IsRuntimeDeterminedSubtype: CORINFO_FLG_SHAREDINST
            //       3. HasVariance: CORINFO_FLG_VARIANCE
            //       4. Finalizer support: CORINFO_FLG_HAS_FINALIZER

            CorInfoFlag result = (CorInfoFlag)0;

            // The array flag is used to identify the faked-up methods on
            // array types, i.e. .ctor, Get, Set and Address
            if (type.IsArray)
                result |= CorInfoFlag.CORINFO_FLG_ARRAY;

            if (type.IsInterface)
                result |= CorInfoFlag.CORINFO_FLG_INTERFACE;

            if (type.IsArray || type.IsString)
                result |= CorInfoFlag.CORINFO_FLG_VAROBJSIZE;

            if (type.IsValueType)
            {
                result |= CorInfoFlag.CORINFO_FLG_VALUECLASS;

                // TODO
                // if (type.IsUnsafeValueType)
                //    result |= CorInfoFlag.CORINFO_FLG_UNSAFE_VALUECLASS;
            }

            if (type.IsDelegate)
                result |= CorInfoFlag.CORINFO_FLG_DELEGATE;

            var metadataType = type as MetadataType;
            if (metadataType != null)
            {
                if (metadataType.ContainsPointers)
                    result |= CorInfoFlag.CORINFO_FLG_CONTAINS_GC_PTR;

                if (metadataType.IsBeforeFieldInit)
                    result |= CorInfoFlag.CORINFO_FLG_BEFOREFIELDINIT;

                if (metadataType.IsSealed)
                    result |= CorInfoFlag.CORINFO_FLG_FINAL;
            }

            return (uint)result;
        }

        [return: MarshalAs(UnmanagedType.Bool)]

        private bool isStructRequiringStackAllocRetBuf(CORINFO_CLASS_STRUCT_* cls)
        {
            // Disable this optimization. It has limited value (only kicks in on x86, and only for less common structs),
            // causes bugs and introduces odd ABI differences not compatible with ReadyToRun.
            return false;
        }

        private CORINFO_MODULE_STRUCT_* getClassModule(CORINFO_CLASS_STRUCT_* cls)
        { throw new NotImplementedException("getClassModule"); }
        private CORINFO_ASSEMBLY_STRUCT_* getModuleAssembly(CORINFO_MODULE_STRUCT_* mod)
        { throw new NotImplementedException("getModuleAssembly"); }
        private byte* getAssemblyName(CORINFO_ASSEMBLY_STRUCT_* assem)
        { throw new NotImplementedException("getAssemblyName"); }

        private void* LongLifetimeMalloc(UIntPtr sz)
        {
            return (void*)Marshal.AllocCoTaskMem((int)sz);
        }

        private void LongLifetimeFree(void* obj)
        {
            Marshal.FreeCoTaskMem((IntPtr)obj);
        }

        private byte* getClassModuleIdForStatics(CORINFO_CLASS_STRUCT_* cls, CORINFO_MODULE_STRUCT_** pModule, void** ppIndirection)
        { throw new NotImplementedException("getClassModuleIdForStatics"); }

        private uint getClassSize(CORINFO_CLASS_STRUCT_* cls)
        {
            TypeDesc type = HandleToObject(cls);
            return (uint)type.GetElementSize();
        }

        private uint getClassAlignmentRequirement(CORINFO_CLASS_STRUCT_* cls, [MarshalAs(UnmanagedType.Bool)]bool fDoubleAlignHint)
        { throw new NotImplementedException("getClassAlignmentRequirement"); }

        private int GatherClassGCLayout(TypeDesc type, byte* gcPtrs)
        {
            int result = 0;

            foreach (var field in type.GetFields())
            {
                if (field.IsStatic)
                    continue;

                CorInfoGCType gcType = CorInfoGCType.TYPE_GC_NONE;

                var fieldType = field.FieldType;
                if (fieldType.IsValueType)
                {
                    if (!((MetadataType)fieldType).ContainsPointers)
                        continue;

                    gcType = CorInfoGCType.TYPE_GC_OTHER;
                }
                else if ((fieldType is DefType) || (fieldType is ArrayType))
                {
                    gcType = CorInfoGCType.TYPE_GC_REF;
                }
                else if (fieldType.IsByRef)
                {
                    gcType = CorInfoGCType.TYPE_GC_BYREF;
                }
                else
                {
                    continue;
                }

                Debug.Assert(field.Offset % PointerSize == 0);
                byte* fieldGcPtrs = gcPtrs + field.Offset / PointerSize;

                if (gcType == CorInfoGCType.TYPE_GC_OTHER)
                {
                    result += GatherClassGCLayout(fieldType, fieldGcPtrs);
                }
                else
                {
                    // Ensure that if we have multiple fields with the same offset, 
                    // that we don't double count the data in the gc layout.
                    if (*fieldGcPtrs == (byte)CorInfoGCType.TYPE_GC_NONE)
                    {
                        *fieldGcPtrs = (byte)gcType;
                        result++;
                    }
                    else
                    {
                        Debug.Assert(*fieldGcPtrs == (byte)gcType);
                    }
                }
            }

            return result;
        }

        private uint getClassGClayout(CORINFO_CLASS_STRUCT_* cls, byte* gcPtrs)
        {
            uint result = 0;

            MetadataType type = (MetadataType)HandleToObject(cls);

            Debug.Assert(type.IsValueType);

            int pointerSize = PointerSize;

            int ptrsCount = AlignmentHelper.AlignUp(type.InstanceByteCount, pointerSize) / pointerSize;

            // Assume no GC pointers at first
            for (int i = 0; i < ptrsCount; i++)
                gcPtrs[i] = (byte)CorInfoGCType.TYPE_GC_NONE;

            if (type.ContainsPointers)
            {
                result = (uint)GatherClassGCLayout(type, gcPtrs);
            }
            return result;
        }

        private uint getClassNumInstanceFields(CORINFO_CLASS_STRUCT_* cls)
        {
            TypeDesc type = HandleToObject(cls);

            uint result = 0;
            foreach (var field in type.GetFields())
            {
                if (!field.IsStatic)
                    result++;
            }

            return result;
        }

        private CORINFO_FIELD_STRUCT_* getFieldInClass(CORINFO_CLASS_STRUCT_* clsHnd, int num)
        {
            TypeDesc classWithFields = HandleToObject(clsHnd);

            int iCurrentFoundField = -1;
            foreach (var field in classWithFields.GetFields())
            {
                if (field.IsStatic)
                    continue;

                ++iCurrentFoundField;
                if (iCurrentFoundField == num)
                {
                    return ObjectToHandle(field);
                }
            }

            // We could not find the field that was searched for.
            throw new InvalidOperationException();
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        private bool checkMethodModifier(CORINFO_METHOD_STRUCT_* hMethod, byte* modifier, [MarshalAs(UnmanagedType.Bool)]bool fOptional)
        { throw new NotImplementedException("checkMethodModifier"); }
        private CorInfoHelpFunc getNewHelper(ref CORINFO_RESOLVED_TOKEN pResolvedToken, CORINFO_METHOD_STRUCT_* callerHandle)
        { throw new NotImplementedException("getNewHelper"); }
        private CorInfoHelpFunc getNewArrHelper(CORINFO_CLASS_STRUCT_* arrayCls)
        { throw new NotImplementedException("getNewArrHelper"); }
        private CorInfoHelpFunc getCastingHelper(ref CORINFO_RESOLVED_TOKEN pResolvedToken, [MarshalAs(UnmanagedType.I1)]bool fThrowing)
        { throw new NotImplementedException("getCastingHelper"); }
        private CorInfoHelpFunc getSharedCCtorHelper(CORINFO_CLASS_STRUCT_* clsHnd)
        { throw new NotImplementedException("getSharedCCtorHelper"); }
        private CorInfoHelpFunc getSecurityPrologHelper(CORINFO_METHOD_STRUCT_* ftn)
        { throw new NotImplementedException("getSecurityPrologHelper"); }

        private CORINFO_CLASS_STRUCT_* getTypeForBox(CORINFO_CLASS_STRUCT_* cls)
        {
            var type = HandleToObject(cls);

            var typeForBox = type.IsNullable ? type.Instantiation[0] : type;

            return ObjectToHandle(typeForBox);
        }

        private CorInfoHelpFunc getBoxHelper(CORINFO_CLASS_STRUCT_* cls)
        {
            var type = HandleToObject(cls);

            return type.IsNullable ? CorInfoHelpFunc.CORINFO_HELP_BOX_NULLABLE : CorInfoHelpFunc.CORINFO_HELP_BOX;
        }

        private CorInfoHelpFunc getUnBoxHelper(CORINFO_CLASS_STRUCT_* cls)
        {
            var type = HandleToObject(cls);

            return type.IsNullable ? CorInfoHelpFunc.CORINFO_HELP_UNBOX_NULLABLE : CorInfoHelpFunc.CORINFO_HELP_UNBOX;
        }

        private void getReadyToRunHelper(ref CORINFO_RESOLVED_TOKEN pResolvedToken, CorInfoHelpFunc id, ref CORINFO_CONST_LOOKUP pLookup)
        {
            pLookup.accessType = InfoAccessType.IAT_VALUE;

            switch (id)
            {
                case CorInfoHelpFunc.CORINFO_HELP_READYTORUN_NEW:
                    {
                        var type = HandleToObject(pResolvedToken.hClass);
                        Debug.Assert(type is DefType);

                        pLookup.addr = (void*)ObjectToHandle(_compilation.NodeFactory.ReadyToRunHelper(ReadyToRunHelperId.NewHelper, type));
                    }
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_READYTORUN_NEWARR_1:
                    {
                        var type = HandleToObject(pResolvedToken.hClass);
                        Debug.Assert(type.IsSzArray);

                        pLookup.addr = (void*)ObjectToHandle(_compilation.NodeFactory.ReadyToRunHelper(ReadyToRunHelperId.NewArr1, type));
                    }
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_READYTORUN_ISINSTANCEOF:
                    {
                        var type = HandleToObject(pResolvedToken.hClass);

                        pLookup.addr = (void*)ObjectToHandle(_compilation.NodeFactory.ReadyToRunHelper(ReadyToRunHelperId.IsInstanceOf, type));
                    }
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_READYTORUN_CHKCAST:
                    {
                        var type = HandleToObject(pResolvedToken.hClass);

                        pLookup.addr = (void*)ObjectToHandle(_compilation.NodeFactory.ReadyToRunHelper(ReadyToRunHelperId.CastClass, type));
                    }
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_READYTORUN_STATIC_BASE:
                    {
                        var type = HandleToObject(pResolvedToken.hClass);

                        pLookup.addr = (void*)ObjectToHandle(_compilation.NodeFactory.ReadyToRunHelper(ReadyToRunHelperId.GetNonGCStaticBase, type));
                    }
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_READYTORUN_DELEGATE_CTOR:
                    {
                        var method = HandleToObject(pResolvedToken.hMethod);

                        DelegateInfo delegateInfo = _compilation.GetDelegateCtor(method);

                        pLookup.addr = (void*)ObjectToHandle(_compilation.NodeFactory.ReadyToRunHelper(ReadyToRunHelperId.DelegateCtor, delegateInfo));
                    }
                    break;
                default:
                    throw new NotImplementedException("ReadyToRun: " + id.ToString());
            }
        }

        private byte* getHelperName(CorInfoHelpFunc helpFunc)
        {
            return (byte*)GetPin(StringToUTF8(helpFunc.ToString()));
        }

        private CorInfoInitClassResult initClass(CORINFO_FIELD_STRUCT_* field, CORINFO_METHOD_STRUCT_* method, CORINFO_CONTEXT_STRUCT* context, [MarshalAs(UnmanagedType.Bool)]bool speculative)
        {
            FieldDesc fd = field == null ? null : HandleToObject(field);
            Debug.Assert(fd == null || fd.IsStatic);

            MethodDesc md = HandleToObject(method);
            TypeDesc type = fd != null ? fd.OwningType : typeFromContext(context);

            if (!_compilation.HasLazyStaticConstructor(type))
            {
                return CorInfoInitClassResult.CORINFO_INITCLASS_NOT_REQUIRED;
            }

            MetadataType typeToInit = (MetadataType)type;

            if (typeToInit.IsModuleType)
            {
                // For both jitted and ngen code the global class is always considered initialized
                return CorInfoInitClassResult.CORINFO_INITCLASS_NOT_REQUIRED;
            }

            if (fd == null)
            {
                if (typeToInit.IsBeforeFieldInit)
                {
                    // We can wait for field accesses to run .cctor
                    return CorInfoInitClassResult.CORINFO_INITCLASS_NOT_REQUIRED;
                }

                // Run .cctor on statics & constructors
                if (md.Signature.IsStatic)
                {
                    // Except don't class construct on .cctor - it would be circular
                    if (md.IsStaticConstructor)
                    {
                        return CorInfoInitClassResult.CORINFO_INITCLASS_NOT_REQUIRED;
                    }
                }
                else if (!md.IsConstructor && !typeToInit.IsValueType)
                {
                    // According to the spec, we should be able to do this optimization for both reference and valuetypes.
                    // To maintain backward compatibility, we are doing it for reference types only.
                    // For instance methods of types with precise-initialization
                    // semantics, we can assume that the .ctor triggerred the
                    // type initialization.
                    // This does not hold for NULL "this" object. However, the spec does
                    // not require that case to work.
                    return CorInfoInitClassResult.CORINFO_INITCLASS_NOT_REQUIRED;
                }
            }

            // TODO: before giving up and asking to generate a helper call, check to see if this is some pattern we can
            //       prove doesn't need initclass anymore because we initialized it earlier.

            return CorInfoInitClassResult.CORINFO_INITCLASS_USE_HELPER;
        }

        private void classMustBeLoadedBeforeCodeIsRun(CORINFO_CLASS_STRUCT_* cls)
        {
        }

        private CORINFO_CLASS_STRUCT_* getBuiltinClass(CorInfoClassId classId)
        {
            switch (classId)
            {
                case CorInfoClassId.CLASSID_SYSTEM_OBJECT:
                    return ObjectToHandle(_compilation.TypeSystemContext.GetWellKnownType(WellKnownType.Object));

                case CorInfoClassId.CLASSID_TYPED_BYREF:
                    // TODO: better exception type: invalid input IL
                    throw new NotSupportedException("TypedReference not supported in .NET Core");

                case CorInfoClassId.CLASSID_TYPE_HANDLE:
                    return ObjectToHandle(_compilation.TypeSystemContext.GetWellKnownType(WellKnownType.RuntimeTypeHandle));

                case CorInfoClassId.CLASSID_FIELD_HANDLE:
                    return ObjectToHandle(_compilation.TypeSystemContext.GetWellKnownType(WellKnownType.RuntimeFieldHandle));

                case CorInfoClassId.CLASSID_METHOD_HANDLE:
                    return ObjectToHandle(_compilation.TypeSystemContext.GetWellKnownType(WellKnownType.RuntimeMethodHandle));

                case CorInfoClassId.CLASSID_ARGUMENT_HANDLE:
                    // TODO: better exception type: invalid input IL
                    throw new NotSupportedException("Vararg methods not supported in .NET Core");

                case CorInfoClassId.CLASSID_STRING:
                    return ObjectToHandle(_compilation.TypeSystemContext.GetWellKnownType(WellKnownType.String));

                case CorInfoClassId.CLASSID_RUNTIME_TYPE:
                    // This is used in a JIT optimization. It's not applicable due to the structure of CoreRT CoreLib.
                    return null;

                default:
                    throw new NotImplementedException();
            }
        }

        private CorInfoType getTypeForPrimitiveValueClass(CORINFO_CLASS_STRUCT_* cls)
        {
            var type = HandleToObject(cls);

            if (!type.IsPrimitive && !type.IsEnum)
                return CorInfoType.CORINFO_TYPE_UNDEF;

            return asCorInfoType(type);
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        private bool canCast(CORINFO_CLASS_STRUCT_* child, CORINFO_CLASS_STRUCT_* parent)
        { throw new NotImplementedException("canCast"); }
        [return: MarshalAs(UnmanagedType.Bool)]
        private bool areTypesEquivalent(CORINFO_CLASS_STRUCT_* cls1, CORINFO_CLASS_STRUCT_* cls2)
        { throw new NotImplementedException("areTypesEquivalent"); }
        private CORINFO_CLASS_STRUCT_* mergeClasses(CORINFO_CLASS_STRUCT_* cls1, CORINFO_CLASS_STRUCT_* cls2)
        { throw new NotImplementedException("mergeClasses"); }
        private CORINFO_CLASS_STRUCT_* getParentType(CORINFO_CLASS_STRUCT_* cls)
        { throw new NotImplementedException("getParentType"); }

        private CorInfoType getChildType(CORINFO_CLASS_STRUCT_* clsHnd, ref CORINFO_CLASS_STRUCT_* clsRet)
        {
            CorInfoType result = CorInfoType.CORINFO_TYPE_UNDEF;

            var td = HandleToObject(clsHnd);
            if (td.IsArray || td.IsByRef)
            {
                TypeDesc returnType = ((ParameterizedType)td).ParameterType;
                result = asCorInfoType(returnType, out clsRet);
            }
            else
                clsRet = null;

            return result;
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        private bool satisfiesClassConstraints(CORINFO_CLASS_STRUCT_* cls)
        { throw new NotImplementedException("satisfiesClassConstraints"); }

        [return: MarshalAs(UnmanagedType.Bool)]
        private bool isSDArray(CORINFO_CLASS_STRUCT_* cls)
        {
            var td = HandleToObject(cls);
            return td.IsSzArray;
        }

        private uint getArrayRank(CORINFO_CLASS_STRUCT_* cls)
        { throw new NotImplementedException("getArrayRank"); }

        private void* getArrayInitializationData(CORINFO_FIELD_STRUCT_* field, uint size)
        {
            var fd = HandleToObject(field);

            // Check for invalid arguments passed to InitializeArray intrinsic
            if (!fd.HasRva ||
                size > fd.FieldType.GetElementSize())
            {
                return null;
            }

            return (void*)ObjectToHandle(_compilation.GetFieldRvaData(fd));
        }

        private CorInfoIsAccessAllowedResult canAccessClass(ref CORINFO_RESOLVED_TOKEN pResolvedToken, CORINFO_METHOD_STRUCT_* callerHandle, ref CORINFO_HELPER_DESC pAccessHelper)
        {
            // TODO: Access check
            return CorInfoIsAccessAllowedResult.CORINFO_ACCESS_ALLOWED;
        }

        private byte* getFieldName(CORINFO_FIELD_STRUCT_* ftn, byte** moduleName)
        {
            var field = HandleToObject(ftn);
            if (moduleName != null)
            {
                MetadataType typeDef = field.OwningType.GetTypeDefinition() as MetadataType;
                if (typeDef != null)
                    *moduleName = (byte*)GetPin(StringToUTF8(typeDef.GetFullName()));
                else
                    *moduleName = (byte*)GetPin(StringToUTF8("unknown"));
            }

            return (byte*)GetPin(StringToUTF8(field.Name));
        }

        private CORINFO_CLASS_STRUCT_* getFieldClass(CORINFO_FIELD_STRUCT_* field)
        {
            var fieldDesc = HandleToObject(field);
            return ObjectToHandle(fieldDesc.OwningType);
        }

        private CorInfoType getFieldType(CORINFO_FIELD_STRUCT_* field, ref CORINFO_CLASS_STRUCT_* structType, CORINFO_CLASS_STRUCT_* memberParent)
        {
            var fieldDesc = HandleToObject(field);
            return asCorInfoType(fieldDesc.FieldType, out structType);
        }

        private uint getFieldOffset(CORINFO_FIELD_STRUCT_* field)
        {
            var fieldDesc = HandleToObject(field);

            Debug.Assert(fieldDesc.Offset != FieldAndOffset.InvalidOffset);

            return (uint)fieldDesc.Offset;
        }

        [return: MarshalAs(UnmanagedType.I1)]
        private bool isWriteBarrierHelperRequired(CORINFO_FIELD_STRUCT_* field)
        { throw new NotImplementedException("isWriteBarrierHelperRequired"); }

        private void getFieldInfo(ref CORINFO_RESOLVED_TOKEN pResolvedToken, CORINFO_METHOD_STRUCT_* callerHandle, CORINFO_ACCESS_FLAGS flags, ref CORINFO_FIELD_INFO pResult)
        {
#if DEBUG
            // In debug, write some bogus data to the struct to ensure we have filled everything
            // properly.
            fixed (CORINFO_FIELD_INFO* tmp = &pResult)
                MemoryHelper.FillMemory((byte*)tmp, 0xcc, Marshal.SizeOf<CORINFO_FIELD_INFO>());
#endif

            Debug.Assert(((int)flags & ((int)CORINFO_ACCESS_FLAGS.CORINFO_ACCESS_GET |
                                        (int)CORINFO_ACCESS_FLAGS.CORINFO_ACCESS_SET |
                                        (int)CORINFO_ACCESS_FLAGS.CORINFO_ACCESS_ADDRESS |
                                        (int)CORINFO_ACCESS_FLAGS.CORINFO_ACCESS_INIT_ARRAY)) != 0);

            var field = HandleToObject(pResolvedToken.hField);

            CORINFO_FIELD_ACCESSOR fieldAccessor;
            CORINFO_FIELD_FLAGS fieldFlags = (CORINFO_FIELD_FLAGS)0;

            if (field.IsStatic)
            {
                fieldFlags |= CORINFO_FIELD_FLAGS.CORINFO_FLG_FIELD_STATIC;

                if (field.HasRva)
                {
                    throw new NotSupportedException("getFieldInfo for RVA mapped field");
                }

                fieldAccessor = CORINFO_FIELD_ACCESSOR.CORINFO_FIELD_STATIC_SHARED_STATIC_HELPER;
                pResult.helper = CorInfoHelpFunc.CORINFO_HELP_READYTORUN_STATIC_BASE;

                ReadyToRunHelperId helperId;
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

                pResult.fieldLookup.addr = (void*)ObjectToHandle(_compilation.NodeFactory.ReadyToRunHelper(helperId, field.OwningType));
                pResult.fieldLookup.accessType = InfoAccessType.IAT_VALUE;
            }
            else
            {
                fieldAccessor = CORINFO_FIELD_ACCESSOR.CORINFO_FIELD_INSTANCE;
            }

            if (field.IsInitOnly)
                fieldFlags |= CORINFO_FIELD_FLAGS.CORINFO_FLG_FIELD_FINAL;

            pResult.fieldAccessor = fieldAccessor;
            pResult.fieldFlags = fieldFlags;
            pResult.fieldType = getFieldType(pResolvedToken.hField, ref pResult.structType, pResolvedToken.hClass);
            pResult.accessAllowed = CorInfoIsAccessAllowedResult.CORINFO_ACCESS_ALLOWED;
            pResult.offset = (uint)field.Offset;

            // TODO: We need to implement access checks for fields and methods.  See JitInterface.cpp in mrtjit
            //       and STS::AccessCheck::CanAccess.
        }

        [return: MarshalAs(UnmanagedType.I1)]
        private bool isFieldStatic(CORINFO_FIELD_STRUCT_* fldHnd)
        {
            return HandleToObject(fldHnd).IsStatic;
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

        private void getBoundaries(CORINFO_METHOD_STRUCT_* ftn, ref uint cILOffsets, ref uint* pILOffsets, BoundaryTypes* implicitBoundaries)
        {
            // TODO: Debugging
            cILOffsets = 0;
            pILOffsets = null;
            *implicitBoundaries = BoundaryTypes.DEFAULT_BOUNDARIES;
        }

        // Create a DebugLocInfo which is a table from native offset to sourece line.
        // using native to il offset (pMap) and il to source line (_sequencePoints).
        private void setBoundaries(CORINFO_METHOD_STRUCT_* ftn, uint cMap, OffsetMapping* pMap)
        {
            Debug.Assert(_debugLocInfos == null);
            // No interest if sequencePoints is not populated before.
            if (_sequencePoints == null)
            {
                return;
            }

            List<DebugLocInfo> debugLocInfos = new List<DebugLocInfo>();
            for (int i = 0; i < cMap; i++)
            {
                SequencePoint s;
                if (_sequencePoints.TryGetValue((int)pMap[i].ilOffset, out s))
                {
                    Debug.Assert(!string.IsNullOrEmpty(s.Document));
                    int nativeOffset = (int)pMap[i].nativeOffset;
                    DebugLocInfo loc = new DebugLocInfo(nativeOffset, s.Document, s.LineNumber);

                    // https://github.com/dotnet/corert/issues/270
                    // We often miss line number at 0 offset, which prevents debugger from
                    // stepping into callee.
                    // Synthesize a location info at 0 offset assuming line number is minus one
                    // from the first entry.
                    if (debugLocInfos.Count == 0 && nativeOffset != 0)
                    {
                        DebugLocInfo firstLoc = new DebugLocInfo(0, loc.FileName, loc.LineNumber - 1, loc.ColNumber);
                        debugLocInfos.Add(firstLoc);
                    }

                    debugLocInfos.Add(loc);
                }
            }

            if (debugLocInfos.Count > 0)
            {
                _debugLocInfos = debugLocInfos.ToArray();
            }
        }

        private void getVars(CORINFO_METHOD_STRUCT_* ftn, ref uint cVars, ILVarInfo** vars, [MarshalAs(UnmanagedType.U1)] ref bool extendOthers)
        {
            // TODO: Debugging

            cVars = 0;
            *vars = null;

            // Just tell the JIT to extend everything.
            extendOthers = true;
        }

        private void setVars(CORINFO_METHOD_STRUCT_* ftn, uint cVars, NativeVarInfo* vars)
        {
            if (_localSlotToInfoMap == null && _parameterIndexToNameMap == null)
            {
                return;
            }

            uint paramCount = (_parameterIndexToNameMap == null) ? 0 : (uint)_parameterIndexToNameMap.Count;
            Dictionary<uint, DebugVarInfo> debugVars = new Dictionary<uint, DebugVarInfo>();
            int i;

            for (i = 0; i < cVars; i++)
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

            i = 0;
            _debugVarInfos = new DebugVarInfo[debugVars.Count];
            foreach (var debugVar in debugVars)
            {
                _debugVarInfos[i] = debugVar.Value;
                i++;
            }
        }

        private void updateDebugVarInfo(Dictionary<uint, DebugVarInfo> debugVars, string name,
                                        bool isParam, NativeVarInfo nativeVarInfo)
        {
            DebugVarInfo debugVar;

            if (!debugVars.TryGetValue(nativeVarInfo.varNumber, out debugVar))
            {
                // TODO: Force an INT32 type (0x0074 in CodeView) for now. Fix it later.
                // ISSUE #784. 
                debugVar = new DebugVarInfo(name, isParam, typeIndex : 0x0074);
                debugVars[nativeVarInfo.varNumber] = debugVar;
            }

            debugVar.Ranges.Add(nativeVarInfo);
        }

        private void* allocateArray(uint cBytes)
        {
            return (void*)Marshal.AllocCoTaskMem((int)cBytes);
        }

        private void freeArray(void* array)
        {
            Marshal.FreeCoTaskMem((IntPtr)array);
        }

        private CORINFO_ARG_LIST_STRUCT_* getArgNext(CORINFO_ARG_LIST_STRUCT_* args)
        {
            return (CORINFO_ARG_LIST_STRUCT_*)((int)args + 1);
        }

        private CorInfoTypeWithMod getArgType(CORINFO_SIG_INFO* sig, CORINFO_ARG_LIST_STRUCT_* args, ref CORINFO_CLASS_STRUCT_* vcTypeRet)
        {
            int index = (int)args;
            Object sigObj = HandleToObject((IntPtr)sig->pSig);

            MethodSignature methodSig = sigObj as MethodSignature;

            if (methodSig != null)
            {
                TypeDesc type = methodSig[index];

                CorInfoType corInfoType = asCorInfoType(type, out vcTypeRet);
                return (CorInfoTypeWithMod)corInfoType;
            }
            else
            {
                LocalVariableDefinition[] locals = (LocalVariableDefinition[])sigObj;
                TypeDesc type = locals[index].Type;

                CorInfoType corInfoType = asCorInfoType(type, out vcTypeRet);

                return (CorInfoTypeWithMod)corInfoType | (locals[index].IsPinned ? CorInfoTypeWithMod.CORINFO_TYPE_MOD_PINNED : 0);
            }
        }

        private CORINFO_CLASS_STRUCT_* getArgClass(CORINFO_SIG_INFO* sig, CORINFO_ARG_LIST_STRUCT_* args)
        {
            int index = (int)args;
            Object sigObj = HandleToObject((IntPtr)sig->pSig);

            MethodSignature methodSig = sigObj as MethodSignature;
            if (methodSig != null)
            {
                TypeDesc type = methodSig[index];
                return ObjectToHandle(type);
            }
            else
            {
                LocalVariableDefinition[] locals = (LocalVariableDefinition[])sigObj;
                TypeDesc type = locals[index].Type;
                return ObjectToHandle(type);
            }
        }

        private CorInfoType getHFAType(CORINFO_CLASS_STRUCT_* hClass)
        { throw new NotImplementedException("getHFAType"); }
        private HRESULT GetErrorHRESULT(_EXCEPTION_POINTERS* pExceptionPointers)
        { throw new NotImplementedException("GetErrorHRESULT"); }
        private uint GetErrorMessage(short* buffer, uint bufferLength)
        { throw new NotImplementedException("GetErrorMessage"); }

        private int FilterException(_EXCEPTION_POINTERS* pExceptionPointers)
        {
            // This method is completely handled by the C++ wrapper to the JIT-EE interface,
            // and should never reach the managed implementation.
            Debug.Assert(false, "CorInfoImpl.FilterException should not be called");
            throw new NotSupportedException("FilterException");
        }

        private void HandleException(_EXCEPTION_POINTERS* pExceptionPointers)
        {
            // This method is completely handled by the C++ wrapper to the JIT-EE interface,
            // and should never reach the managed implementation.
            Debug.Assert(false, "CorInfoImpl.HandleException should not be called");
            throw new NotSupportedException("HandleException");
        }

        private void ThrowExceptionForJitResult(HRESULT result)
        { throw new NotImplementedException("ThrowExceptionForJitResult"); }
        private void ThrowExceptionForHelper(ref CORINFO_HELPER_DESC throwHelper)
        { throw new NotImplementedException("ThrowExceptionForHelper"); }

        private void getEEInfo(ref CORINFO_EE_INFO pEEInfoOut)
        {
            pEEInfoOut = new CORINFO_EE_INFO();

#if DEBUG
            // In debug, write some bogus data to the struct to ensure we have filled everything
            // properly.
            fixed (CORINFO_EE_INFO* tmp = &pEEInfoOut)
                MemoryHelper.FillMemory((byte*)tmp, 0xcc, Marshal.SizeOf<CORINFO_EE_INFO>());
#endif

            int pointerSize = this.PointerSize;

            pEEInfoOut.offsetOfDelegateInstance = (uint)pointerSize;            // Delegate::m_firstParameter
            pEEInfoOut.offsetOfDelegateFirstTarget = (uint)(4 * pointerSize);   // Delegate::m_functionPointer

            pEEInfoOut.offsetOfObjArrayData = (uint)(2 * pointerSize);
        }

        [return: MarshalAs(UnmanagedType.LPWStr)]
        private string getJitTimeLogFilename()
        {
            return null;
        }

        private mdToken getMethodDefFromMethod(CORINFO_METHOD_STRUCT_* hMethod)
        { throw new NotImplementedException("getMethodDefFromMethod"); }

        private static byte[] StringToUTF8(string s)
        {
            int byteCount = Encoding.UTF8.GetByteCount(s);
            byte[] bytes = new byte[byteCount + 1];
            Encoding.UTF8.GetBytes(s, 0, s.Length, bytes, 0);
            return bytes;
        }

        private byte* getMethodName(CORINFO_METHOD_STRUCT_* ftn, byte** moduleName)
        {
            MethodDesc method = HandleToObject(ftn);

            if (moduleName != null)
            {
                MetadataType typeDef = method.OwningType.GetTypeDefinition() as MetadataType;
                if (typeDef != null)
                    *moduleName = (byte*)GetPin(StringToUTF8(typeDef.GetFullName()));
                else
                    *moduleName = (byte*)GetPin(StringToUTF8("unknown"));
            }

            return (byte*)GetPin(StringToUTF8(method.Name));
        }

        private uint getMethodHash(CORINFO_METHOD_STRUCT_* ftn)
        {
            return (uint)HandleToObject(ftn).GetHashCode();
        }

        private byte* findNameOfToken(CORINFO_MODULE_STRUCT_* moduleHandle, mdToken token, byte* szFQName, UIntPtr FQNameCapacity)
        { throw new NotImplementedException("findNameOfToken"); }

        private bool getSystemVAmd64PassStructInRegisterDescriptor(CORINFO_CLASS_STRUCT_* structHnd, SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR* structPassInRegDescPtr)
        {
            TypeDesc type = HandleToObject(structHnd);

            if (type.IsValueType)
            {
                // TODO: actually implement
                // https://github.com/dotnet/corert/issues/158
                if (type.GetElementSize() <= 8)
                {
                    structPassInRegDescPtr->passedInRegisters = true;
                    structPassInRegDescPtr->eightByteCount = 1;
                    structPassInRegDescPtr->eightByteClassifications0 = SystemVClassificationType.SystemVClassificationTypeInteger;
                    structPassInRegDescPtr->eightByteSizes0 = (byte)type.GetElementSize();
                    structPassInRegDescPtr->eightByteOffsets0 = 0;
                }
                else
                    structPassInRegDescPtr->passedInRegisters = false;
            }
            else
            {
                structPassInRegDescPtr->passedInRegisters = false;
            }

            return true;
        }

        private uint getThreadTLSIndex(ref void* ppIndirection)
        { throw new NotImplementedException("getThreadTLSIndex"); }
        private void* getInlinedCallFrameVptr(ref void* ppIndirection)
        { throw new NotImplementedException("getInlinedCallFrameVptr"); }
        private int* getAddrOfCaptureThreadGlobal(ref void* ppIndirection)
        { throw new NotImplementedException("getAddrOfCaptureThreadGlobal"); }
        private SIZE_T* getAddrModuleDomainID(CORINFO_MODULE_STRUCT_* module)
        { throw new NotImplementedException("getAddrModuleDomainID"); }
        private void* getHelperFtn(CorInfoHelpFunc ftnNum, ref void* ppIndirection)
        {
            JitHelperId id;

            switch (ftnNum)
            {
                case CorInfoHelpFunc.CORINFO_HELP_THROW: id = JitHelperId.Throw; break;
                case CorInfoHelpFunc.CORINFO_HELP_RETHROW: id = JitHelperId.Rethrow; break;
                case CorInfoHelpFunc.CORINFO_HELP_OVERFLOW: id = JitHelperId.Overflow; break;
                case CorInfoHelpFunc.CORINFO_HELP_RNGCHKFAIL: id = JitHelperId.RngChkFail; break;
                case CorInfoHelpFunc.CORINFO_HELP_FAIL_FAST: id = JitHelperId.FailFast; break;
                case CorInfoHelpFunc.CORINFO_HELP_THROWNULLREF: id = JitHelperId.ThrowNullRef; break;
                case CorInfoHelpFunc.CORINFO_HELP_THROWDIVZERO: id = JitHelperId.ThrowDivZero; break;

                case CorInfoHelpFunc.CORINFO_HELP_ASSIGN_REF: id = JitHelperId.WriteBarrier; break;
                case CorInfoHelpFunc.CORINFO_HELP_CHECKED_ASSIGN_REF: id = JitHelperId.CheckedWriteBarrier; break;
                case CorInfoHelpFunc.CORINFO_HELP_ASSIGN_BYREF: id = JitHelperId.ByRefWriteBarrier; break;

                case CorInfoHelpFunc.CORINFO_HELP_ARRADDR_ST: id = JitHelperId.Stelem_Ref; break;
                case CorInfoHelpFunc.CORINFO_HELP_LDELEMA_REF: id = JitHelperId.Ldelema_Ref; break;

                case CorInfoHelpFunc.CORINFO_HELP_MEMSET: id = JitHelperId.MemSet; break;
                case CorInfoHelpFunc.CORINFO_HELP_MEMCPY: id = JitHelperId.MemCpy; break;

                case CorInfoHelpFunc.CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE: id = JitHelperId.GetRuntimeTypeHandle; break;
                case CorInfoHelpFunc.CORINFO_HELP_METHODDESC_TO_STUBRUNTIMEMETHOD: id = JitHelperId.GetRuntimeMethodHandle; break;
                case CorInfoHelpFunc.CORINFO_HELP_FIELDDESC_TO_STUBRUNTIMEFIELD: id = JitHelperId.GetRuntimeFieldHandle; break;

                case CorInfoHelpFunc.CORINFO_HELP_BOX: id = JitHelperId.Box; break;
                case CorInfoHelpFunc.CORINFO_HELP_BOX_NULLABLE: id = JitHelperId.Box_Nullable; break;
                case CorInfoHelpFunc.CORINFO_HELP_UNBOX: id = JitHelperId.Unbox; break;
                case CorInfoHelpFunc.CORINFO_HELP_UNBOX_NULLABLE: id = JitHelperId.Unbox_Nullable; break;
                case CorInfoHelpFunc.CORINFO_HELP_NEW_MDARR: id = JitHelperId.NewMultiDimArr; break;

                case CorInfoHelpFunc.CORINFO_HELP_LMUL: id = JitHelperId.LMul; break;
                case CorInfoHelpFunc.CORINFO_HELP_LMUL_OVF: id = JitHelperId.LMulOfv; break;
                case CorInfoHelpFunc.CORINFO_HELP_ULMUL_OVF: id = JitHelperId.ULMulOvf; break;
                case CorInfoHelpFunc.CORINFO_HELP_LDIV: id = JitHelperId.LDiv; break;
                case CorInfoHelpFunc.CORINFO_HELP_LMOD: id = JitHelperId.LMod; break;
                case CorInfoHelpFunc.CORINFO_HELP_ULDIV: id = JitHelperId.ULDiv; break;
                case CorInfoHelpFunc.CORINFO_HELP_ULMOD: id = JitHelperId.ULMod; break;
                case CorInfoHelpFunc.CORINFO_HELP_LLSH: id = JitHelperId.LLsh; break;
                case CorInfoHelpFunc.CORINFO_HELP_LRSH: id = JitHelperId.LRsh; break;
                case CorInfoHelpFunc.CORINFO_HELP_LRSZ: id = JitHelperId.LRsz; break;
                case CorInfoHelpFunc.CORINFO_HELP_LNG2DBL: id = JitHelperId.Lng2Dbl; break;
                case CorInfoHelpFunc.CORINFO_HELP_ULNG2DBL: id = JitHelperId.ULng2Dbl; break;

                case CorInfoHelpFunc.CORINFO_HELP_DIV: id = JitHelperId.Div; break;
                case CorInfoHelpFunc.CORINFO_HELP_MOD: id = JitHelperId.Mod; break;
                case CorInfoHelpFunc.CORINFO_HELP_UDIV: id = JitHelperId.UDiv; break;
                case CorInfoHelpFunc.CORINFO_HELP_UMOD: id = JitHelperId.UMod; break;

                case CorInfoHelpFunc.CORINFO_HELP_DBL2INT: id = JitHelperId.Dbl2Int; break;
                case CorInfoHelpFunc.CORINFO_HELP_DBL2INT_OVF: id = JitHelperId.Dbl2IntOvf; break;
                case CorInfoHelpFunc.CORINFO_HELP_DBL2LNG: id = JitHelperId.Dbl2Lng; break;
                case CorInfoHelpFunc.CORINFO_HELP_DBL2LNG_OVF: id = JitHelperId.Dbl2LngOvf; break;
                case CorInfoHelpFunc.CORINFO_HELP_DBL2UINT: id = JitHelperId.Dbl2UInt; break;
                case CorInfoHelpFunc.CORINFO_HELP_DBL2UINT_OVF: id = JitHelperId.Dbl2UIntOvf; break;
                case CorInfoHelpFunc.CORINFO_HELP_DBL2ULNG: id = JitHelperId.Dbl2ULng; break;
                case CorInfoHelpFunc.CORINFO_HELP_DBL2ULNG_OVF: id = JitHelperId.Dbl2ULngOvf; break;

                case CorInfoHelpFunc.CORINFO_HELP_FLTREM: id = JitHelperId.DblRem; break;
                case CorInfoHelpFunc.CORINFO_HELP_DBLREM: id = JitHelperId.FltRem; break;
                case CorInfoHelpFunc.CORINFO_HELP_FLTROUND: id = JitHelperId.DblRound; break;
                case CorInfoHelpFunc.CORINFO_HELP_DBLROUND: id = JitHelperId.FltRound; break;

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

            return (void*)ObjectToHandle(entryPoint);
        }

        private void getFunctionEntryPoint(CORINFO_METHOD_STRUCT_* ftn, ref CORINFO_CONST_LOOKUP pResult, CORINFO_ACCESS_FLAGS accessFlags)
        { throw new NotImplementedException("getFunctionEntryPoint"); }
        private void getFunctionFixedEntryPoint(CORINFO_METHOD_STRUCT_* ftn, ref CORINFO_CONST_LOOKUP pResult)
        { throw new NotImplementedException("getFunctionFixedEntryPoint"); }
        private void* getMethodSync(CORINFO_METHOD_STRUCT_* ftn, ref void* ppIndirection)
        { throw new NotImplementedException("getMethodSync"); }

        private CorInfoHelpFunc getLazyStringLiteralHelper(CORINFO_MODULE_STRUCT_* handle)
        {
            // TODO: Lazy string literal helper
            return CorInfoHelpFunc.CORINFO_HELP_UNDEF;
        }

        private CORINFO_MODULE_STRUCT_* embedModuleHandle(CORINFO_MODULE_STRUCT_* handle, ref void* ppIndirection)
        { throw new NotImplementedException("embedModuleHandle"); }
        private CORINFO_CLASS_STRUCT_* embedClassHandle(CORINFO_CLASS_STRUCT_* handle, ref void* ppIndirection)
        { throw new NotImplementedException("embedClassHandle"); }
        private CORINFO_METHOD_STRUCT_* embedMethodHandle(CORINFO_METHOD_STRUCT_* handle, ref void* ppIndirection)
        { throw new NotImplementedException("embedMethodHandle"); }
        private CORINFO_FIELD_STRUCT_* embedFieldHandle(CORINFO_FIELD_STRUCT_* handle, ref void* ppIndirection)
        { throw new NotImplementedException("embedFieldHandle"); }

        private void embedGenericHandle(ref CORINFO_RESOLVED_TOKEN pResolvedToken, [MarshalAs(UnmanagedType.Bool)]bool fEmbedParent, ref CORINFO_GENERICHANDLE_RESULT pResult)
        {
#if DEBUG
            // In debug, write some bogus data to the struct to ensure we have filled everything
            // properly.
            fixed (CORINFO_GENERICHANDLE_RESULT* tmp = &pResult)
                MemoryHelper.FillMemory((byte*)tmp, 0xcc, Marshal.SizeOf<CORINFO_GENERICHANDLE_RESULT>());
#endif

            if (!fEmbedParent && pResolvedToken.hMethod != null)
            {
                throw new NotImplementedException("embedGenericHandle");
            }
            else if (!fEmbedParent && pResolvedToken.hField != null)
            {
                pResult.handleType = CorInfoGenericHandleType.CORINFO_HANDLETYPE_FIELD;
                pResult.compileTimeHandle = (CORINFO_GENERIC_STRUCT_*)pResolvedToken.hField;

                // fRuntimeLookup = th.IsSharedByGenericInstantiations() && pFD->IsStatic();

                // TODO: shared generics
                // If the target is not shared then we've already got our result and
                // can simply do a static look up
                pResult.lookup.lookupKind.needsRuntimeLookup = false;

                pResult.lookup.constLookup.handle = (CORINFO_GENERIC_STRUCT_*)pResult.compileTimeHandle;
                pResult.lookup.constLookup.accessType = InfoAccessType.IAT_VALUE;
            }
            else
            {
                TypeDesc td = HandleToObject(pResolvedToken.hClass);

                pResult.handleType = CorInfoGenericHandleType.CORINFO_HANDLETYPE_CLASS;
                pResult.compileTimeHandle = (CORINFO_GENERIC_STRUCT_*)pResolvedToken.hClass;

                // TODO? If we're embedding a method handle for a method that points to a sub-class of the actual
                //       class, we might need to embed the actual declaring type in compileTimeHandle.  

                // TODO: shared generics
                // IsSharedByGenericInstantiations would not work here. The runtime lookup is required
                // even for standalone generic variables that show up as __Canon here.
                //fRuntimeLookup = th.IsCanonicalSubtype();

                // If the target is not shared then we've already got our result and
                // can simply do a static look up
                pResult.lookup.lookupKind.needsRuntimeLookup = false;

                if (pResolvedToken.tokenType == CorInfoTokenKind.CORINFO_TOKENKIND_NewObj)
                {
                    pResult.lookup.constLookup.handle = (CORINFO_GENERIC_STRUCT_*)ObjectToHandle(_compilation.NodeFactory.ConstructedTypeSymbol(td));
                }
                else
                    pResult.lookup.constLookup.handle = (CORINFO_GENERIC_STRUCT_*)ObjectToHandle(_compilation.NodeFactory.NecessaryTypeSymbol(td));
                pResult.lookup.constLookup.accessType = InfoAccessType.IAT_VALUE;
            }

            Debug.Assert(pResult.compileTimeHandle != null);
        }

        private void getLocationOfThisType(out CORINFO_LOOKUP_KIND result, CORINFO_METHOD_STRUCT_* context)
        {
            result = new CORINFO_LOOKUP_KIND();
            result.needsRuntimeLookup = false;
            result.runtimeLookupKind = CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_THISOBJ;

            // TODO: shared generics
        }

        private void* getPInvokeUnmanagedTarget(CORINFO_METHOD_STRUCT_* method, ref void* ppIndirection)
        { throw new NotImplementedException("getPInvokeUnmanagedTarget"); }
        private void* getAddressOfPInvokeFixup(CORINFO_METHOD_STRUCT_* method, ref void* ppIndirection)
        { throw new NotImplementedException("getAddressOfPInvokeFixup"); }
        private void getAddressOfPInvokeTarget(CORINFO_METHOD_STRUCT_* method, ref CORINFO_CONST_LOOKUP pLookup)
        { throw new NotImplementedException("getAddressOfPInvokeTarget"); }
        private void* GetCookieForPInvokeCalliSig(CORINFO_SIG_INFO* szMetaSig, ref void* ppIndirection)
        { throw new NotImplementedException("GetCookieForPInvokeCalliSig"); }
        [return: MarshalAs(UnmanagedType.I1)]
        private bool canGetCookieForPInvokeCalliSig(CORINFO_SIG_INFO* szMetaSig)
        { throw new NotImplementedException("canGetCookieForPInvokeCalliSig"); }
        private CORINFO_JUST_MY_CODE_HANDLE_* getJustMyCodeHandle(CORINFO_METHOD_STRUCT_* method, ref CORINFO_JUST_MY_CODE_HANDLE_** ppIndirection)
        { throw new NotImplementedException("getJustMyCodeHandle"); }
        private void GetProfilingHandle([MarshalAs(UnmanagedType.Bool)] ref bool pbHookFunction, ref void* pProfilerHandle, [MarshalAs(UnmanagedType.Bool)] ref bool pbIndirectedHandles)
        { throw new NotImplementedException("GetProfilingHandle"); }

        private void getCallInfo(ref CORINFO_RESOLVED_TOKEN pResolvedToken, CORINFO_RESOLVED_TOKEN* pConstrainedResolvedToken, CORINFO_METHOD_STRUCT_* callerHandle, CORINFO_CALLINFO_FLAGS flags, ref CORINFO_CALL_INFO pResult)
        {
#if DEBUG
            // In debug, write some bogus data to the struct to ensure we have filled everything
            // properly.
            fixed (CORINFO_CALL_INFO* tmp = &pResult)
                MemoryHelper.FillMemory((byte*)tmp, 0xcc, Marshal.SizeOf<CORINFO_CALL_INFO>());
#endif
            MethodDesc method = HandleToObject(pResolvedToken.hMethod);

            // Spec says that a callvirt lookup ignores static methods. Since static methods
            // can't have the exact same signature as instance methods, a lookup that found
            // a static method would have never found an instance method.
            if (method.Signature.IsStatic && (flags & CORINFO_CALLINFO_FLAGS.CORINFO_CALLINFO_CALLVIRT) != 0)
            {
                throw new BadImageFormatException();
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
                pResult.thisTransform = CORINFO_THIS_TRANSFORM.CORINFO_NO_THIS_TRANSFORM;
            }
            else
            {
                // We have a "constrained." call.  Try a partial resolve of the constraint call.  Note that this
                // will not necessarily resolve the call exactly, since we might be compiling
                // shared generic code - it may just resolve it to a candidate suitable for
                // JIT compilation, and require a runtime lookup for the actual code pointer
                // to call.

                MethodDesc directMethod = constrainedType.TryResolveConstraintMethodApprox(exactType, method, out forceUseRuntimeLookup);
                if (directMethod != null)
                {
                    // Either
                    //    1. no constraint resolution at compile time (!directMethod)
                    // OR 2. no code sharing lookup in call
                    // OR 3. we have have resolved to an instantiating stub

                    methodAfterConstraintResolution = directMethod;

                    Debug.Assert(!methodAfterConstraintResolution.OwningType.IsInterface);
                    resolvedConstraint = true;
                    pResult.thisTransform = CORINFO_THIS_TRANSFORM.CORINFO_NO_THIS_TRANSFORM;

                    exactType = constrainedType;
                }
                else if (constrainedType.IsValueType)
                {
                    pResult.thisTransform = CORINFO_THIS_TRANSFORM.CORINFO_BOX_THIS;
                }
                else
                {
                    pResult.thisTransform = CORINFO_THIS_TRANSFORM.CORINFO_DEREF_THIS;
                }
            }

            MethodDesc targetMethod = methodAfterConstraintResolution;

            //
            // Determine whether to perform direct call
            //

            bool directCall = false;
            bool resolvedCallVirt = false;

            if ((flags & CORINFO_CALLINFO_FLAGS.CORINFO_CALLINFO_LDFTN) != 0)
            {
                // Delegate targets are always treated as direct calls here. (It would be nice to clean it up...).
                directCall = true;
            }
            else if (targetMethod.Signature.IsStatic)
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

            pResult.hMethod = ObjectToHandle(targetMethod);
            pResult.methodFlags = getMethodAttribsInternal(targetMethod);

            pResult.classFlags = getClassAttribsInternal(targetMethod.OwningType);

            Get_CORINFO_SIG_INFO(targetMethod.Signature, out pResult.sig);

            // Get the required verification information in case it is needed by the verifier later
            if (pResult.hMethod != pResolvedToken.hMethod)
            {
                pResult.verMethodFlags = getMethodAttribsInternal(targetMethod);
                Get_CORINFO_SIG_INFO(targetMethod.Signature, out pResult.verSig);
            }
            else
            {
                pResult.verMethodFlags = pResult.methodFlags;
                pResult.verSig = pResult.sig;
            }

            pResult.accessAllowed = CorInfoIsAccessAllowedResult.CORINFO_ACCESS_ALLOWED;

            pResult._nullInstanceCheck = (uint)(((flags & CORINFO_CALLINFO_FLAGS.CORINFO_CALLINFO_CALLVIRT) != 0) ? 1 : 0);

            // TODO: Support Generics
            if (targetMethod.HasInstantiation)
            {
                pResult.contextHandle = contextFromMethod(targetMethod);
            }
            else
            {
                pResult.contextHandle = contextFromType(targetMethod.OwningType);
            }

            pResult._exactContextNeedsRuntimeLookup = 0;

            pResult.codePointerOrStubLookup.lookupKind.needsRuntimeLookup = false;

            if (directCall)
            {
                if (targetMethod.IsConstructor && targetMethod.OwningType.IsString)
                {
                    // Calling a string constructor doesn't call the actual constructor.
                    targetMethod = targetMethod.GetStringInitializer();
                }

                
                pResult.codePointerOrStubLookup.constLookup.accessType = InfoAccessType.IAT_VALUE;

                ISymbolNode targetNode;

                // Compensate for always treating delegates as direct calls above
                if ((flags & CORINFO_CALLINFO_FLAGS.CORINFO_CALLINFO_LDFTN) != 0 &&
                    (flags & CORINFO_CALLINFO_FLAGS.CORINFO_CALLINFO_CALLVIRT) != 0 && !resolvedCallVirt)
                {
                    pResult.kind = CORINFO_CALL_KIND.CORINFO_VIRTUALCALL_LDVIRTFTN;
                    targetNode = _compilation.NodeFactory.ReadyToRunHelper(ReadyToRunHelperId.ResolveVirtualFunction, targetMethod);
                }
                else
                {
                    pResult.kind = CORINFO_CALL_KIND.CORINFO_CALL;
                    targetNode = _compilation.NodeFactory.MethodEntrypoint(targetMethod);
                }

                pResult.codePointerOrStubLookup.constLookup.addr = (void*)ObjectToHandle(targetNode);

                pResult.nullInstanceCheck = resolvedCallVirt;
            }
            else if (!targetMethod.OwningType.IsInterface)
            {
                // CORINFO_CALL_CODE_POINTER tells the JIT that this is indirect
                // call that should not be inlined.
                pResult.kind = CORINFO_CALL_KIND.CORINFO_CALL_CODE_POINTER;
                pResult.codePointerOrStubLookup.constLookup.accessType = InfoAccessType.IAT_VALUE;

                pResult.codePointerOrStubLookup.constLookup.addr =
                    (void*)ObjectToHandle(_compilation.NodeFactory.ReadyToRunHelper(ReadyToRunHelperId.VirtualCall, targetMethod));

                if ((pResult.methodFlags & (uint)CorInfoFlag.CORINFO_FLG_DELEGATE_INVOKE) != 0)
                {
                    pResult._nullInstanceCheck = 1;
                }
            }
            else
            {
                pResult.nullInstanceCheck = true;
                pResult.kind = CORINFO_CALL_KIND.CORINFO_CALL_CODE_POINTER;
                pResult.codePointerOrStubLookup.lookupKind.needsRuntimeLookup = false;
                pResult.codePointerOrStubLookup.constLookup.accessType = InfoAccessType.IAT_VALUE;

                pResult.codePointerOrStubLookup.constLookup.addr =
                    (void*)ObjectToHandle(_compilation.NodeFactory.ReadyToRunHelper(ReadyToRunHelperId.InterfaceDispatch, targetMethod));
            }

            // TODO: Generics
            // pResult.instParamLookup
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        private bool canAccessFamily(CORINFO_METHOD_STRUCT_* hCaller, CORINFO_CLASS_STRUCT_* hInstanceType)
        { throw new NotImplementedException("canAccessFamily"); }
        [return: MarshalAs(UnmanagedType.Bool)]
        private bool isRIDClassDomainID(CORINFO_CLASS_STRUCT_* cls)
        { throw new NotImplementedException("isRIDClassDomainID"); }
        private uint getClassDomainID(CORINFO_CLASS_STRUCT_* cls, ref void* ppIndirection)
        { throw new NotImplementedException("getClassDomainID"); }
        private void* getFieldAddress(CORINFO_FIELD_STRUCT_* field, ref void* ppIndirection)
        { throw new NotImplementedException("getFieldAddress"); }
        private IntPtr getVarArgsHandle(CORINFO_SIG_INFO* pSig, ref void* ppIndirection)
        { throw new NotImplementedException("getVarArgsHandle"); }
        [return: MarshalAs(UnmanagedType.I1)]
        private bool canGetVarArgsHandle(CORINFO_SIG_INFO* pSig)
        { throw new NotImplementedException("canGetVarArgsHandle"); }

        private InfoAccessType constructStringLiteral(CORINFO_MODULE_STRUCT_* module, mdToken metaTok, ref void* ppValue)
        {
            MethodIL methodIL = (MethodIL)HandleToObject((IntPtr)module);
            object literal = methodIL.GetObject((int)metaTok);
            ppValue = (void*)ObjectToHandle(_compilation.NodeFactory.StringIndirection((string)literal));
            return InfoAccessType.IAT_PPVALUE;
        }

        private InfoAccessType emptyStringLiteral(ref void* ppValue)
        { throw new NotImplementedException("emptyStringLiteral"); }
        private uint getFieldThreadLocalStoreID(CORINFO_FIELD_STRUCT_* field, ref void* ppIndirection)
        { throw new NotImplementedException("getFieldThreadLocalStoreID"); }
        private void setOverride(IntPtr pOverride, CORINFO_METHOD_STRUCT_* currentMethod)
        { throw new NotImplementedException("setOverride"); }
        private void addActiveDependency(CORINFO_MODULE_STRUCT_* moduleFrom, CORINFO_MODULE_STRUCT_* moduleTo)
        { throw new NotImplementedException("addActiveDependency"); }
        private CORINFO_METHOD_STRUCT_* GetDelegateCtor(CORINFO_METHOD_STRUCT_* methHnd, CORINFO_CLASS_STRUCT_* clsHnd, CORINFO_METHOD_STRUCT_* targetMethodHnd, ref DelegateCtorArgs pCtorData)
        { throw new NotImplementedException("GetDelegateCtor"); }
        private void MethodCompileComplete(CORINFO_METHOD_STRUCT_* methHnd)
        { throw new NotImplementedException("MethodCompileComplete"); }
        private void* getTailCallCopyArgsThunk(CORINFO_SIG_INFO* pSig, CorInfoHelperTailCallSpecialHandling flags)
        { throw new NotImplementedException("getTailCallCopyArgsThunk"); }

        private void* getMemoryManager()
        {
            // This method is completely handled by the C++ wrapper to the JIT-EE interface,
            // and should never reach the managed implementation.
            Debug.Assert(false, "CorInfoImpl.getMemoryManager should not be called");
            throw new NotSupportedException("getMemoryManager");
        }

        private byte[] _code;
        private byte[] _coldCode;

        private byte[] _roData;
        private BlobNode _roDataBlob;

        private int _numFrameInfos;
        private int _usedFrameInfos;
        private FrameInfo[] _frameInfos;

        private CORINFO_EH_CLAUSE[] _ehClauses;

        private Dictionary<int, SequencePoint> _sequencePoints;
        private Dictionary<uint, ILLocalVariable> _localSlotToInfoMap;
        private Dictionary<uint, string> _parameterIndexToNameMap;
        private DebugLocInfo[] _debugLocInfos;
        private DebugVarInfo[] _debugVarInfos;

        private void allocMem(uint hotCodeSize, uint coldCodeSize, uint roDataSize, uint xcptnsCount, CorJitAllocMemFlag flag, ref void* hotCodeBlock, ref void* coldCodeBlock, ref void* roDataBlock)
        {
            hotCodeBlock = (void*)GetPin(_code = new byte[hotCodeSize]);

            if (coldCodeSize != 0)
                coldCodeBlock = (void*)GetPin(_coldCode = new byte[coldCodeSize]);

            if (roDataSize != 0)
            {
                int alignment = 8;

                if ((flag & CorJitAllocMemFlag.CORJIT_ALLOCMEM_FLG_RODATA_16BYTE_ALIGN) != 0)
                {
                    alignment = 16;
                }
                else if (roDataSize < 8)
                {
                    alignment = PointerSize;
                }

                _roData = new byte[roDataSize];

                _roDataBlob = _compilation.NodeFactory.ReadOnlyDataBlob(
                    "__readonlydata_" + _compilation.NameMangler.GetMangledMethodName(MethodBeingCompiled),
                    _roData, alignment);

                roDataBlock = (void*)GetPin(_roData);
            }

            if (_numFrameInfos > 0)
            {
                _frameInfos = new FrameInfo[_numFrameInfos];
            }
        }

        private void reserveUnwindInfo([MarshalAs(UnmanagedType.Bool)]bool isFunclet, [MarshalAs(UnmanagedType.Bool)]bool isColdCode, uint unwindSize)
        {
            _numFrameInfos++;
        }

        private void allocUnwindInfo(byte* pHotCode, byte* pColdCode, uint startOffset, uint endOffset, uint unwindSize, byte* pUnwindBlock, CorJitFuncKind funcKind)
        {
            int extraBlobData = 0;

            if (_compilation.Options.TargetOS == TargetOS.Windows)
            {
                // The unwind info blob are followed by byte that identifies the type of the funclet
                // and other optional data that follows
                extraBlobData = 1;
            }

            byte[] blobData = new byte[unwindSize + extraBlobData];

            for (uint i = 0; i < unwindSize; i++)
            {
                blobData[i] = pUnwindBlock[i];
            }

            if (extraBlobData != 0)
            {
                // Capture the type of the funclet in unwind info blob
                pUnwindBlock[unwindSize] = (byte)funcKind;
            }

            _frameInfos[_usedFrameInfos++] = new FrameInfo((int)startOffset, (int)endOffset, blobData);
        }

        private void* allocGCInfo(UIntPtr size)
        {
            // TODO: GC Info
            return (void*)GetPin(new byte[(int)size]);
        }

        private void yieldExecution()
        {
            // Nothing to do
        }

        private void setEHcount(uint cEH)
        {
            _ehClauses = new CORINFO_EH_CLAUSE[cEH];
        }

        private void setEHinfo(uint EHnumber, ref CORINFO_EH_CLAUSE clause)
        {
            _ehClauses[EHnumber] = clause;
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        private bool logMsg(uint level, byte* fmt, IntPtr args)
        {
            // Console.WriteLine(Marshal.PtrToStringAnsi((IntPtr)fmt));
            return false;
        }

        private int doAssert(byte* szFile, int iLine, byte* szExpr)
        {
            Log.WriteLine(Marshal.PtrToStringAnsi((IntPtr)szFile) + ":" + iLine);
            Log.WriteLine(Marshal.PtrToStringAnsi((IntPtr)szExpr));

            return 1;
        }

        private void reportFatalError(CorJitResult result)
        { throw new NotImplementedException("reportFatalError"); }
        private HRESULT allocBBProfileBuffer(uint count, ref ProfileBuffer* profileBuffer)
        { throw new NotImplementedException("allocBBProfileBuffer"); }
        private HRESULT getBBProfileData(CORINFO_METHOD_STRUCT_* ftnHnd, ref uint count, ref ProfileBuffer* profileBuffer, ref uint numRuns)
        { throw new NotImplementedException("getBBProfileData"); }

        private void recordCallSite(uint instrOffset, CORINFO_SIG_INFO* callSig, CORINFO_METHOD_STRUCT_* methodHandle)
        {
        }

        private ArrayBuilder<Relocation> _relocs;

        /// <summary>
        /// Various type of block.
        /// </summary>
        public enum BlockType : sbyte
        {
            /// <summary>Not a generated block.</summary>
            Unknown = -1,
            /// <summary>Represent code.</summary>
            Code = 0,
            /// <summary>Represent cold code (i.e. code not called frequently).</summary>
            ColdCode = 1,
            /// <summary>Read-only data.</summary>
            ROData = 2
        }

        private BlockType findKnownBlock(void* location, out int offset)
        {
            fixed (byte* pCode = _code)
            {
                if (pCode <= (byte*)location && (byte*)location < pCode + _code.Length)
                {
                    offset = (int)((byte*)location - pCode);
                    return BlockType.Code;
                }
            }

            if (_coldCode != null)
            {
                fixed (byte* pColdCode = _coldCode)
                {
                    if (pColdCode <= (byte*)location && (byte*)location < pColdCode + _coldCode.Length)
                    {
                        offset = (int)((byte*)location - pColdCode);
                        return BlockType.ColdCode;
                    }
                }
            }

            if (_roData != null)
            {
                fixed (byte* pROData = _roData)
                {
                    if (pROData <= (byte*)location && (byte*)location < pROData + _roData.Length)
                    {
                        offset = (int)((byte*)location - pROData);
                        return BlockType.ROData;
                    }
                }
            }

            offset = 0;
            return BlockType.Unknown;
        }

        private void recordRelocation(void* location, void* target, ushort fRelocType, ushort slotNum, int addlDelta)
        {
            Relocation reloc;

            reloc.RelocType = (RelocType)fRelocType;

            BlockType locationBlock = findKnownBlock(location, out reloc.Offset);
            Debug.Assert(locationBlock != BlockType.Unknown, "BlockType.Unknown not expected");

            // TODO: Arbitrary relocs
            if (locationBlock != BlockType.Code)
                throw new NotImplementedException("Arbitrary relocs");

            BlockType targetBlock = findKnownBlock(target, out reloc.Delta);
            switch (targetBlock)
            {
                case BlockType.Code:
                    reloc.Target = _methodCodeNode;
                    break;

                case BlockType.ColdCode:
                    // TODO: Arbitrary relocs
                    throw new NotImplementedException("Arbitrary relocs");

                case BlockType.ROData:
                    reloc.Target = _roDataBlob;
                    break;

                default:
                    // Reloc points to something outside of the generated blocks
                    var targetObject = HandleToObject((IntPtr)target);

                    if (targetObject is FieldDesc)
                    {
                        // We only support FieldDesc for InitializeArray intrinsic right now.
                        throw new NotImplementedException("RuntimeFieldHandle is not implemented");
                    }

                    reloc.Target = (ISymbolNode)targetObject;
                    break;
            }

            reloc.Delta += addlDelta;

            if (_relocs.Count == 0)
                _relocs.EnsureCapacity(_code.Length / 32 + 1);
            _relocs.Add(reloc);
        }

        private ushort getRelocTypeHint(void* target)
        {
            if (_compilation.TypeSystemContext.Target.Architecture == TargetArchitecture.X64)
                return (ushort)ILCompiler.DependencyAnalysis.RelocType.IMAGE_REL_BASED_REL32;

            return UInt16.MaxValue;
        }

        private void getModuleNativeEntryPointRange(ref void* pStart, ref void* pEnd)
        { throw new NotImplementedException("getModuleNativeEntryPointRange"); }

        private uint getExpectedTargetArchitecture()
        {
            return 0x8664; // AMD64
        }

        private uint getJitFlags(ref CORJIT_FLAGS flags, uint sizeInBytes)
        {
            flags.corJitFlags = 
                CorJitFlag.CORJIT_FLG_SKIP_VERIFICATION |
                CorJitFlag.CORJIT_FLG_READYTORUN |
                CorJitFlag.CORJIT_FLG_RELOC |
                CorJitFlag.CORJIT_FLG_DEBUG_INFO |
                CorJitFlag.CORJIT_FLG_PREJIT;

            if (_compilation.Options.TargetOS != TargetOS.Windows)
            {
                flags.corJitFlags |= CorJitFlag.CORJIT_FLG_CFI_UNWIND;
            }

            flags.corJitFlags2 = 0;

            return (uint)sizeof(CORJIT_FLAGS);
        }
    }
}
