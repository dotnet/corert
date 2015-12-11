// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Internal.Runtime;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace System.Runtime
{
    // Fundamental runtime type representation
    internal unsafe struct EEType
    {
        [StructLayout(LayoutKind.Explicit)]
        private unsafe struct RelatedTypeUnion
        {
            // Kinds.CanonicalEEType
            [FieldOffset(0)]
            public EEType* _pBaseType;
            [FieldOffset(0)]
            public EEType** _ppBaseTypeViaIAT;

            // Kinds.ClonedEEType
            [FieldOffset(0)]
            public EEType** _ppCanonicalTypeViaIAT;

            // Kinds.ArrayEEType
            [FieldOffset(0)]
            public EEType* _pRelatedParameterType;
            [FieldOffset(0)]
            public EEType** _ppRelatedParameterTypeViaIAT;
        }

        // CS0169: The private field '{blah}' is never used
        // CS0649: Field '{blah}' is never assigned to, and will always have its default value
#pragma warning disable 169, 649

        private UInt16 _usComponentSize;
        private UInt16 _usFlags;
        private UInt32 _uBaseSize;
        private RelatedTypeUnion _relatedType;
        private UInt16 _usNumVtableSlots;
        private UInt16 _usNumInterfaces;
        private UInt32 _uHashCode;

        // vtable follows

#pragma warning restore
        
        private EETypeKind Kind
        {
            get
            {
                return (EETypeKind)(_usFlags & (ushort)EETypeFlags.EETypeKindMask);
            }
        }

        internal UInt16 FlagBits
        {
            get
            {
                return _usFlags;
            }
        }

        internal UInt32 BaseSize
        {
            get
            {
                return _uBaseSize;
            }
        }

        internal UInt16 ComponentSize
        {
            get
            {
                return _usComponentSize;
            }
        }

        internal UInt16 NumVTableSlots
        {
            get
            {
                return _usNumVtableSlots;
            }
        }

        internal bool IsFinalizable
        {
            get
            {
                return ((_usFlags & (ushort)EETypeFlags.HasFinalizerFlag) != 0);
            }
        }

        internal bool IsInterface
        {
            get
            {
                return ((_usFlags & (ushort)EETypeFlags.IsInterfaceFlag) != 0);
            }
        }

        internal bool IsCanonical
        {
            get
            {
                return Kind == EETypeKind.CanonicalEEType;
            }
        }

        internal bool IsCloned
        {
            get
            {
                return Kind == EETypeKind.ClonedEEType;
            }
        }

        internal bool HasReferenceFields
        {
            get
            {
                return ((_usFlags & (UInt16)EETypeFlags.HasPointersFlag) != 0);
            }
        }

        internal bool HasOptionalFields
        {
            get
            {
                return ((_usFlags & (UInt16)EETypeFlags.OptionalFieldsFlag) != 0);
            }
        }

        // RH only natively supports single-dimensional arrays, so this check implicitly means
        // "is single dimensional array"
        internal bool IsArray
        {
            get
            {
                return IsParameterizedType && ParameterizedTypeShape != 0;  // See comment above ParameterizedTypeShape for details.
            }
        }

        internal bool IsParameterizedType
        {
            get
            {
                return Kind == EETypeKind.ParameterizedEEType;
            }
        }

        // The parameterized type shape defines the particular form of parameterized type that
        // is being represented.
        // Currently, the meaning is a shape of 0 indicates that this is a Pointer
        // and non-zero indicates that this is an array.
        // Two types are not equivalent if their shapes do not exactly match.
        internal UInt32 ParameterizedTypeShape
        {
            get
            {
                return _uBaseSize;
            }
        }

        internal bool IsRelatedTypeViaIAT
        {
            get
            {
                return ((_usFlags & (UInt16)EETypeFlags.RelatedTypeViaIATFlag) != 0);
            }
        }

        internal bool IsValueType
        {
            get
            {
                return ((_usFlags & (UInt16)EETypeFlags.ValueTypeFlag) != 0);
            }
        }

        internal bool IsReferenceType
        {
            get
            {
                return !IsValueType;
            }
        }

        internal bool IsRuntimeAllocated
        {
            get
            {
                return ((_usFlags & (UInt16)EETypeFlags.RuntimeAllocatedFlag) != 0);
            }
        }

        internal bool IsGeneric
        {
            get
            {
                return (_usFlags & (UInt16)EETypeFlags.IsGenericFlag) != 0;
            }
        }

        // Mark or determine that a type is generic and one or more of it's type parameters is co- or
        // contra-variant. This only applies to interface and delegate types.
        internal bool HasGenericVariance
        {
            get
            {
                return ((_usFlags & (UInt16)EETypeFlags.GenericVarianceFlag) != 0);
            }
        }

        // Mark or determine that a type requires 8-byte alignment for its fields (required only on certain
        // platforms, only ARM so far).
        internal unsafe bool RequiresAlign8
        {
            get
            {
#if FEATURE_64BIT_ALIGNMENT
                // For now this access to RareFlags is the only managed access required to optional fields. So
                // do the flags lookup via a co-op call into the runtime rather than duplicate all of the
                // logic to locate and decompress optional fields in managed code.
                fixed (EEType* pThis = &this)    
                    return (InternalCalls.RhpGetEETypeRareFlags(pThis) & (UInt32)EETypeRareFlags.RequiresAlign8Flag) != 0;
#else
                return false;
#endif
            }
        }

        // Determine whether a type supports ICastable.
        internal unsafe bool IsICastable
        {
            get
            {
                fixed (EEType* pThis = &this)
                    return (InternalCalls.RhpGetEETypeRareFlags(pThis) & (UInt32)EETypeRareFlags.ICastableFlag) != 0;
            }
        }

        // For an ICastable type return a pointer to code that implements ICastable.IsInstanceOfInterface.
        internal unsafe IntPtr ICastableIsInstanceOfInterfaceMethod
        {
            get
            {
                fixed (EEType* pThis = &this)
                    return InternalCalls.RhpGetICastableIsInstanceOfInterfaceMethod(pThis);
            }
        }

        // For an ICastable type return a pointer to code that implements ICastable.GetImplTypeMethod.
        internal unsafe IntPtr ICastableGetImplTypeMethod
        {
            get
            {
                fixed (EEType* pThis = &this)
                    return InternalCalls.RhpGetICastableGetImplTypeMethod(pThis);
            }
        }

        // Determine whether a type is an instantiation of Nullable<T>.
        internal unsafe bool IsNullable
        {
            get
            {
                fixed (EEType* pThis = &this)
                    return (InternalCalls.RhpGetEETypeRareFlags(pThis) & (UInt32)EETypeRareFlags.IsNullableFlag) != 0;
            }
        }

        // Retrieve the value type T from a Nullable<T>.
        internal unsafe EEType* GetNullableType()
        {
            fixed (EEType* pThis = &this)
                return InternalCalls.RhpGetNullableEEType(pThis);
        }

        // Retrieve the offset of the value embedded in a Nullable<T>.
        internal unsafe byte GetNullableValueOffset()
        {
            fixed (EEType* pThis = &this)
                return InternalCalls.RhpGetNullableEETypeValueOffset(pThis);
        }

        internal unsafe bool IsDynamicType
        {
            get
            {
                fixed (EEType* pThis = &this)
                    return (InternalCalls.RhpGetEETypeRareFlags(pThis) & (UInt32)EETypeRareFlags.IsDynamicTypeFlag) != 0;
            }
        }

        internal EEType* CanonicalEEType
        {
            get
            {
                // cloned EETypes must always refer to types in other modules
                Debug.Assert(IsCloned, "only cloned EETypes have canonical equivalents");
                Debug.Assert(IsRelatedTypeViaIAT, "cloned types should point to their master via IAT");

                return *_relatedType._ppCanonicalTypeViaIAT;
            }
        }

        internal EEType* NonArrayBaseType
        {
            get
            {
                Debug.Assert(!IsArray, "array type not supported in BaseType");

                if (IsCloned)
                {
                    // Assuming that since this is not an Array, the CanonicalEEType is also not an array
                    return CanonicalEEType->NonArrayBaseType;
                }

                Debug.Assert(IsCanonical, "we expect canonical types here");

                if (IsRelatedTypeViaIAT)
                {
                    return *_relatedType._ppBaseTypeViaIAT;
                }

                return _relatedType._pBaseType;
            }
        }

        internal EEType* ArrayBaseType
        {
            get
            {
                fixed (EEType* pThis = &this)
                    return InternalCalls.RhpGetArrayBaseType(pThis);
            }
        }

        internal EEType* NonClonedNonArrayBaseType
        {
            get
            {
                Debug.Assert(!IsArray, "array type not supported in NonArrayBaseType");
                Debug.Assert(!IsCloned, "cloned type not supported in NonClonedNonArrayBaseType");
                Debug.Assert(IsCanonical || IsGenericTypeDefinition, "we expect canonical types here");

                if (IsRelatedTypeViaIAT)
                {
                    return *_relatedType._ppBaseTypeViaIAT;
                }

                return _relatedType._pBaseType;
            }
        }

        internal EEType* BaseType
        {
            get
            {
                Debug.Assert(!IsParameterizedType, "array type not supported in NonArrayBaseType");
                Debug.Assert(!IsCloned, "cloned type not supported in NonClonedNonArrayBaseType");
                Debug.Assert(IsCanonical, "we expect canonical types here");
                Debug.Assert(!IsRelatedTypeViaIAT, "Non IAT");

                return _relatedType._pBaseType;
            }
            set
            {
                Debug.Assert(!IsParameterizedType, "array type not supported in NonArrayBaseType");
                Debug.Assert(!IsCloned, "cloned type not supported in NonClonedNonArrayBaseType");
                Debug.Assert(IsCanonical, "we expect canonical types here");
                _usFlags &= (ushort)~EETypeFlags.RelatedTypeViaIATFlag;
                _relatedType._pBaseType = value;
            }
        }

        internal EEType* RelatedParameterType
        {
            get
            {
                Debug.Assert(IsParameterizedType, "RelatedParameterType can only be used on array or pointer EETypees");
                Debug.Assert(!IsCloned, "cloned array types are not allowed");

                if (IsRelatedTypeViaIAT)
                {
                    return *_relatedType._ppRelatedParameterTypeViaIAT;
                }

                return _relatedType._pRelatedParameterType;
            }
        }

        internal int NumInterfaces
        {
            get
            {
                return _usNumInterfaces;
            }
        }

        internal uint HashCode
        {
            get
            {
                return _uHashCode;
            }
        }


        internal EEInterfaceInfo* InterfaceMap
        {
            get
            {
                fixed (EEType* start = &this)
                {
                    // interface info table starts after the vtable and has m_usNumInterfaces entries
                    return (EEInterfaceInfo*)((byte*)start + sizeof(EEType) + sizeof(void*) * _usNumVtableSlots);
                }
            }
        }

        internal bool HasDispatchMap
        {
            get
            {
                fixed (EEType* pThis = &this)
                    return InternalCalls.RhpHasDispatchMap(pThis);
            }
        }

        internal DispatchResolve.DispatchMap* DispatchMap
        {
            get
            {
                fixed (EEType* pThis = &this)
                    return InternalCalls.RhpGetDispatchMap(pThis);
            }
        }

        internal TypeCast.CorElementType CorElementType
        {
            get
            {
                return (TypeCast.CorElementType)((_usFlags & (ushort)EETypeFlags.CorElementTypeMask) >> (ushort)EETypeFlags.CorElementTypeShift);
            }
        }

        internal unsafe IntPtr* GetVTableStartAddress()
        {
            byte* pResult;

            // EETypes are always in unmanaged memory, so 'leaking' the 'fixed pointer' is safe.
            fixed (EEType* pThis = &this)
                pResult = (byte*)pThis;

            pResult += sizeof(EEType);
            return (IntPtr*)pResult;
        }

        internal IntPtr GetSealedVirtualSlot(ushort index)
        {
            fixed (EEType* pThis = &this)
                return InternalCalls.RhpGetSealedVirtualSlot(pThis, index);
        }

        // Returns an address in the module most closely associated with this EEType that can be handed to
        // EH.GetClasslibException and use to locate the compute the correct exception type. In most cases 
        // this is just the EEType pointer itself, but when this type represents a generic that has been 
        // unified at runtime (and thus the EEType pointer resides in the process heap rather than a specific 
        // module) we need to do some work.
        internal unsafe IntPtr GetAssociatedModuleAddress()
        {
            fixed (EEType* pThis = &this)
            {
                if (!IsRuntimeAllocated && !IsDynamicType)
                    return (IntPtr)pThis;

                // There are currently three types of runtime allocated EETypes, arrays, pointers, and generic types.
                // Arrays/Pointers can be handled by looking at their element type.
                if (IsParameterizedType)
                    return pThis->RelatedParameterType->GetAssociatedModuleAddress();

                // Generic types are trickier. Often we could look at the parent type (since eventually it
                // would derive from the class library's System.Object which is definitely not runtime
                // allocated). But this breaks down for generic interfaces. Instead we fetch the generic
                // instantiation information and use the generic type definition, which will always be module
                // local. We know this lookup will succeed since we're dealing with a unified generic type
                // and the unification process requires this metadata.
                EETypeRef* pInstantiation;
                int arity;
                GenericVariance* pVarianceInfo;
                EEType* pGenericType = InternalCalls.RhGetGenericInstantiation(pThis,
                                                                                &arity,
                                                                                &pInstantiation,
                                                                                &pVarianceInfo);

                Debug.Assert(pGenericType != null, "Generic type expected");

                return (IntPtr)pGenericType;
            }
        }

        internal bool IsGenericTypeDefinition
        {
            get
            {
                return Kind == EETypeKind.GenericTypeDefEEType;
            }
        }

        internal bool IsPointerTypeDefinition
        {
            get
            {
                if (Kind != EETypeKind.ParameterizedEEType)
                    return false;
                return ParameterizedTypeShape == 0; // See comment above ParameterizedTypeShape for details.
            }
        }

        // Get the address of the finalizer method for finalizable types.
        internal IntPtr FinalizerCode
        {
            get
            {
                Debug.Assert(IsFinalizable, "Can't get finalizer for non-finalizeable type");
                fixed (EEType* start = &this)
                {
                    // Finalizer code address is stored after the vtable and interface map.
                    return *(IntPtr*)((byte*)start +
                                      sizeof(EEType) +
                                      (sizeof(void*) * _usNumVtableSlots) +
                                      (sizeof(EEInterfaceInfo) * NumInterfaces));
                }
            }
        }

        /// <summary>
        /// Return true if type is good for simple casting : canonical, no related type via IAT, no generic variance
        /// </summary>
        internal bool SimpleCasting()
        {
            return (_usFlags & (ushort)EETypeFlags.ComplexCastingMask) == (ushort)EETypeKind.CanonicalEEType;
        }

        /// <summary>
        /// Does this type have a class constructor.
        /// </summary>
        internal bool HasCctor
        {
            get
            {
                fixed (EEType* pThis = &this)
                    return (InternalCalls.RhpGetEETypeRareFlags(pThis) & (UInt32)EETypeRareFlags.HasCctorFlag) != 0;
            }
        }

        /// <summary>
        /// Return true if both types are good for simple casting: canonical, no related type via IAT, no generic variance
        /// </summary>
        static internal bool BothSimpleCasting(EEType* pThis, EEType* pOther)
        {
            return ((pThis->_usFlags | pOther->_usFlags) & (ushort)EETypeFlags.ComplexCastingMask) == (ushort)EETypeKind.CanonicalEEType;
        }

        internal bool IsEquivalentTo(EEType* pOtherEEType)
        {
            fixed (EEType* pThis = &this)
            {
                if (pThis == pOtherEEType)
                    return true;

                EEType* pThisEEType = pThis;

                if (pThisEEType->IsCloned)
                    pThisEEType = pThisEEType->CanonicalEEType;

                if (pOtherEEType->IsCloned)
                    pOtherEEType = pOtherEEType->CanonicalEEType;

                if (pThisEEType == pOtherEEType)
                    return true;

                if (pThisEEType->IsParameterizedType && pOtherEEType->IsParameterizedType)
                {
                    return pThisEEType->RelatedParameterType->IsEquivalentTo(pOtherEEType->RelatedParameterType) &&
                        pThisEEType->ParameterizedTypeShape == pOtherEEType->ParameterizedTypeShape;
                }
            }

            return false;
        }
    }

    // CS0169: The private field '{blah}' is never used
    // CS0649: Field '{blah}' is never assigned to, and will always have its default value
#pragma warning disable 169, 649

    // Wrapper around EEType pointers that may be indirected through the IAT if their low bit is set.
    internal unsafe struct EETypeRef
    {
        private byte* _value;

        public EEType* Value
        {
            get
            {
                if (((int)_value & 1) == 0)
                    return (EEType*)_value;
                return *(EEType**)(_value - 1);
            }
        }
    }

    internal unsafe struct EEInterfaceInfo
    {
        [StructLayout(LayoutKind.Explicit)]
        private unsafe struct InterfaceTypeUnion
        {
            [FieldOffset(0)]
            public EEType* _pInterfaceEEType;
            [FieldOffset(0)]
            public EEType** _ppInterfaceEETypeViaIAT;
        }

        private InterfaceTypeUnion _interfaceType;

        internal EEType* InterfaceType
        {
            get
            {
                if ((unchecked((uint)_interfaceType._pInterfaceEEType) & 1u) != 0)
                {
#if BIT64
                    EEType** ppInterfaceEETypeViaIAT = (EEType**)(((ulong)_interfaceType._ppInterfaceEETypeViaIAT) & ~1ul);
#else
                    EEType** ppInterfaceEETypeViaIAT = (EEType**)(((uint)_interfaceType._ppInterfaceEETypeViaIAT) & ~1u);
#endif
                    return *ppInterfaceEETypeViaIAT;
                }

                return _interfaceType._pInterfaceEEType;
            }
            set { _interfaceType._pInterfaceEEType = value; }
        }

#pragma warning restore
    };

    internal static class WellKnownEETypes
    {
        // Returns true if the passed in EEType is the EEType for System.Object
        // This is recognized by the fact that System.Object and interfaces are the only ones without a base type
        internal static unsafe bool IsSystemObject(EEType* pEEType)
        {
            if (pEEType->IsArray)
                return false;
            return (pEEType->NonArrayBaseType == null) && !pEEType->IsInterface;
        }

        // Returns true if the passed in EEType is the EEType for System.Array.
        // The binder sets a special CorElementType for this well known type
        internal static unsafe bool IsSystemArray(EEType* pEEType)
        {
            return (pEEType->CorElementType == TypeCast.CorElementType.ELEMENT_TYPE_ARRAY);
        }
    }
} // System.Runtime
