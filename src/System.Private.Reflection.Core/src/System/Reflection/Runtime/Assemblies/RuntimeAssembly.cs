// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.Modules;
using System.Reflection.Runtime.TypeInfos;
using System.Reflection.Runtime.TypeParsing;
using System.Reflection.Runtime.CustomAttributes;
using System.Collections.Generic;

using Internal.Reflection.Core;
using Internal.Reflection.Core.Execution;

using Internal.Reflection.Tracing;

namespace System.Reflection.Runtime.Assemblies
{
    //
    // The runtime's implementation of an Assembly. 
    //
    internal abstract partial class RuntimeAssembly : Assembly, IEquatable<RuntimeAssembly>
    {
        public bool Equals(RuntimeAssembly other)
        {
            if (other == null)
                return false;

            return this.Equals((object)other);
        }

        public sealed override String FullName
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.Assembly_FullName(this);
#endif

                return GetName().FullName;
            }
        }

        public sealed override Module ManifestModule
        {
            get
            {
                return RuntimeModule.GetRuntimeModule(this);
            }
        }

        public sealed override IEnumerable<Module> Modules
        {
            get
            {
                yield return ManifestModule;
            }
        }


        public sealed override Type GetType(String name, bool throwOnError, bool ignoreCase)
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.Assembly_GetType(this, name);
#endif

            if (name == null)
                throw new ArgumentNullException();
            if (name.Length == 0)
                throw new ArgumentException();

            TypeName typeName = TypeParser.ParseAssemblyQualifiedTypeName(name, throwOnError: throwOnError);
            if (typeName == null)
                return null;
            if (typeName is AssemblyQualifiedTypeName)
            {
                if (throwOnError)
                    throw new ArgumentException(SR.Argument_AssemblyGetTypeCannotSpecifyAssembly);  // Cannot specify an assembly qualifier in a typename passed to Assembly.GetType()
                else
                    return null;
            }

            CoreAssemblyResolver coreAssemblyResolver = RuntimeAssembly.GetRuntimeAssemblyIfExists;
            CoreTypeResolver coreTypeResolver =
                delegate (Assembly containingAssemblyIfAny, string coreTypeName)
                {
                    if (containingAssemblyIfAny == null)
                        return GetTypeCore(coreTypeName, ignoreCase: ignoreCase);
                    else
                        return containingAssemblyIfAny.GetTypeCore(coreTypeName, ignoreCase: ignoreCase);
                };
            GetTypeOptions getTypeOptions = new GetTypeOptions(coreAssemblyResolver, coreTypeResolver, throwOnError: throwOnError, ignoreCase: ignoreCase);

            return typeName.ResolveType(this, getTypeOptions);
        }

#pragma warning disable 0067  // Silence warning about ModuleResolve not being used.
        public sealed override event ModuleResolveEventHandler ModuleResolve;
#pragma warning restore 0067

        public sealed override bool ReflectionOnly
        {
            get
            {
                return false; // ReflectionOnly loading not supported.
            }
        }

        internal abstract RuntimeAssemblyName RuntimeAssemblyName { get; }

        public sealed override AssemblyName GetName()
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.Assembly_GetName(this);
#endif
            return RuntimeAssemblyName.ToAssemblyName();
        }

        /// <summary>
        /// Helper routine for the more general Type.GetType() family of apis.
        ///
        /// Resolves top-level named types only. No nested types. No constructed types.
        ///
        /// Returns null if the type does not exist. Throws for all other error cases.
        /// </summary>
        internal RuntimeTypeInfo GetTypeCore(string fullName, bool ignoreCase)
        {
            if (ignoreCase)
                return GetTypeCoreCaseInsensitive(fullName);
            else
                return GetTypeCoreCaseSensitive(fullName);
        }

        // Types that derive from RuntimeAssembly must implement the following public surface area members
        public abstract override IEnumerable<CustomAttributeData> CustomAttributes { get; }
        public abstract override IEnumerable<TypeInfo> DefinedTypes { get; }
        public abstract override IEnumerable<Type> ExportedTypes { get; }
        public abstract override ManifestResourceInfo GetManifestResourceInfo(String resourceName);
        public abstract override String[] GetManifestResourceNames();
        public abstract override Stream GetManifestResourceStream(String name);
        public abstract override bool Equals(Object obj);
        public abstract override int GetHashCode();


        /// <summary>
        /// Perform a lookup for a type based on a name. Overriders are expected to
        /// have a non-cached implementation, as the result is expected to be cached by
        /// callers of this method. Should be implemented by every format specific 
        /// RuntimeAssembly implementor
        /// </summary>
        internal abstract RuntimeTypeInfo UncachedGetTypeCoreCaseSensitive(string fullName);


        /// <summary>
        /// Perform a lookup for a type based on a name. Overriders may or may not 
        /// have a cached implementation, as the result is not expected to be cached by
        /// callers of this method, but it is also a rarely used api. Should be 
        /// implemented by every format specific RuntimeAssembly implementor
        /// </summary>
        internal abstract RuntimeTypeInfo GetTypeCoreCaseInsensitive(string fullName);

        internal RuntimeTypeInfo GetTypeCoreCaseSensitive(string fullName)
        {
            return this.CaseSensitiveTypeTable.GetOrAdd(fullName);
        }

        private CaseSensitiveTypeCache CaseSensitiveTypeTable
        {
            get
            {
                return _lazyCaseSensitiveTypeTable ?? (_lazyCaseSensitiveTypeTable = new CaseSensitiveTypeCache(this));
            }
        }

        private volatile CaseSensitiveTypeCache _lazyCaseSensitiveTypeTable;

        private sealed class CaseSensitiveTypeCache : ConcurrentUnifier<string, RuntimeTypeInfo>
        {
            public CaseSensitiveTypeCache(RuntimeAssembly runtimeAssembly)
            {
                _runtimeAssembly = runtimeAssembly;
            }

            protected sealed override RuntimeTypeInfo Factory(string key)
            {
                return _runtimeAssembly.UncachedGetTypeCoreCaseSensitive(key);
            }

            private readonly RuntimeAssembly _runtimeAssembly;
        }
    }
}



