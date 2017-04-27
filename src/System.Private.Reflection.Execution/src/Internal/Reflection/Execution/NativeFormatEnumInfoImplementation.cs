// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Reflection;
using global::System.Collections.Generic;

using global::Internal.Runtime.Augments;

using global::Internal.Reflection.Core;
using global::Internal.Reflection.Core.Execution;

using global::Internal.Metadata.NativeFormat;

namespace Internal.Reflection.Execution
{
    internal sealed class NativeFormatEnumInfoImplementation : EnumInfoImplementation
    {
        public NativeFormatEnumInfoImplementation(Type enumType, MetadataReader reader, TypeDefinitionHandle typeDefinitionHandle) : base(enumType)
        {
            _reader = reader;
            _typeDefinition = typeDefinitionHandle.GetTypeDefinition(reader);
        }

        protected sealed override KeyValuePair<String, ulong>[] ReadNamesAndValues()
        {
            LowLevelList<KeyValuePair<String, ulong>> namesAndUnboxedValues = new LowLevelList<KeyValuePair<String, ulong>>();
            MetadataReader reader = _reader;
            foreach (FieldHandle fieldHandle in _typeDefinition.Fields)
            {
                Field field = fieldHandle.GetField(reader);
                if (0 != (field.Flags & FieldAttributes.Static))
                {
                    String name = field.Name.GetString(reader);
                    Handle valueHandle = field.DefaultValue;
                    ulong lValue = ReadUnboxedEnumValue(reader, valueHandle);
                    namesAndUnboxedValues.Add(new KeyValuePair<String, ulong>(name, lValue));
                }
            }

            return namesAndUnboxedValues.ToArray();
        }

        //
        // This returns the underlying enum values as "ulong" regardless of the actual underlying type. Signed integral types 
        // get sign-extended into the 64-bit value, unsigned types get zero-extended.
        //
        private static ulong ReadUnboxedEnumValue(MetadataReader reader, Handle valueHandle)
        {
            HandleType handleType = valueHandle.HandleType;
            switch (handleType)
            {
                case HandleType.ConstantBooleanValue:
                    {
                        bool v = valueHandle.ToConstantBooleanValueHandle(reader).GetConstantBooleanValue(reader).Value;
                        return v ? 1UL : 0UL;
                    }

                case HandleType.ConstantCharValue:
                    {
                        char v = valueHandle.ToConstantCharValueHandle(reader).GetConstantCharValue(reader).Value;
                        return (ulong)(long)v;
                    }

                case HandleType.ConstantByteValue:
                    {
                        byte v = valueHandle.ToConstantByteValueHandle(reader).GetConstantByteValue(reader).Value;
                        return (ulong)(long)v;
                    }

                case HandleType.ConstantSByteValue:
                    {
                        sbyte v = valueHandle.ToConstantSByteValueHandle(reader).GetConstantSByteValue(reader).Value;
                        return (ulong)(long)v;
                    }

                case HandleType.ConstantUInt16Value:
                    {
                        UInt16 v = valueHandle.ToConstantUInt16ValueHandle(reader).GetConstantUInt16Value(reader).Value;
                        return (ulong)(long)v;
                    }

                case HandleType.ConstantInt16Value:
                    {
                        Int16 v = valueHandle.ToConstantInt16ValueHandle(reader).GetConstantInt16Value(reader).Value;
                        return (ulong)(long)v;
                    }

                case HandleType.ConstantUInt32Value:
                    {
                        UInt32 v = valueHandle.ToConstantUInt32ValueHandle(reader).GetConstantUInt32Value(reader).Value;
                        return (ulong)(long)v;
                    }

                case HandleType.ConstantInt32Value:
                    {
                        Int32 v = valueHandle.ToConstantInt32ValueHandle(reader).GetConstantInt32Value(reader).Value;
                        return (ulong)(long)v;
                    }

                case HandleType.ConstantUInt64Value:
                    {
                        UInt64 v = valueHandle.ToConstantUInt64ValueHandle(reader).GetConstantUInt64Value(reader).Value;
                        return (ulong)(long)v;
                    }

                case HandleType.ConstantInt64Value:
                    {
                        Int64 v = valueHandle.ToConstantInt64ValueHandle(reader).GetConstantInt64Value(reader).Value;
                        return (ulong)(long)v;
                    }

                default:
                    throw new BadImageFormatException();
            }
        }

        private readonly MetadataReader _reader;
        private readonly TypeDefinition _typeDefinition;
    }
}
