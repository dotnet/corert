// Licensed to the .NET Foundation under one or more agreements.
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
        internal static bool CanAccess(this EcmaType currentClass, EcmaType targetClass)
        {
            // Check access to class instantiations if generic class
            if (targetClass.HasInstantiation)
            {
                foreach (var inst in targetClass.Instantiation)
                {
                    if (!currentClass.CanAccess((EcmaType)inst))
                        return false;
                }
            }

            if (targetClass.ContainingType == null)
            {
                // a non-nested class can be either all public or accessible only from its own assembly (and friends)
                if ((targetClass.Attributes & TypeAttributes.Public) != 0)
                    return true;
                else
                {
                    return currentClass.Module == targetClass.Module;
#if false
                    return (pTargetAssembly == pCurrentAssembly) || pTargetAssembly->GrantsFriendAccessTo(pCurrentAssembly);
#endif
                }
            }

            // Target class is nested
            MethodAttributes visibility = NestedToMethodAccess(targetClass.Attributes);

            // Translate access check into member access check, i.e. check whether the current class can access
            // a member of the enclosing class with the visibility of target class
            return currentClass.CanAccessMember((EcmaType)targetClass.ContainingType, visibility);
        }

        internal static bool CanAccess(this EcmaType currentType, EcmaMethod targetMethod)
        {
            // If generic method, check instantiation access
            if (!currentType.CanAccessMethodInstantiation(targetMethod))
                return false;

            return currentType.CanAccessMember((EcmaType)targetMethod.OwningType, targetMethod.Attributes & MethodAttributes.MemberAccessMask);
        }

        private static bool CanAccessMember(this EcmaType currentType, EcmaType targetType, MethodAttributes memberVisibility)
        {
            // Check access to class defining member
            if (!currentType.CanAccess(targetType))
                return false;

#if false
            // if caller is transparent, and target is non-public and critical, then fail access check
            if (!CheckTransparentAccessToCriticalCode(pCurrentMD, dwMemberAccess, pTargetMT, pOptionalTargetMethod, pOptionalTargetField))
                return FALSE;
#endif

            if (memberVisibility == MethodAttributes.Public)
                return true;

            // This is module-scope checking, to support C++ file & function statics.
            if (memberVisibility == MethodAttributes.PrivateScope)
                return currentType.Module == targetType.Module;

            if (memberVisibility == MethodAttributes.Assembly)
            {
#if false
                return (pCurrentAssembly == pTargetAssembly || pTargetAssembly->GrantsFriendAccessTo(pCurrentAssembly));
#endif
                return currentType.Module == targetType.Module;
            }

            if (memberVisibility == MethodAttributes.FamANDAssem)
            {
#if false
                if ((pCurrentAssembly != pTargetAssembly) && !pTargetAssembly->GrantsFriendAccessTo(pCurrentAssembly))
                    return false;
#endif
                if (currentType.Module != targetType.Module)
                    return false;
            }

            // Nested classes can access all members of their parent class.
            var targetTypeDef = targetType.GetTypeDefinition();
            do
            {
                // Classes have access to all of their own members
                if (currentType.GetTypeDefinition() == targetTypeDef)
                    return true;

                switch (memberVisibility)
                {
                    case MethodAttributes.FamORAssem:
#if false
                        // If the current assembly is same as the desired target, or if it grants friend access, allow access.
                        if (pCurrentAssembly == pTargetAssembly || pTargetAssembly->GrantsFriendAccessTo(pCurrentAssembly))
                            return TRUE;
#endif
                        if (currentType.Module == targetType.Module)
                            return true;

                        // Check if current class is subclass of target
                        if (IsSubclassOf(currentType, targetType))
                            return true;
                        break;
                    case MethodAttributes.Family:
                    case MethodAttributes.FamANDAssem:
                        // Assembly acces was already checked earlier, so only need to check family access
                        if (IsSubclassOf(currentType, targetType))
                            return true;
                        break;
                    case MethodAttributes.Private:
                        break; // Already handled by loop
                    default:
                        Debug.Assert(false);
                        break;
                }

                currentType = (EcmaType)currentType.ContainingType;
            } while (currentType != null);

            return false;
        }

        private static bool CanAccessMethodInstantiation(this EcmaType currentType, EcmaMethod targetMethod)
        {
            if (targetMethod.HasInstantiation)
            {
                foreach (var inst in targetMethod.Instantiation)
                {
                    if (!currentType.CanAccess((EcmaType)inst))
                        return false;
                }
            }

            return true;
        }

        private static MethodAttributes NestedToMethodAccess(TypeAttributes nestedVisibility)
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

        private static bool IsSubclassOf(DefType currentType, DefType targetType)
        {
            var targetTypeDef = targetType.GetTypeDefinition();
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
