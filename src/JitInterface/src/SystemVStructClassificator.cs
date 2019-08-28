// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
#if SUPPORT_JIT
using Internal.Runtime.CompilerServices;
#endif

using Internal.IL;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using ILCompiler;
using ILCompiler.DependencyAnalysis;

#if READYTORUN
using System.Reflection.Metadata.Ecma335;
using ILCompiler.DependencyAnalysis.ReadyToRun;
#endif

namespace Internal.JitInterface
{
    internal class SystemVStructClassificator
    {
        const int CLR_SYSTEMV_MAX_EIGHTBYTES_COUNT_TO_PASS_IN_REGISTERS = 2;
        const int CLR_SYSTEMV_MAX_STRUCT_BYTES_TO_PASS_IN_REGISTERS = 16;

        const int SYSTEMV_EIGHT_BYTE_SIZE_IN_BYTES = 8; // Size of an eightbyte in bytes.
        const int SYSTEMV_MAX_NUM_FIELDS_IN_REGISTER_PASSED_STRUCT = 16; // Maximum number of fields in struct passed in registers

        private Dictionary<TypeDesc, SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR> _classificationCache = new Dictionary<TypeDesc, SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR>();

        internal struct SystemVStructRegisterPassingHelper
        {
            internal SystemVStructRegisterPassingHelper(int totalStructSize)
            {
                structSize = totalStructSize;
                eightByteCount = 0;
                inEmbeddedStruct = false;
                currentUniqueOffsetField = 0;
                largestFieldOffset = -1;

                eightByteClassifications = new SystemVClassificationType[CLR_SYSTEMV_MAX_EIGHTBYTES_COUNT_TO_PASS_IN_REGISTERS];
                eightByteSizes = new int[CLR_SYSTEMV_MAX_EIGHTBYTES_COUNT_TO_PASS_IN_REGISTERS];
                eightByteOffsets = new int[CLR_SYSTEMV_MAX_EIGHTBYTES_COUNT_TO_PASS_IN_REGISTERS];

                fieldClassifications = new SystemVClassificationType[SYSTEMV_MAX_NUM_FIELDS_IN_REGISTER_PASSED_STRUCT];
                fieldSizes = new int[SYSTEMV_MAX_NUM_FIELDS_IN_REGISTER_PASSED_STRUCT];
                fieldOffsets = new int[SYSTEMV_MAX_NUM_FIELDS_IN_REGISTER_PASSED_STRUCT];
                            
                for (int i = 0; i < CLR_SYSTEMV_MAX_EIGHTBYTES_COUNT_TO_PASS_IN_REGISTERS; i++)
                {
                    eightByteClassifications[i] = SystemVClassificationType.SystemVClassificationTypeNoClass;
                    eightByteSizes[i] = 0;
                    eightByteOffsets[i] = 0;
                }

                // Initialize the work arrays
                for (int i = 0; i < SYSTEMV_MAX_NUM_FIELDS_IN_REGISTER_PASSED_STRUCT; i++)
                {
                    fieldClassifications[i] = SystemVClassificationType.SystemVClassificationTypeNoClass;
                    fieldSizes[i] = 0;
                    fieldOffsets[i] = 0;
                }
            }

            // Input state.
            public int                         structSize;

            // These fields are the output; these are what is computed by the classification algorithm.
            public int                         eightByteCount;
            public SystemVClassificationType[] eightByteClassifications;
            public int[]                       eightByteSizes;
            public int[]                       eightByteOffsets;

            // Helper members to track state.
            public bool                        inEmbeddedStruct;
            public int                         currentUniqueOffsetField; // A virtual field that could encompass many overlapping fields.
            public int                         largestFieldOffset;
            public SystemVClassificationType[] fieldClassifications;
            public int[]                       fieldSizes;
            public int[]                       fieldOffsets;
        };

