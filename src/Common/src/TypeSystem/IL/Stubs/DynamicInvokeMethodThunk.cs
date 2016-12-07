// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;
using Interlocked = System.Threading.Interlocked;

namespace Internal.IL.Stubs
{
    /// <summary>
    /// Thunk to dynamically invoke a method using reflection. The method accepts an object[] of parameters
    /// to target method, lays them out on the stack, and calls the target method. This thunk has heavy
    /// dependencies on the general dynamic invocation infrastructure in System.InvokeUtils and gets called from there
    /// at runtime. See comments in System.InvokeUtils for a more thorough explanation.
    /// </summary>
    internal class DynamicInvokeMethodThunk : ILStubMethod
    {
        private TypeDesc _owningType;
        private DynamicInvokeMethodSignature _targetSignature;

        private TypeDesc[] _instantiation;
        private MethodSignature _signature;

        public DynamicInvokeMethodThunk(TypeDesc owningType, DynamicInvokeMethodSignature signature)
        {
            _owningType = owningType;
            _targetSignature = signature;
        }

        public override TypeSystemContext Context
        {
            get
            {
                return _owningType.Context;
            }
        }

        public override TypeDesc OwningType
        {
            get
            {
                return _owningType;
            }
        }

        private MetadataType InvokeUtilsType
        {
            get
            {
                return Context.SystemModule.GetKnownType("System", "InvokeUtils");
            }
        }

        private MetadataType ArgSetupStateType
        {
            get
            {
                return InvokeUtilsType.GetNestedType("ArgSetupState");
            }
        }

        public override MethodSignature Signature
        {
            get
            {
                if (_signature == null)
                {
                    _signature = new MethodSignature(
                        MethodSignatureFlags.Static,
                        Instantiation.Length,
                        Context.GetWellKnownType(WellKnownType.Object),
                        new TypeDesc[]
                        {
                            Context.GetWellKnownType(WellKnownType.Object),  // thisPtr
                            Context.GetWellKnownType(WellKnownType.IntPtr),  // methodToCall
                            ArgSetupStateType.MakeByRefType(),               // argSetupState
                            Context.GetWellKnownType(WellKnownType.Boolean), // targetIsThisCall
                        });
                }

                return _signature;
            }
        }

        public override Instantiation Instantiation
        {
            get
            {
                if (_instantiation == null)
                {
                    TypeDesc[] instantiation =
                        new TypeDesc[_targetSignature.HasReturnValue ? _targetSignature.Length + 1 : _targetSignature.Length];

                    for (int i = 0; i < _targetSignature.Length; i++)
                        instantiation[i] = new DynamicInvokeThunkGenericParameter(Context, i);

                    if (_targetSignature.HasReturnValue)
                        instantiation[_targetSignature.Length] =
                            new DynamicInvokeThunkGenericParameter(Context, _targetSignature.Length);

                    Interlocked.CompareExchange(ref _instantiation, instantiation, null);
                }

                return new Instantiation(_instantiation);
            }
        }

        public override string Name
        {
            get
            {
                StringBuilder sb = new StringBuilder("InvokeRet");

                if (_targetSignature.HasReturnValue)
                    sb.Append('O');
                else
                    sb.Append('V');

                for (int i = 0; i < _targetSignature.Length; i++)
                    sb.Append(_targetSignature[i] == DynamicInvokeMethodParameterKind.Value ? 'I' : 'R');

                return sb.ToString();
            }
        }

