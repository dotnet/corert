// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.TypeInfos;
using System.Reflection.Runtime.MethodInfos;

using Internal.Metadata.NativeFormat;
using Internal.Reflection.Core.Execution;

// 
// It is common practice for app code to compare Type objects using reference equality with the expectation that reference equality
// is equivalent to semantic equality. To support this, all RuntimeTypeObject objects are interned using weak references.
// 
// This assumption is baked into the codebase in these places:
//
//   - RuntimeTypeInfo.Equals(object) implements itself as Object.ReferenceEquals(this, obj)
//
//   - RuntimeTypeInfo.GetHashCode() is implemented in a flavor-specific manner (We can't use Object.GetHashCode()
//     because we don't want the hash value to change if a type is collected and resurrected later.)
//
// This assumption is actualized as follows:
//
//   - RuntimeTypeInfo classes hide their constructor. The only way to instantiate a RuntimeTypeInfo
//     is through its public static factory method which ensures the interning and are collected in this one
//     file for easy auditing and to help ensure that they all operate in a consistent manner.
//
//   - The TypeUnifier extension class provides a more friendly interface to the rest of the codebase.
// 

namespace System.Reflection.Runtime.General
{
    internal static class TypeUnifier
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RuntimeTypeInfo GetNamedType(this TypeDefinitionHandle typeDefinitionHandle, MetadataReader reader)
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
            return type.CastToRuntimeTypeInfo();
        }

        //======================================================================================================
        // This next group services the Type.GetTypeFromHandle() path. Since we already have a RuntimeTypeHandle
        // in that case, we pass it in as an extra argument as an optimization (otherwise, the unifier will 
        // waste cycles looking up the handle again from the mapping tables.)
        //======================================================================================================
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RuntimeTypeInfo GetNamedType(this TypeDefinitionHandle typeDefinitionHandle, MetadataReader reader, RuntimeTypeHandle precomputedTypeHandle)
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

namespace System.Reflection.Runtime.TypeInfos
{
    //-----------------------------------------------------------------------------------------------------------
    // TypeInfos for type definitions (i.e. "Foo" and "Foo<>" but not "Foo<int>")
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimeNamedTypeInfo : RuntimeTypeInfo
    {
        internal static RuntimeNamedTypeInfo GetRuntimeNamedTypeInfo(MetadataReader metadataReader, TypeDefinitionHandle typeDefHandle, RuntimeTypeHandle precomputedTypeHandle)
        {
            RuntimeTypeHandle typeHandle = precomputedTypeHandle;
            if (typeHandle.IsNull())
            {
                if (!ReflectionCoreExecution.ExecutionEnvironment.TryGetNamedTypeForMetadata(metadataReader, typeDefHandle, out typeHandle))
                    typeHandle = default(RuntimeTypeHandle);
            }
            UnificationKey key = new UnificationKey(metadataReader, typeDefHandle, typeHandle);

            RuntimeNamedTypeInfo type = NamedTypeTable.Table.GetOrAdd(key);
            type.EstablishDebugName();
            return type;
        }

        private sealed class NamedTypeTable : ConcurrentUnifierW<UnificationKey, RuntimeNamedTypeInfo>
        {
            protected sealed override RuntimeNamedTypeInfo Factory(UnificationKey key)
            {
                return new RuntimeNamedTypeInfo(key.Reader, key.TypeDefinitionHandle, key.TypeHandle);
            }

            public static readonly NamedTypeTable Table = new NamedTypeTable();
        }
    }

