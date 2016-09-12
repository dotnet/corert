// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Collections.Generic;

namespace System.Reflection.Runtime.BindingFlagSupport
{
    internal static class MemberEnumerator
    {
        //
        // Enumerates members, optionally filtered by a name, in the given class and its base classes (but not implemented interfaces.)
        // Basically emulates the old Type.GetFoo(BindingFlags) api.
        //
        public static IEnumerable<M> GetMembers<M>(this Type type, Object nameFilterOrAnyName, BindingFlags bindingFlags, bool allowPrefixing = false) where M : MemberInfo
        {
            // Do all the up-front argument validation here so that the exception occurs on call rather than on the first move.
            if (type == null)
            {
                throw new ArgumentNullException();
            }
            if (nameFilterOrAnyName == null)
            {
                throw new ArgumentNullException();
            }

            String optionalNameFilter;
            if (nameFilterOrAnyName == AnyName)
            {
                optionalNameFilter = null;
            }
            else
            {
                optionalNameFilter = (String)nameFilterOrAnyName;
            }

            return Stage2Filter<M>(type, optionalNameFilter, bindingFlags, allowPrefixing);
        }

        //
        // Take the result of Stage1Filter and filter by the BindingFlag bits.
        //
        private static IEnumerable<M> Stage2Filter<M>(Type type, String optionalNameFilter, BindingFlags bindingFlags, bool allowPrefixing) where M : MemberInfo
        {
            MemberPolicies<M> policies = MemberPolicies<M>.Default;
            bindingFlags = policies.ModifyBindingFlags(bindingFlags);
            bool ignoreCase = (bindingFlags & BindingFlags.IgnoreCase) != 0;
            bool declaredOnly = (bindingFlags & BindingFlags.DeclaredOnly) != 0;
            QueriedMemberList<M> queriedMembers = Stage1Filter<M>(type, optionalNameFilter, ignoreCase: ignoreCase, declaredOnly: declaredOnly, allowPrefixing: allowPrefixing);
            for (int i = 0; i < queriedMembers.Count; i++)
            {
                BindingFlags allFlagsThatMustMatch = queriedMembers.AllFlagsThatMustMatchNoCopy[i];
                if ((bindingFlags & allFlagsThatMustMatch) == allFlagsThatMustMatch)
                    yield return queriedMembers.MembersNoCopy[i];
            }
        }

        //
        // Filter by name and visibility from the ReflectedType.
        //
        private static QueriedMemberList<M> Stage1Filter<M>(Type type, String optionalNameFilter, bool ignoreCase, bool declaredOnly, bool allowPrefixing) where M : MemberInfo
        {
            Type reflectedType = type;

            MemberPolicies<M> policies = MemberPolicies<M>.Default;

            StringComparison comparisonType = ignoreCase ? StringComparison.CurrentCultureIgnoreCase : StringComparison.CurrentCulture;
            bool inBaseClass = false;

            bool nameFilterIsPrefix = false;
            if (allowPrefixing && optionalNameFilter != null && optionalNameFilter.EndsWith("*", StringComparison.Ordinal))
            {
                nameFilterIsPrefix = true;
                optionalNameFilter = optionalNameFilter.Substring(0, optionalNameFilter.Length - 1);
            }

            QueriedMemberList<M> queriedMembers = new QueriedMemberList<M>();
            while (type != null)
            {
                int numCandidatesInDerivedTypes = queriedMembers.Count;

                TypeInfo typeInfo = type.GetTypeInfo();

                foreach (M member in policies.GetDeclaredMembers(typeInfo))
                {
                    if (optionalNameFilter != null)
                    {
                        if (nameFilterIsPrefix)
                        {
                            if (!member.Name.StartsWith(optionalNameFilter, comparisonType))
                            {
                                continue;
                            }
                        }
                        else if (!member.Name.Equals(optionalNameFilter, comparisonType))
                        {
                            continue;
                        }
                    }

                    MethodAttributes visibility;
                    bool isStatic;
                    bool isVirtual;
                    bool isNewSlot;
                    policies.GetMemberAttributes(member, out visibility, out isStatic, out isVirtual, out isNewSlot);

                    if (inBaseClass && visibility == MethodAttributes.Private)
                        continue;
                
                    if (numCandidatesInDerivedTypes != 0 && policies.IsSuppressedByMoreDerivedMember(member, queriedMembers.MembersNoCopy, startIndex: 0, endIndex: numCandidatesInDerivedTypes))
                        continue;

                    BindingFlags allFlagsThatMustMatch = (BindingFlags)0;
                    allFlagsThatMustMatch |= (isStatic ? BindingFlags.Static : BindingFlags.Instance);
                    if (isStatic && inBaseClass)
                        allFlagsThatMustMatch |= BindingFlags.FlattenHierarchy;
                    allFlagsThatMustMatch |= ((visibility == MethodAttributes.Public) ? BindingFlags.Public : BindingFlags.NonPublic);
                    
                    if (inBaseClass)
                    {
                        queriedMembers.Add(policies.GetInheritedMemberInfo(member, reflectedType), allFlagsThatMustMatch);
                    }
                    else
                    {
                        queriedMembers.Add(member, allFlagsThatMustMatch);
                    }
                }

                if (declaredOnly)
                    break;

                inBaseClass = true;
                type = typeInfo.BaseType;
            }

            return queriedMembers;
        }

        //
        // If member is a virtual member that implicitly overrides a member in a base class, return the overridden member.
        // Otherwise, return null.
        //
        // - MethodImpls ignored. (I didn't say it made sense, this is just how the desktop api we're porting behaves.)
        // - Implemented interfaces ignores. (I didn't say it made sense, this is just how the desktop api we're porting behaves.) 
        //
        public static M GetImplicitlyOverriddenBaseClassMember<M>(this M member) where M : MemberInfo
        {
            MemberPolicies<M> policies = MemberPolicies<M>.Default;
            MethodAttributes visibility;
            bool isStatic;
            bool isVirtual;
            bool isNewSlot;
            policies.GetMemberAttributes(member, out visibility, out isStatic, out isVirtual, out isNewSlot);
            if (isNewSlot || !isVirtual)
            {
                return null;
            }
            String name = member.Name;
            TypeInfo typeInfo = member.DeclaringType.GetTypeInfo();
            for (; ;)
            {
                Type baseType = typeInfo.BaseType;
                if (baseType == null)
                {
                    return null;
                }
                typeInfo = baseType.GetTypeInfo();
                foreach (M candidate in policies.GetDeclaredMembers(typeInfo))
                {
                    if (candidate.Name != name)
                    {
                        continue;
                    }
                    MethodAttributes candidateVisibility;
                    bool isCandidateStatic;
                    bool isCandidateVirtual;
                    bool isCandidateNewSlot;
                    policies.GetMemberAttributes(member, out candidateVisibility, out isCandidateStatic, out isCandidateVirtual, out isCandidateNewSlot);
                    if (!isCandidateVirtual)
                    {
                        continue;
                    }
                    if (!policies.AreNamesAndSignatureEqual(member, candidate))
                    {
                        continue;
                    }
                    return candidate;
                }
            }
        }

        // Uniquely allocated sentinel "string"
        //  - can't use null as that may be an app-supplied null, which we have to throw ArgumentNullException for.
        //  - risky to use a proper String as the FX or toolchain can unexpectedly give you back a shared string
        //    even when you'd swear you were allocating a new one.
        public static readonly Object AnyName = new Object();
    }
}
