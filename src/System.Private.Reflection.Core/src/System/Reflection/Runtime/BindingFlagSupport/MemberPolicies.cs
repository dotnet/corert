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

    //==========================================================================================================================
    // Policies for fields.
    //==========================================================================================================================
    internal sealed class FieldPolicies : MemberPolicies<FieldInfo>
    {
        public sealed override IEnumerable<FieldInfo> GetDeclaredMembers(TypeInfo typeInfo)
        {
            return typeInfo.DeclaredFields;
        }

        public sealed override IEnumerable<FieldInfo> CoreGetDeclaredMembers(RuntimeTypeInfo type, NameFilter optionalNameFilter, RuntimeTypeInfo reflectedType)
        {
            return type.CoreGetDeclaredFields(optionalNameFilter, reflectedType);
        }

        public sealed override bool AlwaysTreatAsDeclaredOnly => false;

        public sealed override void GetMemberAttributes(FieldInfo member, out MethodAttributes visibility, out bool isStatic, out bool isVirtual, out bool isNewSlot)
        {
            FieldAttributes fieldAttributes = member.Attributes;
            visibility = (MethodAttributes)(fieldAttributes & FieldAttributes.FieldAccessMask);
            isStatic = (0 != (fieldAttributes & FieldAttributes.Static));
            isVirtual = false;
            isNewSlot = false;
        }

        public sealed override bool AreNamesAndSignatureEqual(FieldInfo member1, FieldInfo member2)
        {
            Debug.Assert(false, "This code path should be unreachable as fields are never \"virtual\".");
            throw new NotSupportedException();
        }

        public sealed override bool IsSuppressedByMoreDerivedMember(FieldInfo member, FieldInfo[] priorMembers, int startIndex, int endIndex)
        {
            return false;
        }
    }


    //==========================================================================================================================
    // Policies for constructors.
    //==========================================================================================================================
    internal sealed class ConstructorPolicies : MemberPolicies<ConstructorInfo>
    {
        public sealed override IEnumerable<ConstructorInfo> GetDeclaredMembers(TypeInfo typeInfo)
        {
            return typeInfo.DeclaredConstructors;
        }

        public sealed override IEnumerable<ConstructorInfo> CoreGetDeclaredMembers(RuntimeTypeInfo type, NameFilter optionalNameFilter, RuntimeTypeInfo reflectedType)
        {
            Debug.Assert(reflectedType.Equals(type));  // Constructor queries are always performed as if BindingFlags.DeclaredOnly are set so the reflectedType should always be the declaring type.
            return type.CoreGetDeclaredConstructors(optionalNameFilter);
        }

        public sealed override BindingFlags ModifyBindingFlags(BindingFlags bindingFlags)
        {
            // Constructors are not inherited.
            return bindingFlags | BindingFlags.DeclaredOnly;
        }

        public sealed override bool AlwaysTreatAsDeclaredOnly => true;

        public sealed override void GetMemberAttributes(ConstructorInfo member, out MethodAttributes visibility, out bool isStatic, out bool isVirtual, out bool isNewSlot)
        {
            MethodAttributes methodAttributes = member.Attributes;
            visibility = methodAttributes & MethodAttributes.MemberAccessMask;
            isStatic = (0 != (methodAttributes & MethodAttributes.Static));
            isVirtual = false;
            isNewSlot = false;
        }

        public sealed override bool AreNamesAndSignatureEqual(ConstructorInfo member1, ConstructorInfo member2)
        {
            Debug.Assert(false, "This code path should be unreachable as constructors are never \"virtual\".");
            throw new NotSupportedException();
        }

        public sealed override bool IsSuppressedByMoreDerivedMember(ConstructorInfo member, ConstructorInfo[] priorMembers, int startIndex, int endIndex)
        {
            return false;
        }
    }


    //==========================================================================================================================
    // Policies for methods.
    //==========================================================================================================================
    internal sealed class MethodPolicies : MemberPolicies<MethodInfo>
    {
        public sealed override IEnumerable<MethodInfo> GetDeclaredMembers(TypeInfo typeInfo)
        {
            return typeInfo.DeclaredMethods;
        }

        public sealed override IEnumerable<MethodInfo> CoreGetDeclaredMembers(RuntimeTypeInfo type, NameFilter optionalNameFilter, RuntimeTypeInfo reflectedType)
        {
            return type.CoreGetDeclaredMethods(optionalNameFilter, reflectedType);
        }

        public sealed override bool AlwaysTreatAsDeclaredOnly => false;

        public sealed override void GetMemberAttributes(MethodInfo member, out MethodAttributes visibility, out bool isStatic, out bool isVirtual, out bool isNewSlot)
        {
            MethodAttributes methodAttributes = member.Attributes;
            visibility = methodAttributes & MethodAttributes.MemberAccessMask;
            isStatic = (0 != (methodAttributes & MethodAttributes.Static));
            isVirtual = (0 != (methodAttributes & MethodAttributes.Virtual));
            isNewSlot = (0 != (methodAttributes & MethodAttributes.NewSlot));
        }

        public sealed override bool AreNamesAndSignatureEqual(MethodInfo member1, MethodInfo member2)
        {
            return AreNamesAndSignaturesEqual(member1, member2);
        }

        //
        // Methods hide methods in base types if they share the same vtable slot.
        //
        public sealed override bool IsSuppressedByMoreDerivedMember(MethodInfo member, MethodInfo[] priorMembers, int startIndex, int endIndex)
        {
            if (!member.IsVirtual)
                return false;

            for (int i = startIndex; i < endIndex; i++)
            {
                MethodInfo prior = priorMembers[i];
                MethodAttributes attributes = prior.Attributes & (MethodAttributes.Virtual | MethodAttributes.VtableLayoutMask);
                if (attributes != (MethodAttributes.Virtual | MethodAttributes.ReuseSlot))
                    continue;
                if (!AreNamesAndSignatureEqual(prior, member))
                    continue;

                return true;
            }
            return false;
        }
    }

    //==========================================================================================================================
    // Policies for properties.
    //==========================================================================================================================
    internal sealed class PropertyPolicies : MemberPolicies<PropertyInfo>
    {
        public sealed override IEnumerable<PropertyInfo> GetDeclaredMembers(TypeInfo typeInfo)
        {
            return typeInfo.DeclaredProperties;
        }

        public sealed override IEnumerable<PropertyInfo> CoreGetDeclaredMembers(RuntimeTypeInfo type, NameFilter optionalNameFilter, RuntimeTypeInfo reflectedType)
        {
            return type.CoreGetDeclaredProperties(optionalNameFilter, reflectedType);
        }

        public sealed override bool AlwaysTreatAsDeclaredOnly => false;

        public sealed override void GetMemberAttributes(PropertyInfo member, out MethodAttributes visibility, out bool isStatic, out bool isVirtual, out bool isNewSlot)
        {
            MethodInfo accessorMethod = GetAccessorMethod(member);
            if (accessorMethod == null)
            {
                // If we got here, this is a inherited PropertyInfo that only had private accessors and is now refusing to give them out
                // because that's what the rules of inherited PropertyInfo's are. Such a PropertyInfo is also considered private and will never be
                // given out of a Type.GetProperty() call. So all we have to do is set its visibility to Private and it will get filtered out.
                // Other values need to be set to satisify C# but they are meaningless.
                visibility = MethodAttributes.Private;
                isStatic = false;
                isVirtual = false;
                isNewSlot = true;
                return;
            }

            MethodAttributes methodAttributes = accessorMethod.Attributes;
            visibility = methodAttributes & MethodAttributes.MemberAccessMask;
            isStatic = (0 != (methodAttributes & MethodAttributes.Static));
            isVirtual = (0 != (methodAttributes & MethodAttributes.Virtual));
            isNewSlot = (0 != (methodAttributes & MethodAttributes.NewSlot));
        }

        public sealed override bool AreNamesAndSignatureEqual(PropertyInfo member1, PropertyInfo member2)
        {
            return AreNamesAndSignaturesEqual(GetAccessorMethod(member1), GetAccessorMethod(member2));
        }

        //
        // Desktop compat: Properties hide properties in base types if they share the same vtable slot, or 
        // have the same name, return type, signature and hasThis value.
        //
        public sealed override bool IsSuppressedByMoreDerivedMember(PropertyInfo member, PropertyInfo[] priorMembers, int startIndex, int endIndex)
        {
            for (int i = startIndex; i < endIndex; i++)
            {
                PropertyInfo prior = priorMembers[i];
                if (!AreNamesAndSignatureEqual(prior, member))
                    continue;
                if (GetAccessorMethod(prior).IsStatic != GetAccessorMethod(member).IsStatic)
                    continue;
                if (!(prior.PropertyType.Equals(member.PropertyType)))
                    continue;

                return true;
            }
            return false;
        }

        private MethodInfo GetAccessorMethod(PropertyInfo property)
        {
            MethodInfo accessor = property.GetMethod;
            if (accessor == null)
            {
                accessor = property.SetMethod;
            }

            return accessor;
        }
    }

    //==========================================================================================================================
    // Policies for events.
    //==========================================================================================================================
    internal sealed class EventPolicies : MemberPolicies<EventInfo>
    {
        public sealed override IEnumerable<EventInfo> GetDeclaredMembers(TypeInfo typeInfo)
        {
            return typeInfo.DeclaredEvents;
        }

        public sealed override IEnumerable<EventInfo> CoreGetDeclaredMembers(RuntimeTypeInfo type, NameFilter optionalNameFilter, RuntimeTypeInfo reflectedType)
        {
            return type.CoreGetDeclaredEvents(optionalNameFilter, reflectedType);
        }

        public sealed override bool AlwaysTreatAsDeclaredOnly => false;

        public sealed override void GetMemberAttributes(EventInfo member, out MethodAttributes visibility, out bool isStatic, out bool isVirtual, out bool isNewSlot)
        {
            MethodInfo accessorMethod = GetAccessorMethod(member);
            MethodAttributes methodAttributes = accessorMethod.Attributes;
            visibility = methodAttributes & MethodAttributes.MemberAccessMask;
            isStatic = (0 != (methodAttributes & MethodAttributes.Static));
            isVirtual = (0 != (methodAttributes & MethodAttributes.Virtual));
            isNewSlot = (0 != (methodAttributes & MethodAttributes.NewSlot));
        }

        //
        // Desktop compat: Events hide events in base types if they have the same name.
        //
        public sealed override bool IsSuppressedByMoreDerivedMember(EventInfo member, EventInfo[] priorMembers, int startIndex, int endIndex)
        {
            for (int i = startIndex; i < endIndex; i++)
            {
                if (priorMembers[i].Name == member.Name)
                    return true;
            }
            return false;
        }

        public sealed override bool AreNamesAndSignatureEqual(EventInfo member1, EventInfo member2)
        {
            return AreNamesAndSignaturesEqual(GetAccessorMethod(member1), GetAccessorMethod(member2));
        }

        private MethodInfo GetAccessorMethod(EventInfo e)
        {
            MethodInfo accessor = e.AddMethod;
            return accessor;
        }
    }

    //==========================================================================================================================
    // Policies for nested types.
    //
    // Nested types enumerate a little differently than other members:
    //
    //    Base classes are never searched, regardless of BindingFlags.DeclaredOnly value.
    //
    //    Public|NonPublic|IgnoreCase are the only relevant BindingFlags. The apis ignore any other bits.
    //
    //    There is no such thing as a "static" or "instanced" nested type. For enumeration purposes,
    //    we'll arbitrarily denote all nested types as "static."
    //
    //==========================================================================================================================
    internal sealed class NestedTypePolicies : MemberPolicies<Type>
    {
        public sealed override IEnumerable<Type> GetDeclaredMembers(TypeInfo typeInfo)
        {
            return typeInfo.DeclaredNestedTypes;
        }

        public sealed override IEnumerable<Type> CoreGetDeclaredMembers(RuntimeTypeInfo type, NameFilter optionalNameFilter, RuntimeTypeInfo reflectedType)
        {
            Debug.Assert(reflectedType.Equals(type));  // NestedType queries are always performed as if BindingFlags.DeclaredOnly are set so the reflectedType should always be the declaring type.
            return type.CoreGetDeclaredNestedTypes(optionalNameFilter);
        }

        public sealed override bool AlwaysTreatAsDeclaredOnly => true;

        public sealed override void GetMemberAttributes(Type member, out MethodAttributes visibility, out bool isStatic, out bool isVirtual, out bool isNewSlot)
        {
            isStatic = true;
            isVirtual = false;
            isNewSlot = false;

            // Since we never search base types for nested types, we don't need to map every visibility value one to one.
            // We just need to distinguish between "public" and "everything else."
            visibility = member.IsNestedPublic ? MethodAttributes.Public : MethodAttributes.Private;
        }

        public sealed override bool AreNamesAndSignatureEqual(Type member1, Type member2)
        {
            Debug.Assert(false, "This code path should be unreachable as nested types are never \"virtual\".");
            throw new NotSupportedException();
        }

        public sealed override bool IsSuppressedByMoreDerivedMember(Type member, Type[] priorMembers, int startIndex, int endIndex)
        {
            return false;
        }

        public sealed override BindingFlags ModifyBindingFlags(BindingFlags bindingFlags)
        {
            bindingFlags &= BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;
            bindingFlags |= BindingFlags.Static | BindingFlags.DeclaredOnly;
            return bindingFlags;
        }
    }
}