        public unsafe bool getSystemVAmd64PassStructInRegisterDescriptor(TypeDesc typeDesc, SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR* structPassInRegDescPtr)
        {
            structPassInRegDescPtr->passedInRegisters = false;
            
            int typeSize = typeDesc.GetElementSize().AsInt;
            if (typeDesc.IsValueType && (typeSize <= CLR_SYSTEMV_MAX_STRUCT_BYTES_TO_PASS_IN_REGISTERS))
            {
                Debug.Assert((TypeDef2SystemVClassification(typeDesc) == SystemVClassificationType.SystemVClassificationTypeStruct) ||
                             (TypeDef2SystemVClassification(typeDesc) == SystemVClassificationType.SystemVClassificationTypeTypedReference));

                if (_classificationCache.TryGetValue(typeDesc, out SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR descriptor))
                {
                    *structPassInRegDescPtr = descriptor;
                }
                else
                {
                    SystemVStructRegisterPassingHelper helper = new SystemVStructRegisterPassingHelper(typeSize);
                    bool canPassInRegisters = ClassifyEightBytes(typeDesc, ref helper, 0);
                    if (canPassInRegisters)
                    {
                        structPassInRegDescPtr->passedInRegisters = canPassInRegisters;
                        structPassInRegDescPtr->eightByteCount = (byte)helper.eightByteCount;
                        Debug.Assert(structPassInRegDescPtr->eightByteCount <= CLR_SYSTEMV_MAX_EIGHTBYTES_COUNT_TO_PASS_IN_REGISTERS);

                        structPassInRegDescPtr->eightByteClassifications0 = helper.eightByteClassifications[0];
                        structPassInRegDescPtr->eightByteSizes0 = (byte)helper.eightByteSizes[0];
                        structPassInRegDescPtr->eightByteOffsets0 = (byte)helper.eightByteOffsets[0];
                        
                        structPassInRegDescPtr->eightByteClassifications1 = helper.eightByteClassifications[1];
                        structPassInRegDescPtr->eightByteSizes1 = (byte)helper.eightByteSizes[1];
                        structPassInRegDescPtr->eightByteOffsets1 = (byte)helper.eightByteOffsets[1];
                    }

                    _classificationCache.Add(typeDesc, *structPassInRegDescPtr);
                }
            }

            return true;
        }

