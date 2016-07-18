// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.IO;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;
using global::System.Collections.Concurrent;

using global::System.Reflection.Runtime.General;
using global::System.Reflection.Runtime.Types;
using global::System.Reflection.Runtime.TypeInfos;
using global::System.Reflection.Runtime.Assemblies;
using global::System.Reflection.Runtime.Dispensers;
using global::System.Reflection.Runtime.MethodInfos;
using global::System.Reflection.Runtime.PropertyInfos;

using global::Internal.Reflection.Core;
using global::Internal.Reflection.Core.Execution;
using global::Internal.Reflection.Core.NonPortable;
using global::Internal.Reflection.Extensibility;

using global::Internal.Metadata.NativeFormat;

//=================================================================================================================
// This file collects the various chokepoints that create the various Runtime*Info objects. This allows
// easy reviewing of the overall caching and unification policy.
//
// The dispenser functions are defined as static members of the associated Info class. This permits us
// to keep the constructors private to ensure that these really are the only ways to obtain these objects.
//=================================================================================================================

namespace System.Reflection.Runtime.Assemblies
{
    //-----------------------------------------------------------------------------------------------------------
    // Assemblies (maps 1-1 with a MetadataReader/ScopeDefinitionHandle.
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimeAssembly : ExtensibleAssembly
    {
        internal static RuntimeAssembly GetRuntimeAssembly(ReflectionDomain reflectionDomain, RuntimeAssemblyName assemblyRefName)
        {
            RuntimeAssembly result;
            Exception assemblyLoadException = TryGetRuntimeAssembly(reflectionDomain, assemblyRefName, out result);
            if (assemblyLoadException != null)
                throw assemblyLoadException;
            return result;
        }

        internal static Exception TryGetRuntimeAssembly(ReflectionDomain reflectionDomain, RuntimeAssemblyName assemblyRefName, out RuntimeAssembly result)
        {
            Debug.Assert(reflectionDomain == ReflectionCoreExecution.ExecutionDomain, "User Reflection Domains not yet implemented.");
            result = _assemblyRefNameToAssemblyDispenser.GetOrAdd(assemblyRefName);
            if (result != null)
                return null;
            else
                return new FileNotFoundException(SR.Format(SR.FileNotFound_AssemblyNotFound, assemblyRefName.FullName));
        }

        private static Dispenser<RuntimeAssemblyName, RuntimeAssembly> _assemblyRefNameToAssemblyDispenser =
            DispenserFactory.CreateDispenser<RuntimeAssemblyName, RuntimeAssembly>(
                DispenserScenario.AssemblyRefName_Assembly,
                delegate (RuntimeAssemblyName assemblyRefName)
                {
                    ReflectionDomain reflectionDomain = ReflectionCoreExecution.ExecutionDomain; //@todo: Need to use the correct reflection domain!
                    AssemblyBinder binder = reflectionDomain.ReflectionDomainSetup.AssemblyBinder;
                    AssemblyName convertedAssemblyRefName = assemblyRefName.ToAssemblyName();
                    MetadataReader reader;
                    ScopeDefinitionHandle scope;
                    Exception exception;
                    IEnumerable<QScopeDefinition> overflowScopes;
                    if (!binder.Bind(convertedAssemblyRefName, out reader, out scope, out overflowScopes, out exception))
                        return null;
                    return GetRuntimeAssembly(reader, scope, overflowScopes, reflectionDomain);
                }
        );


        private static RuntimeAssembly GetRuntimeAssembly(MetadataReader reader, ScopeDefinitionHandle scope, IEnumerable<QScopeDefinition> overflows, ReflectionDomain reflectionDomain)
        {
            return _scopeToAssemblyDispenser.GetOrAdd(new RuntimeAssemblyKey(reader, scope, overflows));
        }

        private static Dispenser<RuntimeAssemblyKey, RuntimeAssembly> _scopeToAssemblyDispenser =
            DispenserFactory.CreateDispenserV<RuntimeAssemblyKey, RuntimeAssembly>(
                DispenserScenario.Scope_Assembly,
                delegate (RuntimeAssemblyKey qScopeDefinition)
                {
                    return new RuntimeAssembly(qScopeDefinition.Reader, qScopeDefinition.Handle, qScopeDefinition.Overflows);
                }
        );

        //-----------------------------------------------------------------------------------------------------------
        // Captures a qualified scope (a reader plus a handle) representing the canonical definition of an assembly,
        // plus a set of "overflow" scopes representing additional pieces of the assembly.
        //-----------------------------------------------------------------------------------------------------------
        private struct RuntimeAssemblyKey : IEquatable<RuntimeAssemblyKey>
        {
            public RuntimeAssemblyKey(MetadataReader reader, ScopeDefinitionHandle handle, IEnumerable<QScopeDefinition> overflows)
            {
                _reader = reader;
                _handle = handle;
                _overflows = overflows;
            }

            public MetadataReader Reader { get { return _reader; } }
            public ScopeDefinitionHandle Handle { get { return _handle; } }
            public IEnumerable<QScopeDefinition> Overflows { get { return _overflows; } }
            public ScopeDefinition ScopeDefinition
            {
                get
                {
                    return _handle.GetScopeDefinition(_reader);
                }
            }

            public override bool Equals(Object obj)
            {
                if (!(obj is RuntimeAssemblyKey))
                    return false;
                return Equals((RuntimeAssemblyKey)obj);
            }


            public bool Equals(RuntimeAssemblyKey other)
            {
                // Equality depends only on the canonical definition of an assembly, not
                // the overflows.
                if (!(this._reader == other._reader))
                    return false;
                if (!(this._handle.Equals(other._handle)))
                    return false;
                return true;
            }

            public override int GetHashCode()
            {
                return _handle.GetHashCode();
            }

            private readonly MetadataReader _reader;
            private readonly ScopeDefinitionHandle _handle;
            private readonly IEnumerable<QScopeDefinition> _overflows;
        }
    }
}

