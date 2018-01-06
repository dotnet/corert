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
        private readonly PInvokeFlags _flags;
        private readonly InteropStateManager _interopStateManager;

        private PInvokeILEmitter(MethodDesc targetMethod, PInvokeILEmitterConfiguration pinvokeILEmitterConfiguration, InteropStateManager interopStateManager)
        {
            Debug.Assert(targetMethod.IsPInvoke || targetMethod is DelegateMarshallingMethodThunk);
            _targetMethod = targetMethod;
            _pInvokeILEmitterConfiguration = pinvokeILEmitterConfiguration;
            _importMetadata = targetMethod.GetPInvokeMethodMetadata();
            _interopStateManager = interopStateManager;

            //
            // targetMethod could be either a PInvoke or a DelegateMarshallingMethodThunk
            // ForwardNativeFunctionWrapper method thunks are marked as PInvokes, so it is
            // important to check them first here so that we get the right flags.
            // 
            DelegateMarshallingMethodThunk delegateThunk = _targetMethod as DelegateMarshallingMethodThunk;

            if (delegateThunk != null)
            {
                _flags = ((EcmaType)delegateThunk.DelegateType).GetDelegatePInvokeFlags();
            }
            else
            {
                Debug.Assert(_targetMethod.IsPInvoke);
                _flags = _importMetadata.Flags;
            }
            _marshallers = InitializeMarshallers(targetMethod, interopStateManager, _flags);
        }

        private static Marshaller[] InitializeMarshallers(MethodDesc targetMethod, InteropStateManager interopStateManager, PInvokeFlags flags)
        {
            bool isDelegate = targetMethod is DelegateMarshallingMethodThunk;
            MethodSignature methodSig = isDelegate ? ((DelegateMarshallingMethodThunk)targetMethod).DelegateSignature : targetMethod.Signature;
            MarshalDirection direction = isDelegate ? ((DelegateMarshallingMethodThunk)targetMethod).Direction: MarshalDirection.Forward;
            int indexOffset = 0;
            if (!methodSig.IsStatic && direction == MarshalDirection.Forward)
            {
                // For instance methods(eg. Forward delegate marshalling thunk), first argument is 
                // the instance
                indexOffset = 1;
            }
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
                                                    direction,
                                                    marshallers,
                                                    interopStateManager,
                                                    indexOffset + parameterMetadata.Index,
                                                    flags,
                                                    parameterMetadata.In,
                                                    parameterMetadata.Out,
                                                    parameterMetadata.Return
                                                    );
            }

            return marshallers;
        }

        private void EmitDelegateCall(DelegateMarshallingMethodThunk delegateMethod, PInvokeILCodeStreams ilCodeStreams)
        {
            ILEmitter emitter = ilCodeStreams.Emitter;
            ILCodeStream fnptrLoadStream = ilCodeStreams.FunctionPointerLoadStream;
            ILCodeStream marshallingCodeStream = ilCodeStreams.MarshallingCodeStream;
            ILCodeStream callsiteSetupCodeStream = ilCodeStreams.CallsiteSetupCodeStream;
            TypeSystemContext context = _targetMethod.Context;

            Debug.Assert(delegateMethod != null);

            if (delegateMethod.Kind == DelegateMarshallingMethodThunkKind.ReverseOpenStatic)
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

                MethodSignature managedSignature = new MethodSignature(
                    MethodSignatureFlags.Static, 0, _marshallers[0].ManagedParameterType, parameters);

                fnptrLoadStream.Emit(ILOpcode.call, emitter.NewToken(
                    delegateMethod.Context.GetHelperType("InteropHelpers").GetKnownMethod(
                        "GetCurrentCalleeOpenStaticDelegateFunctionPointer", null)));

                ILLocalVariable vDelegateStub = emitter.NewLocal(
                    delegateMethod.Context.GetWellKnownType(WellKnownType.IntPtr));

                fnptrLoadStream.EmitStLoc(vDelegateStub);
                callsiteSetupCodeStream.EmitLdLoc(vDelegateStub);
                callsiteSetupCodeStream.Emit(ILOpcode.calli, emitter.NewToken(managedSignature));
            }
            else if (delegateMethod.Kind == DelegateMarshallingMethodThunkKind.ReverseClosed)
            {
                //
                // For closed delegates call
                //     InteropHelpers.GetCurrentCalleeDelegate<Delegate>
                // which returns the delegate. Do a CallVirt on the invoke method.
                //
                MethodDesc instantiatedHelper = delegateMethod.Context.GetInstantiatedMethod(
                    delegateMethod.Context.GetHelperType("InteropHelpers")
                    .GetKnownMethod("GetCurrentCalleeDelegate", null),
                        new Instantiation((delegateMethod.DelegateType)));

                fnptrLoadStream.Emit(ILOpcode.call, emitter.NewToken(instantiatedHelper));

                ILLocalVariable vDelegateStub = emitter.NewLocal(delegateMethod.DelegateType);
                fnptrLoadStream.EmitStLoc(vDelegateStub);
                marshallingCodeStream.EmitLdLoc(vDelegateStub);
                MethodDesc invokeMethod = delegateMethod.DelegateType.GetKnownMethod("Invoke", null);
                callsiteSetupCodeStream.Emit(ILOpcode.callvirt, emitter.NewToken(invokeMethod));
            }
            else if (delegateMethod.Kind == DelegateMarshallingMethodThunkKind
                .ForwardNativeFunctionWrapper)
            {
                // if the SetLastError flag is set in UnmanagedFunctionPointerAttribute, clear the error code before doing P/Invoke 
                if (_flags.SetLastError)
                {
                    callsiteSetupCodeStream.Emit(ILOpcode.call, emitter.NewToken(
                                InteropTypes.GetPInvokeMarshal(context).GetKnownMethod("ClearLastWin32Error", null)));
                }

                //
                // For NativeFunctionWrapper we need to load the native function and call it
                //
                fnptrLoadStream.EmitLdArg(0);
                fnptrLoadStream.Emit(ILOpcode.call, emitter.NewToken(InteropTypes
                    .GetNativeFunctionPointerWrapper(context)
                    .GetMethod("get_NativeFunctionPointer", null)));

                var fnPtr = emitter.NewLocal(
                    context.GetWellKnownType(WellKnownType.IntPtr));

                fnptrLoadStream.EmitStLoc(fnPtr);
                callsiteSetupCodeStream.EmitLdLoc(fnPtr);

                TypeDesc nativeReturnType = _marshallers[0].NativeParameterType;
                TypeDesc[] nativeParameterTypes = new TypeDesc[_marshallers.Length - 1];

                for (int i = 1; i < _marshallers.Length; i++)
                {
                    nativeParameterTypes[i - 1] = _marshallers[i].NativeParameterType;
                }

                MethodSignature nativeSig = new MethodSignature(
                    MethodSignatureFlags.Static | _flags.UnmanagedCallingConvention, 0, nativeReturnType, nativeParameterTypes);

                callsiteSetupCodeStream.Emit(ILOpcode.calli, emitter.NewToken(nativeSig));

                // if the SetLastError flag is set in UnmanagedFunctionPointerAttribute, call the PInvokeMarshal.
                // SaveLastWin32Error so that last error can be used later by calling 
                // PInvokeMarshal.GetLastWin32Error
                if (_flags.SetLastError)
                {
                    callsiteSetupCodeStream.Emit(ILOpcode.call, emitter.NewToken(
                                InteropTypes.GetPInvokeMarshal(context)
                                .GetKnownMethod("SaveLastWin32Error", null)));
                }
            }
            else
            {
                Debug.Fail("Unexpected DelegateMarshallingMethodThunkKind");
            }
        }

        private void EmitPInvokeCall(PInvokeILCodeStreams ilCodeStreams)
        {
            ILEmitter emitter = ilCodeStreams.Emitter;
            ILCodeStream fnptrLoadStream = ilCodeStreams.FunctionPointerLoadStream;
            ILCodeStream callsiteSetupCodeStream = ilCodeStreams.CallsiteSetupCodeStream;
            TypeSystemContext context = _targetMethod.Context;

            TypeDesc nativeReturnType = _marshallers[0].NativeParameterType;
            TypeDesc[] nativeParameterTypes = new TypeDesc[_marshallers.Length - 1];

            // if the SetLastError flag is set in DllImport, clear the error code before doing P/Invoke 
            if (_flags.SetLastError)
            {
                callsiteSetupCodeStream.Emit(ILOpcode.call, emitter.NewToken(
                            InteropTypes.GetPInvokeMarshal(context).GetKnownMethod("ClearLastWin32Error", null)));
            }

            for (int i = 1; i < _marshallers.Length; i++)
            {
                nativeParameterTypes[i - 1] = _marshallers[i].NativeParameterType;
            }

            if (MarshalHelpers.UseLazyResolution(_targetMethod,
                _importMetadata.Module,
                _pInvokeILEmitterConfiguration))
            {
                MetadataType lazyHelperType = context.GetHelperType("InteropHelpers");
                FieldDesc lazyDispatchCell = _interopStateManager.GetPInvokeLazyFixupField(_targetMethod);

                fnptrLoadStream.Emit(ILOpcode.ldsflda, emitter.NewToken(lazyDispatchCell));
                fnptrLoadStream.Emit(ILOpcode.call, emitter.NewToken(lazyHelperType
                    .GetKnownMethod("ResolvePInvoke", null)));

                MethodSignatureFlags unmanagedCallConv = _flags.UnmanagedCallingConvention;

                MethodSignature nativeSig = new MethodSignature(
                    _targetMethod.Signature.Flags | unmanagedCallConv, 0, nativeReturnType,
                    nativeParameterTypes);

                ILLocalVariable vNativeFunctionPointer = emitter.NewLocal(context
                    .GetWellKnownType(WellKnownType.IntPtr));

                fnptrLoadStream.EmitStLoc(vNativeFunctionPointer);
                callsiteSetupCodeStream.EmitLdLoc(vNativeFunctionPointer);
                callsiteSetupCodeStream.Emit(ILOpcode.calli, emitter.NewToken(nativeSig));
            }
            else
            {
                // Eager call
                MethodSignature nativeSig = new MethodSignature(
                    _targetMethod.Signature.Flags, 0, nativeReturnType, nativeParameterTypes);

                MethodDesc nativeMethod =
                    new PInvokeTargetNativeMethod(_targetMethod, nativeSig);

                callsiteSetupCodeStream.Emit(ILOpcode.call, emitter.NewToken(nativeMethod));
            }

            // if the SetLastError flag is set in DllImport, call the PInvokeMarshal.
            // SaveLastWin32Error so that last error can be used later by calling 
            // PInvokeMarshal.GetLastWin32Error
            if (_flags.SetLastError)
            {
                callsiteSetupCodeStream.Emit(ILOpcode.call, emitter.NewToken(
                            InteropTypes.GetPInvokeMarshal(context)
                            .GetKnownMethod("SaveLastWin32Error", null)));
            }
        }

        private MethodIL EmitIL()
        {
            PInvokeILCodeStreams pInvokeILCodeStreams = new PInvokeILCodeStreams();
            ILEmitter emitter = pInvokeILCodeStreams.Emitter;
            ILCodeStream unmarshallingCodestream = pInvokeILCodeStreams.UnmarshallingCodestream;

            // Marshal the arguments
            for (int i = 0; i < _marshallers.Length; i++)
            {
                _marshallers[i].EmitMarshallingIL(pInvokeILCodeStreams);
            }

            // make the call
            DelegateMarshallingMethodThunk delegateMethod = _targetMethod as DelegateMarshallingMethodThunk;
            if (delegateMethod != null)
            {
                EmitDelegateCall(delegateMethod, pInvokeILCodeStreams);
            }
            else
            {
                EmitPInvokeCall(pInvokeILCodeStreams);
            }

            _marshallers[0].LoadReturnValue(unmarshallingCodestream);
            unmarshallingCodestream.Emit(ILOpcode.ret);

            return new  PInvokeILStubMethodIL((ILStubMethodIL)emitter.Link(_targetMethod), IsStubRequired());
        }

        public static MethodIL EmitIL(MethodDesc method, 
            PInvokeILEmitterConfiguration pinvokeILEmitterConfiguration, 
            InteropStateManager interopStateManager)
        {
            try
            {
                return new PInvokeILEmitter(method, pinvokeILEmitterConfiguration, interopStateManager)
                    .EmitIL();
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

            if (MarshalHelpers.UseLazyResolution(_targetMethod, _importMetadata.Module, 
                _pInvokeILEmitterConfiguration))
            {
                return true;
            }
            if (_flags.SetLastError)
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

    public sealed class PInvokeILStubMethodIL : ILStubMethodIL
    {
        public bool IsStubRequired { get; }
        public PInvokeILStubMethodIL(ILStubMethodIL methodIL, bool isStubRequired) : base(methodIL)
        {
            IsStubRequired = isStubRequired;
        }
    }
}
