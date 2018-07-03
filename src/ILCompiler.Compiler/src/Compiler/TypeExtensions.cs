// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.IL;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler
{
    public static class TypeExtensions
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
        public static DefType GetClosestDefType(this TypeDesc type)
        {
            if (type.IsArray)
            {
                if (type.IsArrayTypeWithoutGenericInterfaces())
                    return type.Context.GetWellKnownType(WellKnownType.Array);

                MetadataType arrayShadowType = type.Context.SystemModule.GetKnownType("System", "Array`1");
                return arrayShadowType.MakeInstantiatedType(((ArrayType)type).ElementType);
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

        /// <summary>
        /// Returns true if '<paramref name="method"/>' is the "Address" method on multidimensional array types.
        /// </summary>
        public static bool IsArrayAddressMethod(this MethodDesc method)
        {
            var arrayMethod = method as ArrayMethod;
            return arrayMethod != null && arrayMethod.Kind == ArrayMethodKind.Address;
        }

        /// <summary>
        /// Gets a value indicating whether this type has any generic virtual methods.
        /// </summary>
        public static bool HasGenericVirtualMethods(this TypeDesc type)
        {
            foreach (var method in type.GetAllMethods())
            {
                if (method.IsVirtual && method.HasInstantiation)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Wrapper helper function around the IsCanonicalDefinitionType API on the TypeSystemContext
        /// </summary>
        public static bool IsCanonicalDefinitionType(this TypeDesc type, CanonicalFormKind kind)
        {
            return type.Context.IsCanonicalDefinitionType(type, kind);
        }

        /// <summary>
        /// Gets the value of the field ordinal. Ordinals are computed by also including static fields, but excluding
        /// literal fields and fields with RVAs.
        /// </summary>
        public static int GetFieldOrdinal(this FieldDesc inputField)
        {
            // Make sure we are asking the question for a valid instance or static field
            Debug.Assert(!inputField.HasRva && !inputField.IsLiteral);

            int fieldOrdinal = 0;
            foreach (FieldDesc field in inputField.OwningType.GetFields())
            {
                // If this field does not contribute to layout, skip
                if (field.HasRva || field.IsLiteral)
                    continue;

                if (field == inputField)
                    return fieldOrdinal;

                fieldOrdinal++;
            }

            Debug.Assert(false);
            return -1;
        }

        /// <summary>
        /// What is the maximum number of steps that need to be taken from this type to its most contained generic type.
        /// i.e.
        /// System.Int32 => 0
        /// List&lt;System.Int32&gt; => 1
        /// Dictionary&lt;System.Int32,System.Int32&gt; => 1
        /// Dictionary&lt;List&lt;System.Int32&gt;,&lt;System.Int32&gt; => 2
        /// </summary>
        public static int GetGenericDepth(this TypeDesc type)
        {
            if (type.HasInstantiation)
            {
                int maxGenericDepthInInstantiation = 0;
                foreach (TypeDesc instantiationType in type.Instantiation)
                {
                    maxGenericDepthInInstantiation = Math.Max(instantiationType.GetGenericDepth(), maxGenericDepthInInstantiation);
                }

                return maxGenericDepthInInstantiation + 1;
            }

            return 0;
        }

        /// <summary>
        /// Determine if a type has a generic depth greater than a given value
        /// </summary>
        public static bool IsGenericDepthGreaterThan(this TypeDesc type, int depth)
        {
            if (depth < 0)
                return true;

            foreach (TypeDesc instantiationType in type.Instantiation)
            {
                if (instantiationType.IsGenericDepthGreaterThan(depth - 1))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether an array type does implements the generic collection interfaces. This is the case
        /// for multi-dimensional arrays, and arrays of pointers.
        /// </summary>
        public static bool IsArrayTypeWithoutGenericInterfaces(this TypeDesc type)
        {
            if (!type.IsArray)
                return false;

            var arrayType = (ArrayType)type;
            TypeDesc elementType = arrayType.ElementType;
            return type.IsMdArray || elementType.IsPointer || elementType.IsFunctionPointer;
        }
    }
}
