// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Collections.Generic;

using Internal.Runtime.Augments;

using Internal.Reflection.Core;
using Internal.Reflection.Core.Execution;

using System.Reflection.Metadata;

namespace Internal.Reflection.Execution
{
    internal sealed class EcmaFormatEnumInfoImplementation : EnumInfoImplementation
    {
        public EcmaFormatEnumInfoImplementation(Type enumType, MetadataReader reader, TypeDefinitionHandle typeDefinitionHandle) : base(enumType)
        {
            _reader = reader;
            _typeDefinition = reader.GetTypeDefinition(typeDefinitionHandle);
        }

        protected sealed override KeyValuePair<String, ulong>[] ReadNamesAndValues()
        {
            LowLevelList<KeyValuePair<String, ulong>> namesAndUnboxedValues = new LowLevelList<KeyValuePair<String, ulong>>();
            MetadataReader reader = _reader;
            foreach (FieldDefinitionHandle fieldHandle in _typeDefinition.GetFields())
            {
                FieldDefinition field = reader.GetFieldDefinition(fieldHandle);
                if (0 != (field.Attributes & FieldAttributes.Static))
                {
                    String name = reader.GetString(field.Name);

                    if ((field.Attributes & FieldAttributes.HasDefault) != FieldAttributes.HasDefault)
                        throw new BadImageFormatException();

                    ConstantHandle valueHandle = field.GetDefaultValue();

                    ulong ulValue = ReadUnboxedEnumValue(reader, valueHandle);
                    namesAndUnboxedValues.Add(new KeyValuePair<String, ulong>(name, ulValue));
                }
            }

            return namesAndUnboxedValues.ToArray();
        }

        
        //
        // This returns the underlying enum values as "ulong" regardless of the actual underlying type. Signed integral types 
        // get sign-extended into the 64-bit value, unsigned types get zero-extended.
        //
        public static ulong ReadUnboxedEnumValue(MetadataReader metadataReader, ConstantHandle constantHandle)
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
                    return reader.ReadBoolean() ? 1UL : 0UL;;

                case ConstantTypeCode.Char:
                    return (ulong)(long)reader.ReadChar();

                case ConstantTypeCode.SByte:
                    return (ulong)(long)reader.ReadSByte();

                case ConstantTypeCode.Int16:
                    return (ulong)(long)reader.ReadInt16();

                case ConstantTypeCode.Int32:
                    return (ulong)(long)reader.ReadInt32();

                case ConstantTypeCode.Int64:
                    return (ulong)(long)reader.ReadInt64();

                case ConstantTypeCode.Byte:
                    return (ulong)(long)reader.ReadByte();

                case ConstantTypeCode.UInt16:
                    return (ulong)(long)reader.ReadUInt16();

                case ConstantTypeCode.UInt32:
                    return (ulong)(long)reader.ReadUInt32();

                case ConstantTypeCode.UInt64:
                    return (ulong)(long)reader.ReadUInt64();
            }

            throw new BadImageFormatException();
        } 

        private readonly MetadataReader _reader;
        private readonly TypeDefinition _typeDefinition;
    }
}
