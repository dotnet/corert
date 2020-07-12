// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;

using System.Reflection.Runtime.General;
using System.Reflection.Runtime.TypeInfos;
using System.Reflection.Runtime.Assemblies;
using System.Reflection.Runtime.Assemblies.EcmaFormat;
using System.Reflection.Runtime.TypeParsing;

using Internal.Reflection.Core;
using Internal.Reflection.Core.Execution;

using System.Reflection.Metadata;

namespace System.Reflection.Runtime.General
{
    internal static partial class TypeResolver
    {
        //
        // Main routine to resolve a typeDef/Ref/Spec.
        //
        internal static RuntimeTypeInfo Resolve(this Handle typeDefRefOrSpec, MetadataReader reader, TypeContext typeContext)
        {
            Exception exception = null;
            RuntimeTypeInfo runtimeType = typeDefRefOrSpec.TryResolve(reader, typeContext, ref exception);
            if (runtimeType == null)
                throw exception;
            return runtimeType;
        }

        internal static RuntimeTypeInfo Resolve(this EntityHandle typeDefRefOrSpec, MetadataReader reader, TypeContext typeContext)
        {
            return ((Handle)typeDefRefOrSpec).Resolve(reader, typeContext);
        }

        internal static RuntimeTypeInfo TryResolve(this Handle typeDefRefOrSpec, MetadataReader reader, TypeContext typeContext, ref Exception exception)
        {
            HandleKind handleKind = typeDefRefOrSpec.Kind;
            if (handleKind == HandleKind.TypeDefinition)
                return ((TypeDefinitionHandle)typeDefRefOrSpec).ResolveTypeDefinition(reader);
            else if (handleKind == HandleKind.TypeReference)
                return ((TypeReferenceHandle)typeDefRefOrSpec).TryResolveTypeReference(reader, ref exception);
            else if (handleKind == HandleKind.TypeSpecification)
                return ((TypeSpecificationHandle)typeDefRefOrSpec).TryResolveTypeSignature(reader, typeContext, ref exception);
            else
                throw new BadImageFormatException();  // Expected TypeRef, Def or Spec.
        }


        //
        // Main routine to resolve a typeDefinition.
        //
        internal static RuntimeTypeInfo ResolveTypeDefinition(this TypeDefinitionHandle typeDefinitionHandle, MetadataReader reader)
        {
            return typeDefinitionHandle.GetNamedType(reader);
        }

        //
        // Main routine to parse a metadata type specification signature.
        //
        private static RuntimeTypeInfo TryResolveTypeSignature(this TypeSpecificationHandle typeSpecHandle, MetadataReader reader, TypeContext typeContext, ref Exception exception)
        {
            TypeSpecification typeSpec = reader.GetTypeSpecification(typeSpecHandle);
            ReflectionTypeProvider refTypeProvider = new ReflectionTypeProvider(throwOnError: false);
            RuntimeTypeInfo result = typeSpec.DecodeSignature<RuntimeTypeInfo, TypeContext>(refTypeProvider, typeContext);
            exception = refTypeProvider.ExceptionResult;
            return result;
        }

        private static string GetFullyQualifiedTypeName(this TypeReference typeReference, MetadataReader reader)
        {
            string fullName = reader.GetString(typeReference.Name);

            if (!typeReference.Namespace.IsNil)
                fullName = reader.GetString(typeReference.Namespace) + "." + fullName;

            return fullName;
        }
        private static RuntimeTypeInfo TryResolveTypeByName(MetadataReader reader, string fullName, ref Exception exception)
        {
            RuntimeAssembly assembly = EcmaFormatRuntimeAssembly.GetRuntimeAssembly(reader);
            return assembly.GetTypeCore(fullName, false);
        }

        //
        // Main routine to resolve a typeReference.
        //
        private static RuntimeTypeInfo TryResolveTypeReference(this TypeReferenceHandle typeReferenceHandle, MetadataReader reader, ref Exception exception)
        {
            TypeReference typeReference = reader.GetTypeReference(typeReferenceHandle);
            EntityHandle resolutionScope = typeReference.ResolutionScope;
            if (resolutionScope.IsNil)
            {
                // Search for an entry in the exported type table for this type
                return TryResolveTypeByName(reader, typeReference.GetFullyQualifiedTypeName(reader), ref exception);
            }

            switch (resolutionScope.Kind)
            {
                case HandleKind.ModuleReference:
                    // multi-module assemblies are not supported by this runtime
                    exception = new PlatformNotSupportedException();
                    return null;

                case HandleKind.TypeReference:
                {
                    // This is a nested type. Find the enclosing type, then search for a matching nested type
                    RuntimeTypeInfo enclosingType = ((TypeReferenceHandle)typeReference.ResolutionScope).TryResolveTypeReference(reader, ref exception);
                    if (enclosingType == null)
                    {
                        Debug.Assert(exception != null);
                        return null;
                    }
                    MetadataStringComparer stringComparer = reader.StringComparer;
                    foreach (var nestedType in enclosingType.GetNestedTypes())
                    {
                        if (stringComparer.Equals(typeReference.Name, nestedType.Name))
                        {
                            if (stringComparer.Equals(typeReference.Namespace, nestedType.Namespace))
                            {
                                return (RuntimeTypeInfo)nestedType.GetTypeInfo();
                            }
                        }
                    }
                    exception = ReflectionCoreExecution.ExecutionDomain.CreateMissingMetadataException(enclosingType, reader.GetString(typeReference.Name));
                    return null;
                }

                case HandleKind.AssemblyReference:
                {
                    string fullName = typeReference.GetFullyQualifiedTypeName(reader);

                    RuntimeAssemblyName runtimeAssemblyName = ((AssemblyReferenceHandle)resolutionScope).ToRuntimeAssemblyName(reader);
                    RuntimeAssembly runtimeAssembly;
                    exception = RuntimeAssembly.TryGetRuntimeAssembly(runtimeAssemblyName, out runtimeAssembly);
                    if (exception != null)
                        return null;
                    RuntimeTypeInfo runtimeType = runtimeAssembly.GetTypeCore(fullName, ignoreCase: false);
                    if (runtimeType == null)
                    {
                        exception = Helpers.CreateTypeLoadException(fullName, runtimeAssemblyName.FullName);
                        return null;
                    }
                    return runtimeType;
                }

                case HandleKind.ModuleDefinition:
                    return TryResolveTypeByName(reader, typeReference.GetFullyQualifiedTypeName(reader), ref exception);

                default:
                    exception = new BadImageFormatException();
                    return null;
            }
        }
    }
}

