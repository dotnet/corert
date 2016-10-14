﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

extern alias ECMA;

using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Reflection;
using ECMA::System.Reflection.Metadata;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.Modules;
using System.Reflection.Runtime.TypeInfos;
using System.Reflection.Runtime.TypeParsing;
using System.Reflection.Runtime.CustomAttributes;
using System.Collections.Generic;

using Internal.Reflection.Core;
using Internal.Reflection.Core.Execution;

using Internal.Reflection.Tracing;

namespace System.Reflection.Runtime.Assemblies.EcmaFormat
{
    internal partial class EcmaFormatRuntimeAssembly : RuntimeAssembly
    {
        public EcmaFormatRuntimeAssembly(MetadataReader reader)
        {
            AssemblyDefinition = reader.GetAssemblyDefinition();
            MetadataReader = reader;
        }

        public sealed override IEnumerable<CustomAttributeData> CustomAttributes
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.Assembly_CustomAttributes(this);
#endif
                throw new NotImplementedException();
                /*
                                foreach (QScopeDefinition scope in AllScopes)
                                {
                                    foreach (CustomAttributeData cad in RuntimeCustomAttributeData.GetCustomAttributes(scope.Reader, scope.ScopeDefinition.CustomAttributes))
                                        yield return cad;

                                    foreach (CustomAttributeData cad in ReflectionCoreExecution.ExecutionEnvironment.GetPsuedoCustomAttributes(scope.Reader, scope.Handle))
                                        yield return cad;
                                }*/
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
                throw new NotImplementedException();
                /*
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
                }*/
            }
        }

        public sealed override IEnumerable<Type> ExportedTypes
        {
            get
            {
                throw new NotImplementedException();
                /*
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
                }*/
            }
        }

        public sealed override ManifestResourceInfo GetManifestResourceInfo(String resourceName)
        {
            throw new NotImplementedException();
            //return ReflectionCoreExecution.ExecutionEnvironment.GetManifestResourceInfo(this, resourceName);
        }

        public sealed override String[] GetManifestResourceNames()
        {
            throw new NotImplementedException();
            //return ReflectionCoreExecution.ExecutionEnvironment.GetManifestResourceNames(this);
        }

        public sealed override Stream GetManifestResourceStream(String name)
        {
            throw new NotImplementedException();
            //return ReflectionCoreExecution.ExecutionEnvironment.GetManifestResourceStream(this, name);
        }

        public sealed override AssemblyName GetName()
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.Assembly_GetName(this);
#endif
            throw new NotImplementedException();
//            return Scope.Handle.ToRuntimeAssemblyName(Scope.Reader).ToAssemblyName();
        }

        public sealed override bool Equals(Object obj)
        {
            throw new NotImplementedException();
//            NativeFormatRuntimeAssembly other = obj as NativeFormamsbutRuntimeAssembly;
  //          return Equals(other);
        }

        public bool Equals(EcmaFormatRuntimeAssembly other)
        {
            throw new NotImplementedException();
/*
            if (other == null)
                return false;
            if (!(this.Scope.Reader == other.Scope.Reader))
                return false;
            if (!(this.Scope.Handle.Equals(other.Scope.Handle)))
                return false;
            return true;*/
        }

        public sealed override int GetHashCode()
        {
            throw new NotImplementedException();

//            return Scope.Handle.GetHashCode();
        }

        internal AssemblyDefinition AssemblyDefinition { get; }
        internal MetadataReader MetadataReader { get; }
    }
}
