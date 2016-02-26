// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Internal.TypeSystem;

namespace ILCompiler
{
    internal class CompilerMetadataFieldLayoutAlgorithm : MetadataFieldLayoutAlgorithm
    {
        protected override void PrepareRuntimeSpecificStaticFieldLayout(TypeSystemContext context, ref ComputedStaticFieldLayout layout)
        {
            // Thread/GC statics start with a pointer to the "EEType" that signals the size and GCDesc to the GC
            layout.GcStatics.Size = context.Target.PointerSize;
            layout.ThreadStatics.Size = context.Target.PointerSize;
        }

        protected override void FinalizeRuntimeSpecificStaticFieldLayout(TypeSystemContext context, ref ComputedStaticFieldLayout layout)
        {
            // If the size of GC/Thread Statics is equal to the size set in PrepareRuntimeSpecificStaticFieldLayout, we
            // don't have any GC/Thread statics
            if (layout.GcStatics.Size == context.Target.PointerSize)
            {
                layout.GcStatics.Size = 0;
            }

            if (layout.ThreadStatics.Size == context.Target.PointerSize)
            {
                layout.ThreadStatics.Size = 0;
            }
        }
    }
}
