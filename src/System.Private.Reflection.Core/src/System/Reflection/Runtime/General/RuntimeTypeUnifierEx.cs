// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using global::System;
using global::System.Collections.Generic;
using global::System.Collections.Concurrent;
using global::System.Runtime.CompilerServices;
using global::System.Reflection.Runtime.Types;
using global::System.Reflection.Runtime.TypeInfos;
using global::System.Reflection.Runtime.MethodInfos;

using global::Internal.Reflection.Core.Execution;
using global::Internal.Reflection.Core.NonPortable;

using global::Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.General
{
    //
    // ! If you change this policy to not unify all instances, you must change the implementation of Equals/GetHashCode in the runtime type classes.
    //
    // The RuntimeTypeUnifier and its companion RuntimeTypeUnifierEx maintains a record of all System.Type objects 
    // created by the runtime. The split into two classes is an artifact of reflection being implemented partly in System.Private.CoreLib and
    // partly in S.R.R. 
    //
    // Though the present incarnation enforces the "one instance per semantic identity rule", its surface area is also designed
    // to be able to switch to a non-unified model if desired.
    //
    // ! If you do switch away from a "one instance per semantic identity rule", you must also change the implementation
    // ! of RuntimeType.Equals() and RuntimeType.GetHashCode().
    //

    internal static class RuntimeTypeUnifierEx
    {
        //
        // Retrieves the unified Type object for a named type that has metadata associated with it.
        //
        public static RuntimeType GetNamedType(MetadataReader reader, TypeDefinitionHandle typeDefinitionHandle)
        {
            return TypeTableForNamedTypes.Table.GetOrAdd(new QTypeDefinition(reader, typeDefinitionHandle)).WithDebugName();
        }

        //
        // Type table for all named types that have metadata associated with it.
        //
        private sealed class TypeTableForNamedTypes : ConcurrentUnifierW<QTypeDefinition, RuntimeType>
        {
            private TypeTableForNamedTypes() { }

            protected sealed override RuntimeType Factory(QTypeDefinition qTypeDefinition)
            {
                RuntimeTypeHandle runtimeTypeHandle;
                if (ReflectionCoreExecution.ExecutionEnvironment.TryGetNamedTypeForMetadata(qTypeDefinition.Reader, qTypeDefinition.Handle, out runtimeTypeHandle))
                    return ReflectionCoreNonPortable.GetTypeForRuntimeTypeHandle(runtimeTypeHandle);
                else
                    return RuntimeInspectionOnlyNamedType.GetRuntimeInspectionOnlyNamedType(qTypeDefinition.Reader, qTypeDefinition.Handle);
            }

            public static TypeTableForNamedTypes Table = new TypeTableForNamedTypes();
        }



        //
        // Retrieves the unified Type object for a generic type parameter type.
        //
        internal static RuntimeType GetRuntimeGenericParameterTypeForTypes(RuntimeNamedTypeInfo typeOwner, GenericParameterHandle genericParameterHandle)
        {
            RuntimeGenericParameterTypeForTypes.UnificationKey key = new RuntimeGenericParameterTypeForTypes.UnificationKey(typeOwner.Reader, typeOwner.TypeDefinitionHandle, genericParameterHandle);
            return GenericParameterTypeForTypesTable.Table.GetOrAdd(key).WithDebugName();
        }

        private sealed class GenericParameterTypeForTypesTable : ConcurrentUnifierW<RuntimeGenericParameterTypeForTypes.UnificationKey, RuntimeGenericParameterTypeForTypes>
        {
            protected sealed override RuntimeGenericParameterTypeForTypes Factory(RuntimeGenericParameterTypeForTypes.UnificationKey key)
            {
                RuntimeNamedTypeInfo typeOwner = RuntimeNamedTypeInfo.GetRuntimeNamedTypeInfo(key.Reader, key.TypeDefinitionHandle);
                return new RuntimeGenericParameterTypeForTypes(key.Reader, key.GenericParameterHandle, typeOwner);
            }

            public static GenericParameterTypeForTypesTable Table = new GenericParameterTypeForTypesTable();
        }


        //
        // Retrieves the unified Type object for a generic method parameter type.
        //
        internal static RuntimeGenericParameterTypeForMethods GetRuntimeGenericParameterTypeForMethods(RuntimeNamedMethodInfo methodOwner, MetadataReader reader, GenericParameterHandle genericParameterHandle)
        {
            RuntimeGenericParameterTypeForMethods.UnificationKey key = new RuntimeGenericParameterTypeForMethods.UnificationKey(methodOwner, reader, genericParameterHandle);
            return GenericParameterTypeForMethodsTable.Table.GetOrAdd(key);
        }

        private sealed class GenericParameterTypeForMethodsTable : ConcurrentUnifierWKeyed<RuntimeGenericParameterTypeForMethods.UnificationKey, RuntimeGenericParameterTypeForMethods>
        {
            protected sealed override RuntimeGenericParameterTypeForMethods Factory(RuntimeGenericParameterTypeForMethods.UnificationKey key)
            {
                return new RuntimeGenericParameterTypeForMethods(key.Reader, key.GenericParameterHandle, key.MethodOwner);
            }

            public static GenericParameterTypeForMethodsTable Table = new GenericParameterTypeForMethodsTable();
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static RuntimeType WithDebugName(this RuntimeType runtimeType)
        {
#if DEBUG
            if (runtimeType != null)
                runtimeType.EstablishDebugName();
#endif
            return runtimeType;
        }
    }
}
