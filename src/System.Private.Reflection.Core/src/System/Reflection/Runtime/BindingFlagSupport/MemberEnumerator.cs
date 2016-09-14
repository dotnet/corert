// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Reflection.Runtime.BindingFlagSupport
{
    internal static class MemberEnumerator
    {
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
    }
}
