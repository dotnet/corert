// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//
// Why this file exists:
//
// Because the Reflection base types have so many overridable members, it becomes difficult to distinguish
// members we decided not to override vs. those we forgot to override. It would be nice if C# had a construct to 
// tell the reader (and Intellisense) that we've made an explicit decision *not* to override an inherited member, 
// but since it doesn't, we'll make do with this instead.
//
// In DEBUG builds, we'll add a base-delegating override so that it's clear we made an explicit decision
// to accept the base class's implementation. In RELEASE builds, we'll #if'd these out to avoid the extra metadata and runtime
// cost. That way, every overridable member is accounted for (i.e. the codebase should always be kept in a state
// where hitting "override" + SPACE never brings up additional suggestions in Intellisense.)
//
// To avoid introducing inadvertent inconsistencies between DEBUG and RELEASE behavior due to the fragile base class 
// problem, only do this for public or protected members that already exist on the public api type. Since we know 
// we'll never remove those members, we'll avoid the problem of "base" being compile-bound to something different
// from the runtime "base."
//

using System;
using System.IO;
using System.Reflection;
using System.Globalization;
using System.Collections.Generic;

namespace System.Reflection.Runtime.Assemblies
{
    internal partial class RuntimeAssembly
    {
#if DEBUG
        public sealed override object CreateInstance(string typeName, bool ignoreCase, BindingFlags bindingAttr, Binder binder, object[] args, CultureInfo culture, object[] activationAttributes) => base.CreateInstance(typeName, ignoreCase, bindingAttr, binder, args, culture, activationAttributes);
        public sealed override Type GetType(string name) => base.GetType(name);
        public sealed override Type GetType(string name, bool throwOnError) => base.GetType(name, throwOnError);
        public sealed override bool IsDynamic => base.IsDynamic;
        public sealed override string ToString() => base.ToString();
        public sealed override string EscapedCodeBase => base.EscapedCodeBase;
        public sealed override FileStream[] GetFiles() => base.GetFiles();
#endif //DEBUG
    }
}

namespace System.Reflection.Runtime.MethodInfos
{
    internal abstract partial class RuntimeConstructorInfo
    {
#if DEBUG
        public sealed override MemberTypes MemberType => base.MemberType;
#endif //DEBUG
    }
}

namespace System.Reflection.Runtime.CustomAttributes
{
    internal abstract partial class RuntimeCustomAttributeData
    {
#if DEBUG
        public sealed override bool Equals(object obj) => base.Equals(obj);
        public sealed override int GetHashCode() => base.GetHashCode();
#endif //DEBUG
    }
}

namespace System.Reflection.Runtime.EventInfos
{
    internal abstract partial class RuntimeEventInfo
    {
#if DEBUG
        public sealed override MemberTypes MemberType => base.MemberType;
        public sealed override bool IsMulticast => base.IsMulticast;
        public sealed override void AddEventHandler(object target, Delegate handler) => base.AddEventHandler(target, handler);
        public sealed override void RemoveEventHandler(object target, Delegate handler) => base.RemoveEventHandler(target, handler);
#endif //DEBUG
    }
}

namespace System.Reflection.Runtime.FieldInfos
{
    internal abstract partial class RuntimeFieldInfo
    {
#if DEBUG
        public sealed override MemberTypes MemberType => base.MemberType;
        public sealed override bool IsSecurityCritical => base.IsSecurityCritical;
        public sealed override bool IsSecuritySafeCritical => base.IsSecuritySafeCritical;
        public sealed override bool IsSecurityTransparent => base.IsSecurityTransparent;
#endif //DEBUG
    }
}

namespace System.Reflection.Runtime.MethodInfos
{
    internal abstract partial class RuntimeMethodInfo
    {
#if DEBUG
        public sealed override MemberTypes MemberType => base.MemberType;
#endif //DEBUG
    }
}

namespace System.Reflection.Runtime.Modules
{
    internal abstract partial class RuntimeModule
    {
#if DEBUG
        public sealed override Type[] FindTypes(TypeFilter filter, object filterCriteria) => base.FindTypes(filter, filterCriteria);
        public sealed override Type GetType(string className) => base.GetType(className);
        public sealed override Type GetType(string className, bool ignoreCase) => base.GetType(className, ignoreCase);
#endif //DEBUG
    }
}

namespace System.Reflection.Runtime.ParameterInfos
{
    internal abstract partial class RuntimeParameterInfo
    {
#if DEBUG
#endif //DEBUG
    }
}

namespace System.Reflection.Runtime.PropertyInfos
{
    internal abstract partial class RuntimePropertyInfo
    {
#if DEBUG
        public sealed override MemberTypes MemberType => base.MemberType;
        public sealed override object GetValue(object obj, object[] index) => base.GetValue(obj, index);
        public sealed override void SetValue(object obj, object value, object[] index) => base.SetValue(obj, value, index);
#endif //DEBUG
    }
}

namespace System.Reflection.Runtime.TypeInfos
{
    internal abstract partial class RuntimeTypeInfo
    {
#if DEBUG
        public sealed override Type[] FindInterfaces(TypeFilter filter, object filterCriteria) => base.FindInterfaces(filter, filterCriteria);
        public sealed override MemberInfo[] FindMembers(MemberTypes memberType, BindingFlags bindingAttr, MemberFilter filter, object filterCriteria) => base.FindMembers(memberType, bindingAttr, filter, filterCriteria);
        public sealed override EventInfo[] GetEvents() => base.GetEvents();
        protected sealed override bool IsContextfulImpl() => base.IsContextfulImpl();
        public sealed override bool IsSubclassOf(Type c) => base.IsSubclassOf(c);
        protected sealed override bool IsMarshalByRefImpl() => base.IsMarshalByRefImpl();
        public sealed override bool IsInstanceOfType(object o) => base.IsInstanceOfType(o);
        public sealed override bool IsSerializable => base.IsSerializable;
        public sealed override bool IsEquivalentTo(Type other) => base.IsEquivalentTo(other); // Note: If we enable COM type equivalence, this is no longer the correct implementation.
        public sealed override bool IsSignatureType => base.IsSignatureType;

        public sealed override IEnumerable<ConstructorInfo> DeclaredConstructors => base.DeclaredConstructors;
        public sealed override IEnumerable<EventInfo> DeclaredEvents => base.DeclaredEvents;
        public sealed override IEnumerable<FieldInfo> DeclaredFields => base.DeclaredFields;
        public sealed override IEnumerable<MemberInfo> DeclaredMembers => base.DeclaredMembers;
        public sealed override IEnumerable<MethodInfo> DeclaredMethods => base.DeclaredMethods;
        public sealed override IEnumerable<TypeInfo> DeclaredNestedTypes => base.DeclaredNestedTypes;
        public sealed override IEnumerable<PropertyInfo> DeclaredProperties => base.DeclaredProperties;

        public sealed override EventInfo GetDeclaredEvent(string name) => base.GetDeclaredEvent(name);
        public sealed override FieldInfo GetDeclaredField(string name) => base.GetDeclaredField(name);
        public sealed override MethodInfo GetDeclaredMethod(string name) => base.GetDeclaredMethod(name);
        public sealed override TypeInfo GetDeclaredNestedType(string name) => base.GetDeclaredNestedType(name);
        public sealed override PropertyInfo GetDeclaredProperty(string name) => base.GetDeclaredProperty(name);

        public sealed override IEnumerable<MethodInfo> GetDeclaredMethods(string name) => base.GetDeclaredMethods(name);
#endif //DEBUG
    }
}


