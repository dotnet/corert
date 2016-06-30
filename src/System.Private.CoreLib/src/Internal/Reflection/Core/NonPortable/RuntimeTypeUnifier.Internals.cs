// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Concurrent;

using Internal.Runtime.Augments;

namespace Internal.Reflection.Core.NonPortable
{
    internal static partial class RuntimeTypeUnifier
    {
        //
        // TypeTable mapping raw RuntimeTypeHandles (normalized or otherwise) to RuntimeTypes.
        //
        // Note the relationship between RuntimeTypeHandleToRuntimeTypeCache and TypeTableForTypesWithEETypes and TypeTableForEENamedGenericTypes.
        // The latter two exist to enforce the creation of one Type instance per semantic identity. RuntimeTypeHandleToRuntimeTypeCache, on the other
        // hand, exists for fast lookup. It hashes and compares on the raw IntPtr value of the RuntimeTypeHandle. Because Redhawk
        // can and does create multiple EETypes for the same semantically identical type, the same RuntimeType can legitimately appear twice
        // in this table. The factory, however, does a second lookup in the true unifying tables rather than creating the RuntimeType itself.
        // Thus, the one-to-one relationship between Type reference identity and Type semantic identity is preserved.
        //
        private sealed class RuntimeTypeHandleToRuntimeTypeCache : ConcurrentUnifierW<RawRuntimeTypeHandleKey, RuntimeType>
        {
            private RuntimeTypeHandleToRuntimeTypeCache() { }

            protected sealed override RuntimeType Factory(RawRuntimeTypeHandleKey rawRuntimeTypeHandleKey)
            {
                RuntimeTypeHandle runtimeTypeHandle = rawRuntimeTypeHandleKey.RuntimeTypeHandle;

                // Desktop compat: Allows Type.GetTypeFromHandle(default(RuntimeTypeHandle)) to map to null.
                if (runtimeTypeHandle.RawValue == (IntPtr)0)
                    return null;
                EETypePtr eeType = runtimeTypeHandle.ToEETypePtr();
                return TypeTableForTypesWithEETypes.Table.GetOrAdd(eeType);
            }

            public static RuntimeTypeHandleToRuntimeTypeCache Table = new RuntimeTypeHandleToRuntimeTypeCache();
        }

        //
        // Type table for *all* RuntimeTypes that have an EEType associated with it (named types,
        // arrays, constructed generic types.)
        //
        // The EEType itself serves as the dictionary key.
        //
        // This table's key uses semantic identity as the compare function. Thus, it properly serves to unify all semantically equivalent types
        // into a single Type instance. 
        //
        private sealed class TypeTableForTypesWithEETypes : ConcurrentUnifierW<EETypePtr, RuntimeType>
        {
            private TypeTableForTypesWithEETypes() { }

            protected sealed override RuntimeType Factory(EETypePtr eeType)
            {
                if (eeType.IsDefType)
                {
                    if (eeType.IsGenericTypeDefinition)
                    {
                        return new RuntimeEENamedGenericType(eeType);
                    }
                    else if (eeType.IsGeneric)
                    {
                        // Reflection blocked constructed generic types simply pretend to not be generic
                        // This is reasonable, as the behavior of reflection blocked types is supposed
                        // to be that they expose the minimal information about a type that is necessary
                        // for users of Object.GetType to move from that type to a type that isn't
                        // reflection blocked. By not revealing that reflection blocked types are generic
                        // we are making it appear as if implementation detail types exposed to user code
                        // are all non-generic, which is theoretically possible, and by doing so
                        // we avoid (in all known circumstances) the very complicated case of representing 
                        // the interfaces, base types, and generic parameter types of reflection blocked 
                        // generic type definitions.
                        if (RuntimeAugments.Callbacks.IsReflectionBlocked(new RuntimeTypeHandle(eeType)))
                        {
                            return new RuntimeEENamedNonGenericType(eeType);
                        }

#if !REAL_MULTIDIM_ARRAYS
                        if (RuntimeImports.AreTypesAssignable(eeType, EETypePtr.EETypePtrOf<MDArrayRank2>()))
                            return new RuntimeEEArrayType(eeType, rank: 2);
                        if (RuntimeImports.AreTypesAssignable(eeType, EETypePtr.EETypePtrOf<MDArrayRank3>()))
                            return new RuntimeEEArrayType(eeType, rank: 3);
                        if (RuntimeImports.AreTypesAssignable(eeType, EETypePtr.EETypePtrOf<MDArrayRank4>()))
                            return new RuntimeEEArrayType(eeType, rank: 4);
#endif
                        return new RuntimeEEConstructedGenericType(eeType);
                    }
                    else
                    {
                        return new RuntimeEENamedNonGenericType(eeType);
                    }
                }
                else if (eeType.IsArray)
                {
#if REAL_MULTIDIM_ARRAYS
                    if (!eeType.IsSzArray)
                        return new RuntimeEEArrayType(eeType, eeType.ArrayRank);
                    else
                        return new RuntimeEEArrayType(eeType);
#else
                        return new RuntimeEEArrayType(eeType);
#endif
                }
                else if (eeType.IsPointer)
                {
                    return new RuntimeEEPointerType(eeType);
                }
                else
                {
                    throw new ArgumentException(SR.Arg_InvalidRuntimeTypeHandle);
                }
            }