        public override MethodIL EmitIL()
        {
            ILEmitter emitter = new ILEmitter();
            ILCodeStream argSetupStream = emitter.NewCodeStream();
            ILCodeStream thisCallSiteSetupStream = emitter.NewCodeStream();
            ILCodeStream staticCallSiteSetupStream = emitter.NewCodeStream();

            // This function will look like
            //
            // !For each parameter to the method
            //    !if (parameter is In Parameter)
            //       localX is TypeOfParameterX&
            //       ldtoken TypeOfParameterX
            //       call DynamicInvokeParamHelperIn(RuntimeTypeHandle)
            //       stloc localX
            //    !else
            //       localX is TypeOfParameter
            //       ldtoken TypeOfParameterX
            //       call DynamicInvokeParamHelperRef(RuntimeTypeHandle)
            //       stloc localX

            // ldarg.2
            // call DynamicInvokeArgSetupComplete(ref ArgSetupState)

            // *** Thiscall instruction stream starts here ***

            // ldarg.3 // Load targetIsThisCall
            // brfalse Not_this_call

            // ldarg.0 // Load this pointer
            // !For each parameter
            //    !if (parameter is In Parameter)
            //       ldloc localX
            //       ldobj TypeOfParameterX
            //    !else
            //       ldloc localX
            // ldarg.1
            // calli ReturnType thiscall(TypeOfParameter1, ...)
            // !if ((ReturnType == void)
            //    ldnull
            // !else
            //    box ReturnType
            // ret

            // *** Static call instruction stream starts here ***

            // Not_this_call:
            // !For each parameter
            //    !if (parameter is In Parameter)
            //       ldloc localX
            //       ldobj TypeOfParameterX
            //    !else
            //       ldloc localX
            // ldarg.1
            // calli ReturnType (TypeOfParameter1, ...)
            // !if ((ReturnType == void)
            //    ldnull
            // !else
            //    box ReturnType
            // ret

            ILCodeLabel lStaticCall = emitter.NewCodeLabel();
            thisCallSiteSetupStream.EmitLdArg(3); // targetIsThisCall
            thisCallSiteSetupStream.Emit(ILOpcode.brfalse, lStaticCall);
            staticCallSiteSetupStream.EmitLabel(lStaticCall);

            thisCallSiteSetupStream.EmitLdArg(0); // thisPtr

            ILToken tokDynamicInvokeParamHelperRef =
                emitter.NewToken(InvokeUtilsType.GetKnownMethod("DynamicInvokeParamHelperRef", null));
            ILToken tokDynamicInvokeParamHelperIn =
                emitter.NewToken(InvokeUtilsType.GetKnownMethod("DynamicInvokeParamHelperIn", null));

            TypeDesc[] targetMethodSignature = new TypeDesc[_targetSignature.Length];

            for (int paramIndex = 0; paramIndex < _targetSignature.Length; paramIndex++)
            {
                TypeDesc paramType = Context.GetSignatureVariable(paramIndex, true);
                ILToken tokParamType = emitter.NewToken(paramType);
                ILLocalVariable local = emitter.NewLocal(paramType.MakeByRefType());

                thisCallSiteSetupStream.EmitLdLoc(local);
                staticCallSiteSetupStream.EmitLdLoc(local);

                argSetupStream.Emit(ILOpcode.ldtoken, tokParamType);

                if (_targetSignature[paramIndex] == DynamicInvokeMethodParameterKind.Reference)
                {
                    argSetupStream.Emit(ILOpcode.call, tokDynamicInvokeParamHelperRef);

                    targetMethodSignature[paramIndex] = paramType.MakeByRefType();
                }
                else
                {
                    argSetupStream.Emit(ILOpcode.call, tokDynamicInvokeParamHelperIn);

                    thisCallSiteSetupStream.Emit(ILOpcode.ldobj, tokParamType);
                    staticCallSiteSetupStream.Emit(ILOpcode.ldobj, tokParamType);

                    targetMethodSignature[paramIndex] = paramType;
                }
                argSetupStream.EmitStLoc(local);
            }

            argSetupStream.EmitLdArg(2); // argSetupState
            argSetupStream.Emit(ILOpcode.call, emitter.NewToken(InvokeUtilsType.GetKnownMethod("DynamicInvokeArgSetupComplete", null)));

            thisCallSiteSetupStream.EmitLdArg(1); // methodToCall
            staticCallSiteSetupStream.EmitLdArg(1); // methodToCall

            TypeDesc returnType = _targetSignature.HasReturnValue ?
                Context.GetSignatureVariable(_targetSignature.Length, true) :
                Context.GetWellKnownType(WellKnownType.Void);

            MethodSignature thisCallMethodSig = new MethodSignature(0, 0, returnType, targetMethodSignature);
            thisCallSiteSetupStream.Emit(ILOpcode.calli, emitter.NewToken(thisCallMethodSig));

            MethodSignature staticCallMethodSig = new MethodSignature(MethodSignatureFlags.Static, 0, returnType, targetMethodSignature);
            staticCallSiteSetupStream.Emit(ILOpcode.calli, emitter.NewToken(staticCallMethodSig));

            if (_targetSignature.HasReturnValue)
            {
                ILToken tokReturnType = emitter.NewToken(returnType);
                thisCallSiteSetupStream.Emit(ILOpcode.box, tokReturnType);
                staticCallSiteSetupStream.Emit(ILOpcode.box, tokReturnType);
            }
            else
            {
                thisCallSiteSetupStream.Emit(ILOpcode.ldnull);
                staticCallSiteSetupStream.Emit(ILOpcode.ldnull);
            }

            thisCallSiteSetupStream.Emit(ILOpcode.ret);
            staticCallSiteSetupStream.Emit(ILOpcode.ret);

            return emitter.Link(this);
        }

