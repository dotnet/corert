// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

namespace Internal.IL
{
    public static class TypeSystemHelpers
    {
        /// <summary>
        /// Checks whether the instantiation of this <see cref="TypeDesc"/> is compliant with the 
        /// constraints of its generic parameters. Returns true if the type has no instantiation.
        /// </summary>
        /// <param name="classType">The class to check the instantiation of.</param>
        /// <returns>True if the class instantiation satisfied the class constraints, otherwhise false.</returns>
        public static bool SatisfiesConstraints(this TypeDesc classType)
        {
            var parent = classType.BaseType;
            if (parent != null && !SatisfiesConstraints(parent))
                return false;

            if (!classType.HasInstantiation)
                return true;

            return SatisfiesInstantiationConstraints(classType.Instantiation, classType.GetTypeDefinition().Instantiation);
        }

        /// <summary>
        /// Checks whether the instantiation of this <see cref= "MethodDesc" /> is compliant with the
        /// constraints of its generic parameters. Returns true if the method has no instantiation.
        /// </summary>
        /// <param name="method">The method to check the instantiation of.</param>
        /// <returns>True if the method instantiation satisfied the method constraints, otherwhise false.</returns>
        public static bool SatisfiesConstraints(this MethodDesc method)
        {
            if (!method.HasInstantiation)
                return true;

            return SatisfiesInstantiationConstraints(method.Instantiation, method.GetMethodDefinition().Instantiation);
        }

        private static bool SatisfiesInstantiationConstraints(Instantiation instantiation, Instantiation typicalInstantiation)
        {
            if (instantiation.Length != typicalInstantiation.Length)
                return false;

            for (int i = 0; i < instantiation.Length; i++)
            {
                if (!SatisfiesTypeConstraints(instantiation[i], (GenericParameterDesc)typicalInstantiation[i]))
                    return false;
            }

            return true;
        }

        private static bool SatisfiesTypeConstraints(TypeDesc type, GenericParameterDesc constraintsType)
        {
            if (type == constraintsType)
                return true;

            // Check special constraints
            if (type.IsGenericParameter)
            {
                // Type to check is generic itself
                var genericType = (GenericParameterDesc)type;

                if (constraintsType.HasNotNullableValueTypeConstraint &&
                    !GenericSatisfiesSpecialConstraint(genericType, GenericConstraints.NotNullableValueTypeConstraint))
                    return false;

                if (constraintsType.HasReferenceTypeConstraint &&
                    !GenericSatisfiesSpecialConstraint(genericType, GenericConstraints.ReferenceTypeConstraint))
                    return false;

                if (constraintsType.HasDefaultConstructorConstraint &&
                    !GenericSatisfiesSpecialConstraint(genericType, GenericConstraints.DefaultConstructorConstraint))
                    return false;
            }
            else
            {
                // Type to check is non generic
                if (constraintsType.HasNotNullableValueTypeConstraint &&
                    (!type.IsValueType || type.IsNullable))
                    return false;

                if (constraintsType.HasReferenceTypeConstraint &&
                    type.IsValueType)
                    return false;

                if (constraintsType.HasDefaultConstructorConstraint &&
                    !type.HasExplicitOrImplicitDefaultConstructor())
                    return false;
            }

            // Check general subtype constraints
            foreach (var constraint in constraintsType.TypeConstraints)
            {
                if (!type.CanCastTo(constraint))
                    return false;
            }

            return true;
        }

        // Used to determine whether a type parameter used to instantiate another type parameter with a specific special 
        // constraint satisfies that constraint.
        private static bool GenericSatisfiesSpecialConstraint(GenericParameterDesc type, GenericConstraints specialConstraint)
        {
            // Check if type has specialConstraint on its own
            if ((type.Constraints & specialConstraint) != 0)
                return true;

            // Value type always has default constructor
            if (specialConstraint == GenericConstraints.DefaultConstructorConstraint && type.HasNotNullableValueTypeConstraint)
                return true;

            // The special constraints did not match, check if there is a primary type constaint,
            // that would always satisfy the special constraint
            foreach (var constraint in type.TypeConstraints)
            {
                if (constraint.IsGenericParameter || constraint.IsInterface)
                    continue;

                if (GenericConstraints.NotNullableValueTypeConstraint == specialConstraint)
                {
                    if (constraint.IsValueType && !constraint.IsNullable)
                        return true;
                }
                else if (GenericConstraints.ReferenceTypeConstraint == specialConstraint)
                {
                    if (!constraint.IsValueType)
                        return true;
                }
                else if (GenericConstraints.DefaultConstructorConstraint == specialConstraint)
                {
                    // As constraint is only ancestor, can only be sure whether type has public default constructor if it is a value type
                    if (constraint.IsValueType && constraint.HasExplicitOrImplicitDefaultConstructor())
                        return true;
                }

            }

            // type did not satisfy special constraint in any way
            return false;
        }

        /// <summary>
        /// Returns the "reduced type" based on the definition in the ECMA-335 standard (I.8.7).
        /// </summary>
        public static TypeDesc GetReducedType(this TypeDesc type)
        {
            if (type == null)
                return null;

            var category = type.UnderlyingType.Category;

            switch (category)
            {
                case TypeFlags.Byte:
                    return type.Context.GetWellKnownType(WellKnownType.SByte);
                case TypeFlags.UInt16:
                    return type.Context.GetWellKnownType(WellKnownType.Int16);
                case TypeFlags.UInt32:
                    return type.Context.GetWellKnownType(WellKnownType.Int32);
                case TypeFlags.UInt64:
                    return type.Context.GetWellKnownType(WellKnownType.Int64);
                case TypeFlags.UIntPtr:
                    return type.Context.GetWellKnownType(WellKnownType.IntPtr);

                default:
                    return type.UnderlyingType; //Reduced type is type itself
            }
        }

        /// <summary>
        /// Returns the "verification type" based on the definition in the ECMA-335 standard (I.8.7).
        /// </summary>
        public static TypeDesc GetVerificationType(this TypeDesc type)
        {
            if (type == null)
                return null;

            if (type.IsByRef)
            {
                var parameterVerificationType = GetVerificationType(type.GetParameterType());
                return type.Context.GetByRefType(parameterVerificationType);
            }
            else
            {
                var reducedType = GetReducedType(type);
                switch (reducedType.Category)
                {
                    case TypeFlags.Boolean:
                        return type.Context.GetWellKnownType(WellKnownType.SByte);

                    case TypeFlags.Char:
                        return type.Context.GetWellKnownType(WellKnownType.Int16);

                    default:
                        return reducedType; // Verification type is reduced type
                }
            }
        }

        /// <summary>
        /// Returns the "intermediate type" based on the definition in the ECMA-335 standard (I.8.7).
        /// </summary>
        public static TypeDesc GetIntermediateType(this TypeDesc type)
        {
            var verificationType = GetVerificationType(type);

            if (verificationType == null)
                return null;

            switch (verificationType.Category)
            {
                case TypeFlags.SByte:
                case TypeFlags.Int16:
                case TypeFlags.Int32:
                    return type.Context.GetWellKnownType(WellKnownType.Int32);
                case TypeFlags.Single:
                case TypeFlags.Double:
                    return type.Context.GetWellKnownType(WellKnownType.Double);
                default:
                    return verificationType;
            }
        }
    }
}
