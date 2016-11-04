// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.TypeSystem;
using System.Diagnostics;
using Internal.NativeFormat;
using System.Collections.Generic;
using Internal.Runtime.Augments;

namespace Internal.Runtime.TypeLoader
{
    /// <summary>
    /// Reads field layout based on native layout data
    /// information
    /// </summary>
    internal class NativeLayoutFieldAlgorithm : FieldLayoutAlgorithm
    {
        private NoMetadataFieldLayoutAlgorithm _noMetadataFieldLayoutAlgorithm = new NoMetadataFieldLayoutAlgorithm();
        private const int InstanceAlignmentEntry = 4;

        public unsafe override bool ComputeContainsGCPointers(DefType type)
        {
            if (type.IsTemplateCanonical())
            {
                return type.ComputeTemplate().RuntimeTypeHandle.ToEETypePtr()->HasGCPointers;
            }
            else
            {
                if (type.RetrieveRuntimeTypeHandleIfPossible())
                {
                    return type.RuntimeTypeHandle.ToEETypePtr()->HasGCPointers;
                }

                return type.GetOrCreateTypeBuilderState().InstanceGCLayout != null;
            }
        }

        public override ComputedInstanceFieldLayout ComputeInstanceLayout(DefType type, InstanceLayoutKind layoutKind)
        {
            if (!type.IsTemplateUniversal() && (layoutKind == InstanceLayoutKind.TypeOnly))
            {
                // Non universal generics can just use the template's layout
                DefType template = (DefType)type.ComputeTemplate();
                return _noMetadataFieldLayoutAlgorithm.ComputeInstanceLayout(template, InstanceLayoutKind.TypeOnly);
            }

            // Only needed for universal generics, or when looking up an offset for a field for a universal generic
            LowLevelList<int> fieldOffsets;
            int[] position = ComputeTypeSizeAndAlignment(type, FieldLoadState.Instance, out fieldOffsets);

            int numInstanceFields = 0;
            foreach (NativeLayoutFieldDesc field in type.NativeLayoutFields)
            {
                if (!field.IsStatic)
                {
                    numInstanceFields++;
                }
            }

            int byteCountAlignment = position[InstanceAlignmentEntry];
            byteCountAlignment = type.Context.Target.GetObjectAlignment(byteCountAlignment);

            ComputedInstanceFieldLayout layout = new ComputedInstanceFieldLayout()
            {
                Offsets = new FieldAndOffset[numInstanceFields],
                ByteCountAlignment = byteCountAlignment,
                ByteCountUnaligned = position[(int)NativeFormat.FieldStorage.Instance],
                PackValue = 0 // TODO, as we add more metadata handling logic, find out if its necessary to use a meaningful value here
            };

            if (!type.IsValueType)
            {
                layout.FieldAlignment = type.Context.Target.PointerSize;
                layout.FieldSize = type.Context.Target.PointerSize;
            }
            else
            {
                layout.FieldAlignment = position[InstanceAlignmentEntry];
                layout.FieldSize = MemoryHelpers.AlignUp(position[(int)NativeFormat.FieldStorage.Instance], layout.FieldAlignment);
            }

            int curInstanceField = 0;
            foreach (NativeLayoutFieldDesc field in type.NativeLayoutFields)
            {
                if (!field.IsStatic)
                {
                    layout.Offsets[curInstanceField] = new FieldAndOffset(field, fieldOffsets[curInstanceField]);
                    curInstanceField++;
                }
            }

            return layout;
        }