            public static TypeTableForTypesWithEETypes Table = new TypeTableForTypesWithEETypes();
        }

        //
        // Type table for all SZ RuntimeArrayTypes.
        // The element type serves as the dictionary key.
        //
        private sealed class TypeTableForArrayTypes : ConcurrentUnifierWKeyed<RuntimeType, RuntimeArrayType>
        {
            protected sealed override RuntimeArrayType Factory(RuntimeType elementType)
            {
                // We only permit creating parameterized types if the pay-for-play policy specifically allows them *or* if the result
                // type would be an open type.

                RuntimeTypeHandle runtimeTypeHandle;
                RuntimeTypeHandle elementTypeHandle;
                if (elementType.InternalTryGetTypeHandle(out elementTypeHandle) &&
                    RuntimeAugments.Callbacks.TryGetArrayTypeForElementType(elementTypeHandle, out runtimeTypeHandle))
                    return (RuntimeArrayType)(RuntimeTypeUnifier.GetTypeForRuntimeTypeHandle(runtimeTypeHandle));

                if (elementType.IsByRef)
                    throw new TypeLoadException(SR.Format(SR.ArgumentException_InvalidArrayElementType, elementType));
                if (elementType.InternalIsGenericTypeDefinition)
                    throw new ArgumentException(SR.Format(SR.ArgumentException_InvalidArrayElementType, elementType));
                if (!elementType.InternalIsOpen)
                    throw RuntimeAugments.Callbacks.CreateMissingArrayTypeException(elementType, false, 1);
                return new RuntimeInspectionOnlyArrayType(elementType);
            }

            public static TypeTableForArrayTypes Table = new TypeTableForArrayTypes();
        }

        //
        // Type table for all MultiDim RuntimeArrayTypes.
        // The element type serves as the dictionary key.
        //
        private sealed class TypeTableForMultiDimArrayTypes : ConcurrentUnifierWKeyed<RuntimeType, RuntimeArrayType>
        {
            public TypeTableForMultiDimArrayTypes(int rank)
            {
                _rank = rank;
            }

            protected sealed override RuntimeArrayType Factory(RuntimeType elementType)
            {
                // We only permit creating parameterized types if the pay-for-play policy specifically allows them *or* if the result
                // type would be an open type.

                RuntimeTypeHandle runtimeTypeHandle;
                RuntimeTypeHandle elementTypeHandle;
                if (elementType.InternalTryGetTypeHandle(out elementTypeHandle) &&
                    RuntimeAugments.Callbacks.TryGetMultiDimArrayTypeForElementType(elementTypeHandle, _rank, out runtimeTypeHandle))
                    return (RuntimeArrayType)(RuntimeTypeUnifier.GetTypeForRuntimeTypeHandle(runtimeTypeHandle));
                if (elementType.IsByRef)
                    throw new TypeLoadException(SR.Format(SR.ArgumentException_InvalidArrayElementType, elementType));
                if (elementType.InternalIsGenericTypeDefinition)
                    throw new ArgumentException(SR.Format(SR.ArgumentException_InvalidArrayElementType, elementType));
                if (!elementType.InternalIsOpen)
                    throw RuntimeAugments.Callbacks.CreateMissingArrayTypeException(elementType, true, _rank);
                return new RuntimeInspectionOnlyArrayType(elementType, _rank);
            }

            private int _rank;
        }

        //
        // For the hopefully rare case of multidim arrays, we have a dictionary of dictionaries.
        //
        private sealed class TypeTableForMultiDimArrayTypesTable : ConcurrentUnifier<int, TypeTableForMultiDimArrayTypes>
        {
            protected sealed override TypeTableForMultiDimArrayTypes Factory(int rank)
            {
                Debug.Assert(rank > 0);
                return new TypeTableForMultiDimArrayTypes(rank);
            }

            public static TypeTableForMultiDimArrayTypesTable Table = new TypeTableForMultiDimArrayTypesTable();
        }


