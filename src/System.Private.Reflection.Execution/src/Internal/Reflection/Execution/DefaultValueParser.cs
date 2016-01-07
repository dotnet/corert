// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using global::System;
using global::System.Reflection;
using global::System.Collections.Generic;
using global::System.Runtime.InteropServices;
using global::System.Runtime.CompilerServices;

using global::Internal.Runtime.Augments;

using global::Internal.Reflection.Core;
using global::Internal.Reflection.Core.Execution;
using global::Internal.Reflection.Extensions.NonPortable;

using global::Internal.Metadata.NativeFormat;

namespace Internal.Reflection.Execution
{
    internal static class DefaultValueParser
    {
        public static bool GetDefaultValueIfAny(MemberType memberType, MetadataReader reader, Handle constantHandle, Type declaredType, IEnumerable<CustomAttributeData> customAttributes, out Object defaultValue)
        {
            if (!(constantHandle.IsNull(reader)))
            {
                defaultValue = ParseMetadataConstant(reader, constantHandle);
                if (declaredType.GetTypeInfo().IsEnum)
                    defaultValue = Enum.ToObject(declaredType, defaultValue);
                return true;
            }

            if (memberType != MemberType.Property)  // the attributes in question cannot be applied to properties.
            {
                // Legacy: If there are multiple default value attribute, the desktop picks one at random (and so do we...)
                foreach (CustomAttributeData cad in customAttributes)
                {
                    Type attributeType = cad.AttributeType;
                    TypeInfo attributeTypeInfo = attributeType.GetTypeInfo();
                    if (attributeTypeInfo.IsSubclassOf(typeof(CustomConstantAttribute)))
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
            }

            defaultValue = null;
            return false;
        }

        // Not all default value types are permitted in all scenarios.
        public enum MemberType
        {
            Field = 1,
            Property = 2,
            Parameter = 3,
        }

        private static Object ParseMetadataConstant(MetadataReader reader, Handle handle)
        {
            switch (handle.HandleType)
            {
                case HandleType.ConstantBooleanValue:
                    return handle.ToConstantBooleanValueHandle(reader).GetConstantBooleanValue(reader).Value;

                case HandleType.ConstantStringValue:
                    return handle.ToConstantStringValueHandle(reader).GetConstantStringValue(reader).Value;

                case HandleType.ConstantCharValue:
                    return handle.ToConstantCharValueHandle(reader).GetConstantCharValue(reader).Value;

                case HandleType.ConstantByteValue:
                    return handle.ToConstantByteValueHandle(reader).GetConstantByteValue(reader).Value;

                case HandleType.ConstantSByteValue:
                    return handle.ToConstantSByteValueHandle(reader).GetConstantSByteValue(reader).Value;

                case HandleType.ConstantInt16Value:
                    return handle.ToConstantInt16ValueHandle(reader).GetConstantInt16Value(reader).Value;

                case HandleType.ConstantUInt16Value:
                    return handle.ToConstantUInt16ValueHandle(reader).GetConstantUInt16Value(reader).Value;

                case HandleType.ConstantInt32Value:
                    return handle.ToConstantInt32ValueHandle(reader).GetConstantInt32Value(reader).Value;

                case HandleType.ConstantUInt32Value:
                    return handle.ToConstantUInt32ValueHandle(reader).GetConstantUInt32Value(reader).Value;

                case HandleType.ConstantInt64Value:
                    return handle.ToConstantInt64ValueHandle(reader).GetConstantInt64Value(reader).Value;

                case HandleType.ConstantUInt64Value:
                    return handle.ToConstantUInt64ValueHandle(reader).GetConstantUInt64Value(reader).Value;

                case HandleType.ConstantSingleValue:
                    return handle.ToConstantSingleValueHandle(reader).GetConstantSingleValue(reader).Value;

                case HandleType.ConstantDoubleValue:
                    return handle.ToConstantDoubleValueHandle(reader).GetConstantDoubleValue(reader).Value;

                case HandleType.ConstantReferenceValue:
                    return null;

                default:
                    throw new BadImageFormatException();
            }
        }
    }
}
