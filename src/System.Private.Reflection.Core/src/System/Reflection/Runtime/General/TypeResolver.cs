// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;

using global::System.Reflection.Runtime.General;
using global::System.Reflection.Runtime.Types;
using global::System.Reflection.Runtime.TypeInfos;
using global::System.Reflection.Runtime.Assemblies;
using global::System.Reflection.Runtime.TypeParsing;

using global::Internal.Reflection.Core;
using global::Internal.Reflection.Core.Execution;
using global::Internal.Reflection.Core.NonPortable;

using global::Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.General
{
    internal static class TypeResolver
    {
        //
        // Main routine to resolve a typeDef/Ref/Spec.
        //
        internal static RuntimeType Resolve(this ReflectionDomain reflectionDomain, MetadataReader reader, Handle typeDefRefOrSpec, TypeContext typeContext)
        {
            Exception exception = null;
            RuntimeType runtimeType = reflectionDomain.TryResolve(reader, typeDefRefOrSpec, typeContext, ref exception);
            if (runtimeType == null)
                throw exception;
            return runtimeType;
        }

        internal static RuntimeType TryResolve(this ReflectionDomain reflectionDomain, MetadataReader reader, Handle typeDefRefOrSpec, TypeContext typeContext, ref Exception exception)
        {
            HandleType handleType = typeDefRefOrSpec.HandleType;
            if (handleType == HandleType.TypeDefinition)
                return reflectionDomain.ResolveTypeDefinition(reader, typeDefRefOrSpec.ToTypeDefinitionHandle(reader));
            else if (handleType == HandleType.TypeReference)
                return reflectionDomain.TryResolveTypeReference(reader, typeDefRefOrSpec.ToTypeReferenceHandle(reader), ref exception);
            else if (handleType == HandleType.TypeSpecification)
                return reflectionDomain.TryResolveTypeSignature(reader, typeDefRefOrSpec.ToTypeSpecificationHandle(reader), typeContext, ref exception);
            else
                throw new BadImageFormatException();  // Expected TypeRef, Def or Spec.
        }


        //
        // Main routine to resolve a typeDefinition.
        //
        internal static RuntimeType ResolveTypeDefinition(this ReflectionDomain reflectionDomain, MetadataReader reader, TypeDefinitionHandle typeDefinitionHandle)
        {
            return RuntimeTypeUnifierEx.GetNamedType(reader, typeDefinitionHandle);
        }

        //
        // Main routine to parse a metadata type specification signature.
        //
        private static RuntimeType TryResolveTypeSignature(this ReflectionDomain reflectionDomain, MetadataReader reader, TypeSpecificationHandle typeSpecHandle, TypeContext typeContext, ref Exception exception)
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
                        RuntimeType elementType = reflectionDomain.TryResolve(reader, sig.ElementType, typeContext, ref exception);
                        if (elementType == null)
                            return null;
                        return ReflectionCoreNonPortable.GetMultiDimArrayType(elementType, rank);
                    }

                case HandleType.ByReferenceSignature:
                    {
                        ByReferenceSignature sig = typeHandle.ToByReferenceSignatureHandle(reader).GetByReferenceSignature(reader);
                        RuntimeType targetType = reflectionDomain.TryResolve(reader, sig.Type, typeContext, ref exception);
                        if (targetType == null)
                            return null;
                        return ReflectionCoreNonPortable.GetByRefType(targetType);
                    }

                case HandleType.MethodTypeVariableSignature:
                    {
                        MethodTypeVariableSignature sig = typeHandle.ToMethodTypeVariableSignatureHandle(reader).GetMethodTypeVariableSignature(reader);
                        return typeContext.GenericMethodArguments[sig.Number];
                    }

                case HandleType.PointerSignature:
                    {
                        PointerSignature sig = typeHandle.ToPointerSignatureHandle(reader).GetPointerSignature(reader);
                        RuntimeType targetType = reflectionDomain.TryResolve(reader, sig.Type, typeContext, ref exception);
                        if (targetType == null)
                            return null;
                        return ReflectionCoreNonPortable.GetPointerType(targetType);
                    }

                case HandleType.SZArraySignature:
                    {
                        SZArraySignature sig = typeHandle.ToSZArraySignatureHandle(reader).GetSZArraySignature(reader);
                        RuntimeType elementType = reflectionDomain.TryResolve(reader, sig.ElementType, typeContext, ref exception);
                        if (elementType == null)
                            return null;
                        return ReflectionCoreNonPortable.GetArrayType(elementType);
                    }

                case HandleType.TypeDefinition:
                    {
                        return reflectionDomain.ResolveTypeDefinition(reader, typeHandle.ToTypeDefinitionHandle(reader));
                    }

                case HandleType.TypeInstantiationSignature:
                    {
                        TypeInstantiationSignature sig = typeHandle.ToTypeInstantiationSignatureHandle(reader).GetTypeInstantiationSignature(reader);
                        RuntimeType genericTypeDefinition = reflectionDomain.TryResolve(reader, sig.GenericType, typeContext, ref exception);
                        if (genericTypeDefinition == null)
                            return null;
                        LowLevelList<RuntimeType> genericTypeArguments = new LowLevelList<RuntimeType>();
                        foreach (Handle genericTypeArgumentHandle in sig.GenericTypeArguments)
                        {
                            RuntimeType genericTypeArgument = reflectionDomain.TryResolve(reader, genericTypeArgumentHandle, typeContext, ref exception);
                            if (genericTypeArgument == null)
                                return null;
                            genericTypeArguments.Add(genericTypeArgument);
                        }
                        return ReflectionCoreNonPortable.GetConstructedGenericType(genericTypeDefinition, genericTypeArguments.ToArray());
                    }

                case HandleType.TypeReference:
                    {
                        return reflectionDomain.TryResolveTypeReference(reader, typeHandle.ToTypeReferenceHandle(reader), ref exception);
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
        private static RuntimeType TryResolveTypeReference(this ReflectionDomain reflectionDomain, MetadataReader reader, TypeReferenceHandle typeReferenceHandle, ref Exception exception)
        {
            {
                ExecutionDomain executionDomain = reflectionDomain as ExecutionDomain;
                if (executionDomain != null)
                {
                    RuntimeTypeHandle resolvedRuntimeTypeHandle;
                    if (executionDomain.ExecutionEnvironment.TryGetNamedTypeForTypeReference(reader, typeReferenceHandle, out resolvedRuntimeTypeHandle))
                        return ReflectionCoreNonPortable.GetTypeForRuntimeTypeHandle(resolvedRuntimeTypeHandle);
                }
            }

            TypeReference typeReference = typeReferenceHandle.GetTypeReference(reader);
            String name = typeReference.TypeName.GetString(reader);
            Handle parent = typeReference.ParentNamespaceOrType;
            HandleType parentType = parent.HandleType;
            TypeInfo outerTypeInfo = null;

            // Check if this is a reference to a nested type.

            if (parentType == HandleType.TypeDefinition)
            {
                outerTypeInfo = RuntimeNamedTypeInfo.GetRuntimeNamedTypeInfo(reader, parent.ToTypeDefinitionHandle(reader));
            }
            else if (parentType == HandleType.TypeReference)
            {
                RuntimeType outerType = reflectionDomain.TryResolveTypeReference(reader, parent.ToTypeReferenceHandle(reader), ref exception);
                if (outerType == null)
                    return null;
                outerTypeInfo = outerType.GetTypeInfo();   // Since we got to outerType via a metadata reference, we're assured GetTypeInfo() won't throw a MissingMetadataException.
            }
            if (outerTypeInfo != null)
            {
                // It was a nested type. We've already resolved the containing type recursively - just find the nested among its direct children.
                TypeInfo resolvedTypeInfo = outerTypeInfo.GetDeclaredNestedType(name);
                if (resolvedTypeInfo == null)
                {
                    exception = reflectionDomain.CreateMissingMetadataException(outerTypeInfo, name);
                    return null;
                }
                return (RuntimeType)(resolvedTypeInfo.AsType());
            }


            // If we got here, the typeReference was to a non-nested type. 
            if (parentType == HandleType.NamespaceReference)
            {
                AssemblyQualifiedTypeName assemblyQualifiedTypeName = parent.ToNamespaceReferenceHandle(reader).ToAssemblyQualifiedTypeName(name, reader);
                RuntimeType runtimeType;
                exception = assemblyQualifiedTypeName.TryResolve(reflectionDomain, null, /*ignoreCase: */false, out runtimeType);
                if (exception != null)
                    return null;
                return runtimeType;
            }

            throw new BadImageFormatException(); // Expected TypeReference parent to be typeRef, typeDef or namespaceRef.
        }
    }
}

