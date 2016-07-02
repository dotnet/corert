// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using Debug = global::System.Diagnostics.Debug;

namespace Internal.Reflection.Execution
{
    internal static partial class ConstraintValidator
    {
        private static TypeInfo[] TypesToTypeInfos(Type[] types)
        {
            TypeInfo[] result = new TypeInfo[types.Length];
            for (int i = 0; i < types.Length; i++)
                result[i] = types[i].GetTypeInfo();
            return result;
        }

        private static bool SatisfiesConstraints(this TypeInfo genericVariable, SigTypeContext typeContextOfConstraintDeclarer, TypeInfo typeArg)
        {
            GenericParameterAttributes specialConstraints = genericVariable.GenericParameterAttributes & GenericParameterAttributes.SpecialConstraintMask;

            if ((specialConstraints & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0)
            {
                if (!typeArg.IsValueType)
                    return false;
                else
                {
                    // the type argument is a value type, however if it is any kind of Nullable we want to fail
                    // as the constraint accepts any value type except Nullable types (Nullable itself is a value type)
                    if (typeArg.IsNullable())
                        return false;
                }
            }

            if ((specialConstraints & GenericParameterAttributes.ReferenceTypeConstraint) != 0)
            {
                if (typeArg.IsValueType)
                    return false;
            }

            if ((specialConstraints & GenericParameterAttributes.DefaultConstructorConstraint) != 0)
            {
                if (!typeArg.HasExplicitOrImplicitPublicDefaultConstructor())
                    return false;
            }

            // Now check general subtype constraints
            foreach (var constraint in genericVariable.GetGenericParameterConstraints())
            {
                TypeInfo typeConstraint = constraint.GetTypeInfo();

                TypeInfo instantiatedTypeConstraint = typeConstraint.Instantiate(typeContextOfConstraintDeclarer);

                // System.Object constraint will be always satisfied - even if argList is empty
                if (instantiatedTypeConstraint.IsSystemObject())
                    continue;

                // if a concrete type can be cast to the constraint, then this constraint will be satisifed
                if (!AreTypesAssignable(typeArg, instantiatedTypeConstraint))
                    return false;
            }

            return true;
        }

        private static void EnsureSatisfiesClassConstraints(TypeInfo[] typeParameters, TypeInfo[] typeArguments, object definition, SigTypeContext typeContext)
        {
            if (typeParameters.Length != typeArguments.Length)
            {
                throw new ArgumentException(SR.Argument_GenericArgsCount);
            }

            // Do sanity validation of all arguments first. The actual constraint validation can fail in unexpected ways 
            // if it hits SigTypeContext with these never valid types.
            for (int i = 0; i < typeParameters.Length; i++)
            {
                TypeInfo actualArg = typeArguments[i];

                if (actualArg.IsSystemVoid() || (actualArg.HasElementType && !actualArg.IsArray))
                {
                    throw new ArgumentException(SR.Format(SR.Argument_NeverValidGenericArgument, actualArg));
                }
            }

            for (int i = 0; i < typeParameters.Length; i++)
            {
                TypeInfo formalArg = typeParameters[i];
                TypeInfo actualArg = typeArguments[i];

                if (!formalArg.SatisfiesConstraints(typeContext, actualArg))
                {
                    throw new ArgumentException(SR.Format(SR.Argument_ConstraintFailed, actualArg, definition.ToString(), formalArg),
                        String.Format("GenericArguments[{0}]", i));
                }
            }
        }

        public static void EnsureSatisfiesClassConstraints(TypeInfo typeDefinition, TypeInfo[] typeArguments)
        {
            TypeInfo[] typeParameters = TypesToTypeInfos(typeDefinition.GenericTypeParameters);
            SigTypeContext typeContext = new SigTypeContext(typeArguments, null);
            EnsureSatisfiesClassConstraints(typeParameters, typeArguments, typeDefinition, typeContext);
        }

        public static void EnsureSatisfiesClassConstraints(MethodInfo reflectionMethodInfo)
        {
            MethodInfo genericMethodDefinition = reflectionMethodInfo.GetGenericMethodDefinition();
            TypeInfo[] methodArguments = TypesToTypeInfos(reflectionMethodInfo.GetGenericArguments());
            TypeInfo[] methodParameters = TypesToTypeInfos(genericMethodDefinition.GetGenericArguments());
            TypeInfo[] typeArguments = TypesToTypeInfos(reflectionMethodInfo.DeclaringType.GetGenericArguments());
            SigTypeContext typeContext = new SigTypeContext(typeArguments, methodArguments);
            EnsureSatisfiesClassConstraints(methodParameters, methodArguments, genericMethodDefinition, typeContext);
        }
    }
}
