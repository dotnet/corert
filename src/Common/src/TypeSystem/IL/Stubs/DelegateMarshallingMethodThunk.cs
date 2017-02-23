// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.TypeSystem;

namespace Internal.IL.Stubs
{
    /// <summary>
    /// Thunk to marshal delegate parameters and invoke the appropriate delegate function pointer
    /// </summary>
    internal class DelegateMarshallingMethodThunk : ILStubMethod
    {
        private TypeDesc _owningType;
        private MethodSignature _delegateSignature; // signature of the delegate
        private MethodSignature _signature;         // signature of the native callable marshalling stub
        private TypeDesc _delegateType;
        private MethodIL _methodIL;
        private string _name;

        public DelegateMarshallingMethodThunk(TypeDesc owningType, TypeDesc delegateType, string name)
        {
            _owningType = owningType;
            _delegateType = delegateType;
            MethodDesc invokeMethod = delegateType.GetMethod("Invoke", null);
            _name = name;
            _delegateSignature = invokeMethod.Signature;
            _methodIL = PInvokeILEmitter.EmitIL(this, null);
            _signature = ((PInvokeILStubMethodIL)_methodIL).NativeCallableSignature;
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

        public TypeDesc DelegateType
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
                return _signature;
            }
        }

        public MethodSignature DelegateSignature
        {
            get
            {
                return _delegateSignature;
            }
        }


        public override string Name
        {
            get
            {
                return _name;
            }
        }

        public override MethodIL EmitIL()
        {
            return _methodIL;
        }
    }

    
    internal struct DelegateInvokeMethodSignature : IEquatable<DelegateInvokeMethodSignature>
    {
        public  readonly MethodSignature Signature;

        public DelegateInvokeMethodSignature(TypeDesc delegateType)
        {
            MethodDesc invokeMethod = delegateType.GetMethod("Invoke", null);
            Signature = invokeMethod.Signature;
        }

        public override int GetHashCode()
        {
            return Signature.GetHashCode();
        }

        // TODO: Use the MarshallerKind for each parameter to compare whether two signatures are similar(ie. whether two delegates can share marshalling stubs)
        public bool Equals(DelegateInvokeMethodSignature other)
        {
            if (Signature.ReturnType != other.Signature.ReturnType)
                return false;

            if (Signature.Length != other.Signature.Length)
                return false;

            for (int i = 0; i < Signature.Length; i++)
            {
                if (Signature[i] != other.Signature[i])
                    return false;
            }

            return true;
        }
    }

}
