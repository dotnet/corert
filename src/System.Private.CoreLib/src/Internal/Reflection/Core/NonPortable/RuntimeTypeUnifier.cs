// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Internal.Reflection.Core.NonPortable
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
    // 
    // Internal details:
    //
    //  The RuntimeType is not a single class but a family of classes that can be categorized along two dimensions:
    //
    //    - Type structure (named vs. array vs. generic instance, etc.)
    //
    //    - Is invokable (i.e. has a RuntimeTypeHandle.)
    //
    //  Taking advantage of this, RuntimeTypeUnifier splits the unification across several type tables, each with its own separate lock.
    //  Each type table owns a specific group of RuntimeTypes. These groups can overlap. In particular, types with EETypes can and do
    //  appear in both TypeTableForTypesWithEETypes and the "inspection" type table for the type's specific flavor. This allows
    //  fast lookups for both the Object.GetType() calls and the metadata initiated lookups.
    //
    internal static partial class RuntimeTypeUnifier
    {
        //
        // Retrieves the unified Type object for an "array of elementType."
        //
        public static RuntimeType GetArrayType(RuntimeType elementType)
        {
            return TypeTableForArrayTypes.Table.GetOrAdd(elementType).WithDebugName();
        }

        //
        // Retrieves the unified Type object for a multidimensional "array of elementType."
        //
        public static RuntimeType GetMultiDimArrayType(RuntimeType elementType, int rank)
        {
            TypeTableForMultiDimArrayTypes table = TypeTableForMultiDimArrayTypesTable.Table.GetOrAdd(rank);
            return table.GetOrAdd(elementType).WithDebugName();
        }

        //
        // Retrieves the unified Type object for an "byref of targetType."
        //
        public static RuntimeType GetByRefType(RuntimeType targetType)
        {
            return TypeTableForByRefTypes.Table.GetOrAdd(targetType).WithDebugName();
        }

        //
        // Retrieves the unified Type object for an "pointer of targetType."
        //
        public static RuntimeType GetPointerType(RuntimeType targetType)
        {
            return TypeTableForPointerTypes.Table.GetOrAdd(targetType).WithDebugName();
        }

        //
        // Retrieves the unified Type object for given RuntimeTypeHandle (this is basically the Type.GetTypeFromHandle() api without the input validation.)
        //
        public static RuntimeType GetTypeForRuntimeTypeHandle(RuntimeTypeHandle runtimeTypeHandle)
        {
            return RuntimeTypeHandleToRuntimeTypeCache.Table.GetOrAdd(new RawRuntimeTypeHandleKey(runtimeTypeHandle)).WithDebugName();
        }

        //
        // Retrieves the unified Type object for a constructed generic type.
        //
        public static RuntimeType GetConstructedGenericType(RuntimeType genericTypeDefinition, RuntimeType[] genericTypeArguments)
        {
            ConstructedGenericTypeKey key = new ConstructedGenericTypeKey(genericTypeDefinition, genericTypeArguments);
            RuntimeType result = TypeTableForConstructedGenericTypes.Table.GetOrAdd(key).WithDebugName();
            return result;
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


