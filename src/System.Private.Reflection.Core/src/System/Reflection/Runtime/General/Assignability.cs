// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Reflection.Runtime.TypeInfos;

using Internal.Reflection.Core;

namespace System.Reflection.Runtime.General
{
    internal static class Assignability
    {
        public static bool IsAssignableFrom(TypeInfo toTypeInfo, TypeInfo fromTypeInfo, FoundationTypes foundationTypes)
        {
            if (toTypeInfo == null)
                throw new NullReferenceException();
            if (fromTypeInfo == null)
                return false;   // It would be more appropriate to throw ArgumentNullException here, but returning "false" is the desktop-compat behavior.

            if (fromTypeInfo.Equals(toTypeInfo))
                return true;

            if (toTypeInfo.IsGenericTypeDefinition)
            {
                // Asking whether something can cast to a generic type definition is arguably meaningless. The desktop CLR Reflection layer converts all
                // generic type definitions to generic type instantiations closed over the formal generic type parameters. The .NET Native framework
                // keeps the two separate. Fortunately, under either interpretation, returning "false" unless the two types are identical is still a 
                // defensible behavior. To avoid having the rest of the code deal with the differing interpretations, we'll short-circuit this now.
                return false;
            }

            if (fromTypeInfo.IsGenericTypeDefinition)
            {
                // The desktop CLR Reflection layer converts all generic type definitions to generic type instantiations closed over the formal 
                // generic type parameters. The .NET Native framework keeps the two separate. For the purpose of IsAssignableFrom(), 
                // it makes sense to unify the two for the sake of backward compat. We'll just make the transform here so that the rest of code
                // doesn't need to know about this quirk.
                fromTypeInfo = fromTypeInfo.GetGenericTypeDefinition().MakeGenericType(fromTypeInfo.GenericTypeParameters).GetTypeInfo();
            }

            if (fromTypeInfo.CanCastTo(toTypeInfo, foundationTypes))
                return true;

            Type toType = toTypeInfo.AsType();
            Type fromType = fromTypeInfo.AsType();

            // Desktop compat: IsAssignableFrom() considers T as assignable to Nullable<T> (but does not check if T is a generic parameter.)
            if (!fromType.IsGenericParameter)
            {
                Type nullableUnderlyingType = Nullable.GetUnderlyingType(toType);
                if (nullableUnderlyingType != null && nullableUnderlyingType.Equals(fromType))
                    return true;
            }
            return false;
        }

