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
    /// Thunk to marshal delegate parameters and invoke the appropriate delegate function pointer
    /// </summary>
    internal class DelegateMarshallingMethodThunk : ILStubMethod
    {
        private TypeDesc _owningType;
        private MethodSignature _delegateSignature; // signature of the delegate
        private MethodSignature _signature;         // signature of the native callable marshalling stub
        private TypeDesc _delegateType;
        private MethodIL _methodIL;

        public DelegateMarshallingMethodThunk(TypeDesc owningType, TypeDesc delegateType)
        {
            _owningType = owningType;
            _delegateType = delegateType;
            MethodDesc invokeMethod = delegateType.GetMethod("Invoke", null);
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
                return "ReverseDelegateStub__" + ILCompiler.DependencyAnalysis.NodeFactory.NameMangler.GetMangledTypeName(_delegateType);
            }
        }

        public override MethodIL EmitIL()
        {
            return _methodIL;
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
