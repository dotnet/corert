// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;

namespace Internal.Runtime
{
    // Extensions to EEType that are specific to the use in Runtime.Base.
    unsafe partial struct EEType
    {
        internal DispatchResolve.DispatchMap* DispatchMap
        {
            get
            {
                fixed (EEType* pThis = &this)
                    return InternalCalls.RhpGetDispatchMap(pThis);
            }
        }

        internal EEType* GetArrayEEType()
        {
#if INPLACE_RUNTIME
            return EETypePtr.EETypePtrOf<Array>().ToPointer();
#else
            fixed (EEType* pThis = &this)
                return InternalCalls.RhpGetArrayBaseType(pThis);
#endif
        }

        internal Exception GetClasslibException(ExceptionIDs id)
        {
#if INPLACE_RUNTIME
            return RuntimeExceptionHelpers.GetRuntimeException(id);
#else
            DynamicModule* dynamicModule = this.DynamicModule;
            if (dynamicModule != null)
            {
                IntPtr getRuntimeException = dynamicModule->GetRuntimeException;
                if (getRuntimeException != IntPtr.Zero)
                {
                    return CalliIntrinsics.Call<Exception>(getRuntimeException, id);
                }
            }
            if (IsParameterizedType)
            {
                return RelatedParameterType->GetClasslibException(id);
            }

            return EH.GetClasslibException(id, GetAssociatedModuleAddress());
#endif
        }

        internal void SetToCloneOf(EEType *pOrigType)
        {
            Debug.Assert((_usFlags & (ushort)EETypeFlags.EETypeKindMask) == 0, "should be a canonical type");
            this._usFlags |= (ushort)EETypeKind.ClonedEEType;
            this._relatedType._pCanonicalType = pOrigType;
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

                // There are currently four types of runtime allocated EETypes, arrays, pointers, byrefs, and generic types.
                // Arrays/Pointers/ByRefs can be handled by looking at their element type.
                if (IsParameterizedType)
                    return pThis->RelatedParameterType->GetAssociatedModuleAddress();

                if (!IsGeneric)
                {
                    // No way to resolve module information for a non-generic dynamic type.
                    return IntPtr.Zero;
                }

                // Generic types are trickier. Often we could look at the parent type (since eventually it
                // would derive from the class library's System.Object which is definitely not runtime
                // allocated). But this breaks down for generic interfaces. Instead we fetch the generic
                // instantiation information and use the generic type definition, which will always be module
                // local. We know this lookup will succeed since we're dealing with a unified generic type
                // and the unification process requires this metadata.
                EEType* pGenericType = pThis->GenericDefinition;

                Debug.Assert(pGenericType != null, "Generic type expected");

                return (IntPtr)pGenericType;
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
            return (pEEType->CorElementType == CorElementType.ELEMENT_TYPE_ARRAY);
        }
    }
}
