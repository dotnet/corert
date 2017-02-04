// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System.Diagnostics;

namespace Internal.TypeSystem
{
    public static class TypeSystemConstraintsHelpers
    {
        private static bool VerifyGenericParamConstraint(Instantiation typeInstantiation, Instantiation methodInstantiation, GenericParameterDesc genericParam, TypeDesc instantiationParam)
        {
            // Check class constraint
            if (genericParam.HasReferenceTypeConstraint)
            {
                if (!instantiationParam.IsGCPointer)
                    return false;
            }

            // Check default constructor constraint
            if (genericParam.HasDefaultConstructorConstraint)
            {
                if (!instantiationParam.HasExplicitOrImplicitDefaultConstructor())
                    return false;
            }

            // Check struct constraint
            if (genericParam.HasNotNullableValueTypeConstraint)
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
