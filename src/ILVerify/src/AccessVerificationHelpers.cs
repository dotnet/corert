﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Reflection;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILVerify
{
    internal static class AccessVerificationHelpers
    {
        /// <summary>
        /// Returns whether the class <paramref name="currentClass"/> can access the class <paramref name="targetClass"/>.
        /// </summary>
        internal static bool CanAccess(this TypeDesc currentClass, TypeDesc targetClass)
        {
            if (targetClass.IsGenericParameter || targetClass.IsSignatureVariable)
                return true; // Generic parameters are always accessible

            if (targetClass.IsArray)
                return currentClass.CanAccess(targetClass = ((ArrayType)targetClass).ParameterType);

            // Check access to class instantiations if generic class
            if (targetClass.HasInstantiation && !currentClass.CanAccessInstantiation(targetClass.Instantiation))
                return false;

            var currentTypeDef = (EcmaType)currentClass.GetTypeDefinition();
            var targetTypeDef = (EcmaType)targetClass.GetTypeDefinition();

            var targetContainingType = targetTypeDef.ContainingType;
            if (targetContainingType == null)
            {
                // a non-nested class can be either all public or accessible only from its own assembly (and friends)
                if ((targetTypeDef.Attributes & TypeAttributes.Public) != 0)
                    return true;
                else
                {
                    return currentTypeDef.Module == targetTypeDef.Module;
#if false
                    return (pTargetAssembly == pCurrentAssembly) || pTargetAssembly->GrantsFriendAccessTo(pCurrentAssembly);
#endif
                }
            }

            // Target class is nested
            MethodAttributes visibility = NestedToMethodAccessAttribute(targetTypeDef.Attributes);

            // Translate access check into member access check, i.e. check whether the current class can access
            // a member of the enclosing class with the visibility of target class
            return currentTypeDef.CanAccessMember(targetContainingType, visibility);
        }

        /// <summary>
        /// Returns whether the class <paramref name="currentClass"/> can access the method <paramref name="targetMethod"/>.
        /// </summary>
        internal static bool CanAccess(this TypeDesc currentType, MethodDesc targetMethod)
        {
            // If generic method, check instantiation access
            if (targetMethod.HasInstantiation && !currentType.CanAccessInstantiation(targetMethod.Instantiation))
                return false;

            var targetMethodDef = (EcmaMethod)targetMethod.GetTypicalMethodDefinition();
            var currentTypeDef = (EcmaType)currentType.GetTypeDefinition();

            if (!currentTypeDef.CanAccessMember(targetMethod.OwningType, targetMethodDef.Attributes & MethodAttributes.MemberAccessMask))
                return false;

            return currentTypeDef.CanAccessMethodSignature(targetMethod);
        }

        /// <summary>
        /// Returns whether the class <paramref name="currentClass"/> can access the field <paramref name="targetField"/>.
        /// </summary>
        internal static bool CanAccess(this TypeDesc currentType, FieldDesc targetField)
        {
            // Check access to field owning type
            var targetFieldDef = (EcmaField)targetField.GetTypicalFieldDefinition();
            var currentTypeDef = (EcmaType)currentType.GetTypeDefinition();

            var targetFieldAccess = FieldToMethodAccessAttribute(targetFieldDef.Attributes);

            if (!currentTypeDef.CanAccessMember(targetField.OwningType, targetFieldAccess))
                return false;

            // Check access to field type itself
            return currentType.CanAccess(targetField.FieldType);
        }

        private static bool CanAccessMember(this EcmaType currentType, TypeDesc targetType, MethodAttributes memberVisibility)
        {
            // Check access to class defining member
            if (!currentType.CanAccess(targetType))
                return false;

            var targetTypeDef = (EcmaType)targetType.GetTypeDefinition();
#if false
            // if caller is transparent, and target is non-public and critical, then fail access check
            if (!CheckTransparentAccessToCriticalCode(pCurrentMD, dwMemberAccess, pTargetMT, pOptionalTargetMethod, pOptionalTargetField))
                return FALSE;
#endif

            if (memberVisibility == MethodAttributes.Public)
                return true;

            // This is module-scope checking, to support C++ file & function statics.
            if (memberVisibility == MethodAttributes.PrivateScope)
                return currentType.Module == targetTypeDef.Module;

            if (memberVisibility == MethodAttributes.Assembly)
            {
#if false
                return (pCurrentAssembly == pTargetAssembly || pTargetAssembly->GrantsFriendAccessTo(pCurrentAssembly));
#endif
                return currentType.Module == targetTypeDef.Module;
            }

            if (memberVisibility == MethodAttributes.FamANDAssem)
            {
#if false
                if ((pCurrentAssembly != pTargetAssembly) && !pTargetAssembly->GrantsFriendAccessTo(pCurrentAssembly))
                    return false;
#endif
                if (currentType.Module != targetTypeDef.Module)
                    return false;
            }

            // Nested classes can access all members of their parent class.
            do
            {
                // Classes have access to all of their own members
                if (currentType == targetTypeDef)
                    return true;

                switch (memberVisibility)
                {
                    case MethodAttributes.FamORAssem:
#if false
                        // If the current assembly is same as the desired target, or if it grants friend access, allow access.
                        if (pCurrentAssembly == pTargetAssembly || pTargetAssembly->GrantsFriendAccessTo(pCurrentAssembly))
                            return TRUE;
#endif
                        if (currentType.Module == targetTypeDef.Module)
                            return true;

                        // Check if current class is subclass of target
                        if (IsSubclassOf(currentType, targetTypeDef))
                            return true;
                        break;
                    case MethodAttributes.Family:
                    case MethodAttributes.FamANDAssem:
                        // Assembly acces was already checked earlier, so only need to check family access
                        if (IsSubclassOf(currentType, targetTypeDef))
                            return true;
                        break;
                    case MethodAttributes.Private:
                        break; // Already handled by loop
                    default:
                        Debug.Assert(false);
                        break;
                }

                var containingType = currentType.ContainingType;
                if (containingType != null)
                    currentType = (EcmaType)containingType.GetTypeDefinition();
                else
                    currentType = null;
            } while (currentType != null);

            return false;
        }

        private static bool CanAccessInstantiation(this TypeDesc currentType, Instantiation instantiation)
        {
            foreach (var inst in instantiation)
            {
                if (!currentType.CanAccess(inst))
                    return false;
            }

            return true;
        }

        private static bool CanAccessMethodSignature(this TypeDesc currentType, MethodDesc targetMethod)
        {
            var methodSig = targetMethod.Signature;

            // Check return type
            var returnType = methodSig.ReturnType;
            if (returnType.IsByRef)
                returnType = ((ByRefType)returnType).ParameterType;

            if (!returnType.IsGenericParameter && !returnType.IsSignatureVariable // Generic parameters are always accessible
                && !returnType.IsVoid)
            {
                if (!currentType.CanAccess(returnType))
                    return false;
            }

            // Check arguments
            for (int i = 0; i < methodSig.Length; ++i)
            {
                var param = methodSig[i];
                if (param.IsByRef)
                    param = ((ByRefType)param).ParameterType;

                if (param.IsGenericParameter || param.IsSignatureVariable)
                    continue; // Generic parameters are always accessible

                if (!currentType.CanAccess(param))
                    return false;
            }

            return true;
        }

        private static MethodAttributes NestedToMethodAccessAttribute(TypeAttributes nestedVisibility)
        {
            switch (nestedVisibility & TypeAttributes.VisibilityMask)
            {
                case TypeAttributes.NestedAssembly:
                    return MethodAttributes.Assembly;
                case TypeAttributes.NestedFamANDAssem:
                    return MethodAttributes.FamANDAssem;
                case TypeAttributes.NestedFamily:
                    return MethodAttributes.Family;
                case TypeAttributes.NestedFamORAssem:
                    return MethodAttributes.FamORAssem;
                case TypeAttributes.NestedPrivate:
                    return MethodAttributes.Private;
                case TypeAttributes.NestedPublic:
                    return MethodAttributes.Public;
                default:
                    Debug.Assert(false);
                    return MethodAttributes.Public;
            }
        }

        private static MethodAttributes FieldToMethodAccessAttribute(FieldAttributes attributes)
        {
            switch (attributes & FieldAttributes.FieldAccessMask)
            {
                case FieldAttributes.Assembly:
                    return MethodAttributes.Assembly;
                case FieldAttributes.FamANDAssem:
                    return MethodAttributes.FamANDAssem;
                case FieldAttributes.Family:
                    return MethodAttributes.Family;
                case FieldAttributes.FamORAssem:
                    return MethodAttributes.FamORAssem;
                case FieldAttributes.Private:
                    return MethodAttributes.Private;
                case FieldAttributes.PrivateScope:
                    return MethodAttributes.PrivateScope;
                case FieldAttributes.Public:
                    return MethodAttributes.Public;
                default:
                    Debug.Assert(false);
                    return MethodAttributes.Public;
            }
        }

        private static bool IsSubclassOf(TypeDesc currentType, TypeDesc targetTypeDef)
        {
            while (currentType != null)
            {
                if (currentType.GetTypeDefinition() == targetTypeDef)
                    return true;

                currentType = currentType.BaseType;
            }

            return false;
        }
    }
}
