// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace Internal.IL.Stubs
{
    /// <summary>
    /// Provides implementation for the various intrinsics on System.InvokeUtils.
    /// These are general dynamic invocation helpers. See System.InvokeUtils for description.
    /// </summary>
    public struct InvokeUtilsIntrinsics
    {
        private TypeSystemContext _context;

        private InvokeUtilsIntrinsics(TypeSystemContext context)
        {
            _context = context;
        }

        private DefType IntPtrType
        {
            get
            {
                return _context.GetWellKnownType(WellKnownType.IntPtr);
            }
        }

        private DefType ObjectType
        {
            get
            {
                return _context.GetWellKnownType(WellKnownType.Object);
            }
        }

        private FieldDesc ObjectEETypeField
        {
            get
            {
                return ObjectType.GetKnownField("m_pEEType");
            }
        }

        private MethodDesc ObjectGetEETypePtrMethod
        {
            get
            {
                return ObjectType.GetKnownMethod("get_EETypePtr", null);
            }
        }

        private TypeDesc EETypePtrType
        {
            get
            {
                return _context.SystemModule.GetKnownType("System", "EETypePtr");
            }
        }

        private MetadataType RuntimeImportsType
        {
            get
            {
                return _context.SystemModule.GetKnownType("System.Runtime", "RuntimeImports");
            }
        }

        public static MethodIL EmitIL(MethodDesc method)
        {
            Debug.Assert(((MetadataType)method.OwningType).Name == "InvokeUtils");

            var instance = new InvokeUtilsIntrinsics(method.Context);

            switch (method.Name)
            {
                case "DynamicInvokeUnboxIntoActualNullable":
                    return instance.EmitDynamicInvokeUnboxIntoActualNullable(method);

                case "DynamicInvokeBoxIntoNonNullable":
                    return instance.EmitDynamicInvokeBoxIntoNonNullable(method);

                case "CallIHelperThisCall":
                    return instance.EmitCallIHelperThisCall(method);

                case "CallIHelperStaticCall":
                    return instance.EmitCallIHelperStaticCall(method);

                case "CallIHelperStaticCallWithInstantiation":
                    return instance.EmitCallIHelperStaticCallWithInstantiation(method);

                default:
                    Debug.Fail("Unknown method: " + method.ToString());
                    return null;
            }
        }

        private MethodIL EmitDynamicInvokeUnboxIntoActualNullable(MethodDesc method)
        {
            Debug.Assert(
                method.Signature.IsStatic &&
                method.Signature.ReturnType.IsVoid &&
                method.Signature.Length == 3 &&
                method.Signature[0] == ObjectType &&
                method.Signature[1] == ObjectType &&
                method.Signature[2] == EETypePtrType);

            ILEmitter emit = new ILEmitter();
            ILCodeStream codeStream = emit.NewCodeStream();

            ILLocalVariable pinnedActualBoxedNullable = emit.NewLocal(ObjectType, true);

            codeStream.EmitLdArg(0);
            codeStream.EmitStLoc(pinnedActualBoxedNullable);
            codeStream.EmitLdArg(1);
            codeStream.EmitLdArg(0);
            codeStream.Emit(ILOpcode.ldflda, emit.NewToken(ObjectEETypeField));
            codeStream.Emit(ILOpcode.sizeof_, emit.NewToken(_context.GetWellKnownType(WellKnownType.IntPtr)));
            codeStream.Emit(ILOpcode.add);
            codeStream.EmitLdArg(2);
            codeStream.Emit(ILOpcode.call, emit.NewToken(RuntimeImportsType.GetKnownMethod("RhUnbox", null)));
            codeStream.Emit(ILOpcode.ret);

            return emit.Link(method);
        }

        private MethodIL EmitDynamicInvokeBoxIntoNonNullable(MethodDesc method)
        {
            Debug.Assert(
                method.Signature.IsStatic &&
                method.Signature.ReturnType == ObjectType &&
                method.Signature.Length == 1 &&
                method.Signature[0] == ObjectType);

            ILEmitter emit = new ILEmitter();
            ILCodeStream codeStream = emit.NewCodeStream();

            ILLocalVariable pinnedActualBoxedNullable = emit.NewLocal(ObjectType, true);

            codeStream.EmitLdArg(0);
            codeStream.EmitStLoc(pinnedActualBoxedNullable);
            codeStream.EmitLdArg(0);
            codeStream.Emit(ILOpcode.call, emit.NewToken(ObjectGetEETypePtrMethod));
            codeStream.EmitLdArg(0);
            codeStream.Emit(ILOpcode.ldflda, emit.NewToken(ObjectEETypeField));
            codeStream.Emit(ILOpcode.sizeof_, emit.NewToken(_context.GetWellKnownType(WellKnownType.IntPtr)));
            codeStream.Emit(ILOpcode.add);
            codeStream.Emit(ILOpcode.call, emit.NewToken(RuntimeImportsType.GetKnownMethod("RhBox", null)));
            codeStream.Emit(ILOpcode.ret);

            return emit.Link(method);
        }

        private MethodIL EmitCallIHelperThisCall(MethodDesc method)
        {
            Debug.Assert(method.Signature.IsStatic &&
                method.Signature.Length == 5 &&
                method.Signature.ReturnType == ObjectType &&
                method.Signature[0] == ObjectType &&
                method.Signature[1] == IntPtrType &&
                method.Signature[2] == ObjectType &&
                method.Signature[3] == IntPtrType);

            TypeDesc refToArgSetupStateType = method.Signature[4];
            Debug.Assert(refToArgSetupStateType.IsByRef &&
                ((MetadataType)((ByRefType)refToArgSetupStateType).ParameterType).Name == "ArgSetupState");

            ILEmitter emit = new ILEmitter();
            ILCodeStream codeStream = emit.NewCodeStream();

            MethodSignature targetSig = new MethodSignature(0, 0,
                ObjectType,
                new TypeDesc[] {
                    ObjectType,
                    IntPtrType,
                    refToArgSetupStateType
                });

            codeStream.EmitLdArg(2);
            codeStream.EmitLdArg(0);
            codeStream.EmitLdArg(1);
            codeStream.EmitLdArg(4);
            codeStream.EmitLdArg(3);
            codeStream.Emit(ILOpcode.calli, emit.NewToken(targetSig));
            codeStream.Emit(ILOpcode.ret);

            return emit.Link(method);
        }

        private MethodIL EmitCallIHelperStaticCall(MethodDesc method)
        {
            Debug.Assert(method.Signature.IsStatic &&
                method.Signature.Length == 5 &&
                method.Signature.ReturnType == ObjectType &&
                method.Signature[0] == ObjectType &&
                method.Signature[1] == IntPtrType &&
                method.Signature[2] == IntPtrType &&
                method.Signature[4].IsWellKnownType(WellKnownType.Boolean));

            TypeDesc refToArgSetupStateType = method.Signature[3];
            Debug.Assert(refToArgSetupStateType.IsByRef &&
                ((MetadataType)((ByRefType)refToArgSetupStateType).ParameterType).Name == "ArgSetupState");

            ILEmitter emit = new ILEmitter();
            ILCodeStream codeStream = emit.NewCodeStream();

            MethodSignature targetSig = new MethodSignature(MethodSignatureFlags.Static, 0,
                ObjectType,
                new TypeDesc[] {
                    ObjectType,
                    IntPtrType,
                    refToArgSetupStateType,
                    _context.GetWellKnownType(WellKnownType.Boolean)
                });

            codeStream.EmitLdArg(0);
            codeStream.EmitLdArg(1);
            codeStream.EmitLdArg(3);
            codeStream.EmitLdArg(4);
            codeStream.EmitLdArg(2);
            codeStream.Emit(ILOpcode.calli, emit.NewToken(targetSig));
            codeStream.Emit(ILOpcode.ret);

            return emit.Link(method);
        }

        private MethodIL EmitCallIHelperStaticCallWithInstantiation(MethodDesc method)
        {
            Debug.Assert(method.Signature.IsStatic &&
                method.Signature.Length == 6 &&
                method.Signature.ReturnType == ObjectType &&
                method.Signature[0] == ObjectType &&
                method.Signature[1] == IntPtrType &&
                method.Signature[2] == IntPtrType &&
                method.Signature[4].IsWellKnownType(WellKnownType.Boolean) &&
                method.Signature[5] == IntPtrType);

            TypeDesc refToArgSetupStateType = method.Signature[3];
            Debug.Assert(refToArgSetupStateType.IsByRef &&
                ((MetadataType)((ByRefType)refToArgSetupStateType).ParameterType).Name == "ArgSetupState");

            ILEmitter emit = new ILEmitter();
            ILCodeStream codeStream = emit.NewCodeStream();

            MethodSignature targetSig = new MethodSignature(MethodSignatureFlags.Static, 0,
                ObjectType,
                new TypeDesc[] {
                    IntPtrType,
                    ObjectType,
                    IntPtrType,
                    refToArgSetupStateType,
                    _context.GetWellKnownType(WellKnownType.Boolean)
                });

            codeStream.EmitLdArg(5);
            codeStream.EmitLdArg(0);
            codeStream.EmitLdArg(1);
            codeStream.EmitLdArg(3);
            codeStream.EmitLdArg(4);
            codeStream.EmitLdArg(2);
            codeStream.Emit(ILOpcode.calli, emit.NewToken(targetSig));
            codeStream.Emit(ILOpcode.ret);

            return emit.Link(method);
        }
    }

    // TODO: once C# supports ByRef returns, make InvokeUtils.DynamicInvokeParamHelperIn and
    // InvokeUtils.DynamicInvokeParamHelperRef just return that and make this a regular intrinsic
    public class DynamicInvokeParamHelperMethod : ILStubMethod
    {
        private TypeDesc _owningType;

        // TODO: move DynamicInvokeParamType enum into Common and type it as such here
        private int _paramType;

        private MethodSignature _signature;

        public DynamicInvokeParamHelperMethod(TypeDesc owningType, int paramType)
        {
            _owningType = owningType;
            _paramType = paramType;
        }

        public override TypeSystemContext Context
        {
            get
            {
                return _owningType.Context;
            }
        }

        public override bool IsNoInlining
        {
            get
            {
                return true;
            }
        }

        public override TypeDesc OwningType
        {
            get
            {
                return _owningType;
            }
        }

        public override string Name
        {
            get
            {
                return "DynamicInvokeParamHelper" + (_paramType == 0 ? "In" : "Ref");
            }
        }

        public override MethodSignature Signature
        {
            get
            {
                if (_signature == null)
                {
                    _signature = new MethodSignature(MethodSignatureFlags.Static,
                        0,
                        Context.GetWellKnownType(WellKnownType.IntPtr).MakeByRefType(),
                        new TypeDesc[] { Context.GetWellKnownType(WellKnownType.RuntimeTypeHandle) });
                }

                return _signature;
            }
        }

        public override MethodIL EmitIL()
        {
            ILEmitter emit = new ILEmitter();
            ILCodeStream codeStream = emit.NewCodeStream();

            ILLocalVariable index = emit.NewLocal(Context.GetWellKnownType(WellKnownType.Int32));
            ILLocalVariable lookupType = emit.NewLocal(Context.GetWellKnownType(WellKnownType.Int32));
            ILLocalVariable foundObject = emit.NewLocal(Context.GetWellKnownType(WellKnownType.Object));

            ILCodeLabel label = emit.NewCodeLabel();

            codeStream.EmitLdArg(0);
            codeStream.EmitLdLoca(lookupType);
            codeStream.EmitLdLoca(index);
            codeStream.EmitLdc(_paramType);
            codeStream.Emit(ILOpcode.call,
                emit.NewToken(Context.SystemModule.GetKnownType("System", "InvokeUtils").GetKnownMethod("DynamicInvokeParamHelperCore", null)));
            codeStream.EmitStLoc(foundObject);
            codeStream.EmitLdLoc(lookupType);
            codeStream.EmitLdc(0);
            codeStream.Emit(ILOpcode.bne_un, label);

            codeStream.EmitLdLoc(foundObject);
            codeStream.Emit(ILOpcode.ldflda,
                emit.NewToken(Context.GetWellKnownType(WellKnownType.Object).GetKnownField("m_pEEType")));
            codeStream.EmitLdc(Context.Target.PointerSize);
            codeStream.Emit(ILOpcode.add);
            codeStream.Emit(ILOpcode.ret);

            codeStream.EmitLabel(label);
            codeStream.EmitLdLoc(foundObject);
            codeStream.EmitLdLoc(index);
            codeStream.Emit(ILOpcode.ldelema,
                emit.NewToken(Context.GetWellKnownType(WellKnownType.Object)));
            codeStream.Emit(ILOpcode.ret);

            return emit.Link(this);
        }
    }
}
