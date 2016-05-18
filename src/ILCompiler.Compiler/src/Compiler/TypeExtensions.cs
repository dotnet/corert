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

        static public MetadataType GetClosestMetadataType(this TypeDesc type)
        {
            if (type.IsSzArray && !((ArrayType)type).ElementType.IsPointer)
            {
                MetadataType arrayType = type.Context.SystemModule.GetKnownType("System", "Array`1");
                return arrayType.MakeInstantiatedType(((ArrayType)type).ElementType);
            }
            else if (type.IsArray)
            {
                return (MetadataType)type.Context.GetWellKnownType(WellKnownType.Array);
            }

            Debug.Assert(type is MetadataType);
            return (MetadataType)type;
        }
    }
}