namespace System.Reflection.Runtime.Modules
{
    //-----------------------------------------------------------------------------------------------------------
    // Modules (these exist only because Modules still exist in the Win8P surface area. There is a 1-1
    //          mapping between Assemblies and Modules.)
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimeModule : ExtensibleModule
    {
        internal static RuntimeModule GetRuntimeModule(RuntimeAssembly assembly)
        {
            return new RuntimeModule(assembly);
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

            if (!(elementType.ReflectionDomain is ExecutionDomain))
                return default(RuntimeTypeHandle);

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
                throw elementType.ReflectionDomain.CreateMissingArrayTypeException(elementType.AsType(), multiDim, rank);
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
            if (!(elementType.ReflectionDomain is ExecutionDomain))
                return default(RuntimeTypeHandle);

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
            if (!(genericTypeDefinition.ReflectionDomain is ExecutionDomain))
                return default(RuntimeTypeHandle);

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
                    throw key.GenericTypeDefinition.ReflectionDomain.CreateMissingConstructedGenericTypeException(key.GenericTypeDefinition.AsType(), key.GenericTypeArguments.CloneTypeArray());

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
                RuntimeNamedTypeInfo typeOwner = key.TypeDefinitionHandle.GetNamedType(key.Reader);
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

namespace System.Reflection.Runtime.FieldInfos
{
    //-----------------------------------------------------------------------------------------------------------
    // FieldInfos
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimeFieldInfo : ExtensibleFieldInfo
    {
        internal static RuntimeFieldInfo GetRuntimeFieldInfo(FieldHandle fieldHandle, RuntimeNamedTypeInfo definingTypeInfo, RuntimeTypeInfo contextTypeInfo)
        {
            return new RuntimeFieldInfo(fieldHandle, definingTypeInfo, contextTypeInfo).WithDebugName();
        }
    }
}

namespace System.Reflection.Runtime.MethodInfos
{
    //-----------------------------------------------------------------------------------------------------------
    // ConstructorInfos
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimePlainConstructorInfo : RuntimeConstructorInfo
    {
        internal static RuntimePlainConstructorInfo GetRuntimePlainConstructorInfo(MethodHandle methodHandle, RuntimeNamedTypeInfo definingTypeInfo, RuntimeTypeInfo contextTypeInfo)
        {
            return new RuntimePlainConstructorInfo(methodHandle, definingTypeInfo, contextTypeInfo);
        }
    }

    //-----------------------------------------------------------------------------------------------------------
    // Constructors for array types.
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimeSyntheticConstructorInfo : RuntimeConstructorInfo
    {
        internal static RuntimeSyntheticConstructorInfo GetRuntimeSyntheticConstructorInfo(SyntheticMethodId syntheticMethodId, RuntimeType declaringType, RuntimeType[] runtimeParameterTypesAndReturn, InvokerOptions options, Func<Object, Object[], Object> invoker)
        {
            return new RuntimeSyntheticConstructorInfo(syntheticMethodId, declaringType, runtimeParameterTypesAndReturn, options, invoker);
        }
    }

    //-----------------------------------------------------------------------------------------------------------
    // MethodInfos for method definitions (i.e. Foo.Moo() or Foo.Moo<>() but not Foo.Moo<int>)
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimeNamedMethodInfo : RuntimeMethodInfo
    {
        internal static RuntimeNamedMethodInfo GetRuntimeNamedMethodInfo(MethodHandle methodHandle, RuntimeNamedTypeInfo definingTypeInfo, RuntimeTypeInfo contextTypeInfo)
        {
            RuntimeNamedMethodInfo method = new RuntimeNamedMethodInfo(methodHandle, definingTypeInfo, contextTypeInfo);
            method.WithDebugName();
            return method;
        }
    }

    //-----------------------------------------------------------------------------------------------------------
    // MethodInfos for constructed generic methods (Foo.Moo<int> but not Foo.Moo<>)
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimeConstructedGenericMethodInfo : RuntimeMethodInfo
    {
        internal static RuntimeMethodInfo GetRuntimeConstructedGenericMethodInfo(RuntimeNamedMethodInfo genericMethodDefinition, RuntimeType[] genericTypeArguments)
        {
            return new RuntimeConstructedGenericMethodInfo(genericMethodDefinition, genericTypeArguments).WithDebugName();
        }
    }

    //-----------------------------------------------------------------------------------------------------------
    // MethodInfos for the Get/Set methods on array types.
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimeSyntheticMethodInfo : RuntimeMethodInfo
    {
        internal static RuntimeMethodInfo GetRuntimeSyntheticMethodInfo(SyntheticMethodId syntheticMethodId, String name, RuntimeType declaringType, RuntimeType[] runtimeParameterTypesAndReturn, InvokerOptions options, Func<Object, Object[], Object> invoker)
        {
            return new RuntimeSyntheticMethodInfo(syntheticMethodId, name, declaringType, runtimeParameterTypesAndReturn, options, invoker).WithDebugName();
        }
    }
}

namespace System.Reflection.Runtime.PropertyInfos
{
    //-----------------------------------------------------------------------------------------------------------
    // PropertyInfos
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimePropertyInfo : ExtensiblePropertyInfo
    {
        internal static RuntimePropertyInfo GetRuntimePropertyInfo(PropertyHandle propertyHandle, RuntimeNamedTypeInfo definingTypeInfo, RuntimeTypeInfo contextTypeInfo)
        {
            return new RuntimePropertyInfo(propertyHandle, definingTypeInfo, contextTypeInfo).WithDebugName();
        }
    }
}

namespace System.Reflection.Runtime.EventInfos
{
    //-----------------------------------------------------------------------------------------------------------
    // EventInfos
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimeEventInfo : ExtensibleEventInfo
    {
        internal static RuntimeEventInfo GetRuntimeEventInfo(EventHandle eventHandle, RuntimeNamedTypeInfo definingTypeInfo, RuntimeTypeInfo contextTypeInfo)
        {
            return new RuntimeEventInfo(eventHandle, definingTypeInfo, contextTypeInfo).WithDebugName();
        }
    }
}

namespace System.Reflection.Runtime.ParameterInfos
{
    //-----------------------------------------------------------------------------------------------------------
    // ParameterInfos for MethodBase objects with no Parameter metadata.
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimeThinMethodParameterInfo : RuntimeMethodParameterInfo
    {
        internal static RuntimeThinMethodParameterInfo GetRuntimeThinMethodParameterInfo(MethodBase member, int position, ReflectionDomain reflectionDomain, MetadataReader reader, Handle typeHandle, TypeContext typeContext)
        {
            return new RuntimeThinMethodParameterInfo(member, position, reflectionDomain, reader, typeHandle, typeContext);
        }
    }

    //-----------------------------------------------------------------------------------------------------------
    // ParameterInfos for MethodBase objects with Parameter metadata.
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimeFatMethodParameterInfo : RuntimeMethodParameterInfo
    {
        internal static RuntimeFatMethodParameterInfo GetRuntimeFatMethodParameterInfo(MethodBase member, MethodHandle methodHandle, int position, ParameterHandle parameterHandle, ReflectionDomain reflectionDomain, MetadataReader reader, Handle typeHandle, TypeContext typeContext)
        {
            return new RuntimeFatMethodParameterInfo(member, methodHandle, position, parameterHandle, reflectionDomain, reader, typeHandle, typeContext);
        }
    }

    //-----------------------------------------------------------------------------------------------------------
    // ParameterInfos returned by PropertyInfo.GetIndexParameters()
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimePropertyIndexParameterInfo : RuntimeParameterInfo
    {
        internal static RuntimePropertyIndexParameterInfo GetRuntimePropertyIndexParameterInfo(RuntimePropertyInfo member, RuntimeParameterInfo backingParameter)
        {
            return new RuntimePropertyIndexParameterInfo(member, backingParameter);
        }
    }

    //-----------------------------------------------------------------------------------------------------------
    // ParameterInfos returned by Get/Set methods on array types.
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimeSyntheticParameterInfo : RuntimeParameterInfo
    {
        internal static RuntimeSyntheticParameterInfo GetRuntimeSyntheticParameterInfo(MemberInfo member, int position, RuntimeType parameterType)
        {
            return new RuntimeSyntheticParameterInfo(member, position, parameterType);
        }
    }
}

namespace System.Reflection.Runtime.CustomAttributes
{
    //-----------------------------------------------------------------------------------------------------------
    // CustomAttributeData objects returned by various CustomAttributes properties.
    //-----------------------------------------------------------------------------------------------------------
    internal abstract partial class RuntimeCustomAttributeData : ExtensibleCustomAttributeData
    {
        internal static IEnumerable<CustomAttributeData> GetCustomAttributes(ReflectionDomain reflectionDomain, MetadataReader reader, IEnumerable<CustomAttributeHandle> customAttributeHandles)
        {
            foreach (CustomAttributeHandle customAttributeHandle in customAttributeHandles)
                yield return GetCustomAttributeData(reflectionDomain, reader, customAttributeHandle);
        }

        private static CustomAttributeData GetCustomAttributeData(ReflectionDomain reflectionDomain, MetadataReader reader, CustomAttributeHandle customAttributeHandle)
        {
            return new RuntimeNormalCustomAttributeData(reflectionDomain, reader, customAttributeHandle);
        }
    }
}

namespace System.Reflection.Runtime.TypeParsing
{
    //-----------------------------------------------------------------------------------------------------------
    // Name looks of namespace types. (Affects both type reference resolution and Type.GetType() calls.)
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class NamespaceTypeName : NamedTypeName
    {
        public sealed override Exception TryResolve(ReflectionDomain reflectionDomain, RuntimeAssembly currentAssembly, bool ignoreCase, out RuntimeType result)
        {
            result = _runtimeNamespaceTypeByNameDispenser.GetOrAdd(new NamespaceTypeNameKey(reflectionDomain, currentAssembly, this));
            if (result != null)
                return null;
            if (!ignoreCase)
                return new TypeLoadException(SR.Format(SR.TypeLoad_TypeNotFound, this.ToString(), currentAssembly.FullName));

            return TryResolveCaseInsensitive(reflectionDomain, currentAssembly, out result);
        }

        private static Dispenser<NamespaceTypeNameKey, RuntimeType> _runtimeNamespaceTypeByNameDispenser =
            DispenserFactory.CreateDispenserV<NamespaceTypeNameKey, RuntimeType>(
                DispenserScenario.AssemblyAndNamespaceTypeName_Type,
                delegate (NamespaceTypeNameKey key)
                {
                    RuntimeType result;
                    Exception typeLoadException = key.NamespaceTypeName.UncachedTryResolveCaseSensitive(key.ReflectionDomain, key.RuntimeAssembly, out result);
                    if (typeLoadException != null)
                        return null;
                    else
                        return result;
                }
            );

        private static LowLevelDictionary<String, QHandle> GetCaseInsensitiveTypeDictionary(RuntimeAssembly assembly)
        {
            return _caseInsensitiveTypeDictionaryDispenser.GetOrAdd(assembly);
        }

        private static Dispenser<RuntimeAssembly, LowLevelDictionary<String, QHandle>> _caseInsensitiveTypeDictionaryDispenser =
            DispenserFactory.CreateDispenserV<RuntimeAssembly, LowLevelDictionary<String, QHandle>>(
                DispenserScenario.RuntimeAssembly_CaseInsensitiveTypeDictionary,
                CreateCaseInsensitiveTypeDictionary
            );


        //
        // Hash key for resolving NamespaceTypeNames to RuntimeTypes.
        //
        private struct NamespaceTypeNameKey : IEquatable<NamespaceTypeNameKey>
        {
            public NamespaceTypeNameKey(ReflectionDomain reflectionDomain, RuntimeAssembly runtimeAssembly, NamespaceTypeName namespaceTypeName)
            {
                _reflectionDomain = reflectionDomain;
                _runtimeAssembly = runtimeAssembly;
                _namespaceTypeName = namespaceTypeName;
            }

            public ReflectionDomain ReflectionDomain
            {
                get
                {
                    return _reflectionDomain;
                }
            }

            public RuntimeAssembly RuntimeAssembly
            {
                get
                {
                    return _runtimeAssembly;
                }
            }

            public NamespaceTypeName NamespaceTypeName
            {
                get
                {
                    return _namespaceTypeName;
                }
            }

            public override bool Equals(Object obj)
            {
                if (!(obj is NamespaceTypeNameKey))
                    return false;
                return Equals((NamespaceTypeNameKey)obj);
            }

            public bool Equals(NamespaceTypeNameKey other)
            {
                if (!(this._namespaceTypeName._name.Equals(other._namespaceTypeName._name)))
                    return false;
                if (!(this._namespaceTypeName._namespaceParts.Length == other._namespaceTypeName._namespaceParts.Length))
                    return false;
                int count = this._namespaceTypeName._namespaceParts.Length;
                for (int i = 0; i < count; i++)
                {
                    if (!(this._namespaceTypeName._namespaceParts[i] == other._namespaceTypeName._namespaceParts[i]))
                        return false;
                }
                if (!(this._runtimeAssembly.Equals(other._runtimeAssembly)))
                    return false;
                return true;
            }

            public override int GetHashCode()
            {
                return _namespaceTypeName._name.GetHashCode();
            }

            private ReflectionDomain _reflectionDomain;
            private RuntimeAssembly _runtimeAssembly;
            private NamespaceTypeName _namespaceTypeName;
        }
    }
}

