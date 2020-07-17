// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.Serialization;
using System.Reflection.Runtime.Assemblies;
using System.Collections.Generic;

namespace System.Reflection.Runtime.Modules
{
    //
    // The runtime's implementation of a Module.
    //
    // Modules are quite meaningless in ProjectN but we have to keep up the appearances since they still exist in Win8P's surface area.
    // As far as ProjectN is concerned, each Assembly has one module.
    //
    internal abstract partial class RuntimeModule : Module
    {
        protected RuntimeModule()
            : base()
        { }

        public abstract override Assembly Assembly { get; }

        public abstract override IEnumerable<CustomAttributeData> CustomAttributes { get; }

        public sealed override string FullyQualifiedName
        {
            get
            {
                return "<Unknown>";
            }
        }

        public abstract override string Name { get; }

        public sealed override bool Equals(object obj)
        {
            if (!(obj is RuntimeModule other))
                return false;
            return Assembly.Equals(other.Assembly);
        }

        public sealed override int GetHashCode()
        {
            return Assembly.GetHashCode();
        }

        public sealed override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            throw new PlatformNotSupportedException();
        }

        public abstract override int MetadataToken { get; }

        public sealed override Type GetType(string name, bool throwOnError, bool ignoreCase)
        {
            return Assembly.GetType(name, throwOnError, ignoreCase);
        }

        public sealed override Type[] GetTypes()
        {
            Debug.Assert(this.Equals(Assembly.ManifestModule)); // We only support single-module assemblies so we have to be the manifest module.
            return Assembly.GetTypes();
        }

        public abstract override Guid ModuleVersionId { get; }

        public sealed override string ToString()
        {
            return "<Unknown>";
        }

        public sealed override bool IsResource() { throw new PlatformNotSupportedException(); }
        public sealed override void GetPEKind(out PortableExecutableKinds peKind, out ImageFileMachine machine) { throw new PlatformNotSupportedException(); }
        public sealed override int MDStreamVersion { get { throw new PlatformNotSupportedException(); } }
        public sealed override string ScopeName { get { throw new PlatformNotSupportedException(); } }

        public sealed override FieldInfo GetField(string name, BindingFlags bindingAttr) { throw new PlatformNotSupportedException(); }
        public sealed override FieldInfo[] GetFields(BindingFlags bindingFlags) { throw new PlatformNotSupportedException(); }
        protected sealed override MethodInfo GetMethodImpl(string name, BindingFlags bindingAttr, Binder binder, CallingConventions callConvention, Type[] types, ParameterModifier[] modifiers) { throw new PlatformNotSupportedException(); }
        public sealed override MethodInfo[] GetMethods(BindingFlags bindingFlags) { throw new PlatformNotSupportedException(); }
        public sealed override FieldInfo ResolveField(int metadataToken, Type[] genericTypeArguments, Type[] genericMethodArguments) { throw new PlatformNotSupportedException(); }
        public sealed override MemberInfo ResolveMember(int metadataToken, Type[] genericTypeArguments, Type[] genericMethodArguments) { throw new PlatformNotSupportedException(); }
        public sealed override MethodBase ResolveMethod(int metadataToken, Type[] genericTypeArguments, Type[] genericMethodArguments) { throw new PlatformNotSupportedException(); }
        public sealed override byte[] ResolveSignature(int metadataToken) { throw new PlatformNotSupportedException(); }
        public sealed override string ResolveString(int metadataToken) { throw new PlatformNotSupportedException(); }
        public sealed override Type ResolveType(int metadataToken, Type[] genericTypeArguments, Type[] genericMethodArguments) { throw new PlatformNotSupportedException(); }

        protected sealed override ModuleHandle GetModuleHandleImpl() => new ModuleHandle(this);
    }
}

