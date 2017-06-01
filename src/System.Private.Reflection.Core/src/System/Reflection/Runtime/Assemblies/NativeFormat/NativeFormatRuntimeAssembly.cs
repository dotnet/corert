﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.MethodInfos;
using System.Reflection.Runtime.MethodInfos.NativeFormat;
using System.Reflection.Runtime.Modules;
using System.Reflection.Runtime.Modules.NativeFormat;
using System.Reflection.Runtime.TypeInfos;
using System.Reflection.Runtime.TypeInfos.NativeFormat;
using System.Reflection.Runtime.TypeParsing;
using System.Reflection.Runtime.CustomAttributes;
using System.Collections.Generic;

using Internal.Reflection.Core;
using Internal.Reflection.Core.Execution;
using Internal.Metadata.NativeFormat;
using Internal.Reflection.Tracing;

namespace System.Reflection.Runtime.Assemblies.NativeFormat
{
    internal sealed partial class NativeFormatRuntimeAssembly : RuntimeAssembly
    {
        private NativeFormatRuntimeAssembly(MetadataReader reader, ScopeDefinitionHandle scope, IEnumerable<QScopeDefinition> overflowScopes)
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

                    foreach (CustomAttributeData cad in ReflectionCoreExecution.ExecutionEnvironment.GetPseudoCustomAttributes(scope.Reader, scope.Handle))
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

        public sealed override MethodInfo EntryPoint
        {
            get
            {
                // The scope that defines metadata for the owning type of the entrypoint will be the one
                // to carry the entrypoint token information. Find it by iterating over all scopes.

                foreach (QScopeDefinition scope in AllScopes)
                {
                    MetadataReader reader = scope.Reader;

                    QualifiedMethodHandle entrypointHandle = scope.ScopeDefinition.EntryPoint;
                    if (!entrypointHandle.IsNull(reader))
                    {
                        QualifiedMethod entrypointMethod = entrypointHandle.GetQualifiedMethod(reader);
                        TypeDefinitionHandle declaringTypeHandle = entrypointMethod.EnclosingType;
                        MethodHandle methodHandle = entrypointMethod.Method;
                        NativeFormatRuntimeNamedTypeInfo containingType = NativeFormatRuntimeNamedTypeInfo.GetRuntimeNamedTypeInfo(reader, declaringTypeHandle, default(RuntimeTypeHandle));
                        return RuntimeNamedMethodInfo<NativeFormatMethodCommon>.GetRuntimeNamedMethodInfo(new NativeFormatMethodCommon(methodHandle, containingType, containingType), containingType);
                    }
                }

                return null;
            }
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

        public sealed override Module ManifestModule
        {
            get
            {
                return NativeFormatRuntimeModule.GetRuntimeModule(this);
            }
        }

        internal sealed override RuntimeAssemblyName RuntimeAssemblyName
        {
            get
            {
                return Scope.Handle.ToRuntimeAssemblyName(Scope.Reader);
            }
        }

        public sealed override bool Equals(Object obj)
        {
            NativeFormatRuntimeAssembly other = obj as NativeFormatRuntimeAssembly;
            return Equals(other);
        }

        public bool Equals(NativeFormatRuntimeAssembly other)
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

        internal sealed override void RunModuleConstructor()
        {
            // Nothing to do for the native format. ILC groups all module cctors into StartupCodeTrigger, and this executes at 
            // the begining of the process. All module cctors execute eagerly.
            return;
        }
    }
}
