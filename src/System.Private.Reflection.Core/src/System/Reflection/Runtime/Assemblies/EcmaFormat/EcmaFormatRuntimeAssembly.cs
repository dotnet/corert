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
using System.Reflection.Runtime.MethodInfos;
using System.Reflection.Runtime.MethodInfos.EcmaFormat;
using System.Reflection.Runtime.TypeInfos;
using System.Reflection.Runtime.TypeInfos.EcmaFormat;
using System.Reflection.Runtime.TypeParsing;
using System.Reflection.Runtime.CustomAttributes;
using System.Collections.Generic;

using Internal.Reflection.Core;
using Internal.Reflection.Core.Execution;

using Internal.Reflection.Tracing;

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

using Internal.Runtime.TypeLoader;

namespace System.Reflection.Runtime.Assemblies.EcmaFormat
{
    internal sealed partial class EcmaFormatRuntimeAssembly : RuntimeAssembly
    {
        private EcmaFormatRuntimeAssembly(MetadataReader reader)
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
                foreach (CustomAttributeData cad in RuntimeCustomAttributeData.GetCustomAttributes(MetadataReader, MetadataReader.GetAssemblyDefinition().GetCustomAttributes()))
                    yield return cad;
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
                TypeDefinitionHandleCollection allTypes = MetadataReader.TypeDefinitions;

                bool skipFirstType = true; // The first type is always the module type, which isn't returned by this api.
                foreach (TypeDefinitionHandle typeDefinitionHandle in allTypes)
                {
                    if (skipFirstType)
                    {
                        skipFirstType = false;
                        continue;
                    }
                    yield return typeDefinitionHandle.GetNamedType(MetadataReader);
                }
            }
        }

        public sealed override IEnumerable<Type> ExportedTypes
        {
            get
            {
                return Array.Empty<Type>();
                /*
                 * TODO! Implement
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

        internal sealed override RuntimeAssemblyName RuntimeAssemblyName
        {
            get
            {
                return AssemblyDefinition.ToRuntimeAssemblyName(MetadataReader);
            }
        }

        public sealed override bool Equals(Object obj)
        {
            EcmaFormatRuntimeAssembly other = obj as EcmaFormatRuntimeAssembly;
            return Equals(other);
        }

        public bool Equals(EcmaFormatRuntimeAssembly other)
        {
            return Object.ReferenceEquals(other.MetadataReader, MetadataReader);
        }

        public sealed override int GetHashCode()
        {
            return MetadataReader.GetHashCode();
        }

        public sealed override MethodInfo EntryPoint 
        {
            get 
            {
                 
                CorHeader corHeader = PEReader.PEHeaders.CorHeader; 
                if ((corHeader.Flags & CorFlags.NativeEntryPoint) != 0) 
                { 
                    // Entrypoint is an RVA to an unmanaged method 
                    return null; 
                } 

                int entryPointToken = corHeader.EntryPointTokenOrRelativeVirtualAddress; 
                if (entryPointToken == 0) 
                { 
                    // No entrypoint 
                    return null; 
                } 
 
                Handle handle = MetadataTokens.Handle(entryPointToken); 

                if (handle.Kind != HandleKind.MethodDefinition)
                {
                    return null;
                }

                MethodDefinitionHandle methodHandle = (MethodDefinitionHandle)handle;
                TypeDefinitionHandle declaringType = MetadataReader.GetMethodDefinition(methodHandle).GetDeclaringType();
                RuntimeTypeInfo runtimeType = declaringType.GetNamedType(MetadataReader);

                return RuntimeNamedMethodInfo<EcmaFormatMethodCommon>.GetRuntimeNamedMethodInfo(new EcmaFormatMethodCommon(methodHandle, (EcmaFormatRuntimeNamedTypeInfo)runtimeType, runtimeType), runtimeType);
            }
        }


        internal AssemblyDefinition AssemblyDefinition { get; }
        internal MetadataReader MetadataReader { get; }
        internal PEReader PEReader
        {
            get
            {
                var moduleInfo = ModuleList.Instance.GetModuleInfoForMetadataReader(MetadataReader);
                return moduleInfo.EcmaPEInfo.PE;
            }
        }
    }
}
