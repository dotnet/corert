// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using Internal.TypeSystem.TypesDebugInfo;

using ILCompiler.DependencyAnalysis;

namespace ILCompiler
{
    public class UserDefinedTypeDescriptor
    {
        object _lock = new object();
        NodeFactory _nodeFactory;

        NodeFactory NodeFactory => _nodeFactory;

        bool Is64Bit => NodeFactory.Target.PointerSize == 8;

        TargetAbi Abi => NodeFactory.Target.Abi;

        public UserDefinedTypeDescriptor(ITypesDebugInfoWriter objectWriter, NodeFactory nodeFactory)
        {
            _objectWriter = objectWriter;
            _nodeFactory = nodeFactory;
        }

        // Get type index for use as a variable/parameter
        public uint GetVariableTypeIndex(TypeDesc type)
        {
            lock (_lock)
            {
                return GetVariableTypeIndex(type, true);
            }
        }

        // Get Type index for this pointer of specified type
        public uint GetThisTypeIndex(TypeDesc type)
        {
            lock (_lock)
            {
                uint typeIndex;

                if (_thisTypes.TryGetValue(type, out typeIndex))
                    return typeIndex;

                PointerTypeDescriptor descriptor = new PointerTypeDescriptor();
                // Note the use of GetTypeIndex here instead of GetVariableTypeIndex (We need the type exactly, not a reference to the type (as would happen for arrays/classes), and not a primitive value (as would happen for primitives))
                descriptor.ElementType = GetTypeIndex(type, true);
                descriptor.Is64Bit = Is64Bit ? 1 : 0;
                descriptor.IsConst = 1;
                descriptor.IsReference = 0;

                typeIndex = _objectWriter.GetPointerTypeIndex(descriptor);
                _thisTypes.Add(type, typeIndex);
                return typeIndex;
            }
        }

        // Get type index for method
        public uint GetMethodTypeIndex(MethodDesc method)
        {
            lock (_lock)
            {
                uint typeIndex;

                if (_methodIndices.TryGetValue(method, out typeIndex))
                    return typeIndex;

                MemberFunctionTypeDescriptor descriptor = new MemberFunctionTypeDescriptor();
                MethodSignature signature = method.Signature;

                descriptor.ReturnType = GetVariableTypeIndex(DebuggerCanonicalize(signature.ReturnType));
                descriptor.ThisAdjust = 0;
                descriptor.CallingConvention = 0x4; // Near fastcall
                descriptor.TypeIndexOfThisPointer = signature.IsStatic ? (uint)PrimitiveTypeDescriptor.TYPE_ENUM.T_VOID : GetThisTypeIndex(method.OwningType);
                descriptor.ContainingClass = GetTypeIndex(method.OwningType, true);

                try
                {
                    descriptor.NumberOfArguments = checked((ushort)signature.Length);
                }
                catch (OverflowException)
                {
                    return 0;
                }

                uint[] args = new uint[signature.Length];
                for (int i = 0; i < args.Length; i++)
                    args[i] = GetVariableTypeIndex(DebuggerCanonicalize(signature[i]));


                typeIndex = _objectWriter.GetMemberFunctionTypeIndex(descriptor, args);
                _methodIndices.Add(method, typeIndex);
                return typeIndex;
            }
        }

        // Get type index for specific method by name
        public uint GetMethodFunctionIdTypeIndex(MethodDesc method)
        {
            lock (_lock)
            {
                uint typeIndex;

                if (_methodIdIndices.TryGetValue(method, out typeIndex))
                    return typeIndex;

                MemberFunctionIdTypeDescriptor descriptor = new MemberFunctionIdTypeDescriptor();

                descriptor.MemberFunction = GetMethodTypeIndex(method);
                descriptor.ParentClass = GetTypeIndex(method.OwningType, true);
                descriptor.Name = method.Name;

                typeIndex = _objectWriter.GetMemberFunctionId(descriptor);
                _methodIdIndices.Add(method, typeIndex);
                return typeIndex;
            }
        }

