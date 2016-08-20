// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


/*============================================================
**
  Type:  TypeInfo
**
==============================================================*/

using global::System;
using global::System.Collections.Generic;

namespace System.Reflection
{
    public abstract class TypeInfo : Type, IReflectableType
    {
        protected TypeInfo()
        {
        }

        public abstract Assembly Assembly { get; }
        public abstract TypeAttributes Attributes { get; }
        public abstract Type BaseType { get; }
        public abstract bool ContainsGenericParameters { get; }

        public virtual IEnumerable<ConstructorInfo> DeclaredConstructors
        {
            get
            {
                return GetDeclaredMembersOfType<ConstructorInfo>();
            }
        }

        public virtual IEnumerable<EventInfo> DeclaredEvents
        {
            get
            {
                return GetDeclaredMembersOfType<EventInfo>();
            }
        }

        public virtual IEnumerable<FieldInfo> DeclaredFields
        {
            get
            {
                return GetDeclaredMembersOfType<FieldInfo>();
            }
        }

        public virtual IEnumerable<MemberInfo> DeclaredMembers
        {
            get
            {
                throw NotImplemented.ByDesign;
            }
        }

        public virtual IEnumerable<MethodInfo> DeclaredMethods
        {
            get
            {
                return GetDeclaredMembersOfType<MethodInfo>();
            }
        }

        public virtual IEnumerable<TypeInfo> DeclaredNestedTypes
        {
            get
            {
                return GetDeclaredMembersOfType<TypeInfo>();
            }
        }

        public virtual IEnumerable<PropertyInfo> DeclaredProperties
        {
            get
            {
                return GetDeclaredMembersOfType<PropertyInfo>();
            }
        }

        private IEnumerable<T> GetDeclaredMembersOfType<T>() where T : MemberInfo
        {
            IEnumerable<MemberInfo> members = this.DeclaredMembers;
            foreach (MemberInfo member in members)
            {
                T memberAsT = member as T;
                if (memberAsT != null)
                    yield return memberAsT;
            }
        }


        public abstract MethodBase DeclaringMethod { get; }
        public abstract GenericParameterAttributes GenericParameterAttributes { get; }

        public virtual Type[] GenericTypeParameters
        {
            get
            {
                if (IsGenericTypeDefinition)
                {
                    return GenericTypeArguments;
                }
                else
                {
                    return Array.Empty<Type>();
                }
            }
        }

        public abstract Guid GUID { get; }

        public virtual IEnumerable<Type> ImplementedInterfaces
        {
            get
            {
                throw NotImplemented.ByDesign;
            }
        }



        public bool IsAbstract
        {
            get
            {
                return 0 != (this.Attributes & TypeAttributes.Abstract);
            }
        }

        public bool IsAnsiClass
        {
            get
            {
                return TypeAttributes.AnsiClass == (this.Attributes & TypeAttributes.StringFormatMask);
            }
        }

        public bool IsAutoClass
        {
            get
            {
                return TypeAttributes.AutoClass == (this.Attributes & TypeAttributes.StringFormatMask);
            }
        }

        public bool IsAutoLayout
        {
            get
            {
                return TypeAttributes.AutoLayout == (this.Attributes & TypeAttributes.LayoutMask);
            }
        }

        public bool IsClass
        {
            get
            {
                return (TypeAttributes.Class == (this.Attributes & TypeAttributes.ClassSemanticsMask)) && !IsValueType;
            }
        }

        public abstract bool IsEnum { get; }

        public virtual bool IsCOMObject { get { return false; } }

        public bool IsExplicitLayout
        {
            get
            {
                return TypeAttributes.ExplicitLayout == (this.Attributes & TypeAttributes.LayoutMask);
            }
        }

        public abstract bool IsGenericType { get; }
        public abstract bool IsGenericTypeDefinition { get; }

        public bool IsImport
        {
            get
            {
                return 0 != (this.Attributes & TypeAttributes.Import);
            }
        }

        public bool IsInterface
        {
            get
            {
                return TypeAttributes.Interface == (this.Attributes & TypeAttributes.ClassSemanticsMask);
            }
        }

        public bool IsLayoutSequential
        {
            get
            {
                return TypeAttributes.SequentialLayout == (this.Attributes & TypeAttributes.LayoutMask);
            }
        }


        public bool IsMarshalByRef { get { return false; } }


        public bool IsNestedAssembly
        {
            get
            {
                return TypeAttributes.NestedAssembly == (this.Attributes & TypeAttributes.VisibilityMask);
            }
        }


        public bool IsNestedFamANDAssem
        {
            get
            {
                return TypeAttributes.NestedFamANDAssem == (this.Attributes & TypeAttributes.VisibilityMask);
            }
        }


        public bool IsNestedFamily
        {
            get
            {
                return TypeAttributes.NestedFamily == (this.Attributes & TypeAttributes.VisibilityMask);
            }
        }

        public bool IsNestedFamORAssem
        {
            get
            {
                return TypeAttributes.NestedFamORAssem == (this.Attributes & TypeAttributes.VisibilityMask);
            }
        }


        public bool IsNestedPrivate
        {
            get
            {
                return TypeAttributes.NestedPrivate == (this.Attributes & TypeAttributes.VisibilityMask);
            }
        }



        public bool IsNestedPublic
        {
            get
            {
                return TypeAttributes.NestedPublic == (this.Attributes & TypeAttributes.VisibilityMask);
            }
        }


        public bool IsNotPublic
        {
            get
            {
                return TypeAttributes.NotPublic == (this.Attributes & TypeAttributes.VisibilityMask);
            }
        }

        public virtual bool IsPrimitive
        {
            get
            {
                throw NotImplemented.ByDesign;
            }
        }

