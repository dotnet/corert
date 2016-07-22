// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;

using global::System.Reflection.Runtime.General;
using global::System.Reflection.Runtime.TypeInfos;
using global::System.Reflection.Runtime.Assemblies;
using global::System.Reflection.Runtime.TypeParsing;

using global::Internal.Reflection.Core;
using global::Internal.Reflection.Core.Execution;

using global::Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.General
{
    internal static class TypeResolver
    {
        //
        // Main routine to resolve a typeDef/Ref/Spec.
        //
        internal static RuntimeTypeInfo Resolve(this Handle typeDefRefOrSpec, MetadataReader reader,TypeContext typeContext)
        {
            Exception exception = null;
            RuntimeTypeInfo runtimeType = typeDefRefOrSpec.TryResolve(reader, typeContext, ref exception);
            if (runtimeType == null)
                throw exception;
            return runtimeType;
        }

        internal static RuntimeTypeInfo TryResolve(this Handle typeDefRefOrSpec, MetadataReader reader, TypeContext typeContext, ref Exception exception)
        {
            HandleType handleType = typeDefRefOrSpec.HandleType;
            if (handleType == HandleType.TypeDefinition)
                return typeDefRefOrSpec.ToTypeDefinitionHandle(reader).ResolveTypeDefinition(reader);
            else if (handleType == HandleType.TypeReference)
                return typeDefRefOrSpec.ToTypeReferenceHandle(reader).TryResolveTypeReference(reader, ref exception);
            else if (handleType == HandleType.TypeSpecification)
                return typeDefRefOrSpec.ToTypeSpecificationHandle(reader).TryResolveTypeSignature(reader, typeContext, ref exception);
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
            Handle typeHandle = typeSpecHandle.GetTypeSpecification(reader).Signature;
            switch (typeHandle.HandleType)
            {
                case HandleType.ArraySignature:
                    {
                        ArraySignature sig = typeHandle.ToArraySignatureHandle(reader).GetArraySignature(reader);
                        int rank = sig.Rank;
                        if (rank <= 0)
                            throw new BadImageFormatException(); // Bad rank.
                        RuntimeTypeInfo elementType = sig.ElementType.TryResolve(reader, typeContext, ref exception);
                        if (elementType == null)
                            return null;
                        return elementType.GetMultiDimArrayType(rank);
                    }

                case HandleType.ByReferenceSignature:
                    {
                        ByReferenceSignature sig = typeHandle.ToByReferenceSignatureHandle(reader).GetByReferenceSignature(reader);
                        RuntimeTypeInfo targetType = sig.Type.TryResolve(reader, typeContext, ref exception);
                        if (targetType == null)
                            return null;
                        return targetType.GetByRefType();
                    }

                case HandleType.MethodTypeVariableSignature:
                    {
                        MethodTypeVariableSignature sig = typeHandle.ToMethodTypeVariableSignatureHandle(reader).GetMethodTypeVariableSignature(reader);
                        return typeContext.GenericMethodArguments[sig.Number];
                    }

                case HandleType.PointerSignature:
                    {
                        PointerSignature sig = typeHandle.ToPointerSignatureHandle(reader).GetPointerSignature(reader);
                        RuntimeTypeInfo targetType = sig.Type.TryResolve(reader, typeContext, ref exception);
                        if (targetType == null)
                            return null;
                        return targetType.GetPointerType();
                    }

                case HandleType.SZArraySignature:
                    {
                        SZArraySignature sig = typeHandle.ToSZArraySignatureHandle(reader).GetSZArraySignature(reader);
                        RuntimeTypeInfo elementType = sig.ElementType.TryResolve(reader, typeContext, ref exception);
                        if (elementType == null)
                            return null;
                        return elementType.GetArrayType();
                    }

                case HandleType.TypeDefinition:
                    {
                        return typeHandle.ToTypeDefinitionHandle(reader).ResolveTypeDefinition(reader);
                    }

                case HandleType.TypeInstantiationSignature:
                    {
                        TypeInstantiationSignature sig = typeHandle.ToTypeInstantiationSignatureHandle(reader).GetTypeInstantiationSignature(reader);
                        RuntimeTypeInfo genericTypeDefinition = sig.GenericType.TryResolve(reader, typeContext, ref exception);
                        if (genericTypeDefinition == null)
                            return null;
                        LowLevelList<RuntimeTypeInfo> genericTypeArguments = new LowLevelList<RuntimeTypeInfo>();
                        foreach (Handle genericTypeArgumentHandle in sig.GenericTypeArguments)
                        {
                            RuntimeTypeInfo genericTypeArgument = genericTypeArgumentHandle.TryResolve(reader, typeContext, ref exception);
                            if (genericTypeArgument == null)
                                return null;
                            genericTypeArguments.Add(genericTypeArgument);
                        }
                        return genericTypeDefinition.GetConstructedGenericType(genericTypeArguments.ToArray());
                    }

                case HandleType.TypeReference:
                    {
                        return typeHandle.ToTypeReferenceHandle(reader).TryResolveTypeReference(reader, ref exception);
                    }

                case HandleType.TypeVariableSignature:
                    {
                        TypeVariableSignature sig = typeHandle.ToTypeVariableSignatureHandle(reader).GetTypeVariableSignature(reader);
                        return typeContext.GenericTypeArguments[sig.Number];
                    }

                default:
                    throw new NotSupportedException(); // Unexpected Type signature type.
            }
        }

        //
        // Main routine to resolve a typeReference.
        //
        private static RuntimeTypeInfo TryResolveTypeReference(this TypeReferenceHandle typeReferenceHandle, MetadataReader reader, ref Exception exception)
        {
           RuntimeTypeHandle resolvedRuntimeTypeHandle;
           if (ReflectionCoreExecution.ExecutionEnvironment.TryGetNamedTypeForTypeReference(reader, typeReferenceHandle, out resolvedRuntimeTypeHandle))
               return resolvedRuntimeTypeHandle.GetTypeForRuntimeTypeHandle();

            TypeReference typeReference = typeReferenceHandle.GetTypeReference(reader);
            String name = typeReference.TypeName.GetString(reader);
            Handle parent = typeReference.ParentNamespaceOrType;
            HandleType parentType = parent.HandleType;
            TypeInfo outerTypeInfo = null;

            // Check if this is a reference to a nested type.

            if (parentType == HandleType.TypeDefinition)
            {
                outerTypeInfo = parent.ToTypeDefinitionHandle(reader).GetNamedType(reader);
            }
            else if (parentType == HandleType.TypeReference)
            {
                RuntimeTypeInfo outerType = parent.ToTypeReferenceHandle(reader).TryResolveTypeReference(reader, ref exception);
                if (outerType == null)
                    return null;
                outerTypeInfo = outerType;   // Since we got to outerType via a metadata reference, we're assured GetTypeInfo() won't throw a MissingMetadataException.
            }
            if (outerTypeInfo != null)
            {
                // It was a nested type. We've already resolved the containing type recursively - just find the nested among its direct children.
                TypeInfo resolvedTypeInfo = outerTypeInfo.GetDeclaredNestedType(name);
                if (resolvedTypeInfo == null)
                {
                    exception = ReflectionCoreExecution.ExecutionDomain.CreateMissingMetadataException(outerTypeInfo, name);
                    return null;
                }
                return resolvedTypeInfo.CastToRuntimeTypeInfo();
            }


            // If we got here, the typeReference was to a non-nested type. 
            if (parentType == HandleType.NamespaceReference)
            {
                AssemblyQualifiedTypeName assemblyQualifiedTypeName = parent.ToNamespaceReferenceHandle(reader).ToAssemblyQualifiedTypeName(name, reader);
                RuntimeTypeInfo runtimeType;
                exception = assemblyQualifiedTypeName.TryResolve(null, /*ignoreCase: */false, out runtimeType);
                if (exception != null)
                    return null;
                return runtimeType;
            }

            throw new BadImageFormatException(); // Expected TypeReference parent to be typeRef, typeDef or namespaceRef.
        }
    }
}

