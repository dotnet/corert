// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler
{
    internal class CompilerMetadataFieldLayoutAlgorithm : MetadataFieldLayoutAlgorithm
    {
        // GC statics start with a pointer to the "EEType" that signals the size and GCDesc to the GC
        public static LayoutInt GetGCStaticFieldOffset(TypeSystemContext context) => context.Target.LayoutPointerSize;

        protected override void PrepareRuntimeSpecificStaticFieldLayout(TypeSystemContext context, ref ComputedStaticFieldLayout layout)
        {
            LayoutInt offset = GetGCStaticFieldOffset(context);
            layout.GcStatics.Size = offset;
            layout.ThreadGcStatics.Size = offset;
        }

        protected override void FinalizeRuntimeSpecificStaticFieldLayout(TypeSystemContext context, ref ComputedStaticFieldLayout layout)
        {
            LayoutInt offset = GetGCStaticFieldOffset(context);

            // If the size of GCStatics is equal to the size set in PrepareRuntimeSpecificStaticFieldLayout, we
            // don't have any GC statics
            if (layout.GcStatics.Size == offset)
            {
                layout.GcStatics.Size = LayoutInt.Zero;
            }
            if (layout.ThreadGcStatics.Size == offset)
            {
                layout.ThreadGcStatics.Size = LayoutInt.Zero;
            }

            // CoreRT makes no distinction between Gc / non-Gc thread statics. All are placed into ThreadGcStatics since thread statics
            // are typically rare.
            Debug.Assert(layout.ThreadNonGcStatics.Size == LayoutInt.Zero);
        }
    }
}
