// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    public enum InstanceLayoutKind
    {
        TypeOnly,
        TypeAndFields
    }

    public enum StaticLayoutKind
    {
        StaticRegionSizes,
        StaticRegionSizesAndFields
    }

    public abstract class FieldLayoutAlgorithm
    {
        /// <summary>
        /// Compute the instance field layout for a DefType. Must not depend on static field layout for any other type.
        /// </summary>
        public abstract ComputedInstanceFieldLayout ComputeInstanceLayout(DefType type, InstanceLayoutKind layoutKind);

        /// <summary>
        /// Compute the static field layout for a DefType. Must not depend on static field layout for any other type.
        /// </summary>
        public abstract ComputedStaticFieldLayout ComputeStaticFieldLayout(DefType type, StaticLayoutKind layoutKind);

        /// <summary>
        /// Compute if the fields of the specified type contain a GC pointer
        /// </summary>
        public abstract bool ComputeContainsPointers(DefType type);
    }

    public struct ComputedInstanceFieldLayout
    {
        public int PackValue;
        public int FieldSize;
        public int FieldAlignment;
        public int ByteCountUnaligned;
        public int ByteCountAlignment;

        /// <summary>
        /// If Offsets is non-null, then all field based layout is complete.
        /// Otherwise, only the non-field based data is considered to be complete
        /// </summary>
        public FieldAndOffset[] Offsets;
    }

    public struct StaticsBlock
    {
        public int Size;
        public int LargestAlignment;
    }

    public struct ComputedStaticFieldLayout
    {
        public StaticsBlock NonGcStatics;
        public StaticsBlock GcStatics;
        public StaticsBlock ThreadStatics;

        /// <summary>
        /// If Offsets is non-null, then all field based layout is complete.
        /// Otherwise, only the non-field based data is considered to be complete
        /// </summary>
        public FieldAndOffset[] Offsets;
    }
}