    //-----------------------------------------------------------------------------------------------------------
    // TypeInfos for type definitions (i.e. "Foo" and "Foo<>" but not "Foo<int>") that aren't opted into metadata.
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimeNoMetadataNamedTypeInfo : RuntimeTypeInfo
    {
        internal static RuntimeNoMetadataNamedTypeInfo GetRuntimeNoMetadataNamedTypeInfo(RuntimeTypeHandle typeHandle, bool isGenericTypeDefinition)
        {
            RuntimeNoMetadataNamedTypeInfo type;
            if (isGenericTypeDefinition)
                type = GenericNoMetadataNamedTypeTable.Table.GetOrAdd(new RuntimeTypeHandleKey(typeHandle));
            else
                type = NoMetadataNamedTypeTable.Table.GetOrAdd(new RuntimeTypeHandleKey(typeHandle));
            type.EstablishDebugName();
            return type;
        }

        private sealed class NoMetadataNamedTypeTable : ConcurrentUnifierW<RuntimeTypeHandleKey, RuntimeNoMetadataNamedTypeInfo>
        {
            protected sealed override RuntimeNoMetadataNamedTypeInfo Factory(RuntimeTypeHandleKey key)
            {
                return new RuntimeNoMetadataNamedTypeInfo(key.TypeHandle, isGenericTypeDefinition: false);
            }

            public static readonly NoMetadataNamedTypeTable Table = new NoMetadataNamedTypeTable();
        }

        private sealed class GenericNoMetadataNamedTypeTable : ConcurrentUnifierW<RuntimeTypeHandleKey, RuntimeNoMetadataNamedTypeInfo>
        {
            protected sealed override RuntimeNoMetadataNamedTypeInfo Factory(RuntimeTypeHandleKey key)
            {
                return new RuntimeNoMetadataNamedTypeInfo(key.TypeHandle, isGenericTypeDefinition: true);
            }

            public static readonly GenericNoMetadataNamedTypeTable Table = new GenericNoMetadataNamedTypeTable();
        }
    }

    //-----------------------------------------------------------------------------------------------------------
    // TypeInfos that represent type definitions (i.e. Foo or Foo<>) or constructed generic types (Foo<int>)
    // that can never be reflection-enabled due to the framework Reflection block.
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimeBlockedTypeInfo : RuntimeTypeInfo
    {
        internal static RuntimeBlockedTypeInfo GetRuntimeBlockedTypeInfo(RuntimeTypeHandle typeHandle, bool isGenericTypeDefinition)
        {
            RuntimeBlockedTypeInfo type;
            if (isGenericTypeDefinition)
                type = GenericBlockedTypeTable.Table.GetOrAdd(new RuntimeTypeHandleKey(typeHandle));
            else
                type = BlockedTypeTable.Table.GetOrAdd(new RuntimeTypeHandleKey(typeHandle));
            type.EstablishDebugName();
            return type;
        }

        private sealed class BlockedTypeTable : ConcurrentUnifierW<RuntimeTypeHandleKey, RuntimeBlockedTypeInfo>
        {
            protected sealed override RuntimeBlockedTypeInfo Factory(RuntimeTypeHandleKey key)
            {
                return new RuntimeBlockedTypeInfo(key.TypeHandle, isGenericTypeDefinition: false);
            }

            public static readonly BlockedTypeTable Table = new BlockedTypeTable();
        }

        private sealed class GenericBlockedTypeTable : ConcurrentUnifierW<RuntimeTypeHandleKey, RuntimeBlockedTypeInfo>
        {
            protected sealed override RuntimeBlockedTypeInfo Factory(RuntimeTypeHandleKey key)
            {
                return new RuntimeBlockedTypeInfo(key.TypeHandle, isGenericTypeDefinition: true);
            }

            public static readonly GenericBlockedTypeTable Table = new GenericBlockedTypeTable();
        }
    }

