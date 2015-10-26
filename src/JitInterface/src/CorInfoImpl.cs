// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Reflection;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using Internal.IL;

using ILToNative;

namespace Internal.JitInterface
{
    unsafe partial class CorInfoImpl
    {
        IntPtr _comp;

        [DllImport("kernel32.dll", SetLastError = true)]
        extern static IntPtr LoadLibraryEx(string s, IntPtr handle, int flags);

        [DllImport("kernel32.dll")]
        extern static IntPtr GetProcAddress(IntPtr handle, string s);

        [UnmanagedFunctionPointerAttribute(CallingConvention.StdCall)]
        delegate IntPtr _getJIT();

        IntPtr _jit;

        [UnmanagedFunctionPointerAttribute(CallingConvention.StdCall)]
        delegate CorJitResult _compileMethod(IntPtr _this, IntPtr comp, ref CORINFO_METHOD_INFO info, uint flags,
            out IntPtr nativeEntry, out uint codeSize);

        _compileMethod _compile;

        Compilation _compilation;

        public CorInfoImpl(Compilation compilation)
        {
            _compilation = compilation;

            _comp = CreateUnmanagedInstance();

            string clrjitPath = AppContext.BaseDirectory + "\\protojit.dll";
            IntPtr jit = LoadLibraryEx(clrjitPath, new IntPtr(0), 0x1300);

            IntPtr proc = GetProcAddress(jit, "getJit");
            if (proc == new IntPtr(0))
                throw new Exception("JIT initialization failed");

            var getJIT = Marshal.GetDelegateForFunctionPointer<_getJIT>(proc);
            _jit = getJIT();

            _compile = Marshal.GetDelegateForFunctionPointer<_compileMethod>(**((IntPtr**)_jit));
        }

        public TextWriter Log
        {
            get
            {
                return _compilation.Log;
            }
        }

        public MethodCode CompileMethod(MethodDesc method)
        {
            try
            {
                CORINFO_METHOD_INFO methodInfo;
                Get_CORINFO_METHOD_INFO(method, out methodInfo);

                uint flags = (uint)(
                    CorJitFlag.CORJIT_FLG_SKIP_VERIFICATION |
                    CorJitFlag.CORJIT_FLG_READYTORUN |
                    CorJitFlag.CORJIT_FLG_RELOC |
                    CorJitFlag.CORJIT_FLG_PREJIT);

                IntPtr nativeEntry;
                uint codeSize;
                _compile(_jit, _comp, ref methodInfo, flags, out nativeEntry, out codeSize);

                if (_relocs != null)
                    _relocs.Sort((x, y) => (x.Block != y.Block) ? (x.Block - y.Block) : (x.Offset - y.Offset));

                return new MethodCode()
                {
                    Code = _code,
                    ColdCode = _coldCode,
                    ROData = _roData,

                    Relocs = (_relocs != null) ? _relocs.ToArray() : null
                };
            }
            finally
            {
                FlushPins();
            }
        }

        int PointerSize
        {
            get
            {
                return _compilation.TypeSystemContext.Target.PointerSize;
            }
        }

        // TODO: Free pins at the end of the compilation
        Dictionary<Object, GCHandle> _pins = new Dictionary<object, GCHandle>();

        IntPtr GetPin(Object obj)
        {
            GCHandle handle;
            if (!_pins.TryGetValue(obj, out handle))
            {
                handle = GCHandle.Alloc(obj, GCHandleType.Pinned);
                _pins.Add(obj, handle);
            }
            return handle.AddrOfPinnedObject();
        }
        void FlushPins()
        {
            foreach (var pin in _pins)
                pin.Value.Free();
            _pins.Clear();

            _code = null;
            _coldCode = null;
            _roData = null;
            _relocs = null;
        }

        Dictionary<Object, IntPtr> _objectToHandle = new Dictionary<Object, IntPtr>();
        List<Object> _handleToObject = new List<Object>();

        const int handleMultipler = 8;
        const int handleBase = 0x420000;

        IntPtr ObjectToHandle(Object obj)
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

        Object HandleToObject(IntPtr handle)
        {
            int index = ((int)handle - handleBase) / handleMultipler;
            return _handleToObject[index];
        }

        MethodDesc HandleToObject(CORINFO_METHOD_STRUCT_* method) { return (MethodDesc)HandleToObject((IntPtr)method); }
        CORINFO_METHOD_STRUCT_* ObjectToHandle(MethodDesc method) { return (CORINFO_METHOD_STRUCT_*)ObjectToHandle((Object)method); }

        TypeDesc HandleToObject(CORINFO_CLASS_STRUCT_* type) { return (TypeDesc)HandleToObject((IntPtr)type); }
        CORINFO_CLASS_STRUCT_* ObjectToHandle(TypeDesc type) { return (CORINFO_CLASS_STRUCT_*)ObjectToHandle((Object)type); }

        FieldDesc HandleToObject(CORINFO_FIELD_STRUCT_* field) { return (FieldDesc)HandleToObject((IntPtr)field); }
        CORINFO_FIELD_STRUCT_* ObjectToHandle(FieldDesc field) { return (CORINFO_FIELD_STRUCT_*)ObjectToHandle((Object)field); }

        bool Get_CORINFO_METHOD_INFO(MethodDesc method, out CORINFO_METHOD_INFO methodInfo)
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

