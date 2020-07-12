// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

using Internal.TypeSystem;

namespace Internal.Runtime
{
    internal static class EETypeBuilderHelpers
    {
        private static EETypeElementType ComputeEETypeElementType(TypeDesc type)
        {
            // Enums are represented as their underlying type
            type = type.UnderlyingType;

            if (type.IsWellKnownType(WellKnownType.Array))
            {
                // SystemArray is a special EETypeElementType that doesn't exist in TypeFlags
                return EETypeElementType.SystemArray;
            }
            else
            {
                // The rest of TypeFlags should be directly castable to EETypeElementType.
                // Spot check the enums match.
                Debug.Assert((int)TypeFlags.Void == (int)EETypeElementType.Void);
                Debug.Assert((int)TypeFlags.IntPtr == (int)EETypeElementType.IntPtr);
                Debug.Assert((int)TypeFlags.Single == (int)EETypeElementType.Single);
                Debug.Assert((int)TypeFlags.UInt32 == (int)EETypeElementType.UInt32);
                Debug.Assert((int)TypeFlags.Pointer == (int)EETypeElementType.Pointer);
                Debug.Assert((int)TypeFlags.Array == (int)EETypeElementType.Array);

                EETypeElementType elementType = (EETypeElementType)type.Category;

                // Would be surprising to get these here though.
                Debug.Assert(elementType != EETypeElementType.SystemArray);
                Debug.Assert(elementType <= EETypeElementType.Pointer);

                return elementType;

            }            
        }

        public static UInt16 ComputeFlags(TypeDesc type)
        {
            UInt16 flags = type.IsParameterizedType ?
                (UInt16)EETypeKind.ParameterizedEEType : (UInt16)EETypeKind.CanonicalEEType;

            // The top 5 bits of flags are used to convey enum underlying type, primitive type, or mark the type as being System.Array
            EETypeElementType elementType = ComputeEETypeElementType(type);
            flags |= (UInt16)((UInt16)elementType << (UInt16)EETypeFlags.ElementTypeShift);

            if (type.IsGenericDefinition)
            {
                flags |= (UInt16)EETypeKind.GenericTypeDefEEType;

                // Generic type definition EETypes don't set the other flags.
                return flags;
            }

            if (type.HasFinalizer)
            {
                flags |= (UInt16)EETypeFlags.HasFinalizerFlag;
            }

            if (type.IsDefType
                && !type.IsCanonicalSubtype(CanonicalFormKind.Universal)
                && ((DefType)type).ContainsGCPointers)
            {
                flags |= (UInt16)EETypeFlags.HasPointersFlag;
            }
            else if (type.IsArray && !type.IsCanonicalSubtype(CanonicalFormKind.Universal))
            {
                var arrayElementType = ((ArrayType)type).ElementType;
                if ((arrayElementType.IsValueType && ((DefType)arrayElementType).ContainsGCPointers) || arrayElementType.IsGCPointer)
                {
                    flags |= (UInt16)EETypeFlags.HasPointersFlag;
                }
            }

            if (type.HasInstantiation)
            {
                flags |= (UInt16)EETypeFlags.IsGenericFlag;

                if (type.HasVariance)
                {
                    flags |= (UInt16)EETypeFlags.GenericVarianceFlag;
                }
            }

            return flags;
        }

        // These masks and paddings have been chosen so that the ValueTypePadding field can always fit in a byte of data
        // if the alignment is 8 bytes or less. If the alignment is higher then there may be a need for more bits to hold
        // the rest of the padding data.
        // If paddings of greater than 7 bytes are necessary, then the high bits of the field represent that padding
        private const UInt32 ValueTypePaddingLowMask = 0x7;
        private const UInt32 ValueTypePaddingHighMask = 0xFFFFFF00;
        private const UInt32 ValueTypePaddingMax = 0x07FFFFFF;
        private const int ValueTypePaddingHighShift = 8;
        private const UInt32 ValueTypePaddingAlignmentMask = 0xF8;
        private const int ValueTypePaddingAlignmentShift = 3;

        /// <summary>
        /// Compute the encoded value type padding and alignment that are stored as optional fields on an
        /// <c>EEType</c>. This padding as added to naturally align value types when laid out as fields
        /// of objects on the GCHeap. The amount of padding is recorded to allow unboxing to locals /
        /// arrays of value types which don't need it.
        /// </summary>
        internal static UInt32 ComputeValueTypeFieldPaddingFieldValue(UInt32 padding, UInt32 alignment, int targetPointerSize)
        {
            // For the default case, return 0
            if ((padding == 0) && (alignment == targetPointerSize))
                return 0;

            UInt32 alignmentLog2 = 0;
            Debug.Assert(alignment != 0);

            while ((alignment & 1) == 0)
            {
                alignmentLog2++;
                alignment = alignment >> 1;
            }
            Debug.Assert(alignment == 1);

            Debug.Assert(ValueTypePaddingMax >= padding);

            // Our alignment values here are adjusted by one to allow for a default of 0 (which represents pointer alignment)
            alignmentLog2++;

            UInt32 paddingLowBits = padding & ValueTypePaddingLowMask;
            UInt32 paddingHighBits = ((padding & ~ValueTypePaddingLowMask) >> ValueTypePaddingAlignmentShift) << ValueTypePaddingHighShift;
            UInt32 alignmentLog2Bits = alignmentLog2 << ValueTypePaddingAlignmentShift;
            Debug.Assert((alignmentLog2Bits & ~ValueTypePaddingAlignmentMask) == 0);
            return paddingLowBits | paddingHighBits | alignmentLog2Bits;
        }
    }
}
