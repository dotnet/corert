﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.IL;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler
{
    internal static class TypeExtensions
    {
        public static bool IsSealed(this TypeDesc type)
        {
            var metadataType = type as MetadataType;
            if (metadataType != null)
            {
                return metadataType.IsSealed || metadataType.IsModuleType;
            }

            Debug.Assert(type.IsArray, "IsSealed on a type with no virtual methods?");
            return true;
        }

        /// <summary>
        /// Gets the type that defines virtual method slots for the specified type.
        /// </summary>
        static public DefType GetClosestDefType(this TypeDesc type)
        {
            if (type.IsArray)
            {
                var arrayType = (ArrayType)type;
                TypeDesc elementType = arrayType.ElementType;
                if (arrayType.IsSzArray && !elementType.IsPointer && !elementType.IsFunctionPointer)
                {
                    MetadataType arrayShadowType = type.Context.SystemModule.GetKnownType("System", "Array`1");
                    return arrayShadowType.MakeInstantiatedType(elementType);
                }
                return type.Context.GetWellKnownType(WellKnownType.Array);
            }

            Debug.Assert(type is DefType);
            return (DefType)type;
        }

        /// <summary>
        /// Gets a value indicating whether the method requires a hidden instantiation argument in addition
        /// to the formal arguments defined in the method signature.
        /// </summary>
        public static bool RequiresInstArg(this MethodDesc method)
        {
            return method.IsSharedByGenericInstantiations &&
                (method.HasInstantiation || method.Signature.IsStatic || method.ImplementationType.IsValueType);
        }

        /// <summary>
        /// Gets a value indicating whether the method acquires the generic context from a hidden
        /// instantiation argument that points to the method's generic dictionary.
        /// </summary>
        public static bool RequiresInstMethodDescArg(this MethodDesc method)
        {
            return method.HasInstantiation && method.IsSharedByGenericInstantiations;
        }

        /// <summary>
        /// Gets a value indicating whether the method acquires the generic context from a hidden
        /// instantiation argument that points to the generic dictionary of the method's owning type.
        /// </summary>
        public static bool RequiresInstMethodTableArg(this MethodDesc method)
        {
            return (method.Signature.IsStatic || method.ImplementationType.IsValueType) &&
                method.IsSharedByGenericInstantiations &&
                !method.HasInstantiation;
        }

        /// <summary>
        /// Gets a value indicating whether the method acquires the generic context from the this pointer.
        /// </summary>
        public static bool AcquiresInstMethodTableFromThis(this MethodDesc method)
        {
            return method.IsSharedByGenericInstantiations &&
                !method.HasInstantiation &&
                !method.Signature.IsStatic &&
                !method.ImplementationType.IsValueType;
        }
    }
}