        private TypeDesc DebuggerCanonicalize(TypeDesc type)
        {
            if (type.IsCanonicalSubtype(CanonicalFormKind.Any))
                return type.ConvertToCanonForm(CanonicalFormKind.Specific);

            return type;
        }

        private uint GetVariableTypeIndex(TypeDesc type, bool needsCompleteIndex)
        {
            uint variableTypeIndex = 0;
            if (type.IsPrimitive)
            {
                variableTypeIndex = PrimitiveTypeDescriptor.GetPrimitiveTypeIndex(type);
            }
            else
            {
                type = DebuggerCanonicalize(type);

                if ((type.IsDefType && !type.IsValueType) || type.IsArray)
                {
                    // The type index of a variable/field of a reference type is wrapped 
                    // in a pointer, as these fields are really pointer fields, and the data is on the heap
                    variableTypeIndex = 0;
                    if (_knownReferenceWrappedTypes.TryGetValue(type, out variableTypeIndex))
                    {
                        return variableTypeIndex;
                    }
                    else
                    {
                        uint typeindex = GetTypeIndex(type, false);

                        PointerTypeDescriptor descriptor = new PointerTypeDescriptor();
                        descriptor.ElementType = typeindex;
                        descriptor.Is64Bit = Is64Bit ? 1 : 0;
                        descriptor.IsConst = 0;
                        descriptor.IsReference = 1;

                        variableTypeIndex = _objectWriter.GetPointerTypeIndex(descriptor);
                        _knownReferenceWrappedTypes[type] = variableTypeIndex;

                        return variableTypeIndex;
                    }
                }
                else if (type.IsEnum)
                {
                    // Enum's use the LF_ENUM record as the variable type index, but it is required to also emit a regular structure record for them.

                    if (_enumTypes.TryGetValue(type, out variableTypeIndex))
                        return variableTypeIndex;

                    variableTypeIndex = GetEnumTypeIndex(type);

                    GetTypeIndex(type, false); // Ensure regular structure record created
                }

                variableTypeIndex = GetTypeIndex(type, needsCompleteIndex);
            }
            return variableTypeIndex;
        }

        /// <summary>
        /// Get type index for type without the type being wrapped as a reference (as a variable or field must be)
        /// </summary>
        /// <param name="type"></param>
        /// <param name="needsCompleteType"></param>
        /// <returns></returns>
        private uint GetTypeIndex(TypeDesc type, bool needsCompleteType)
        {
            uint typeIndex = 0;
            if (needsCompleteType ?
                _completeKnownTypes.TryGetValue(type, out typeIndex)
                : _knownTypes.TryGetValue(type, out typeIndex))
            {
                return typeIndex;
            }
            else
            {
                return GetNewTypeIndex(type, needsCompleteType);
            }
        }

        private uint GetNewTypeIndex(TypeDesc type, bool needsCompleteType)
        {
            if (type.IsArray)
            {
                return GetArrayTypeIndex(type);
            }
            else if (type.IsDefType)
            {
                return GetClassTypeIndex(type, needsCompleteType);
            }
            else if (type.IsPointer)
            {
                return GetPointerTypeIndex(((ParameterizedType)type).ParameterType);
            }
            else if (type.IsByRef)
            {
                return GetByRefTypeIndex(((ParameterizedType)type).ParameterType);
            }

            return 0;
        }

        private uint GetPointerTypeIndex(TypeDesc pointeeType)
        {
            uint typeIndex;

            if (_pointerTypes.TryGetValue(pointeeType, out typeIndex))
                return typeIndex;

            PointerTypeDescriptor descriptor = new PointerTypeDescriptor();
            descriptor.ElementType = GetVariableTypeIndex(pointeeType, false);
            descriptor.Is64Bit = Is64Bit ? 1 : 0;
            descriptor.IsConst = 0;
            descriptor.IsReference = 0;

            // Calling GetVariableTypeIndex may have filled in _pointerTypes
            if (_pointerTypes.TryGetValue(pointeeType, out typeIndex))
                return typeIndex;

            typeIndex = _objectWriter.GetPointerTypeIndex(descriptor);
            _pointerTypes.Add(pointeeType, typeIndex);
            return typeIndex;
        }

