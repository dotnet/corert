// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.TypeInfos;
using System.Reflection.Runtime.TypeInfos.EcmaFormat;
using System.Reflection.Runtime.MethodInfos;

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
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
    internal static partial class TypeUnifier
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RuntimeTypeDefinitionTypeInfo GetNamedType(this TypeDefinitionHandle typeDefinitionHandle, MetadataReader reader)
        {
            return typeDefinitionHandle.GetNamedType(reader, default(RuntimeTypeHandle));
        }

        //======================================================================================================
        // This next group services the Type.GetTypeFromHandle() path. Since we already have a RuntimeTypeHandle
        // in that case, we pass it in as an extra argument as an optimization (otherwise, the unifier will 
        // waste cycles looking up the handle again from the mapping tables.)
        //======================================================================================================
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RuntimeTypeDefinitionTypeInfo GetNamedType(this TypeDefinitionHandle typeDefinitionHandle, MetadataReader reader, RuntimeTypeHandle precomputedTypeHandle)
        {
            return EcmaFormatRuntimeNamedTypeInfo.GetRuntimeNamedTypeInfo(reader, typeDefinitionHandle, precomputedTypeHandle: precomputedTypeHandle);
        }
    }
}

namespace System.Reflection.Runtime.TypeInfos.EcmaFormat
{
    //-----------------------------------------------------------------------------------------------------------
    // TypeInfos for type definitions (i.e. "Foo" and "Foo<>" but not "Foo<int>")
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class EcmaFormatRuntimeNamedTypeInfo : RuntimeNamedTypeInfo
    {
        internal static EcmaFormatRuntimeNamedTypeInfo GetRuntimeNamedTypeInfo(MetadataReader metadataReader, TypeDefinitionHandle typeDefHandle, RuntimeTypeHandle precomputedTypeHandle)
        {
            RuntimeTypeHandle typeHandle = precomputedTypeHandle;
            if (typeHandle.IsNull())
            {
                if (!ReflectionCoreExecution.ExecutionEnvironment.TryGetNamedTypeForMetadata(new QTypeDefinition(metadataReader, typeDefHandle), out typeHandle))
                    typeHandle = default(RuntimeTypeHandle);
            }
            UnificationKey key = new UnificationKey(metadataReader, typeDefHandle, typeHandle);

            EcmaFormatRuntimeNamedTypeInfo type = NamedTypeTable.Table.GetOrAdd(key);
            type.EstablishDebugName();
            return type;
        }

        private sealed class NamedTypeTable : ConcurrentUnifierW<UnificationKey, EcmaFormatRuntimeNamedTypeInfo>
        {
            protected sealed override EcmaFormatRuntimeNamedTypeInfo Factory(UnificationKey key)
            {
                return new EcmaFormatRuntimeNamedTypeInfo(key.Reader, key.TypeDefinitionHandle, key.TypeHandle);
            }

            public static readonly NamedTypeTable Table = new NamedTypeTable();
        }
    }

    //-----------------------------------------------------------------------------------------------------------
    // TypeInfos for generic parameters on types.
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class EcmaFormatRuntimeGenericParameterTypeInfoForTypes : EcmaFormatRuntimeGenericParameterTypeInfo
    {
        //
        // For app-compat reasons, we need to make sure that only TypeInfo instance exists for a given semantic type. If you change this, you must change the way
        // RuntimeTypeInfo.Equals() is implemented.
        // 
        internal static EcmaFormatRuntimeGenericParameterTypeInfoForTypes GetRuntimeGenericParameterTypeInfoForTypes(EcmaFormatRuntimeNamedTypeInfo typeOwner, GenericParameterHandle genericParameterHandle)
        {
            UnificationKey key = new UnificationKey(typeOwner.Reader, typeOwner.TypeDefinitionHandle, genericParameterHandle);
            EcmaFormatRuntimeGenericParameterTypeInfoForTypes type = GenericParameterTypeForTypesTable.Table.GetOrAdd(key);
            type.EstablishDebugName();
            return type;
        }

        private sealed class GenericParameterTypeForTypesTable : ConcurrentUnifierW<UnificationKey, EcmaFormatRuntimeGenericParameterTypeInfoForTypes>
        {
            protected sealed override EcmaFormatRuntimeGenericParameterTypeInfoForTypes Factory(UnificationKey key)
            {
                RuntimeTypeDefinitionTypeInfo typeOwner = key.TypeDefinitionHandle.GetNamedType(key.Reader);
                return new EcmaFormatRuntimeGenericParameterTypeInfoForTypes(key.Reader, key.GenericParameterHandle, typeOwner);
            }

            public static readonly GenericParameterTypeForTypesTable Table = new GenericParameterTypeForTypesTable();
        }
    }

    //-----------------------------------------------------------------------------------------------------------
    // TypeInfos for generic parameters on methods.
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class EcmaFormatRuntimeGenericParameterTypeInfoForMethods : EcmaFormatRuntimeGenericParameterTypeInfo, IKeyedItem<EcmaFormatRuntimeGenericParameterTypeInfoForMethods.UnificationKey>
    {
        //
        // For app-compat reasons, we need to make sure that only TypeInfo instance exists for a given semantic type. If you change this, you must change the way
        // RuntimeTypeInfo.Equals() is implemented.
        // 
        internal static EcmaFormatRuntimeGenericParameterTypeInfoForMethods GetRuntimeGenericParameterTypeInfoForMethods(RuntimeNamedMethodInfo methodOwner, MetadataReader reader, GenericParameterHandle genericParameterHandle)
        {
            UnificationKey key = new UnificationKey(methodOwner, reader, genericParameterHandle);
            EcmaFormatRuntimeGenericParameterTypeInfoForMethods type = GenericParameterTypeForMethodsTable.Table.GetOrAdd(key);
            type.EstablishDebugName();
            return type;
        }

        private sealed class GenericParameterTypeForMethodsTable : ConcurrentUnifierWKeyed<UnificationKey, EcmaFormatRuntimeGenericParameterTypeInfoForMethods>
        {
            protected sealed override EcmaFormatRuntimeGenericParameterTypeInfoForMethods Factory(UnificationKey key)
            {
                return new EcmaFormatRuntimeGenericParameterTypeInfoForMethods(key.Reader, key.GenericParameterHandle, key.MethodOwner);
            }

            public static readonly GenericParameterTypeForMethodsTable Table = new GenericParameterTypeForMethodsTable();
        }
    }
}
