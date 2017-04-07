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
    /// Thunk to marshal delegate parameters and invoke the appropriate delegate function pointer
    /// </summary>
    public class DelegateMarshallingMethodThunk : ILStubMethod
    {
        private readonly TypeDesc _owningType;
        private readonly MetadataType _delegateType;
        private readonly InteropStateManager _interopStateManager;
        private readonly MethodDesc _invokeMethod;
        private MethodSignature _signature;         // signature of the native callable marshalling stub

        public bool IsOpenStaticDelegate
        {
            get;
        }

        public DelegateMarshallingMethodThunk(MetadataType delegateType, TypeDesc owningType,
                InteropStateManager interopStateManager, bool isOpenStaticDelegate)
        {
            _owningType = owningType;
            _delegateType = delegateType;
            _invokeMethod = delegateType.GetMethod("Invoke", null);
            _interopStateManager = interopStateManager;
            IsOpenStaticDelegate = isOpenStaticDelegate;
        }

        public override TypeSystemContext Context
        {
            get
            {
                return _owningType.Context;
            }
        }

        public override bool IsNativeCallable
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

        public MetadataType DelegateType
        {
            get
            {
                return _delegateType;
            }
        }

        public override MethodSignature Signature
        {
            get
            {
                if (_signature == null)
                {
                    bool isAnsi = true;
                    var ecmaType = _delegateType as EcmaType;
                    if (ecmaType != null)
                    {
                        isAnsi = ecmaType.GetDelegatePInvokeFlags().CharSet == System.Runtime.InteropServices.CharSet.Ansi;
                    }

                    MethodSignature delegateSignature = _invokeMethod.Signature;
                    TypeDesc[] nativeParameterTypes = new TypeDesc[delegateSignature.Length];
                    ParameterMetadata[] parameterMetadataArray = _invokeMethod.GetParameterMetadata();
                    int parameterIndex = 0;

                    MarshalAsDescriptor marshalAs = null;
                    if (parameterMetadataArray != null && parameterMetadataArray.Length > 0 && parameterMetadataArray[0].Index == 0)
                    {
                        marshalAs = parameterMetadataArray[parameterIndex++].MarshalAsDescriptor;
                    }

                    TypeDesc nativeReturnType = MarshalHelpers.GetNativeMethodParameterType(delegateSignature.ReturnType, null, _interopStateManager, true, isAnsi);
                    for (int i = 0; i < delegateSignature.Length; i++)
                    {
                        int sequence = i + 1;
                        Debug.Assert(parameterIndex == parameterMetadataArray.Length || sequence <= parameterMetadataArray[parameterIndex].Index);
                        if (parameterIndex == parameterMetadataArray.Length || sequence < parameterMetadataArray[parameterIndex].Index)
                        {
                            // if we don't have metadata for the parameter, marshalAs is null
                            marshalAs = null;
                        }
                        else
                        {
                            Debug.Assert(sequence == parameterMetadataArray[parameterIndex].Index);
                            marshalAs = parameterMetadataArray[parameterIndex++].MarshalAsDescriptor;
                        }
                        bool isByRefType = delegateSignature[i].IsByRef;

                        var managedType = isByRefType ? delegateSignature[i].GetParameterType() : delegateSignature[i];

                        var nativeType = MarshalHelpers.GetNativeMethodParameterType(managedType, marshalAs, _interopStateManager, false, isAnsi);

                        nativeParameterTypes[i] = isByRefType ? nativeType.MakePointerType() : nativeType;
                     }
                    _signature = new MethodSignature(MethodSignatureFlags.Static, 0, nativeReturnType, nativeParameterTypes);
                }
                return _signature;
            }
        }

        public override ParameterMetadata[] GetParameterMetadata()
        {
            return _invokeMethod.GetParameterMetadata();
        }

        public override PInvokeMetadata GetPInvokeMethodMetadata()
        {
            return _invokeMethod.GetPInvokeMethodMetadata();
        }

        public MethodSignature DelegateSignature
        {
            get
            {
                return _invokeMethod.Signature;
            }
        }


        public override string Name
        {
            get
            {
                if (IsOpenStaticDelegate)
                {
                    return "ReverseOpenStaticDelegateStub__" + DelegateType.Name;
                }
                else
                {
                    return "ReverseDelegateStub__" + DelegateType.Name;
                }
            }
        }

        public override MethodIL EmitIL()
        {
            return PInvokeILEmitter.EmitIL(this, default(PInvokeILEmitterConfiguration), _interopStateManager);
        }
    }
}