        private uint GetByRefTypeIndex(TypeDesc pointeeType)
        {
            uint typeIndex;

            if (_byRefTypes.TryGetValue(pointeeType, out typeIndex))
                return typeIndex;

            PointerTypeDescriptor descriptor = new PointerTypeDescriptor();
            descriptor.ElementType = GetVariableTypeIndex(pointeeType, false);
            descriptor.Is64Bit = Is64Bit ? 1 : 0;
            descriptor.IsConst = 0;
            descriptor.IsReference = 1;

            // Calling GetVariableTypeIndex may have filled in _byRefTypes
            if (_byRefTypes.TryGetValue(pointeeType, out typeIndex))
                return typeIndex;

            typeIndex = _objectWriter.GetPointerTypeIndex(descriptor);
            _byRefTypes.Add(pointeeType, typeIndex);
            return typeIndex;
        }

        private uint GetEnumTypeIndex(TypeDesc type)
        {
            System.Diagnostics.Debug.Assert(type.IsEnum, "GetEnumTypeIndex was called with wrong type");
            DefType defType = type as DefType;
            System.Diagnostics.Debug.Assert(defType != null, "GetEnumTypeIndex was called with non def type");
            List<FieldDesc> fieldsDescriptors = new List<FieldDesc>();
            foreach (var field in defType.GetFields())
            {
                if (field.IsLiteral)
                {
                    fieldsDescriptors.Add(field);
                }
            }
            EnumTypeDescriptor enumTypeDescriptor = new EnumTypeDescriptor
            {
                ElementCount = (ulong)fieldsDescriptors.Count,
                ElementType = PrimitiveTypeDescriptor.GetPrimitiveTypeIndex(defType.UnderlyingType),
                Name = _objectWriter.GetMangledName(type),
            };
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
            return typeIndex;
        }

        private uint GetArrayTypeIndex(TypeDesc type)
        {
            System.Diagnostics.Debug.Assert(type.IsArray, "GetArrayTypeIndex was called with wrong type");
            ArrayType arrayType = (ArrayType)type;

            uint elementSize = (uint)type.Context.Target.PointerSize;
            LayoutInt layoutElementSize = arrayType.GetElementSize();
            if (!layoutElementSize.IsIndeterminate)
                elementSize = (uint)layoutElementSize.AsInt;

            ArrayTypeDescriptor arrayTypeDescriptor = new ArrayTypeDescriptor
            {
                Rank = (uint)arrayType.Rank,
                ElementType = GetVariableTypeIndex(arrayType.ElementType, false),
                Size = elementSize,
                IsMultiDimensional = arrayType.IsMdArray ? 1 : 0
            };

            ClassTypeDescriptor classDescriptor = new ClassTypeDescriptor
            {
                IsStruct = 0,
                Name = _objectWriter.GetMangledName(type),
                BaseClassId = GetTypeIndex(arrayType.BaseType, false)
            };

            uint typeIndex = _objectWriter.GetArrayTypeIndex(classDescriptor, arrayTypeDescriptor);
            _knownTypes[type] = typeIndex;
            _completeKnownTypes[type] = typeIndex;
            return typeIndex;
        }

        private ulong GetEnumRecordValue(FieldDesc field)
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

        TypeDesc GetFieldDebugType(FieldDesc field)
        {
            TypeDesc type = field.FieldType;

            // TODO: check the type's generic complexity
            if (NodeFactory.LazyGenericsPolicy.UsesLazyGenerics(type))
            {
                type = type.ConvertToCanonForm(CanonicalFormKind.Specific);
            }

            return type;
        }

