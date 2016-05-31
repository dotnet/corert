// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

using Internal.TypeSystem;

namespace Internal.Runtime
{
    internal static class EETypeBuilderHelpers
    {
        private static CorElementType ComputeRhCorElementType(TypeDesc type)
        {
            Debug.Assert(type.IsPrimitive);
            Debug.Assert(type.Category != TypeFlags.Unknown);

            switch (type.Category)
            {
                case TypeFlags.Void:
                    return CorElementType.ELEMENT_TYPE_VOID;
                case TypeFlags.Boolean:
                    return CorElementType.ELEMENT_TYPE_BOOLEAN;
                case TypeFlags.Char:
                    return CorElementType.ELEMENT_TYPE_CHAR;
                case TypeFlags.SByte:
                    return CorElementType.ELEMENT_TYPE_I1;
                case TypeFlags.Byte:
                    return CorElementType.ELEMENT_TYPE_U1;
                case TypeFlags.Int16:
                    return CorElementType.ELEMENT_TYPE_I2;
                case TypeFlags.UInt16:
                    return CorElementType.ELEMENT_TYPE_U2;
                case TypeFlags.Int32:
                    return CorElementType.ELEMENT_TYPE_I4;
                case TypeFlags.UInt32:
                    return CorElementType.ELEMENT_TYPE_U4;
                case TypeFlags.Int64:
                    return CorElementType.ELEMENT_TYPE_I8;
                case TypeFlags.UInt64:
                    return CorElementType.ELEMENT_TYPE_U8;
                case TypeFlags.IntPtr:
                    return CorElementType.ELEMENT_TYPE_I;
                case TypeFlags.UIntPtr:
                    return CorElementType.ELEMENT_TYPE_U;
                case TypeFlags.Single:
                    return CorElementType.ELEMENT_TYPE_R4;
                case TypeFlags.Double:
                    return CorElementType.ELEMENT_TYPE_R8;
                default:
                    break;
            }

            Debug.Assert(false, "Primitive type value expected.");
            return 0;
        }

        public static UInt16 ComputeFlags(TypeDesc type)
        {
            UInt16 flags = (UInt16)EETypeKind.CanonicalEEType;

            if (type.IsInterface)
            {
                flags |= (UInt16)EETypeFlags.IsInterfaceFlag;
            }

            if (type.IsValueType)
            {
                flags |= (UInt16)EETypeFlags.ValueTypeFlag;
            }

            if (type.IsGenericDefinition)
            {
                flags |= (UInt16)EETypeKind.GenericTypeDefEEType;

                // Generic type definition EETypes don't set the other flags.
                return flags;
            }

            if (type.IsArray || type.IsPointer)
            {
                flags = (UInt16)EETypeKind.ParameterizedEEType;
            }

            if (type.HasFinalizer)
            {
                flags |= (UInt16)EETypeFlags.HasFinalizerFlag;
            }

            if (type.IsDefType && ((DefType)type).ContainsGCPointers)
            {
                flags |= (UInt16)EETypeFlags.HasPointersFlag;
            }
            else if (type.IsArray)
            {
                var elementType = ((ArrayType)type).ElementType;
                if ((elementType.IsValueType && ((DefType)elementType).ContainsGCPointers) || elementType.IsGCPointer)
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

            CorElementType corElementType = CorElementType.ELEMENT_TYPE_END;

            // The top 5 bits of flags are used to convey enum underlying type, primitive type, or mark the type as being System.Array
            if (type.IsEnum)
            {
                TypeDesc underlyingType = type.UnderlyingType;
                Debug.Assert(TypeFlags.SByte <= underlyingType.Category && underlyingType.Category <= TypeFlags.UInt64);
                corElementType = ComputeRhCorElementType(underlyingType);
            }
            else if (type.IsPrimitive)
            {
                corElementType = ComputeRhCorElementType(type);
            }
            else if (type.IsWellKnownType(WellKnownType.Array))
            {
                // Mark System.Array with CorElementType so casting code can distinguish it
                corElementType = CorElementType.ELEMENT_TYPE_ARRAY;
            }

            if (corElementType != CorElementType.ELEMENT_TYPE_END)
            {
                flags |= (UInt16)((UInt16)corElementType << (UInt16)EETypeFlags.CorElementTypeShift);
            }

            return flags;
        }

        public static bool ComputeRequiresAlign8(TypeDesc type)
        {
            if (type.Context.Target.Architecture != TargetArchitecture.ARM)
            {
                return false;
            }

            if (type.IsArray)
            {
                var elementType = ((ArrayType)type).ElementType;
                if ((elementType.IsValueType) && ((DefType)elementType).InstanceByteAlignment > 4)
                {
                    return true;
                }
            }
            else if (type.IsDefType && ((DefType)type).InstanceByteAlignment > 4)
            {
                return true;
            }

            return false;
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
        internal static UInt32 ComputeValueTypeFieldPaddingFieldValue(UInt32 padding, UInt32 alignment)
        {
            // For the default case, return 0
            if ((padding == 0) && (alignment == IntPtr.Size))
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