    //-----------------------------------------------------------------------------------------------------------
    // TypeInfos for Sz and multi-dim Array types.
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimeArrayTypeInfo : RuntimeHasElementTypeInfo
    {
        internal static RuntimeArrayTypeInfo GetArrayTypeInfo(RuntimeTypeInfo elementType, bool multiDim, int rank, RuntimeTypeHandle precomputedTypeHandle)
        {
            Debug.Assert(multiDim || rank == 1);

            RuntimeTypeHandle typeHandle = precomputedTypeHandle.IsNull() ? GetRuntimeTypeHandleIfAny(elementType, multiDim, rank) : precomputedTypeHandle;
            UnificationKey key = new UnificationKey(elementType, typeHandle);
            RuntimeArrayTypeInfo type;
            if (!multiDim)
                type = ArrayTypeTable.Table.GetOrAdd(key);
            else
                type = TypeTableForMultiDimArrayTypeTables.Table.GetOrAdd(rank).GetOrAdd(key);
            type.EstablishDebugName();
            return type;
        }

        private static RuntimeTypeHandle GetRuntimeTypeHandleIfAny(RuntimeTypeInfo elementType, bool multiDim, int rank)
        {
            Debug.Assert(multiDim || rank == 1);

            RuntimeTypeHandle elementTypeHandle = elementType.InternalTypeHandleIfAvailable;
            if (elementTypeHandle.IsNull())
                return default(RuntimeTypeHandle);

            RuntimeTypeHandle typeHandle;
            if (!multiDim)
            {
                if (!ReflectionCoreExecution.ExecutionEnvironment.TryGetArrayTypeForElementType(elementTypeHandle, out typeHandle))
                    return default(RuntimeTypeHandle);
            }
            else
            {
                if (!ReflectionCoreExecution.ExecutionEnvironment.TryGetMultiDimArrayTypeForElementType(elementTypeHandle, rank, out typeHandle))
                    return default(RuntimeTypeHandle);
            }

            return typeHandle;
        }

        private sealed class ArrayTypeTable : ConcurrentUnifierWKeyed<UnificationKey, RuntimeArrayTypeInfo>
        {
            protected sealed override RuntimeArrayTypeInfo Factory(UnificationKey key)
            {
                ValidateElementType(key.ElementType, key.TypeHandle, multiDim: false, rank: 1);

                return new RuntimeArrayTypeInfo(key, multiDim: false, rank: 1);
            }

            public static readonly ArrayTypeTable Table = new ArrayTypeTable();
        }

        private sealed class MultiDimArrayTypeTable : ConcurrentUnifierWKeyed<UnificationKey, RuntimeArrayTypeInfo>
        {
            public MultiDimArrayTypeTable(int rank)
            {
                _rank = rank;
            }

            protected sealed override RuntimeArrayTypeInfo Factory(UnificationKey key)
            {
                ValidateElementType(key.ElementType, key.TypeHandle, multiDim: true, rank: _rank);

                return new RuntimeArrayTypeInfo(key, multiDim: true, rank: _rank);
            }

            private readonly int _rank;
        }

        //
        // For the hopefully rare case of multidim arrays, we have a dictionary of dictionaries.
        //
        private sealed class TypeTableForMultiDimArrayTypeTables : ConcurrentUnifier<int, MultiDimArrayTypeTable>
        {
            protected sealed override MultiDimArrayTypeTable Factory(int rank)
            {
                Debug.Assert(rank > 0);
                return new MultiDimArrayTypeTable(rank);
            }

            public static readonly TypeTableForMultiDimArrayTypeTables Table = new TypeTableForMultiDimArrayTypeTables();
        }

        private static void ValidateElementType(RuntimeTypeInfo elementType, RuntimeTypeHandle typeHandle, bool multiDim, int rank)
        {
            Debug.Assert(multiDim || rank == 1);

            if (elementType.IsByRef)
                throw new TypeLoadException(SR.Format(SR.ArgumentException_InvalidArrayElementType, elementType));
            if (elementType.IsGenericTypeDefinition)
                throw new ArgumentException(SR.Format(SR.ArgumentException_InvalidArrayElementType, elementType));

            // We only permit creating parameterized types if the pay-for-play policy specifically allows them *or* if the result
            // type would be an open type.
            if (typeHandle.IsNull() && !elementType.ContainsGenericParameters)
                throw ReflectionCoreExecution.ExecutionDomain.CreateMissingArrayTypeException(elementType.AsType(), multiDim, rank);
        }
    }

    //-----------------------------------------------------------------------------------------------------------
    // TypeInfos for ByRef types.
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimeByRefTypeInfo : RuntimeHasElementTypeInfo
    {
        internal static RuntimeByRefTypeInfo GetByRefTypeInfo(RuntimeTypeInfo elementType)
        {
            RuntimeTypeHandle typeHandle = default(RuntimeTypeHandle);
            RuntimeByRefTypeInfo type = ByRefTypeTable.Table.GetOrAdd(new UnificationKey(elementType, typeHandle));
            type.EstablishDebugName();
            return type;
        }

        private sealed class ByRefTypeTable : ConcurrentUnifierWKeyed<UnificationKey, RuntimeByRefTypeInfo>
        {
            protected sealed override RuntimeByRefTypeInfo Factory(UnificationKey key)
            {
                return new RuntimeByRefTypeInfo(key);
            }

            public static readonly ByRefTypeTable Table = new ByRefTypeTable();
        }
    }