        private uint GetClassTypeIndex(TypeDesc type, bool needsCompleteType)
        {
            DefType defType = type as DefType;
            System.Diagnostics.Debug.Assert(defType != null, "GetClassTypeIndex was called with non def type");
            ClassTypeDescriptor classTypeDescriptor = new ClassTypeDescriptor
            {
                IsStruct = type.IsValueType ? 1 : 0,
                Name = _objectWriter.GetMangledName(type),
                BaseClassId = 0
            };

            uint typeIndex = _objectWriter.GetClassTypeIndex(classTypeDescriptor);
            _knownTypes[type] = typeIndex;

            if (type.HasBaseType && !type.IsValueType)
            {
                classTypeDescriptor.BaseClassId = GetTypeIndex(defType.BaseType, true);
            }

            List<DataFieldDescriptor> fieldsDescs = new List<DataFieldDescriptor>();
            List<DataFieldDescriptor> nonGcStaticFields = new List<DataFieldDescriptor>();
            List<DataFieldDescriptor> gcStaticFields = new List<DataFieldDescriptor>();
            List<DataFieldDescriptor> threadStaticFields = new List<DataFieldDescriptor>();

            bool isCanonical = defType.IsCanonicalSubtype(CanonicalFormKind.Any);

            foreach (var fieldDesc in defType.GetFields())
            {
                if (fieldDesc.HasRva || fieldDesc.IsLiteral)
                    continue;

                if (isCanonical && fieldDesc.IsStatic)
                    continue;

                LayoutInt fieldOffset = fieldDesc.Offset;
                int fieldOffsetEmit = fieldOffset.IsIndeterminate ? 0xBAAD : fieldOffset.AsInt;
                DataFieldDescriptor field = new DataFieldDescriptor
                {
                    FieldTypeIndex = GetVariableTypeIndex(GetFieldDebugType(fieldDesc), false),
                    Offset = (ulong)fieldOffsetEmit,
                    Name = fieldDesc.Name
                };

                if (fieldDesc.IsStatic)
                {
                    if (fieldDesc.IsThreadStatic)
                        threadStaticFields.Add(field);
                    else if (fieldDesc.HasGCStaticBase)
                        gcStaticFields.Add(field);
                    else
                        nonGcStaticFields.Add(field);
                }
                else
                {
                    fieldsDescs.Add(field);
                }
            }

            InsertStaticFieldRegionMember(fieldsDescs, defType, nonGcStaticFields, WindowsNodeMangler.NonGCStaticMemberName, "__type_" + WindowsNodeMangler.NonGCStaticMemberName, false);
            InsertStaticFieldRegionMember(fieldsDescs, defType, gcStaticFields, WindowsNodeMangler.GCStaticMemberName, "__type_" + WindowsNodeMangler.GCStaticMemberName, Abi == TargetAbi.CoreRT);
            InsertStaticFieldRegionMember(fieldsDescs, defType, threadStaticFields, WindowsNodeMangler.ThreadStaticMemberName, "__type_" + WindowsNodeMangler.ThreadStaticMemberName, Abi == TargetAbi.CoreRT);

            DataFieldDescriptor[] fields = new DataFieldDescriptor[fieldsDescs.Count];
            for (int i = 0; i < fieldsDescs.Count; ++i)
            {
                fields[i] = fieldsDescs[i];
            }

            LayoutInt elementSize = defType.GetElementSize();
            int elementSizeEmit = elementSize.IsIndeterminate ? 0xBAAD : elementSize.AsInt;
            ClassFieldsTypeDescriptor fieldsDescriptor = new ClassFieldsTypeDescriptor
            {
                Size = (ulong)elementSizeEmit,
                FieldsCount = fieldsDescs.Count
            };

            uint completeTypeIndex = _objectWriter.GetCompleteClassTypeIndex(classTypeDescriptor, fieldsDescriptor, fields);
            _completeKnownTypes[type] = completeTypeIndex;

            if (needsCompleteType)
                return completeTypeIndex;
            else
                return typeIndex;
        }