        private static SystemVClassificationType TypeDef2SystemVClassification(TypeDesc typeDesc)
        {
            SystemVClassificationType[] toSystemVAmd64ClassificationTypeMap = {
                SystemVClassificationType.SystemVClassificationTypeUnknown,             // Unknown
                SystemVClassificationType.SystemVClassificationTypeUnknown,             // Void
                SystemVClassificationType.SystemVClassificationTypeInteger,             // Boolean
                SystemVClassificationType.SystemVClassificationTypeInteger,             // Char
                SystemVClassificationType.SystemVClassificationTypeInteger,             // SByte
                SystemVClassificationType.SystemVClassificationTypeInteger,             // Byte
                SystemVClassificationType.SystemVClassificationTypeInteger,             // Int16
                SystemVClassificationType.SystemVClassificationTypeInteger,             // UInt16
                SystemVClassificationType.SystemVClassificationTypeInteger,             // Int32
                SystemVClassificationType.SystemVClassificationTypeInteger,             // UInt32
                SystemVClassificationType.SystemVClassificationTypeInteger,             // Int64
                SystemVClassificationType.SystemVClassificationTypeInteger,             // UInt64
                SystemVClassificationType.SystemVClassificationTypeInteger,             // IntPtr
                SystemVClassificationType.SystemVClassificationTypeInteger,             // UIntPtr
                SystemVClassificationType.SystemVClassificationTypeSSE,                 // Single
                SystemVClassificationType.SystemVClassificationTypeSSE,                 // Double
                SystemVClassificationType.SystemVClassificationTypeStruct,              // ValueType
                SystemVClassificationType.SystemVClassificationTypeInteger,             // Enum
                SystemVClassificationType.SystemVClassificationTypeIntegerReference,    // Nullable
                SystemVClassificationType.SystemVClassificationTypeUnknown,             // Unused
                SystemVClassificationType.SystemVClassificationTypeIntegerReference,    // Class
                SystemVClassificationType.SystemVClassificationTypeIntegerReference,    // Interface
                SystemVClassificationType.SystemVClassificationTypeUnknown,             // Unused
                SystemVClassificationType.SystemVClassificationTypeIntegerReference,    // Array
                SystemVClassificationType.SystemVClassificationTypeIntegerReference,    // SzArray
                SystemVClassificationType.SystemVClassificationTypeIntegerByRef,        // ByRef
                SystemVClassificationType.SystemVClassificationTypeInteger,             // Pointer
                SystemVClassificationType.SystemVClassificationTypeInteger,             // FunctionPointer
                SystemVClassificationType.SystemVClassificationTypeIntegerReference,    // GenericParameter
                SystemVClassificationType.SystemVClassificationTypeIntegerReference,    // SignatureTypeVariable
                SystemVClassificationType.SystemVClassificationTypeIntegerReference,    // SignatureMethodVariable
            };

            if (typeDesc.IsWellKnownType(WellKnownType.TypedReference))
            {
                // There is no category representing typed reference
                return SystemVClassificationType.SystemVClassificationTypeTypedReference;
            }

            int category = (int)typeDesc.Category;

            Debug.Assert(toSystemVAmd64ClassificationTypeMap.Length == (int)TypeFlags.MaxCategory);

            // spot check of the map
            Debug.Assert(toSystemVAmd64ClassificationTypeMap[(int)TypeFlags.IntPtr] == SystemVClassificationType.SystemVClassificationTypeInteger);
            Debug.Assert(toSystemVAmd64ClassificationTypeMap[(int)TypeFlags.Pointer] == SystemVClassificationType.SystemVClassificationTypeInteger);
            Debug.Assert(toSystemVAmd64ClassificationTypeMap[(int)TypeFlags.ValueType] == SystemVClassificationType.SystemVClassificationTypeStruct);
            Debug.Assert(toSystemVAmd64ClassificationTypeMap[(int)TypeFlags.ByRef] == SystemVClassificationType.SystemVClassificationTypeIntegerByRef);

            return (category < toSystemVAmd64ClassificationTypeMap.Length) ? toSystemVAmd64ClassificationTypeMap[category] : SystemVClassificationType.SystemVClassificationTypeUnknown;
        }

        // If we have a field classification already, but there is a union, we must merge the classification type of the field. Returns the
        // new, merged classification type.
        static SystemVClassificationType ReClassifyField(SystemVClassificationType originalClassification, SystemVClassificationType newFieldClassification)
        {
            Debug.Assert((newFieldClassification == SystemVClassificationType.SystemVClassificationTypeInteger) ||
                            (newFieldClassification == SystemVClassificationType.SystemVClassificationTypeIntegerReference) ||
                            (newFieldClassification == SystemVClassificationType.SystemVClassificationTypeIntegerByRef) ||
                            (newFieldClassification == SystemVClassificationType.SystemVClassificationTypeSSE));

            switch (newFieldClassification)
            {
            case SystemVClassificationType.SystemVClassificationTypeInteger:
                // Integer overrides everything; the resulting classification is Integer. Can't merge Integer and IntegerReference.
                Debug.Assert((originalClassification == SystemVClassificationType.SystemVClassificationTypeInteger) ||
                                (originalClassification == SystemVClassificationType.SystemVClassificationTypeSSE));

                return SystemVClassificationType.SystemVClassificationTypeInteger;

            case SystemVClassificationType.SystemVClassificationTypeSSE:
                // If the old and new classifications are both SSE, then the merge is SSE, otherwise it will be integer. Can't merge SSE and IntegerReference.
                Debug.Assert((originalClassification == SystemVClassificationType.SystemVClassificationTypeInteger) ||
                                (originalClassification == SystemVClassificationType.SystemVClassificationTypeSSE));

                if (originalClassification == SystemVClassificationType.SystemVClassificationTypeSSE)
                {
                    return SystemVClassificationType.SystemVClassificationTypeSSE;
                }
                else
                {
                    return SystemVClassificationType.SystemVClassificationTypeInteger;
                }

            case SystemVClassificationType.SystemVClassificationTypeIntegerReference:
                // IntegerReference can only merge with IntegerReference.
                Debug.Assert(originalClassification == SystemVClassificationType.SystemVClassificationTypeIntegerReference);
                return SystemVClassificationType.SystemVClassificationTypeIntegerReference;

            case SystemVClassificationType.SystemVClassificationTypeIntegerByRef:
                // IntegerByReference can only merge with IntegerByReference.
                Debug.Assert(originalClassification == SystemVClassificationType.SystemVClassificationTypeIntegerByRef);
                return SystemVClassificationType.SystemVClassificationTypeIntegerByRef;

            default:
                Debug.Assert(false); // Unexpected type.
                return SystemVClassificationType.SystemVClassificationTypeUnknown;
            }
        }

