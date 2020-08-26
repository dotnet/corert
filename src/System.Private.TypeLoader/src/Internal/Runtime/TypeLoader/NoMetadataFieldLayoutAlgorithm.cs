// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Internal.TypeSystem;
using System.Diagnostics;

namespace Internal.Runtime.TypeLoader
{
    /// <summary>
    /// Useable when we have runtime EEType structures. Can represent the field layout necessary 
    /// to represent the size/alignment of the overall type, but must delegate to either NativeLayoutFieldAlgorithm
    /// or MetadataFieldLayoutAlgorithm to get information about individual fields.
    /// </summary>
    internal class NoMetadataFieldLayoutAlgorithm : FieldLayoutAlgorithm
    {
        private MetadataFieldLayoutAlgorithm _metadataFieldLayoutAlgorithm = new MetadataFieldLayoutAlgorithm();
        private static NativeLayoutFieldAlgorithm s_nativeLayoutFieldAlgorithm = new NativeLayoutFieldAlgorithm();

        public unsafe override bool ComputeContainsGCPointers(DefType type)
        {
            return type.RuntimeTypeHandle.ToEETypePtr()->HasGCPointers;
        }

        /// <summary>
        /// Reads the minimal information about type layout encoded in the 
        /// EEType. That doesn't include field information.
        /// </summary>
        public unsafe override ComputedInstanceFieldLayout ComputeInstanceLayout(DefType type, InstanceLayoutKind layoutKind)
        {
            // If we need the field information, delegate to the native layout algorithm or metadata algorithm
            if (layoutKind != InstanceLayoutKind.TypeOnly)
            {
                if (type.HasNativeLayout)
                    return s_nativeLayoutFieldAlgorithm.ComputeInstanceLayout(type, layoutKind);
                else
                    return _metadataFieldLayoutAlgorithm.ComputeInstanceLayout(type, layoutKind);
            }

            type.RetrieveRuntimeTypeHandleIfPossible();
            Debug.Assert(!type.RuntimeTypeHandle.IsNull());
            EEType* eeType = type.RuntimeTypeHandle.ToEETypePtr();

            ComputedInstanceFieldLayout layout = new ComputedInstanceFieldLayout()
            {
                ByteCountAlignment = new LayoutInt(IntPtr.Size),
                ByteCountUnaligned = new LayoutInt(eeType->IsInterface ? IntPtr.Size : checked((int)eeType->FieldByteCountNonGCAligned)),
                FieldAlignment = new LayoutInt(eeType->FieldAlignmentRequirement),
                Offsets = (layoutKind == InstanceLayoutKind.TypeOnly) ? null : Array.Empty<FieldAndOffset>(), // No fields in EETypes
            };

            if (eeType->IsValueType)
            {
                int valueTypeSize = checked((int)eeType->ValueTypeSize);
                layout.FieldSize = new LayoutInt(valueTypeSize);
            }
            else
            {
                layout.FieldSize = new LayoutInt(IntPtr.Size);
            }

            if ((eeType->RareFlags & EETypeRareFlags.RequiresAlign8Flag) == EETypeRareFlags.RequiresAlign8Flag)
            {
                layout.ByteCountAlignment = new LayoutInt(8);
            }

            return layout;
        }

        public override ComputedStaticFieldLayout ComputeStaticFieldLayout(DefType type, StaticLayoutKind layoutKind)
        {
            // We can only reach this for pre-created types where we actually need field information
            // In that case, fall through to one of the other field layout algorithms.
            if (type.HasNativeLayout)
                return s_nativeLayoutFieldAlgorithm.ComputeStaticFieldLayout(type, layoutKind);
            else if (type is MetadataType)
                return _metadataFieldLayoutAlgorithm.ComputeStaticFieldLayout(type, layoutKind);

            // No statics information available
            ComputedStaticFieldLayout staticLayout = new ComputedStaticFieldLayout()
            {
                GcStatics = default(StaticsBlock),
                NonGcStatics = default(StaticsBlock),
                Offsets = Array.Empty<FieldAndOffset>(), // No fields are considered to exist for completely NoMetadataTypes
                ThreadGcStatics = default(StaticsBlock),
                ThreadNonGcStatics = default(StaticsBlock),
            };
            return staticLayout;
        }

        public override ValueTypeShapeCharacteristics ComputeValueTypeShapeCharacteristics(DefType type)
        {
            if (type.Context.Target.Architecture == TargetArchitecture.ARM)
            {
                unsafe
                {
                    // On ARM, the HFA type is encoded into the EEType directly
                    type.RetrieveRuntimeTypeHandleIfPossible();
                    Debug.Assert(!type.RuntimeTypeHandle.IsNull());
                    EEType* eeType = type.RuntimeTypeHandle.ToEETypePtr();

                    if (!eeType->IsHFA)
                        return ValueTypeShapeCharacteristics.None;

                    if (eeType->RequiresAlign8)
                        return ValueTypeShapeCharacteristics.Float64Aggregate;
                    else
                        return ValueTypeShapeCharacteristics.Float32Aggregate;
                }
            }
            else
            {
                Debug.Assert(
                    type.Context.Target.Architecture == TargetArchitecture.X86 ||
                    type.Context.Target.Architecture == TargetArchitecture.X64);

                return ValueTypeShapeCharacteristics.None;
            }
        }
    }
}
