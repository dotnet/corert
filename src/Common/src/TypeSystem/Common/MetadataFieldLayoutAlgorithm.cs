// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    /// <summary>
    /// MetadataFieldLayout algorithm which can be used to compute field layout
    /// for any MetadataType where all fields are available by calling GetFields.
    /// </summary>
    public class MetadataFieldLayoutAlgorithm : FieldLayoutAlgorithm
    {
        public override ComputedInstanceFieldLayout ComputeInstanceLayout(DefType defType, InstanceLayoutKind layoutKind)
        {
            MetadataType type = (MetadataType)defType;
            // CLI - Partition 1, section 9.5 - Generic types shall not be marked explicitlayout.  
            if (type.HasInstantiation && type.IsExplicitLayout)
            {
                ThrowHelper.ThrowTypeLoadException(ExceptionStringID.ClassLoadExplicitGeneric, type.GetTypeDefinition());
            }

            // Count the number of instance fields in advance for convenience
            int numInstanceFields = 0;
            foreach (var field in type.GetFields())
            {
                if (field.IsStatic)
                    continue;

                TypeDesc fieldType = field.FieldType;

                // ByRef instance fields are not allowed.
                if (fieldType.IsByRef)
                    ThrowHelper.ThrowTypeLoadException(ExceptionStringID.ClassLoadGeneral, type);

                // ByRef-like instance fields on non-byref-like types are not allowed.
                if (fieldType.IsByRefLike && !type.IsByRefLike)
                    ThrowHelper.ThrowTypeLoadException(ExceptionStringID.ClassLoadGeneral, type);

                numInstanceFields++;
            }

            if (type.IsModuleType)
            {
                // This is a global type, it must not have instance fields.
                if (numInstanceFields > 0)
                {
                    ThrowHelper.ThrowTypeLoadException(ExceptionStringID.ClassLoadGeneral, type);
                }

                // Global types do not do the rest of instance field layout.
                ComputedInstanceFieldLayout result = new ComputedInstanceFieldLayout();
                result.Offsets = Array.Empty<FieldAndOffset>();
                return result;
            }

            // CLI - Partition 2, section 22.8
            // A type has layout if it is marked SequentialLayout or ExplicitLayout.  If any type within an inheritance chain has layout, 
            // then so shall all its base classes, up to the one that descends immediately from System.ValueType (if it exists in the type’s 
            // hierarchy); otherwise, from System.Object
            // Note: While the CLI isn't clearly worded, the layout needs to be the same for the entire chain.
            // If the current type isn't ValueType or System.Object and has a layout and the parent type isn't
            // ValueType or System.Object then the layout type attributes need to match
            if ((!type.IsValueType && !type.IsObject) &&
                (type.IsSequentialLayout || type.IsExplicitLayout) &&
                (!type.BaseType.IsValueType && !type.BaseType.IsObject))
            {
                MetadataType baseType = type.MetadataBaseType;

                if (type.IsSequentialLayout != baseType.IsSequentialLayout ||
                    type.IsExplicitLayout != baseType.IsExplicitLayout)
                {
                    ThrowHelper.ThrowTypeLoadException(ExceptionStringID.ClassLoadBadFormat, type);
                }
            }

            // Enum types must have a single instance field
            if (type.IsEnum && numInstanceFields != 1)
            {
                ThrowHelper.ThrowTypeLoadException(ExceptionStringID.ClassLoadGeneral, type);
            }

            if (type.IsPrimitive)
            {
                // Primitive types are special - they may have a single field of the same type
                // as the type itself. They do not do the rest of instance field layout.
                if (numInstanceFields > 1)
                {
                    ThrowHelper.ThrowTypeLoadException(ExceptionStringID.ClassLoadGeneral, type);
                }

                SizeAndAlignment instanceByteSizeAndAlignment;
                var sizeAndAlignment = ComputeInstanceSize(
                    type,
                    type.Context.Target.GetWellKnownTypeSize(type),
                    type.Context.Target.GetWellKnownTypeAlignment(type),
                    out instanceByteSizeAndAlignment
                    );

                ComputedInstanceFieldLayout result = new ComputedInstanceFieldLayout
                {
                    ByteCountUnaligned = instanceByteSizeAndAlignment.Size,
                    ByteCountAlignment = instanceByteSizeAndAlignment.Alignment,
                    FieldAlignment = sizeAndAlignment.Alignment,
                    FieldSize = sizeAndAlignment.Size,
                };

                if (numInstanceFields > 0)
                {
                    FieldDesc instanceField = null;
                    foreach (FieldDesc field in type.GetFields())
                    {
                        if (!field.IsStatic)
                        {
                            Debug.Assert(instanceField == null, "Unexpected extra instance field");
                            instanceField = field;
                        }
                    }

                    Debug.Assert(instanceField != null, "Null instance field");

                    result.Offsets = new FieldAndOffset[] {
                        new FieldAndOffset(instanceField, LayoutInt.Zero)
                    };
                }
                else
                {
                    result.Offsets = Array.Empty<FieldAndOffset>();
                }

                return result;
            }

            // If the type has layout, read its packing and size info
            // If the type has explicit layout, also read the field offset info
            if (type.IsExplicitLayout || type.IsSequentialLayout)
            {
                if (type.IsEnum)
                {
                    ThrowHelper.ThrowTypeLoadException(ExceptionStringID.ClassLoadBadFormat, type);
                }

                var layoutMetadata = type.GetClassLayout();

                // If packing is out of range or not a power of two, throw that the size is invalid
                int packing = layoutMetadata.PackingSize;
                if (packing < 0 || packing > 128 || ((packing & (packing - 1)) != 0))
                {
                    ThrowHelper.ThrowTypeLoadException(ExceptionStringID.ClassLoadBadFormat, type);
                }

                Debug.Assert(layoutMetadata.Offsets == null || layoutMetadata.Offsets.Length == numInstanceFields);
            }

            // At this point all special cases are handled and all inputs validated

            if (type.IsExplicitLayout)
            {
                return ComputeExplicitFieldLayout(type, numInstanceFields);
            }
            else if (type.IsSequentialLayout || type.Context.Target.Abi == TargetAbi.ProjectN)
            {
                return ComputeSequentialFieldLayout(type, numInstanceFields);
            }
            else
            {
                return ComputeAutoFieldLayout(type, numInstanceFields);
            }
        }

        public override ComputedStaticFieldLayout ComputeStaticFieldLayout(DefType defType, StaticLayoutKind layoutKind)
        {
            MetadataType type = (MetadataType)defType;
            int numStaticFields = 0;

            foreach (var field in type.GetFields())
            {
                if (!field.IsStatic || field.HasRva || field.IsLiteral)
                    continue;

                numStaticFields++;
            }

            ComputedStaticFieldLayout result;
            result.GcStatics = new StaticsBlock();
            result.NonGcStatics = new StaticsBlock();
            result.ThreadStatics = new StaticsBlock();

            if (numStaticFields == 0)
            {
                result.Offsets = Array.Empty<FieldAndOffset>();
                return result;
            }

            result.Offsets = new FieldAndOffset[numStaticFields];

            PrepareRuntimeSpecificStaticFieldLayout(type.Context, ref result);

            int index = 0;

            foreach (var field in type.GetFields())
            {
                // Nonstatic fields, literal fields, and RVA mapped fields don't participate in layout
                if (!field.IsStatic || field.HasRva || field.IsLiteral)
                    continue;

                TypeDesc fieldType = field.FieldType;
                if (fieldType.IsByRef || (fieldType.IsValueType && ((DefType)fieldType).IsByRefLike))
                {
                    ThrowHelper.ThrowTypeLoadException(ExceptionStringID.ClassLoadGeneral, type);
                }

                ref StaticsBlock block = ref GetStaticsBlockForField(ref result, field);
                SizeAndAlignment sizeAndAlignment = ComputeFieldSizeAndAlignment(fieldType, type.Context.Target.DefaultPackingSize);

                block.Size = LayoutInt.AlignUp(block.Size, sizeAndAlignment.Alignment);
                result.Offsets[index] = new FieldAndOffset(field, block.Size);
                block.Size = block.Size + sizeAndAlignment.Size;

                block.LargestAlignment = LayoutInt.Max(block.LargestAlignment, sizeAndAlignment.Alignment);

                index++;
            }

            FinalizeRuntimeSpecificStaticFieldLayout(type.Context, ref result);

            return result;
        }

        private ref StaticsBlock GetStaticsBlockForField(ref ComputedStaticFieldLayout layout, FieldDesc field)
        {
            if (field.IsThreadStatic)
                return ref layout.ThreadStatics;
            else if (field.HasGCStaticBase)
                return ref layout.GcStatics;
            else
                return ref layout.NonGcStatics;
        }

        public override bool ComputeContainsGCPointers(DefType type)
        {
            bool someFieldContainsPointers = false;

            foreach (var field in type.GetFields())
            {
                if (field.IsStatic)
                    continue;

                TypeDesc fieldType = field.FieldType;
                if (fieldType.IsValueType)
                {
                    if (fieldType.IsPrimitive)
                        continue;

                    if (((DefType)fieldType).ContainsGCPointers)
                    {
                        someFieldContainsPointers = true;
                        break;
                    }
                }
                else if (fieldType.IsGCPointer)
                {
                    someFieldContainsPointers = true;
                    break;
                }
            }

            return someFieldContainsPointers;
        }

        /// <summary>
        /// Called during static field layout to setup initial contents of statics blocks
        /// </summary>
        protected virtual void PrepareRuntimeSpecificStaticFieldLayout(TypeSystemContext context, ref ComputedStaticFieldLayout layout)
        {
        }

        /// <summary>
        /// Called during static field layout to finish static block layout
        /// </summary>
        protected virtual void FinalizeRuntimeSpecificStaticFieldLayout(TypeSystemContext context, ref ComputedStaticFieldLayout layout)
        {
        }

        private static ComputedInstanceFieldLayout ComputeExplicitFieldLayout(MetadataType type, int numInstanceFields)
        {
            // Instance slice size is the total size of instance not including the base type.
            // It is calculated as the field whose offset and size add to the greatest value.
            LayoutInt cumulativeInstanceFieldPos =
                type.HasBaseType && !type.IsValueType ? type.BaseType.InstanceByteCount : LayoutInt.Zero;
            LayoutInt instanceSize = cumulativeInstanceFieldPos;

            var layoutMetadata = type.GetClassLayout();

            int packingSize = ComputePackingSize(type, layoutMetadata);
            LayoutInt largestAlignmentRequired = LayoutInt.One;

            var offsets = new FieldAndOffset[numInstanceFields];
            int fieldOrdinal = 0;

            foreach (var fieldAndOffset in layoutMetadata.Offsets)
            {
                var fieldSizeAndAlignment = ComputeFieldSizeAndAlignment(fieldAndOffset.Field.FieldType, packingSize);

                largestAlignmentRequired = LayoutInt.Max(fieldSizeAndAlignment.Alignment, largestAlignmentRequired);

                if (fieldAndOffset.Offset == FieldAndOffset.InvalidOffset)
                    ThrowHelper.ThrowTypeLoadException(ExceptionStringID.ClassLoadBadFormat, type);

                LayoutInt computedOffset = fieldAndOffset.Offset + cumulativeInstanceFieldPos;

                if (fieldAndOffset.Field.FieldType.IsGCPointer && !computedOffset.IsIndeterminate)
                {
                    int offsetModulo = computedOffset.AsInt % type.Context.Target.PointerSize;
                    if (offsetModulo != 0)
                    {
                        // GC pointers MUST be aligned.
                        ThrowHelper.ThrowTypeLoadException(ExceptionStringID.ClassLoadExplicitLayout, type, fieldAndOffset.Offset.ToStringInvariant());
                    }
                }

                offsets[fieldOrdinal] = new FieldAndOffset(fieldAndOffset.Field, computedOffset);

                LayoutInt fieldExtent = computedOffset + fieldSizeAndAlignment.Size;
                instanceSize = LayoutInt.Max(fieldExtent, instanceSize);

                fieldOrdinal++;
            }

            if (type.IsValueType)
            {
                instanceSize = LayoutInt.Max(new LayoutInt(layoutMetadata.Size), instanceSize);
            }

            SizeAndAlignment instanceByteSizeAndAlignment;
            var instanceSizeAndAlignment = ComputeInstanceSize(type, instanceSize, largestAlignmentRequired, out instanceByteSizeAndAlignment);

            ComputedInstanceFieldLayout computedLayout = new ComputedInstanceFieldLayout();
            computedLayout.FieldAlignment = instanceSizeAndAlignment.Alignment;
            computedLayout.FieldSize = instanceSizeAndAlignment.Size;
            computedLayout.ByteCountUnaligned = instanceByteSizeAndAlignment.Size;
            computedLayout.ByteCountAlignment = instanceByteSizeAndAlignment.Alignment;
            computedLayout.Offsets = offsets;

            return computedLayout;
        }

        private static ComputedInstanceFieldLayout ComputeSequentialFieldLayout(MetadataType type, int numInstanceFields)
        {
            var offsets = new FieldAndOffset[numInstanceFields];

            // For types inheriting from another type, field offsets continue on from where they left off
            LayoutInt cumulativeInstanceFieldPos = ComputeBytesUsedInParentType(type);

            var layoutMetadata = type.GetClassLayout();

            LayoutInt largestAlignmentRequirement = LayoutInt.One;
            int fieldOrdinal = 0;
            int packingSize = ComputePackingSize(type, layoutMetadata);

            foreach (var field in type.GetFields())
            {
                if (field.IsStatic)
                    continue;

                var fieldSizeAndAlignment = ComputeFieldSizeAndAlignment(field.FieldType, packingSize);

                largestAlignmentRequirement = LayoutInt.Max(fieldSizeAndAlignment.Alignment, largestAlignmentRequirement);

                cumulativeInstanceFieldPos = LayoutInt.AlignUp(cumulativeInstanceFieldPos, fieldSizeAndAlignment.Alignment);
                offsets[fieldOrdinal] = new FieldAndOffset(field, cumulativeInstanceFieldPos);
                cumulativeInstanceFieldPos = checked(cumulativeInstanceFieldPos + fieldSizeAndAlignment.Size);

                fieldOrdinal++;
            }

            if (type.IsValueType)
            {
                cumulativeInstanceFieldPos = LayoutInt.Max(cumulativeInstanceFieldPos, new LayoutInt(layoutMetadata.Size));
            }

            SizeAndAlignment instanceByteSizeAndAlignment;
            var instanceSizeAndAlignment = ComputeInstanceSize(type, cumulativeInstanceFieldPos, largestAlignmentRequirement, out instanceByteSizeAndAlignment);

            ComputedInstanceFieldLayout computedLayout = new ComputedInstanceFieldLayout();
            computedLayout.FieldAlignment = instanceSizeAndAlignment.Alignment;
            computedLayout.FieldSize = instanceSizeAndAlignment.Size;
            computedLayout.ByteCountUnaligned = instanceByteSizeAndAlignment.Size;
            computedLayout.ByteCountAlignment = instanceByteSizeAndAlignment.Alignment;
            computedLayout.Offsets = offsets;

            return computedLayout;
        }

        private static ComputedInstanceFieldLayout ComputeAutoFieldLayout(MetadataType type, int numInstanceFields)
        {
            // For types inheriting from another type, field offsets continue on from where they left off
            LayoutInt cumulativeInstanceFieldPos = ComputeBytesUsedInParentType(type);

            var layoutMetadata = type.GetClassLayout();

            int packingSize = ComputePackingSize(type, layoutMetadata);
            LayoutInt largestAlignmentRequired = LayoutInt.One;

            var offsets = new FieldAndOffset[numInstanceFields];
            int fieldOrdinal = 0;

            // Initialize three different sets of lists to hold the instance fields
            //   1. List of value class fields
            //   2. Lists of GC Pointer fields. These will be stored in a dictionary indexed by the size of the field
            //   3. Lists of remaining fields. These will be stored in a dictionary indexed by the size of the field
            List<FieldDesc> instanceValueClassFieldDescs = new List<FieldDesc>();
            Dictionary<int, List<FieldDesc>> instanceGCPointerFieldsBySize = new Dictionary<int, List<FieldDesc>>();
            Dictionary<int, List<FieldDesc>> instanceNonGCPointerFieldsBySize = new Dictionary<int, List<FieldDesc>>();

            int maxInstanceFieldSize = int.MinValue;

            // Iterate over all fields and do the following
            //   - Add instance fields to the appropriate list in the dictionary (while maintaining the enumerated order)
            //   - Save the largest alignment we've seen
            //   - For non-value class fields, save the largest size we've seen
            foreach (var field in type.GetFields())
            {
                if (field.IsStatic)
                    continue;

                var fieldSizeAndAlignment = ComputeFieldSizeAndAlignment(field.FieldType, packingSize);

                largestAlignmentRequired = LayoutInt.Max(fieldSizeAndAlignment.Alignment, largestAlignmentRequired);

                if (field.FieldType.IsGCPointer)
                {
                    int fieldSizeAsInt = fieldSizeAndAlignment.Size.AsInt;
                    maxInstanceFieldSize = Math.Max(fieldSizeAsInt, maxInstanceFieldSize);

                    if (!instanceGCPointerFieldsBySize.TryGetValue(fieldSizeAsInt, out List<FieldDesc> instanceFieldsWithMatchingSize))
                    {
                        instanceFieldsWithMatchingSize = new List<FieldDesc>();
                        instanceGCPointerFieldsBySize[fieldSizeAsInt] = instanceFieldsWithMatchingSize;
                    }
                    instanceFieldsWithMatchingSize.Add(field);
                }
                else if (IsByValueClass(field.FieldType))
                {
                    instanceValueClassFieldDescs.Add(field);
                }
                else
                {
                    int fieldSizeAsInt = fieldSizeAndAlignment.Size.AsInt;
                    maxInstanceFieldSize = Math.Max(fieldSizeAsInt, maxInstanceFieldSize);

                    if (!instanceNonGCPointerFieldsBySize.TryGetValue(fieldSizeAsInt, out List<FieldDesc> instanceFieldsWithMatchingSize))
                    {
                        instanceFieldsWithMatchingSize = new List<FieldDesc>();
                        instanceNonGCPointerFieldsBySize[fieldSizeAsInt] = instanceFieldsWithMatchingSize;
                    }
                    instanceFieldsWithMatchingSize.Add(field);
                }
            }

            // If the position is Indeterminate, proceed immediately to placing the fields
            if (!cumulativeInstanceFieldPos.IsIndeterminate)
            {
                // Align the field offset to the required alignment of an int: 4
                // Not sure why that is, but a .NET Core 2.1 app seems to align the subclass's fields automatically to this boundary
                cumulativeInstanceFieldPos = LayoutInt.AlignUp(cumulativeInstanceFieldPos, new LayoutInt(4));

                // First, place small fields immediately after the parent field bytes if there are a number of field bytes that are not aligned
                // GC pointer fields and value class fields are not considered for this optimization
                // Place the fields in order, starting with the largest size.
                int parentByteOffsetModulo = cumulativeInstanceFieldPos.AsInt % type.Context.Target.PointerSize;
                if (parentByteOffsetModulo != 0)
                {
                    int alignmentBytesRemaining = type.BaseType.InstanceByteCount.AsInt - cumulativeInstanceFieldPos.AsInt;
                    bool continueProcessingInstanceFields = true;

                    for (int i = maxInstanceFieldSize; continueProcessingInstanceFields && i > 0; i /= 2)
                    {
                        // Continue to next size if the current size will absolutely not fit into the remaining space
                        if (i > alignmentBytesRemaining)
                            continue;

                        if (alignmentBytesRemaining == 0)
                        {
                            break;
                        }

                        Debug.Assert(alignmentBytesRemaining > 0);

                        // Iterate over the variable of size i if we have stored any
                        if (instanceNonGCPointerFieldsBySize.TryGetValue(i, out List<FieldDesc> instanceNonGCPointerFields) && instanceNonGCPointerFields.Count > 0)
                        {
                            while (instanceNonGCPointerFields.Count > 0)
                            {
                                FieldDesc field = instanceNonGCPointerFields[0];
                                var fieldSizeAndAlignment = ComputeFieldSizeAndAlignment(field.FieldType, packingSize);

                                LayoutInt tentativeInstanceFieldOffSet = LayoutInt.AlignUp(cumulativeInstanceFieldPos, fieldSizeAndAlignment.Alignment);
                                LayoutInt tentativeCumulativeInstanceFieldPos = checked(tentativeInstanceFieldOffSet + fieldSizeAndAlignment.Size);

                                // If the alignment and size of the new field would extend past the unaligned space, stop processing this optimization
                                if (tentativeCumulativeInstanceFieldPos.AsInt > type.BaseType.InstanceByteCount.AsInt)
                                {
                                    continueProcessingInstanceFields = false;
                                    break;
                                }

                                offsets[fieldOrdinal] = new FieldAndOffset(field, tentativeInstanceFieldOffSet);
                                cumulativeInstanceFieldPos = tentativeCumulativeInstanceFieldPos;
                                alignmentBytesRemaining = type.BaseType.InstanceByteCount.AsInt - cumulativeInstanceFieldPos.AsInt;

                                fieldOrdinal++;
                                instanceNonGCPointerFields.RemoveAt(0);
                            }
                        }
                    }
                }
            }

            // Next, place GC pointer fields and non-GC pointer fields
            // Starting with the largest-sized fields, place the GC pointer fields in order then place the non-GC pointer fields in order.
            // Once the largest-sized fields are placed, repeat with the next-largest-sized group of fields and continue.
            for (int i = maxInstanceFieldSize; i > 0; i /= 2)
            {
                if (instanceGCPointerFieldsBySize.TryGetValue(i, out List<FieldDesc> instanceGCPointerFields) && instanceGCPointerFields.Count > 0)
                {
                    PlaceOrderedInstanceFields(instanceGCPointerFields, packingSize, offsets, ref cumulativeInstanceFieldPos, ref fieldOrdinal);
                }

                if (instanceNonGCPointerFieldsBySize.TryGetValue(i, out List<FieldDesc> instanceNonGCPointerFields) && instanceNonGCPointerFields.Count > 0)
                {
                    PlaceOrderedInstanceFields(instanceNonGCPointerFields, packingSize, offsets, ref cumulativeInstanceFieldPos, ref fieldOrdinal);
                }
            }

            // Place value class fields last
            PlaceOrderedInstanceFields(instanceValueClassFieldDescs, packingSize, offsets, ref cumulativeInstanceFieldPos, ref fieldOrdinal);

            if (type.IsValueType)
            {
                cumulativeInstanceFieldPos = LayoutInt.Max(cumulativeInstanceFieldPos, new LayoutInt(layoutMetadata.Size));
            }

            SizeAndAlignment instanceByteSizeAndAlignment;
            var instanceSizeAndAlignment = ComputeInstanceSize(type, cumulativeInstanceFieldPos, largestAlignmentRequired, out instanceByteSizeAndAlignment);

            ComputedInstanceFieldLayout computedLayout = new ComputedInstanceFieldLayout();
            computedLayout.FieldAlignment = instanceSizeAndAlignment.Alignment;
            computedLayout.FieldSize = instanceSizeAndAlignment.Size;
            computedLayout.ByteCountUnaligned = instanceByteSizeAndAlignment.Size;
            computedLayout.ByteCountAlignment = instanceByteSizeAndAlignment.Alignment;
            computedLayout.Offsets = offsets;

            return computedLayout;
        }

        private static void PlaceOrderedInstanceFields(IEnumerable<FieldDesc> fieldDescs, int packingSize, FieldAndOffset[] offsets, ref LayoutInt instanceFieldPos, ref int fieldOrdinal)
        {
            foreach (var field in fieldDescs)
            {
                var fieldSizeAndAlignment = ComputeFieldSizeAndAlignment(field.FieldType, packingSize);

                instanceFieldPos = LayoutInt.AlignUp(instanceFieldPos, fieldSizeAndAlignment.Alignment);
                offsets[fieldOrdinal] = new FieldAndOffset(field, instanceFieldPos);
                instanceFieldPos = checked(instanceFieldPos + fieldSizeAndAlignment.Size);

                fieldOrdinal++;
            }
        }

        private static bool IsByValueClass(TypeDesc type)
        {
            return type.IsValueType && !type.IsPrimitive;
        }

        private static LayoutInt ComputeBytesUsedInParentType(DefType type)
        {
            LayoutInt cumulativeInstanceFieldPos = LayoutInt.Zero;

            if (!type.IsValueType && type.HasBaseType)
            {
                cumulativeInstanceFieldPos = type.BaseType.InstanceByteCountUnaligned;
            }

            return cumulativeInstanceFieldPos;
        }

        private static SizeAndAlignment ComputeFieldSizeAndAlignment(TypeDesc fieldType, int packingSize)
        {
            SizeAndAlignment result;

            if (fieldType.IsDefType)
            {
                if (fieldType.IsValueType)
                {
                    DefType metadataType = (DefType)fieldType;
                    result.Size = metadataType.InstanceFieldSize;
                    result.Alignment = metadataType.InstanceFieldAlignment;
                }
                else
                {
                    result.Size = fieldType.Context.Target.LayoutPointerSize;
                    result.Alignment = fieldType.Context.Target.LayoutPointerSize;
                }
            }
            else if (fieldType.IsArray)
            {
                // This could use InstanceFieldSize/Alignment (and those results should match what's here)
                // but, its more efficient to just assume pointer size instead of fulling processing
                // the instance field layout of fieldType here.
                result.Size = fieldType.Context.Target.LayoutPointerSize;
                result.Alignment = fieldType.Context.Target.LayoutPointerSize;
            }
            else
            {
                Debug.Assert(fieldType.IsPointer || fieldType.IsFunctionPointer);
                result.Size = fieldType.Context.Target.LayoutPointerSize;
                result.Alignment = fieldType.Context.Target.LayoutPointerSize;
            }

            result.Alignment = LayoutInt.Min(result.Alignment, new LayoutInt(packingSize));

            return result;
        }

        private static int ComputePackingSize(MetadataType type, ClassLayoutMetadata layoutMetadata)
        {
            // If a type contains pointers then the metadata specified packing size is ignored (On desktop this is disqualification from ManagedSequential)
            if (layoutMetadata.PackingSize == 0 || type.ContainsGCPointers)
                return type.Context.Target.DefaultPackingSize;
            else
                return layoutMetadata.PackingSize;
        }

        private static SizeAndAlignment ComputeInstanceSize(MetadataType type, LayoutInt instanceSize, LayoutInt alignment, out SizeAndAlignment byteCount)
        {
            SizeAndAlignment result;

            int targetPointerSize = type.Context.Target.PointerSize;

            // Pad the length of structs to be 1 if they are empty so we have no zero-length structures
            if (type.IsValueType && instanceSize == LayoutInt.Zero)
            {
                instanceSize = LayoutInt.One;
            }

            if (type.IsValueType)
            {
                instanceSize = LayoutInt.AlignUp(instanceSize, alignment);
                result.Size = instanceSize;
                result.Alignment = alignment;
            }
            else
            {
                result.Size = new LayoutInt(targetPointerSize);
                result.Alignment = new LayoutInt(targetPointerSize);
                if (type.HasBaseType)
                    alignment = LayoutInt.Max(alignment, type.BaseType.InstanceByteAlignment);
            }

            // Determine the alignment needed by the type when allocated
            // This is target specific, and not just pointer sized due to 
            // 8 byte alignment requirements on ARM for longs and doubles
            alignment = type.Context.Target.GetObjectAlignment(alignment);

            byteCount.Size = instanceSize;
            byteCount.Alignment = alignment;

            return result;
        }

        public override ValueTypeShapeCharacteristics ComputeValueTypeShapeCharacteristics(DefType type)
        {
            if (!type.IsValueType)
                return ValueTypeShapeCharacteristics.None;

            ValueTypeShapeCharacteristics result = ComputeHomogeneousFloatAggregateCharacteristic(type);

            // TODO: System V AMD64 characteristics (https://github.com/dotnet/corert/issues/158)

            return result;
        }

        private ValueTypeShapeCharacteristics ComputeHomogeneousFloatAggregateCharacteristic(DefType type)
        {
            Debug.Assert(type.IsValueType);

            MetadataType metadataType = (MetadataType)type;

            // No HFAs with explicit layout. There may be cases where explicit layout may be still
            // eligible for HFA, but it is hard to tell the real intent. Make it simple and just 
            // unconditionally disable HFAs for explicit layout.
            if (metadataType.IsExplicitLayout)
                return ValueTypeShapeCharacteristics.None;

            switch (metadataType.Category)
            {
                case TypeFlags.Single:
                case TypeFlags.Double:
                    // These are the primitive types that constitute a HFA type.
                    return ValueTypeShapeCharacteristics.HomogenousFloatAggregate;

                case TypeFlags.ValueType:
                    DefType expectedElementType = null;

                    foreach (FieldDesc field in metadataType.GetFields())
                    {
                        if (field.IsStatic)
                            continue;

                        // If a field isn't a DefType, then this type cannot be an HFA type
                        // If a field isn't a HFA type, then this type cannot be an HFA type
                        DefType fieldType = field.FieldType as DefType;
                        if (fieldType == null || !fieldType.IsHfa)
                            return ValueTypeShapeCharacteristics.None;

                        if (expectedElementType == null)
                        {
                            // If we hadn't yet figured out what form of HFA this type might be, we've
                            // now found one case.
                            expectedElementType = fieldType.HfaElementType;
                            Debug.Assert(expectedElementType != null);
                        }
                        else if (expectedElementType != fieldType.HfaElementType)
                        {
                            // If we had already determined the possible HFA type of the current type, but
                            // the field we've encountered is not of that type, then the current type cannot
                            // be an HFA type.
                            return ValueTypeShapeCharacteristics.None;
                        }
                    }

                    // No fields means this is not HFA.
                    if (expectedElementType == null)
                        return ValueTypeShapeCharacteristics.None;

                    // Types which are indeterminate in field size are not considered to be HFA
                    if (expectedElementType.InstanceFieldSize.IsIndeterminate)
                        return ValueTypeShapeCharacteristics.None;

                    // Types which are indeterminate in field size are not considered to be HFA
                    if (type.InstanceFieldSize.IsIndeterminate)
                        return ValueTypeShapeCharacteristics.None;

                    // Note that we check the total size, but do not perform any checks on number of fields:
                    // - Type of fields can be HFA valuetype itself
                    // - Managed C++ HFA valuetypes have just one <alignment member> of type float to signal that 
                    //   the valuetype is HFA and explicitly specified size
                    int maxSize = expectedElementType.InstanceFieldSize.AsInt * expectedElementType.Context.Target.MaximumHfaElementCount;
                    if (type.InstanceFieldSize.AsInt > maxSize)
                        return ValueTypeShapeCharacteristics.None;

                    // All the tests passed. This is an HFA type.
                    return ValueTypeShapeCharacteristics.HomogenousFloatAggregate;
            }

            return ValueTypeShapeCharacteristics.None;
        }

        public override DefType ComputeHomogeneousFloatAggregateElementType(DefType type)
        {
            if (!type.IsHfa)
                return null;

            if (type.IsWellKnownType(WellKnownType.Double) || type.IsWellKnownType(WellKnownType.Single))
                return type;

            for (;;)
            {
                Debug.Assert(type.IsValueType);

                // All HFA fields have to be of the same HFA type, so we can just return the type of the first field
                TypeDesc firstFieldType = null;
                foreach (var field in type.GetFields())
                {
                    if (field.IsStatic)
                        continue;

                    firstFieldType = field.FieldType;
                    break;
                }
                Debug.Assert(firstFieldType != null, "Why is IsHfa true on this type?");

                switch (firstFieldType.Category)
                {
                    case TypeFlags.Single:
                    case TypeFlags.Double:
                        return (DefType)firstFieldType;

                    case TypeFlags.ValueType:
                        // Drill into the struct and find the type of its first field
                        type = (DefType)firstFieldType;
                        break;

                    default:
                        Debug.Fail("Why is IsHfa true on this type?");
                        return null;
                }
            }
        }

        private struct SizeAndAlignment
        {
            public LayoutInt Size;
            public LayoutInt Alignment;
        }
    }
}
