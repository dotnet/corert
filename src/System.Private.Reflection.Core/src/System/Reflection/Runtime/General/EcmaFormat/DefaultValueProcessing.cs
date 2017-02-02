// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using Internal.Reflection.Extensions.NonPortable;

using System.Reflection.Metadata;

namespace System.Reflection.Runtime
{
    public static class DefaultValueProcessing
    {
        public static bool GetDefaultValueIfAny(MetadataReader reader, ref FieldDefinition field, FieldInfo fieldInfo, out object defaultValue)
        {
            if (0 != (field.Attributes & FieldAttributes.HasDefault))
            {
                defaultValue = ConstantValueAsObject(field.GetDefaultValue(), reader);
                return true;
            }
            else
            {
                return GetCustomAttributeDefaultValueIfAny(fieldInfo.CustomAttributes, out defaultValue);
            }
        }

        public static bool GetDefaultValueIfAny(MetadataReader reader, ref Parameter parameter, ParameterInfo parameterInfo, out object defaultValue)
        {
            if (0 != (parameter.Attributes & ParameterAttributes.HasDefault))
            {
                defaultValue = ConstantValueAsObject(parameter.GetDefaultValue(), reader);
                return true;
            }
            else
            {
                return GetCustomAttributeDefaultValueIfAny(parameterInfo.CustomAttributes, out defaultValue);
            }
        }

        public static bool GetDefaultValueIfAny(MetadataReader reader, ref PropertyDefinition property, PropertyInfo propertyInfo, out object defaultValue)
        {
            if (0 != (property.Attributes & PropertyAttributes.HasDefault))
            {
                defaultValue = ConstantValueAsObject(property.GetDefaultValue(), reader);
                return true;
            }
            else
            {
                // Custom attributes default values cannot be specified on properties
                defaultValue = null;
                return false;
            }
        }                

        private static bool GetCustomAttributeDefaultValueIfAny(IEnumerable<CustomAttributeData> customAttributes, out object defaultValue)
        {
            // Legacy: If there are multiple default value attribute, the desktop picks one at random (and so do we...)
            foreach (CustomAttributeData cad in customAttributes)
            {
                Type attributeType = cad.AttributeType;
                if (attributeType.IsSubclassOf(typeof(CustomConstantAttribute)))
                {
                    CustomConstantAttribute customConstantAttribute = (CustomConstantAttribute)(cad.Instantiate());
                    defaultValue = customConstantAttribute.Value;
                    return true;
                }
                if (attributeType.Equals(typeof(DecimalConstantAttribute)))
                {
                    DecimalConstantAttribute decimalConstantAttribute = (DecimalConstantAttribute)(cad.Instantiate());
                    defaultValue = decimalConstantAttribute.Value;
                    return true;
                }
            }

            defaultValue = null;
            return false;
        }

        public static object ConstantValueAsObject(ConstantHandle constantHandle, MetadataReader metadataReader)
        {
            if (constantHandle.IsNil)
                throw new BadImageFormatException();

            Constant constantValue = metadataReader.GetConstant(constantHandle);

            if (constantValue.Value.IsNil)
                throw new BadImageFormatException();

            BlobReader reader = metadataReader.GetBlobReader(constantValue.Value);

            switch (constantValue.TypeCode)
            {
                case ConstantTypeCode.Boolean:
                    return reader.ReadBoolean();

                case ConstantTypeCode.Char:
                    return reader.ReadChar();

                case ConstantTypeCode.SByte:
                    return reader.ReadSByte();

                case ConstantTypeCode.Int16:
                    return reader.ReadInt16();

                case ConstantTypeCode.Int32:
                    return reader.ReadInt32();

                case ConstantTypeCode.Int64:
                    return reader.ReadInt64();

                case ConstantTypeCode.Byte:
                    return reader.ReadByte();

                case ConstantTypeCode.UInt16:
                    return reader.ReadUInt16();

                case ConstantTypeCode.UInt32:
                    return reader.ReadUInt32();

                case ConstantTypeCode.UInt64:
                    return reader.ReadUInt64();

                case ConstantTypeCode.Single:
                    return reader.ReadSingle();

                case ConstantTypeCode.Double:
                    return reader.ReadDouble();

                case ConstantTypeCode.String:
                    return reader.ReadUTF16(reader.Length);

                case ConstantTypeCode.NullReference:
                    // Partition II section 22.9:
                    // The encoding of Type for the nullref value is ELEMENT_TYPE_CLASS with a Value of a 4-byte zero.
                    // Unlike uses of ELEMENT_TYPE_CLASS in signatures, this one is not followed by a type token.
                    if (reader.ReadUInt32() == 0)
                    {
                        return null;
                    }

                    break;
            }

            throw new BadImageFormatException();
        } 
    }
}