        private void InsertStaticFieldRegionMember(List<DataFieldDescriptor> fieldDescs, DefType defType, List<DataFieldDescriptor> staticFields, string staticFieldForm, string staticFieldFormTypePrefix, bool staticDataInObject)
        {
            if (staticFields != null && (staticFields.Count > 0))
            {
                // Generate struct symbol for type describing individual fields of the statics region
                ClassFieldsTypeDescriptor fieldsDescriptor = new ClassFieldsTypeDescriptor
                {
                    Size = (ulong)0,
                    FieldsCount = staticFields.Count
                };

                ClassTypeDescriptor classTypeDescriptor = new ClassTypeDescriptor
                {
                    IsStruct = !staticDataInObject ? 1 : 0,
                    Name = staticFieldFormTypePrefix + _objectWriter.GetMangledName(defType),
                    BaseClassId = 0
                };

                if (staticDataInObject)
                {
                    classTypeDescriptor.BaseClassId = GetTypeIndex(defType.Context.GetWellKnownType(WellKnownType.Object), true);
                }

                uint staticFieldRegionTypeIndex = _objectWriter.GetCompleteClassTypeIndex(classTypeDescriptor, fieldsDescriptor, staticFields.ToArray());
                uint staticFieldRegionSymbolTypeIndex = staticFieldRegionTypeIndex;

                // This means that access to this static region is done via a double indirection
                if (staticDataInObject)
                {
                    PointerTypeDescriptor pointerTypeDescriptor = new PointerTypeDescriptor();
                    pointerTypeDescriptor.Is64Bit = Is64Bit ? 1 : 0;
                    pointerTypeDescriptor.IsConst = 0;
                    pointerTypeDescriptor.IsReference = 0;
                    pointerTypeDescriptor.ElementType = staticFieldRegionTypeIndex;

                    uint intermediatePointerDescriptor = _objectWriter.GetPointerTypeIndex(pointerTypeDescriptor);
                    pointerTypeDescriptor.ElementType = intermediatePointerDescriptor;
                    staticFieldRegionSymbolTypeIndex = _objectWriter.GetPointerTypeIndex(pointerTypeDescriptor);
                }

                DataFieldDescriptor staticRegionField = new DataFieldDescriptor
                {
                    FieldTypeIndex = staticFieldRegionSymbolTypeIndex,
                    Offset = 0xFFFFFFFF,
                    Name = staticFieldForm
                };

                fieldDescs.Add(staticRegionField);
            }
        }

        private ITypesDebugInfoWriter _objectWriter;
        private Dictionary<TypeDesc, uint> _knownTypes = new Dictionary<TypeDesc, uint>();
        private Dictionary<TypeDesc, uint> _completeKnownTypes = new Dictionary<TypeDesc, uint>();
        private Dictionary<TypeDesc, uint> _knownReferenceWrappedTypes = new Dictionary<TypeDesc, uint>();
        private Dictionary<TypeDesc, uint> _pointerTypes = new Dictionary<TypeDesc, uint>();
        private Dictionary<TypeDesc, uint> _enumTypes = new Dictionary<TypeDesc, uint>();
        private Dictionary<TypeDesc, uint> _byRefTypes = new Dictionary<TypeDesc, uint>();
        private Dictionary<TypeDesc, uint> _thisTypes = new Dictionary<TypeDesc, uint>();
        private Dictionary<MethodDesc, uint> _methodIndices = new Dictionary<MethodDesc, uint>();
        private Dictionary<MethodDesc, uint> _methodIdIndices = new Dictionary<MethodDesc, uint>();

        public ICollection<KeyValuePair<TypeDesc, uint>> CompleteKnownTypes => _completeKnownTypes;
    }
}
