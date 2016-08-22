// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Globalization;
using System.Collections.Generic;

namespace Internal.Reflection.Extensibility
{
    public abstract class ExtensibleAssembly : Assembly
    {
        protected ExtensibleAssembly()
        {
        }
    }

    public abstract class ExtensibleConstructorInfo : ConstructorInfo
    {
        protected ExtensibleConstructorInfo()
        {
        }
    }

    public abstract class ExtensibleCustomAttributeData : CustomAttributeData
    {
        protected ExtensibleCustomAttributeData()
        {
        }

        public static CustomAttributeNamedArgument CreateCustomAttributeNamedArgument(System.Type attributeType, string memberName, bool isField, CustomAttributeTypedArgument typedValue)
        {
            return new CustomAttributeNamedArgument(attributeType, memberName, isField, typedValue);
        }

        public static CustomAttributeTypedArgument CreateCustomAttributeTypedArgument(System.Type argumentType, object value)
        {
            return new CustomAttributeTypedArgument(argumentType, value);
        }
    }

    public abstract class ExtensibleEventInfo : EventInfo
    {
        protected ExtensibleEventInfo()
        {
        }
    }

    public abstract class ExtensibleFieldInfo : FieldInfo
    {
        protected ExtensibleFieldInfo()
        {
        }
    }

    public abstract class ExtensibleMethodInfo : MethodInfo
    {
        protected ExtensibleMethodInfo()
        {
        }
    }

    public abstract class ExtensibleModule : Module
    {
        protected ExtensibleModule()
        {
        }
    }

    public abstract class ExtensibleParameterInfo : ParameterInfo
    {
        protected ExtensibleParameterInfo()
        {
        }
    }

    public abstract class ExtensiblePropertyInfo : PropertyInfo
    {
        protected ExtensiblePropertyInfo()
        {
        }

        //
        // InheritedPropertyInfo in S.R.TypeExtensions (which lives over in corefx land) still derives from this. Until we can wean that
        // off ExtensiblePropertyInfo, these methods project a 1.0 style surface area to InheritedPropertyInfo.
        //

        public sealed override object GetValue(object obj, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture)
        {
            if (invokeAttr != BindingFlags.Default || binder != null || culture != null)
                throw new NotImplementedException();
            return GetValue(obj, index);
        }
        public new virtual object GetValue(object obj, object[] index) { throw NotImplemented.ByDesign; }

        public sealed override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture)
        {
            if (invokeAttr != BindingFlags.Default || binder != null || culture != null)
                throw new NotImplementedException();
            SetValue(obj, value, index);
        }
        public new virtual void SetValue(object obj, object value, object[] index) { throw NotImplemented.ByDesign; }

        public override object[] GetCustomAttributes(bool inherit) { throw NotImplemented.ByDesign; }
        public override object[] GetCustomAttributes(Type attributeType, bool inherit) { throw NotImplemented.ByDesign; }
        public override bool IsDefined(Type attributeType, bool inherit) { throw NotImplemented.ByDesign; }
        public override Type ReflectedType { get { throw NotImplemented.ByDesign; } }

        public override MethodInfo[] GetAccessors(bool nonPublic) { throw NotImplemented.ByDesign; }
        public override MethodInfo GetGetMethod(bool nonPublic) { throw NotImplemented.ByDesign; }
        public override MethodInfo GetSetMethod(bool nonPublic) { throw NotImplemented.ByDesign; }
    }

    public abstract class ExtensibleTypeInfo : TypeInfo
    {
        protected ExtensibleTypeInfo()
        {
        }

        // TypeInfo/Type will undergo a lot of shakeup so we'll use this to project a 1.0-compatible viewpoint
        // on downward types so we can manage the switchover more easily.

        public override object[] GetCustomAttributes(bool inherit) { throw NotImplemented.ByDesign; }
        public override object[] GetCustomAttributes(Type attributeType, bool inherit) { throw NotImplemented.ByDesign; }
        public override bool IsDefined(Type attributeType, bool inherit) { throw NotImplemented.ByDesign; }
        public override Type ReflectedType { get { throw NotImplemented.ByDesign; } }

        protected sealed override bool IsCOMObjectImpl() => IsCOMObject;
        public new virtual bool IsCOMObject { get { throw NotImplemented.ByDesign; } }

        protected sealed override bool IsPrimitiveImpl() => IsPrimitive;
        public new virtual bool IsPrimitive { get { throw NotImplemented.ByDesign; } }

        protected sealed override bool IsValueTypeImpl() => IsValueType;
        public new virtual bool IsValueType { get { throw NotImplemented.ByDesign; } }

        // There is no IsEnumImpl()
        public new virtual bool IsEnum { get { throw NotImplemented.ByDesign; } }

        protected sealed override TypeAttributes GetAttributeFlagsImpl() => Attributes;
        public new virtual TypeAttributes Attributes { get { throw NotImplemented.ByDesign; } }

        protected sealed override bool HasElementTypeImpl() => IsArray || IsByRef || IsPointer;

        public sealed override Type UnderlyingSystemType => this;
    }
}