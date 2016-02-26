// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.IO;
using global::System.Text;
using global::System.Diagnostics;
using global::System.Reflection;
using global::System.Reflection.Runtime.General;
using global::System.Reflection.Runtime.Modules;
using global::System.Reflection.Runtime.TypeInfos;
using global::System.Reflection.Runtime.TypeParsing;
using global::System.Reflection.Runtime.CustomAttributes;
using global::System.Collections.Generic;

using global::Internal.Reflection.Core;
using global::Internal.Reflection.Core.Execution;
using global::Internal.Reflection.Core.NonPortable;
using global::Internal.Reflection.Extensibility;
using global::Internal.Metadata.NativeFormat;

using global::Internal.Reflection.Tracing;

namespace System.Reflection.Runtime.Assemblies
{
    //
    // The runtime's implementation of an Assembly. 
    //
    internal sealed partial class RuntimeAssembly : ExtensibleAssembly, IEquatable<RuntimeAssembly>
    {
        private RuntimeAssembly(MetadataReader reader, ScopeDefinitionHandle scope, IEnumerable<QScopeDefinition> overflowScopes)
        {
            Scope = new QScopeDefinition(reader, scope);
            OverflowScopes = overflowScopes;
        }

        public sealed override IEnumerable<CustomAttributeData> CustomAttributes
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.Assembly_CustomAttributes(this);
#endif

                foreach (QScopeDefinition scope in AllScopes)
                {
                    foreach (CustomAttributeData cad in RuntimeCustomAttributeData.GetCustomAttributes(this.ReflectionDomain, scope.Reader, scope.ScopeDefinition.CustomAttributes))
                        yield return cad;

                    ExecutionDomain executionDomain = this.ReflectionDomain as ExecutionDomain;
                    if (executionDomain != null)
                    {
                        foreach (CustomAttributeData cad in executionDomain.ExecutionEnvironment.GetPsuedoCustomAttributes(scope.Reader, scope.Handle))
                            yield return cad;
                    }
                }
            }
        }

        public sealed override IEnumerable<TypeInfo> DefinedTypes
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.Assembly_DefinedTypes(this);
#endif

                foreach (QScopeDefinition scope in AllScopes)
                {
                    MetadataReader reader = scope.Reader;
                    ScopeDefinition scopeDefinition = scope.ScopeDefinition;
                    IEnumerable<NamespaceDefinitionHandle> topLevelNamespaceHandles = new NamespaceDefinitionHandle[] { scopeDefinition.RootNamespaceDefinition };
                    IEnumerable<NamespaceDefinitionHandle> allNamespaceHandles = reader.GetTransitiveNamespaces(topLevelNamespaceHandles);
                    IEnumerable<TypeDefinitionHandle> allTopLevelTypes = reader.GetTopLevelTypes(allNamespaceHandles);
                    IEnumerable<TypeDefinitionHandle> allTypes = reader.GetTransitiveTypes(allTopLevelTypes, publicOnly: false);
                    foreach (TypeDefinitionHandle typeDefinitionHandle in allTypes)
                        yield return RuntimeNamedTypeInfo.GetRuntimeNamedTypeInfo(reader, typeDefinitionHandle);
                }
            }
        }

        public sealed override IEnumerable<Type> ExportedTypes
        {
            get
            {
                foreach (QScopeDefinition scope in AllScopes)
                {
                    MetadataReader reader = scope.Reader;
                    ScopeDefinition scopeDefinition = scope.ScopeDefinition;
                    ReflectionDomain reflectionDomain = this.ReflectionDomain;
                    IEnumerable<NamespaceDefinitionHandle> topLevelNamespaceHandles = new NamespaceDefinitionHandle[] { scopeDefinition.RootNamespaceDefinition };
                    IEnumerable<NamespaceDefinitionHandle> allNamespaceHandles = reader.GetTransitiveNamespaces(topLevelNamespaceHandles);
                    IEnumerable<TypeDefinitionHandle> allTopLevelTypes = reader.GetTopLevelTypes(allNamespaceHandles);
                    IEnumerable<TypeDefinitionHandle> allTypes = reader.GetTransitiveTypes(allTopLevelTypes, publicOnly: true);
                    foreach (TypeDefinitionHandle typeDefinitionHandle in allTypes)
                        yield return reflectionDomain.ResolveTypeDefinition(reader, typeDefinitionHandle);
                }
            }
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

        public sealed override bool Equals(Object obj)
        {
            RuntimeAssembly other = obj as RuntimeAssembly;
            return Equals(other);
        }

        public bool Equals(RuntimeAssembly other)
        {
            if (other == null)
                return false;
            if (!(this.Scope.Reader == other.Scope.Reader))
                return false;
            if (!(this.Scope.Handle.Equals(other.Scope.Handle)))
                return false;
            return true;
        }

        public sealed override int GetHashCode()
        {
            return Scope.Handle.GetHashCode();
        }

        public sealed override ManifestResourceInfo GetManifestResourceInfo(String resourceName)
        {
            ExecutionDomain executionDomain = this.ReflectionDomain as ExecutionDomain;
            if (executionDomain == null)
                throw new PlatformNotSupportedException();
            return executionDomain.ExecutionEnvironment.GetManifestResourceInfo(this, resourceName);
        }

        public sealed override String[] GetManifestResourceNames()
        {
            ExecutionDomain executionDomain = this.ReflectionDomain as ExecutionDomain;
            if (executionDomain == null)
                throw new PlatformNotSupportedException();
            return executionDomain.ExecutionEnvironment.GetManifestResourceNames(this);
        }

        public sealed override Stream GetManifestResourceStream(String name)
        {
            ExecutionDomain executionDomain = this.ReflectionDomain as ExecutionDomain;
            if (executionDomain == null)
                throw new PlatformNotSupportedException();
            return executionDomain.ExecutionEnvironment.GetManifestResourceStream(this, name);
        }

        public sealed override AssemblyName GetName()
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.Assembly_GetName(this);
#endif

            return Scope.Handle.ToRuntimeAssemblyName(Scope.Reader).ToAssemblyName();
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

            AssemblyQualifiedTypeName assemblyQualifiedTypeName;
            try
            {
                assemblyQualifiedTypeName = TypeParser.ParseAssemblyQualifiedTypeName(name);
                if (assemblyQualifiedTypeName.AssemblyName != null)
                    throw new ArgumentException(SR.Argument_AssemblyGetTypeCannotSpecifyAssembly);  // Cannot specify an assembly qualifier in a typename passed to Assembly.GetType()
            }
            catch (ArgumentException)
            {
                if (throwOnError)
                    throw;
                return null;
            }

            RuntimeType result;
            Exception typeLoadException = assemblyQualifiedTypeName.TypeName.TryResolve(this.ReflectionDomain, this, ignoreCase, out result);
            if (typeLoadException != null)
            {
                if (throwOnError)
                    throw typeLoadException;
                return null;
            }
            return result;
        }

        internal QScopeDefinition Scope { get; private set; }

        internal IEnumerable<QScopeDefinition> OverflowScopes { get; private set; }

        internal IEnumerable<QScopeDefinition> AllScopes
        {
            get
            {
                yield return Scope;

                foreach (QScopeDefinition overflowScope in OverflowScopes)
                {
                    yield return overflowScope;
                }
            }
        }

        internal ReflectionDomain ReflectionDomain
        {
            get
            {
                return ReflectionCoreExecution.ExecutionDomain;  //@todo: User Reflection Domains not yet supported.
            }
        }
    }
}



