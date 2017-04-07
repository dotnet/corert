// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;
using Internal.TypeSystem.Interop;
using Debug = System.Diagnostics.Debug;
using Internal.TypeSystem.Ecma;

namespace Internal.IL.Stubs
{
    /// <summary>
    /// Provides method bodies for PInvoke methods
    /// 
    /// This by no means intends to provide full PInvoke support. The intended use of this is to
    /// a) prevent calls getting generated to targets that require a full marshaller
    /// (this compiler doesn't provide that), and b) offer a hand in some very simple marshalling
    /// situations (but support for this part might go away as the product matures).
    /// </summary>
    public struct PInvokeILEmitter
    {
        private readonly MethodDesc _targetMethod;
        private readonly Marshaller[] _marshallers;
        private readonly PInvokeILEmitterConfiguration _pInvokeILEmitterConfiguration;
        private readonly PInvokeMetadata _importMetadata;
        private readonly InteropStateManager _interopStateManager;

        private PInvokeILEmitter(MethodDesc targetMethod, PInvokeILEmitterConfiguration pinvokeILEmitterConfiguration, InteropStateManager interopStateManager)
        {
            Debug.Assert(targetMethod.IsPInvoke || targetMethod is DelegateMarshallingMethodThunk);
            _targetMethod = targetMethod;
            _pInvokeILEmitterConfiguration = pinvokeILEmitterConfiguration;
            _importMetadata = targetMethod.GetPInvokeMethodMetadata();
            _interopStateManager = interopStateManager;

            PInvokeFlags flags = new PInvokeFlags();
            if (targetMethod.IsPInvoke)
            {
                flags = _importMetadata.Flags;
            }
            else 
            {
                var delegateType = ((DelegateMarshallingMethodThunk)_targetMethod).DelegateType as EcmaType;
                if (delegateType != null)
                {
                    flags = delegateType.GetDelegatePInvokeFlags();
                }
            }
            
            _marshallers = InitializeMarshallers(targetMethod, interopStateManager, flags);
        }

        private static Marshaller[] InitializeMarshallers(MethodDesc targetMethod, InteropStateManager interopStateManager, PInvokeFlags flags)
        {
            bool isDelegate = targetMethod is DelegateMarshallingMethodThunk;
            MethodSignature methodSig = isDelegate ? ((DelegateMarshallingMethodThunk)targetMethod).DelegateSignature : targetMethod.Signature;
            ParameterMetadata[] parameterMetadataArray = targetMethod.GetParameterMetadata();
            Marshaller[] marshallers = new Marshaller[methodSig.Length + 1];
            int parameterIndex = 0;
            ParameterMetadata parameterMetadata;

            for (int i = 0; i < marshallers.Length; i++)
            {
                Debug.Assert(parameterIndex == parameterMetadataArray.Length || i <= parameterMetadataArray[parameterIndex].Index);
                if (parameterIndex == parameterMetadataArray.Length || i < parameterMetadataArray[parameterIndex].Index)
                {
                    // if we don't have metadata for the parameter, create a dummy one
                    parameterMetadata = new ParameterMetadata(i, ParameterMetadataAttributes.None, null);
                }
                else 
                {
                    Debug.Assert(i == parameterMetadataArray[parameterIndex].Index);
                    parameterMetadata = parameterMetadataArray[parameterIndex++];
                }
                TypeDesc parameterType = (i == 0) ? methodSig.ReturnType : methodSig[i - 1];  //first item is the return type
                marshallers[i] = Marshaller.CreateMarshaller(parameterType,
                                                    MarshallerType.Argument,
                                                    parameterMetadata.MarshalAsDescriptor,
                                                    isDelegate ? MarshalDirection.Reverse : MarshalDirection.Forward,
                                                    marshallers,
                                                    interopStateManager,
                                                    parameterMetadata.Index,
                                                    flags,
                                                    parameterMetadata.In,
                                                    parameterMetadata.Out,
                                                    parameterMetadata.Return
                                                    );
            }

            return marshallers;
        }