    //-----------------------------------------------------------------------------------------------------------
    // TypeInfos for Pointer types.
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimePointerTypeInfo : RuntimeHasElementTypeInfo
    {
        internal static RuntimePointerTypeInfo GetPointerTypeInfo(RuntimeTypeInfo elementType, RuntimeTypeHandle precomputedTypeHandle)
        {
            RuntimeTypeHandle typeHandle = precomputedTypeHandle.IsNull() ? GetRuntimeTypeHandleIfAny(elementType) : precomputedTypeHandle;
            RuntimePointerTypeInfo type = PointerTypeTable.Table.GetOrAdd(new UnificationKey(elementType, typeHandle));
            type.EstablishDebugName();
            return type;
        }

        private static RuntimeTypeHandle GetRuntimeTypeHandleIfAny(RuntimeTypeInfo elementType)
        {
            RuntimeTypeHandle elementTypeHandle = elementType.InternalTypeHandleIfAvailable;
            if (elementTypeHandle.IsNull())
                return default(RuntimeTypeHandle);

            RuntimeTypeHandle typeHandle;
            if (!ReflectionCoreExecution.ExecutionEnvironment.TryGetPointerTypeForTargetType(elementTypeHandle, out typeHandle))
                return default(RuntimeTypeHandle);

            return typeHandle;
        }

        private sealed class PointerTypeTable : ConcurrentUnifierWKeyed<UnificationKey, RuntimePointerTypeInfo>
        {
            protected sealed override RuntimePointerTypeInfo Factory(UnificationKey key)
            {
                return new RuntimePointerTypeInfo(key);
            }

            public static readonly PointerTypeTable Table = new PointerTypeTable();
        }
    }

    //-----------------------------------------------------------------------------------------------------------
    // TypeInfos for Constructed generic types ("Foo<int>")
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimeConstructedGenericTypeInfo : RuntimeTypeInfo, IKeyedItem<RuntimeConstructedGenericTypeInfo.UnificationKey>
    {
        internal static RuntimeConstructedGenericTypeInfo GetRuntimeConstructedGenericTypeInfo(RuntimeTypeInfo genericTypeDefinition, RuntimeTypeInfo[] genericTypeArguments, RuntimeTypeHandle precomputedTypeHandle)
        {
            RuntimeTypeHandle typeHandle = precomputedTypeHandle.IsNull() ? GetRuntimeTypeHandleIfAny(genericTypeDefinition, genericTypeArguments) : precomputedTypeHandle;
            UnificationKey key = new UnificationKey(genericTypeDefinition, genericTypeArguments, typeHandle);
            RuntimeConstructedGenericTypeInfo typeInfo = ConstructedGenericTypeTable.Table.GetOrAdd(key);
            typeInfo.EstablishDebugName();
            return typeInfo;
        }

        private static RuntimeTypeHandle GetRuntimeTypeHandleIfAny(RuntimeTypeInfo genericTypeDefinition, RuntimeTypeInfo[] genericTypeArguments)
        {
            RuntimeTypeHandle genericTypeDefinitionHandle = genericTypeDefinition.InternalTypeHandleIfAvailable;
            if (genericTypeDefinitionHandle.IsNull())
                return default(RuntimeTypeHandle);

            if (ReflectionCoreExecution.ExecutionEnvironment.IsReflectionBlocked(genericTypeDefinitionHandle))
                return default(RuntimeTypeHandle);

            int count = genericTypeArguments.Length;
            RuntimeTypeHandle[] genericTypeArgumentHandles = new RuntimeTypeHandle[count];
            for (int i = 0; i < count; i++)
            {
                RuntimeTypeHandle genericTypeArgumentHandle = genericTypeArguments[i].InternalTypeHandleIfAvailable;
                if (genericTypeArgumentHandle.IsNull())
                    return default(RuntimeTypeHandle);
                genericTypeArgumentHandles[i] = genericTypeArgumentHandle;
            }

            RuntimeTypeHandle typeHandle;
            if (!ReflectionCoreExecution.ExecutionEnvironment.TryGetConstructedGenericTypeForComponents(genericTypeDefinitionHandle, genericTypeArgumentHandles, out typeHandle))
                return default(RuntimeTypeHandle);

            return typeHandle;
        }

        private sealed class ConstructedGenericTypeTable : ConcurrentUnifierWKeyed<UnificationKey, RuntimeConstructedGenericTypeInfo>
        {
            protected sealed override RuntimeConstructedGenericTypeInfo Factory(UnificationKey key)
            {
                bool atLeastOneOpenType = false;
                foreach (RuntimeTypeInfo genericTypeArgument in key.GenericTypeArguments)
                {
                    if (genericTypeArgument.IsByRef || genericTypeArgument.IsGenericTypeDefinition)
                        throw new ArgumentException(SR.Format(SR.ArgumentException_InvalidTypeArgument, genericTypeArgument));
                    if (genericTypeArgument.ContainsGenericParameters)
                        atLeastOneOpenType = true;
                }

                // We only permit creating parameterized types if the pay-for-play policy specifically allows them *or* if the result
                // type would be an open type.
                if (key.TypeHandle.IsNull() && !atLeastOneOpenType)
                    throw ReflectionCoreExecution.ExecutionDomain.CreateMissingConstructedGenericTypeException(key.GenericTypeDefinition.AsType(), key.GenericTypeArguments.CloneTypeArray());

                return new RuntimeConstructedGenericTypeInfo(key);
            }

            public static readonly ConstructedGenericTypeTable Table = new ConstructedGenericTypeTable();
        }
    }

