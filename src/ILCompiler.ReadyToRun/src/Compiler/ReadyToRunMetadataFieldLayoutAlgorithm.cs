// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using ILCompiler.DependencyAnalysis.ReadyToRun;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler
{
    internal class ReadyToRunMetadataFieldLayoutAlgorithm : MetadataFieldLayoutAlgorithm
    {
        /// <summary>
        /// Map from EcmaModule instances to field layouts within the individual modules.
        /// </summary>
        private ModuleFieldLayoutMap _moduleFieldLayoutMap;

        public ReadyToRunMetadataFieldLayoutAlgorithm()
        {
            _moduleFieldLayoutMap = new ModuleFieldLayoutMap();
        }

        public override ComputedStaticFieldLayout ComputeStaticFieldLayout(DefType defType, StaticLayoutKind layoutKind)
        {
            ComputedStaticFieldLayout layout = new ComputedStaticFieldLayout();
            if (defType.GetTypeDefinition() is EcmaType ecmaType)
            {
                // ECMA types are the only ones that can have statics
                ModuleFieldLayout moduleFieldLayout = _moduleFieldLayoutMap.GetOrCreateValue(ecmaType.EcmaModule);
                layout.GcStatics = moduleFieldLayout.GcStatics;
                layout.NonGcStatics = moduleFieldLayout.NonGcStatics;
                layout.ThreadGcStatics = moduleFieldLayout.ThreadGcStatics;
                layout.ThreadNonGcStatics = moduleFieldLayout.ThreadNonGcStatics;
                if (defType is EcmaType nonGenericType)
                {
                    moduleFieldLayout.TypeToFieldMap.TryGetValue(nonGenericType.Handle, out layout.Offsets);
                }
                else if (defType is InstantiatedType instantiatedType)
                {
                    layout.Offsets = _moduleFieldLayoutMap.GetOrAddDynamicLayout(defType, moduleFieldLayout);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            return layout;
        }

        /// <summary>
        /// Map from modules to their static field layouts.
        /// </summary>
        private class ModuleFieldLayoutMap : LockFreeReaderHashtable<EcmaModule, ModuleFieldLayout>
        {
            /// <summary>
            /// In various helper structures, we refer to regular vs. thread-local statics via indices 0-1.
            /// </summary>
            private const int StaticIndexRegular = 0;

            /// <summary>
            /// In various helper structures, we refer to regular vs. thread-local statics via indices 0-1.
            /// </summary>
            private const int StaticIndexThreadLocal = 1;

            /// <summary>
            /// Number of elements in a helper structure intended to comprise a regular and a thread-local
            /// statics variant.
            /// </summary>
            private const int StaticIndexCount = 2;

            /// <summary>
            /// CoreCLR DomainLocalModule::OffsetOfDataBlob() / sizeof(void *)
            /// </summary>
            private const int DomainLocalModuleDataBlobOffsetAsIntPtrCount = 6;

            /// <summary>
            /// CoreCLR ThreadLocalModule::OffsetOfDataBlob() / sizeof(void *)
            /// </summary>
            private const int ThreadLocalModuleDataBlobOffsetAsIntPtrCount = 3;

            /// <summary>
            /// CoreCLR DomainLocalModule::NormalDynamicEntry::OffsetOfDataBlob for 32-bit platforms
            /// </summary>
            private const int DomainLocalModuleNormalDynamicEntryOffsetOfDataBlob32Bit = 4;

            /// <summary>
            /// CoreCLR DomainLocalModule::NormalDynamicEntry::OffsetOfDataBlob for 64-bit platforms
            /// </summary>
            private const int DomainLocalModuleNormalDynamicEntryOffsetOfDataBlob64Bit = 16;

            protected override bool CompareKeyToValue(EcmaModule key, ModuleFieldLayout value)
            {
                return key == value.Module;
            }

            protected override bool CompareValueToValue(ModuleFieldLayout value1, ModuleFieldLayout value2)
            {
                return value1.Module == value2.Module;
            }

            protected override ModuleFieldLayout CreateValueFromKey(EcmaModule module)
            {
                int typeCountInModule = module.MetadataReader.GetTableRowCount(TableIndex.TypeDef);
                int pointerSize = module.Context.Target.PointerSize;

                // 0 corresponds to "normal" statics, 1 to thread-local statics
                LayoutInt[] gcStatics = new LayoutInt[StaticIndexCount]
                {
                    LayoutInt.Zero,
                    LayoutInt.Zero
                };
                LayoutInt[] nonGcStatics = new LayoutInt[StaticIndexCount]
                {
                    new LayoutInt(DomainLocalModuleDataBlobOffsetAsIntPtrCount * pointerSize + typeCountInModule),
                    new LayoutInt(ThreadLocalModuleDataBlobOffsetAsIntPtrCount * pointerSize + typeCountInModule),
                };
                Dictionary<TypeDefinitionHandle, FieldAndOffset[]> typeToFieldMap = new Dictionary<TypeDefinitionHandle, FieldAndOffset[]>();

                foreach (TypeDefinitionHandle typeDefHandle in module.MetadataReader.TypeDefinitions)
                {
                    TypeDefinition typeDef = module.MetadataReader.GetTypeDefinition(typeDefHandle);
                    List<FieldAndOffset> fieldsForType = null;
                    if (typeDef.GetGenericParameters().Count != 0)
                    {
                        // Generic types are exempt from the static field layout algorithm, see
                        // <a href="https://github.com/dotnet/coreclr/blob/659af58047a949ed50d11101708538d2e87f2568/src/vm/ceeload.cpp#L2049">this check</a>.
                        continue;
                    }

                    foreach (FieldDefinitionHandle fieldDefHandle in typeDef.GetFields())
                    {
                        FieldDefinition fieldDef = module.MetadataReader.GetFieldDefinition(fieldDefHandle);
                        if ((fieldDef.Attributes & (FieldAttributes.Static | FieldAttributes.Literal)) == FieldAttributes.Static)
                        {
                            int index = (IsFieldThreadStatic(in fieldDef, module.MetadataReader) ? StaticIndexThreadLocal : StaticIndexRegular);
                            int alignment;
                            int size;
                            bool isGcPointerField;
                            bool isGcBoxedField;

                            CorElementType corElementType;
                            EntityHandle valueTypeHandle;

                            GetFieldElementTypeAndValueTypeHandle(in fieldDef, module.MetadataReader, out corElementType, out valueTypeHandle);
                            FieldDesc fieldDesc = module.GetField(fieldDefHandle);

                            GetElementTypeInfo(module, fieldDesc, valueTypeHandle, corElementType, pointerSize, out alignment, out size, out isGcPointerField, out isGcBoxedField);

                            LayoutInt offset = LayoutInt.Zero;
                            if (size != 0)
                            {
                                offset = LayoutInt.AlignUp(nonGcStatics[index], new LayoutInt(alignment));
                                nonGcStatics[index] = offset + new LayoutInt(size);
                            }
                            if (isGcPointerField || isGcBoxedField)
                            {
                                offset = LayoutInt.AlignUp(gcStatics[index], new LayoutInt(pointerSize));
                                gcStatics[index] = offset + new LayoutInt(pointerSize);
                            }
                            if (fieldsForType == null)
                            {
                                fieldsForType = new List<FieldAndOffset>();
                            }
                            fieldsForType.Add(new FieldAndOffset(fieldDesc, offset));
                        }
                    }

                    if (fieldsForType != null)
                    {
                        typeToFieldMap.Add(typeDefHandle, fieldsForType.ToArray());
                    }
                }

                LayoutInt blockAlignment = new LayoutInt(TargetDetails.MaximumPrimitiveSize);

                return new ModuleFieldLayout(
                    module,
                    gcStatics: new StaticsBlock() { Size = gcStatics[StaticIndexRegular], LargestAlignment = blockAlignment },
                    nonGcStatics: new StaticsBlock() { Size = nonGcStatics[StaticIndexRegular], LargestAlignment = blockAlignment },
                    threadGcStatics: new StaticsBlock() { Size = gcStatics[StaticIndexThreadLocal], LargestAlignment = blockAlignment },
                    threadNonGcStatics: new StaticsBlock() { Size = nonGcStatics[StaticIndexThreadLocal], LargestAlignment = blockAlignment },
                    typeToFieldMap: typeToFieldMap);
            }

            private void GetElementTypeInfo(
                EcmaModule module, 
                FieldDesc fieldDesc,
                EntityHandle valueTypeHandle, 
                CorElementType elementType, 
                int pointerSize, 
                out int alignment, 
                out int size, 
                out bool isGcPointerField,
                out bool isGcBoxedField)
            {
                alignment = 1;
                size = 0;
                isGcPointerField = false;
                isGcBoxedField = false;

                switch (elementType)
                {
                    case CorElementType.ELEMENT_TYPE_I1:
                    case CorElementType.ELEMENT_TYPE_U1:
                    case CorElementType.ELEMENT_TYPE_BOOLEAN:
                        size = 1;
                        break;

                    case CorElementType.ELEMENT_TYPE_I2:
                    case CorElementType.ELEMENT_TYPE_U2:
                    case CorElementType.ELEMENT_TYPE_CHAR:
                        alignment = 2;
                        size = 2;
                        break;

                    case CorElementType.ELEMENT_TYPE_I4:
                    case CorElementType.ELEMENT_TYPE_U4:
                    case CorElementType.ELEMENT_TYPE_R4:
                        alignment = 4;
                        size = 4;
                        break;

                    case CorElementType.ELEMENT_TYPE_FNPTR:
                    case CorElementType.ELEMENT_TYPE_PTR:
                    case CorElementType.ELEMENT_TYPE_I:
                    case CorElementType.ELEMENT_TYPE_U:
                        alignment = pointerSize;
                        size = pointerSize;
                        break;

                    case CorElementType.ELEMENT_TYPE_I8:
                    case CorElementType.ELEMENT_TYPE_U8:
                    case CorElementType.ELEMENT_TYPE_R8:
                        alignment = 8;
                        size = 8;
                        break;

                    case CorElementType.ELEMENT_TYPE_VAR:
                    case CorElementType.ELEMENT_TYPE_MVAR:
                    case CorElementType.ELEMENT_TYPE_STRING:
                    case CorElementType.ELEMENT_TYPE_SZARRAY:
                    case CorElementType.ELEMENT_TYPE_ARRAY:
                    case CorElementType.ELEMENT_TYPE_CLASS:
                    case CorElementType.ELEMENT_TYPE_OBJECT:
                        isGcPointerField = true;
                        break;

                    case CorElementType.ELEMENT_TYPE_BYREF:
                        ThrowHelper.ThrowTypeLoadException(ExceptionStringID.ClassLoadGeneral, fieldDesc.OwningType);
                        break;

                    // Statics for valuetypes where the valuetype is defined in this module are handled here. 
                    // Other valuetype statics utilize the pessimistic model below.
                    case CorElementType.ELEMENT_TYPE_VALUETYPE:
                        isGcBoxedField = true;
                        if (IsTypeByRefLike(valueTypeHandle, module.MetadataReader))
                        {
                            ThrowHelper.ThrowTypeLoadException(ExceptionStringID.ClassLoadGeneral, fieldDesc.OwningType);
                        }
                        break;

                    case CorElementType.ELEMENT_TYPE_END:
                    default:
                        isGcBoxedField = true;
                        if (!valueTypeHandle.IsNil)
                        {
                            // Allocate pessimistic non-GC area for cross-module fields as that's what CoreCLR does
                            // <a href="https://github.com/dotnet/coreclr/blob/659af58047a949ed50d11101708538d2e87f2568/src/vm/ceeload.cpp#L2124">here</a>
                            alignment = TargetDetails.MaximumPrimitiveSize;
                            size = TargetDetails.MaximumPrimitiveSize;
                        }
                        else
                        {
                            // Field has an unexpected type
                            throw new InvalidProgramException();
                        }
                        break;
                }
            }

            public FieldAndOffset[] GetOrAddDynamicLayout(DefType defType, ModuleFieldLayout moduleFieldLayout)
            {
                FieldAndOffset[] fieldsForType;
                if (!moduleFieldLayout.TryGetDynamicLayout(defType, out fieldsForType))
                {
                    fieldsForType = CreateDynamicLayout(defType, moduleFieldLayout.Module);
                    moduleFieldLayout.AddDynamicLayout(defType, fieldsForType);
                }
                return fieldsForType;
            }

            private FieldAndOffset[] CreateDynamicLayout(DefType defType, EcmaModule module)
            {
                List<FieldAndOffset> fieldsForType = null;
                int pointerSize = module.Context.Target.PointerSize;

                // In accordance with CoreCLR runtime conventions,
                // index 0 corresponds to regular statics, index 1 to thread-local statics.
                int[][] nonGcStaticsCount = new int[StaticIndexCount][]
                {
                    new int[TargetDetails.MaximumLog2PrimitiveSize + 1],
                    new int[TargetDetails.MaximumLog2PrimitiveSize + 1],
                };

                int[] gcPointerCount = new int[StaticIndexCount];
                int[] gcBoxedCount = new int[StaticIndexCount];

                foreach (FieldDesc field in defType.GetFields())
                {
                    FieldDefinition fieldDef = module.MetadataReader.GetFieldDefinition(((EcmaField)field.GetTypicalFieldDefinition()).Handle);
                    if ((fieldDef.Attributes & (FieldAttributes.Static | FieldAttributes.Literal)) == FieldAttributes.Static)
                    {
                        int index = (IsFieldThreadStatic(in fieldDef, module.MetadataReader) ? StaticIndexThreadLocal : StaticIndexRegular);
                        int alignment;
                        int size;
                        bool isGcPointerField;
                        bool isGcBoxedField;

                        CorElementType corElementType;
                        EntityHandle valueTypeHandle;

                        GetFieldElementTypeAndValueTypeHandle(in fieldDef, module.MetadataReader, out corElementType, out valueTypeHandle);

                        GetElementTypeInfo(module, field, valueTypeHandle, corElementType, pointerSize, out alignment, out size, out isGcPointerField, out isGcBoxedField);
                        if (isGcPointerField)
                        {
                            gcPointerCount[index]++;
                        }
                        else if (isGcBoxedField)
                        {
                            gcBoxedCount[index]++;
                        }
                        if (size != 0)
                        {
                            int log2Size = GetLog2Size(size);
                            nonGcStaticsCount[index][log2Size]++;
                        }
                    }
                }

                int nonGcInitialOffset;
                switch (pointerSize)
                {
                    case 4:
                        nonGcInitialOffset = DomainLocalModuleNormalDynamicEntryOffsetOfDataBlob32Bit;
                        break;

                    case 8:
                        nonGcInitialOffset = DomainLocalModuleNormalDynamicEntryOffsetOfDataBlob64Bit;
                        break;

                    default:
                        throw new NotImplementedException();
                }

                LayoutInt[] nonGcStaticFieldOffsets = new LayoutInt[StaticIndexCount]
                {
                    new LayoutInt(nonGcInitialOffset),
                    new LayoutInt(nonGcInitialOffset),
                };

                LayoutInt[][] nonGcStatics = new LayoutInt[StaticIndexCount][]
                {
                    new LayoutInt[TargetDetails.MaximumLog2PrimitiveSize + 1],
                    new LayoutInt[TargetDetails.MaximumLog2PrimitiveSize + 1],
                };

                for (int log2Size = TargetDetails.MaximumLog2PrimitiveSize; log2Size >= 0; log2Size--)
                {
                    for (int index = 0; index < StaticIndexCount; index++)
                    {
                        LayoutInt offset = nonGcStaticFieldOffsets[index];
                        nonGcStatics[index][log2Size] = offset;
                        offset += new LayoutInt(nonGcStaticsCount[index][log2Size] << log2Size);
                        nonGcStaticFieldOffsets[index] = offset;
                    }
                }

                LayoutInt[] gcBoxedFieldOffsets = new LayoutInt[StaticIndexCount];
                LayoutInt[] gcPointerFieldOffsets = new LayoutInt[StaticIndexCount] 
                {
                    new LayoutInt(gcBoxedCount[StaticIndexRegular] * pointerSize),
                    new LayoutInt(gcBoxedCount[StaticIndexThreadLocal] * pointerSize)
                };

                foreach (FieldDesc field in defType.GetFields())
                {
                    FieldDefinitionHandle fieldDefHandle = ((EcmaField)field.GetTypicalFieldDefinition()).Handle;
                    FieldDefinition fieldDef = module.MetadataReader.GetFieldDefinition(fieldDefHandle);
                    if ((fieldDef.Attributes & (FieldAttributes.Static | FieldAttributes.Literal)) == FieldAttributes.Static)
                    {
                        int index = (IsFieldThreadStatic(in fieldDef, module.MetadataReader) ? StaticIndexThreadLocal : StaticIndexRegular);
                        int alignment;
                        int size;
                        bool isGcPointerField;
                        bool isGcBoxedField;

                        CorElementType corElementType;
                        EntityHandle valueTypeHandle;

                        GetFieldElementTypeAndValueTypeHandle(in fieldDef, module.MetadataReader, out corElementType, out valueTypeHandle);

                        GetElementTypeInfo(module, field, valueTypeHandle, corElementType, pointerSize, out alignment, out size, out isGcPointerField, out isGcBoxedField);

                        LayoutInt offset = LayoutInt.Zero;

                        if (size != 0)
                        {
                            int log2Size = GetLog2Size(size);
                            offset = nonGcStatics[index][log2Size];
                            nonGcStatics[index][log2Size] += new LayoutInt(1 << log2Size);
                        }
                        if (isGcPointerField)
                        {
                            offset = gcPointerFieldOffsets[index];
                            gcPointerFieldOffsets[index] += new LayoutInt(pointerSize);
                        }
                        else if (isGcBoxedField)
                        {
                            offset = gcBoxedFieldOffsets[index];
                            gcBoxedFieldOffsets[index] += new LayoutInt(pointerSize);
                        }

                        if (fieldsForType == null)
                        {
                            fieldsForType = new List<FieldAndOffset>();
                        }
                        fieldsForType.Add(new FieldAndOffset(field, offset));
                    }
                }

                return fieldsForType == null ? null : fieldsForType.ToArray();
            }


            protected override int GetKeyHashCode(EcmaModule key)
            {
                return key.GetHashCode();
            }

            protected override int GetValueHashCode(ModuleFieldLayout value)
            {
                return value.Module.GetHashCode();
            }

            /// <summary>
            /// Try to locate the ThreadStatic custom attribute on the field (much like EcmaField.cs does in the method InitializeFieldFlags).
            /// </summary>
            /// <param name="fieldDef">Field definition</param>
            /// <param name="metadataReader">Metadata reader for the module</param>
            /// <returns>true when the field is marked with the ThreadStatic custom attribute</returns>
            private static bool IsFieldThreadStatic(in FieldDefinition fieldDef, MetadataReader metadataReader)
            {
                return !metadataReader.GetCustomAttributeHandle(fieldDef.GetCustomAttributes(), "System", "ThreadStaticAttribute").IsNil;
            }

            /// <summary>
            /// Try to locate the IsByRefLike attribute on the type (much like EcmaType does in ComputeTypeFlags).
            /// </summary>
            /// <param name="typeDefHandle">Handle to the field type to analyze</param>
            /// <param name="metadataReader">Metadata reader for the active module</param>
            /// <returns></returns>
            private static bool IsTypeByRefLike(EntityHandle typeDefHandle, MetadataReader metadataReader)
            {
                return typeDefHandle.Kind == HandleKind.TypeDefinition &&
                    !metadataReader.GetCustomAttributeHandle(
                        metadataReader.GetTypeDefinition((TypeDefinitionHandle)typeDefHandle).GetCustomAttributes(),
                        "System.Runtime.CompilerServices",
                        "IsByRefLikeAttribute").IsNil;
            }

            /// <summary>
            /// Partially decode field signature to obtain CorElementType and optionally the type handle for VALUETYPE fields.
            /// </summary>
            /// <param name="fieldDef">Metadata field definition</param>
            /// <param name="metadataReader">Metadata reader for the active module</param>
            /// <param name="corElementType">Output element type decoded from the signature</param>
            /// <param name="valueTypeHandle">Value type handle decoded from the signature</param>
            private static void GetFieldElementTypeAndValueTypeHandle(
                in FieldDefinition fieldDef,
                MetadataReader metadataReader,
                out CorElementType corElementType,
                out EntityHandle valueTypeHandle)
            {
                BlobReader signature = metadataReader.GetBlobReader(fieldDef.Signature);
                SignatureHeader signatureHeader = signature.ReadSignatureHeader();
                if (signatureHeader.Kind != SignatureKind.Field)
                {
                    throw new InvalidProgramException();
                }

                corElementType = ReadElementType(ref signature);
                valueTypeHandle = default(EntityHandle);
                if (corElementType == CorElementType.ELEMENT_TYPE_GENERICINST)
                {
                    corElementType = ReadElementType(ref signature);
                }

                if (corElementType == CorElementType.ELEMENT_TYPE_VALUETYPE)
                {
                    valueTypeHandle = signature.ReadTypeHandle();
                }
            }

            /// <summary>
            /// Extract element type from a field signature after skipping various modifiers.
            /// </summary>
            /// <param name="signature">Signature byte array</param>
            /// <param name="index">On input, index into the signature array. Gets modified to point after the element type on return.</param>
            /// <returns></returns>
            private static CorElementType ReadElementType(ref BlobReader signature)
            {
                // SigParser::PeekElemType
                byte signatureByte = signature.ReadByte();
                if (signatureByte < (byte)CorElementType.ELEMENT_TYPE_CMOD_REQD)
                {
                    // Fast path
                    return (CorElementType)signatureByte;
                }

                // SigParser::SkipCustomModifiers -> SkipAnyVASentinel
                if (signatureByte == (byte)CorElementType.ELEMENT_TYPE_SENTINEL)
                {
                    signatureByte = signature.ReadByte();
                }

                // SigParser::SkipCustomModifiers - modifier loop
                while (signatureByte == (byte)CorElementType.ELEMENT_TYPE_CMOD_REQD ||
                    signatureByte == (byte)CorElementType.ELEMENT_TYPE_CMOD_OPT)
                {
                    signature.ReadCompressedInteger();
                    signatureByte = signature.ReadByte();
                }
                return (CorElementType)signatureByte;
            }


            /// <summary>
            /// Return the integral value of dyadic logarithm of given size
            /// up to MaximumLog2PrimitiveSize.
            /// </summary>
            /// <param name="size">Size to calculate base 2 logarithm for</param>
            /// <returns></returns>
            private static int GetLog2Size(int size)
            {
                switch (size)
                {
                    case 0:
                    case 1:
                        return 0;
                    case 2:
                        return 1;
                    case 3:
                    case 4:
                        return 2;
                    default:
                        Debug.Assert(TargetDetails.MaximumLog2PrimitiveSize == 3);
                        return TargetDetails.MaximumLog2PrimitiveSize;
                }
            }
        }

        /// <summary>
        /// Field layouts for a given EcmaModule.
        /// </summary>
        private class ModuleFieldLayout
        {
            public EcmaModule Module { get; }

            public StaticsBlock GcStatics { get; }

            public StaticsBlock NonGcStatics { get;  }

            public StaticsBlock ThreadGcStatics { get;  }

            public StaticsBlock ThreadNonGcStatics { get;  }

            public IReadOnlyDictionary<TypeDefinitionHandle, FieldAndOffset[]> TypeToFieldMap { get; }

            private Dictionary<DefType, FieldAndOffset[]> _genericTypeToFieldMap;

            public ModuleFieldLayout(
                EcmaModule module, 
                StaticsBlock gcStatics, 
                StaticsBlock nonGcStatics, 
                StaticsBlock threadGcStatics, 
                StaticsBlock threadNonGcStatics,
                IReadOnlyDictionary<TypeDefinitionHandle, FieldAndOffset[]> typeToFieldMap)
            {
                Module = module;
                GcStatics = gcStatics;
                NonGcStatics = nonGcStatics;
                ThreadGcStatics = threadGcStatics;
                ThreadNonGcStatics = threadNonGcStatics;
                TypeToFieldMap = typeToFieldMap;

                _genericTypeToFieldMap = new Dictionary<DefType, FieldAndOffset[]>();
            }

            public bool TryGetDynamicLayout(DefType instantiatedType, out FieldAndOffset[] fieldMap)
            {
                return _genericTypeToFieldMap.TryGetValue(instantiatedType, out fieldMap);
            }

            public void AddDynamicLayout(DefType instantiatedType, FieldAndOffset[] fieldMap)
            {
                _genericTypeToFieldMap.Add(instantiatedType, fieldMap);
            }
        }
    }
}