        private static bool CanCastTo(this TypeInfo fromTypeInfo, TypeInfo toTypeInfo, FoundationTypes foundationTypes)
        {
            if (fromTypeInfo.Equals(toTypeInfo))
                return true;

            if (fromTypeInfo.IsArray)
            {
                if (toTypeInfo.IsInterface)
                    return fromTypeInfo.CanCastArrayToInterface(toTypeInfo, foundationTypes);

                Type toType = toTypeInfo.AsType();
                if (fromTypeInfo.IsSubclassOf(toType))
                    return true;  // T[] is castable to Array or Object.

                if (!toTypeInfo.IsArray)
                    return false;

                int rank = fromTypeInfo.GetArrayRank();
                if (rank != toTypeInfo.GetArrayRank())
                    return false;

                bool fromTypeIsSzArray = fromTypeInfo.IsSzArray(foundationTypes);
                bool toTypeIsSzArray = toTypeInfo.IsSzArray(foundationTypes);
                if (fromTypeIsSzArray != toTypeIsSzArray)
                {
                    // T[] is assignable to T[*] but not vice-versa.
                    if (!(rank == 1 && !toTypeIsSzArray))
                    {
                        return false; // T[*] is not castable to T[]
                    }
                }

                TypeInfo toElementTypeInfo = toTypeInfo.GetElementType().GetTypeInfo();
                TypeInfo fromElementTypeInfo = fromTypeInfo.GetElementType().GetTypeInfo();
                return fromElementTypeInfo.IsElementTypeCompatibleWith(toElementTypeInfo, foundationTypes);
            }

            if (fromTypeInfo.IsByRef)
            {
                if (!toTypeInfo.IsByRef)
                    return false;

                TypeInfo toElementTypeInfo = toTypeInfo.GetElementType().GetTypeInfo();
                TypeInfo fromElementTypeInfo = fromTypeInfo.GetElementType().GetTypeInfo();
                return fromElementTypeInfo.IsElementTypeCompatibleWith(toElementTypeInfo, foundationTypes);
            }

            if (fromTypeInfo.IsPointer)
            {
                Type toType = toTypeInfo.AsType();
                if (toType.Equals(foundationTypes.SystemObject))
                    return true;  // T* is castable to Object.

                if (toType.Equals(foundationTypes.SystemUIntPtr))
                    return true;  // T* is castable to UIntPtr (but not IntPtr)

                if (!toTypeInfo.IsPointer)
                    return false;

                TypeInfo toElementTypeInfo = toTypeInfo.GetElementType().GetTypeInfo();
                TypeInfo fromElementTypeInfo = fromTypeInfo.GetElementType().GetTypeInfo();
                return fromElementTypeInfo.IsElementTypeCompatibleWith(toElementTypeInfo, foundationTypes);
            }

            if (fromTypeInfo.IsGenericParameter)
            {
                //
                // A generic parameter can be cast to any of its constraints, or object, if none are specified, or ValueType if the "struct" constraint is
                // specified.
                //
                // This has to be coded as its own case as TypeInfo.BaseType on a generic parameter doesn't always return what you'd expect.
                //
                Type toType = toTypeInfo.AsType();
                if (toType.Equals(foundationTypes.SystemObject))
                    return true;

                if (toType.Equals(foundationTypes.SystemValueType))
                {
                    GenericParameterAttributes attributes = fromTypeInfo.GenericParameterAttributes;
                    if ((attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0)
                        return true;
                }

                foreach (Type constraintType in fromTypeInfo.GetGenericParameterConstraints())
                {
                    if (constraintType.GetTypeInfo().CanCastTo(toTypeInfo, foundationTypes))
                        return true;
                }

                return false;
            }

            if (toTypeInfo.IsArray || toTypeInfo.IsByRef || toTypeInfo.IsPointer || toTypeInfo.IsGenericParameter)
                return false;

            if (fromTypeInfo.MatchesWithVariance(toTypeInfo, foundationTypes))
                return true;

            if (toTypeInfo.IsInterface)
            {
                foreach (Type ifc in fromTypeInfo.ImplementedInterfaces)
                {
                    if (ifc.GetTypeInfo().MatchesWithVariance(toTypeInfo, foundationTypes))
                        return true;
                }
                return false;
            }
            else
            {
                // Interfaces are always castable to System.Object. The code below will not catch this as interfaces report their BaseType as null. 
                if (toTypeInfo.AsType().Equals(foundationTypes.SystemObject) && fromTypeInfo.IsInterface)
                    return true;

                TypeInfo walk = fromTypeInfo;
                for (;;)
                {
                    Type baseType = walk.BaseType;
                    if (baseType == null)
                        return false;
                    walk = baseType.GetTypeInfo();
                    if (walk.MatchesWithVariance(toTypeInfo, foundationTypes))
                        return true;
                }
            }
        }

        private static bool IsSzArray(this TypeInfo typeInfo, FoundationTypes foundationTypes)
        {
            if (!typeInfo.IsArray)
                return false;

            if (typeInfo.GetArrayRank() != 1)
                return false;

            if (((RuntimeTypeInfo)typeInfo).InternalIsMultiDimArray)
                return false;

            return true;
        }

        //
        // Check a base type or implemented interface type for equivalence (taking into account variance for generic instantiations.)
        // Does not check ancestors recursively.
        //
        private static bool MatchesWithVariance(this TypeInfo fromTypeInfo, TypeInfo toTypeInfo, FoundationTypes foundationTypes)
        {
            Debug.Assert(!(fromTypeInfo.IsArray || fromTypeInfo.IsByRef || fromTypeInfo.IsPointer || fromTypeInfo.IsGenericParameter));
            Debug.Assert(!(toTypeInfo.IsArray || toTypeInfo.IsByRef || toTypeInfo.IsPointer || toTypeInfo.IsGenericParameter));

            if (fromTypeInfo.Equals(toTypeInfo))
                return true;

            if (!(fromTypeInfo.AsType().IsConstructedGenericType && toTypeInfo.AsType().IsConstructedGenericType))
                return false;

            TypeInfo genericTypeDefinition = fromTypeInfo.GetGenericTypeDefinition().GetTypeInfo();
            if (!genericTypeDefinition.AsType().Equals(toTypeInfo.GetGenericTypeDefinition()))
                return false;

            Type[] fromTypeArguments = fromTypeInfo.GenericTypeArguments;
            Type[] toTypeArguments = toTypeInfo.GenericTypeArguments;
            Type[] genericTypeParameters = genericTypeDefinition.GenericTypeParameters;
            for (int i = 0; i < genericTypeParameters.Length; i++)
            {
                TypeInfo fromTypeArgumentInfo = fromTypeArguments[i].GetTypeInfo();
                TypeInfo toTypeArgumentInfo = toTypeArguments[i].GetTypeInfo();

                GenericParameterAttributes attributes = genericTypeParameters[i].GetTypeInfo().GenericParameterAttributes;
                switch (attributes & GenericParameterAttributes.VarianceMask)
                {
                    case GenericParameterAttributes.Covariant:
                        if (!(fromTypeArgumentInfo.IsGcReferenceTypeAndCastableTo(toTypeArgumentInfo, foundationTypes)))
                            return false;
                        break;

                    case GenericParameterAttributes.Contravariant:
                        if (!(toTypeArgumentInfo.IsGcReferenceTypeAndCastableTo(fromTypeArgumentInfo, foundationTypes)))
                            return false;
                        break;

                    case GenericParameterAttributes.None:
                        if (!(fromTypeArgumentInfo.Equals(toTypeArgumentInfo)))
                            return false;
                        break;

                    default:
                        throw new BadImageFormatException();  // Unexpected variance value in metadata.
                }
            }
            return true;
        }

        //
        // A[] can cast to B[] if one of the following are true:
        //
        //    A can cast to B under variance rules.
        //
        //    A and B are both integers or enums and have the same reduced type (i.e. represent the same-sized integer, ignoring signed/unsigned differences.)
        //        "char" is not interchangable with short/ushort. "bool" is not interchangable with byte/sbyte.
        //
        // For desktop compat, A& and A* follow the same rules.
        //
        private static bool IsElementTypeCompatibleWith(this TypeInfo fromTypeInfo, TypeInfo toTypeInfo, FoundationTypes foundationTypes)
        {
            if (fromTypeInfo.IsGcReferenceTypeAndCastableTo(toTypeInfo, foundationTypes))
                return true;

            Type reducedFromType = fromTypeInfo.AsType().ReducedType(foundationTypes);
            Type reducedToType = toTypeInfo.AsType().ReducedType(foundationTypes);
            if (reducedFromType.Equals(reducedToType))
                return true;

            return false;
        }

        private static Type ReducedType(this Type t, FoundationTypes foundationTypes)
        {
            if (t.GetTypeInfo().IsEnum)
                t = Enum.GetUnderlyingType(t);

            if (t.Equals(foundationTypes.SystemByte))
                return foundationTypes.SystemSByte;

            if (t.Equals(foundationTypes.SystemUInt16))
                return foundationTypes.SystemInt16;

            if (t.Equals(foundationTypes.SystemUInt32))
                return foundationTypes.SystemInt32;

            if (t.Equals(foundationTypes.SystemUInt64))
                return foundationTypes.SystemInt64;

            if (t.Equals(foundationTypes.SystemUIntPtr) || t.Equals(foundationTypes.SystemIntPtr))
            {
#if WIN64
                return foundationTypes.SystemInt64;
#else
                return foundationTypes.SystemInt32;
#endif
            }

            return t;
        }

        //
        // Contra/CoVariance.
        //
        // IEnumerable<D> can cast to IEnumerable<B> if D can cast to B and if there's no possibility that D is a value type.
        //
        private static bool IsGcReferenceTypeAndCastableTo(this TypeInfo fromTypeInfo, TypeInfo toTypeInfo, FoundationTypes foundationTypes)
        {
            if (fromTypeInfo.Equals(toTypeInfo))
                return true;

            if (fromTypeInfo.ProvablyAGcReferenceType(foundationTypes))
                return fromTypeInfo.CanCastTo(toTypeInfo, foundationTypes);

            return false;
        }

        //
        // A true result indicates that a type can never be a value type. This is important when testing variance-compatibility.
        //
        private static bool ProvablyAGcReferenceType(this TypeInfo t, FoundationTypes foundationTypes)
        {
            if (t.IsGenericParameter)
            {
                GenericParameterAttributes attributes = t.GenericParameterAttributes;
                if ((attributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0)
                    return true;   // generic parameter with a "class" constraint.
            }

            return t.ProvablyAGcReferenceTypeHelper(foundationTypes);
        }

        private static bool ProvablyAGcReferenceTypeHelper(this TypeInfo t, FoundationTypes foundationTypes)
        {
            if (t.IsArray)
                return true;

            if (t.IsByRef || t.IsPointer)
                return false;

            if (t.IsGenericParameter)
            {
                // We intentionally do not check for a "class" constraint on generic parameter ancestors.
                // That's because this property does not propagate up the constraining hierarchy.
                // (e.g. "class A<S, T> where S : T, where T : class" does not guarantee that S is a class.)

                foreach (Type constraintType in t.GetGenericParameterConstraints())
                {
                    if (constraintType.GetTypeInfo().ProvablyAGcReferenceTypeHelper(foundationTypes))
                        return true;
                }
                return false;
            }

            return t.IsClass && !t.Equals(foundationTypes.SystemObject) && !t.Equals(foundationTypes.SystemValueType) && !t.Equals(foundationTypes.SystemEnum);
        }

        //
        // T[] casts to IList<T>. This could be handled by the normal ancestor-walking code
        // but for one complication: T[] also casts to IList<U> if T[] casts to U[].
        //
        private static bool CanCastArrayToInterface(this TypeInfo fromTypeInfo, TypeInfo toTypeInfo, FoundationTypes foundationTypes)
        {
            Debug.Assert(fromTypeInfo.IsArray);
            Debug.Assert(toTypeInfo.IsInterface);

            Type toType = toTypeInfo.AsType();

            if (toType.IsConstructedGenericType)
            {
                Type[] toTypeGenericTypeArguments = toTypeInfo.GenericTypeArguments;
                if (toTypeGenericTypeArguments.Length != 1)
                    return false;
                TypeInfo toElementTypeInfo = toTypeGenericTypeArguments[0].GetTypeInfo();

                Type toTypeGenericTypeDefinition = toTypeInfo.GetGenericTypeDefinition();
                TypeInfo fromElementTypeInfo = fromTypeInfo.GetElementType().GetTypeInfo();
                foreach (Type ifc in fromTypeInfo.ImplementedInterfaces)
                {
                    if (ifc.IsConstructedGenericType)
                    {
                        Type ifcGenericTypeDefinition = ifc.GetGenericTypeDefinition();
                        if (ifcGenericTypeDefinition.Equals(toTypeGenericTypeDefinition))
                        {
                            if (fromElementTypeInfo.IsElementTypeCompatibleWith(toElementTypeInfo, foundationTypes))
                                return true;
                        }
                    }
                }
                return false;
            }
            else
            {
                foreach (Type ifc in fromTypeInfo.ImplementedInterfaces)
                {
                    if (ifc.Equals(toType))
                        return true;
                }
                return false;
            }
        }
    }
}