        private MethodIL EmitIL()
        {
            PInvokeILCodeStreams pInvokeILCodeStreams = new PInvokeILCodeStreams();
            ILEmitter emitter = pInvokeILCodeStreams.Emitter;
            ILCodeStream fnptrLoadStream = pInvokeILCodeStreams.FunctionPointerLoadStream;
            ILCodeStream callsiteSetupCodeStream = pInvokeILCodeStreams.CallsiteSetupCodeStream;
            ILCodeStream unmarshallingCodestream = pInvokeILCodeStreams.UnmarshallingCodestream;
            TypeSystemContext context = _targetMethod.Context;

            // Marshal the arguments
            for (int i = 0; i < _marshallers.Length; i++)
            {
                _marshallers[i].EmitMarshallingIL(pInvokeILCodeStreams);
            }

            // make the call
            TypeDesc nativeReturnType = _marshallers[0].NativeParameterType;
            TypeDesc[] nativeParameterTypes = new TypeDesc[_marshallers.Length - 1];

            for (int i = 1; i < _marshallers.Length; i++)
            {
                nativeParameterTypes[i - 1] = _marshallers[i].NativeParameterType;
            }

            MethodSignature nativeSig;
            // if the SetLastError flag is set in DllImport, clear the error code before doing P/Invoke 
            if (_importMetadata.Flags.SetLastError)
            {
                callsiteSetupCodeStream.Emit(ILOpcode.call, emitter.NewToken(
                            InteropTypes.GetPInvokeMarshal(context).GetKnownMethod("ClearLastWin32Error", null)));
            }

            DelegateMarshallingMethodThunk delegateMethod = _targetMethod as DelegateMarshallingMethodThunk;
            if (delegateMethod != null)
            {
                if (delegateMethod.IsOpenStaticDelegate)
                {
                    //
                    // For Open static delegates call 
                    //     InteropHelpers.GetCurrentCalleeOpenStaticDelegateFunctionPointer()
                    // which returns a function pointer. Just call the function pointer and we are done.
                    // 
                    TypeDesc[] parameters = new TypeDesc[_marshallers.Length - 1];
                    for (int i = 1; i < _marshallers.Length; i++)
                    {
                        parameters[i - 1] = _marshallers[i].ManagedParameterType;
                    }

                    MethodSignature managedSignature = new MethodSignature(MethodSignatureFlags.Static, 0, _marshallers[0].ManagedParameterType, parameters);
                    fnptrLoadStream.Emit(ILOpcode.call, emitter.NewToken(delegateMethod.Context.GetHelperType("InteropHelpers").GetKnownMethod("GetCurrentCalleeOpenStaticDelegateFunctionPointer", null)));
                    ILLocalVariable vDelegateStub = emitter.NewLocal(delegateMethod.Context.GetWellKnownType(WellKnownType.IntPtr));
                    fnptrLoadStream.EmitStLoc(vDelegateStub);
                    callsiteSetupCodeStream.EmitLdLoc(vDelegateStub);
                    callsiteSetupCodeStream.Emit(ILOpcode.calli, emitter.NewToken(managedSignature));
                }
                else
                {
                    //
                    // For closed delegates call
                    //     InteropHelpers.GetCurrentCalleeDelegate<Delegate>
                    // which returns the delegate. Do a CallVirt on the invoke method.
                    //
                    MethodDesc instantiatedHelper = delegateMethod.Context.GetInstantiatedMethod(
                        delegateMethod.Context.GetHelperType("InteropHelpers").GetKnownMethod("GetCurrentCalleeDelegate", null),
                        new Instantiation((delegateMethod.DelegateType)));
                    fnptrLoadStream.Emit(ILOpcode.call, emitter.NewToken(instantiatedHelper));

                    ILLocalVariable vDelegateStub = emitter.NewLocal(delegateMethod.DelegateType);
                    fnptrLoadStream.EmitStLoc(vDelegateStub);
                    fnptrLoadStream.EmitLdLoc(vDelegateStub);
                    MethodDesc invokeMethod = delegateMethod.DelegateType.GetKnownMethod("Invoke", null);
                    callsiteSetupCodeStream.Emit(ILOpcode.callvirt, emitter.NewToken(invokeMethod));
                }

            }
            else if (MarshalHelpers.UseLazyResolution(_targetMethod, _importMetadata.Module, _pInvokeILEmitterConfiguration))
            {
                MetadataType lazyHelperType = _targetMethod.Context.GetHelperType("InteropHelpers");
                FieldDesc lazyDispatchCell = new PInvokeLazyFixupField((DefType)_targetMethod.OwningType, _importMetadata);
                fnptrLoadStream.Emit(ILOpcode.ldsflda, emitter.NewToken(lazyDispatchCell));
                fnptrLoadStream.Emit(ILOpcode.call, emitter.NewToken(lazyHelperType.GetKnownMethod("ResolvePInvoke", null)));

                MethodSignatureFlags unmanagedCallConv = _importMetadata.Flags.UnmanagedCallingConvention;

                nativeSig = new MethodSignature(
                    _targetMethod.Signature.Flags | unmanagedCallConv, 0, nativeReturnType, nativeParameterTypes);

                ILLocalVariable vNativeFunctionPointer = emitter.NewLocal(_targetMethod.Context.GetWellKnownType(WellKnownType.IntPtr));
                fnptrLoadStream.EmitStLoc(vNativeFunctionPointer);
                callsiteSetupCodeStream.EmitLdLoc(vNativeFunctionPointer);
                callsiteSetupCodeStream.Emit(ILOpcode.calli, emitter.NewToken(nativeSig));
            }
            else
            {
                // Eager call
                nativeSig = new MethodSignature(
                    _targetMethod.Signature.Flags, 0, nativeReturnType, nativeParameterTypes);

                MethodDesc nativeMethod =
                    new PInvokeTargetNativeMethod(_targetMethod, nativeSig);

                callsiteSetupCodeStream.Emit(ILOpcode.call, emitter.NewToken(nativeMethod));
            }
            
            // if the SetLastError flag is set in DllImport, call the PInvokeMarshal.SaveLastWin32Error so that last error can be used later 
            // by calling PInvokeMarshal.GetLastWin32Error
            if (_importMetadata.Flags.SetLastError)
            {
                callsiteSetupCodeStream.Emit(ILOpcode.call, emitter.NewToken(
                            InteropTypes.GetPInvokeMarshal(context).GetKnownMethod("SaveLastWin32Error", null)));
            }

            unmarshallingCodestream.Emit(ILOpcode.ret);

            return new  PInvokeILStubMethodIL((ILStubMethodIL)emitter.Link(_targetMethod), IsStubRequired());
        }

