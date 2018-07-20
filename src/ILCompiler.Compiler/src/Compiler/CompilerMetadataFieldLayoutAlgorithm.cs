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
        private const int DomainLocalModuleDataBlobOffset = 0x30;

        private const int ThreadLocalModuleDataBlobOffset = 0x18;

        private bool _isReadyToRunCodegen;

        private LayoutInt _initialNonGcStaticsOffset;

        public CompilerMetadataFieldLayoutAlgorithm()
        {
            _isReadyToRunCodegen = false;
            _initialNonGcStaticsOffset = LayoutInt.Zero;
        }

        public void SetReadyToRunMode(int numberOfTypesInModule)
        {
            _isReadyToRunCodegen = true;
            _initialNonGcStaticsOffset = new LayoutInt(DomainLocalModuleDataBlobOffset + numberOfTypesInModule);
        }

        protected override void PrepareRuntimeSpecificStaticFieldLayout(TypeSystemContext context, ref ComputedStaticFieldLayout layout)
        {
            if (_isReadyToRunCodegen)
            {
                layout.NonGcStatics.Size = _initialNonGcStaticsOffset;
                layout.GcStatics.Size = LayoutInt.Zero;
                layout.ThreadStatics.Size = LayoutInt.Zero;
            }
            else
            {
                // GC statics start with a pointer to the "EEType" that signals the size and GCDesc to the GC
                layout.GcStatics.Size = context.Target.LayoutPointerSize;
                layout.ThreadStatics.Size = context.Target.LayoutPointerSize;
            }
        }

        protected override void FinalizeRuntimeSpecificStaticFieldLayout(TypeSystemContext context, ref ComputedStaticFieldLayout layout)
        {
            if (layout.NonGcStatics.Size == _initialNonGcStaticsOffset)
            {
                // No non-GC statics, set statics size to 0
                layout.NonGcStatics.Size = LayoutInt.Zero;
            }
            // If the size of GCStatics is equal to the size set in PrepareRuntimeSpecificStaticFieldLayout, we
            // don't have any GC statics
            if (layout.GcStatics.Size == context.Target.LayoutPointerSize)
            {
                layout.GcStatics.Size = LayoutInt.Zero;
            }
            if (layout.ThreadStatics.Size == context.Target.LayoutPointerSize)
            {
                layout.ThreadStatics.Size = LayoutInt.Zero;
            }
        }
    }
}
