// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Reflection.Runtime.General;

using Internal.Reflection.Extensions.NonPortable;

using System.Reflection.Metadata;

namespace System.Reflection.Runtime.General.EcmaFormat
{
    internal static class DefaultValueProcessing
    {
        public static bool GetDefaultValueIfAny(MetadataReader reader, ref FieldDefinition field, FieldInfo fieldInfo, bool raw, out object defaultValue)
        {
            if (0 != (field.Attributes & FieldAttributes.HasDefault))
            {
                defaultValue = ConstantValueAsObject(field.GetDefaultValue(), reader, fieldInfo.FieldType, raw);
                return true;
            }
            else
            {
                return Helpers.GetCustomAttributeDefaultValueIfAny(fieldInfo.CustomAttributes, raw, out defaultValue);
            }
        }

        public static bool GetDefaultValueIfAny(MetadataReader reader, ref Parameter parameter, ParameterInfo parameterInfo, bool raw, out object defaultValue)
        {
            if (0 != (parameter.Attributes & ParameterAttributes.HasDefault))
            {
                defaultValue = ConstantValueAsObject(parameter.GetDefaultValue(), reader, parameterInfo.ParameterType, raw);
                return true;
            }
            else
            {
                return Helpers.GetCustomAttributeDefaultValueIfAny(parameterInfo.CustomAttributes, raw, out defaultValue);
            }
        }

        public static bool GetDefaultValueIfAny(MetadataReader reader, ref PropertyDefinition property, PropertyInfo propertyInfo, bool raw, out object defaultValue)
        {
            if (0 != (property.Attributes & PropertyAttributes.HasDefault))
            {
                defaultValue = ConstantValueAsObject(property.GetDefaultValue(), reader, propertyInfo.PropertyType, raw);
                return true;
            }
            else
            {
                // Custom attributes default values cannot be specified on properties
                defaultValue = null;
                return false;
            }
        }

        private static object ConstantValueAsObject(ConstantHandle constantHandle, MetadataReader metadataReader, Type declaredType, bool raw)
        {
            object defaultValue = constantHandle.ParseConstantValue(metadataReader);
            if ((!raw) && declaredType.IsEnum && defaultValue != null)
                defaultValue = Enum.ToObject(declaredType, defaultValue);
            return defaultValue;
        }
    }
}
