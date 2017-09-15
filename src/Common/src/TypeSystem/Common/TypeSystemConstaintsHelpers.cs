// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Internal.TypeSystem
{
    public static class TypeSystemConstraintsHelpers
    {
        private static bool VerifyGenericParamConstraint(Instantiation typeInstantiation, Instantiation methodInstantiation, GenericParameterDesc genericParam, TypeDesc instantiationParam)
        {
            // Check class constraint
            if (genericParam.HasReferenceTypeConstraint)
            {
                if (!instantiationParam.IsGCPointer && !CheckGenericSpecialConstraint(instantiationParam, GenericConstraints.ReferenceTypeConstraint))
                    return false;
            }

            // Check default constructor constraint
            if (genericParam.HasDefaultConstructorConstraint)
            {
                if (!instantiationParam.HasExplicitOrImplicitDefaultConstructor() && !CheckGenericSpecialConstraint(instantiationParam, GenericConstraints.DefaultConstructorConstraint))
                    return false;
            }

            // Check struct constraint
            if (genericParam.HasNotNullableValueTypeConstraint && !CheckGenericSpecialConstraint(instantiationParam, GenericConstraints.NotNullableValueTypeConstraint))
            {
                if (!instantiationParam.IsValueType)
                    return false;

                if (instantiationParam.IsNullable)
                    return false;
            }

            foreach (var constraintType in genericParam.TypeConstraints)
            {
                var instantiatedType = constraintType.InstantiateSignature(typeInstantiation, methodInstantiation);
                if (!instantiationParam.CanCastTo(instantiatedType))
                    return false;
            }

            return true;
        }

        // Used to determine whether a type parameter used to instantiate another type parameter with a specific special 
        // constraint satisfies that constraint.
        private static bool CheckGenericSpecialConstraint(TypeDesc type, GenericConstraints specialConstraint)
        {
            if (!type.IsGenericParameter)
                return false;

            var genericType = (GenericParameterDesc)type;

            // Check if type has specialConstraint on its own
            if ((genericType.Constraints & specialConstraint) != 0)
                return true;

            // Value type always has default constructor
            if (specialConstraint == GenericConstraints.DefaultConstructorConstraint && genericType.HasNotNullableValueTypeConstraint)
                return true;

            // The special constraints did not match, check if there is a primary type constraint,
            // that would always satisfy the special constraint
            foreach (var constraint in genericType.TypeConstraints)
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
                    if (constraint.IsValueType)
                        return true;
                }

            }

            // type did not satisfy special constraint in any way
            return false;
        }

        public static bool CheckValidInstantiationArguments(this Instantiation instantiation)
        {
            foreach(var arg in instantiation)
            {
                if (arg.IsPointer || arg.IsByRef || arg.IsGenericParameter || arg.IsVoid)
                    return false;

                if (arg.HasInstantiation)
                {
                    if (!CheckValidInstantiationArguments(arg.Instantiation))
                        return false;
                }
            }
            return true;
        }

        public static bool CheckConstraints(this TypeDesc type)
        {
            TypeDesc uninstantiatedType = type.GetTypeDefinition();

            // Non-generic types always pass constraints check
            if (uninstantiatedType == type)
                return true;

            for (int i = 0; i < uninstantiatedType.Instantiation.Length; i++)
            {
                if (!VerifyGenericParamConstraint(type.Instantiation, default(Instantiation), (GenericParameterDesc)uninstantiatedType.Instantiation[i], type.Instantiation[i]))
                    return false;
            }

            return true;
        }

        public static bool CheckConstraints(this MethodDesc method)
        {
            if (!method.OwningType.CheckConstraints())
                return false;

            // Non-generic methods always pass constraints check
            if (!method.HasInstantiation)
                return true;

            MethodDesc uninstantiatedMethod = method.GetMethodDefinition();
            for (int i = 0; i < uninstantiatedMethod.Instantiation.Length; i++)
            {
                if (!VerifyGenericParamConstraint(method.OwningType.Instantiation, method.Instantiation, (GenericParameterDesc)uninstantiatedMethod.Instantiation[i], method.Instantiation[i]))
                    return false;
            }

            return true;
        }
    }
}
