// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Reflection.Metadata;
using Internal.TypeSystem.Ecma;

namespace Internal.TypeSystem.TypesDebugInfo
{
    public class UserDefinedTypeDescriptor
    {
        public UserDefinedTypeDescriptor(ITypesDebugInfoWriter objectWriter)
        {
            _objectWriter = objectWriter;
        }
        public uint GetTypeIndex(TypeDesc type)
        {
            uint variableTypeIndex = 0;
            if (!_knownTypes.TryGetValue(type, out variableTypeIndex))
            {
                variableTypeIndex = GetNewTypeIndex(type);
            }
            return variableTypeIndex;          
        }

        private uint GetNewTypeIndex(TypeDesc type)
        {
            switch (type.Category)
            {
                case TypeFlags.ValueType:
                case TypeFlags.Class:
                    return GetClassTypeIndex(type);
                case TypeFlags.Enum:
                    return GetEnumTypeIndex(type);
            }
            return 0;
        }

        public uint GetEnumTypeIndex(TypeDesc type)
        {
            DefType defType = type as DefType;
            if (defType == null)
            {
                return 0;
            }
            EnumTypeDescriptor enumTypeDescriptor = new EnumTypeDescriptor();
            List<FieldDesc> fieldsDescriptors = new List<FieldDesc>();
            foreach (var field in defType.GetFields())
            {
                if (field.IsLiteral)
                {
                    fieldsDescriptors.Add(field);
                }
            }
            enumTypeDescriptor.ElementCount = (ulong)fieldsDescriptors.Count;
            enumTypeDescriptor.ElementType = _objectWriter.GetVariableTypeIndex(defType.UnderlyingType);
            enumTypeDescriptor.Name = defType.Name;
            enumTypeDescriptor.UniqueName = defType.GetFullName();
            EnumRecordTypeDescriptor[] typeRecords = new EnumRecordTypeDescriptor[enumTypeDescriptor.ElementCount];
            for (int i = 0; i < fieldsDescriptors.Count; ++i)
            {
                FieldDesc field = fieldsDescriptors[i];
                EnumRecordTypeDescriptor recordTypeDescriptor;
                recordTypeDescriptor.Value = GetEnumRecordValue(field);
                recordTypeDescriptor.Name = field.Name;
                typeRecords[i] = recordTypeDescriptor;
            }
            return _knownTypes[type] = _objectWriter.GetEnumTypeIndex(enumTypeDescriptor, typeRecords);
        }

        public ulong  GetEnumRecordValue(FieldDesc field)
        {
            var ecmaField = field as EcmaField;
            if (ecmaField != null)
            {
                MetadataReader reader = ecmaField.MetadataReader;
                FieldDefinition fieldDef = reader.GetFieldDefinition(ecmaField.Handle);
                ConstantHandle defaultValueHandle = fieldDef.GetDefaultValue();
                if (!defaultValueHandle.IsNil)
                {
                    return HandleConstant(ecmaField.Module, defaultValueHandle);
                }             
            }
            return 0;
        }

        private ulong HandleConstant(EcmaModule module, ConstantHandle constantHandle)
        {
            MetadataReader reader = module.MetadataReader;
            Constant constant = reader.GetConstant(constantHandle);
            BlobReader blob = reader.GetBlobReader(constant.Value);
            switch (constant.TypeCode)
            {
                case ConstantTypeCode.Byte:
                    return (ulong)blob.ReadByte();
                case ConstantTypeCode.Int16:
                    return (ulong)blob.ReadInt16();
                case ConstantTypeCode.Int32:
                    return (ulong)blob.ReadInt32();
                case ConstantTypeCode.Int64:
                    return (ulong)blob.ReadInt64();
                case ConstantTypeCode.SByte:
                    return (ulong)blob.ReadSByte();
                case ConstantTypeCode.UInt16:
                    return (ulong)blob.ReadUInt16();
                case ConstantTypeCode.UInt32:
                    return (ulong)blob.ReadUInt32();
                case ConstantTypeCode.UInt64:
                    return (ulong)blob.ReadUInt64();
            }
            System.Diagnostics.Debug.Assert(false);
            return 0;
        }

        public uint GetClassTypeIndex(TypeDesc type)
        {
            DefType defType = type as DefType;
            if (defType == null)
            {
                return 0;
            }
            ClassTypeDescriptor classTypeDescriptor = new ClassTypeDescriptor();
            classTypeDescriptor.IsStruct = type.IsValueType? 1:0;
            classTypeDescriptor.Name = defType.Name;
            classTypeDescriptor.UniqueName = defType.GetFullName();
            classTypeDescriptor.BaseClassId = 0;
            if (type.HasBaseType && !type.IsValueType)
            {
                classTypeDescriptor.BaseClassId = _objectWriter.GetVariableTypeIndex(defType.BaseType);
            }
            uint result = _knownTypes[type] = _objectWriter.GetClassTypeIndex(classTypeDescriptor);

            List <DataFieldDescriptor> fieldsDescs = new List<DataFieldDescriptor>();
            foreach (var fieldDesc in defType.GetFields())
            {
                if (fieldDesc.HasRva || fieldDesc.IsLiteral)
                    continue;
                DataFieldDescriptor field = new DataFieldDescriptor();
                field.FieldTypeIndex = _objectWriter.GetVariableTypeIndex(fieldDesc.FieldType);
                field.Offset = fieldDesc.Offset.AsInt;
                field.Name = fieldDesc.Name;
                fieldsDescs.Add(field);                
            }

            DataFieldDescriptor[] fields = new DataFieldDescriptor[fieldsDescs.Count];
            for (int i = 0; i < fieldsDescs.Count; ++i)
            {
                fields[i] = fieldsDescs[i];
            }
            ClassFieldsTypeDescriptior fiedlsDescriptor = new ClassFieldsTypeDescriptior();
            fiedlsDescriptor.FieldsCount = fieldsDescs.Count;
            fiedlsDescriptor.Size = defType.GetElementSize().AsInt;

            _objectWriter.CompleteClassDescription(classTypeDescriptor, fiedlsDescriptor, fields);
            return result;
        }

        private ITypesDebugInfoWriter _objectWriter;
        private Dictionary<TypeDesc, uint> _knownTypes = new Dictionary<TypeDesc, uint>();
    }
}
