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

        public uint GetVariableTypeIndex(TypeDesc type, bool needsCompleteIndex)
        {
            uint typeIndex = 0;
            if (type.IsPrimitive)
            {
                typeIndex = PrimitiveTypeDescriptor.GetPrimitiveTypeIndex(type);
            }
            else
            {
                typeIndex = GetTypeIndex(type, needsCompleteIndex);
            }
            return typeIndex;
        }

        public uint GetTypeIndex(TypeDesc type, bool needsCompleteType)
        {
            uint variableTypeIndex = 0;
            if (needsCompleteType ?
                _completeKnownTypes.TryGetValue(type, out variableTypeIndex)
                : _knownTypes.TryGetValue(type, out variableTypeIndex))
            {
                return variableTypeIndex;
            }
            else
            {
                return GetNewTypeIndex(type, needsCompleteType);
            }
        }

        private uint GetNewTypeIndex(TypeDesc type, bool needsCompleteType)
        {

            if (type.IsEnum)
            {
                return GetEnumTypeIndex(type);
            }
            else if (type.IsDefType)
            {
                return GetClassTypeIndex(type, needsCompleteType);
            }
            return 0;
        }

        public uint GetEnumTypeIndex(TypeDesc type)
        {
            System.Diagnostics.Debug.Assert(type.IsEnum, "GetEnumTypeIndex was called with wrong type");
            DefType defType = type as DefType;
            System.Diagnostics.Debug.Assert(defType != null, "GetEnumTypeIndex was called with non def type");
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
            enumTypeDescriptor.ElementType = PrimitiveTypeDescriptor.GetPrimitiveTypeIndex(defType.UnderlyingType);
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
            uint typeIndex = _objectWriter.GetEnumTypeIndex(enumTypeDescriptor, typeRecords);
            _knownTypes[type] = typeIndex;
            _completeKnownTypes[type] = typeIndex;
            return typeIndex;
        }

        public ulong GetEnumRecordValue(FieldDesc field)
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

        public uint GetClassTypeIndex(TypeDesc type, bool needsCompleteType)
        {
            DefType defType = type as DefType;
            System.Diagnostics.Debug.Assert(defType != null, "GetClassTypeIndex was called with non def type");
            ClassTypeDescriptor classTypeDescriptor = new ClassTypeDescriptor();
            classTypeDescriptor.IsStruct = type.IsValueType ? 1 : 0;
            classTypeDescriptor.Name = defType.Name;
            classTypeDescriptor.UniqueName = defType.GetFullName();
            classTypeDescriptor.BaseClassId = 0;

            uint typeIndex = _objectWriter.GetClassTypeIndex(classTypeDescriptor);
            _knownTypes[type] = typeIndex;

            if (type.HasBaseType && !type.IsValueType)
            {
                classTypeDescriptor.BaseClassId = GetVariableTypeIndex(defType.BaseType, false);
            }

            List<DataFieldDescriptor> fieldsDescs = new List<DataFieldDescriptor>();
            foreach (var fieldDesc in defType.GetFields())
            {
                if (fieldDesc.HasRva || fieldDesc.IsLiteral)
                    continue;
                DataFieldDescriptor field = new DataFieldDescriptor();
                field.FieldTypeIndex = GetVariableTypeIndex(fieldDesc.FieldType, false);
                field.Offset = (ulong)fieldDesc.Offset.AsInt;
                field.Name = fieldDesc.Name;
                fieldsDescs.Add(field);
            }

            DataFieldDescriptor[] fields = new DataFieldDescriptor[fieldsDescs.Count];
            for (int i = 0; i < fieldsDescs.Count; ++i)
            {
                fields[i] = fieldsDescs[i];
            }
            ClassFieldsTypeDescriptor fieldsDescriptor = new ClassFieldsTypeDescriptor();
            fieldsDescriptor.FieldsCount = fieldsDescs.Count;
            fieldsDescriptor.Size = (ulong)defType.GetElementSize().AsInt;

            uint completeTypeIndex = _objectWriter.GetCompleteClassTypeIndex(classTypeDescriptor, fieldsDescriptor, fields);
            _completeKnownTypes[type] = completeTypeIndex;

            if (needsCompleteType)
                return completeTypeIndex;
            else
                return typeIndex;
        }

        private ITypesDebugInfoWriter _objectWriter;
        private Dictionary<TypeDesc, uint> _knownTypes = new Dictionary<TypeDesc, uint>();
        private Dictionary<TypeDesc, uint> _completeKnownTypes = new Dictionary<TypeDesc, uint>();
    }
}