        public override ComputedStaticFieldLayout ComputeStaticFieldLayout(DefType type, StaticLayoutKind layoutKind)
        {
            if (!type.IsTemplateUniversal() && (layoutKind == StaticLayoutKind.StaticRegionSizes))
            {
                return ParseStaticRegionSizesFromNativeLayout(type);
            }

            LowLevelList<int> fieldOffsets;
            int[] position = ComputeTypeSizeAndAlignment(type, FieldLoadState.Statics, out fieldOffsets);

            int numStaticFields = 0;
            foreach (NativeLayoutFieldDesc field in type.NativeLayoutFields)
            {
                if (field.IsStatic)
                {
                    numStaticFields++;
                }
            }

            ComputedStaticFieldLayout layout = new ComputedStaticFieldLayout();

            layout.Offsets = new FieldAndOffset[numStaticFields];

            if (numStaticFields > 0)
            {
                layout.GcStatics = new StaticsBlock() { Size = position[(int)NativeFormat.FieldStorage.GCStatic], LargestAlignment = DefType.MaximumAlignmentPossible };
                layout.NonGcStatics = new StaticsBlock() { Size = position[(int)NativeFormat.FieldStorage.NonGCStatic], LargestAlignment = DefType.MaximumAlignmentPossible };
                layout.ThreadStatics = new StaticsBlock() { Size = position[(int)NativeFormat.FieldStorage.TLSStatic], LargestAlignment = DefType.MaximumAlignmentPossible };
            }

            int curStaticField = 0;
            foreach (NativeLayoutFieldDesc field in type.NativeLayoutFields)
            {
                if (field.IsStatic)
                {
                    layout.Offsets[curStaticField] = new FieldAndOffset(field, fieldOffsets[curStaticField]);
                    curStaticField++;
                }
            }

            return layout;
        }

        private ComputedStaticFieldLayout ParseStaticRegionSizesFromNativeLayout(TypeDesc type)
        {
            int nonGcDataSize = 0;
            int gcDataSize = 0;
            int threadDataSize = 0;

            TypeBuilderState state = type.GetOrCreateTypeBuilderState();
            NativeParser typeInfoParser = state.GetParserForNativeLayoutInfo();

            BagElementKind kind;
            while ((kind = typeInfoParser.GetBagElementKind()) != BagElementKind.End)
            {
                switch (kind)
                {
                    case BagElementKind.NonGcStaticDataSize:
                        TypeLoaderLogger.WriteLine("Found BagElementKind.NonGcStaticDataSize");
                        // Use checked typecast to int to ensure there aren't any overflows/truncations (size value used in allocation of memory later)
                        nonGcDataSize = checked((int)typeInfoParser.GetUnsigned());
                        break;

                    case BagElementKind.GcStaticDataSize:
                        TypeLoaderLogger.WriteLine("Found BagElementKind.GcStaticDataSize");
                        // Use checked typecast to int to ensure there aren't any overflows/truncations (size value used in allocation of memory later)
                        gcDataSize = checked((int)typeInfoParser.GetUnsigned());
                        break;

                    case BagElementKind.ThreadStaticDataSize:
                        TypeLoaderLogger.WriteLine("Found BagElementKind.ThreadStaticDataSize");
                        // Use checked typecast to int to ensure there aren't any overflows/truncations (size value used in allocation of memory later)
                        threadDataSize = checked((int)typeInfoParser.GetUnsigned());
                        break;

                    default:
                        typeInfoParser.SkipInteger();
                        break;
                }
            }

            ComputedStaticFieldLayout staticLayout = new ComputedStaticFieldLayout()
            {
                GcStatics = new StaticsBlock() { Size = gcDataSize, LargestAlignment = DefType.MaximumAlignmentPossible },
                NonGcStatics = new StaticsBlock() { Size = nonGcDataSize, LargestAlignment = DefType.MaximumAlignmentPossible },
                Offsets = null, // We're not computing field offsets here, so return null
                ThreadStatics = new StaticsBlock() { Size = threadDataSize, LargestAlignment = DefType.MaximumAlignmentPossible },
            };

            return staticLayout;
        }

