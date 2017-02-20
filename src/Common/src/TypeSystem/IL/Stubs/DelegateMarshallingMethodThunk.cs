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
    internal class DelegateMarshallingMethodThunk : ILStubMethod
    {
        private TypeDesc _owningType;
        private static int s_stubMethodIdCounter;
        private string _name;
        private MethodSignature _signature;
        private TypeDesc _delegateType;

        public DelegateMarshallingMethodThunk(TypeDesc owningType, TypeDesc delegateType)
        {
            _owningType = owningType;
            _delegateType = delegateType;
            MethodDesc invokeMethod = delegateType.GetMethod("Invoke", null);
            _signature = invokeMethod.Signature;
            _name = "ReverseDelegateStub" + GetNextStubMethodId();
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
        private int GetNextStubMethodId()
        {
            return System.Threading.Interlocked.Increment(ref s_stubMethodIdCounter);
        }

        public void SetNativeCallableSignature(MethodSignature signature)
        {
            _signature = signature;
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
            return null;
        }
    }


    internal enum DelegateInvokeMethodParameterKind
    {
        None,
        Value,
        Reference
    }

    internal struct DelegateInvokeMethodSignature : IEquatable<DelegateInvokeMethodSignature>
    {
        public MethodSignature Signature;
        public bool HasReturnValue
        {
            get
            {
                return !Signature.ReturnType.IsVoid;
            }
        }

        public int Length
        {
            get
            {
                return Signature.Length;
            }
        }

        public DelegateInvokeMethodParameterKind this[int index]
        {
            get
            {
                return Signature[index].IsByRef ?
                    DelegateInvokeMethodParameterKind.Reference :
                    DelegateInvokeMethodParameterKind.Value;
            }
        }

        public DelegateInvokeMethodSignature(TypeDesc delegateType)
        {
            MethodDesc invokeMethod = delegateType.GetMethod("Invoke", null);
            Signature = invokeMethod.Signature;
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

        public bool Equals(DelegateInvokeMethodSignature other)
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