        public static MethodIL EmitIL(MethodDesc method, PInvokeILEmitterConfiguration pinvokeILEmitterConfiguration, InteropStateManager interopStateManager)
        {
            try
            {
                return new PInvokeILEmitter(method, pinvokeILEmitterConfiguration, interopStateManager).EmitIL();
            }
            catch (NotSupportedException)
            {
                string message = "Method '" + method.ToString() +
                    "' requires non-trivial marshalling that is not yet supported by this compiler.";
                return MarshalHelpers.EmitExceptionBody(message, method);
            }
            catch (InvalidProgramException ex)
            {
                Debug.Assert(!String.IsNullOrEmpty(ex.Message));
                return MarshalHelpers.EmitExceptionBody(ex.Message, method);
            }
        }

        private bool IsStubRequired()
        {
            Debug.Assert(_targetMethod.IsPInvoke || _targetMethod is DelegateMarshallingMethodThunk);

            if (_targetMethod is DelegateMarshallingMethodThunk)
            {
                return true;
            }

            if (MarshalHelpers.UseLazyResolution(_targetMethod, _importMetadata.Module, _pInvokeILEmitterConfiguration))
            {
                return true;
            }
            if (_importMetadata.Flags.SetLastError)
            {
                return true;
            }

            for (int i = 0; i < _marshallers.Length; i++)
            {
                if (_marshallers[i].IsMarshallingRequired())
                    return true;
            }
            return false;
        }
    }