        internal static void EnsureFieldLayoutLoadedForGenericType(DefType type)
        {
            if (type.NativeLayoutFields != null)
                return;

            if (!type.IsTemplateUniversal())
            {
                // We can hit this case where the template of type in question is not a universal canonical type.
                // Example:
                //  BaseType<T> { ... }
                //  DerivedType<T, U> : BaseType<T> { ... }
                // and an instantiation like DerivedType<string, int>. In that case, BaseType<string> will have a non-universal
                // template type, and requires special handling to compute its size and field layout.
                EnsureFieldLayoutLoadedForNonUniversalType(type);
            }
            else
            {
                TypeBuilderState state = type.GetOrCreateTypeBuilderState();
                NativeParser typeInfoParser = state.GetParserForNativeLayoutInfo();
                NativeParser fieldLayoutParser = typeInfoParser.GetParserForBagElementKind(BagElementKind.FieldLayout);
                EnsureFieldLayoutLoadedForUniversalType(type, state.NativeLayoutInfo.LoadContext, fieldLayoutParser);
            }
        }

        private static void EnsureFieldLayoutLoadedForUniversalType(DefType type, NativeLayoutInfoLoadContext loadContext, NativeParser fieldLayoutParser)
        {
            Debug.Assert(type.HasInstantiation);
            Debug.Assert(type.ComputeTemplate().IsCanonicalSubtype(CanonicalFormKind.Universal));

            if (type.NativeLayoutFields != null)
                return;

            type.NativeLayoutFields = ParseFieldLayout(type, loadContext, fieldLayoutParser);
        }

        private static void EnsureFieldLayoutLoadedForNonUniversalType(DefType type)
        {
            Debug.Assert(type.HasInstantiation);
            Debug.Assert(!type.ComputeTemplate().IsCanonicalSubtype(CanonicalFormKind.Universal));

            if (type.NativeLayoutFields != null)
                return;

            // Look up the universal template for this type.  Only the universal template has field layout
            // information, so we have to use it to parse the field layout.
            NativeLayoutInfoLoadContext universalLayoutLoadContext;
            NativeParser typeInfoParser = type.GetOrCreateTypeBuilderState().GetParserForUniversalNativeLayoutInfo(out universalLayoutLoadContext);

            if (typeInfoParser.IsNull)
                throw new TypeBuilder.MissingTemplateException();

            // Now parse that layout into the NativeLayoutFields array.
            NativeParser fieldLayoutParser = typeInfoParser.GetParserForBagElementKind(BagElementKind.FieldLayout);
            type.NativeLayoutFields = ParseFieldLayout(type, universalLayoutLoadContext, fieldLayoutParser);
        }

        private static NativeLayoutFieldDesc[] ParseFieldLayout(DefType owningType,
            NativeLayoutInfoLoadContext nativeLayoutInfoLoadContext, NativeParser fieldLayoutParser)
        {
            if (fieldLayoutParser.IsNull)
                return Empty<NativeLayoutFieldDesc>.Array;

            uint numFields = fieldLayoutParser.GetUnsigned();
            var fields = new NativeLayoutFieldDesc[numFields];

            for (int i = 0; i < numFields; i++)
            {
                TypeDesc fieldType = nativeLayoutInfoLoadContext.GetType(ref fieldLayoutParser);
                NativeFormat.FieldStorage storage = (NativeFormat.FieldStorage)fieldLayoutParser.GetUnsigned();
                fields[i] = new NativeLayoutFieldDesc(owningType, fieldType, storage);
            }

            return fields;
        }

        /// <summary>
        /// Determine the state of things before we start processing the fields of a specific type.
        /// This will initialize the state to be aware of the size/characteristics of base types,
        /// and whether or not this type is a valuetype.
        /// </summary>
        /// <param name="type">Type we are computing layout for</param>
        /// <param name="initialSize">What the initial Instance size should be</param>
        /// <param name="alignRequired">What is the basic alignment requirement of the base type or 1 if there is no base type to consider</param>
        internal void ComputeTypeSizeBeforeFields(TypeDesc type, out int initialSize, out int alignRequired)
        {
            // Account for the EEType pointer in objects...
            initialSize = IntPtr.Size;
            alignRequired = 1;

            if (type.IsValueType)
            {
                // ...unless the type is a ValueType which doesn't have the EEType pointer.
                initialSize = 0;
            }
            else if (type.BaseType != null)
            {
                // If there is a base type, use the initialSize and alignRequired from that
                DefType baseType = type.BaseType;
                initialSize = baseType.InstanceByteCountUnaligned;
                alignRequired = baseType.InstanceByteAlignment;
            }
        }

