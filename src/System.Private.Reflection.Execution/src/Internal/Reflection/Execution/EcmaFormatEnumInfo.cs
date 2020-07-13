// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Runtime.General;

using Internal.Runtime.Augments;

namespace Internal.Reflection.Execution
{
    static class EcmaFormatEnumInfo
    {
        public static EnumInfo Create(RuntimeTypeHandle typeHandle, MetadataReader reader, TypeDefinitionHandle typeDefHandle)
        {
            TypeDefinition typeDef = reader.GetTypeDefinition(typeDefHandle);

            // Per the spec, Enums are required to have one instance field. The rest are statics.
            int staticFieldCount = typeDef.GetFields().Count - 1;

            string[] names = new string[staticFieldCount];
            object[] values = new object[staticFieldCount];

            int i = 0;
            foreach (FieldDefinitionHandle fieldHandle in typeDef.GetFields())
            {
                FieldDefinition field = reader.GetFieldDefinition(fieldHandle);
                if (0 != (field.Attributes & FieldAttributes.Static))
                {
                    if (i >= staticFieldCount || (field.Attributes & FieldAttributes.HasDefault) != FieldAttributes.HasDefault)
                        throw new BadImageFormatException();

                    names[i] = reader.GetString(field.Name);
                    values[i] = field.GetDefaultValue().ParseConstantValue(reader);
                    i++;
                }
            }

            bool isFlags = false;
            foreach (CustomAttributeHandle cah in typeDef.GetCustomAttributes())
            {
                if (cah.IsCustomAttributeOfType(reader, "System", "FlagsAttribute"))
                    isFlags = true;
            }

            return new EnumInfo(RuntimeAugments.GetEnumUnderlyingType(typeHandle), values, names, isFlags);
        }
    }
}