        /// <summary>
        /// Returns 'true' if the struct is passed in registers, 'false' otherwise.
        /// </summary>
        private static bool ClassifyEightBytes(TypeDesc typeDesc, 
                                               ref SystemVStructRegisterPassingHelper helper,
                                               int startOffsetOfStruct)
        {
            FieldDesc firstField = null;
            int numIntroducedFields = 0;
            foreach (FieldDesc field in typeDesc.GetFields())
            {
                if (!field.IsLiteral && !field.IsStatic)
                {
                    if (firstField == null)
                    {
                        firstField = field;
                    }
                    numIntroducedFields++;
                }
            }

            if (numIntroducedFields == 0)
            {
                return false;
            }

            // The SIMD Intrinsic types are meant to be handled specially and should not be passed as struct registers
            if (typeDesc.IsIntrinsic)
            {
                InstantiatedType instantiatedType = typeDesc as InstantiatedType;
                if (instantiatedType != null)
                {
                    string typeName = instantiatedType.Name;
                    string namespaceName = instantiatedType.Namespace;

                    // TODO: is this what we get for the typeName?
                    if (typeName == "Vector256`1" || typeName == "Vector128`1" || typeName == "Vector64`1")
                    {
                        Debug.Assert(namespaceName == "System.Runtime.Intrinsics");
                        return false;
                    }

                    if ((typeName ==  "Vector`1") && (namespaceName == "System.Numerics"))
                    {
                        return false;
                    }
                }
            }

            MetadataType mdType = typeDesc as MetadataType;
            Debug.Assert(mdType != null);

            TypeDesc firstFieldElementType = firstField.FieldType;
            int firstFieldSize = firstFieldElementType.GetElementSize().AsInt;

            bool isFixedBuffer = mdType.HasCustomAttribute("System.Runtime.CompilerServices", "FixedBufferAttribute");
            if (isFixedBuffer)
            {
                Debug.Assert(mdType.IsExplicitLayout);
                numIntroducedFields = typeDesc.GetElementSize().AsInt / firstFieldSize;
            }

            IEnumerator<FieldDesc> fieldEnumerator = typeDesc.GetFields().GetEnumerator();

            bool hasField = fieldEnumerator.MoveNext();
            // We've already verified that the type has some fields at the beginning of this function
            Debug.Assert(hasField);

            FieldAndOffset[] fieldsAndOffsets = mdType.GetClassLayout().Offsets;

            for (int fieldIndex = 0; fieldIndex < numIntroducedFields; fieldIndex++)
            {
                int fieldOffset;
                FieldDesc field;

                if (mdType.IsExplicitLayout)
                {
                    Debug.Assert(fieldIndex < fieldsAndOffsets.Length);
                    if (isFixedBuffer)
                    {
                        field = firstField;
                        fieldOffset = fieldIndex * firstFieldSize;
                    }
                    else
                    {
                        field = fieldsAndOffsets[fieldIndex].Field;
                        fieldOffset = fieldsAndOffsets[fieldIndex].Offset.AsInt;
                    }
                }
                else
                {
                    // Ignore static and literal fields as they don't contribute to the layout
                    do
                    {
                        field = fieldEnumerator.Current;
                        fieldEnumerator.MoveNext();
                    }
                    while (field.IsLiteral || field.IsStatic);

                    fieldOffset = field.Offset.AsInt;
                }

                int normalizedFieldOffset = fieldOffset + startOffsetOfStruct;

                int fieldSize = field.FieldType.GetElementSize().AsInt;
                Debug.Assert(fieldSize != -1);

                // The field can't span past the end of the struct.
                if ((normalizedFieldOffset + fieldSize) > helper.structSize)
                {
                    Debug.Assert(false, "Invalid struct size. The size of fields and overall size don't agree");
                    return false;
                }

                SystemVClassificationType fieldClassificationType;
                if (typeDesc.IsByReferenceOfT)
                {
                    // ByReference<T> is a special type whose single IntPtr field holds a by-ref potentially interior pointer to GC
                    // memory, so classify its field as such
                    Debug.Assert(numIntroducedFields == 1);
                    Debug.Assert(field.FieldType.IsWellKnownType(WellKnownType.IntPtr));

                    fieldClassificationType = SystemVClassificationType.SystemVClassificationTypeIntegerByRef;
                }
                else
                {
                    fieldClassificationType = TypeDef2SystemVClassification(field.FieldType);
                }

                if (fieldClassificationType == SystemVClassificationType.SystemVClassificationTypeStruct)
                {
                    bool inEmbeddedStructPrev = helper.inEmbeddedStruct;
                    helper.inEmbeddedStruct = true;

                    bool structRet = false;
                    structRet = ClassifyEightBytes(field.FieldType, ref helper, normalizedFieldOffset);
                    
                    helper.inEmbeddedStruct = inEmbeddedStructPrev;

                    if (!structRet)
                    {
                        // If the nested struct says not to enregister, there's no need to continue analyzing at this level. Just return do not enregister.
                        return false;
                    }

                    continue;
                }

                if (fieldClassificationType == SystemVClassificationType.SystemVClassificationTypeTypedReference || 
                    TypeDef2SystemVClassification(typeDesc) == SystemVClassificationType.SystemVClassificationTypeTypedReference)
                {
                    // The TypedReference is a very special type.
                    // In source/metadata it has two fields - Type and Value and both are defined of type IntPtr.
                    // When the VM creates a layout of the type it changes the type of the Value to ByRef type and the
                    // type of the Type field is left to IntPtr (TYPE_I internally - native int type.)
                    // This requires a special treatment of this type. The code below handles the both fields (and this entire type).

                    for (int i = 0; i < 2; i++)
                    {
                        fieldSize = 8;
                        fieldOffset = (i == 0 ? 0 : 8);
                        normalizedFieldOffset = fieldOffset + startOffsetOfStruct;
                        fieldClassificationType = (i == 0 ? SystemVClassificationType.SystemVClassificationTypeIntegerByRef : SystemVClassificationType.SystemVClassificationTypeInteger);
                        if ((normalizedFieldOffset % fieldSize) != 0)
                        {
                            // The spec requires that struct values on the stack from register passed fields expects
                            // those fields to be at their natural alignment.
                            return false;
                        }

                        helper.largestFieldOffset = (int)normalizedFieldOffset;

                        // Set the data for a new field.

                        // The new field classification must not have been initialized yet.
                        Debug.Assert(helper.fieldClassifications[helper.currentUniqueOffsetField] == SystemVClassificationType.SystemVClassificationTypeNoClass);

                        // There are only a few field classifications that are allowed.
                        Debug.Assert((fieldClassificationType == SystemVClassificationType.SystemVClassificationTypeInteger) ||
                            (fieldClassificationType == SystemVClassificationType.SystemVClassificationTypeIntegerReference) ||
                            (fieldClassificationType == SystemVClassificationType.SystemVClassificationTypeIntegerByRef) ||
                            (fieldClassificationType == SystemVClassificationType.SystemVClassificationTypeSSE));

                        helper.fieldClassifications[helper.currentUniqueOffsetField] = fieldClassificationType;
                        helper.fieldSizes[helper.currentUniqueOffsetField] = fieldSize;
                        helper.fieldOffsets[helper.currentUniqueOffsetField] = normalizedFieldOffset;

                        helper.currentUniqueOffsetField++;
                    }

                    // Both fields of the special TypedReference struct are handled.
                    // Done classifying the System.TypedReference struct fields.
                    break;
                }

                if ((normalizedFieldOffset % fieldSize) != 0)
                {
                    // The spec requires that struct values on the stack from register passed fields expects
                    // those fields to be at their natural alignment.
                    return false;
                }

                if (normalizedFieldOffset <= helper.largestFieldOffset)
                {
                    // Find the field corresponding to this offset and update the size if needed.
                    // If the offset matches a previously encountered offset, update the classification and field size.
                    int i;
                    for (i = helper.currentUniqueOffsetField - 1; i >= 0; i--)
                    {
                        if (helper.fieldOffsets[i] == normalizedFieldOffset)
                        {
                            if (fieldSize > helper.fieldSizes[i])
                            {
                                helper.fieldSizes[i] = fieldSize;
                            }

                            helper.fieldClassifications[i] = ReClassifyField(helper.fieldClassifications[i], fieldClassificationType);

                            break;
                        }
                    }

                    if (i >= 0)
                    {
                        // The proper size of the union set of fields has been set above; continue to the next field.
                        continue;
                    }
                }
                else
                {
                    helper.largestFieldOffset = (int)normalizedFieldOffset;
                }

                // Set the data for a new field.

                // The new field classification must not have been initialized yet.
                Debug.Assert(helper.fieldClassifications[helper.currentUniqueOffsetField] == SystemVClassificationType.SystemVClassificationTypeNoClass);

                // There are only a few field classifications that are allowed.
                Debug.Assert((fieldClassificationType == SystemVClassificationType.SystemVClassificationTypeInteger) ||
                                (fieldClassificationType == SystemVClassificationType.SystemVClassificationTypeIntegerReference) ||
                                (fieldClassificationType == SystemVClassificationType.SystemVClassificationTypeIntegerByRef) ||
                                (fieldClassificationType == SystemVClassificationType.SystemVClassificationTypeSSE));

                helper.fieldClassifications[helper.currentUniqueOffsetField] = fieldClassificationType;
                helper.fieldSizes[helper.currentUniqueOffsetField] = fieldSize;
                helper.fieldOffsets[helper.currentUniqueOffsetField] = normalizedFieldOffset;

                Debug.Assert(helper.currentUniqueOffsetField < SYSTEMV_MAX_NUM_FIELDS_IN_REGISTER_PASSED_STRUCT);
                helper.currentUniqueOffsetField++;

            }

            AssignClassifiedEightByteTypes(ref helper);

            return true;
        }

