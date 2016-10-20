// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.IL.Stubs;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;
using Interlocked = System.Threading.Interlocked;

namespace Internal.IL
{
    /// <summary>
    /// Represents a delegate and provides access to compiler-generated methods on the delegate type.
    /// </summary>
    public class DelegateInfo
    {
        private TypeDesc _delegateType;

        private MethodSignature _signature;

        private MethodDesc _getThunkMethod;
        private DelegateThunkCollection _thunks;

        /// <summary>
        /// Gets the Delegate.GetThunk override implementation for this delegate type.
        /// </summary>
        public MethodDesc GetThunkMethod
        {
            get
            {
                if (_getThunkMethod == null)
                {
                    Interlocked.CompareExchange(ref _getThunkMethod, new DelegateGetThunkMethodOverride(this), null);
                }

                return _getThunkMethod;
            }
        }

        /// <summary>
        /// Gets the collection of delegate invocation thunks.
        /// </summary>
        public DelegateThunkCollection Thunks
        {
            get
            {
                if (_thunks == null)
                {
                    Interlocked.CompareExchange(ref _thunks, new DelegateThunkCollection(this), null);
                }
                return _thunks;
            }
        }

        /// <summary>
        /// Gets the signature of the delegate type.
        /// </summary>
        public MethodSignature Signature
        {
            get
            {
                if (_signature == null)
                {
                    _signature = _delegateType.GetKnownMethod("Invoke", null).Signature;
                }
                return _signature;
            }
        }

        /// <summary>
        /// Gets the type of the delegate.
        /// </summary>
        public TypeDesc Type
        {
            get
            {
                return _delegateType;
            }
        }

        public DelegateInfo(TypeDesc delegateType)
        {
            Debug.Assert(delegateType.IsDelegate);
            Debug.Assert(delegateType.IsTypeDefinition);

            _delegateType = delegateType;
        }
    }

    /// <summary>
    /// Represents a collection of delegate invocation thunks.
    /// </summary>
    public class DelegateThunkCollection
    {
        private MethodDesc _openStaticThunk;
        private MethodDesc _multicastThunk;
        private MethodDesc _closedStaticThunk;
        private MethodDesc _invokeThunk;
        private MethodDesc _closedInstanceOverGeneric;

        internal DelegateThunkCollection(DelegateInfo owningDelegate)
        {
            _openStaticThunk = new DelegateInvokeOpenStaticThunk(owningDelegate);
            _multicastThunk = new DelegateInvokeMulticastThunk(owningDelegate);
            _closedStaticThunk = new DelegateInvokeClosedStaticThunk(owningDelegate);
            _invokeThunk = new DelegateDynamicInvokeThunk(owningDelegate);
            _closedInstanceOverGeneric = new DelegateInvokeInstanceClosedOverGenericMethodThunk(owningDelegate);
        }

        public MethodDesc this[DelegateThunkKind kind]
        {
            get
            {
                switch (kind)
                {
                    case DelegateThunkKind.OpenStaticThunk:
                        return _openStaticThunk;
                    case DelegateThunkKind.MulticastThunk:
                        return _multicastThunk;
                    case DelegateThunkKind.ClosedStaticThunk:
                        return _closedStaticThunk;
                    case DelegateThunkKind.DelegateInvokeThunk:
                        return _invokeThunk;
                    case DelegateThunkKind.ClosedInstanceThunkOverGenericMethod:
                        return _closedInstanceOverGeneric;
                    default:
                        return null;
                }
            }
        }
    }

    // TODO: Unify with the consts used in Delegate.cs within the class library.
    public enum DelegateThunkKind
    {
        MulticastThunk = 0,
        ClosedStaticThunk = 1,
        OpenStaticThunk = 2,
        ClosedInstanceThunkOverGenericMethod = 3, // This may not exist
        DelegateInvokeThunk = 4,
        OpenInstanceThunk = 5,        // This may not exist
        ReversePinvokeThunk = 6,       // This may not exist
        ObjectArrayThunk = 7,         // This may not exist
    }
}