        /// <summary>
        /// While computing layout, we don't generally compute the full field information. This function is used to 
        /// gate how much of field layout to run
        /// </summary>
        /// <param name="fieldStorage">the conceptual location of the field</param>
        /// <param name="loadRequested">what sort of load was requested</param>
        /// <returns></returns>
        internal bool ShouldProcessField(NativeFormat.FieldStorage fieldStorage, FieldLoadState loadRequested)
        {
            if (fieldStorage == (int)NativeFormat.FieldStorage.Instance)
            {
                // Make sure we wanted to load instance fields.
                if ((loadRequested & FieldLoadState.Instance) == FieldLoadState.None)
                    return false;
            }
            else if ((loadRequested & FieldLoadState.Statics) == FieldLoadState.None)
            {
                // Otherwise the field is a static, and we only want instance fields.
                return false;
            }

            return true;
        }

        // The layout algorithm should probably compute results and let the caller set things
        internal unsafe int[] ComputeTypeSizeAndAlignment(TypeDesc type, FieldLoadState loadRequested, out LowLevelList<int> fieldOffsets)
        {
            fieldOffsets = null;
            TypeLoaderLogger.WriteLine("Laying out type " + type.ToString() + ". IsValueType: " + (type.IsValueType ? "true" : "false") + ". LoadRequested = " + ((int)loadRequested).LowLevelToString());

            Debug.Assert(loadRequested != FieldLoadState.None);
            Debug.Assert(type is ArrayType || (type is DefType && ((DefType)type).HasInstantiation));

            bool isArray = type is ArrayType;

            int[] position = new int[5];
            int alignRequired = 1;

            if ((loadRequested & FieldLoadState.Instance) == FieldLoadState.Instance)
            {
                ComputeTypeSizeBeforeFields(type, out position[(int)NativeFormat.FieldStorage.Instance], out alignRequired);
            }

            if (!isArray)
            {
                // Once this is done, the NativeLayoutFields on the type are initialized
                EnsureFieldLayoutLoadedForGenericType((DefType)type);
                Debug.Assert(type.NativeLayoutFields != null);
            }

            int instanceFields = 0;

            if (!isArray && type.NativeLayoutFields.Length > 0)
            {
                fieldOffsets = new LowLevelList<int>(type.NativeLayoutFields.Length);
                for (int i = 0; i < type.NativeLayoutFields.Length; i++)
                {
                    TypeDesc fieldType = type.NativeLayoutFields[i].FieldType;
                    int fieldStorage = (int)type.NativeLayoutFields[i].FieldStorage;

                    if (!ShouldProcessField((NativeFormat.FieldStorage)fieldStorage, loadRequested))
                        continue;

                    // For value types, we will attempt to get the size and alignment from
                    // the runtime if possible, otherwise GetFieldSizeAndAlignment will
                    // recurse to lay out nested struct fields.
                    int alignment;
                    int size;
                    GetFieldSizeAlignment(fieldType, out size, out alignment);

                    Debug.Assert(alignment > 0);

                    if (fieldStorage == (int)NativeFormat.FieldStorage.Instance)
                    {
                        instanceFields++;

                        // Ensure alignment of type is sufficient for this field
                        if (alignRequired < alignment)
                            alignRequired = alignment;
                    }

                    position[fieldStorage] = MemoryHelpers.AlignUp(position[fieldStorage], alignment);
                    TypeLoaderLogger.WriteLine(" --> Field type " + fieldType.ToString() +
                        " storage " + ((uint)(type.NativeLayoutFields[i].FieldStorage)).LowLevelToString() +
                        " offset " + position[fieldStorage].LowLevelToString() +
                        " alignment " + alignment.LowLevelToString());

                    fieldOffsets.Add(position[fieldStorage]);
                    position[fieldStorage] += size;
                }
            }

            // Pad the length of structs to be 1 if they are empty so we have no zero-length structures
            if ((position[(int)NativeFormat.FieldStorage.Instance] == 0) && type.IsValueType)
                position[(int)NativeFormat.FieldStorage.Instance] = 1;

            Debug.Assert(alignRequired == 1 || alignRequired == 2 || alignRequired == 4 || alignRequired == 8);

            position[InstanceAlignmentEntry] = alignRequired;

            return position;
        }