        //
        // Type table for all RuntimeByRefTypes. (There's no such thing as an EEType for a byref so all ByRef types are "inspection only.")
        // The target type serves as the dictionary key.
        //
        private sealed class TypeTableForByRefTypes : ConcurrentUnifierWKeyed<RuntimeType, RuntimeByRefType>
        {
            private TypeTableForByRefTypes() { }

            protected sealed override RuntimeByRefType Factory(RuntimeType targetType)
            {
                return new RuntimeByRefType(targetType);
            }

            public static TypeTableForByRefTypes Table = new TypeTableForByRefTypes();
        }

        //
        // Type table for all RuntimePointerTypes.
        // The target type serves as the dictionary key.
        //
        private sealed class TypeTableForPointerTypes : ConcurrentUnifierWKeyed<RuntimeType, RuntimePointerType>
        {
            private TypeTableForPointerTypes() { }

            protected sealed override RuntimePointerType Factory(RuntimeType elementType)
            {
                RuntimeTypeHandle thElementType;

                if (elementType.InternalTryGetTypeHandle(out thElementType))
                {
                    RuntimeTypeHandle thForPointerType;

                    if (RuntimeAugments.Callbacks.TryGetPointerTypeForTargetType(thElementType, out thForPointerType))
                    {
                        Debug.Assert(thForPointerType.ToEETypePtr().IsPointer);
                        return (RuntimePointerType)(RuntimeTypeUnifier.GetTypeForRuntimeTypeHandle(thForPointerType));
                    }
                }

                return new RuntimeInspectionOnlyPointerType(elementType);
            }

            public static TypeTableForPointerTypes Table = new TypeTableForPointerTypes();
        }

        //
        // Type table for all constructed generic types.
        //
        private sealed class TypeTableForConstructedGenericTypes : ConcurrentUnifierWKeyed<ConstructedGenericTypeKey, RuntimeConstructedGenericType>
        {
            private TypeTableForConstructedGenericTypes() { }

            protected sealed override RuntimeConstructedGenericType Factory(ConstructedGenericTypeKey key)
            {
                // We only permit creating parameterized types if the pay-for-play policy specifically allows them *or* if the result
                // type would be an open type.

                RuntimeTypeHandle runtimeTypeHandle;
                if (TryFindRuntimeTypeHandleForConstructedGenericType(key, out runtimeTypeHandle))
                    return (RuntimeConstructedGenericType)(RuntimeTypeUnifier.GetTypeForRuntimeTypeHandle(runtimeTypeHandle));

                bool atLeastOneOpenType = false;
                foreach (RuntimeType genericTypeArgument in key.GenericTypeArguments)
                {
                    if (genericTypeArgument.IsByRef || genericTypeArgument.InternalIsGenericTypeDefinition)
                        throw new ArgumentException(SR.Format(SR.ArgumentException_InvalidTypeArgument, genericTypeArgument));
                    if (genericTypeArgument.InternalIsOpen)
                        atLeastOneOpenType = true;
                }

                if (!atLeastOneOpenType)
                    throw RuntimeAugments.Callbacks.CreateMissingConstructedGenericTypeException(key.GenericTypeDefinition, key.GenericTypeArguments);

                return new RuntimeInspectionOnlyConstructedGenericType(key.GenericTypeDefinition, key.GenericTypeArguments);
            }

            private bool TryFindRuntimeTypeHandleForConstructedGenericType(ConstructedGenericTypeKey key, out RuntimeTypeHandle runtimeTypeHandle)
            {
                runtimeTypeHandle = default(RuntimeTypeHandle);

                RuntimeTypeHandle genericTypeDefinitionHandle = default(RuntimeTypeHandle);
                if (!key.GenericTypeDefinition.InternalTryGetTypeHandle(out genericTypeDefinitionHandle))
                    return false;

                if (RuntimeAugments.Callbacks.IsReflectionBlocked(genericTypeDefinitionHandle))
                    return false;

                RuntimeType[] genericTypeArguments = key.GenericTypeArguments;
                RuntimeTypeHandle[] genericTypeArgumentHandles = new RuntimeTypeHandle[genericTypeArguments.Length];
                for (int i = 0; i < genericTypeArguments.Length; i++)
                {
                    if (!genericTypeArguments[i].InternalTryGetTypeHandle(out genericTypeArgumentHandles[i]))
                        return false;
                }

                if (!RuntimeAugments.Callbacks.TryGetConstructedGenericTypeForComponents(genericTypeDefinitionHandle, genericTypeArgumentHandles, out runtimeTypeHandle))
                    return false;
                return true;
            }

            public static TypeTableForConstructedGenericTypes Table = new TypeTableForConstructedGenericTypes();
        }
    }
}