        // Assigns the classification types to the array with eightbyte types.
        private static void AssignClassifiedEightByteTypes(ref SystemVStructRegisterPassingHelper helper)
        {
            const int CLR_SYSTEMV_MAX_BYTES_TO_PASS_IN_REGISTERS = CLR_SYSTEMV_MAX_EIGHTBYTES_COUNT_TO_PASS_IN_REGISTERS * SYSTEMV_EIGHT_BYTE_SIZE_IN_BYTES;
            //static_assert_no_msg(CLR_SYSTEMV_MAX_BYTES_TO_PASS_IN_REGISTERS == SYSTEMV_MAX_NUM_FIELDS_IN_REGISTER_PASSED_STRUCT);

            if (!helper.inEmbeddedStruct)
            {
                int largestFieldOffset = helper.largestFieldOffset;
                Debug.Assert(largestFieldOffset != -1);

                // We're at the top level of the recursion, and we're done looking at the fields.
                // Now sort the fields by offset and set the output data.

                //Span<int> sortedFieldOrder = stackalloc int[CLR_SYSTEMV_MAX_BYTES_TO_PASS_IN_REGISTERS];
                int[] sortedFieldOrder = new int[CLR_SYSTEMV_MAX_BYTES_TO_PASS_IN_REGISTERS];
                for (int i = 0; i < CLR_SYSTEMV_MAX_BYTES_TO_PASS_IN_REGISTERS; i++)
                {
                    sortedFieldOrder[i] = -1;
                }

                int numFields = helper.currentUniqueOffsetField;
                for (int i = 0; i < numFields; i++)
                {
                    Debug.Assert(helper.fieldOffsets[i] < CLR_SYSTEMV_MAX_BYTES_TO_PASS_IN_REGISTERS);
                    Debug.Assert(sortedFieldOrder[helper.fieldOffsets[i]] == -1); // we haven't seen this field offset yet.
                    sortedFieldOrder[helper.fieldOffsets[i]] = i;
                }

                // Calculate the eightbytes and their types.

                int lastFieldOrdinal = sortedFieldOrder[largestFieldOffset];
                int offsetAfterLastFieldByte = largestFieldOffset + helper.fieldSizes[lastFieldOrdinal];
                SystemVClassificationType lastFieldClassification = helper.fieldClassifications[lastFieldOrdinal];

                int usedEightBytes = 0;
                int accumulatedSizeForEightBytes = 0;
                bool foundFieldInEightByte = false;
                for (int offset = 0; offset < helper.structSize; offset++)
                {
                    SystemVClassificationType fieldClassificationType;
                    int fieldSize = 0;

                    int ordinal = sortedFieldOrder[offset];
                    if (ordinal == -1)
                    {
                        if (offset < accumulatedSizeForEightBytes)
                        {
                            // We're within a field and there is not an overlapping field that starts here.
                            // There's no work we need to do, so go to the next loop iteration.
                            continue;
                        }

                        // If there is no field that starts as this offset and we are not within another field,
                        // treat its contents as padding.
                        // Any padding that follows the last field receives the same classification as the
                        // last field; padding between fields receives the NO_CLASS classification as per
                        // the SysV ABI spec.
                        fieldSize = 1;
                        fieldClassificationType = offset < offsetAfterLastFieldByte ? SystemVClassificationType.SystemVClassificationTypeNoClass : lastFieldClassification;
                    }
                    else
                    {
                        foundFieldInEightByte = true;
                        fieldSize = helper.fieldSizes[ordinal];
                        Debug.Assert(fieldSize > 0);

                        fieldClassificationType = helper.fieldClassifications[ordinal];
                        Debug.Assert(fieldClassificationType != SystemVClassificationType.SystemVClassificationTypeMemory && fieldClassificationType != SystemVClassificationType.SystemVClassificationTypeUnknown);
                    }

                    int fieldStartEightByte = offset / SYSTEMV_EIGHT_BYTE_SIZE_IN_BYTES;
                    int fieldEndEightByte = (offset + fieldSize - 1) / SYSTEMV_EIGHT_BYTE_SIZE_IN_BYTES;

                    Debug.Assert(fieldEndEightByte < CLR_SYSTEMV_MAX_EIGHTBYTES_COUNT_TO_PASS_IN_REGISTERS);

                    usedEightBytes = Math.Max(usedEightBytes, fieldEndEightByte + 1);

                    for (int currentFieldEightByte = fieldStartEightByte; currentFieldEightByte <= fieldEndEightByte; currentFieldEightByte++)
                    {
                        if (helper.eightByteClassifications[currentFieldEightByte] == fieldClassificationType)
                        {
                            // Do nothing. The eight-byte already has this classification.
                        }
                        else if (helper.eightByteClassifications[currentFieldEightByte] == SystemVClassificationType.SystemVClassificationTypeNoClass)
                        {
                            helper.eightByteClassifications[currentFieldEightByte] = fieldClassificationType;
                        }
                        else if ((helper.eightByteClassifications[currentFieldEightByte] == SystemVClassificationType.SystemVClassificationTypeInteger) ||
                            (fieldClassificationType == SystemVClassificationType.SystemVClassificationTypeInteger))
                        {
                            Debug.Assert((fieldClassificationType != SystemVClassificationType.SystemVClassificationTypeIntegerReference) && 
                                            (fieldClassificationType != SystemVClassificationType.SystemVClassificationTypeIntegerByRef));

                            helper.eightByteClassifications[currentFieldEightByte] = SystemVClassificationType.SystemVClassificationTypeInteger;
                        }
                        else if ((helper.eightByteClassifications[currentFieldEightByte] == SystemVClassificationType.SystemVClassificationTypeIntegerReference) ||
                            (fieldClassificationType == SystemVClassificationType.SystemVClassificationTypeIntegerReference))
                        {
                            helper.eightByteClassifications[currentFieldEightByte] = SystemVClassificationType.SystemVClassificationTypeIntegerReference;
                        }
                        else if ((helper.eightByteClassifications[currentFieldEightByte] == SystemVClassificationType.SystemVClassificationTypeIntegerByRef) ||
                            (fieldClassificationType == SystemVClassificationType.SystemVClassificationTypeIntegerByRef))
                        {
                            helper.eightByteClassifications[currentFieldEightByte] = SystemVClassificationType.SystemVClassificationTypeIntegerByRef;
                        }
                        else
                        {
                            helper.eightByteClassifications[currentFieldEightByte] = SystemVClassificationType.SystemVClassificationTypeSSE;
                        }
                    }

                    if ((offset + 1) % SYSTEMV_EIGHT_BYTE_SIZE_IN_BYTES == 0) // If we just finished checking the last byte of an eightbyte
                    {
                        if (!foundFieldInEightByte)
                        {
                            // If we didn't find a field in an eight-byte (i.e. there are no explicit offsets that start a field in this eightbyte)
                            // then the classification of this eightbyte might be NoClass. We can't hand a classification of NoClass to the JIT
                            // so set the class to Integer (as though the struct has a char[8] padding) if the class is NoClass.
                            if (helper.eightByteClassifications[offset / SYSTEMV_EIGHT_BYTE_SIZE_IN_BYTES] == SystemVClassificationType.SystemVClassificationTypeNoClass)
                            {
                                helper.eightByteClassifications[offset / SYSTEMV_EIGHT_BYTE_SIZE_IN_BYTES] = SystemVClassificationType.SystemVClassificationTypeInteger;
                            }
                        }

                        foundFieldInEightByte = false;
                    }

                    accumulatedSizeForEightBytes = Math.Max(accumulatedSizeForEightBytes, offset + fieldSize);
                }

                for (int currentEightByte = 0; currentEightByte < usedEightBytes; currentEightByte++)
                {
                    int eightByteSize = accumulatedSizeForEightBytes < (SYSTEMV_EIGHT_BYTE_SIZE_IN_BYTES * (currentEightByte + 1))
                        ? accumulatedSizeForEightBytes % SYSTEMV_EIGHT_BYTE_SIZE_IN_BYTES
                        :   SYSTEMV_EIGHT_BYTE_SIZE_IN_BYTES;

                    // Save data for this eightbyte.
                    helper.eightByteSizes[currentEightByte] = eightByteSize;
                    helper.eightByteOffsets[currentEightByte] = currentEightByte * SYSTEMV_EIGHT_BYTE_SIZE_IN_BYTES;
                }

                helper.eightByteCount = usedEightBytes;

                Debug.Assert(helper.eightByteCount <= CLR_SYSTEMV_MAX_EIGHTBYTES_COUNT_TO_PASS_IN_REGISTERS);

#if DEBUG
                for (int i = 0; i < helper.eightByteCount; i++)
                {
                    Debug.Assert(helper.eightByteClassifications[i] != SystemVClassificationType.SystemVClassificationTypeNoClass);
                }
#endif // DEBUG
            }
        }
    }
}