        private class DynamicInvokeThunkGenericParameter : GenericParameterDesc
        {
            public DynamicInvokeThunkGenericParameter(TypeSystemContext context, int index)
            {
                Context = context;
                Index = index;
            }

            public override TypeSystemContext Context
            {
                get;
            }

            public override int Index
            {
                get;
            }

            public override GenericParameterKind Kind
            {
                get
                {
                    return GenericParameterKind.Method;
                }
            }
        }
    }

    internal enum DynamicInvokeMethodParameterKind
    {
        None,
        Value,
        Reference,
    }

    /// <summary>
    /// Wraps a <see cref="MethodSignature"/> to reduce it's fidelity.
    /// </summary>
    internal struct DynamicInvokeMethodSignature : IEquatable<DynamicInvokeMethodSignature>
    {
        private MethodSignature _signature;

        public bool HasReturnValue
        {
            get
            {
                return !_signature.ReturnType.IsVoid;
            }
        }

        public int Length
        {
            get
            {
                return _signature.Length;
            }
        }

        public DynamicInvokeMethodParameterKind this[int index]
        {
            get
            {
                return _signature[index].IsByRef ?
                    DynamicInvokeMethodParameterKind.Reference :
                    DynamicInvokeMethodParameterKind.Value;
            }
        }

        public DynamicInvokeMethodSignature(MethodSignature concreteSignature)
        {
            // ByRef returns should have been filtered out elsewhere. We don't handle them
            // because reflection can't invoke such methods.
            Debug.Assert(!concreteSignature.ReturnType.IsByRef);
            _signature = concreteSignature;
        }

        public override bool Equals(object obj)
        {
            return obj is DynamicInvokeMethodSignature && Equals((DynamicInvokeMethodSignature)obj);
        }

        public override int GetHashCode()
        {
            int hashCode = HasReturnValue ? 17 : 23;

            for (int i = 0; i < Length; i++)
            {
                int value = (int)this[i] * 0x5498341 + 0x832424;
                hashCode = hashCode * 31 + value;
            }

            return hashCode;
        }

        public bool Equals(DynamicInvokeMethodSignature other)
        {
            if (HasReturnValue != other.HasReturnValue)
                return false;
            
            if (Length != other.Length)
                return false;

            for (int i = 0; i < Length; i++)
            {
                if (this[i] != other[i])
                    return false;
            }

            return true;
        }
    }
}
