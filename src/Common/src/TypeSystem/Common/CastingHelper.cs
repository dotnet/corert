// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    public static partial class CastingHelper
    {
        /// <summary>
        /// Returns true if '<paramref name="thisType"/>' can be cast to '<paramref name="otherType"/>'.
        /// Assumes '<paramref name="thisType"/>' is in it's boxed form if it's a value type (i.e.
        /// [System.Int32].CanCastTo([System.Object]) will return true).
        /// </summary>
        public static bool CanCastTo(this TypeDesc thisType, TypeDesc otherType)
        {
            return thisType.CanCastToInternal(otherType, null);
        }

        private static bool CanCastToInternal(this TypeDesc thisType, TypeDesc otherType, StackOverflowProtect protect)
        {
            if (thisType == otherType)
            {
                return true;
            }
            else if (thisType.IsGenericParameter)
            {
                // A boxed variable type can be cast to any of its constraints, or object, if none are specified
                if (otherType.IsObject)
                {
                    return true;
                }

                var genericParamTypeThis = (GenericParameterDesc)thisType;

                if (genericParamTypeThis.HasNotNullableValueTypeConstraint &&
                    thisType.Context.IsWellKnownType(otherType, WellKnownType.ValueType))
                {
                    return true;
                }

                foreach (var typeConstraint in genericParamTypeThis.TypeConstraints)
                {
                    if (typeConstraint.CanCastToInternal(otherType, protect))
                    {
                        return true;
                    }
                }
            }
            else if (thisType.Variety() != otherType.Variety())
            {
                if (thisType.IsArray && otherType.IsDefType)
                {
                    return thisType.CanCastToClassOrInterface(otherType, protect);
                }

                return false;
            }
            else
            {
                switch (thisType.Variety())
                {
                    case TypeKind.Pointer:
                    case TypeKind.ByRef:
                        // Feel free to remove the assert if you ever hit this and after some thought find out it's okay.
                        // This code was ported from the Project N compiler, but it doesn't feel right.
                        Debug.Assert(false, "Did we box a pointer/byref?");
                        goto case TypeKind.SzArray;

                    case TypeKind.SzArray:
                        var thisParameterizedType = (ParameterizedType)thisType;
                        var otherParameterizedType = (ParameterizedType)otherType;
                        return thisParameterizedType.CanCastParamTo(otherParameterizedType.ParameterType, protect);

                    case TypeKind.Array:
                        var thisArrayType = (ArrayType)thisType;
                        var otherArrayType = (ArrayType)otherType;
                        return thisArrayType.Rank == otherArrayType.Rank
                            && thisArrayType.CanCastParamTo(otherArrayType.ParameterType, protect);

                    case TypeKind.DefType:
                        return thisType.CanCastToClassOrInterface(otherType, protect);
                }
            }

            return false;
        }

        private static bool CanCastParamTo(this ParameterizedType thisType, TypeDesc paramType, StackOverflowProtect protect)
        {
            // While boxed value classes inherit from object their
            // unboxed versions do not.  Parameterized types have the
            // unboxed version, thus, if the from type parameter is value
            // class then only an exact match/equivalence works.
            if (thisType.ParameterType == paramType)
            {
                return true;
            }

            return ParametrizedTypeCastHelper(thisType.ParameterType, paramType, protect);
        }

        private static bool IsObjRef(this TypeDesc type)
        {
            TypeFlags category = type.Category;
            return category == TypeFlags.Class || category == TypeFlags.Array;
        }
        
        private static bool ParametrizedTypeCastHelper(TypeDesc curTypesParm, TypeDesc otherTypesParam, StackOverflowProtect protect)
        {
            // Object parameters don't need an exact match but only inheritance, check for that
            TypeDesc fromParamUnderlyingType = curTypesParm.UnderlyingType;
            if (fromParamUnderlyingType.IsObjRef())
            {
                return curTypesParm.CanCastToInternal(otherTypesParam, protect);
            }
            else if (curTypesParm.IsGenericParameter)
            {
                var genericVariableFromParam = (GenericParameterDesc)curTypesParm;
                if (genericVariableFromParam.HasReferenceTypeConstraint)
                {
                    return genericVariableFromParam.CanCastToInternal(otherTypesParam, protect);
                }
            }
            else if (fromParamUnderlyingType.IsPrimitive)
            {
                TypeDesc toParamUnderlyingType = otherTypesParam.UnderlyingType;
                if (toParamUnderlyingType.IsPrimitive)
                {
                    if (toParamUnderlyingType == fromParamUnderlyingType)
                    {
                        return true;
                    }

                    // Primitive types such as E_T_I4 and E_T_U4 are interchangeable
                    // Enums with interchangeable underlying types are interchangable
                    // BOOL is NOT interchangeable with I1/U1, neither CHAR -- with I2/U2
                    // Float and dobule are not interchangable here.
                    TypeFlags fromParamCategory = fromParamUnderlyingType.Category;
                    TypeFlags toParamCategory = toParamUnderlyingType.Category;

                    if (fromParamCategory != TypeFlags.Boolean &&
                        toParamCategory != TypeFlags.Boolean &&
                        fromParamCategory != TypeFlags.Char &&
                        toParamCategory != TypeFlags.Char &&
                        fromParamCategory != TypeFlags.Single &&
                        toParamCategory != TypeFlags.Single &&
                        fromParamCategory != TypeFlags.Double &&
                        toParamCategory != TypeFlags.Double)
                    {
                        if (((DefType)curTypesParm).InstanceFieldSize == ((DefType)otherTypesParam).InstanceFieldSize)
                        {
                            return true;
                        }
                    }
                }
            }

            // Anything else is not a match
            return false;
        }

        private static bool CanCastToClassOrInterface(this TypeDesc thisType, TypeDesc otherType, StackOverflowProtect protect)
        {
            if (otherType.IsInterface)
            {
                return thisType.CanCastToInterface(otherType, protect);
            }
            else
            {
                return thisType.CanCastToClass(otherType, protect);
            }
        }

        private static bool CanCastToInterface(this TypeDesc thisType, TypeDesc otherType, StackOverflowProtect protect)
        {
            if (!otherType.HasVariance)
            {
                return thisType.CanCastToNonVariantInterface(otherType,protect);
            }
            else
            {
                if (thisType.CanCastByVarianceToInterfaceOrDelegate(otherType, protect))
                {
                    return true;
                }

                foreach (var interfaceType in thisType.RuntimeInterfaces)
                {
                    if (interfaceType.CanCastByVarianceToInterfaceOrDelegate(otherType, protect))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool CanCastToNonVariantInterface(this TypeDesc thisType, TypeDesc otherType, StackOverflowProtect protect)
        {
            if (otherType == thisType)
            {
                return true;
            }

            foreach (var interfaceType in thisType.RuntimeInterfaces)
            {
                if (interfaceType == otherType)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CanCastByVarianceToInterfaceOrDelegate(this TypeDesc thisType, TypeDesc otherType, StackOverflowProtect protectInput)
        {
            if (!thisType.HasSameTypeDefinition(otherType))
            {
                return false;
            }

            var stackOverflowProtectKey = new CastingPair(thisType, otherType);
            if (protectInput != null)
            {
                if (protectInput.Contains(stackOverflowProtectKey))
                    return false;
            }

            StackOverflowProtect protect = new StackOverflowProtect(stackOverflowProtectKey, protectInput);

            Instantiation instantiationThis = thisType.Instantiation;
            Instantiation instantiationTarget = otherType.Instantiation;
            Instantiation instantiationOpen = thisType.GetTypeDefinition().Instantiation;

            Debug.Assert(instantiationThis.Length == instantiationTarget.Length &&
                instantiationThis.Length == instantiationOpen.Length);

            for (int i = 0; i < instantiationThis.Length; i++)
            {
                TypeDesc arg = instantiationThis[i];
                TypeDesc targetArg = instantiationTarget[i];
                
                if (arg != targetArg)
                {
                    GenericParameterDesc openArgType = (GenericParameterDesc)instantiationOpen[i];

                    switch (openArgType.Variance)
                    {
                        case GenericVariance.Covariant:
                            if (!arg.IsBoxedAndCanCastTo(targetArg, protect))
                                return false;
                            break;

                        case GenericVariance.Contravariant:
                            if (!targetArg.IsBoxedAndCanCastTo(arg, protect))
                                return false;
                            break;

                        default:
                            // non-variant
                            Debug.Assert(openArgType.Variance == GenericVariance.None);
                            return false;
                    }
                }
            }

            return true;
        }

        private static bool CanCastToClass(this TypeDesc thisType, TypeDesc otherType, StackOverflowProtect protect)
        {
            TypeDesc curType = thisType;

            // If the target type has variant type parameters, we take a slower path
            if (curType.HasVariance)
            {
                // First chase inheritance hierarchy until we hit a class that only differs in its instantiation
                do
                {
                    if (curType == otherType)
                    {
                        return true;
                    }

                    if (curType.CanCastByVarianceToInterfaceOrDelegate(otherType, protect))
                    {
                        return true;
                    }

                    curType = curType.BaseType;
                }
                while (curType != null);
            }
            else
            {
                // If there are no variant type parameters, just chase the hierarchy

                // Allow curType to be nullable, which means this method
                // will additionally return true if curType is Nullable<T> && (
                //    currType == otherType
                // OR otherType is System.ValueType or System.Object)

                // Always strip Nullable from the otherType, if present
                if (otherType.IsNullable && !curType.IsNullable)
                {
                    return thisType.CanCastTo(otherType.Instantiation[0]);
                }

                do
                {
                    if (curType == otherType)
                        return true;

                    curType = curType.BaseType;
                } while (curType != null);
            }

            return false;
        }

        private static bool IsBoxedAndCanCastTo(this TypeDesc thisType, TypeDesc otherType, StackOverflowProtect protect)
        {
            TypeDesc fromUnderlyingType = thisType.UnderlyingType;

            if (fromUnderlyingType.IsObjRef())
            {
                return thisType.CanCastToInternal(otherType, protect);
            }
            else if (thisType.IsGenericParameter)
            {
                var genericVariableFromParam = (GenericParameterDesc)thisType;
                if (genericVariableFromParam.HasReferenceTypeConstraint)
                {
                    return genericVariableFromParam.CanCastToInternal(otherType, protect);
                }
            }

            return false;
        }

        private static TypeKind Variety(this TypeDesc type)
        {
            switch (type.Category)
            {
                case TypeFlags.Array:
                    return type.IsSzArray ? TypeKind.SzArray : TypeKind.Array;
                case TypeFlags.GenericParameter:
                    return TypeKind.GenericParameter;
                case TypeFlags.ByRef:
                    return TypeKind.ByRef;
                case TypeFlags.Pointer:
                    return TypeKind.Pointer;
                default:
                    Debug.Assert(type is DefType);
                    return TypeKind.DefType;
            }
        }

        private enum TypeKind
        {
            DefType,
            ByRef,
            Pointer,
            SzArray,
            Array,
            GenericParameter,
        }

        private class StackOverflowProtect
        {
            private CastingPair _value;
            private StackOverflowProtect _previous;

            public StackOverflowProtect(CastingPair value, StackOverflowProtect previous)
            {
                _value = value;
                _previous = previous;
            }

            public bool Contains(CastingPair value)
            {
                for (var current = this; current != null; current = current._previous)
                    if (current._value.Equals(value))
                        return true;
                return false;
            }
        }

        private struct CastingPair
        {
            public readonly TypeDesc FromType;
            public readonly TypeDesc ToType;

            public CastingPair(TypeDesc fromType, TypeDesc toType)
            {
                FromType = fromType;
                ToType = toType;
            }

            public bool Equals(CastingPair other) => FromType == other.FromType && ToType == other.ToType;
        }
    }
}
