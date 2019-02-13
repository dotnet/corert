// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Internal.TypeSystem
{
    public static class ReadyToRunTypeSystemHelpers
    {
        public static MethodDesc TryResolveConstraintMethodApprox(this DefType constrainedType, DefType interfaceType, MethodDesc interfaceMethod, out bool forceRuntimeLookup)
        {
            forceRuntimeLookup = false;

            // We can't resolve constraint calls effectively for reference types, and there's
            // not a lot of perf. benefit in doing it anyway.
            if (!constrainedType.IsValueType)
            {
                return null;
            }

            // Non-virtual methods called through constraints simply resolve to the specified method without constraint resolution.
            if (!interfaceMethod.IsVirtual)
            {
                return null;
            }

            DefType constrainedCanonType = (DefType)constrainedType.ConvertToCanonForm(CanonicalFormKind.Specific);

            MethodDesc method = null;

            MethodDesc genInterfaceMethod = interfaceMethod.GetMethodDefinition();
            if (genInterfaceMethod.OwningType.IsInterface)
            {
                // Sometimes (when compiling shared generic code)
                // we don't have enough exact type information at JIT time
                // even to decide whether we will be able to resolve to an unboxed entry point...
                // To cope with this case we always go via the helper function if there's any
                // chance of this happening by checking for all interfaces which might possibly
                // be compatible with the call (verification will have ensured that
                // at least one of them will be)

                TypeDesc interfaceCanonType = interfaceType.ConvertToCanonForm(CanonicalFormKind.Specific);
                MethodDesc genInterfaceCanonMethod = genInterfaceMethod.GetCanonMethodTarget(CanonicalFormKind.Specific);

                // Enumerate all potential interface instantiations
                int potentialMatchingInterfaces = 0;
                foreach (MetadataType potentialInterface in constrainedCanonType.RuntimeInterfaces)
                {
                    if (potentialInterface.ConvertToCanonForm(CanonicalFormKind.Specific) == interfaceCanonType)
                    {
                        potentialMatchingInterfaces++;
                        method = constrainedCanonType.GetMethodDescForInterfaceMethod(potentialInterface, genInterfaceCanonMethod, throwOnConflict: false);
                        // See code:#TryResolveConstraintMethodApprox_DoNotReturnParentMethod
                        if (method != null && !method.OwningType.IsValueType)
                        {
                            Debug.WriteLine("TryResolveConstraintMethodApprox: {0} not a value type method\n", method);
                            return null;
                        }
                    }
                }

                if (potentialMatchingInterfaces == 0)
                {
                    throw new NotImplementedException($"At least one interface has to implement the method {genInterfaceMethod}, otherwise there's a bug in JIT/verification.");
                }

                if (potentialMatchingInterfaces > 1)
                {
                    // We have more potentially matching interfaces
                    Debug.Assert(interfaceType.HasInstantiation);

                    bool isExactMethodResolved = false;

                    if (!interfaceType.IsCanonicalSubtype(CanonicalFormKind.Specific) &&
                        !interfaceType.IsTypeDefinition &&
                        !constrainedType.IsCanonicalSubtype(CanonicalFormKind.Specific) &&
                        !constrainedType.IsTypeDefinition)
                    {
                        // We have exact interface and type instantiations (no generic variables and __Canon used  anywhere)
                        if (constrainedType.CanCastTo(interfaceType))
                        {
                            // We can resolve to exact method
                            method = constrainedType.GetMethodDescForInterfaceMethod((MetadataType)interfaceType, interfaceMethod, throwOnConflict: false);
                            isExactMethodResolved = (method != null);
                        }
                    }

                    if (!isExactMethodResolved)
                    {
                        // We couldn't resolve the interface statically
                        // Notify the caller that it should use runtime lookup
                        // Note that we can leave pMD incorrect, because we will use runtime lookup
                        forceRuntimeLookup = true;
                    }
                }
                else
                {
                    // If we can resolve the interface exactly then do so (e.g. when doing the exact 
                    // lookup at runtime, or when not sharing generic code).
                    if (constrainedCanonType.CanCastTo(interfaceType))
                    {
                        method = constrainedCanonType.GetMethodDescForInterfaceMethod((MetadataType)interfaceType, genInterfaceMethod, throwOnConflict: false);
                        if (method == null)
                        {
                            Debug.WriteLine("TryResolveConstraintMethodApprox: failed to find method desc for interface method\n");
                        }
                    }
                }
            }
            else if (genInterfaceMethod.IsVirtual)
            {
                method = constrainedType.FindVirtualFunctionTargetMethodOnObjectType(genInterfaceMethod);
            }
            else
            {
                // The method will be null if calling a non-virtual instance 
                // methods on System.Object, i.e. when these are used as a constraint.
                method = null;
            }

            if (method == null)
            {
                // Fall back to VSD
                return null;
            }

            //#TryResolveConstraintMethodApprox_DoNotReturnParentMethod
            // Only return a method if the value type itself declares the method, 
            // otherwise we might get a method from Object or System.ValueType
            if (!method.OwningType.IsValueType)
            {
                // Fall back to VSD
                return null;
            }

            // We've resolved the method, ignoring its generic method arguments
            // If the method is a generic method then go and get the instantiated descriptor
            if (interfaceMethod.HasInstantiation)
            {
                method = method.MakeInstantiatedMethod(interfaceMethod.Instantiation);
            }

            Debug.Assert(method != null);
            //assert(!pMD->IsUnboxingStub());

            return method;
        }

        private static MethodDesc GetMethodDescForInterfaceMethod(this DefType thisType, MetadataType interfaceType, MethodDesc interfaceMethod, bool throwOnConflict)
        {
            Debug.Assert(interfaceMethod.OwningType.IsInterface);

            MethodDesc result = null;
            MetadataType currentType = (MetadataType)thisType;
            do
            {
                result = currentType.ResolveInterfaceMethodToVirtualMethodOnType(interfaceType, interfaceMethod);
                currentType = currentType.MetadataBaseType;
            }
            while (result == null && currentType != null);

            return result;
        }

        private static MethodDesc ResolveInterfaceMethodToVirtualMethodOnType(this MetadataType currentType, MetadataType interfaceType, MethodDesc interfaceMethod)
        {
            if (currentType.IsInterface)
                return null;

            MethodDesc methodImpl = FindImplFromDeclFromMethodImpls(currentType, interfaceMethod);
            if (methodImpl != null)
                return methodImpl;

            // If interface is explicitly defined on a type, search for a name/sig match.
            bool foundExplicitInterface = IsInterfaceExplicitlyImplementedOnType(currentType, interfaceType);
            MetadataType baseType = currentType.MetadataBaseType;

            if (foundExplicitInterface)
            {
                MethodDesc foundOnCurrentType = FindMatchingVirtualMethodOnTypeByNameAndSig(interfaceMethod, currentType
                    , reverseMethodSearch: false /* When searching for name/sig overrides on a type that explicitly defines an interface, search through the type in the forward direction*/
                    , nameSigMatchMethodIsValidCandidate: null);
                foundOnCurrentType = FindSlotDefiningMethodForVirtualMethod(foundOnCurrentType);

                if (baseType == null)
                    return foundOnCurrentType;

                if (foundOnCurrentType == null && (ResolveInterfaceMethodToVirtualMethodOnType(baseType, interfaceType, interfaceMethod) == null))
                {
                    // TODO! Does this handle the case where the base type explicitly implements the interface, but is abstract
                    // and doesn't actually have an implementation?
                    if (!IsInterfaceImplementedOnType(baseType, interfaceType))
                    {
                        return FindNameSigOverrideForInterfaceMethodRecursive(interfaceMethod, baseType);
                    }
                }
                return foundOnCurrentType;
            }
            else
            {
                // Implicit interface case
                if (!IsInterfaceImplementedOnType(currentType, interfaceType))
                {
                    // If the interface isn't implemented on this type at all, don't go searching
                    return null;
                }

                // This is an implicitly implemented interface method. Only return a vlaue if this is the first type in the class
                // hierarchy that implements the interface. NOTE: If we pay attention to whether or not the parent type is 
                // abstract or not, we may be able to be more efficient here, but let's skip that for now
                MethodDesc baseClassImplementationOfInterfaceMethod = ResolveInterfaceMethodToVirtualMethodOnTypeRecursive(interfaceMethod, baseType);
                if (baseClassImplementationOfInterfaceMethod != null)
                {
                    return null;
                }
                else
                {
                    MethodDesc foundOnCurrentType = FindMatchingVirtualMethodOnTypeByNameAndSig(interfaceMethod, currentType
                                            , reverseMethodSearch: false /* When searching for name/sig overrides on a type that is the first type in the hierarchy to require the interface, search through the type in the forward direction*/
                                            , nameSigMatchMethodIsValidCandidate: null);

                    foundOnCurrentType = FindSlotDefiningMethodForVirtualMethod(foundOnCurrentType);

                    if (foundOnCurrentType != null)
                        return foundOnCurrentType;

                    return FindNameSigOverrideForInterfaceMethodRecursive(interfaceMethod, baseType);
                }
            }
        }

        private static MethodDesc FindNameSigOverrideForInterfaceMethodRecursive(MethodDesc interfaceMethod, MetadataType currentType)
        {
            while (true)
            {
                if (currentType == null)
                    return null;

                MethodDesc nameSigOverride = FindMatchingVirtualMethodOnTypeByNameAndSig(interfaceMethod, currentType
                    , reverseMethodSearch: true /* When searching for a name sig match for an interface on parent types search in reverse order of declaration */
                    , nameSigMatchMethodIsValidCandidate: null);

                if (nameSigOverride != null)
                {
                    return FindSlotDefiningMethodForVirtualMethod(nameSigOverride);
                }

                currentType = currentType.MetadataBaseType;
            }
        }

        private static MethodDesc ResolveInterfaceMethodToVirtualMethodOnTypeRecursive(MethodDesc interfaceMethod, MetadataType currentType)
        {
            while (true)
            {
                if (currentType == null)
                    return null;

                MetadataType interfaceType = (MetadataType)interfaceMethod.OwningType;

                if (!IsInterfaceImplementedOnType(currentType, interfaceType))
                {
                    // If the interface isn't implemented on this type at all, don't go searching
                    return null;
                }

                MethodDesc currentTypeInterfaceResolution = currentType.ResolveInterfaceMethodToVirtualMethodOnType(interfaceMethod);
                if (currentTypeInterfaceResolution != null)
                    return currentTypeInterfaceResolution;

                currentType = currentType.MetadataBaseType;
            }
        }


        private static MethodDesc FindMatchingVirtualMethodOnTypeByNameAndSig(MethodDesc targetMethod, DefType currentType, bool reverseMethodSearch, Func<MethodDesc, MethodDesc, bool> nameSigMatchMethodIsValidCandidate)
        {
            string name = targetMethod.Name;
            MethodSignature sig = targetMethod.Signature;

            MethodDesc implMethod = null;
            foreach (MethodDesc candidate in currentType.GetAllMethods())
            {
                if (!candidate.IsVirtual)
                    continue;

                if (candidate.Name == name)
                {
                    if (candidate.Signature.Equals(sig))
                    {
                        if (nameSigMatchMethodIsValidCandidate == null || nameSigMatchMethodIsValidCandidate(targetMethod, candidate))
                        {
                            implMethod = candidate;

                            // If reverseMethodSearch is enabled, we want to find the last match on this type, not the first
                            // (reverseMethodSearch is used for most matches except for searches for name/sig method matches for interface methods on the most derived type)
                            if (!reverseMethodSearch)
                                return implMethod;
                        }
                    }
                }
            }

            return implMethod;
        }


        private static bool IsInterfaceImplementedOnType(DefType type, MetadataType interfaceType)
        {
            foreach (TypeDesc iface in type.RuntimeInterfaces)
            {
                if (iface == interfaceType)
                    return true;
            }
            return false;
        }

        private static MethodDesc FindImplFromDeclFromMethodImpls(MetadataType type, MethodDesc decl)
        {
            MethodImplRecord[] foundMethodImpls = type.FindMethodsImplWithMatchingDeclName(decl.Name);

            if (foundMethodImpls == null)
                return null;

            bool interfaceDecl = decl.OwningType.IsInterface;

            foreach (MethodImplRecord record in foundMethodImpls)
            {
                MethodDesc recordDecl = record.Decl;

                if (interfaceDecl != recordDecl.OwningType.IsInterface)
                    continue;

                if (!interfaceDecl)
                    recordDecl = FindSlotDefiningMethodForVirtualMethod(recordDecl);

                if (recordDecl == decl)
                {
                    return FindSlotDefiningMethodForVirtualMethod(record.Body);
                }
            }

            return null;
        }

        private static bool IsInterfaceExplicitlyImplementedOnType(MetadataType type, MetadataType interfaceType)
        {
            foreach (TypeDesc iface in type.ExplicitlyImplementedInterfaces)
            {
                if (iface == interfaceType)
                    return true;
            }
            return false;
        }

        // This function looks for the base type method that defines the slot for a method
        // This is either the newslot method most derived that is in the parent hierarchy of method
        // or the least derived method that isn't newslot that matches by name and sig.
        public static MethodDesc FindSlotDefiningMethodForVirtualMethod(MethodDesc method)
        {
            if (method == null)
                return method;

            DefType currentType = method.OwningType.BaseType;

            // Loop until a newslot method is found
            while ((currentType != null) && !method.IsNewSlot)
            {
                MethodDesc foundMethod = FindMatchingVirtualMethodOnTypeByNameAndSig(method, currentType, reverseMethodSearch: true, nameSigMatchMethodIsValidCandidate: null);
                if (foundMethod != null)
                {
                    method = foundMethod;
                }

                currentType = currentType.BaseType;
            }

            // Newslot method found, or if not the least derived method that matches by name and
            // sig is to be returned.
            return method;
        }
    }
}
