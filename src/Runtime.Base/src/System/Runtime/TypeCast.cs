// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Internal.Runtime;

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
        [Flags]
        internal enum AssignmentVariation
        {
            Normal = 0,

            /// <summary>
            /// Assume the source type is boxed so that value types and enums are compatible with Object, ValueType 
            /// and Enum (if applicable)
            /// </summary>
            BoxedSource = 1,

            /// <summary>
            /// Allow identically sized integral types and enums to be considered equivalent (currently used only for 
            /// array element types)
            /// </summary>
            AllowSizeEquivalence = 2,
        }

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
            if (Internal.Runtime.EEType.BothSimpleCasting(pObjType, pTargetType))
            {
                // walk the type hierarchy looking for a match
                do
                {
                    pObjType = pObjType->RawBaseType;

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

                // NOTE: using general assignable path for the cache because of the cost of the variance checks
                if (CastCache.AreTypesAssignableInternal(pObjType, pTargetType, AssignmentVariation.BoxedSource))
                    return obj;
                return null;
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

                throw ((EEType*)pvTargetEEType)->GetClasslibException(ExceptionIDs.InvalidCast);
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

            throw obj.EEType->GetClasslibException(ExceptionIDs.InvalidCast);
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

            if (pObjType->ParameterizedTypeShape == pTargetType->ParameterizedTypeShape &&
                CastCache.AreTypesAssignableInternal(pObjType->RelatedParameterType, pTargetType->RelatedParameterType,
                AssignmentVariation.AllowSizeEquivalence))
            {
                return obj;
            }

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

                throw ((EEType*)pvTargetEEType)->GetClasslibException(ExceptionIDs.InvalidCast);
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

            if (CastCache.AreTypesAssignableInternal(pObjType, pTargetType, AssignmentVariation.BoxedSource))
                return obj;

            // If object type implements ICastable then there's one more way to check whether it implements
            // the interface.
            if (pObjType->IsICastable && IsInstanceOfInterfaceViaCastableObject(obj, pTargetType))
                return obj;

            return null;
        }

        unsafe static bool IsInstanceOfInterfaceViaCastableObject(object obj, EEType* pTargetType)
        {
            // To avoid stack overflow, it is not possible to implement the ICastableObject interface
            // itself via ICastableObject
            if (pTargetType->IsICastable)
                return false;

            // TODO!! BEGIN REMOVE THIS CODE WHEN WE REMOVE ICASTABLE
            // Call the ICastable.IsInstanceOfInterface method directly rather than via an interface
            // dispatch since we know the method address statically. We ignore any cast error exception
            // object passed back on failure (result == false) since IsInstanceOfInterface never throws.
            IntPtr pfnIsInstanceOfInterface = obj.EEType->ICastableIsInstanceOfInterfaceMethod;
            Exception castError = null;
            if (CalliIntrinsics.Call<bool>(pfnIsInstanceOfInterface, obj, pTargetType, out castError))
                return true;

            if (obj is CastableObjectSupport.ICastableObject)
            {
            // TODO!! END REMOVE THIS CODE WHEN WE REMOVE ICASTABLE

            // We ignore any cast error exception
            // object passed back on failure (result == false) since IsInstanceOfInterface never throws.
            CastableObjectSupport.ICastableObject castableObject = (CastableObjectSupport.ICastableObject)obj;
            Exception castableObjectCastError = null;
            if (CastableObjectSupport.GetCastableTargetIfPossible(castableObject, pTargetType, false, ref castableObjectCastError) != null)
                return true;

            // TODO!! BEGIN REMOVE THIS CODE WHEN WE REMOVE ICASTABLE
            }
            // TODO!! END REMOVE THIS CODE WHEN WE REMOVE ICASTABLE
            return false;
        }

        unsafe static bool IsInstanceOfInterfaceViaCastableObjectWithException(object obj, EEType* pTargetType, ref Exception castError)
        {
            // TODO!! BEGIN REMOVE THIS CODE WHEN WE REMOVE ICASTABLE
            // Call the ICastable.IsInstanceOfInterface method directly rather than via an interface
            // dispatch since we know the method address statically.
            IntPtr pfnIsInstanceOfInterface = obj.EEType->ICastableIsInstanceOfInterfaceMethod;
            if (CalliIntrinsics.Call<bool>(pfnIsInstanceOfInterface, obj, pTargetType, out castError))
                return true;
            if (obj is CastableObjectSupport.ICastableObject)
            {
            // TODO!! END REMOVE THIS CODE WHEN WE REMOVE ICASTABLE

            CastableObjectSupport.ICastableObject castableObject = (CastableObjectSupport.ICastableObject)obj;
            return CastableObjectSupport.GetCastableTargetIfPossible(castableObject, pTargetType, true, ref castError) != null;
            // TODO!! BEGIN REMOVE THIS CODE WHEN WE REMOVE ICASTABLE
            }
            return false;
            // TODO!! END REMOVE THIS CODE WHEN WE REMOVE ICASTABLE
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
            // Interfaces which are only variant for arrays have the HasGenericVariance flag set even if they
            // are not variant.
            bool fArrayCovariance = pObjType->IsArray;
            if (pTargetType->HasGenericVariance)
            {
                // Grab details about the instantiation of the target generic interface.
                EEType* pTargetGenericType = pTargetType->GenericDefinition;
                EETypeRef* pTargetInstantiation = pTargetType->GenericArguments;
                int targetArity = (int)pTargetType->GenericArity;
                GenericVariance* pTargetVarianceInfo = pTargetType->GenericVariance;

                Debug.Assert(pTargetVarianceInfo != null, "did not expect empty variance info");


                for (int i = 0; i < numInterfaces; i++)
                {
                    EEType* pInterfaceType = interfaceMap[i].InterfaceType;

                    // We can ignore interfaces which are not also marked as having generic variance
                    // unless we're dealing with array covariance. 
                    //
                    // Interfaces which are only variant for arrays have the HasGenericVariance flag set even if they
                    // are not variant.
                    if (pInterfaceType->HasGenericVariance)
                    {
                        EEType* pInterfaceGenericType = pInterfaceType->GenericDefinition;

                        // If the generic types aren't the same then the types aren't compatible.
                        if (pInterfaceGenericType != pTargetGenericType)
                            continue;

                        // Grab instantiation details for the candidate interface.
                        EETypeRef* pInterfaceInstantiation = pInterfaceType->GenericArguments;
                        int interfaceArity = (int)pInterfaceType->GenericArity;
                        GenericVariance* pInterfaceVarianceInfo = pInterfaceType->GenericVariance;

                        Debug.Assert(pInterfaceVarianceInfo != null, "did not expect empty variance info");

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

            // Interface type equivalence check.
            // Currently only implemented to allow ICastable to be defined in multiple type spaces
            if (pTargetType->IsICastable)
            {
                for (int i = 0; i < numInterfaces; i++)
                {
                    EEType* pInterfaceType = interfaceMap[i].InterfaceType;
                    if (pInterfaceType->IsICastable)
                        return true;
                }
            }

            return false;
        }

        // Compare two types to see if they are compatible via generic variance.
        static private unsafe bool TypesAreCompatibleViaGenericVariance(EEType* pSourceType, EEType* pTargetType)
        {
            EEType* pTargetGenericType = pTargetType->GenericDefinition;
            EEType* pSourceGenericType = pSourceType->GenericDefinition;

            // If the generic types aren't the same then the types aren't compatible.
            if (pSourceGenericType == pTargetGenericType)
            {
                // Get generic instantiation metadata for both types.

                EETypeRef* pTargetInstantiation = pTargetType->GenericArguments;
                int targetArity = (int)pTargetType->GenericArity;
                GenericVariance* pTargetVarianceInfo = pTargetType->GenericVariance;

                Debug.Assert(pTargetVarianceInfo != null, "did not expect empty variance info");

                EETypeRef* pSourceInstantiation = pSourceType->GenericArguments;
                int sourceArity = (int)pSourceType->GenericArity;
                GenericVariance* pSourceVarianceInfo = pSourceType->GenericVariance;

                Debug.Assert(pSourceVarianceInfo != null, "did not expect empty variance info");

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

                        if (!CastCache.AreTypesAssignableInternal(pSourceArgType, pTargetArgType, AssignmentVariation.Normal))
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
                        if (!CastCache.AreTypesAssignableInternal(pSourceArgType, pTargetArgType, AssignmentVariation.AllowSizeEquivalence))
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

                        if (!CastCache.AreTypesAssignableInternal(pTargetArgType, pSourceArgType, AssignmentVariation.Normal))
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
                EEType* pNullableType = pTargetType->NullableType;

                return AreTypesEquivalentInternal(pSourceType, pNullableType);
            }

            return CastCache.AreTypesAssignableInternal(pSourceType, pTargetType, AssignmentVariation.BoxedSource);
        }

        // Internally callable version of the export method above. Has two additional flags:
        //  fBoxedSource            : assume the source type is boxed so that value types and enums are
        //                            compatible with Object, ValueType and Enum (if applicable)
        //  fAllowSizeEquivalence   : allow identically sized integral types and enums to be considered
        //                            equivalent (currently used only for array element types)
        static internal unsafe bool AreTypesAssignableInternal(EEType* pSourceType, EEType* pTargetType, AssignmentVariation variation)
        {
            bool fBoxedSource = ((variation & AssignmentVariation.BoxedSource) == AssignmentVariation.BoxedSource);
            bool fAllowSizeEquivalence = ((variation & AssignmentVariation.AllowSizeEquivalence ) == AssignmentVariation.AllowSizeEquivalence);

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
                if (pSourceType->IsParameterizedType 
                    && (pTargetType->ParameterizedTypeShape == pSourceType->ParameterizedTypeShape))
                {
                    // Source type is also a parameterized type. Are the parameter types compatible? 
                    if (pSourceType->RelatedParameterType->IsPointerType)
                    {
                        // If the parameter types are pointers, then only exact matches are correct.
                        // As we've already called AreTypesEquivalent at the start of this function,
                        // return false as the exact match case has already been handled.
                        // int** is not compatible with uint**, nor is int*[] oompatible with uint*[].
                        return false;
                    }
                    else if (pSourceType->RelatedParameterType->IsByRefType)
                    {
                        // Only allow exact matches for ByRef types - same as pointers above. This should
                        // be unreachable and it's only a defense in depth. ByRefs can't be parameters
                        // of any parameterized type.
                        return false;
                    }
                    else
                    {
                        // Note that using AreTypesAssignableInternal with AssignmentVariation.AllowSizeEquivalence 
                        // here handles array covariance as well as IFoo[] -> Foo[] etc.  We are not using 
                        // AssignmentVariation.BoxedSource because int[] is not assignable to object[].
                        return CastCache.AreTypesAssignableInternal(pSourceType->RelatedParameterType, 
                            pTargetType->RelatedParameterType, AssignmentVariation.AllowSizeEquivalence);
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

            if (CastCache.AreTypesAssignableInternal(pObjType, pTargetType, AssignmentVariation.BoxedSource))
                return obj;

            Exception castError = null;

            // If object type implements ICastable then there's one more way to check whether it implements
            // the interface.
            if (pObjType->IsICastable)
            {
                if (IsInstanceOfInterfaceViaCastableObjectWithException(obj, pTargetType, ref castError))
                    return obj;
            }

            // Throw the invalid cast exception defined by the classlib, using the input EEType* to find the
            // correct classlib unless ICastable.IsInstanceOfInterface returned a more specific exception for
            // us to use.

            if (castError == null)
                castError = ((EEType*)pvTargetEEType)->GetClasslibException(ExceptionIDs.InvalidCast);

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
            if (CastCache.AreTypesAssignableInternal(obj.EEType, arrayElemType, AssignmentVariation.BoxedSource))
                return;

            // If object type implements ICastable then there's one more way to check whether it implements
            // the interface.
            if (obj.EEType->IsICastable && IsInstanceOfInterfaceViaCastableObject(obj, arrayElemType))
                return;

            // Throw the array type mismatch exception defined by the classlib, using the input array's EEType* 
            // to find the correct classlib.

            throw array.EEType->GetClasslibException(ExceptionIDs.ArrayTypeMismatch);
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

            if (!AreTypesEquivalentInternal(elemType, arrayElemType)
            // In addition to the exactness check, add another check to allow non-exact matches through
            // if the element type is a ValueType. The issue here is Universal Generics. The Universal
            // Generic codegen will generate a call to this helper for all ldelema opcodes if the exact
            // type is not known, and this can include ValueTypes. For ValueTypes, the exact check is not
            // desireable as enum's are allowed to pass through this code if they are size matched.
            // While this check is overly broad and allows non-enum valuetypes to also skip the check
            // that is OK, because in the non-enum case the casting operations are sufficient to ensure
            // type safety.
                && !elemType->IsValueType)
            {
                // Throw the array type mismatch exception defined by the classlib, using the input array's EEType* 
                // to find the correct classlib.

                throw array.EEType->GetClasslibException(ExceptionIDs.ArrayTypeMismatch);
            }
        }

        //
        // Array stelem/ldelema helpers with RyuJIT conventions
        //
        [RuntimeExport("RhpStelemRef")]
        static public unsafe void StelemRef(Array array, int index, object obj)
        {
            // This is supported only on arrays
            Debug.Assert(array.EEType->IsArray, "first argument must be an array");

            if (index >= array.Length)
            {
                throw array.EEType->GetClasslibException(ExceptionIDs.IndexOutOfRange);
            }

            if (obj != null)
            {
                EEType* arrayElemType = array.EEType->RelatedParameterType;

                if (!CastCache.AreTypesAssignableInternal(obj.EEType, arrayElemType, AssignmentVariation.BoxedSource))
                {
                    // If object type implements ICastable then there's one more way to check whether it implements
                    // the interface.
                    if (!obj.EEType->IsICastable || !IsInstanceOfInterfaceViaCastableObject(obj, arrayElemType))
                    {
                        // Throw the array type mismatch exception defined by the classlib, using the input array's 
                        // EEType* to find the correct classlib.

                        throw array.EEType->GetClasslibException(ExceptionIDs.ArrayTypeMismatch);
                    }
                }

                // Both bounds and type check are ok.

                // Call write barrier directly. Assigning object reference would call slower checked write barrier.
                ref Object rawData = ref Unsafe.As<byte, Object>(ref array.GetRawSzArrayData());
                InternalCalls.RhpAssignRef(ref Unsafe.Add(ref rawData, index), obj);
            }
            else
            {
                // Storing null does not require write barrier
                ref IntPtr rawData = ref Unsafe.As<byte, IntPtr>(ref array.GetRawSzArrayData());
                Unsafe.Add(ref rawData, index) = default(IntPtr);
            }
        }

        [RuntimeExport("RhpLdelemaRef")]
        static public unsafe ref Object LdelemaRef(Array array, int index, IntPtr elementType)
        {
            Debug.Assert(array.EEType->IsArray, "first argument must be an array");

            EEType* elemType = (EEType*)elementType;
            EEType* arrayElemType = array.EEType->RelatedParameterType;

            if (!AreTypesEquivalentInternal(elemType, arrayElemType))
            {
                // Throw the array type mismatch exception defined by the classlib, using the input array's EEType* 
                // to find the correct classlib.

                throw array.EEType->GetClasslibException(ExceptionIDs.ArrayTypeMismatch);
            }

            ref Object rawData = ref Unsafe.As<byte, Object>(ref array.GetRawSzArrayData());
            return ref Unsafe.Add(ref rawData, index);
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
        static internal unsafe bool AreTypesEquivalentInternal(EEType* pType1, EEType* pType2)
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

        [RuntimeExport("RhTypeCast_IsInstanceOf2")]  // Helper with RyuJIT calling convention
        static public unsafe object IsInstanceOf2(void* pvTargetType, object obj)
        {
            return IsInstanceOf(obj, pvTargetType);
        }

        // this is necessary for shared generic code - Foo<T> may be executing
        // for T being an interface, an array or a class
        [RuntimeExport("RhTypeCast_IsInstanceOf")]
        static public unsafe object IsInstanceOf(object obj, void* pvTargetType)
        {
            // @TODO: consider using the cache directly, but beware of ICastable in the interface case
            EEType* pTargetType = (EEType*)pvTargetType;
            if (pTargetType->IsArray)
                return IsInstanceOfArray(obj, pvTargetType);
            else if (pTargetType->IsInterface)
                return IsInstanceOfInterface(obj, pvTargetType);
            else
                return IsInstanceOfClass(obj, pvTargetType);
        }

        [RuntimeExport("RhTypeCast_CheckCast2")] // Helper with RyuJIT calling convention
        static public unsafe object CheckCast2(void* pvTargetType, Object obj)
        {
            return CheckCast(obj, pvTargetType);
        }

        [RuntimeExport("RhTypeCast_CheckCast")]
        static public unsafe object CheckCast(Object obj, void* pvTargetType)
        {
            // @TODO: consider using the cache directly, but beware of ICastable in the interface case
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

        // source type + target type + assignment variation -> true/false
        [System.Runtime.CompilerServices.EagerStaticClassConstructionAttribute]
        private static class CastCache
        {
            //
            // Cache size parameters
            //

            // Start with small cache size so that the cache entries used by startup one-time only initialization
            // will get flushed soon
            private const int InitialCacheSize = 128; // MUST BE A POWER OF TWO
            private const int DefaultCacheSize = 1024;
            private const int MaximumCacheSize = 128 * 1024;

            //
            // Cache state
            //
            private static Entry[] s_cache = new Entry[InitialCacheSize];   // Initialize the cache eagerly to avoid null checks.
            private static UnsafeGCHandle s_previousCache;
            private static long s_tickCountOfLastOverflow = InternalCalls.PalGetTickCount64();
            private static int s_entries;
            private static bool s_roundRobinFlushing;


            private sealed class Entry
            {
                public Entry    Next;
                public Key      Key;
                public bool     Result;     // @TODO: consider storing this bit in the Key -- there is room
            }

            private unsafe struct Key
            {
                IntPtr _sourceTypeAndVariation;
                IntPtr _targetType;

                public Key(EEType* pSourceType, EEType* pTargetType, AssignmentVariation variation)
                {
                    Debug.Assert((((long)pSourceType) & 3) == 0, "misaligned EEType!");
                    Debug.Assert(((uint)variation) <= 3, "variation enum has an unexpectedly large value!");

                    _sourceTypeAndVariation = (IntPtr)(((byte*)pSourceType) + ((int)variation));
                    _targetType = (IntPtr)pTargetType;
                }

                private static int GetHashCode(IntPtr intptr)
                {
                    return unchecked((int)((long)intptr));
                }

                public int CalculateHashCode()
                {
                    return ((GetHashCode(_targetType) >> 4) ^ GetHashCode(_sourceTypeAndVariation));
                }

                public bool Equals(ref Key other)
                {
                    return (_sourceTypeAndVariation == other._sourceTypeAndVariation) && (_targetType == other._targetType);
                }

                public AssignmentVariation Variation
                {
                    get { return (AssignmentVariation)(unchecked((int)(long)_sourceTypeAndVariation) & 3); }
                }

                public EEType* SourceType { get { return (EEType*)(((long)_sourceTypeAndVariation) & ~3L); } }
                public EEType* TargetType { get { return (EEType*)_targetType; } }

            }

            public unsafe static bool AreTypesAssignableInternal(EEType* pSourceType, EEType* pTargetType, AssignmentVariation variation)
            {
                // Important special case -- it breaks infinite recursion in CastCache itself!
                if (pSourceType == pTargetType)
                    return true;

                Key key = new Key(pSourceType, pTargetType, variation);
                Entry entry = LookupInCache(s_cache, ref key);
                if (entry == null)
                    return CacheMiss(ref key);

                return entry.Result;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Entry LookupInCache(Entry[] cache, ref Key key)
            {
                int entryIndex = key.CalculateHashCode() & (cache.Length - 1);
                Entry entry = cache[entryIndex];
                while (entry != null)
                {
                    if (entry.Key.Equals(ref key))
                        break;
                    entry = entry.Next;
                }
                return entry;
            }

            private unsafe static bool CacheMiss(ref Key key)
            {
                bool result = false;
                bool previouslyCached = false;

                //
                // Try to find the entry in the previous version of the cache that is kept alive by weak reference
                //
                if (s_previousCache.IsAllocated)
                {
                    // Unchecked cast to avoid recursive dependency on array casting
                    Entry[] previousCache = Unsafe.As<Entry[]>(s_previousCache.Target);
                    if (previousCache != null)
                    {
                        Entry previousEntry = LookupInCache(previousCache, ref key);
                        if (previousEntry != null)
                        {
                            result = previousEntry.Result;
                            previouslyCached = true;
                        }
                    }
                }

                //
                // Call into the type cast code to calculate the result
                //
                if (!previouslyCached)
                {
                    result = TypeCast.AreTypesAssignableInternal(key.SourceType, key.TargetType, key.Variation);
                }

                //
                // Update the cache under the lock
                //
                InternalCalls.RhpAcquireCastCacheLock();
                try
                {
                    try
                    {
                        // Avoid duplicate entries
                        Entry existingEntry = LookupInCache(s_cache, ref key);
                        if (existingEntry != null)
                            return existingEntry.Result;

                        // Resize cache as necessary
                        Entry[] cache = ResizeCacheForNewEntryAsNecessary();

                        int entryIndex = key.CalculateHashCode() & (cache.Length - 1);

                        Entry newEntry = new Entry() { Key = key, Result = result, Next = cache[entryIndex] };

                        // BEWARE: Array store check can lead to infinite recursion. We avoid this by making certain 
                        // that the cache trivially answers the case of equivalent types without triggering the cache
                        // miss path. (See CastCache.AreTypesAssignableInternal)
                        cache[entryIndex] = newEntry;  
                        return newEntry.Result;
                    }
                    catch (OutOfMemoryException)
                    {
                        // Entry allocation failed -- but we can still return the correct cast result.
                        return result;
                    }
                }
                finally
                {
                    InternalCalls.RhpReleaseCastCacheLock();
                }
            }

            private static Entry[] ResizeCacheForNewEntryAsNecessary()
            {
                Entry[] cache = s_cache;

                int entries = s_entries++;

                // If the cache has spare space, we are done
                if (2 * entries < cache.Length)
                {
                    if (s_roundRobinFlushing)
                    {
                        cache[2 * entries] = null;
                        cache[2 * entries + 1] = null;
                    }
                    return cache;
                }

                //
                // Now, we have cache that is overflowing with results. We need to decide whether to resize it or start 
                // flushing the old entries instead
                //

                // Start over counting the entries
                s_entries = 0;

                // See how long it has been since the last time the cache was overflowing
                long tickCount = InternalCalls.PalGetTickCount64();
                int tickCountSinceLastOverflow = (int)(tickCount - s_tickCountOfLastOverflow);
                s_tickCountOfLastOverflow = tickCount;

                bool shrinkCache = false;
                bool growCache = false;

                if (cache.Length < DefaultCacheSize)
                {
                    // If the cache have not reached the default size, just grow it without thinking about it much
                    growCache = true;
                }
                else
                {
                    if (tickCountSinceLastOverflow < cache.Length)
                    {
                        // We 'overflow' when 2*entries == cache.Length, so we have cache.Length / 2 entries that were
                        // filled in tickCountSinceLastOverflow ms, which is 2ms/entry

                        // If the fill rate of the cache is faster than ~2ms per entry, grow it
                        if (cache.Length < MaximumCacheSize)
                            growCache = true;
                    }
                    else
                    if (tickCountSinceLastOverflow > cache.Length * 16)
                    {
                        // We 'overflow' when 2*entries == cache.Length, so we have ((cache.Length*16) / 2) entries that
                        // were filled in tickCountSinceLastOverflow ms, which is 32ms/entry

                        // If the fill rate of the cache is slower than 32ms per entry, shrink it
                        if (cache.Length > DefaultCacheSize)
                            shrinkCache = true;
                    }
                    // Otherwise, keep the current size and just keep flushing the entries round robin
                }

                Entry[] newCache = null;
                if (growCache || shrinkCache)
                {
                    try
                    {
                        newCache = new Entry[shrinkCache ? (cache.Length / 2) : (cache.Length * 2)];
                    }
                    catch (OutOfMemoryException)
                    {
                        // Failed to allocate a bigger/smaller cache.  That is fine, keep the old one.
                    }
                }

                if (newCache != null)
                {
                    s_roundRobinFlushing = false;

                    // Keep the reference to the old cache in a weak handle. We will try to use it to avoid hitting the 
                    // cache miss path until the GC collects it.
                    if (s_previousCache.IsAllocated)
                    {
                        s_previousCache.Target = cache;
                    }
                    else
                    {
                        try
                        {
                            s_previousCache = UnsafeGCHandle.Alloc(cache, GCHandleType.Weak);
                        }
                        catch (OutOfMemoryException)
                        {
                            // Failed to allocate the handle to utilize the old cache, that is fine, we will just miss
                            // out on repopulating the new cache from the old cache.
                            s_previousCache = default(UnsafeGCHandle);
                        }
                    }

                    return s_cache = newCache;
                }
                else
                {
                    s_roundRobinFlushing = true;
                    return cache;
                }
            }
        }
    }
}
