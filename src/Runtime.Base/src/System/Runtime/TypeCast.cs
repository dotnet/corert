// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace System.Runtime
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////
    //
    //                                    **** WARNING ****
    //
    // A large portion of the logic present in this file is duplicated 
    // in src\System.Private.Reflection.Execution\Internal\Reflection\Execution\TypeLoader\TypeCast.cs
    // (for dynamic type builder). If you make changes here make sure they are reflected there.
    //
    //                                    **** WARNING ****
    //
    /////////////////////////////////////////////////////////////////////////////////////////////////////

    internal static class TypeCast
    {
        [RuntimeExport("RhTypeCast_IsInstanceOfClass")]
        static public unsafe object IsInstanceOfClass(object obj, void* pvTargetType)
        {
            if (obj == null)
            {
                return null;
            }

            EEType* pTargetType = (EEType*)pvTargetType;
            EEType* pObjType = obj.EEType;

            Debug.Assert(!pTargetType->IsParameterizedType, "IsInstanceOfClass called with parameterized EEType");
            Debug.Assert(!pTargetType->IsInterface, "IsInstanceOfClass called with interface EEType");

            // if the EETypes pointers match, we're done
            if (pObjType == pTargetType)
            {
                return obj;
            }

            // Quick check if both types are good for simple casting: canonical, no related type via IAT, no generic variance
            if (System.Runtime.EEType.BothSimpleCasting(pObjType, pTargetType))
            {
                // walk the type hierarchy looking for a match
                do
                {
                    pObjType = pObjType->BaseType;

                    if (pObjType == null)
                    {
                        return null;
                    }

                    if (pObjType == pTargetType)
                    {
                        return obj;
                    }
                }
                while (pObjType->SimpleCasting());
            }

            if (pTargetType->IsCloned)
            {
                pTargetType = pTargetType->CanonicalEEType;
            }

            if (pObjType->IsCloned)
            {
                pObjType = pObjType->CanonicalEEType;
            }

            // if the EETypes pointers match, we're done
            if (pObjType == pTargetType)
            {
                return obj;
            }

            if (pTargetType->HasGenericVariance && pObjType->HasGenericVariance)
            {
                // Only generic interfaces and delegates can have generic variance and we shouldn't see
                // interfaces for either input here. So if the canonical types are marked as having variance
                // we know we've hit the delegate case. We've dealt with the identical case just above. And
                // the regular path below will handle casting to Object, Delegate and MulticastDelegate. Since
                // we don't support deriving from user delegate classes any further all we have to check here
                // is that the uninstantiated generic delegate definitions are the same and the type
                // parameters are compatible.
                return TypesAreCompatibleViaGenericVariance(pObjType, pTargetType) ? obj : null;
            }

            if (pObjType->IsArray)
            {
                // arrays can be cast to System.Object
                if (WellKnownEETypes.IsSystemObject(pTargetType))
                {
                    return obj;
                }

                // arrays can be cast to System.Array
                if (WellKnownEETypes.IsSystemArray(pTargetType))
                {
                    return obj;
                }

                return null;
            }


            // walk the type hierarchy looking for a match
            while (true)
            {
                pObjType = pObjType->NonClonedNonArrayBaseType;
                if (pObjType == null)
                {
                    return null;
                }

                if (pObjType->IsCloned)
                    pObjType = pObjType->CanonicalEEType;

                if (pObjType == pTargetType)
                {
                    return obj;
                }
            }
        }

        [RuntimeExport("RhTypeCast_CheckCastClass")]
        static public unsafe object CheckCastClass(Object obj, void* pvTargetEEType)
        {
            // a null value can be cast to anything
            if (obj == null)
                return null;

            object result = IsInstanceOfClass(obj, pvTargetEEType);

            if (result == null)
            {
                // Throw the invalid cast exception defined by the classlib, using the input EEType* 
                // to find the correct classlib.

                ExceptionIDs exID = ExceptionIDs.InvalidCast;

                IntPtr addr = ((EEType*)pvTargetEEType)->GetAssociatedModuleAddress();
                Exception e = EH.GetClasslibException(exID, addr);

                BinderIntrinsics.TailCall_RhpThrowEx(e);
            }

            return result;
        }

        [RuntimeExport("RhTypeCast_CheckUnbox")]
        static public unsafe void CheckUnbox(Object obj, byte expectedCorElementType)
        {
            if (obj == null)
            {
                return;
            }

            if (obj.EEType->CorElementType == (CorElementType)expectedCorElementType)
                return;

            // Throw the invalid cast exception defined by the classlib, using the input object's EEType* 
            // to find the correct classlib.

            ExceptionIDs exID = ExceptionIDs.InvalidCast;

            IntPtr addr = obj.EEType->GetAssociatedModuleAddress();
            Exception e = EH.GetClasslibException(exID, addr);

            BinderIntrinsics.TailCall_RhpThrowEx(e);
        }

        [RuntimeExport("RhTypeCast_IsInstanceOfArray")]
        static public unsafe object IsInstanceOfArray(object obj, void* pvTargetType)
        {
            if (obj == null)
            {
                return null;
            }

            EEType* pTargetType = (EEType*)pvTargetType;
            EEType* pObjType = obj.EEType;

            Debug.Assert(pTargetType->IsArray, "IsInstanceOfArray called with non-array EEType");
            Debug.Assert(!pTargetType->IsCloned, "cloned array types are disallowed");

            // if the types match, we are done
            if (pObjType == pTargetType)
            {
                return obj;
            }

            // if the object is not an array, we're done
            if (!pObjType->IsArray)
            {
                return null;
            }

            Debug.Assert(!pObjType->IsCloned, "cloned array types are disallowed");

            // compare the array types structurally

            if (AreTypesAssignableInternal(pObjType->RelatedParameterType, pTargetType->RelatedParameterType, false, true))
                return obj;

            return null;
        }

        [RuntimeExport("RhTypeCast_CheckCastArray")]
        static public unsafe object CheckCastArray(Object obj, void* pvTargetEEType)
        {
            // a null value can be cast to anything
            if (obj == null)
                return null;

            object result = IsInstanceOfArray(obj, pvTargetEEType);

            if (result == null)
            {
                // Throw the invalid cast exception defined by the classlib, using the input EEType* 
                // to find the correct classlib.

                ExceptionIDs exID = ExceptionIDs.InvalidCast;

                IntPtr addr = ((EEType*)pvTargetEEType)->GetAssociatedModuleAddress();
                Exception e = EH.GetClasslibException(exID, addr);

                BinderIntrinsics.TailCall_RhpThrowEx(e);
            }

            return result;
        }

        [RuntimeExport("RhTypeCast_IsInstanceOfInterface")]
        static public unsafe object IsInstanceOfInterface(object obj, void* pvTargetType)
        {
            if (obj == null)
            {
                return null;
            }

            EEType* pTargetType = (EEType*)pvTargetType;
            EEType* pObjType = obj.EEType;

            if (ImplementsInterface(pObjType, pTargetType))
                return obj;

            // If object type implements ICastable then there's one more way to check whether it implements
            // the interface.
            if (pObjType->IsICastable)
            {
                // Call the ICastable.IsInstanceOfInterface method directly rather than via an interface
                // dispatch since we know the method address statically. We ignore any cast error exception
                // object passed back on failure (result == false) since IsInstanceOfInterface never throws.
                IntPtr pfnIsInstanceOfInterface = pObjType->ICastableIsInstanceOfInterfaceMethod;
                Exception castError = null;
                if (CalliIntrinsics.Call<bool>(pfnIsInstanceOfInterface, obj, pTargetType, out castError))
                    return obj;
            }

            return null;
        }

        static internal unsafe bool ImplementsInterface(EEType* pObjType, EEType* pTargetType)
        {
            Debug.Assert(!pTargetType->IsParameterizedType, "did not expect paramterized type");
            Debug.Assert(pTargetType->IsInterface, "IsInstanceOfInterface called with non-interface EEType");

            // This can happen with generic interface types
            // Debug.Assert(!pTargetType->IsCloned, "cloned interface types are disallowed");

            // canonicalize target type
            if (pTargetType->IsCloned)
                pTargetType = pTargetType->CanonicalEEType;

            int numInterfaces = pObjType->NumInterfaces;
            EEInterfaceInfo* interfaceMap = pObjType->InterfaceMap;
            for (int i = 0; i < numInterfaces; i++)
            {
                EEType* pInterfaceType = interfaceMap[i].InterfaceType;

                // canonicalize the interface type
                if (pInterfaceType->IsCloned)
                    pInterfaceType = pInterfaceType->CanonicalEEType;

                if (pInterfaceType == pTargetType)
                {
                    return true;
                }
            }

            // We did not find the interface type in the list of supported interfaces. There's still one
            // chance left: if the target interface is generic and one or more of its type parameters is co or
            // contra variant then the object can still match if it implements a different instantiation of
            // the interface with type compatible generic arguments.
            //
            // An additional edge case occurs because of array covariance. This forces us to treat any generic
            // interfaces implemented by arrays as covariant over their one type parameter.
            bool fArrayCovariance = pObjType->IsArray;
            if (pTargetType->HasGenericVariance || (fArrayCovariance && pTargetType->IsGeneric))
            {
                // Grab details about the instantiation of the target generic interface.
                EETypeRef* pTargetInstantiation;
                int targetArity;
                GenericVariance* pTargetVarianceInfo;
                EEType* pTargetGenericType = InternalCalls.RhGetGenericInstantiation(pTargetType,
                                                                                      &targetArity,
                                                                                      &pTargetInstantiation,
                                                                                      &pTargetVarianceInfo);

                Debug.Assert(pTargetVarianceInfo != null, "did not expect empty variance info");


                for (int i = 0; i < numInterfaces; i++)
                {
                    EEType* pInterfaceType = interfaceMap[i].InterfaceType;

                    // We can ignore interfaces which are not also marked as having generic variance
                    // unless we're dealing with array covariance. 
                    if (pInterfaceType->HasGenericVariance || (fArrayCovariance && pInterfaceType->IsGeneric))
                    {
                        // Grab instantiation details for the candidate interface.
                        EETypeRef* pInterfaceInstantiation;
                        int interfaceArity;
                        GenericVariance* pInterfaceVarianceInfo;
                        EEType* pInterfaceGenericType = InternalCalls.RhGetGenericInstantiation(pInterfaceType,
                                                                                                 &interfaceArity,
                                                                                                 &pInterfaceInstantiation,
                                                                                                 &pInterfaceVarianceInfo);

                        Debug.Assert(pInterfaceVarianceInfo != null, "did not expect empty variance info");

                        // If the generic types aren't the same then the types aren't compatible.
                        if (pInterfaceGenericType != pTargetGenericType)
                            continue;

                        // The types represent different instantiations of the same generic type. The
                        // arity of both had better be the same.
                        Debug.Assert(targetArity == interfaceArity, "arity mismatch betweeen generic instantiations");

                        // Compare the instantiations to see if they're compatible taking variance into account.
                        if (TypeParametersAreCompatible(targetArity,
                                                        pInterfaceInstantiation,
                                                        pTargetInstantiation,
                                                        pTargetVarianceInfo,
                                                        fArrayCovariance))
                            return true;
                    }
                }
            }

            return false;
        }

        // Compare two types to see if they are compatible via generic variance.
        static private unsafe bool TypesAreCompatibleViaGenericVariance(EEType* pSourceType, EEType* pTargetType)
        {
            // Get generic instantiation metadata for both types.

            EETypeRef* pTargetInstantiation;
            int targetArity;
            GenericVariance* pTargetVarianceInfo;
            EEType* pTargetGenericType = InternalCalls.RhGetGenericInstantiation(pTargetType,
                                                                                 &targetArity,
                                                                                 &pTargetInstantiation,
                                                                                 &pTargetVarianceInfo);
            Debug.Assert(pTargetVarianceInfo != null, "did not expect empty variance info");

            EETypeRef* pSourceInstantiation;
            int sourceArity;
            GenericVariance* pSourceVarianceInfo;
            EEType* pSourceGenericType = InternalCalls.RhGetGenericInstantiation(pSourceType,
                                                                                 &sourceArity,
                                                                                 &pSourceInstantiation,
                                                                                 &pSourceVarianceInfo);
            Debug.Assert(pSourceVarianceInfo != null, "did not expect empty variance info");

            // If the generic types aren't the same then the types aren't compatible.
            if (pSourceGenericType == pTargetGenericType)
            {
                // The types represent different instantiations of the same generic type. The
                // arity of both had better be the same.
                Debug.Assert(targetArity == sourceArity, "arity mismatch betweeen generic instantiations");

                // Compare the instantiations to see if they're compatible taking variance into account.
                if (TypeParametersAreCompatible(targetArity,
                                                pSourceInstantiation,
                                                pTargetInstantiation,
                                                pTargetVarianceInfo,
                                                false))
                {
                    return true;
                }
            }

            return false;
        }

        // Compare two sets of generic type parameters to see if they're assignment compatible taking generic
        // variance into account. It's assumed they've already had their type definition matched (which
        // implies their arities are the same as well). The fForceCovariance argument tells the method to
        // override the defined variance of each parameter and instead assume it is covariant. This is used to
        // implement covariant array interfaces.
        static internal unsafe bool TypeParametersAreCompatible(int arity,
                                                               EETypeRef* pSourceInstantiation,
                                                               EETypeRef* pTargetInstantiation,
                                                               GenericVariance* pVarianceInfo,
                                                               bool fForceCovariance)
        {
            // Walk through the instantiations comparing the cast compatibility of each pair
            // of type args.
            for (int i = 0; i < arity; i++)
            {
                EEType* pTargetArgType = pTargetInstantiation[i].Value;
                EEType* pSourceArgType = pSourceInstantiation[i].Value;

                GenericVariance varType;
                if (fForceCovariance)
                    varType = GenericVariance.ArrayCovariant;
                else
                    varType = pVarianceInfo[i];

                switch (varType)
                {
                    case GenericVariance.NonVariant:
                        // Non-variant type params need to be identical.

                        if (!AreTypesEquivalentInternal(pSourceArgType, pTargetArgType))
                            return false;

                        break;

                    case GenericVariance.Covariant:
                        // For covariance (or out type params in C#) the object must implement an
                        // interface with a more derived type arg than the target interface. Or
                        // the object interface can have a type arg that is an interface
                        // implemented by the target type arg.
                        // For instance:
                        //   class Foo : ICovariant<String> is ICovariant<Object>
                        //   class Foo : ICovariant<Bar> is ICovariant<IBar>
                        //   class Foo : ICovariant<IBar> is ICovariant<Object>

                        if (!AreTypesAssignableInternal(pSourceArgType, pTargetArgType, false, false))
                            return false;

                        break;

                    case GenericVariance.ArrayCovariant:
                        // For array covariance the object must be an array with a type arg
                        // that is more derived than that the target interface, or be a primitive
                        // (or enum) with the same size.
                        // For instance:
                        //   string[,,] is object[,,]
                        //   int[,,] is uint[,,]

                        // This call is just like the call for Covariance above except true is passed
                        // to the fAllowSizeEquivalence parameter to allow the int/uint matching to work
                        if (!AreTypesAssignableInternal(pSourceArgType, pTargetArgType, false, true))
                            return false;

                        break;

                    case GenericVariance.Contravariant:
                        // For contravariance (or in type params in C#) the object must implement
                        // an interface with a less derived type arg than the target interface. Or
                        // the object interface can have a type arg that is a class implementing
                        // the interface that is the target type arg.
                        // For instance:
                        //   class Foo : IContravariant<Object> is IContravariant<String>
                        //   class Foo : IContravariant<IBar> is IContravariant<Bar>
                        //   class Foo : IContravariant<Object> is IContravariant<IBar>

                        if (!AreTypesAssignableInternal(pTargetArgType, pSourceArgType, false, false))
                            return false;

                        break;

                    default:
                        Debug.Assert(false, "unknown generic variance type");
                        break;
                }
            }

            return true;
        }

        //
        // Determines if a value of the source type can be assigned to a location of the target type.
        // It does not handle ICastable, and cannot since we do not have an actual object instance here.
        // This routine assumes that the source type is boxed, i.e. a value type source is presumed to be
        // compatible with Object and ValueType and an enum source is additionally compatible with Enum.
        //
        [RuntimeExport("RhTypeCast_AreTypesAssignable")]
        static public unsafe bool AreTypesAssignable(void* pvSourceType, void* pvTargetType)
        {
            EEType* pSourceType = (EEType*)pvSourceType;
            EEType* pTargetType = (EEType*)pvTargetType;

            // Special case: Generic Type definitions are not assignable in a mrt sense
            // in any way. Assignability of those types is handled by reflection logic.
            // Call this case out first and here so that these only somewhat filled in
            // types do not leak into the rest of the type casting logic.
            if (pTargetType->IsGenericTypeDefinition || pSourceType->IsGenericTypeDefinition)
            {
                return false;
            }

            // Special case: T can be cast to Nullable<T> (where T is a value type). Call this case out here
            // since this is only applicable if T is boxed, which is not true for any other callers of
            // AreTypesAssignableInternal, so no sense making all the other paths pay the cost of the check.
            if (pTargetType->IsNullable && pSourceType->IsValueType && !pSourceType->IsNullable)
            {
                EEType* pNullableType = pTargetType->GetNullableType();

                return AreTypesEquivalentInternal(pSourceType, pNullableType);
            }

            return AreTypesAssignableInternal(pSourceType, pTargetType, true, false);
        }

        // Internally callable version of the export method above. Has two additional parameters:
        //  fBoxedSource            : assume the source type is boxed so that value types and enums are
        //                            compatible with Object, ValueType and Enum (if applicable)
        //  fAllowSizeEquivalence   : allow identically sized integral types and enums to be considered
        //                            equivalent (currently used only for array element types)
        static internal unsafe bool AreTypesAssignableInternal(EEType* pSourceType, EEType* pTargetType, bool fBoxedSource, bool fAllowSizeEquivalence)
        {
            //
            // Are the types identical?
            //
            if (AreTypesEquivalentInternal(pSourceType, pTargetType))
                return true;

            //
            // Handle cast to interface cases.
            //
            if (pTargetType->IsInterface)
            {
                // Value types can only be cast to interfaces if they're boxed.
                if (!fBoxedSource && pSourceType->IsValueType)
                    return false;

                if (ImplementsInterface(pSourceType, pTargetType))
                    return true;

                // Are the types compatible due to generic variance?
                if (pTargetType->HasGenericVariance && pSourceType->HasGenericVariance)
                    return TypesAreCompatibleViaGenericVariance(pSourceType, pTargetType);

                return false;
            }
            if (pSourceType->IsInterface)
            {
                // The only non-interface type an interface can be cast to is Object.
                return WellKnownEETypes.IsSystemObject(pTargetType);
            }

            //
            // Handle cast to array or pointer cases.
            //
            if (pTargetType->IsParameterizedType)
            {
                if (pSourceType->IsParameterizedType && (pTargetType->ParameterizedTypeShape == pSourceType->ParameterizedTypeShape))
                {
                    // Source type is also a parameterized type. Are the parameter types compatible? Note that using
                    // AreTypesAssignableInternal here handles array covariance as well as IFoo[] -> Foo[]
                    // etc. Pass false for fBoxedSource since int[] is not assignable to object[].
                    if (pSourceType->RelatedParameterType->IsPointerTypeDefinition)
                    {
                        // If the parameter types are pointers, then only exact matches are correct.
                        // As we've already called AreTypesEquivalent at the start of this function,
                        // return false as the exact match case has already been handled.
                        // int** is not compatible with uint**, nor is int*[] oompatible with uint*[].
                        return false;
                    }
                    else
                    {
                        return AreTypesAssignableInternal(pSourceType->RelatedParameterType, pTargetType->RelatedParameterType, false, true);
                    }
                }

                // Can't cast a non-parameter type to a parameter type or a parameter type of different shape to a parameter type
                return false;
            }
            if (pSourceType->IsArray)
            {
                // Target type is not an array. But we can still cast arrays to Object or System.Array.
                return WellKnownEETypes.IsSystemObject(pTargetType) || WellKnownEETypes.IsSystemArray(pTargetType);
            }
            else if (pSourceType->IsParameterizedType)
            {
                return false;
            }

            //
            // Handle cast to other (non-interface, non-array) cases.
            //

            if (pSourceType->IsValueType)
            {
                // Certain value types of the same size are treated as equivalent when the comparison is
                // between array element types (indicated by fAllowSizeEquivalence). These are integer types
                // of the same size (e.g. int and uint) and the base type of enums vs all integer types of the
                // same size.
                if (fAllowSizeEquivalence && pTargetType->IsValueType)
                {
                    if (ArePrimitveTypesEquivalentSize(pSourceType, pTargetType))
                        return true;

                    // Non-identical value types aren't equivalent in any other case (since value types are
                    // sealed).
                    return false;
                }

                // If the source type is a value type but it's not boxed then we've run out of options: the types
                // are not identical, the target type isn't an interface and we're not allowed to check whether
                // the target type is a parent of this one since value types are sealed and thus the only matches
                // would be against Object, ValueType or Enum, all of which are reference types and not compatible
                // with non-boxed value types.
                if (!fBoxedSource)
                    return false;
            }

            // Sub case of casting between two instantiations of the same delegate type where one or more of
            // the type parameters have variance. Only interfaces and delegate types can have variance over
            // their type parameters and we know that neither type is an interface due to checks above.
            if (pTargetType->HasGenericVariance && pSourceType->HasGenericVariance)
            {
                // We've dealt with the identical case at the start of this method. And the regular path below
                // will handle casting to Object, Delegate and MulticastDelegate. Since we don't support
                // deriving from user delegate classes any further all we have to check here is that the
                // uninstantiated generic delegate definitions are the same and the type parameters are
                // compatible.
                return TypesAreCompatibleViaGenericVariance(pSourceType, pTargetType);
            }

            // Is the source type derived from the target type?
            if (IsDerived(pSourceType, pTargetType))
                return true;

            return false;
        }

        [RuntimeExport("RhTypeCast_CheckCastInterface")]
        static public unsafe object CheckCastInterface(Object obj, void* pvTargetEEType)
        {
            // a null value can be cast to anything
            if (obj == null)
            {
                return null;
            }

            EEType* pTargetType = (EEType*)pvTargetEEType;
            EEType* pObjType = obj.EEType;

            if (ImplementsInterface(pObjType, pTargetType))
                return obj;

            Exception castError = null;

            // If object type implements ICastable then there's one more way to check whether it implements
            // the interface.
            if (pObjType->IsICastable)
            {
                // Call the ICastable.IsInstanceOfInterface method directly rather than via an interface
                // dispatch since we know the method address statically.
                IntPtr pfnIsInstanceOfInterface = pObjType->ICastableIsInstanceOfInterfaceMethod;
                if (CalliIntrinsics.Call<bool>(pfnIsInstanceOfInterface, obj, pTargetType, out castError))
                    return obj;
            }

            // Throw the invalid cast exception defined by the classlib, using the input EEType* to find the
            // correct classlib unless ICastable.IsInstanceOfInterface returned a more specific exception for
            // us to use.

            IntPtr addr = ((EEType*)pvTargetEEType)->GetAssociatedModuleAddress();
            if (castError == null)
                castError = EH.GetClasslibException(ExceptionIDs.InvalidCast, addr);

            BinderIntrinsics.TailCall_RhpThrowEx(castError);
            throw castError;
        }

        [RuntimeExport("RhTypeCast_CheckArrayStore")]
        static public unsafe void CheckArrayStore(object array, object obj)
        {
            if (array == null || obj == null)
            {
                return;
            }

            Debug.Assert(array.EEType->IsArray, "first argument must be an array");

            EEType* arrayElemType = array.EEType->RelatedParameterType;
            bool compatible;
            if (arrayElemType->IsInterface)
            {
                compatible = IsInstanceOfInterface(obj, arrayElemType) != null;
            }
            else if (arrayElemType->IsArray)
            {
                compatible = IsInstanceOfArray(obj, arrayElemType) != null;
            }
            else
            {
                compatible = IsInstanceOfClass(obj, arrayElemType) != null;
            }

            if (!compatible)
            {
                // Throw the array type mismatch exception defined by the classlib, using the input array's EEType* 
                // to find the correct classlib.

                ExceptionIDs exID = ExceptionIDs.ArrayTypeMismatch;

                IntPtr addr = array.EEType->GetAssociatedModuleAddress();
                Exception e = EH.GetClasslibException(exID, addr);

                BinderIntrinsics.TailCall_RhpThrowEx(e);
            }
        }


        [RuntimeExport("RhTypeCast_CheckVectorElemAddr")]
        static public unsafe void CheckVectorElemAddr(void* pvElemType, object array)
        {
            if (array == null)
            {
                return;
            }

            Debug.Assert(array.EEType->IsArray, "second argument must be an array");

            EEType* elemType = (EEType*)pvElemType;
            EEType* arrayElemType = array.EEType->RelatedParameterType;

            if (!AreTypesEquivalentInternal(elemType, arrayElemType))
            {
                // Throw the array type mismatch exception defined by the classlib, using the input array's EEType* 
                // to find the correct classlib.
                ExceptionIDs exID = ExceptionIDs.ArrayTypeMismatch;

                IntPtr addr = array.EEType->GetAssociatedModuleAddress();
                Exception e = EH.GetClasslibException(exID, addr);

                BinderIntrinsics.TailCall_RhpThrowEx(e);
            }
        }


        static internal unsafe bool IsDerived(EEType* pDerivedType, EEType* pBaseType)
        {
            Debug.Assert(!pDerivedType->IsArray, "did not expect array type");
            Debug.Assert(!pDerivedType->IsParameterizedType, "did not expect parameterType");
            Debug.Assert(!pBaseType->IsArray, "did not expect array type");
            Debug.Assert(!pBaseType->IsInterface, "did not expect interface type");
            Debug.Assert(!pBaseType->IsParameterizedType, "did not expect parameterType");
            Debug.Assert(pBaseType->IsCanonical || pBaseType->IsCloned || pBaseType->IsGenericTypeDefinition, "unexpected eetype");
            Debug.Assert(pDerivedType->IsCanonical || pDerivedType->IsCloned || pDerivedType->IsGenericTypeDefinition, "unexpected eetype");

            // If a generic type definition reaches this function, then the function should return false unless the types are equivalent.
            // This works as the NonClonedNonArrayBaseType of a GenericTypeDefinition is always null.

            if (pBaseType->IsCloned)
                pBaseType = pBaseType->CanonicalEEType;

            do
            {
                if (pDerivedType->IsCloned)
                    pDerivedType = pDerivedType->CanonicalEEType;

                if (pDerivedType == pBaseType)
                    return true;

                pDerivedType = pDerivedType->NonClonedNonArrayBaseType;
            }
            while (pDerivedType != null);

            return false;
        }


        [RuntimeExport("RhTypeCast_AreTypesEquivalent")]
        static unsafe public bool AreTypesEquivalent(EETypePtr pType1, EETypePtr pType2)
        {
            return (AreTypesEquivalentInternal(pType1.ToPointer(), pType2.ToPointer()));
        }

        // Method to compare two types pointers for type equality
        // We cannot just compare the pointers as there can be duplicate type instances
        // for cloned and constructed types.
        // There are three separate cases here
        //   1. The pointers are Equal => true
        //   2. Either one or both the types are CLONED, follow to the canonical EEType and check
        //   3. For Arrays/Pointers, we have to further check for rank and element type equality
        static private unsafe bool AreTypesEquivalentInternal(EEType* pType1, EEType* pType2)
        {
            if (pType1 == pType2)
                return true;

            if (pType1->IsCloned)
                pType1 = pType1->CanonicalEEType;

            if (pType2->IsCloned)
                pType2 = pType2->CanonicalEEType;

            if (pType1 == pType2)
                return true;

            if (pType1->IsParameterizedType && pType2->IsParameterizedType)
                return AreTypesEquivalentInternal(pType1->RelatedParameterType, pType2->RelatedParameterType) && pType1->ParameterizedTypeShape == pType2->ParameterizedTypeShape;

            return false;
        }

        // this is necessary for shared generic code - Foo<T> may be executing
        // for T being an interface, an array or a class
        [RuntimeExport("RhTypeCast_IsInstanceOf")]
        static public unsafe object IsInstanceOf(object obj, void* pvTargetType)
        {
            EEType* pTargetType = (EEType*)pvTargetType;
            if (pTargetType->IsArray)
                return IsInstanceOfArray(obj, pvTargetType);
            else if (pTargetType->IsInterface)
                return IsInstanceOfInterface(obj, pvTargetType);
            else
                return IsInstanceOfClass(obj, pvTargetType);
        }

        [RuntimeExport("RhTypeCast_CheckCast")]
        static public unsafe object CheckCast(Object obj, void* pvTargetType)
        {
            EEType* pTargetType = (EEType*)pvTargetType;
            if (pTargetType->IsArray)
                return CheckCastArray(obj, pvTargetType);
            else if (pTargetType->IsInterface)
                return CheckCastInterface(obj, pvTargetType);
            else
                return CheckCastClass(obj, pvTargetType);
        }

        // Returns true of the two types are equivalent primitive types. Used by array casts.
        static private unsafe bool ArePrimitveTypesEquivalentSize(EEType* pType1, EEType* pType2)
        {
            CorElementType sourceCorType = pType1->CorElementType;
            int sourcePrimitiveTypeEquivalenceSize = GetIntegralTypeMatchSize(sourceCorType);

            // Quick check to see if the first type is even primitive.
            if (sourcePrimitiveTypeEquivalenceSize == 0)
                return false;

            CorElementType targetCorType = pType2->CorElementType;
            int targetPrimitiveTypeEquivalenceSize = GetIntegralTypeMatchSize(targetCorType);

            return sourcePrimitiveTypeEquivalenceSize == targetPrimitiveTypeEquivalenceSize;
        }

        private unsafe static int GetIntegralTypeMatchSize(CorElementType corType)
        {
            switch (corType)
            {
                case CorElementType.ELEMENT_TYPE_I1:
                case CorElementType.ELEMENT_TYPE_U1:
                    return 1;
                case CorElementType.ELEMENT_TYPE_I2:
                case CorElementType.ELEMENT_TYPE_U2:
                    return 2;
                case CorElementType.ELEMENT_TYPE_I4:
                case CorElementType.ELEMENT_TYPE_U4:
                    return 4;
                case CorElementType.ELEMENT_TYPE_I8:
                case CorElementType.ELEMENT_TYPE_U8:
                    return 8;
                case CorElementType.ELEMENT_TYPE_I:
                case CorElementType.ELEMENT_TYPE_U:
                    return sizeof(IntPtr);
                default:
                    return 0;
            }
        }

        // copied from CorHdr.h
        internal enum CorElementType : byte
        {
            ELEMENT_TYPE_END = 0x0,
            ELEMENT_TYPE_VOID = 0x1,
            ELEMENT_TYPE_BOOLEAN = 0x2,
            ELEMENT_TYPE_CHAR = 0x3,
            ELEMENT_TYPE_I1 = 0x4,
            ELEMENT_TYPE_U1 = 0x5,
            ELEMENT_TYPE_I2 = 0x6,
            ELEMENT_TYPE_U2 = 0x7,
            ELEMENT_TYPE_I4 = 0x8,
            ELEMENT_TYPE_U4 = 0x9,
            ELEMENT_TYPE_I8 = 0xa,
            ELEMENT_TYPE_U8 = 0xb,
            ELEMENT_TYPE_R4 = 0xc,
            ELEMENT_TYPE_R8 = 0xd,
            ELEMENT_TYPE_STRING = 0xe,

            // every type above PTR will be simple type
            ELEMENT_TYPE_PTR = 0xf,      // PTR <type>
            ELEMENT_TYPE_BYREF = 0x10,     // BYREF <type>

            // Please use ELEMENT_TYPE_VALUETYPE. ELEMENT_TYPE_VALUECLASS is deprecated.
            ELEMENT_TYPE_VALUETYPE = 0x11,     // VALUETYPE <class Token>
            ELEMENT_TYPE_CLASS = 0x12,     // CLASS <class Token>
            ELEMENT_TYPE_VAR = 0x13,     // a class type variable VAR <U1>
            ELEMENT_TYPE_ARRAY = 0x14,     // MDARRAY <type> <rank> <bcount> <bound1> ... <lbcount> <lb1> ...
            ELEMENT_TYPE_GENERICINST = 0x15,     // GENERICINST <generic type> <argCnt> <arg1> ... <argn>
            ELEMENT_TYPE_TYPEDBYREF = 0x16,     // TYPEDREF  (it takes no args) a typed referece to some other type

            ELEMENT_TYPE_I = 0x18,     // native integer size
            ELEMENT_TYPE_U = 0x19,     // native unsigned integer size
            ELEMENT_TYPE_FNPTR = 0x1B,     // FNPTR <complete sig for the function including calling convention>
            ELEMENT_TYPE_OBJECT = 0x1C,     // Shortcut for System.Object
            ELEMENT_TYPE_SZARRAY = 0x1D,     // Shortcut for single dimension zero lower bound array
            // SZARRAY <type>
            ELEMENT_TYPE_MVAR = 0x1e,     // a method type variable MVAR <U1>

            // This is only for binding
            ELEMENT_TYPE_CMOD_REQD = 0x1F,     // required C modifier : E_T_CMOD_REQD <mdTypeRef/mdTypeDef>
            ELEMENT_TYPE_CMOD_OPT = 0x20,     // optional C modifier : E_T_CMOD_OPT <mdTypeRef/mdTypeDef>

            // This is for signatures generated internally (which will not be persisted in any way).
            ELEMENT_TYPE_INTERNAL = 0x21,     // INTERNAL <typehandle>

            // Note that this is the max of base type excluding modifiers
            ELEMENT_TYPE_MAX = 0x22,     // first invalid element type


            ELEMENT_TYPE_MODIFIER = 0x40,
            ELEMENT_TYPE_SENTINEL = 0x01 | ELEMENT_TYPE_MODIFIER, // sentinel for varargs
            ELEMENT_TYPE_PINNED = 0x05 | ELEMENT_TYPE_MODIFIER,
        }
    }
}