    //-----------------------------------------------------------------------------------------------------------
    // TypeInfos for generic parameters on types.
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimeGenericParameterTypeInfoForTypes : RuntimeGenericParameterTypeInfo
    {
        //
        // For app-compat reasons, we need to make sure that only TypeInfo instance exists for a given semantic type. If you change this, you must change the way
        // RuntimeTypeInfo.Equals() is implemented.
        // 
        internal static RuntimeGenericParameterTypeInfoForTypes GetRuntimeGenericParameterTypeInfoForTypes(RuntimeNamedTypeInfo typeOwner, GenericParameterHandle genericParameterHandle)
        {
            UnificationKey key = new UnificationKey(typeOwner.Reader, typeOwner.TypeDefinitionHandle, genericParameterHandle);
            RuntimeGenericParameterTypeInfoForTypes type = GenericParameterTypeForTypesTable.Table.GetOrAdd(key);
            type.EstablishDebugName();
            return type;
        }

        private sealed class GenericParameterTypeForTypesTable : ConcurrentUnifierW<UnificationKey, RuntimeGenericParameterTypeInfoForTypes>
        {
            protected sealed override RuntimeGenericParameterTypeInfoForTypes Factory(UnificationKey key)
            {
                RuntimeTypeInfo typeOwner = key.TypeDefinitionHandle.GetNamedType(key.Reader);
                return new RuntimeGenericParameterTypeInfoForTypes(key.Reader, key.GenericParameterHandle, typeOwner);
            }

            public static readonly GenericParameterTypeForTypesTable Table = new GenericParameterTypeForTypesTable();
        }
    }

    //-----------------------------------------------------------------------------------------------------------
    // TypeInfos for generic parameters on methods.
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimeGenericParameterTypeInfoForMethods : RuntimeGenericParameterTypeInfo, IKeyedItem<RuntimeGenericParameterTypeInfoForMethods.UnificationKey>
    {
        //
        // For app-compat reasons, we need to make sure that only TypeInfo instance exists for a given semantic type. If you change this, you must change the way
        // RuntimeTypeInfo.Equals() is implemented.
        // 
        internal static RuntimeGenericParameterTypeInfoForMethods GetRuntimeGenericParameterTypeInfoForMethods(RuntimeNamedMethodInfo methodOwner, MetadataReader reader, GenericParameterHandle genericParameterHandle)
        {
            UnificationKey key = new UnificationKey(methodOwner, reader, genericParameterHandle);
            RuntimeGenericParameterTypeInfoForMethods type = GenericParameterTypeForMethodsTable.Table.GetOrAdd(key);
            type.EstablishDebugName();
            return type;
        }

        private sealed class GenericParameterTypeForMethodsTable : ConcurrentUnifierWKeyed<UnificationKey, RuntimeGenericParameterTypeInfoForMethods>
        {
            protected sealed override RuntimeGenericParameterTypeInfoForMethods Factory(UnificationKey key)
            {
                return new RuntimeGenericParameterTypeInfoForMethods(key.Reader, key.GenericParameterHandle, key.MethodOwner);
            }

            public static readonly GenericParameterTypeForMethodsTable Table = new GenericParameterTypeForMethodsTable();
        }
    }
}

