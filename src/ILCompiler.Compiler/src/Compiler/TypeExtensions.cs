// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.IL;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler
{
    static class TypeExtensions
    {
        public static bool IsSealed(this TypeDesc type)
        {
            var metadataType = type as MetadataType;
            if (metadataType != null)
            {
                return metadataType.IsSealed;
            }

            Debug.Assert(type.IsArray, "IsSealed on a type with no virtual methods?");
            return true;
        }

        static public DefType GetClosestDefType(this TypeDesc type)
        {
            if (type.IsSzArray && !((ArrayType)type).ElementType.IsPointer)
            {
                MetadataType arrayType = type.Context.SystemModule.GetKnownType("System", "Array`1");
                return arrayType.MakeInstantiatedType(((ArrayType)type).ElementType);
            }
            else if (type.IsArray)
            {
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
            bool result = method.IsCanonicalMethod(CanonicalFormKind.Any) &&
                (method.HasInstantiation || method.Signature.IsStatic || method.OwningType.IsValueType);

            Debug.Assert(result == (method.RequiresInstMethodDescArg() || method.RequiresInstMethodTableArg()));

            return result;
        }

        public static bool RequiresInstMethodDescArg(this MethodDesc method)
        {
            return method.IsCanonicalMethod(CanonicalFormKind.Any) && method.HasInstantiation;
        }

        public static bool RequiresInstMethodTableArg(this MethodDesc method)
        {
            return method.IsCanonicalMethod(CanonicalFormKind.Any) &&
                !method.HasInstantiation &&
                (method.Signature.IsStatic || method.OwningType.IsValueType);
        }

        public static bool IsSharedInstantiationType(this TypeDesc type)
        {
            return type.IsCanonicalSubtype(CanonicalFormKind.Any) || type.IsRuntimeDeterminedSubtype;
        }
    }
}