        public bool IsPublic
        {
            get
            {
                return TypeAttributes.Public == (this.Attributes & TypeAttributes.VisibilityMask);
            }
        }

        public bool IsSealed
        {
            get
            {
                return 0 != (this.Attributes & TypeAttributes.Sealed);
            }
        }

        public abstract bool IsSerializable { get; }

        public bool IsSpecialName
        {
            get
            {
                return 0 != (this.Attributes & TypeAttributes.SpecialName);
            }
        }

        public bool IsUnicodeClass
        {
            get
            {
                return TypeAttributes.UnicodeClass == (this.Attributes & TypeAttributes.StringFormatMask);
            }
        }

        public virtual bool IsValueType
        {
            get
            {
                // Port note: The desktop version inherits an IsValueType from System.Type which calls IsSubclassOf(ValueType) - an implementation
                // which is acknowledged to be incorrect for non-runtime types.
                //
                // However, since this is first time TypeInfo exposes a non-internal .ctor, no one (other than us) could have been subclassing TypeInfo.
                // So we can safely take this opportunity to do the right thing and throw NotImplemented now.
                throw NotImplemented.ByDesign;
            }
        }

        public bool IsVisible
        {
            get
            {
                if (IsGenericParameter)
                    return true;

                if (HasElementType)
                    return GetElementType().GetTypeInfo().IsVisible;

                TypeInfo typeInfo = this;
                while (typeInfo.IsNested)
                {
                    if (!typeInfo.IsNestedPublic)
                        return false;

                    // this should be null for non-nested types.
                    typeInfo = typeInfo.DeclaringType.GetTypeInfo();
                }

                // Now "typeInfo" should be a top level type
                if (!typeInfo.IsPublic)
                    return false;

                Type thisType = this.AsType();
                if (thisType.IsConstructedGenericType)
                {
                    foreach (Type t in thisType.GenericTypeArguments)
                    {
                        if (!t.GetTypeInfo().IsVisible)
                            return false;
                    }
                }

                return true;
            }
        }

        public virtual Type AsType()
        {
            throw NotImplemented.ByDesign;
        }

        public virtual EventInfo GetDeclaredEvent(String name)
        {
            return GetDeclaredMember<EventInfo>(name, DeclaredEvents);
        }

        public virtual FieldInfo GetDeclaredField(String name)
        {
            return GetDeclaredMember<FieldInfo>(name, DeclaredFields);
        }

        public virtual MethodInfo GetDeclaredMethod(String name)
        {
            return GetDeclaredMember<MethodInfo>(name, DeclaredMethods);
        }

        public virtual IEnumerable<MethodInfo> GetDeclaredMethods(String name)
        {
            return GetDeclaredMembers<MethodInfo>(name, DeclaredMethods);
        }

        public virtual TypeInfo GetDeclaredNestedType(String name)
        {
            return GetDeclaredMember<TypeInfo>(name, DeclaredNestedTypes);
        }

        public virtual PropertyInfo GetDeclaredProperty(String name)
        {
            return GetDeclaredMember<PropertyInfo>(name, DeclaredProperties);
        }

        private IEnumerable<T> GetDeclaredMembers<T>(String name, IEnumerable<T> members) where T : MemberInfo
        {
            foreach (T member in members)
            {
                if (member.Name == name)
                    yield return member;
            }
        }

        private T GetDeclaredMember<T>(String name, IEnumerable<T> members) where T : MemberInfo
        {
            if (name == null)
                throw new ArgumentNullException("name");
            IEnumerable<T> matchingMembers = GetDeclaredMembers<T>(name, members);
            IEnumerator<T> e = matchingMembers.GetEnumerator();
            if (!e.MoveNext())
                return null;
            T result = e.Current;
            if (e.MoveNext())
                throw new AmbiguousMatchException();
            return result;
        }


        public abstract Type[] GetGenericParameterConstraints();

        public virtual bool IsAssignableFrom(TypeInfo typeInfo)
        {
            if (typeInfo == null)
                return false;

            if (this.Equals(typeInfo))
                return true;

            // If c is a subclass of this class, then typeInfo can be cast to this type.
            if (typeInfo.IsSubclassOf(this.AsType()))
                return true;

            if (this.IsInterface)
            {
                foreach (Type implementedInterface in typeInfo.ImplementedInterfaces)
                {
                    TypeInfo resolvedImplementedInterface = implementedInterface.GetTypeInfo();
                    if (resolvedImplementedInterface.Equals(this))
                        return true;
                }
                return false;
            }
            else if (IsGenericParameter)
            {
                Type[] constraints = GetGenericParameterConstraints();
                for (int i = 0; i < constraints.Length; i++)
                    if (!constraints[i].GetTypeInfo().IsAssignableFrom(typeInfo))
                        return false;

                return true;
            }

            return false;
        }

        public virtual bool IsSubclassOf(Type c)
        {
            TypeInfo resolvedC = c.GetTypeInfo();
            TypeInfo p = this;
            if (p.Equals(resolvedC))
                return false;
            while (p != null)
            {
                if (p.Equals(resolvedC))
                    return true;
                Type b = p.BaseType;
                if (b == null)
                    break;
                p = b.GetTypeInfo();
            }
            return false;
        }

        // TODO https://github.com/dotnet/corefx/issues/9805: This is inherited from Type and shouldn't need to be redeclared on TypeInfo but 
        //   TypeInfo.MakeGenericType is a well known method to the reducer.
        public abstract override Type MakeGenericType(params Type[] typeArguments);

        TypeInfo System.Reflection.IReflectableType.GetTypeInfo()
        {
            return this;
        }
    }
}

