// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.TypeInfos;

using Internal.Metadata.NativeFormat;
using Internal.Reflection.Core.NonPortable;

namespace System.Reflection.Runtime.General
{
    //
    // The main access point for all Type constructions. RuntimeTypeInfo's are interned using weak pointers. This ensures that 
    // TypeInfo's can be compared for "semantic equality" using reference equality.
    //
    internal static class TypeUnifier
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RuntimeNamedTypeInfo GetNamedType(this TypeDefinitionHandle typeDefinitionHandle, MetadataReader reader)
        {
            return typeDefinitionHandle.GetNamedType(reader, default(RuntimeTypeHandle));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RuntimeTypeInfo GetArrayType(this RuntimeTypeInfo elementType)
        {
            return elementType.GetArrayType(default(RuntimeTypeHandle));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RuntimeTypeInfo GetMultiDimArrayType(this RuntimeTypeInfo elementType, int rank)
        {
            return elementType.GetMultiDimArrayType(rank, default(RuntimeTypeHandle));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RuntimeTypeInfo GetByRefType(this RuntimeTypeInfo targetType)
        {
            return RuntimeByRefTypeInfo.GetByRefTypeInfo(targetType);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RuntimeTypeInfo GetPointerType(this RuntimeTypeInfo targetType)
        {
            return targetType.GetPointerType(default(RuntimeTypeHandle));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RuntimeTypeInfo GetConstructedGenericType(this RuntimeTypeInfo genericTypeDefinition, RuntimeTypeInfo[] genericTypeArguments)
        {
            return genericTypeDefinition.GetConstructedGenericType(genericTypeArguments, default(RuntimeTypeHandle));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RuntimeTypeInfo GetTypeForRuntimeTypeHandle(this RuntimeTypeHandle typeHandle)
        {
            Type type = Type.GetTypeFromHandle(typeHandle);
            return type.GetRuntimeTypeInfo<RuntimeTypeInfo>();
        }

        //======================================================================================================
        // This next group services the Type.GetTypeFromHandle() path. Since we already have a RuntimeTypeHandle
        // in that case, we pass it in as an extra argument as an optimization (otherwise, the unifier will 
        // waste cycles looking up the handle again from the mapping tables.)
        //======================================================================================================
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RuntimeNamedTypeInfo GetNamedType(this TypeDefinitionHandle typeDefinitionHandle, MetadataReader reader, RuntimeTypeHandle precomputedTypeHandle)
        {
            return RuntimeNamedTypeInfo.GetRuntimeNamedTypeInfo(reader, typeDefinitionHandle, precomputedTypeHandle: precomputedTypeHandle);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RuntimeTypeInfo GetArrayType(this RuntimeTypeInfo elementType, RuntimeTypeHandle precomputedTypeHandle)
        {
            return RuntimeArrayTypeInfo.GetArrayTypeInfo(elementType, multiDim: false, rank: 1, precomputedTypeHandle: precomputedTypeHandle);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RuntimeTypeInfo GetMultiDimArrayType(this RuntimeTypeInfo elementType, int rank, RuntimeTypeHandle precomputedTypeHandle)
        {
            return RuntimeArrayTypeInfo.GetArrayTypeInfo(elementType, multiDim: true, rank: rank, precomputedTypeHandle: precomputedTypeHandle);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RuntimeTypeInfo GetPointerType(this RuntimeTypeInfo targetType, RuntimeTypeHandle precomputedTypeHandle)
        {
            return RuntimePointerTypeInfo.GetPointerTypeInfo(targetType, precomputedTypeHandle);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RuntimeTypeInfo GetConstructedGenericType(this RuntimeTypeInfo genericTypeDefinition, RuntimeTypeInfo[] genericTypeArguments, RuntimeTypeHandle precomputedTypeHandle)
        {
            return RuntimeConstructedGenericTypeInfo.GetRuntimeConstructedGenericTypeInfo(genericTypeDefinition, genericTypeArguments, precomputedTypeHandle);
        }
    }
}