    internal sealed class PInvokeILCodeStreams
    {
        public ILEmitter Emitter { get; }
        public ILCodeStream FunctionPointerLoadStream { get; }
        public ILCodeStream MarshallingCodeStream { get; }
        public ILCodeStream CallsiteSetupCodeStream { get; }
        public ILCodeStream ReturnValueMarshallingCodeStream { get; }
        public ILCodeStream UnmarshallingCodestream { get; }
        public PInvokeILCodeStreams()
        {
            Emitter = new ILEmitter();

            // We have 4 code streams:
            // - _marshallingCodeStream is used to convert each argument into a native type and 
            // store that into the local
            // - callsiteSetupCodeStream is used to used to load each previously generated local
            // and call the actual target native method.
            // - _returnValueMarshallingCodeStream is used to convert the native return value 
            // to managed one.
            // - _unmarshallingCodestream is used to propagate [out] native arguments values to 
            // managed ones.
            FunctionPointerLoadStream = Emitter.NewCodeStream();
            MarshallingCodeStream = Emitter.NewCodeStream();
            CallsiteSetupCodeStream = Emitter.NewCodeStream();
            ReturnValueMarshallingCodeStream = Emitter.NewCodeStream();
            UnmarshallingCodestream = Emitter.NewCodeStream();
        }

        public PInvokeILCodeStreams(ILEmitter emitter, ILCodeStream codeStream)
        {
            Emitter = emitter;
            MarshallingCodeStream = codeStream;
        }
    }

    /// <summary>
    /// Synthetic RVA static field that represents PInvoke fixup cell. The RVA data is
    /// backed by a small data structure generated on the fly from the <see cref="PInvokeMetadata"/>
    /// carried by the instance of this class.
    /// </summary>
    public sealed class PInvokeLazyFixupField : FieldDesc
    {
        private DefType _owningType;
        private PInvokeMetadata _pInvokeMetadata;

        public PInvokeLazyFixupField(DefType owningType, PInvokeMetadata pInvokeMetadata)
        {
            _owningType = owningType;
            _pInvokeMetadata = pInvokeMetadata;
        }

        public PInvokeMetadata PInvokeMetadata
        {
            get
            {
                return _pInvokeMetadata;
            }
        }

        public override TypeSystemContext Context
        {
            get
            {
                return _owningType.Context;
            }
        }

        public override TypeDesc FieldType
        {
            get
            {
                return Context.GetHelperType("InteropHelpers").GetNestedType("MethodFixupCell");
            }
        }

        public override bool HasRva
        {
            get
            {
                return true;
            }
        }

        public override bool IsInitOnly
        {
            get
            {
                return false;
            }
        }

        public override bool IsLiteral
        {
            get
            {
                return false;
            }
        }

        public override bool IsStatic
        {
            get
            {
                return true;
            }
        }

        public override bool IsThreadStatic
        {
            get
            {
                return false;
            }
        }

        public override DefType OwningType
        {
            get
            {
                return _owningType;
            }
        }

        public override bool HasCustomAttribute(string attributeNamespace, string attributeName)
        {
            return false;
        }
    }

    public sealed class PInvokeILStubMethodIL : ILStubMethodIL
    {
        public bool IsStubRequired { get; }
        public PInvokeILStubMethodIL(ILStubMethodIL methodIL, bool isStubRequired) : base(methodIL)
        {
            IsStubRequired = isStubRequired;
        }
    }
}
