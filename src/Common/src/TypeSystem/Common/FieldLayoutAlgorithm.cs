// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    public abstract class FieldLayoutAlgorithm
    {
        /// <summary>
        /// Compute the instance field layout for a DefType. Must not depend on static field layout for any other type.
        /// </summary>
        public abstract ComputedInstanceFieldLayout ComputeInstanceFieldLayout(DefType type);

        /// <summary>
        /// Compute the static field layout for a DefType. Must not depend on static field layout for any other type.
        /// </summary>
        public abstract ComputedStaticFieldLayout ComputeStaticFieldLayout(DefType type);

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

        public FieldAndOffset[] Offsets;
    }
}
