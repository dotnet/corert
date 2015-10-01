// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Internal.TypeSystem
{
    static public class TypeSystemHelpers
    {
        static public InstantiatedType MakeInstantiatedType(this MetadataType typeDef, Instantiation instantiation)
        {
            return typeDef.Context.GetInstantiatedType(typeDef, instantiation);
        }

        static public InstantiatedMethod MakeInstantiatedMethod(this MethodDesc methodDef, Instantiation instantiation)
        {
            return methodDef.Context.GetInstantiatedMethod(methodDef, instantiation);
        }

        static public TypeDesc MakeByRefType(this TypeDesc type)
        {
            return type.Context.GetByRefType(type);
        }

        static public TypeFlags UnderlyingCategory(this TypeDesc type)
        {
            TypeFlags category = type.Category;
            if (type.IsEnum)
            {
                foreach (var field in type.GetFields())
                {
                    if (!field.IsStatic)
                        return field.FieldType.Category;
                }
                throw new TypeLoadException();
            }
            return category;
        }
    }
}
