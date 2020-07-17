// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Reflection.Emit
{
    public sealed partial class GenericTypeParameterBuilder : TypeInfo
    {
        internal GenericTypeParameterBuilder()
        {
            // Prevent generating a default constructor
        }

        public override Assembly Assembly
        {
            get
            {
                return default;
            }
        }

        public override string AssemblyQualifiedName
        {
            get
            {
                return default;
            }
        }

        public override Type BaseType
        {
            get
            {
                return default;
            }
        }

        public override bool ContainsGenericParameters
        {
            get
            {
                return default;
            }
        }

        public override MethodBase DeclaringMethod
        {
            get
            {
                return default;
            }
        }

        public override Type DeclaringType
        {
            get
            {
                return default;
            }
        }

        public override string FullName
        {
            get
            {
                return default;
            }
        }

        public override GenericParameterAttributes GenericParameterAttributes
        {
            get
            {
                return default;
            }
        }

        public override int GenericParameterPosition
        {
            get
            {
                return default;
            }
        }

        public override Guid GUID
        {
            get
            {
                return default;
            }
        }

        public override bool IsConstructedGenericType
        {
            get
            {
                return default;
            }
        }

        public override bool IsGenericParameter
        {
            get
            {
                return default;
            }
        }

        public override bool IsGenericType
        {
            get
            {
                return default;
            }
        }

        public override bool IsGenericTypeDefinition
        {
            get
            {
                return default;
            }
        }

        public override Module Module
        {
            get
            {
                return default;
            }
        }

        public override string Name
        {
            get
            {
                return default;
            }
        }

        public override string Namespace
        {
            get
            {
                return default;
            }
        }

        public override Type ReflectedType
        {
            get
            {
                return default;
            }
        }

        public override RuntimeTypeHandle TypeHandle
        {
            get
            {
                return default;
            }
        }

        public override Type UnderlyingSystemType
        {
            get
            {
                return default;
            }
        }

        public override bool Equals(object o)
        {
            return default;
        }

        protected override TypeAttributes GetAttributeFlagsImpl()
        {
            return default;
        }

        protected override ConstructorInfo GetConstructorImpl(BindingFlags bindingAttr, Binder binder, CallingConventions callConvention, Type[] types, ParameterModifier[] modifiers)
        {
            return default;
        }

        public override ConstructorInfo[] GetConstructors(BindingFlags bindingAttr)
        {
            return default;
        }

        public override object[] GetCustomAttributes(bool inherit)
        {
            return default;
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return default;
        }

        public override Type GetElementType()
        {
            return default;
        }

        public override EventInfo GetEvent(string name, BindingFlags bindingAttr)
        {
            return default;
        }

        public override EventInfo[] GetEvents()
        {
            return default;
        }

        public override EventInfo[] GetEvents(BindingFlags bindingAttr)
        {
            return default;
        }

        public override FieldInfo GetField(string name, BindingFlags bindingAttr)
        {
            return default;
        }

        public override FieldInfo[] GetFields(BindingFlags bindingAttr)
        {
            return default;
        }

        public override Type[] GetGenericArguments()
        {
            return default;
        }

        public override Type GetGenericTypeDefinition()
        {
            return default;
        }

        public override int GetHashCode()
        {
            return default;
        }

        public override Type GetInterface(string name, bool ignoreCase)
        {
            return default;
        }

        public override InterfaceMapping GetInterfaceMap(Type interfaceType)
        {
            return default;
        }

        public override Type[] GetInterfaces()
        {
            return default;
        }

        public override MemberInfo[] GetMember(string name, MemberTypes type, BindingFlags bindingAttr)
        {
            return default;
        }

        public override MemberInfo[] GetMembers(BindingFlags bindingAttr)
        {
            return default;
        }

        protected override MethodInfo GetMethodImpl(string name, BindingFlags bindingAttr, Binder binder, CallingConventions callConvention, Type[] types, ParameterModifier[] modifiers)
        {
            return default;
        }

        public override MethodInfo[] GetMethods(BindingFlags bindingAttr)
        {
            return default;
        }

        public override Type GetNestedType(string name, BindingFlags bindingAttr)
        {
            return default;
        }

        public override Type[] GetNestedTypes(BindingFlags bindingAttr)
        {
            return default;
        }

        public override PropertyInfo[] GetProperties(BindingFlags bindingAttr)
        {
            return default;
        }

        protected override PropertyInfo GetPropertyImpl(string name, BindingFlags bindingAttr, Binder binder, Type returnType, Type[] types, ParameterModifier[] modifiers)
        {
            return default;
        }

        protected override bool HasElementTypeImpl()
        {
            return default;
        }

        public override object InvokeMember(string name, BindingFlags invokeAttr, Binder binder, object target, object[] args, ParameterModifier[] modifiers, Globalization.CultureInfo culture, string[] namedParameters)
        {
            return default;
        }

        protected override bool IsArrayImpl()
        {
            return default;
        }

        public override bool IsAssignableFrom(TypeInfo typeInfo)
        {
            return default;
        }

        public override bool IsAssignableFrom(Type c)
        {
            return default;
        }

        protected override bool IsByRefImpl()
        {
            return default;
        }

        public override bool IsByRefLike
        {
            get
            {
                return default;
            }
        }

        protected override bool IsCOMObjectImpl()
        {
            return default;
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            return default;
        }

        protected override bool IsPointerImpl()
        {
            return default;
        }

        protected override bool IsPrimitiveImpl()
        {
            return default;
        }

        public override bool IsSubclassOf(Type c)
        {
            return default;
        }

        public override bool IsTypeDefinition
        {
            get
            {
                return default;
            }
        }

        public override bool IsSZArray
        {
            get
            {
                return default;
            }
        }

        public override bool IsVariableBoundArray
        {
            get
            {
                return default;
            }
        }

        protected override bool IsValueTypeImpl()
        {
            return default;
        }

        public override Type MakeArrayType()
        {
            return default;
        }

        public override Type MakeArrayType(int rank)
        {
            return default;
        }

        public override Type MakeByRefType()
        {
            return default;
        }

        public override Type MakeGenericType(params Type[] typeArguments)
        {
            return default;
        }

        public override Type MakePointerType()
        {
            return default;
        }

        public void SetBaseTypeConstraint(Type baseTypeConstraint)
        {
        }

        public void SetCustomAttribute(ConstructorInfo con, byte[] binaryAttribute)
        {
        }

        public void SetCustomAttribute(CustomAttributeBuilder customBuilder)
        {
        }

        public void SetGenericParameterAttributes(GenericParameterAttributes genericParameterAttributes)
        {
        }

        public void SetInterfaceConstraints(params Type[] interfaceConstraints)
        {
        }

        public override string ToString()
        {
            return default;
        }
    }
}