        internal void GetFieldSizeAlignment(TypeDesc fieldType, out int size, out int alignment)
        {
            Debug.Assert(!fieldType.IsCanonicalSubtype(CanonicalFormKind.Any));

            // All reference and array types are pointer-sized
            if (!fieldType.IsValueType)
            {
                size = IntPtr.Size;
                alignment = IntPtr.Size;
                return;
            }

            // Is this a type that already exists? If so, get its size from the EEType directly
            if (fieldType.RetrieveRuntimeTypeHandleIfPossible())
            {
                unsafe
                {
                    EEType* eeType = fieldType.RuntimeTypeHandle.ToEETypePtr();
                    size = (int)eeType->ValueTypeSize;
                    alignment = eeType->FieldAlignmentRequirement;
                    return;
                }
            }

            // The type of the field must be a generic valuetype that is dynamically being constructed
            Debug.Assert(fieldType.IsValueType);
            DefType fieldDefType = (DefType)fieldType;

            TypeBuilderState state = fieldType.GetOrCreateTypeBuilderState();

            size = fieldDefType.InstanceFieldSize;
            alignment = fieldDefType.InstanceFieldAlignment;
        }

        public override DefType ComputeHomogeneousFloatAggregateElementType(DefType type)
        {
            if (!type.IsValueType)
                return null;

            // Once this is done, the NativeLayoutFields on the type are initialized
            EnsureFieldLayoutLoadedForGenericType((DefType)type);
            Debug.Assert(type.NativeLayoutFields != null);

            // Empty types are not HFA
            if (type.NativeLayoutFields.Length == 0)
                return null;

            DefType currentHfaElementType = null;

            for (int i = 0; i < type.NativeLayoutFields.Length; i++)
            {
                TypeDesc fieldType = type.NativeLayoutFields[i].FieldType;
                if (type.NativeLayoutFields[i].FieldStorage != NativeFormat.FieldStorage.Instance)
                    continue;

                DefType fieldDefType = fieldType as DefType;

                // HFA types cannot contain non-HFA types
                if (fieldDefType == null || !fieldDefType.IsHfa)
                    return null;

                Debug.Assert(fieldDefType.HfaElementType != null);

                if (currentHfaElementType == null)
                    currentHfaElementType = fieldDefType.HfaElementType;
                else if (currentHfaElementType != fieldDefType.HfaElementType)
                    return null; // If the field doesn't have the same HFA type as the one we've looked at before, the type cannot be HFA
            }

            // If we didn't find any instance fields, then this can't be an HFA type
            if (currentHfaElementType == null)
                return null;

            // Note that we check the total size, but do not perform any checks on number of fields:
            // - Type of fields can be HFA valuetype itself
            // - Managed C++ HFA valuetypes have just one <alignment member> of type float to signal that 
            //   the valuetype is HFA and explicitly specified size
            int maxSize = currentHfaElementType.InstanceFieldSize * currentHfaElementType.Context.Target.MaximumHfaElementCount;
            if (type.InstanceFieldSize > maxSize)
                return null;

            return currentHfaElementType;
        }

        public override ValueTypeShapeCharacteristics ComputeValueTypeShapeCharacteristics(DefType type)
        {
            if (!type.IsValueType ||
                ComputeHomogeneousFloatAggregateElementType(type) == null)
            {
                return ValueTypeShapeCharacteristics.None;
            }

            return ValueTypeShapeCharacteristics.HomogenousFloatAggregate;
        }
    }
}
