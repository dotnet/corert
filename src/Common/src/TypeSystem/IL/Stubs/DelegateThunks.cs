// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace Internal.IL.Stubs
{
    /// <summary>
    /// Base class for all delegate invocation thunks.
    /// </summary>
    public abstract class DelegateThunk : ILStubMethod
    {
        private DelegateInfo _delegateInfo;

        public DelegateThunk(DelegateInfo delegateInfo)
        {
            _delegateInfo = delegateInfo;
        }

        public sealed override TypeSystemContext Context
        {
            get
            {
                return _delegateInfo.Type.Context;
            }
        }

        public sealed override TypeDesc OwningType
        {
            get
            {
                return _delegateInfo.Type;
            }
        }

        public sealed override MethodSignature Signature
        {
            get
            {
                return _delegateInfo.Signature;
            }
        }

        public sealed override Instantiation Instantiation
        {
            get
            {
                return Instantiation.Empty;
            }
        }

        protected TypeDesc SystemDelegateType
        {
            get
            {
                return Context.GetWellKnownType(WellKnownType.MulticastDelegate).BaseType;
            }
        }

        protected FieldDesc ExtraFunctionPointerOrDataField
        {
            get
            {
                return SystemDelegateType.GetKnownField("m_extraFunctionPointerOrData");
            }
        }

        protected FieldDesc HelperObjectField
        {
            get
            {
                return SystemDelegateType.GetKnownField("m_helperObject");
            }
        }

        protected FieldDesc FirstParameterField
        {
            get
            {
                return SystemDelegateType.GetKnownField("m_firstParameter");
            }
        }

        protected FieldDesc FunctionPointerField
        {
            get
            {
                return SystemDelegateType.GetKnownField("m_functionPointer");
            }
        }
    }

    /// <summary>
    /// Invoke thunk for open delegates to static methods. Loads all arguments except
    /// the 'this' pointer and performs an indirect call to the delegate target.
    /// This method is injected into delegate types.
    /// </summary>
    public sealed class DelegateInvokeOpenStaticThunk : DelegateThunk
    {
        internal DelegateInvokeOpenStaticThunk(DelegateInfo delegateInfo)
            : base(delegateInfo)
        {
        }

        public override MethodIL EmitIL()
        {
            // Target has the same signature as the Invoke method, except it's static.
            MethodSignatureBuilder builder = new MethodSignatureBuilder(Signature);
            builder.Flags = Signature.Flags | MethodSignatureFlags.Static;

            var emitter = new ILEmitter();
            ILCodeStream codeStream = emitter.NewCodeStream();

            // Load all arguments except 'this'
            for (int i = 0; i < Signature.Length; i++)
            {
                codeStream.EmitLdArg(i + 1);
            }

            // Indirectly call the delegate target static method.
            codeStream.EmitLdArg(0);
            codeStream.Emit(ILOpcode.ldfld, emitter.NewToken(ExtraFunctionPointerOrDataField));
            codeStream.Emit(ILOpcode.calli, emitter.NewToken(builder.ToSignature()));
            codeStream.Emit(ILOpcode.ret);

            return emitter.Link(this);
        }

        public override string Name
        {
            get
            {
                return "InvokeOpenStaticThunk";
            }
        }
    }

    /// <summary>
    /// Invoke thunk for closed delegates to static methods. The target
    /// is a static method, but the first argument is captured by the delegate.
    /// The signature of the target has an extra object-typed argument, followed
    /// by the arguments that are delegate-compatible with the thunk signature.
    /// This method is injected into delegate types.
    /// </summary>
    public sealed class DelegateInvokeClosedStaticThunk : DelegateThunk
    {
        internal DelegateInvokeClosedStaticThunk(DelegateInfo delegateInfo)
            : base(delegateInfo)
        {
        }

        public override MethodIL EmitIL()
        {
            TypeDesc[] targetMethodParameters = new TypeDesc[Signature.Length + 1];
            targetMethodParameters[0] = Context.GetWellKnownType(WellKnownType.Object);

            for (int i = 0; i < Signature.Length; i++)
            {
                targetMethodParameters[i + 1] = Signature[i];
            }

            var targetMethodSignature = new MethodSignature(
                Signature.Flags | MethodSignatureFlags.Static, 0, Signature.ReturnType, targetMethodParameters);

            var emitter = new ILEmitter();
            ILCodeStream codeStream = emitter.NewCodeStream();

            // Load the stored 'this'
            codeStream.EmitLdArg(0);
            codeStream.Emit(ILOpcode.ldfld, emitter.NewToken(HelperObjectField));

            // Load all arguments except 'this'
            for (int i = 0; i < Signature.Length; i++)
            {
                codeStream.EmitLdArg(i + 1);
            }

            // Indirectly call the delegate target static method.
            codeStream.EmitLdArg(0);
            codeStream.Emit(ILOpcode.ldfld, emitter.NewToken(ExtraFunctionPointerOrDataField));
            codeStream.Emit(ILOpcode.calli, emitter.NewToken(targetMethodSignature));
            codeStream.Emit(ILOpcode.ret);

            return emitter.Link(this);
        }

        public override string Name
        {
            get
            {
                return "InvokeClosedStaticThunk";
            }
        }
    }

    /// <summary>
    /// Multicast invoke thunk for delegates that are a result of Delegate.Combine.
    /// Passes it's arguments to each of the delegates that got combined and calls them
    /// one by one. Returns the value of the last delegate executed.
    /// This method is injected into delegate types.
    /// </summary>
    public sealed class DelegateInvokeMulticastThunk : DelegateThunk
    {
        internal DelegateInvokeMulticastThunk(DelegateInfo delegateInfo)
            : base(delegateInfo)
        {
        }

        public override MethodIL EmitIL()
        {
            ILEmitter emitter = new ILEmitter();
            ILCodeStream codeStream = emitter.NewCodeStream();

            ArrayType invocationListArrayType = SystemDelegateType.MakeArrayType();

            ILLocalVariable delegateArrayLocal = emitter.NewLocal(invocationListArrayType);
            ILLocalVariable invocationCountLocal = emitter.NewLocal(Context.GetWellKnownType(WellKnownType.Int32));
            ILLocalVariable iteratorLocal = emitter.NewLocal(Context.GetWellKnownType(WellKnownType.Int32));
            ILLocalVariable delegateToCallLocal = emitter.NewLocal(SystemDelegateType);

            ILLocalVariable returnValueLocal = 0;
            if (Signature.ReturnType is SignatureVariable || !Signature.ReturnType.IsVoid)
            {
                returnValueLocal = emitter.NewLocal(Signature.ReturnType);
            }

            // Fill in delegateArrayLocal
            // Delegate[] delegateArrayLocal = (Delegate[])this.m_helperObject
            
            // ldarg.0 (this pointer)
            // ldfld Delegate.HelperObjectField
            // castclass Delegate[]
            // stloc delegateArrayLocal
            codeStream.EmitLdArg(0);
            codeStream.Emit(ILOpcode.ldfld, emitter.NewToken(HelperObjectField));
            codeStream.Emit(ILOpcode.castclass, emitter.NewToken(invocationListArrayType));
            codeStream.EmitStLoc(delegateArrayLocal);

            // Fill in invocationCountLocal
            // int invocationCountLocal = this.m_extraFunctionPointerOrData
            // ldarg.0 (this pointer)
            // ldfld Delegate.m_extraFunctionPointerOrData
            // stloc invocationCountLocal
            codeStream.EmitLdArg(0);
            codeStream.Emit(ILOpcode.ldfld, emitter.NewToken(ExtraFunctionPointerOrDataField));
            codeStream.EmitStLoc(invocationCountLocal);

            // Fill in iteratorLocal
            // int iteratorLocal = 0;

            // ldc.0
            // stloc iteratorLocal
            codeStream.EmitLdc(0);
            codeStream.EmitStLoc(iteratorLocal);

            // Loop across every element of the array. 
            ILCodeLabel startOfLoopLabel = emitter.NewCodeLabel();
            codeStream.EmitLabel(startOfLoopLabel);

            // Implement as do/while loop. We only have this stub in play if we're in the multicast situation
            // Find the delegate to call
            // Delegate = delegateToCallLocal = delegateArrayLocal[iteratorLocal];

            // ldloc delegateArrayLocal
            // ldloc iteratorLocal
            // ldelem System.Delegate
            // stloc delegateToCallLocal
            codeStream.EmitLdLoc(delegateArrayLocal);
            codeStream.EmitLdLoc(iteratorLocal);
            codeStream.Emit(ILOpcode.ldelem, emitter.NewToken(SystemDelegateType));
            codeStream.EmitStLoc(delegateToCallLocal);

            // Call the delegate
            // returnValueLocal = delegateToCallLocal(...);

            // ldloc delegateToCallLocal
            // ldfld System.Delegate.m_firstParameter
            // ldarg 1, n
            // ldloc delegateToCallLocal
            // ldfld System.Delegate.m_functionPointer
            // calli returnValueType thiscall (all the params)
            // IF there is a return value
            // stloc returnValueLocal

            codeStream.EmitLdLoc(delegateToCallLocal);
            codeStream.Emit(ILOpcode.ldfld, emitter.NewToken(FirstParameterField));

            for (int i = 0; i < Signature.Length; i++)
            {
                codeStream.EmitLdArg(i + 1);
            }

            codeStream.EmitLdLoc(delegateToCallLocal);
            codeStream.Emit(ILOpcode.ldfld, emitter.NewToken(FunctionPointerField));

            codeStream.Emit(ILOpcode.calli, emitter.NewToken(Signature));

            if (returnValueLocal != 0)
                codeStream.EmitStLoc(returnValueLocal);

            // Increment iteratorLocal
            // ++iteratorLocal;

            // ldloc iteratorLocal
            // ldc.i4.1
            // add
            // stloc iteratorLocal
            codeStream.EmitLdLoc(iteratorLocal);
            codeStream.EmitLdc(1);
            codeStream.Emit(ILOpcode.add);
            codeStream.EmitStLoc(iteratorLocal);

            // Check to see if the loop is done
            codeStream.EmitLdLoc(invocationCountLocal);
            codeStream.EmitLdLoc(iteratorLocal);
            codeStream.Emit(ILOpcode.bne_un, startOfLoopLabel);

            // Return to caller. If the delegate has a return value, be certain to return that.
            // return returnValueLocal;

            // ldloc returnValueLocal
            // ret
            if (returnValueLocal != 0)
                codeStream.EmitLdLoc(returnValueLocal);

            codeStream.Emit(ILOpcode.ret);

            return emitter.Link(this);
        }

        public override string Name
        {
            get
            {
                return "InvokeMulticastThunk";
            }
        }
    }

    /// <summary>
    /// Synthetic method override of "IntPtr Delegate.GetThunk(Int32)". This method is injected
    /// into all delegate types and provides means for System.Delegate to access the various thunks
    /// generated by the compiler.
    /// </summary>
    public sealed class DelegateGetThunkMethodOverride : ILStubMethod
    {
        private DelegateInfo _delegateInfo;
        private MethodSignature _signature;

        internal DelegateGetThunkMethodOverride(DelegateInfo delegateInfo)
        {
            _delegateInfo = delegateInfo;
        }

        public override TypeSystemContext Context
        {
            get
            {
                return _delegateInfo.Type.Context;
            }
        }

        public override TypeDesc OwningType
        {
            get
            {
                return _delegateInfo.Type;
            }
        }

        public override MethodSignature Signature
        {
            get
            {
                if (_signature == null)
                {
                    TypeSystemContext context = _delegateInfo.Type.Context;
                    TypeDesc intPtrType = context.GetWellKnownType(WellKnownType.IntPtr);
                    TypeDesc int32Type = context.GetWellKnownType(WellKnownType.Int32);

                    _signature = new MethodSignature(0, 0, intPtrType, new[] { int32Type });
                }

                return _signature;
            }
        }

        public override MethodIL EmitIL()
        {
            const DelegateThunkKind maxThunkKind = DelegateThunkKind.ObjectArrayThunk;

            ILEmitter emitter = new ILEmitter();

            var codeStream = emitter.NewCodeStream();

            ILCodeLabel returnNullLabel = emitter.NewCodeLabel();

            ILCodeLabel[] labels = new ILCodeLabel[(int)maxThunkKind];
            for (DelegateThunkKind i = 0; i < maxThunkKind; i++)
            {
                MethodDesc thunk = _delegateInfo.Thunks[i];
                if (thunk != null)
                    labels[(int)i] = emitter.NewCodeLabel();
                else
                    labels[(int)i] = returnNullLabel;
            }

            codeStream.EmitLdArg(1);
            codeStream.EmitSwitch(labels);

            codeStream.Emit(ILOpcode.br, returnNullLabel);

            for (DelegateThunkKind i = 0; i < maxThunkKind; i++)
            {
                MethodDesc thunk = _delegateInfo.Thunks[i];
                if (thunk != null)
                {
                    codeStream.EmitLabel(labels[(int)i]);

                    codeStream.Emit(ILOpcode.ldftn, emitter.NewToken(thunk.InstantiateAsOpen()));
                    codeStream.Emit(ILOpcode.ret);
                }
            }

            codeStream.EmitLabel(returnNullLabel);
            codeStream.EmitLdc(0);
            codeStream.Emit(ILOpcode.conv_i);
            codeStream.Emit(ILOpcode.ret);

            return emitter.Link(this);
        }

        public override Instantiation Instantiation
        {
            get
            {
                return Instantiation.Empty;
            }
        }

        public override bool IsVirtual
        {
            get
            {
                return true;
            }
        }

        public override string Name
        {
            get
            {
                return "GetThunk";
            }
        }
    }
}
