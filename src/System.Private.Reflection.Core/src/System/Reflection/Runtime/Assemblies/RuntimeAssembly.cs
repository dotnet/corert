// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
using Internal.Metadata.NativeFormat;

using Internal.Reflection.Tracing;

namespace System.Reflection.Runtime.Assemblies
{
    //
    // The runtime's implementation of an Assembly. 
    //
    internal sealed partial class RuntimeAssembly : Assembly, IEquatable<RuntimeAssembly>
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
                    foreach (CustomAttributeData cad in RuntimeCustomAttributeData.GetCustomAttributes(scope.Reader, scope.ScopeDefinition.CustomAttributes))
                        yield return cad;

                    foreach (CustomAttributeData cad in ReflectionCoreExecution.ExecutionEnvironment.GetPsuedoCustomAttributes(scope.Reader, scope.Handle))
                        yield return cad;
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
                        yield return typeDefinitionHandle.GetNamedType(reader);
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
                    IEnumerable<NamespaceDefinitionHandle> topLevelNamespaceHandles = new NamespaceDefinitionHandle[] { scopeDefinition.RootNamespaceDefinition };
                    IEnumerable<NamespaceDefinitionHandle> allNamespaceHandles = reader.GetTransitiveNamespaces(topLevelNamespaceHandles);
                    IEnumerable<TypeDefinitionHandle> allTopLevelTypes = reader.GetTopLevelTypes(allNamespaceHandles);
                    IEnumerable<TypeDefinitionHandle> allTypes = reader.GetTransitiveTypes(allTopLevelTypes, publicOnly: true);
                    foreach (TypeDefinitionHandle typeDefinitionHandle in allTypes)
                        yield return typeDefinitionHandle.ResolveTypeDefinition(reader);
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
            return ReflectionCoreExecution.ExecutionEnvironment.GetManifestResourceInfo(this, resourceName);
        }

        public sealed override String[] GetManifestResourceNames()
        {
            return ReflectionCoreExecution.ExecutionEnvironment.GetManifestResourceNames(this);
        }

        public sealed override Stream GetManifestResourceStream(String name)
        {
            return ReflectionCoreExecution.ExecutionEnvironment.GetManifestResourceStream(this, name);
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

        internal QScopeDefinition Scope { get; }

        internal IEnumerable<QScopeDefinition> OverflowScopes { get; }

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
    }
}