        void Get_CORINFO_SIG_INFO(MethodSignature signature, out CORINFO_SIG_INFO sig)
        {
            sig.callConv = (CorInfoCallConv)0;
            if (!signature.IsStatic) sig.callConv |= CorInfoCallConv.CORINFO_CALLCONV_HASTHIS;

            TypeDesc returnType = signature.ReturnType;

            CorInfoType corInfoRetType = asCorInfoType(signature.ReturnType, out sig.retTypeClass);
            sig._retType = (byte)corInfoRetType;
            sig.retTypeSigClass = sig.retTypeClass; // The difference between the two is not relevant for ILToNative

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

        void Get_CORINFO_SIG_INFO(TypeDesc[] locals, out CORINFO_SIG_INFO sig)
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

        CorInfoType asCorInfoType(TypeDesc type)
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

        CorInfoType asCorInfoType(TypeDesc type, out CORINFO_CLASS_STRUCT_* structType)
        {
            var corInfoType = asCorInfoType(type);
            structType = ((corInfoType == CorInfoType.CORINFO_TYPE_CLASS) ||
                (corInfoType == CorInfoType.CORINFO_TYPE_VALUECLASS) ||
                (corInfoType == CorInfoType.CORINFO_TYPE_BYREF)) ? ObjectToHandle(type) : null;
            return corInfoType;
        }

        CorInfoIntrinsics asCorInfoIntrinsic(IntrinsicMethodKind methodKind)
        {
            switch (methodKind)
            {
                case IntrinsicMethodKind.RuntimeHelpersInitializeArray:
                    return CorInfoIntrinsics.CORINFO_INTRINSIC_InitializeArray;
                default:
                    return CorInfoIntrinsics.CORINFO_INTRINSIC_Illegal;
            }
        }

        MethodDesc methodFromContext(CORINFO_CONTEXT_STRUCT* contextStruct)
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

        TypeDesc typeFromContext(CORINFO_CONTEXT_STRUCT* contextStruct)
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

        uint getMethodAttribsInternal(MethodDesc method)
        {
            CorInfoFlag result = 0;

            EcmaMethod ecmaMethod = method.GetTypicalMethodDefinition() as EcmaMethod;
            if (ecmaMethod != null)
            {
                var attribs = ecmaMethod.Attributes;

                // CORINFO_FLG_PROTECTED - verification only

                if ((attribs & MethodAttributes.Static) != 0)
                    result |= CorInfoFlag.CORINFO_FLG_STATIC;

                // TODO: if (pMD->IsSynchronized())
                //    result |= CORINFO_FLG_SYNCH;

                if (ecmaMethod.IsIntrinsic)
                    result |= CorInfoFlag.CORINFO_FLG_INTRINSIC;

                if ((attribs & MethodAttributes.Virtual) != 0)
                    result |= CorInfoFlag.CORINFO_FLG_VIRTUAL;
                if ((attribs & MethodAttributes.Abstract) != 0)
                    result |= CorInfoFlag.CORINFO_FLG_ABSTRACT;
                if ((attribs & MethodAttributes.SpecialName) != 0)
                {
                    string name = method.Name;
                    if (name == ".ctor" || name == ".cctor")
                        result |= CorInfoFlag.CORINFO_FLG_CONSTRUCTOR;
                }

                //
                // See if we need to embed a .cctor call at the head of the
                // method body.
                //

                var owningType = method.OwningType;

                var typeAttribs = ((EcmaType)owningType.GetTypeDefinition()).Attributes;

                // method or class might have the final bit
                if ((attribs & MethodAttributes.Final) != 0 || (typeAttribs & TypeAttributes.Sealed) != 0)
                    result |= CorInfoFlag.CORINFO_FLG_FINAL;

                // TODO: Generics
                // if (pMD->IsSharedByGenericInstantiations())
                //     result |= CORINFO_FLG_SHAREDINST;

                // TODO: PInvoke
                // if ((attribs & MethodAttributes.PinvokeImpl) != 0)
                //    result |= CorInfoFlag.CORINFO_FLG_PINVOKE;

                // TODO: Cache inlining hits
                // Check for an inlining directive.

                var implAttribs = ecmaMethod.ImplAttributes;
                if ((implAttribs & MethodImplAttributes.NoInlining) != 0)
                {
                    /* Function marked as not inlineable */
                    result |= CorInfoFlag.CORINFO_FLG_DONT_INLINE;
                }
                else if ((implAttribs & MethodImplAttributes.AggressiveInlining) != 0)
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
            }
            else
            {
                if (method.Signature.IsStatic)
                    result |= CorInfoFlag.CORINFO_FLG_STATIC;
            }

            result |= CorInfoFlag.CORINFO_FLG_NOSECURITYWRAP;

            return (uint)result;
        }

        uint getMethodAttribs(IntPtr _this, CORINFO_METHOD_STRUCT_* ftn)
        {
            return getMethodAttribsInternal(HandleToObject(ftn));
        }

        void setMethodAttribs(IntPtr _this, CORINFO_METHOD_STRUCT_* ftn, CorInfoMethodRuntimeFlags attribs)
        {
            // TODO: Inlining
        }

        void getMethodSig(IntPtr _this, CORINFO_METHOD_STRUCT_* ftn, CORINFO_SIG_INFO* sig, CORINFO_CLASS_STRUCT_* memberParent)
        {
            MethodDesc method = HandleToObject(ftn);

            Get_CORINFO_SIG_INFO(method.Signature, out *sig);
        }

        [return: MarshalAs(UnmanagedType.I1)]
        bool getMethodInfo(IntPtr _this, CORINFO_METHOD_STRUCT_* ftn, ref CORINFO_METHOD_INFO info)
        {
            return Get_CORINFO_METHOD_INFO(HandleToObject(ftn), out info);
        }

        CorInfoInline canInline(IntPtr _this, CORINFO_METHOD_STRUCT_* callerHnd, CORINFO_METHOD_STRUCT_* calleeHnd, ref uint pRestrictions)
        {
            // TODO: Inlining
            return CorInfoInline.INLINE_NEVER;
        }

        void reportInliningDecision(IntPtr _this, CORINFO_METHOD_STRUCT_* inlinerHnd, CORINFO_METHOD_STRUCT_* inlineeHnd, CorInfoInline inlineResult, byte* reason)
        {
        }

        [return: MarshalAs(UnmanagedType.I1)]
        bool canTailCall(IntPtr _this, CORINFO_METHOD_STRUCT_* callerHnd, CORINFO_METHOD_STRUCT_* declaredCalleeHnd, CORINFO_METHOD_STRUCT_* exactCalleeHnd, [MarshalAs(UnmanagedType.I1)]bool fIsTailPrefix)
        {
            // TODO: tail call
            return false;
        }

        void reportTailCallDecision(IntPtr _this, CORINFO_METHOD_STRUCT_* callerHnd, CORINFO_METHOD_STRUCT_* calleeHnd, [MarshalAs(UnmanagedType.I1)]bool fIsTailPrefix, CorInfoTailCall tailCallResult, byte* reason)
        {
        }

        void getEHinfo(IntPtr _this, CORINFO_METHOD_STRUCT_* ftn, uint EHnumber, ref CORINFO_EH_CLAUSE clause)
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

        CORINFO_CLASS_STRUCT_* getMethodClass(IntPtr _this, CORINFO_METHOD_STRUCT_* method)
        {
            var m = HandleToObject(method);
            return ObjectToHandle(m.OwningType);
        }

        CORINFO_MODULE_STRUCT_* getMethodModule(IntPtr _this, CORINFO_METHOD_STRUCT_* method)
        { throw new NotImplementedException(); }
        void getMethodVTableOffset(IntPtr _this, CORINFO_METHOD_STRUCT_* method, ref uint offsetOfIndirection, ref uint offsetAfterIndirection)
        { throw new NotImplementedException(); }
        CorInfoIntrinsics getIntrinsicID(IntPtr _this, CORINFO_METHOD_STRUCT_* method)
        {
            var md = HandleToObject(method);
            return asCorInfoIntrinsic(IntrinsicMethods.GetIntrinsicMethodClassification(md));
        }

        [return: MarshalAs(UnmanagedType.I1)]
        bool isInSIMDModule(IntPtr _this, CORINFO_CLASS_STRUCT_* classHnd)
        {
            // TODO: SIMD
            return false;
        }

        CorInfoUnmanagedCallConv getUnmanagedCallConv(IntPtr _this, CORINFO_METHOD_STRUCT_* method)
        { throw new NotImplementedException(); }
        [return: MarshalAs(UnmanagedType.Bool)]
        bool pInvokeMarshalingRequired(IntPtr _this, CORINFO_METHOD_STRUCT_* method, CORINFO_SIG_INFO* callSiteSig)
        { throw new NotImplementedException(); }
        [return: MarshalAs(UnmanagedType.Bool)]
        bool satisfiesMethodConstraints(IntPtr _this, CORINFO_CLASS_STRUCT_* parent, CORINFO_METHOD_STRUCT_* method)
        { throw new NotImplementedException(); }
        [return: MarshalAs(UnmanagedType.Bool)]
        bool isCompatibleDelegate(IntPtr _this, CORINFO_CLASS_STRUCT_* objCls, CORINFO_CLASS_STRUCT_* methodParentCls, CORINFO_METHOD_STRUCT_* method, CORINFO_CLASS_STRUCT_* delegateCls, [MarshalAs(UnmanagedType.Bool)] ref bool pfIsOpenDelegate)
        { throw new NotImplementedException(); }
        [return: MarshalAs(UnmanagedType.Bool)]
        bool isDelegateCreationAllowed(IntPtr _this, CORINFO_CLASS_STRUCT_* delegateHnd, CORINFO_METHOD_STRUCT_* calleeHnd)
        {
            return true;
        }
        CorInfoInstantiationVerification isInstantiationOfVerifiedGeneric(IntPtr _this, CORINFO_METHOD_STRUCT_* method)
        { throw new NotImplementedException(); }
        void initConstraintsForVerification(IntPtr _this, CORINFO_METHOD_STRUCT_* method, [MarshalAs(UnmanagedType.Bool)] ref bool pfHasCircularClassConstraints, [MarshalAs(UnmanagedType.Bool)] ref bool pfHasCircularMethodConstraint)
        { throw new NotImplementedException(); }
        CorInfoCanSkipVerificationResult canSkipMethodVerification(IntPtr _this, CORINFO_METHOD_STRUCT_* ftnHandle)
        { throw new NotImplementedException(); }

        void methodMustBeLoadedBeforeCodeIsRun(IntPtr _this, CORINFO_METHOD_STRUCT_* method)
        {
        }

        CORINFO_METHOD_STRUCT_* mapMethodDeclToMethodImpl(IntPtr _this, CORINFO_METHOD_STRUCT_* method)
        { throw new NotImplementedException(); }

        void getGSCookie(IntPtr _this, GSCookie* pCookieVal, GSCookie** ppCookieVal)
        {
            // TODO: fully implement GS cookies

            if (pCookieVal != null)
            {
                *pCookieVal = (GSCookie)0x216D6F6D202C6948;
                *ppCookieVal = null;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        void resolveToken(IntPtr _this, ref CORINFO_RESOLVED_TOKEN pResolvedToken)
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

        void findSig(IntPtr _this, CORINFO_MODULE_STRUCT_* module, uint sigTOK, CORINFO_CONTEXT_STRUCT* context, CORINFO_SIG_INFO* sig)
        {
            var methodIL = (MethodIL)HandleToObject((IntPtr)module);
            Get_CORINFO_SIG_INFO((MethodSignature)methodIL.GetObject((int)sigTOK), out *sig);
        }

        void findCallSiteSig(IntPtr _this, CORINFO_MODULE_STRUCT_* module, uint methTOK, CORINFO_CONTEXT_STRUCT* context, CORINFO_SIG_INFO* sig)
        {
            // TODO: dynamic scopes
            // TODO: verification
            var methodIL = (MethodIL)HandleToObject((IntPtr)module);
            Get_CORINFO_SIG_INFO(((MethodDesc)methodIL.GetObject((int)methTOK)).Signature, out *sig);
        }

        CORINFO_CLASS_STRUCT_* getTokenTypeAsHandle(IntPtr _this, ref CORINFO_RESOLVED_TOKEN pResolvedToken)
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

        CorInfoCanSkipVerificationResult canSkipVerification(IntPtr _this, CORINFO_MODULE_STRUCT_* module)
        { throw new NotImplementedException(); }
        [return: MarshalAs(UnmanagedType.Bool)]
        bool isValidToken(IntPtr _this, CORINFO_MODULE_STRUCT_* module, uint metaTOK)
        { throw new NotImplementedException(); }
        [return: MarshalAs(UnmanagedType.Bool)]
        bool isValidStringRef(IntPtr _this, CORINFO_MODULE_STRUCT_* module, uint metaTOK)
        { throw new NotImplementedException(); }
        [return: MarshalAs(UnmanagedType.Bool)]
        bool shouldEnforceCallvirtRestriction(IntPtr _this, CORINFO_MODULE_STRUCT_* scope)
        { throw new NotImplementedException(); }

        CorInfoType asCorInfoType(IntPtr _this, CORINFO_CLASS_STRUCT_* cls)
        {
            var type = HandleToObject(cls);
            return asCorInfoType(type);
        }

        byte* getClassName(IntPtr _this, CORINFO_CLASS_STRUCT_* cls)
        {
            var type = HandleToObject(cls);
            return (byte*)GetPin(StringToUTF8(type.ToString()));
        }

        int appendClassName(IntPtr _this, short** ppBuf, ref int pnBufLen, CORINFO_CLASS_STRUCT_* cls, [MarshalAs(UnmanagedType.Bool)]bool fNamespace, [MarshalAs(UnmanagedType.Bool)]bool fFullInst, [MarshalAs(UnmanagedType.Bool)]bool fAssembly)
        { throw new NotImplementedException(); }

        [return: MarshalAs(UnmanagedType.Bool)]
        bool isValueClass(IntPtr _this, CORINFO_CLASS_STRUCT_* cls)
        {
            return HandleToObject(cls).IsValueType;
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        bool canInlineTypeCheckWithObjectVTable(IntPtr _this, CORINFO_CLASS_STRUCT_* cls)
        { throw new NotImplementedException(); }

        uint getClassAttribs(IntPtr _this, CORINFO_CLASS_STRUCT_* cls)
        {
            TypeDesc type = HandleToObject(cls);
            return getClassAttribsInternal(type);
        }

        uint getClassAttribsInternal(TypeDesc type)
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

            // TODO
            // if (type.ContainsPointers)
            //    result |= CorInfoFlag.CORINFO_FLG_CONTAINS_GC_PTR;

            if (type.IsDelegate)
                result |= CorInfoFlag.CORINFO_FLG_DELEGATE;

            var ecmaType = type.GetTypeDefinition() as EcmaType;
            if (ecmaType != null)
            {
                var attr = ecmaType.Attributes;
                if ((attr & TypeAttributes.BeforeFieldInit) != 0)
                    result |= CorInfoFlag.CORINFO_FLG_BEFOREFIELDINIT;

                if ((attr & TypeAttributes.Sealed) != 0)
                    result |= CorInfoFlag.CORINFO_FLG_FINAL;
            }

            return (uint)result;
        }

        [return: MarshalAs(UnmanagedType.Bool)]

        bool isStructRequiringStackAllocRetBuf(IntPtr _this, CORINFO_CLASS_STRUCT_* cls)
        {
            // Disable this optimization. It has limited value (only kicks in on x86, and only for less common structs),
            // causes bugs and introduces odd ABI differences not compatible with ReadyToRun.
            return false;
        }

        CORINFO_MODULE_STRUCT_* getClassModule(IntPtr _this, CORINFO_CLASS_STRUCT_* cls)
        { throw new NotImplementedException(); }
        CORINFO_ASSEMBLY_STRUCT_* getModuleAssembly(IntPtr _this, CORINFO_MODULE_STRUCT_* mod)
        { throw new NotImplementedException(); }
        byte* getAssemblyName(IntPtr _this, CORINFO_ASSEMBLY_STRUCT_* assem)
        { throw new NotImplementedException(); }

        void* LongLifetimeMalloc(IntPtr _this, UIntPtr sz)
        {
            return (void*)Marshal.AllocCoTaskMem((int)sz);
        }

        void LongLifetimeFree(IntPtr _this, void* obj)
        {
            Marshal.FreeCoTaskMem((IntPtr)obj);
        }

        byte* getClassModuleIdForStatics(IntPtr _this, CORINFO_CLASS_STRUCT_* cls, CORINFO_MODULE_STRUCT_** pModule, void** ppIndirection)
        { throw new NotImplementedException(); }

        uint getClassSize(IntPtr _this, CORINFO_CLASS_STRUCT_* cls)
        {
            TypeDesc type = HandleToObject(cls);
            return (uint)type.GetElementSize();
        }

        uint getClassAlignmentRequirement(IntPtr _this, CORINFO_CLASS_STRUCT_* cls, [MarshalAs(UnmanagedType.Bool)]bool fDoubleAlignHint)
        { throw new NotImplementedException(); }

        int GatherClassGCLayout(TypeDesc type, byte* gcPtrs)
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
                else if (fieldType.HasBaseType)
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

        uint getClassGClayout(IntPtr _this, CORINFO_CLASS_STRUCT_* cls, byte* gcPtrs)
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

        uint getClassNumInstanceFields(IntPtr _this, CORINFO_CLASS_STRUCT_* cls)
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

        CORINFO_FIELD_STRUCT_* getFieldInClass(IntPtr _this, CORINFO_CLASS_STRUCT_* clsHnd, int num)
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
        bool checkMethodModifier(IntPtr _this, CORINFO_METHOD_STRUCT_* hMethod, byte* modifier, [MarshalAs(UnmanagedType.Bool)]bool fOptional)
        { throw new NotImplementedException(); }
        CorInfoHelpFunc getNewHelper(IntPtr _this, ref CORINFO_RESOLVED_TOKEN pResolvedToken, CORINFO_METHOD_STRUCT_* callerHandle)
        { throw new NotImplementedException(); }
        CorInfoHelpFunc getNewArrHelper(IntPtr _this, CORINFO_CLASS_STRUCT_* arrayCls)
        { throw new NotImplementedException(); }
        CorInfoHelpFunc getCastingHelper(IntPtr _this, ref CORINFO_RESOLVED_TOKEN pResolvedToken, [MarshalAs(UnmanagedType.I1)]bool fThrowing)
        { throw new NotImplementedException(); }
        CorInfoHelpFunc getSharedCCtorHelper(IntPtr _this, CORINFO_CLASS_STRUCT_* clsHnd)
        { throw new NotImplementedException(); }
        CorInfoHelpFunc getSecurityPrologHelper(IntPtr _this, CORINFO_METHOD_STRUCT_* ftn)
        { throw new NotImplementedException(); }

        CORINFO_CLASS_STRUCT_* getTypeForBox(IntPtr _this, CORINFO_CLASS_STRUCT_* cls)
        {
            var type = HandleToObject(cls);

            var typeForBox = type.IsNullable ? type.Instantiation[0] : type;

            return ObjectToHandle(typeForBox);
        }

        CorInfoHelpFunc getBoxHelper(IntPtr _this, CORINFO_CLASS_STRUCT_* cls)
        {
            var type = HandleToObject(cls);

            return type.IsNullable ? CorInfoHelpFunc.CORINFO_HELP_BOX_NULLABLE : CorInfoHelpFunc.CORINFO_HELP_BOX;
        }

        CorInfoHelpFunc getUnBoxHelper(IntPtr _this, CORINFO_CLASS_STRUCT_* cls)
        { throw new NotImplementedException(); }

        void getReadyToRunHelper(IntPtr _this, ref CORINFO_RESOLVED_TOKEN pResolvedToken, CorInfoHelpFunc id, ref CORINFO_CONST_LOOKUP pLookup)
        {
            pLookup.accessType = InfoAccessType.IAT_VALUE;

            switch (id)
            {
                case CorInfoHelpFunc.CORINFO_HELP_READYTORUN_NEW:
                    {
                        var type = HandleToObject(pResolvedToken.hClass);
                        _compilation.AddType(type);
                        _compilation.MarkAsConstructed(type);

                        pLookup.addr = (void*)ObjectToHandle(_compilation.GetReadyToRunHelper(ReadyToRunHelperId.NewHelper, type));
                    }
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_READYTORUN_NEWARR_1:
                    {
                        var type = HandleToObject(pResolvedToken.hClass);
                        var arrayType = type.Context.GetArrayType(type);
                        _compilation.AddType(arrayType);
                        _compilation.MarkAsConstructed(arrayType);

                        pLookup.addr = (void*)ObjectToHandle(_compilation.GetReadyToRunHelper(ReadyToRunHelperId.NewArr1, arrayType));
                    }
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_READYTORUN_ISINSTANCEOF:
                    {
                        var type = HandleToObject(pResolvedToken.hClass);
                        _compilation.AddType(type);

                        pLookup.addr = (void*)ObjectToHandle(_compilation.GetReadyToRunHelper(ReadyToRunHelperId.IsInstanceOf, type));
                    }
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_READYTORUN_CHKCAST:
                    {
                        var type = HandleToObject(pResolvedToken.hClass);
                        _compilation.AddType(type);

                        pLookup.addr = (void*)ObjectToHandle(_compilation.GetReadyToRunHelper(ReadyToRunHelperId.CastClass, type));
                    }
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_READYTORUN_STATIC_BASE:
                    {
                        var type = HandleToObject(pResolvedToken.hClass);
                        pLookup.addr = (void*)ObjectToHandle(_compilation.GetReadyToRunHelper(ReadyToRunHelperId.GetNonGCStaticBase, type));
                    }
                    break;
                case CorInfoHelpFunc.CORINFO_HELP_READYTORUN_DELEGATE_CTOR:
                    {
                        var type = HandleToObject(pResolvedToken.hClass);
                        _compilation.AddType(type);
                        var method = HandleToObject(pResolvedToken.hMethod);
                        _compilation.AddMethod(method);

                        DelegateInfo delegateInfo = (DelegateInfo)_compilation.GetDelegateCtor(method);
                        _compilation.AddMethod(delegateInfo.Ctor);

                        pLookup.addr = (void*)ObjectToHandle(_compilation.GetReadyToRunHelper(ReadyToRunHelperId.DelegateCtor, delegateInfo));
                    }
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        byte* getHelperName(IntPtr _this, CorInfoHelpFunc helpFunc)
        { throw new NotImplementedException(); }

        CorInfoInitClassResult initClass(IntPtr _this, CORINFO_FIELD_STRUCT_* field, CORINFO_METHOD_STRUCT_* method, CORINFO_CONTEXT_STRUCT* context, [MarshalAs(UnmanagedType.Bool)]bool speculative)
        {
            FieldDesc fd = field == null ? null : HandleToObject(field);
            Debug.Assert(fd == null || fd.IsStatic);

            MethodDesc md = HandleToObject(method);
            TypeDesc type = fd != null ? fd.OwningType : typeFromContext(context);

            if (!type.HasStaticConstructor)
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

        void classMustBeLoadedBeforeCodeIsRun(IntPtr _this, CORINFO_CLASS_STRUCT_* cls)
        {
        }

        CORINFO_CLASS_STRUCT_* getBuiltinClass(IntPtr _this, CorInfoClassId classId)
        { throw new NotImplementedException(); }

        CorInfoType getTypeForPrimitiveValueClass(IntPtr _this, CORINFO_CLASS_STRUCT_* cls)
        {
            var type = HandleToObject(cls);

            if (!type.IsPrimitive && !type.IsEnum)
                return CorInfoType.CORINFO_TYPE_UNDEF;

            return asCorInfoType(type);
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        bool canCast(IntPtr _this, CORINFO_CLASS_STRUCT_* child, CORINFO_CLASS_STRUCT_* parent)
        { throw new NotImplementedException(); }
        [return: MarshalAs(UnmanagedType.Bool)]
        bool areTypesEquivalent(IntPtr _this, CORINFO_CLASS_STRUCT_* cls1, CORINFO_CLASS_STRUCT_* cls2)
        { throw new NotImplementedException(); }
        CORINFO_CLASS_STRUCT_* mergeClasses(IntPtr _this, CORINFO_CLASS_STRUCT_* cls1, CORINFO_CLASS_STRUCT_* cls2)
        { throw new NotImplementedException(); }
        CORINFO_CLASS_STRUCT_* getParentType(IntPtr _this, CORINFO_CLASS_STRUCT_* cls)
        { throw new NotImplementedException(); }

        CorInfoType getChildType(IntPtr _this, CORINFO_CLASS_STRUCT_* clsHnd, ref CORINFO_CLASS_STRUCT_* clsRet)
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
        bool satisfiesClassConstraints(IntPtr _this, CORINFO_CLASS_STRUCT_* cls)
        { throw new NotImplementedException(); }

        [return: MarshalAs(UnmanagedType.Bool)]
        bool isSDArray(IntPtr _this, CORINFO_CLASS_STRUCT_* cls)
        {
            var td = HandleToObject(cls);
            return td.IsSzArray;
        }

        uint getArrayRank(IntPtr _this, CORINFO_CLASS_STRUCT_* cls)
        { throw new NotImplementedException(); }

        void* getArrayInitializationData(IntPtr _this, CORINFO_FIELD_STRUCT_* field, uint size)
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

        CorInfoIsAccessAllowedResult canAccessClass(IntPtr _this, ref CORINFO_RESOLVED_TOKEN pResolvedToken, CORINFO_METHOD_STRUCT_* callerHandle, ref CORINFO_HELPER_DESC pAccessHelper)
        {
            // TODO: Access check
            return CorInfoIsAccessAllowedResult.CORINFO_ACCESS_ALLOWED;
        }

        byte* getFieldName(IntPtr _this, CORINFO_FIELD_STRUCT_* ftn, byte** moduleName)
        {
            var field = HandleToObject(ftn);
            if (moduleName != null)
            {
                throw new NotImplementedException();
            }

            return (byte*)GetPin(StringToUTF8(field.Name));
        }

        CORINFO_CLASS_STRUCT_* getFieldClass(IntPtr _this, CORINFO_FIELD_STRUCT_* field)
        {
            var fieldDesc = HandleToObject(field);
            return ObjectToHandle(fieldDesc.OwningType);
        }

        CorInfoType getFieldType(IntPtr _this, CORINFO_FIELD_STRUCT_* field, ref CORINFO_CLASS_STRUCT_* structType, CORINFO_CLASS_STRUCT_* memberParent)
        {
            var fieldDesc = HandleToObject(field);
            return asCorInfoType(fieldDesc.FieldType, out structType);
        }

        uint getFieldOffset(IntPtr _this, CORINFO_FIELD_STRUCT_* field)
        {
            var fieldDesc = HandleToObject(field);

            Debug.Assert(fieldDesc.Offset != FieldAndOffset.InvalidOffset);

            return (uint)fieldDesc.Offset;
        }

        [return: MarshalAs(UnmanagedType.I1)]
        bool isWriteBarrierHelperRequired(IntPtr _this, CORINFO_FIELD_STRUCT_* field)
        { throw new NotImplementedException(); }

        void getFieldInfo(IntPtr _this, ref CORINFO_RESOLVED_TOKEN pResolvedToken, CORINFO_METHOD_STRUCT_* callerHandle, CORINFO_ACCESS_FLAGS flags, ref CORINFO_FIELD_INFO pResult)
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
                    throw new NotSupportedException();
                }

                // Make sure to include the type in the compilation
                _compilation.AddType(field.OwningType);

                fieldAccessor = CORINFO_FIELD_ACCESSOR.CORINFO_FIELD_STATIC_SHARED_STATIC_HELPER;
                pResult.helper = CorInfoHelpFunc.CORINFO_HELP_READYTORUN_STATIC_BASE;

                ReadyToRunHelperId helperId;
                if (field.IsThreadStatic)
                {
                    throw new NotImplementedException();
                }
                else if (field.HasGCStaticBase)
                {
                    helperId = ReadyToRunHelperId.GetGCStaticBase;
                }
                else
                {
                    helperId = ReadyToRunHelperId.GetNonGCStaticBase;
                }

                pResult.fieldLookup.addr = (void*)ObjectToHandle(_compilation.GetReadyToRunHelper(helperId, field.OwningType));
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
            pResult.fieldType = getFieldType(_this, pResolvedToken.hField, ref pResult.structType, pResolvedToken.hClass);
            pResult.accessAllowed = CorInfoIsAccessAllowedResult.CORINFO_ACCESS_ALLOWED;
            pResult.offset = (uint)field.Offset;

            // TODO: We need to implement access checks for fields and methods.  See JitInterface.cpp in mrtjit
            //       and STS::AccessCheck::CanAccess.
        }

        [return: MarshalAs(UnmanagedType.I1)]
        bool isFieldStatic(IntPtr _this, CORINFO_FIELD_STRUCT_* fldHnd)
        {
            return HandleToObject(fldHnd).IsStatic;
        }

        void getBoundaries(IntPtr _this, CORINFO_METHOD_STRUCT_* ftn, ref uint cILOffsets, ref uint* pILOffsets, BoundaryTypes* implicitBoundaries)
        {
            // TODO: Debugging
            cILOffsets = 0;
            pILOffsets = null;
            *implicitBoundaries = BoundaryTypes.DEFAULT_BOUNDARIES;
        }
        void setBoundaries(IntPtr _this, CORINFO_METHOD_STRUCT_* ftn, uint cMap, OffsetMapping* pMap)
        {
            // TODO: Debugging
        }
        void getVars(IntPtr _this, CORINFO_METHOD_STRUCT_* ftn, ref uint cVars, ILVarInfo** vars, [MarshalAs(UnmanagedType.U1)] ref bool extendOthers)
        {
            // TODO: Debugging

            cVars = 0;
            *vars = null;

            // Just tell the JIT to extend everything.
            extendOthers = true;
        }
        void setVars(IntPtr _this, CORINFO_METHOD_STRUCT_* ftn, uint cVars, NativeVarInfo* vars)
        {
            // TODO: Debugging
        }

        void* allocateArray(IntPtr _this, uint cBytes)
        {
            return (void *)Marshal.AllocCoTaskMem((int)cBytes);
        }

        void freeArray(IntPtr _this, void* array)
        {
            Marshal.FreeCoTaskMem((IntPtr)array);
        }

        CORINFO_ARG_LIST_STRUCT_* getArgNext(IntPtr _this, CORINFO_ARG_LIST_STRUCT_* args)
        {
            return (CORINFO_ARG_LIST_STRUCT_*)((int)args + 1);
        }

        CorInfoTypeWithMod getArgType(IntPtr _this, CORINFO_SIG_INFO* sig, CORINFO_ARG_LIST_STRUCT_* args, ref CORINFO_CLASS_STRUCT_* vcTypeRet)
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
                TypeDesc type = ((TypeDesc[])sigObj)[index];

                // TODO: Pinning
                CorInfoType corInfoType = asCorInfoType(type, out vcTypeRet);
                return (CorInfoTypeWithMod)corInfoType;
            }
        }

        CORINFO_CLASS_STRUCT_* getArgClass(IntPtr _this, CORINFO_SIG_INFO* sig, CORINFO_ARG_LIST_STRUCT_* args)
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
                TypeDesc type = methodSig[index];

                // TODO: Pinning
                return ObjectToHandle(type);
            }
        }

        CorInfoType getHFAType(IntPtr _this, CORINFO_CLASS_STRUCT_* hClass)
        { throw new NotImplementedException(); }
        HRESULT GetErrorHRESULT(IntPtr _this, _EXCEPTION_POINTERS* pExceptionPointers)
        { throw new NotImplementedException(); }
        uint GetErrorMessage(IntPtr _this, short* buffer, uint bufferLength)
        { throw new NotImplementedException(); }

        int FilterException(IntPtr _this, _EXCEPTION_POINTERS* pExceptionPointers)
        {
            Debug.Assert(false);
            throw new NotImplementedException();
        }

        void HandleException(IntPtr _this, _EXCEPTION_POINTERS* pExceptionPointers)
        { throw new NotImplementedException(); }
        void ThrowExceptionForJitResult(IntPtr _this, HRESULT result)
        { throw new NotImplementedException(); }
        void ThrowExceptionForHelper(IntPtr _this, ref CORINFO_HELPER_DESC throwHelper)
        { throw new NotImplementedException(); }

        void getEEInfo(IntPtr _this, ref CORINFO_EE_INFO pEEInfoOut)
        {
            pEEInfoOut = new CORINFO_EE_INFO();

            int pointerSize = this.PointerSize;

            pEEInfoOut.offsetOfDelegateInstance = (uint)pointerSize;            // Delegate::m_firstParameter
            pEEInfoOut.offsetOfDelegateFirstTarget = (uint)(4 * pointerSize);   // Delegate::m_functionPointer
        }

        [return: MarshalAs(UnmanagedType.LPWStr)]
        string getJitTimeLogFilename(IntPtr _this)
        {
            return null;
        }

        mdToken getMethodDefFromMethod(IntPtr _this, CORINFO_METHOD_STRUCT_* hMethod)
        { throw new NotImplementedException(); }

        static byte[] StringToUTF8(string s)
        {
            int byteCount = Encoding.UTF8.GetByteCount(s);
            byte[] bytes = new byte[byteCount + 1];
            Encoding.UTF8.GetBytes(s, 0, s.Length, bytes, 0);
            return bytes;
        }

        byte* getMethodName(IntPtr _this, CORINFO_METHOD_STRUCT_* ftn, byte** moduleName)
        {
            MethodDesc method = HandleToObject(ftn);

            if (moduleName != null)
            {
                EcmaType ecmaType = method.OwningType.GetTypeDefinition() as EcmaType;
                if (ecmaType != null)
                    *moduleName = (byte *)GetPin(StringToUTF8(ecmaType.Name));
                else
                    *moduleName = (byte*)GetPin(StringToUTF8("unknown"));
            }

            return (byte *)GetPin(StringToUTF8(method.Name));
        }

        uint getMethodHash(IntPtr _this, CORINFO_METHOD_STRUCT_* ftn)
        {
            return (uint)HandleToObject(ftn).GetHashCode();
        }

        byte* findNameOfToken(IntPtr _this, CORINFO_MODULE_STRUCT_* moduleHandle, mdToken token, byte* szFQName, UIntPtr FQNameCapacity)
        { throw new NotImplementedException(); }
        bool getSystemVAmd64PassStructInRegisterDescriptor(IntPtr _this, CORINFO_CLASS_STRUCT_* structHnd, SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR* structPassInRegDescPtr)
        { throw new NotImplementedException(); }
        int getIntConfigValue(IntPtr _this, String name, int defaultValue)
        { throw new NotImplementedException(); }
        short* getStringConfigValue(IntPtr _this, String name)
        { throw new NotImplementedException(); }
        void freeStringConfigValue(IntPtr _this, short* value)
        { throw new NotImplementedException(); }
        uint getThreadTLSIndex(IntPtr _this, ref void* ppIndirection)
        { throw new NotImplementedException(); }
        void* getInlinedCallFrameVptr(IntPtr _this, ref void* ppIndirection)
        { throw new NotImplementedException(); }
        int* getAddrOfCaptureThreadGlobal(IntPtr _this, ref void* ppIndirection)
        { throw new NotImplementedException(); }
        SIZE_T* getAddrModuleDomainID(IntPtr _this, CORINFO_MODULE_STRUCT_* module)
        { throw new NotImplementedException(); }
        void* getHelperFtn(IntPtr _this, CorInfoHelpFunc ftnNum, ref void* ppIndirection)
        {
            switch (ftnNum)
            {
                case CorInfoHelpFunc.CORINFO_HELP_RNGCHKFAIL:
                    return (void*)ObjectToHandle(_compilation.GetJitHelper(JitHelperId.RngChkFail));
                case CorInfoHelpFunc.CORINFO_HELP_ASSIGN_REF:
                    return (void*)ObjectToHandle(_compilation.GetJitHelper(JitHelperId.AssignRef));
                case CorInfoHelpFunc.CORINFO_HELP_CHECKED_ASSIGN_REF:
                    return (void*)ObjectToHandle(_compilation.GetJitHelper(JitHelperId.CheckedAssignRef));
                case CorInfoHelpFunc.CORINFO_HELP_THROW:                
                    return (void*)ObjectToHandle(_compilation.GetJitHelper(JitHelperId.Throw));
                case CorInfoHelpFunc.CORINFO_HELP_FAIL_FAST:
                    return (void*)ObjectToHandle(_compilation.GetJitHelper(JitHelperId.FailFast));
                case CorInfoHelpFunc.CORINFO_HELP_NEW_MDARR:
                    return (void*)ObjectToHandle(_compilation.GetJitHelper(JitHelperId.NewMDArray));
                default:
                    throw new NotImplementedException();
            }
        }
        void getFunctionEntryPoint(IntPtr _this, CORINFO_METHOD_STRUCT_* ftn, ref CORINFO_CONST_LOOKUP pResult, CORINFO_ACCESS_FLAGS accessFlags)
        { throw new NotImplementedException(); }
        void getFunctionFixedEntryPoint(IntPtr _this, CORINFO_METHOD_STRUCT_* ftn, ref CORINFO_CONST_LOOKUP pResult)
        { throw new NotImplementedException(); }
        void* getMethodSync(IntPtr _this, CORINFO_METHOD_STRUCT_* ftn, ref void* ppIndirection)
        { throw new NotImplementedException(); }

        CorInfoHelpFunc getLazyStringLiteralHelper(IntPtr _this, CORINFO_MODULE_STRUCT_* handle)
        {
            // TODO: Lazy string literal helper
            return CorInfoHelpFunc.CORINFO_HELP_UNDEF;
        }

        CORINFO_MODULE_STRUCT_* embedModuleHandle(IntPtr _this, CORINFO_MODULE_STRUCT_* handle, ref void* ppIndirection)
        { throw new NotImplementedException(); }
        CORINFO_CLASS_STRUCT_* embedClassHandle(IntPtr _this, CORINFO_CLASS_STRUCT_* handle, ref void* ppIndirection)
        { throw new NotImplementedException(); }
        CORINFO_METHOD_STRUCT_* embedMethodHandle(IntPtr _this, CORINFO_METHOD_STRUCT_* handle, ref void* ppIndirection)
        { throw new NotImplementedException(); }
        CORINFO_FIELD_STRUCT_* embedFieldHandle(IntPtr _this, CORINFO_FIELD_STRUCT_* handle, ref void* ppIndirection)
        { throw new NotImplementedException(); }

        void embedGenericHandle(IntPtr _this, ref CORINFO_RESOLVED_TOKEN pResolvedToken, [MarshalAs(UnmanagedType.Bool)]bool fEmbedParent, ref CORINFO_GENERICHANDLE_RESULT pResult)
        {
#if DEBUG
            // In debug, write some bogus data to the struct to ensure we have filled everything
            // properly.
            fixed (CORINFO_GENERICHANDLE_RESULT* tmp = &pResult)
                MemoryHelper.FillMemory((byte*)tmp, 0xcc, Marshal.SizeOf<CORINFO_GENERICHANDLE_RESULT>());
#endif

            if (!fEmbedParent && pResolvedToken.hMethod != null)
            {
                throw new NotImplementedException();
            }
            else if (!fEmbedParent && pResolvedToken.hField != null)
            {
                pResult.handleType = CorInfoGenericHandleType.CORINFO_HANDLETYPE_FIELD;
                pResult.compileTimeHandle = (CORINFO_GENERIC_STRUCT_*)pResolvedToken.hField;

                // fRuntimeLookup = th.IsSharedByGenericInstantiations() && pFD->IsStatic();
            }
            else
            {
                TypeDesc td = HandleToObject(pResolvedToken.hClass);

                pResult.handleType = CorInfoGenericHandleType.CORINFO_HANDLETYPE_CLASS;
                pResult.compileTimeHandle = (CORINFO_GENERIC_STRUCT_*)pResolvedToken.hClass;

                // TODO? If we're embedding a method handle for a method that points to a sub-class of the actual
                //       class, we might need to embed the actual declaring type in compileTimeHandle.  

                // IsSharedByGenericInstantiations would not work here. The runtime lookup is required
                // even for standalone generic variables that show up as __Canon here.
                //fRuntimeLookup = th.IsCanonicalSubtype();
            }

            Debug.Assert(pResult.compileTimeHandle != null);

            // TODO: shared generics
            //if (...)
            //{
            //    ...
            //}
            // else
            {
                // If the target is not shared then we've already got our result and
                // can simply do a static look up
                pResult.lookup.lookupKind.needsRuntimeLookup = false;

                pResult.lookup.constLookup.handle = (CORINFO_GENERIC_STRUCT_*)pResult.compileTimeHandle;
                pResult.lookup.constLookup.accessType = InfoAccessType.IAT_VALUE;
            }
        }

        void getLocationOfThisType(IntPtr _this, CORINFO_LOOKUP_KIND* result, CORINFO_METHOD_STRUCT_* context)
        {
            result->needsRuntimeLookup = false;
            result->runtimeLookupKind = CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_THISOBJ;

            // TODO: shared generics
        }

        void* getPInvokeUnmanagedTarget(IntPtr _this, CORINFO_METHOD_STRUCT_* method, ref void* ppIndirection)
        { throw new NotImplementedException(); }
        void* getAddressOfPInvokeFixup(IntPtr _this, CORINFO_METHOD_STRUCT_* method, ref void* ppIndirection)
        { throw new NotImplementedException(); }
        void* GetCookieForPInvokeCalliSig(IntPtr _this, CORINFO_SIG_INFO* szMetaSig, ref void* ppIndirection)
        { throw new NotImplementedException(); }
        [return: MarshalAs(UnmanagedType.I1)]
        bool canGetCookieForPInvokeCalliSig(IntPtr _this, CORINFO_SIG_INFO* szMetaSig)
        { throw new NotImplementedException(); }
        CORINFO_JUST_MY_CODE_HANDLE_* getJustMyCodeHandle(IntPtr _this, CORINFO_METHOD_STRUCT_* method, ref CORINFO_JUST_MY_CODE_HANDLE_** ppIndirection)
        { throw new NotImplementedException(); }
        void GetProfilingHandle(IntPtr _this, [MarshalAs(UnmanagedType.Bool)] ref bool pbHookFunction, ref void* pProfilerHandle, [MarshalAs(UnmanagedType.Bool)] ref bool pbIndirectedHandles)
        { throw new NotImplementedException(); }

        void getCallInfo(IntPtr _this, ref CORINFO_RESOLVED_TOKEN pResolvedToken, CORINFO_RESOLVED_TOKEN* pConstrainedResolvedToken, CORINFO_METHOD_STRUCT_* callerHandle, CORINFO_CALLINFO_FLAGS flags, ref CORINFO_CALL_INFO pResult)
        {
            // TODO: Constrained calls
            if (pConstrainedResolvedToken != null)
                throw new NotImplementedException();

            MethodDesc method = HandleToObject(pResolvedToken.hMethod);

            // TODO: Interface methods
            if (method.IsVirtual && method.OwningType.IsInterface)
                throw new NotImplementedException();

            pResult.hMethod = pResolvedToken.hMethod;
            pResult.methodFlags = getMethodAttribsInternal(method);

            pResult.classFlags = getClassAttribsInternal(method.OwningType);

            Get_CORINFO_SIG_INFO(method.Signature, out pResult.sig);

            pResult.verMethodFlags = pResult.methodFlags;
            pResult.verSig = pResult.sig;

            pResult.accessAllowed = CorInfoIsAccessAllowedResult.CORINFO_ACCESS_ALLOWED;

            // TODO: Constraint calls
            pResult.thisTransform = CORINFO_THIS_TRANSFORM.CORINFO_NO_THIS_TRANSFORM;
            
            pResult.kind = CORINFO_CALL_KIND.CORINFO_CALL;
            pResult._nullInstanceCheck = (uint)(((flags & CORINFO_CALLINFO_FLAGS.CORINFO_CALLINFO_CALLVIRT) != 0) ? 1 : 0);

            // TODO: Generics
            // pResult.contextHandle;
            // pResult._exactContextNeedsRuntimeLookup

            // TODO: CORINFO_VIRTUALCALL_STUB
            // TODO: CORINFO_CALL_CODE_POINTER
            pResult.codePointerOrStubLookup.constLookup.accessType = InfoAccessType.IAT_VALUE;

            if (((flags & CORINFO_CALLINFO_FLAGS.CORINFO_CALLINFO_CALLVIRT) != 0) && method.IsVirtual)
            {
                _compilation.AddVirtualSlot(method);
                pResult.codePointerOrStubLookup.constLookup.addr = 
                    (void *)ObjectToHandle(_compilation.GetReadyToRunHelper(ReadyToRunHelperId.VirtualCall, method));

                if ((pResult.methodFlags & (uint)CorInfoFlag.CORINFO_FLG_DELEGATE_INVOKE) != 0)
                {
                    pResult._nullInstanceCheck = 1;
                }
            }
            else
            {
                _compilation.AddMethod(method);
                pResult.codePointerOrStubLookup.constLookup.addr = pResolvedToken.hMethod;
            }


            // TODO: Generics
            // pResult.instParamLookup
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        bool canAccessFamily(IntPtr _this, CORINFO_METHOD_STRUCT_* hCaller, CORINFO_CLASS_STRUCT_* hInstanceType)
        { throw new NotImplementedException(); }
        [return: MarshalAs(UnmanagedType.Bool)]
        bool isRIDClassDomainID(IntPtr _this, CORINFO_CLASS_STRUCT_* cls)
        { throw new NotImplementedException(); }
        uint getClassDomainID(IntPtr _this, CORINFO_CLASS_STRUCT_* cls, ref void* ppIndirection)
        { throw new NotImplementedException(); }
        void* getFieldAddress(IntPtr _this, CORINFO_FIELD_STRUCT_* field, ref void* ppIndirection)
        { throw new NotImplementedException(); }
        IntPtr getVarArgsHandle(IntPtr _this, CORINFO_SIG_INFO* pSig, ref void* ppIndirection)
        { throw new NotImplementedException(); }
        [return: MarshalAs(UnmanagedType.I1)]
        bool canGetVarArgsHandle(IntPtr _this, CORINFO_SIG_INFO* pSig)
        { throw new NotImplementedException(); }

        InfoAccessType constructStringLiteral(IntPtr _this, CORINFO_MODULE_STRUCT_* module, mdToken metaTok, ref void* ppValue)
        {
            MethodIL methodIL = (MethodIL) HandleToObject((IntPtr)module);
            object literal = methodIL.GetObject((int)metaTok);
            ppValue = (void*) ObjectToHandle(literal);
            return InfoAccessType.IAT_PPVALUE;
        }

        InfoAccessType emptyStringLiteral(IntPtr _this, ref void* ppValue)
        { throw new NotImplementedException(); }
        uint getFieldThreadLocalStoreID(IntPtr _this, CORINFO_FIELD_STRUCT_* field, ref void* ppIndirection)
        { throw new NotImplementedException(); }
        void setOverride(IntPtr _this, IntPtr pOverride, CORINFO_METHOD_STRUCT_* currentMethod)
        { throw new NotImplementedException(); }
        void addActiveDependency(IntPtr _this, CORINFO_MODULE_STRUCT_* moduleFrom, CORINFO_MODULE_STRUCT_* moduleTo)
        { throw new NotImplementedException(); }
        CORINFO_METHOD_STRUCT_* GetDelegateCtor(IntPtr _this, CORINFO_METHOD_STRUCT_* methHnd, CORINFO_CLASS_STRUCT_* clsHnd, CORINFO_METHOD_STRUCT_* targetMethodHnd, ref DelegateCtorArgs pCtorData)
        { throw new NotImplementedException(); }
        void MethodCompileComplete(IntPtr _this, CORINFO_METHOD_STRUCT_* methHnd)
        { throw new NotImplementedException(); }
        void* getTailCallCopyArgsThunk(IntPtr _this, CORINFO_SIG_INFO* pSig, CorInfoHelperTailCallSpecialHandling flags)
        { throw new NotImplementedException(); }

        delegate IntPtr _ClrVirtualAlloc(IntPtr lpAddress, IntPtr dwSize, uint flAllocationType, uint flProtect);
        static IntPtr ClrVirtualAlloc(IntPtr lpAddress, IntPtr dwSize, uint flAllocationType, uint flProtect)
        {
            return Marshal.AllocCoTaskMem((int)dwSize);
        }
        _ClrVirtualAlloc _clrVirtualAlloc;

        delegate bool _ClrVirtualFree(IntPtr lpAddress, IntPtr dwSize, uint dwFreeType);
        static bool ClrVirtualFree(IntPtr lpAddress, IntPtr dwSize, uint dwFreeType)
        {
            Marshal.FreeCoTaskMem(lpAddress);
            return true;
        }
        _ClrVirtualFree _clrVirtualFree;

        IntPtr _memoryManager;

        void* getMemoryManager(IntPtr _this)
        {
            if (_memoryManager != new IntPtr(0))
                return (void *)_memoryManager;

            int vtableSlots = 14;
            IntPtr* vtable = (IntPtr*)Marshal.AllocCoTaskMem(sizeof(IntPtr) * vtableSlots);
            for (int i = 0; i < vtableSlots; i++) vtable[i] = new IntPtr(0);

            // JIT only ever uses ClrVirtualAlloc/ClrVirtualFree
            vtable[3] = Marshal.GetFunctionPointerForDelegate<_ClrVirtualAlloc>(_clrVirtualAlloc = new _ClrVirtualAlloc(ClrVirtualAlloc));
            vtable[4] = Marshal.GetFunctionPointerForDelegate<_ClrVirtualFree>(_clrVirtualFree = new _ClrVirtualFree(ClrVirtualFree));

            IntPtr instance = Marshal.AllocCoTaskMem(sizeof(IntPtr));
            *(IntPtr**)instance = vtable;

            return (void*)(_memoryManager = instance);
        }

        byte[] _code;
        byte[] _coldCode;
        byte[] _roData;

        void allocMem(IntPtr _this, uint hotCodeSize, uint coldCodeSize, uint roDataSize, uint xcptnsCount, CorJitAllocMemFlag flag, ref void* hotCodeBlock, ref void* coldCodeBlock, ref void* roDataBlock)
        {
            hotCodeBlock = (void *)GetPin(_code = new byte[hotCodeSize]);

            if (coldCodeSize != 0)
                coldCodeBlock = (void *)GetPin(_coldCode = new byte[coldCodeSize]);

            if (roDataSize != 0)
                roDataBlock = (void*)GetPin(_roData = new byte[roDataSize]);
        }

        void reserveUnwindInfo(IntPtr _this, [MarshalAs(UnmanagedType.Bool)]bool isFunclet, [MarshalAs(UnmanagedType.Bool)]bool isColdCode, uint unwindSize)
        {
        }

        void allocUnwindInfo(IntPtr _this, byte* pHotCode, byte* pColdCode, uint startOffset, uint endOffset, uint unwindSize, byte* pUnwindBlock, CorJitFuncKind funcKind)
        {
            // TODO: Unwind Info
        }

        void* allocGCInfo(IntPtr _this, UIntPtr size)
        {
            // TODO: GC Info
            return (void *)GetPin(new byte[(int)size]);
        }

        void yieldExecution(IntPtr _this)
        {
            // Nothing to do
        }

        void setEHcount(IntPtr _this, uint cEH)
        {
            // TODO: EH
        }

        void setEHinfo(IntPtr _this, uint EHnumber, ref CORINFO_EH_CLAUSE clause)
        {
            // TODO: EH
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        bool logMsg(IntPtr _this, uint level, byte* fmt, IntPtr args)
        {
            // Console.WriteLine(Marshal.PtrToStringAnsi((IntPtr)fmt));
            return false;
        }

        int doAssert(IntPtr _this, byte* szFile, int iLine, byte* szExpr)
        {
            Log.WriteLine(Marshal.PtrToStringAnsi((IntPtr)szFile) + ":" + iLine);
            Log.WriteLine(Marshal.PtrToStringAnsi((IntPtr)szExpr));

            return 1;
        }

        void reportFatalError(IntPtr _this, CorJitResult result)
        { throw new NotImplementedException(); }
        HRESULT allocBBProfileBuffer(IntPtr _this, uint count, ref ProfileBuffer* profileBuffer)
        { throw new NotImplementedException(); }
        HRESULT getBBProfileData(IntPtr _this, CORINFO_METHOD_STRUCT_* ftnHnd, ref uint count, ref ProfileBuffer* profileBuffer, ref uint numRuns)
        { throw new NotImplementedException(); }

        void recordCallSite(IntPtr _this, uint instrOffset, CORINFO_SIG_INFO* callSig, CORINFO_METHOD_STRUCT_* methodHandle)
        {
        }

        List<Relocation> _relocs;

        int findKnownBlock(void *location, out int offset)
        {
            fixed (byte * pCode = _code)
            {
                if (pCode <= (byte*)location && (byte*)location < pCode + _code.Length)
                {
                    offset = (int)((byte*)location - pCode);
                    return 0;
                }
            }

            if (_coldCode != null)
            {
                fixed (byte* pColdCode = _coldCode)
                {
                    if (pColdCode <= (byte*)location && (byte*)location < pColdCode + _coldCode.Length)
                    {
                        offset = (int)((byte*)location - pColdCode);
                        return 1;
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
                        return 2;
                    }
                }
            }

            offset = 0;
            return -1;
        }

        void recordRelocation(IntPtr _this, void* location, void* target, ushort fRelocType, ushort slotNum, int addlDelta)
        {
            Relocation reloc;

            reloc.RelocType = fRelocType;

            int locationBlock = findKnownBlock(location, out reloc.Offset);
            Debug.Assert(locationBlock >= 0);
            reloc.Block = (sbyte)locationBlock;

            reloc.Target = HandleToObject((IntPtr)target);
            reloc.Delta = addlDelta;

            if (_relocs == null)
                _relocs = new List<Relocation>(_code.Length / 32 + 1);
            _relocs.Add(reloc);
        }

        ushort getRelocTypeHint(IntPtr _this, void* target)
        {
            // TODO: Hint to use REL32
            return 0xFFFF;
        }
        void getModuleNativeEntryPointRange(IntPtr _this, ref void* pStart, ref void* pEnd)
        { throw new NotImplementedException(); }

        uint getExpectedTargetArchitecture(IntPtr _this)
        {
            return 0x8664; // AMD64
        }
    }
}
