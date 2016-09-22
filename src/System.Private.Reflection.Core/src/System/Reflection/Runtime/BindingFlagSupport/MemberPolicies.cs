// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection.Runtime.TypeInfos;

namespace System.Reflection.Runtime.BindingFlagSupport
{
    //=================================================================================================================
    // This class encapsulates the minimum set of arcane desktop CLR policies needed to implement the Get*(BindingFlags) apis.
    //
    // In particular, it encapsulates behaviors such as what exactly determines the "visibility" of a property and event, and
    // what determines whether and how they are overridden.
    //=================================================================================================================
    internal abstract class MemberPolicies<M> where M : MemberInfo
    {
        //=================================================================================================================
        // Subclasses for specific MemberInfo types must override these:
        //=================================================================================================================

        //
        // Returns all of the directly declared members on the given TypeInfo.
        //
        public abstract IEnumerable<M> GetDeclaredMembers(TypeInfo typeInfo);

        //
        // Returns all of the directly declared members on the given TypeInfo whose name matches optionalNameFilter. If optionalNameFilter is null,
        // returns all directly declared members.
        //
        public abstract IEnumerable<M> CoreGetDeclaredMembers(RuntimeTypeInfo type, NameFilter optionalNameFilter, RuntimeTypeInfo reflectedType);

        //
        // Policy to decide whether a member is considered "virtual", "virtual new" and what its member visibility is.
        // (For "visibility", we reuse the MethodAttributes enum since Reflection lacks an element-agnostic enum for this.
        //  Only the MemberAccessMask bits are set.)
        //
        public abstract void GetMemberAttributes(M member, out MethodAttributes visibility, out bool isStatic, out bool isVirtual, out bool isNewSlot);

        //
        // Policy to decide whether two virtual members are signature-compatible for the purpose of implicit overriding. 
        //
        public abstract bool AreNamesAndSignatureEqual(M member1, M member2);

        //
        // Policy to decide how BindingFlags should be reinterpreted for a given member type.
        // This is overridden for nested types which all match on any combination Instance | Static and are never inherited.
        // It is also overridden for constructors which are never inherited.
        //
        public virtual BindingFlags ModifyBindingFlags(BindingFlags bindingFlags)
        {
            return bindingFlags;
        }

        //
        // Policy to decide if BindingFlags is always interpreted as having set DeclaredOnly.
        //
        public abstract bool AlwaysTreatAsDeclaredOnly { get; }

        //
        // Policy to decide how or if members in more derived types hide same-named members in base types.
        // Due to desktop compat concerns, the definitions are a bit more arbitrary than we'd like.
        //
        public abstract bool IsSuppressedByMoreDerivedMember(M member, M[] priorMembers, int startIndex, int endIndex);

        //
        // Policy to decide whether to throw an AmbiguousMatchException on an ambiguous Type.Get*() call.
        // Does not apply to GetConstructor/GetMethod/GetProperty calls that have a non-null Type[] array passed to it.
        //
        // If method returns true, the Get() api will pick the member that's in the most derived type.
        // If method returns false, the Get() api throws AmbiguousMatchException.
        //
        public abstract bool OkToIgnoreAmbiguity(M m1, M m2);

        //
        // Helper method for determining whether two methods are signature-compatible for the purpose of implicit overriding.
        //
        protected static bool AreNamesAndSignaturesEqual(MethodInfo method1, MethodInfo method2)
        {
            if (method1.Name != method2.Name)
            {
                return false;
            }

            ParameterInfo[] p1 = method1.GetParametersNoCopy();
            ParameterInfo[] p2 = method2.GetParametersNoCopy();
            if (p1.Length != p2.Length)
            {
                return false;
            }

            for (int i = 0; i < p1.Length; i++)
            {
                Type parameterType1 = p1[i].ParameterType;
                Type parameterType2 = p2[i].ParameterType;
                if (!(parameterType1.Equals(parameterType2)))
                {
                    return false;
                }
            }
            return true;
        }

        static MemberPolicies()
        {
            Type t = typeof(M);
            if (t.Equals(typeof(FieldInfo)))
            {
                MemberTypeIndex = BindingFlagSupport.MemberTypeIndex.Field;
                Default = (MemberPolicies<M>)(Object)(new FieldPolicies());
            }
            else if (t.Equals(typeof(MethodInfo)))
            {
                MemberTypeIndex = BindingFlagSupport.MemberTypeIndex.Method;
                Default = (MemberPolicies<M>)(Object)(new MethodPolicies());
            }
            else if (t.Equals(typeof(ConstructorInfo)))
            {
                MemberTypeIndex = BindingFlagSupport.MemberTypeIndex.Constructor;
                Default = (MemberPolicies<M>)(Object)(new ConstructorPolicies());
            }
            else if (t.Equals(typeof(PropertyInfo)))
            {
                MemberTypeIndex = BindingFlagSupport.MemberTypeIndex.Property; ;
                Default = (MemberPolicies<M>)(Object)(new PropertyPolicies());
            }
            else if (t.Equals(typeof(EventInfo)))
            {
                MemberTypeIndex = BindingFlagSupport.MemberTypeIndex.Event;
                Default = (MemberPolicies<M>)(Object)(new EventPolicies());
            }
            else if (t.Equals(typeof(Type)))
            {
                MemberTypeIndex = BindingFlagSupport.MemberTypeIndex.NestedType;
                Default = (MemberPolicies<M>)(Object)(new NestedTypePolicies());
            }
            else
            {
                Debug.Assert(false, "Unknown MemberInfo type.");
            }
        }

        //
        // This is a singleton class one for each MemberInfo category: Return the appropriate one. 
        //
        public static readonly MemberPolicies<M> Default;

        //
        // This returns a fixed value from 0 to MemberIndex.Count-1 with each possible type of M 
        // being assigned a unique index (see the MemberTypeIndex for possible values). This is useful
        // for converting a type reference to M to an array index or switch case label.
        //
        public static readonly int MemberTypeIndex;
    }
}
