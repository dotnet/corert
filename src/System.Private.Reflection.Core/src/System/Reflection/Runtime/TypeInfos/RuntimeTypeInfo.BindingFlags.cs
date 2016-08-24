// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.Reflection.Runtime.BindingFlagSupport;

namespace System.Reflection.Runtime.TypeInfos
{
    internal abstract partial class RuntimeTypeInfo
    {
        public sealed override ConstructorInfo[] GetConstructors(BindingFlags bindingAttr) =>  LowLevelTypeExtensions.GetConstructors(this, bindingAttr);

        public sealed override EventInfo[] GetEvents(BindingFlags bindingAttr) => LowLevelTypeExtensions.GetEvents(this, bindingAttr);
        public sealed override EventInfo GetEvent(string name, BindingFlags bindingAttr) => LowLevelTypeExtensions.GetEvent(this, name, bindingAttr);

        public sealed override FieldInfo[] GetFields(BindingFlags bindingAttr) => LowLevelTypeExtensions.GetFields(this, bindingAttr);
        public sealed override FieldInfo GetField(string name, BindingFlags bindingAttr) => LowLevelTypeExtensions.GetField(this, name, bindingAttr);

        public sealed override MemberInfo[] GetMembers(BindingFlags bindingAttr) => LowLevelTypeExtensions.GetMembers(this, bindingAttr);
        public sealed override MemberInfo[] GetMember(string name, BindingFlags bindingAttr) => LowLevelTypeExtensions.GetMember(this, name, bindingAttr);

        public sealed override MethodInfo[] GetMethods(BindingFlags bindingAttr) => LowLevelTypeExtensions.GetMethods(this, bindingAttr);
        protected sealed override MethodInfo GetMethodImpl(string name, BindingFlags bindingAttr, Binder binder, CallingConventions callConvention, Type[] types, ParameterModifier[] modifiers)
        {
            if (binder == null && callConvention == CallingConventions.Any && types == null && modifiers == null)
                return LowLevelTypeExtensions.GetMethod(this, name, bindingAttr);
            throw new NotImplementedException();
        }

        public sealed override Type[] GetNestedTypes(BindingFlags bindingAttr) => LowLevelTypeExtensions.GetNestedTypes(this, bindingAttr);
        public sealed override Type GetNestedType(string name, BindingFlags bindingAttr) => LowLevelTypeExtensions.GetNestedType(this, name, bindingAttr);

        public sealed override PropertyInfo[] GetProperties(BindingFlags bindingAttr) => LowLevelTypeExtensions.GetProperties(this, bindingAttr);
        protected sealed override PropertyInfo GetPropertyImpl(string name, BindingFlags bindingAttr, Binder binder, Type returnType, Type[] types, ParameterModifier[] modifiers)
        {
            if (binder == null && returnType == null && types == null && modifiers == null)
                return LowLevelTypeExtensions.GetProperty(this, name, bindingAttr);
            throw new NotImplementedException();
        }
    }
}

