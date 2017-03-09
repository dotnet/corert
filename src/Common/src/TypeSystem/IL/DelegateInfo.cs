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
        private MethodDesc _reversePInvokeThunk;
        private MethodDesc _invokeObjectArrayThunk;

        internal DelegateThunkCollection(DelegateInfo owningDelegate)
        {
            _openStaticThunk = new DelegateInvokeOpenStaticThunk(owningDelegate);
            _multicastThunk = new DelegateInvokeMulticastThunk(owningDelegate);
            _closedStaticThunk = new DelegateInvokeClosedStaticThunk(owningDelegate);
            _invokeThunk = new DelegateDynamicInvokeThunk(owningDelegate);
            _closedInstanceOverGeneric = new DelegateInvokeInstanceClosedOverGenericMethodThunk(owningDelegate);
            _invokeObjectArrayThunk = new DelegateInvokeObjectArrayThunk(owningDelegate);

            if (!owningDelegate.Type.HasInstantiation && IsNativeCallingConventionCompatible(owningDelegate.Signature))
                _reversePInvokeThunk = new DelegateReversePInvokeThunk(owningDelegate);
        }

        #region Temporary interop logic
        // TODO: interop should provide a way to query this
        private static bool IsNativeCallingConventionCompatible(MethodSignature delegateSignature)
        {
            if (!IsNativeCallingConventionCompatible(delegateSignature.ReturnType))
                return false;
            else
            {
                for (int i = 0; i < delegateSignature.Length; i++)
                {
                    if (!IsNativeCallingConventionCompatible(delegateSignature[i]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsNativeCallingConventionCompatible(TypeDesc type)
        {
            if (type.IsPointer || type.IsByRef)
                return IsNativeCallingConventionCompatible(((ParameterizedType)type).ParameterType);

            if (!type.IsValueType)
                return false;

            if (type.IsPrimitive)
            {
                if (type.IsWellKnownType(WellKnownType.Boolean))
                    return false;

                return true;
            }

            foreach (FieldDesc field in type.GetFields())
            {
                if (!field.IsStatic && !IsNativeCallingConventionCompatible(field.FieldType))
                    return false;
            }

            return true;
        }
        #endregion

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
                    case DelegateThunkKind.ReversePinvokeThunk:
                        return _reversePInvokeThunk;
                    case DelegateThunkKind.ObjectArrayThunk:
                        return _invokeObjectArrayThunk;
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