// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// This file collects all of the Reflection apis that we're adding back for .NETCore 2.0, but haven't implemented yet.
// As we implement them, the apis should be moved out of this file and into the main source file for its containing class.
// Once we've implemented them all, this source file can be deleted.
//

using System;
using System.IO;
using System.Reflection;
using System.Globalization;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;

namespace System.Reflection.Runtime.Assemblies
{
    internal sealed partial class RuntimeAssembly
    {
        public sealed override string CodeBase { get { throw new NotImplementedException(); } }
        public sealed override object CreateInstance(string typeName, bool ignoreCase, BindingFlags bindingAttr, Binder binder, object[] args, CultureInfo culture, object[] activationAttributes) { throw new NotImplementedException(); }
        public sealed override MethodInfo EntryPoint { get { throw new NotImplementedException(); } }
        public sealed override Module GetModule(string name) { throw new NotImplementedException(); }
        public sealed override void GetObjectData(SerializationInfo info, StreamingContext context) { throw new NotImplementedException(); }
        public sealed override AssemblyName[] GetReferencedAssemblies() { throw new NotImplementedException(); }
        public sealed override Assembly GetSatelliteAssembly(CultureInfo culture) { throw new NotImplementedException(); }
        public sealed override Assembly GetSatelliteAssembly(CultureInfo culture, Version version) { throw new NotImplementedException(); }
        public sealed override string ImageRuntimeVersion { get { throw new NotImplementedException(); } }
        public sealed override string Location { get { throw new NotImplementedException(); } }
    }
}

namespace System.Reflection.Runtime.MethodInfos
{
    internal abstract partial class RuntimeConstructorInfo
    {
        public sealed override MethodBody GetMethodBody() { throw new NotImplementedException(); }
        public sealed override RuntimeMethodHandle MethodHandle { get { throw new NotImplementedException(); } }
        public sealed override Type ReflectedType { get { throw new NotImplementedException(); } }
    }
}

namespace System.Reflection.Runtime.CustomAttributes
{
    internal abstract partial class RuntimeCustomAttributeData
    {
    }
}

namespace System.Reflection.Runtime.EventInfos
{
    internal sealed partial class RuntimeEventInfo
    {
        public sealed override MethodInfo[] GetOtherMethods(bool nonPublic) { throw new NotImplementedException(); }
        public sealed override Type ReflectedType { get { throw new NotImplementedException(); } }
    }
}

namespace System.Reflection.Runtime.FieldInfos
{
    internal sealed partial class RuntimeFieldInfo
    {
        public sealed override RuntimeFieldHandle FieldHandle { get { throw new NotImplementedException(); } }
        public sealed override Type[] GetOptionalCustomModifiers() { throw new NotImplementedException(); }
        public sealed override Type[] GetRequiredCustomModifiers() { throw new NotImplementedException(); }
        public sealed override Type ReflectedType { get { throw new NotImplementedException(); } }
    }
}

namespace System.Reflection.Runtime.MethodInfos
{
    internal abstract partial class RuntimeMethodInfo
    {
        public sealed override MethodBody GetMethodBody() { throw new NotImplementedException(); }
        public sealed override RuntimeMethodHandle MethodHandle { get { throw new NotImplementedException(); } }
        public sealed override Type ReflectedType { get { throw new NotImplementedException(); } }
    }
}

namespace System.Reflection.Runtime.Modules
{
    internal sealed partial class RuntimeModule
    {
        public sealed override FieldInfo GetField(string name, BindingFlags bindingAttr) { throw new NotImplementedException(); }
        public sealed override FieldInfo[] GetFields(BindingFlags bindingFlags) { throw new NotImplementedException(); }
        protected sealed override MethodInfo GetMethodImpl(string name, BindingFlags bindingAttr, Binder binder, CallingConventions callConvention, Type[] types, ParameterModifier[] modifiers) { throw new NotImplementedException(); }
        public sealed override MethodInfo[] GetMethods(BindingFlags bindingFlags) { throw new NotImplementedException(); }
        public sealed override void GetObjectData(SerializationInfo info, StreamingContext context) { throw new NotImplementedException(); }
        public sealed override void GetPEKind(out PortableExecutableKinds peKind, out ImageFileMachine machine) { throw new NotImplementedException(); }
        public sealed override bool IsResource() { throw new NotImplementedException(); }
        public sealed override int MDStreamVersion { get { throw new NotImplementedException(); } }
        public sealed override FieldInfo ResolveField(int metadataToken, Type[] genericTypeArguments, Type[] genericMethodArguments) { throw new NotImplementedException(); }
        public sealed override MemberInfo ResolveMember(int metadataToken, Type[] genericTypeArguments, Type[] genericMethodArguments) { throw new NotImplementedException(); }
        public sealed override MethodBase ResolveMethod(int metadataToken, Type[] genericTypeArguments, Type[] genericMethodArguments) { throw new NotImplementedException(); }
        public sealed override byte[] ResolveSignature(int metadataToken) { throw new NotImplementedException(); }
        public sealed override string ResolveString(int metadataToken) { throw new NotImplementedException(); }
        public sealed override Type ResolveType(int metadataToken, Type[] genericTypeArguments, Type[] genericMethodArguments) { throw new NotImplementedException(); }
        public sealed override string ScopeName { get { throw new NotImplementedException(); } }
    }
}

namespace System.Reflection.Runtime.ParameterInfos
{
    internal abstract partial class RuntimeParameterInfo
    {
        public sealed override Type[] GetOptionalCustomModifiers() { throw new NotImplementedException(); }
        public sealed override Type[] GetRequiredCustomModifiers() { throw new NotImplementedException(); }
    }
}

namespace System.Reflection.Runtime.PropertyInfos
{
    internal sealed partial class RuntimePropertyInfo
    {
        public sealed override Type ReflectedType { get { throw new NotImplementedException(); } }
        public sealed override Type[] GetOptionalCustomModifiers() { throw new NotImplementedException(); }
        public sealed override Type[] GetRequiredCustomModifiers() { throw new NotImplementedException(); }
    }
}

namespace System.Reflection.Runtime.TypeInfos
{
    internal abstract partial class RuntimeTypeInfo
    {
        public sealed override MemberInfo[] GetMember(string name, MemberTypes type, BindingFlags bindingAttr) { throw new NotImplementedException(); }
        public sealed override object InvokeMember(string name, BindingFlags invokeAttr, Binder binder, object target, object[] args, ParameterModifier[] modifiers, CultureInfo culture, string[] namedParameters) { throw new NotImplementedException(); }
        public sealed override Type ReflectedType { get { throw new NotImplementedException(); } }
    }
}
